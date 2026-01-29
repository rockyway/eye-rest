using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Hybrid meeting detection service that combines window and network-based detection
    /// </summary>
    public class HybridMeetingDetectionService : IMeetingDetectionService
    {
        private readonly WindowBasedMeetingDetectionService _windowDetection;
        private readonly NetworkBasedMeetingDetectionService _networkDetection;
        private readonly ILogger<HybridMeetingDetectionService> _logger;
        private readonly object _stateLock = new object();

        private bool _isMonitoring;
        private bool _isMeetingActive;
        private List<MeetingApplication> _detectedMeetings = new();
        private MeetingDetectionSettings _settings = new();

        // Track which detection methods are available
        private bool _windowDetectionAvailable = true;
        private bool _networkDetectionAvailable = false;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future network-primary mode
        private bool _useNetworkAsPrimary = true;
#pragma warning restore CS0414

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
                
                // Update settings for both detection services
                if (_windowDetection != null)
                    _windowDetection.Settings = _settings;
                
                if (_networkDetection != null)
                    _networkDetection.Settings = _settings;
            }
        }

        public HybridMeetingDetectionService(
            WindowBasedMeetingDetectionService windowDetection,
            NetworkBasedMeetingDetectionService networkDetection,
            ILogger<HybridMeetingDetectionService> logger)
        {
            _windowDetection = windowDetection;
            _networkDetection = networkDetection;
            _logger = logger;

            // Subscribe to events from both services
            _windowDetection.MeetingStateChanged += OnWindowDetectionStateChanged;
            _networkDetection.MeetingStateChanged += OnNetworkDetectionStateChanged;
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Hybrid meeting detection is already started");
                return;
            }

            try
            {
                _logger.LogInformation("🔀 Starting hybrid meeting detection monitoring");

                // Test availability of both methods
                await TestDetectionAvailabilityAsync();

                // Start available detection methods
                var startTasks = new List<Task>();

                if (_windowDetectionAvailable)
                {
                    startTasks.Add(StartWindowDetectionSafelyAsync());
                }

                if (_networkDetectionAvailable)
                {
                    startTasks.Add(StartNetworkDetectionSafelyAsync());
                }

                if (!startTasks.Any())
                {
                    throw new InvalidOperationException("No detection methods are available");
                }

                // Start both methods (those that are available)
                await Task.WhenAll(startTasks);

                _isMonitoring = true;
                
                _logger.LogInformation($"🔀 Hybrid meeting detection started successfully (Window: {_windowDetectionAvailable}, Network: {_networkDetectionAvailable})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start hybrid meeting detection monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Hybrid meeting detection is not started");
                return;
            }

            try
            {
                _logger.LogInformation("⏹️ Stopping hybrid meeting detection monitoring");

                var stopTasks = new List<Task>();

                if (_windowDetectionAvailable)
                {
                    stopTasks.Add(_windowDetection.StopMonitoringAsync());
                }

                if (_networkDetectionAvailable)
                {
                    stopTasks.Add(_networkDetection.StopMonitoringAsync());
                }

                await Task.WhenAll(stopTasks);

                _isMonitoring = false;

                // Clear current state
                lock (_stateLock)
                {
                    if (_isMeetingActive)
                    {
                        _isMeetingActive = false;
                        _detectedMeetings.Clear();
                        
                        var eventArgs = new MeetingStateEventArgs
                        {
                            IsMeetingActive = false,
                            ActiveMeetings = new List<MeetingApplication>(),
                            StateChangedAt = DateTime.Now,
                            Reason = "Hybrid monitoring stopped"
                        };
                        
                        MeetingStateChanged?.Invoke(this, eventArgs);
                    }
                }

                _logger.LogInformation("⏹️ Hybrid meeting detection monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping hybrid meeting detection monitoring");
                throw;
            }
        }

        public async Task RefreshMeetingStateAsync()
        {
            try
            {
                // Refresh both detection methods
                var refreshTasks = new List<Task>();

                if (_windowDetectionAvailable)
                {
                    refreshTasks.Add(_windowDetection.RefreshMeetingStateAsync());
                }

                if (_networkDetectionAvailable)
                {
                    refreshTasks.Add(_networkDetection.RefreshMeetingStateAsync());
                }

                await Task.WhenAll(refreshTasks);

                // The state will be updated via the event handlers
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing hybrid meeting state");
            }
        }

        private async Task TestDetectionAvailabilityAsync()
        {
            try
            {
                // Test window detection (should always work)
                _windowDetectionAvailable = true;

                // Test network detection
                try
                {
                    _networkDetectionAvailable = await _networkDetection.StartMonitoringAsync().ContinueWith(t => 
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            // Stop it immediately, we just wanted to test
                            _ = _networkDetection.StopMonitoringAsync();
                            return true;
                        }
                        return false;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Network detection not available, will use window detection only");
                    _networkDetectionAvailable = false;
                }

                _logger.LogInformation($"Detection availability: Window={_windowDetectionAvailable}, Network={_networkDetectionAvailable}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing detection availability");
                _windowDetectionAvailable = true; // Fallback to window detection
                _networkDetectionAvailable = false;
            }
        }

        private async Task StartWindowDetectionSafelyAsync()
        {
            try
            {
                await _windowDetection.StartMonitoringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start window detection");
                _windowDetectionAvailable = false;
            }
        }

        private async Task StartNetworkDetectionSafelyAsync()
        {
            try
            {
                await _networkDetection.StartMonitoringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start network detection, continuing with window detection only");
                _networkDetectionAvailable = false;
            }
        }

        private void OnWindowDetectionStateChanged(object? sender, MeetingStateEventArgs e)
        {
            try
            {
                _logger.LogDebug($"Window detection state changed: {e.IsMeetingActive}");
                UpdateHybridState("window");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling window detection state change");
            }
        }

        private void OnNetworkDetectionStateChanged(object? sender, MeetingStateEventArgs e)
        {
            try
            {
                _logger.LogDebug($"Network detection state changed: {e.IsMeetingActive}");
                UpdateHybridState("network");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling network detection state change");
            }
        }

        private void UpdateHybridState(string source)
        {
            lock (_stateLock)
            {
                try
                {
                    // Combine results from both detection methods
                    var allMeetings = new List<MeetingApplication>();

                    if (_windowDetectionAvailable && _windowDetection.IsMeetingActive)
                    {
                        allMeetings.AddRange(_windowDetection.DetectedMeetings);
                    }

                    if (_networkDetectionAvailable && _networkDetection.IsMeetingActive)
                    {
                        allMeetings.AddRange(_networkDetection.DetectedMeetings);
                    }

                    // Remove duplicates (prefer network detection results if both detect the same process)
                    var uniqueMeetings = allMeetings
                        .GroupBy(m => m.ProcessId)
                        .Select(g => g.OrderByDescending(m => m.IsInCall).First())
                        .ToList();

                    var newMeetingState = uniqueMeetings.Any();
                    var stateChanged = _isMeetingActive != newMeetingState;

                    if (stateChanged)
                    {
                        _logger.LogInformation($"🔀 Hybrid meeting state changed: {_isMeetingActive} → {newMeetingState} (triggered by {source}, {uniqueMeetings.Count} unique meetings)");

                        _isMeetingActive = newMeetingState;
                        _detectedMeetings = uniqueMeetings;

                        var eventArgs = new MeetingStateEventArgs
                        {
                            IsMeetingActive = newMeetingState,
                            ActiveMeetings = uniqueMeetings,
                            StateChangedAt = DateTime.Now,
                            Reason = newMeetingState ? $"Meeting detected via {source}" : $"Meeting ended via {source}"
                        };

                        MeetingStateChanged?.Invoke(this, eventArgs);
                    }
                    else
                    {
                        // Update meetings even if state didn't change
                        _detectedMeetings = uniqueMeetings;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating hybrid state");
                }
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

                // Unsubscribe from events
                if (_windowDetection != null)
                {
                    _windowDetection.MeetingStateChanged -= OnWindowDetectionStateChanged;
                }

                if (_networkDetection != null)
                {
                    _networkDetection.MeetingStateChanged -= OnNetworkDetectionStateChanged;
                }

                _logger.LogInformation("HybridMeetingDetectionService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing HybridMeetingDetectionService");
            }
        }
    }
}