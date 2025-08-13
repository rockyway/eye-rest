using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Network-based meeting detection service using UDP endpoint monitoring
    /// </summary>
    public class NetworkBasedMeetingDetectionService : IMeetingDetectionService
    {
        private readonly INetworkEndpointMonitor _networkMonitor;
        private readonly IProcessMonitor _processMonitor;
        private readonly ILogger<NetworkBasedMeetingDetectionService> _logger;
        private readonly DispatcherTimer _monitoringTimer;
        private readonly object _stateLock = new object();

        private bool _isMonitoring;
        private bool _isMeetingActive;
        private List<MeetingApplication> _detectedMeetings = new();
        private MeetingDetectionSettings _settings = new();
        private readonly Dictionary<int, DateTime> _processDetectionHistory = new();

        public event EventHandler<MeetingStateEventArgs>? MeetingStateChanged;

        public bool IsMeetingActive => _isMeetingActive;
        public List<MeetingApplication> DetectedMeetings => _detectedMeetings.ToList();
        public IReadOnlyList<MeetingApplication> ActiveMeetings => _detectedMeetings.AsReadOnly();
        
        public MeetingDetectionSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value ?? new MeetingDetectionSettings();
                UpdateMonitoringInterval();
            }
        }

        public NetworkBasedMeetingDetectionService(
            INetworkEndpointMonitor networkMonitor,
            IProcessMonitor processMonitor,
            ILogger<NetworkBasedMeetingDetectionService> logger)
        {
            _networkMonitor = networkMonitor;
            _processMonitor = processMonitor;
            _logger = logger;

            // Initialize monitoring timer
            _monitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.NetworkPollingIntervalSeconds)
            };
            _monitoringTimer.Tick += OnMonitoringTimerTick;
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Network-based meeting detection is already started");
                return;
            }

            try
            {
                // Check if network monitoring is available
                if (!_networkMonitor.IsNetworkMonitoringAvailable)
                {
                    throw new NotSupportedException("Network monitoring is not available on this system. This may require elevated permissions.");
                }

                if (!await _networkMonitor.TestNetworkAccessAsync())
                {
                    throw new UnauthorizedAccessException("Unable to access network information. Try running as administrator.");
                }

                _logger.LogInformation("🌐 Starting network-based meeting detection monitoring");
                
                _monitoringTimer.Start();
                _isMonitoring = true;
                
                // Perform initial scan
                await RefreshMeetingStateAsync();
                
                _logger.LogInformation("🌐 Network-based meeting detection monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start network-based meeting detection monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Network-based meeting detection is not started");
                return;
            }

            try
            {
                _logger.LogInformation("⏹️ Stopping network-based meeting detection monitoring");
                
                _monitoringTimer.Stop();
                _isMonitoring = false;
                
                // Clear current state
                lock (_stateLock)
                {
                    if (_isMeetingActive)
                    {
                        _isMeetingActive = false;
                        _detectedMeetings.Clear();
                        
                        // Notify state change
                        var eventArgs = new MeetingStateEventArgs
                        {
                            IsMeetingActive = false,
                            ActiveMeetings = new List<MeetingApplication>(),
                            StateChangedAt = DateTime.Now,
                            Reason = "Network monitoring stopped"
                        };
                        
                        MeetingStateChanged?.Invoke(this, eventArgs);
                    }
                }
                
                _processDetectionHistory.Clear();
                
                _logger.LogInformation("⏹️ Network-based meeting detection monitoring stopped successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping network-based meeting detection monitoring");
                throw;
            }
        }

        public async Task RefreshMeetingStateAsync()
        {
            try
            {
                var activeMeetings = await ScanForActiveMeetingsAsync();
                var isMeetingActive = activeMeetings.Any();
                
                lock (_stateLock)
                {
                    var stateChanged = _isMeetingActive != isMeetingActive;
                    
                    if (stateChanged)
                    {
                        _logger.LogInformation($"🌐 Network-based meeting state changed: {_isMeetingActive} → {isMeetingActive} ({activeMeetings.Count} meetings detected via network activity)");
                        
                        _isMeetingActive = isMeetingActive;
                        _detectedMeetings = activeMeetings;
                        
                        // Notify state change
                        var eventArgs = new MeetingStateEventArgs
                        {
                            IsMeetingActive = isMeetingActive,
                            ActiveMeetings = activeMeetings,
                            StateChangedAt = DateTime.Now,
                            Reason = isMeetingActive ? "Network activity detected" : "Network activity ended"
                        };
                        
                        MeetingStateChanged?.Invoke(this, eventArgs);
                    }
                    else
                    {
                        // Update detected meetings even if state didn't change
                        _detectedMeetings = activeMeetings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing network-based meeting state");
            }
        }

        private async Task<List<MeetingApplication>> ScanForActiveMeetingsAsync()
        {
            var activeMeetings = new List<MeetingApplication>();
            
            try
            {
                // Get all Teams processes
                var teamsProcesses = await _processMonitor.GetTeamsProcessesAsync();
                
                if (!teamsProcesses.Any())
                {
                    _logger.LogDebug("No Teams processes found for network monitoring");
                    return activeMeetings;
                }

                _logger.LogDebug($"Checking network activity for {teamsProcesses.Count} Teams processes");

                foreach (var process in teamsProcesses)
                {
                    try
                    {
                        // Check if this process has active network connections
                        bool hasNetworkActivity = await _networkMonitor.HasActiveNetworkConnectionsAsync(process.ProcessId);
                        
                        if (hasNetworkActivity)
                        {
                            _logger.LogInformation($"🌐 Network meeting activity detected for process: {process.ProcessName} (PID: {process.ProcessId})");
                            
                            // Record detection time
                            _processDetectionHistory[process.ProcessId] = DateTime.Now;
                            
                            // Create meeting application entry
                            var meetingApp = new MeetingApplication
                            {
                                ProcessName = process.ProcessName,
                                WindowTitle = process.WindowTitle,
                                StartTime = _processDetectionHistory.GetValueOrDefault(process.ProcessId, DateTime.Now),
                                Type = MapTeamsVersionToMeetingType(process.Version),
                                ProcessId = process.ProcessId,
                                IsInCall = true,
                                MeetingId = ExtractMeetingIdFromNetwork(process),
                                IsActive = true
                            };

                            activeMeetings.Add(meetingApp);
                            
                            if (_settings.LogDetectionActivity)
                            {
                                _logger.LogInformation($"🎯 NETWORK MEETING DETECTED: {meetingApp.ProcessName} - Network activity indicates active meeting");
                            }
                        }
                        else
                        {
                            // Remove from detection history if no activity
                            _processDetectionHistory.Remove(process.ProcessId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace($"Could not analyze network activity for process {process.ProcessName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for active meetings via network monitoring");
            }
            
            return activeMeetings;
        }

        private MeetingType MapTeamsVersionToMeetingType(TeamsVersion version)
        {
            return version switch
            {
                TeamsVersion.Classic => MeetingType.Teams,
                TeamsVersion.NewClient => MeetingType.Teams,
                TeamsVersion.WebView2 => MeetingType.Teams,
                _ => MeetingType.Teams
            };
        }

        private string ExtractMeetingIdFromNetwork(TeamsProcess process)
        {
            // For network-based detection, we can use process info + timestamp
            return $"network-{process.ProcessId}-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private void OnMonitoringTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _ = Task.Run(async () => await RefreshMeetingStateAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in network monitoring timer tick");
            }
        }

        private void UpdateMonitoringInterval()
        {
            if (_monitoringTimer != null)
            {
                _monitoringTimer.Interval = TimeSpan.FromSeconds(_settings.NetworkPollingIntervalSeconds);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isMonitoring)
                {
                    StopMonitoringAsync().Wait(TimeSpan.FromSeconds(5));
                }
                
                _monitoringTimer?.Stop();
                _processDetectionHistory.Clear();
                _logger.LogInformation("NetworkBasedMeetingDetectionService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing NetworkBasedMeetingDetectionService");
            }
        }
    }
}