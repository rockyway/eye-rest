using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class SystemTrayService : ISystemTrayService, IDisposable
    {
        private readonly ILogger<SystemTrayService> _logger;
        private readonly IconService _iconService;
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _pauseMenuItem;
        private ToolStripMenuItem? _resumeMenuItem;
        private ToolStripMenuItem? _pauseForMeetingMenuItem; // NEW: Manual meeting pause
        private ToolStripMenuItem? _timerStatusMenuItem;
        private ToolStripMenuItem? _meetingModeMenuItem;
        private ToolStripMenuItem? _analyticsMenuItem;
        
        private bool _isInMeetingMode;
        private string _meetingType = string.Empty;
        private string _currentTimerStatus = "Ready";
        
        // ENHANCED: Timer details for tooltip
        private TimeSpan _eyeRestRemaining = TimeSpan.Zero;
        private TimeSpan _breakRemaining = TimeSpan.Zero;
        private DateTime _lastTimerUpdate = DateTime.Now;

        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? PauseTimersRequested;
        public event EventHandler? ResumeTimersRequested;
        public event EventHandler? PauseForMeetingRequested; // NEW: Manual pause for meeting
        public event EventHandler? ShowTimerStatusRequested;
        public event EventHandler? ShowAnalyticsRequested;

        public SystemTrayService(ILogger<SystemTrayService> logger, IconService iconService)
        {
            _logger = logger;
            _iconService = iconService;
        }

        public void Initialize()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.DoubleClick += (s, e) => RestoreRequested?.Invoke(this, e);

            CreateContextMenu();
            _notifyIcon.ContextMenuStrip = _contextMenu;

            _logger.LogInformation("🎛️ Enhanced system tray service initialized with advanced controls");
        }

        public void ShowTrayIcon()
        {
            if (_notifyIcon == null) return;
            try
            {
                // ENHANCED: Use active state icon when first showing
                _notifyIcon.Icon = _iconService.GetIconForState(TrayIconState.Active);
                _notifyIcon.Text = "EyeRest Application";
                _notifyIcon.Visible = true;
                _logger.LogInformation("System tray icon is now visible with Active state.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show the system tray icon.");
            }
        }

        public void HideTrayIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _logger.LogInformation("System tray icon hidden.");
            }
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (_notifyIcon == null) return;
            
            // ENHANCED: Update icon visual based on state
            try
            {
                var stateIcon = _iconService.GetIconForState(state);
                _notifyIcon.Icon = stateIcon;
                _logger.LogDebug($"🎨 Icon visual updated for state: {state}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update icon for state {state}, using default icon");
                _notifyIcon.Icon = _iconService.GetApplicationIcon();
            }
            
            var displayText = GetDisplayTextForState(state);
            var tooltipText = GetTooltipTextForState(state);
            
            _notifyIcon.Text = tooltipText;
            UpdateContextMenuForState(state);
            
            _logger.LogInformation($"🎛️ Tray icon updated: {displayText}");
        }

        public void ShowBalloonTip(string title, string text)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(5000, title, text, ToolTipIcon.Info);
                _logger.LogInformation($"💬 Showing balloon tip: {title}");
            }
        }
        
        public void UpdateTimerStatus(string status)
        {
            _currentTimerStatus = status;
            
            if (_timerStatusMenuItem != null)
            {
                _timerStatusMenuItem.Text = $"Status: {status}";
            }
            
            // Update main tooltip with timer details
            UpdateTooltipWithTimerDetails();
            
            _logger.LogDebug($"🎛️ Timer status updated: {status}");
        }
        
        /// <summary>
        /// ENHANCED: Update timer details for tooltip display
        /// </summary>
        public void UpdateTimerDetails(TimeSpan eyeRestRemaining, TimeSpan breakRemaining)
        {
            _eyeRestRemaining = eyeRestRemaining;
            _breakRemaining = breakRemaining;
            _lastTimerUpdate = DateTime.Now;
            
            // Update tooltip with new timer information
            UpdateTooltipWithTimerDetails();
            
            _logger.LogDebug($"🎛️ Timer details updated - Eye rest: {FormatTimeSpan(eyeRestRemaining)}, Break: {FormatTimeSpan(breakRemaining)}");
        }
        
        public void SetMeetingMode(bool isInMeeting, string meetingType = "")
        {
            _isInMeetingMode = isInMeeting;
            _meetingType = meetingType;
            
            if (_meetingModeMenuItem != null)
            {
                if (isInMeeting)
                {
                    _meetingModeMenuItem.Text = $"📹 In Meeting ({meetingType})";
                    _meetingModeMenuItem.Enabled = false; // Show as disabled to indicate active state
                    // Icon update now handled by ApplicationOrchestrator calling UpdateTrayIcon(TrayIconState.MeetingMode)
                }
                else
                {
                    _meetingModeMenuItem.Text = "📹 Meeting Mode: Off";
                    _meetingModeMenuItem.Enabled = true;
                    // Icon update now handled by ApplicationOrchestrator calling UpdateTrayIcon(TrayIconState.Active)
                }
            }
            
            _logger.LogInformation($"🎥 Meeting mode {(isInMeeting ? "activated" : "deactivated")} - Type: {meetingType}");
        }


        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            
            // Timer Status (read-only)
            _timerStatusMenuItem = new ToolStripMenuItem($"Status: {_currentTimerStatus}")
            {
                Enabled = false // Read-only item
            };
            _contextMenu.Items.Add(_timerStatusMenuItem);
            
            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());
            
            // Timer Controls
            _pauseMenuItem = new ToolStripMenuItem("⏸️ Pause Timers", null, OnPauseTimers);
            _resumeMenuItem = new ToolStripMenuItem("▶️ Resume Timers", null, OnResumeTimers) { Enabled = false };
            _pauseForMeetingMenuItem = new ToolStripMenuItem("🎥 Pause for Meeting (30 min)", null, OnPauseForMeeting); // NEW
            
            _contextMenu.Items.Add(_pauseMenuItem);
            _contextMenu.Items.Add(_resumeMenuItem);
            _contextMenu.Items.Add(_pauseForMeetingMenuItem); // NEW
            
            // Meeting Mode Indicator
            _meetingModeMenuItem = new ToolStripMenuItem("📹 Meeting Mode: Off") { Enabled = false };
            _contextMenu.Items.Add(_meetingModeMenuItem);
            
            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());
            
            // Analytics
            _analyticsMenuItem = new ToolStripMenuItem("📊 View Analytics", null, OnShowAnalytics);
            _contextMenu.Items.Add(_analyticsMenuItem);
            
            // Timer Status Details
            _contextMenu.Items.Add("⏱️ Timer Details", null, OnShowTimerStatus);
            
            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());
            
            // Standard Options
            _contextMenu.Items.Add("🏠 Restore Window", null, (s, e) => RestoreRequested?.Invoke(this, e));
            _contextMenu.Items.Add("❌ Exit", null, OnExit);
        }
        
        private void UpdateContextMenuForState(TrayIconState state)
        {
            if (_pauseMenuItem == null || _resumeMenuItem == null) return;
            
            switch (state)
            {
                case TrayIconState.Active:
                    _pauseMenuItem.Enabled = true;
                    _pauseMenuItem.Text = "⏸️ Pause Timers";
                    _resumeMenuItem.Enabled = false;
                    if (_pauseForMeetingMenuItem != null) _pauseForMeetingMenuItem.Enabled = true;
                    break;
                    
                case TrayIconState.Paused:
                    _pauseMenuItem.Enabled = false;
                    _pauseMenuItem.Text = "⏸️ Paused (Manual)";
                    _resumeMenuItem.Enabled = true;
                    _resumeMenuItem.Text = "▶️ Resume Timers";
                    break;
                    
                case TrayIconState.SmartPaused:
                    _pauseMenuItem.Enabled = false;
                    _pauseMenuItem.Text = "⏸️ Smart Paused (Auto)";
                    _resumeMenuItem.Enabled = true;
                    _resumeMenuItem.Text = "▶️ Force Resume";
                    break;
                    
                case TrayIconState.ManuallyPaused:
                    _pauseMenuItem.Enabled = false;
                    _pauseMenuItem.Text = "⏸️ Meeting Pause (Manual)";
                    _resumeMenuItem.Enabled = true;
                    _resumeMenuItem.Text = "▶️ Resume Timers";
                    if (_pauseForMeetingMenuItem != null) _pauseForMeetingMenuItem.Enabled = false;
                    break;
                    
                case TrayIconState.MeetingMode:
                    _pauseMenuItem.Enabled = false;
                    _pauseMenuItem.Text = "⏸️ Paused (Meeting)";
                    _resumeMenuItem.Enabled = false; // Auto-resume when meeting ends
                    break;
                    
                case TrayIconState.UserAway:
                    _pauseMenuItem.Enabled = false;
                    _pauseMenuItem.Text = "⏸️ Paused (Away)";
                    _resumeMenuItem.Enabled = false; // Auto-resume when user returns
                    break;
                    
                case TrayIconState.Break:
                case TrayIconState.EyeRest:
                    _pauseMenuItem.Enabled = false;
                    _resumeMenuItem.Enabled = false;
                    break;
            }
        }
        
        private string GetDisplayTextForState(TrayIconState state)
        {
            return state switch
            {
                TrayIconState.Active => "Active",
                TrayIconState.Paused => "Paused",
                TrayIconState.SmartPaused => "Smart Paused",
                TrayIconState.ManuallyPaused => "Meeting Pause", // NEW
                TrayIconState.Break => "Break Time",
                TrayIconState.EyeRest => "Eye Rest",
                TrayIconState.MeetingMode => $"Meeting ({_meetingType})",
                TrayIconState.UserAway => "User Away",
                TrayIconState.Error => "Error",
                _ => "Unknown"
            };
        }
        
        private string GetTooltipTextForState(TrayIconState state)
        {
            var baseText = "EyeRest";
            var stateText = GetDisplayTextForState(state);
            
            return state switch
            {
                TrayIconState.MeetingMode => $"{baseText} - Meeting Mode ({_meetingType})",
                TrayIconState.SmartPaused => $"{baseText} - Smart Paused (Auto)",
                TrayIconState.ManuallyPaused => $"{baseText} - Meeting Pause (Manual)", // NEW
                TrayIconState.UserAway => $"{baseText} - User Away (Auto-Paused)",
                _ => $"{baseText} - {stateText}"
            };
        }
        
        /// <summary>
        /// ENHANCED: Update tooltip with detailed timer information
        /// </summary>
        private void UpdateTooltipWithTimerDetails()
        {
            if (_notifyIcon == null) return;
            
            var tooltipText = BuildDetailedTooltip();
            
            // Windows tooltip limit is 127 characters, but we'll keep it concise
            if (tooltipText.Length > 120)
            {
                // Fallback to shorter format if too long
                tooltipText = $"EyeRest - {_currentTimerStatus}\nNext: {FormatTimeSpan(_eyeRestRemaining)} | {FormatTimeSpan(_breakRemaining)}";
            }
            
            _notifyIcon.Text = tooltipText;
            _logger.LogDebug($"🎛️ Tooltip updated: {tooltipText.Replace("\n", " | ")}");
        }
        
        /// <summary>
        /// Build detailed tooltip with timer information and accurate status
        /// </summary>
        private string BuildDetailedTooltip()
        {
            var baseText = "EyeRest";
            
            // Handle special states
            if (_isInMeetingMode)
            {
                return $"{baseText} - Meeting Mode ({_meetingType})\nTimers paused during meeting";
            }
            
            // ENHANCED: Handle manual pause with remaining time
            if (_currentTimerStatus.Contains("Meeting Pause") && _currentTimerStatus.Contains("left"))
            {
                return $"{baseText} - Manual Meeting Pause\n{_currentTimerStatus}\nTimers will auto-resume";
            }
            
            // ENHANCED: Determine accurate status based on timer state
            var actualStatus = GetAccurateTimerStatus();
            
            // Regular timer display
            var eyeRestText = FormatTimeSpan(_eyeRestRemaining);
            var breakText = FormatTimeSpan(_breakRemaining);
            
            // Multi-line tooltip with timer details and accurate status
            return $"{baseText} - {actualStatus}\nNext eye rest: {eyeRestText}\nNext break: {breakText}";
        }
        
        /// <summary>
        /// Get accurate timer status based on actual timer state
        /// </summary>
        private string GetAccurateTimerStatus()
        {
            // If we have valid timer data (non-zero), timers are running
            bool hasActiveTimers = _eyeRestRemaining > TimeSpan.Zero || _breakRemaining > TimeSpan.Zero;
            
            // Special case: if both timers are at exactly 1 second, they're about to trigger
            bool timersAboutToTrigger = _eyeRestRemaining.TotalSeconds <= 1 && _breakRemaining.TotalSeconds <= 1;
            
            if (timersAboutToTrigger && hasActiveTimers)
            {
                return "Ready to trigger";
            }
            else if (hasActiveTimers)
            {
                return "Running";
            }
            else if (_currentTimerStatus.Contains("Paused"))
            {
                return _currentTimerStatus; // Keep paused states as-is
            }
            else
            {
                return "Ready"; // Not yet started or stopped
            }
        }
        
        /// <summary>
        /// Format TimeSpan for display (same logic as MainWindowViewModel)
        /// </summary>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            // CONSISTENT: Use same formatting as MainWindowViewModel
            if (timeSpan.TotalSeconds <= 1)
            {
                return "1s"; // Never show 0s - always show minimum 1s when timer is due
            }
            else if (timeSpan.TotalMinutes < 1)
            {
                return $"{timeSpan.Seconds}s";
            }
            else if (timeSpan.TotalHours < 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
        }
        
        private void OnPauseTimers(object? sender, EventArgs e)
        {
            _logger.LogInformation("🎛️ User requested timer pause from system tray");
            PauseTimersRequested?.Invoke(this, e);
        }
        
        private void OnResumeTimers(object? sender, EventArgs e)
        {
            _logger.LogInformation("🎛️ User requested timer resume from system tray");
            ResumeTimersRequested?.Invoke(this, e);
        }
        
        // NEW: Handle pause for meeting request
        private void OnPauseForMeeting(object? sender, EventArgs e)
        {
            _logger.LogInformation("🎥 User requested 30-minute meeting pause from system tray");
            PauseForMeetingRequested?.Invoke(this, e);
        }
        
        private void OnShowTimerStatus(object? sender, EventArgs e)
        {
            _logger.LogInformation("🎛️ User requested timer status from system tray");
            ShowTimerStatusRequested?.Invoke(this, e);
        }
        
        private void OnShowAnalytics(object? sender, EventArgs e)
        {
            _logger.LogInformation("🎛️ User requested analytics from system tray");
            ShowAnalyticsRequested?.Invoke(this, e);
        }
        
        private void OnExit(object? sender, EventArgs e)
        {
            // Just raise the exit event - confirmation is handled by App.xaml.cs OnExitRequested
            // This avoids showing duplicate confirmation dialogs
            _logger.LogInformation("🎛️ Exit requested from system tray - delegating to ExitRequested handler");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _contextMenu?.Dispose();
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _contextMenu = null;
            _logger.LogInformation("🎛️ Enhanced system tray service disposed");
        }
    }
}