using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="ISystemTrayService"/>.
    /// The visual tray icon is managed by Avalonia's TrayIcon API in App.axaml.cs
    /// (same arrangement as macOS); this service handles event routing,
    /// notifications, and tooltip updates.
    /// </summary>
    public class LinuxSystemTrayService : ISystemTrayService
    {
        private readonly ILogger<LinuxSystemTrayService> _logger;
        private bool _isInitialized;
        private TrayIconState _currentState = TrayIconState.Active;

        public LinuxSystemTrayService(ILogger<LinuxSystemTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? PauseTimersRequested;
        public event EventHandler? ResumeTimersRequested;
        public event EventHandler? PauseForMeetingRequested;
        public event EventHandler? PauseForMeeting1hRequested;
#pragma warning disable CS0067 // Events required by interface but raised externally
        public event EventHandler? ShowTimerStatusRequested;
        public event EventHandler? ShowAnalyticsRequested;
        public event EventHandler? BalloonTipClicked;
#pragma warning restore CS0067
        public event Action<TrayIconState>? TrayIconStateChanged;
        public event Action<TimeSpan, TimeSpan, string>? TimerDetailsUpdated;

        public void Initialize()
        {
            _isInitialized = true;
            _logger.LogInformation("Linux system tray service initialized (visual icon managed by Avalonia TrayIcon)");
        }

        public void ShowTrayIcon()
        {
            // Managed by Avalonia TrayIcon in App.axaml.cs
            _logger.LogDebug("ShowTrayIcon called (managed by Avalonia)");
        }

        public void HideTrayIcon()
        {
            // Managed by Avalonia TrayIcon in App.axaml.cs
            _logger.LogDebug("HideTrayIcon called (managed by Avalonia)");
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (!_isInitialized) return;
            _currentState = state;
            _logger.LogDebug("Tray icon state updated to {State}", state);
            TrayIconStateChanged?.Invoke(state);
        }

        public void ShowBalloonTip(string title, string text)
        {
            LinuxNotifications.Send(_logger, title, text);
        }

        private string _lastStatusText = "Running";

        public void UpdateTimerStatus(string status)
        {
            _lastStatusText = status;
            _logger.LogDebug("Timer status: {Status}", status);
        }

        public void UpdateTimerDetails(TimeSpan eyeRestRemaining, TimeSpan breakRemaining)
        {
            // _lastStatusText was set by UpdateTimerStatus() just before this call
            var statusSnapshot = _lastStatusText;
            var tooltipStatus = $"Eye Rest: {FormatTimeSpan(eyeRestRemaining)} | Break: {FormatTimeSpan(breakRemaining)}";
            UpdateTimerStatus(tooltipStatus);
            TimerDetailsUpdated?.Invoke(eyeRestRemaining, breakRemaining, statusSnapshot);
        }

        public void SetMeetingMode(bool isInMeeting, string meetingType = "")
        {
            if (isInMeeting)
            {
                _logger.LogDebug("Meeting mode enabled: {MeetingType}", meetingType);
                UpdateTrayIcon(TrayIconState.MeetingMode);
            }
            else
            {
                _logger.LogDebug("Meeting mode disabled");
                UpdateTrayIcon(TrayIconState.Active);
            }
        }

        // Event-raising methods for Avalonia TrayIcon menu items
        public void OnRestoreRequested() => RestoreRequested?.Invoke(this, EventArgs.Empty);
        public void OnExitRequested() => ExitRequested?.Invoke(this, EventArgs.Empty);
        public void OnPauseTimersRequested() => PauseTimersRequested?.Invoke(this, EventArgs.Empty);
        public void OnResumeTimersRequested() => ResumeTimersRequested?.Invoke(this, EventArgs.Empty);
        public void OnPauseForMeetingRequested() => PauseForMeetingRequested?.Invoke(this, EventArgs.Empty);
        public void OnPauseForMeeting1hRequested() => PauseForMeeting1hRequested?.Invoke(this, EventArgs.Empty);

        private static string FormatTimeSpan(TimeSpan ts)
        {
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
                : $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
    }
}
