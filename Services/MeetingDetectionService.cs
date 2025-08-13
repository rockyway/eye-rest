using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class MeetingDetectionService : IMeetingDetectionService
    {
        private readonly ILogger<MeetingDetectionService> _logger;
        private readonly DispatcherTimer _monitoringTimer;
        private readonly object _stateLock = new object();
        
        private bool _isMonitoring;
        private bool _isMeetingActive;
        private List<MeetingApplication> _detectedMeetings = new();
        private MeetingDetectionSettings _settings = new();
        
        public IReadOnlyList<MeetingApplication> ActiveMeetings => _detectedMeetings.AsReadOnly();
        
        // Meeting detection patterns
        private readonly Dictionary<MeetingType, MeetingDetectionPattern> _detectionPatterns = new()
        {
            {
                MeetingType.Teams, new MeetingDetectionPattern
                {
                    // ENHANCED: Updated Teams process names for both classic and new Teams (including WebView2)
                    ProcessNames = new[] { "ms-teams", "teams", "msteams", "teams2", "msteamsupdate", "msedgewebview2" },
                    // ENHANCED: More comprehensive window title patterns (including "Meeting with" pattern)
                    WindowTitlePatterns = new[] { "Microsoft Teams", "Teams Meeting", "| Microsoft Teams", "| Teams", "Meeting in", "Meeting with", "Calendar | Microsoft Teams", "WebView2: Microsoft Teams" },
                    // ENHANCED: Additional call indicators for better detection
                    CallIndicators = new[] { "Meeting", "Call", "Calling", "In a call", "Connected", "Join", "Present", "Share", "Mute", "Unmute", "People", "Chat" }
                }
            },
            {
                MeetingType.Zoom, new MeetingDetectionPattern
                {
                    ProcessNames = new[] { "zoom", "zoomwebinar" },
                    WindowTitlePatterns = new[] { "Zoom Meeting", "Zoom Webinar", "Zoom Cloud Meetings" },
                    CallIndicators = new[] { "Meeting", "Webinar", "You are muted", "Participants" }
                }
            },
            {
                MeetingType.Webex, new MeetingDetectionPattern
                {
                    ProcessNames = new[] { "ciscowebex", "webexmta", "webex" },
                    WindowTitlePatterns = new[] { "Cisco Webex", "Webex Meeting", "Webex Events" },
                    CallIndicators = new[] { "Meeting", "Mute", "Video", "Share Screen" }
                }
            },
            {
                MeetingType.GoogleMeet, new MeetingDetectionPattern
                {
                    ProcessNames = new[] { "chrome", "msedge", "firefox" },
                    WindowTitlePatterns = new[] { "Google Meet", "meet.google.com" },
                    CallIndicators = new[] { "Meet", "Join", "Camera", "Microphone" }
                }
            },
            {
                MeetingType.Skype, new MeetingDetectionPattern
                {
                    ProcessNames = new[] { "skype", "lync" },
                    WindowTitlePatterns = new[] { "Skype", "Microsoft Lync", "Skype for Business" },
                    CallIndicators = new[] { "Call", "Video call", "Audio call", "Conference" }
                }
            }
        };

        public event EventHandler<MeetingStateEventArgs>? MeetingStateChanged;

        public bool IsMeetingActive => _isMeetingActive;
        public List<MeetingApplication> DetectedMeetings => _detectedMeetings.ToList();
        
        public MeetingDetectionSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value ?? new MeetingDetectionSettings();
                UpdateMonitoringInterval();
            }
        }

        public MeetingDetectionService(ILogger<MeetingDetectionService> logger)
        {
            _logger = logger;
            
            // Initialize monitoring timer
            _monitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_settings.MonitoringIntervalSeconds)
            };
            _monitoringTimer.Tick += OnMonitoringTimerTick;
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Meeting detection is already started");
                return;
            }

            try
            {
                _logger.LogInformation("🎥 Starting meeting detection monitoring");
                
                _monitoringTimer.Start();
                _isMonitoring = true;
                
                // Perform initial scan
                await RefreshMeetingStateAsync();
                
                _logger.LogInformation("🎥 Meeting detection monitoring started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start meeting detection monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Meeting detection is not started");
                return;
            }

            try
            {
                _logger.LogInformation("⏹️ Stopping meeting detection monitoring");
                
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
                            Reason = "Monitoring stopped"
                        };
                        
                        MeetingStateChanged?.Invoke(this, eventArgs);
                    }
                }
                
                _logger.LogInformation("⏹️ Meeting detection monitoring stopped successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping meeting detection monitoring");
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
                        _logger.LogInformation($"🎥 Meeting state changed: {_isMeetingActive} → {isMeetingActive} ({activeMeetings.Count} meetings detected)");
                        
                        _isMeetingActive = isMeetingActive;
                        _detectedMeetings = activeMeetings;
                        
                        // Notify state change
                        var eventArgs = new MeetingStateEventArgs
                        {
                            IsMeetingActive = isMeetingActive,
                            ActiveMeetings = activeMeetings,
                            StateChangedAt = DateTime.Now,
                            Reason = isMeetingActive ? "Meeting detected" : "Meeting ended"
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
                _logger.LogError(ex, "Error refreshing meeting state");
            }
        }

        private async Task<List<MeetingApplication>> ScanForActiveMeetingsAsync()
        {
            var activeMeetings = new List<MeetingApplication>();
            
            try
            {
                var processes = Process.GetProcesses();
                
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.HasExited) continue;
                        
                        var meetingApp = AnalyzeProcess(process);
                        if (meetingApp != null)
                        {
                            activeMeetings.Add(meetingApp);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip processes we can't access (permission issues, etc.)
                        _logger.LogTrace($"Could not analyze process {process.ProcessName}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for active meetings");
            }
            
            return activeMeetings;
        }

        private MeetingApplication? AnalyzeProcess(Process process)
        {
            try
            {
                var processName = process.ProcessName.ToLowerInvariant();
                var windowTitle = GetWindowTitle(process.MainWindowHandle);
                
                // ENHANCED: Debug logging for Teams processes (including WebView2)
                if (processName.Contains("teams") || processName.Contains("msteams") || processName.Contains("msedgewebview2"))
                {
                    _logger.LogDebug($"🔍 Found Teams-related process: {process.ProcessName} (PID: {process.Id}), Window: '{windowTitle}'");
                    
                    // Extra logging for meeting detection
                    if (windowTitle.Contains("Meeting with", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"🎯 MEETING DETECTED: Found 'Meeting with' in window title: '{windowTitle}'");
                    }
                }
                
                // Check each detection pattern
                foreach (var (meetingType, pattern) in _detectionPatterns)
                {
                    if (!IsMeetingTypeEnabled(meetingType)) continue;
                    
                    // Check if process name matches
                    var processMatches = pattern.ProcessNames.Any(name => 
                        processName.Contains(name.ToLowerInvariant()));
                    
                    if (!processMatches) continue;
                    
                    // Check window title patterns
                    var titleMatches = pattern.WindowTitlePatterns.Any(titlePattern =>
                        windowTitle.Contains(titlePattern, StringComparison.OrdinalIgnoreCase));
                    
                    // ENHANCED: For Teams, be more lenient - if process matches, consider it a potential meeting
                    if (!titleMatches && meetingType != MeetingType.GoogleMeet && meetingType != MeetingType.Teams) continue;
                    
                    // For Google Meet, need additional validation since it runs in browser
                    if (meetingType == MeetingType.GoogleMeet)
                    {
                        if (!windowTitle.Contains("meet.google.com", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    
                    // ENHANCED: Special handling for msedgewebview2 - only consider it Teams if window title matches
                    if (processName == "msedgewebview2" && meetingType == MeetingType.Teams)
                    {
                        // WebView2 is used by many apps, so require Teams-related text in the window title
                        // Check for "Meeting with", "WebView2: Microsoft Teams", or other Teams indicators
                        bool isTeamsWebView = windowTitle.Contains("Teams", StringComparison.OrdinalIgnoreCase) ||
                                             windowTitle.Contains("Meeting with", StringComparison.OrdinalIgnoreCase) ||
                                             windowTitle.Contains("WebView2: Microsoft Teams", StringComparison.OrdinalIgnoreCase);
                        if (!isTeamsWebView)
                            continue;
                    }
                    
                    // ENHANCED: For Teams, if process is running and has a window, assume it might be a meeting
                    var isInCall = false;
                    if (meetingType == MeetingType.Teams && processMatches)
                    {
                        // Special case: If we see "Meeting with" in any window title, it's definitely a meeting
                        if (windowTitle.Contains("Meeting with", StringComparison.OrdinalIgnoreCase))
                        {
                            isInCall = true;
                        }
                        else
                        {
                            // For Teams, consider it a meeting if ANY call indicator is found OR if window is visible
                            isInCall = !string.IsNullOrEmpty(windowTitle) && 
                                       (pattern.CallIndicators.Any(indicator =>
                                           windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase)) ||
                                        titleMatches); // If title matches Teams patterns, likely in a meeting
                        }
                    }
                    else
                    {
                        // Check for call indicators in window title for other apps
                        isInCall = pattern.CallIndicators.Any(indicator =>
                            windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // Check if window title is in excluded list
                    if (_settings.ExcludedWindowTitles.Any(excluded =>
                        windowTitle.Contains(excluded, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    
                    return new MeetingApplication
                    {
                        ProcessName = process.ProcessName,
                        WindowTitle = windowTitle,
                        StartTime = GetProcessStartTime(process),
                        Type = meetingType,
                        ProcessId = process.Id,
                        IsInCall = isInCall,
                        MeetingId = ExtractMeetingId(windowTitle, meetingType)
                    };
                }
                
                // Check custom process names
                foreach (var customProcess in _settings.CustomProcessNames)
                {
                    if (processName.Contains(customProcess.ToLowerInvariant()))
                    {
                        return new MeetingApplication
                        {
                            ProcessName = process.ProcessName,
                            WindowTitle = windowTitle,
                            StartTime = GetProcessStartTime(process),
                            Type = MeetingType.Unknown,
                            ProcessId = process.Id,
                            IsInCall = true,
                            MeetingId = "custom"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Error analyzing process {process.ProcessName}: {ex.Message}");
            }
            
            return null;
        }

        private bool IsMeetingTypeEnabled(MeetingType meetingType)
        {
            return meetingType switch
            {
                MeetingType.Teams => _settings.EnableTeamsDetection,
                MeetingType.Zoom => _settings.EnableZoomDetection,
                MeetingType.Webex => _settings.EnableWebexDetection,
                MeetingType.GoogleMeet => _settings.EnableGoogleMeetDetection,
                MeetingType.Skype => _settings.EnableSkypeDetection,
                _ => true
            };
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return string.Empty;
                
                var titleLength = GetWindowTextLength(windowHandle);
                if (titleLength == 0) return string.Empty;
                
                var title = new StringBuilder(titleLength + 1);
                GetWindowText(windowHandle, title, title.Capacity);
                
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private DateTime GetProcessStartTime(Process process)
        {
            try
            {
                return process.StartTime;
            }
            catch
            {
                return DateTime.Now;
            }
        }

        private string ExtractMeetingId(string windowTitle, MeetingType meetingType)
        {
            try
            {
                return meetingType switch
                {
                    MeetingType.Teams => ExtractTeamsMeetingId(windowTitle),
                    MeetingType.Zoom => ExtractZoomMeetingId(windowTitle),
                    MeetingType.Webex => ExtractWebexMeetingId(windowTitle),
                    MeetingType.GoogleMeet => ExtractGoogleMeetId(windowTitle),
                    _ => string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ExtractTeamsMeetingId(string windowTitle)
        {
            // Teams usually shows meeting names or participant info
            var parts = windowTitle.Split('|', '-');
            return parts.Length > 0 ? parts[0].Trim() : string.Empty;
        }

        private string ExtractZoomMeetingId(string windowTitle)
        {
            // Zoom shows meeting ID or name in window title
            if (windowTitle.Contains("Meeting ID:"))
            {
                var start = windowTitle.IndexOf("Meeting ID:") + 11;
                var end = windowTitle.IndexOf(' ', start);
                if (end == -1) end = windowTitle.Length;
                return windowTitle.Substring(start, end - start).Trim();
            }
            return string.Empty;
        }

        private string ExtractWebexMeetingId(string windowTitle)
        {
            // Webex shows meeting name or number
            var parts = windowTitle.Split('-', '|');
            return parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }

        private string ExtractGoogleMeetId(string windowTitle)
        {
            // Google Meet shows meet code in URL
            if (windowTitle.Contains("meet.google.com/"))
            {
                var start = windowTitle.IndexOf("meet.google.com/") + 16;
                var end = windowTitle.IndexOf(' ', start);
                if (end == -1) end = windowTitle.Length;
                return windowTitle.Substring(start, Math.Min(12, end - start)).Trim();
            }
            return string.Empty;
        }

        private void OnMonitoringTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _ = Task.Run(async () => await RefreshMeetingStateAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring timer tick");
            }
        }

        private void UpdateMonitoringInterval()
        {
            if (_monitoringTimer != null)
            {
                _monitoringTimer.Interval = TimeSpan.FromSeconds(_settings.MonitoringIntervalSeconds);
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
                _logger.LogInformation("MeetingDetectionService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MeetingDetectionService");
            }
        }

        #region Windows API Declarations

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        #endregion

        #region Helper Classes

        private class MeetingDetectionPattern
        {
            public string[] ProcessNames { get; set; } = Array.Empty<string>();
            public string[] WindowTitlePatterns { get; set; } = Array.Empty<string>();
            public string[] CallIndicators { get; set; } = Array.Empty<string>();
        }

        #endregion
    }
}