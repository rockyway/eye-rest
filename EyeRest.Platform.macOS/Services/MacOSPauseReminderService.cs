using System;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IPauseReminderService"/>.
    /// Uses UNUserNotificationCenter for native macOS notifications and
    /// System.Threading.Timer for scheduling reminders.
    /// </summary>
    public class MacOSPauseReminderService : IPauseReminderService
    {
        private readonly ILogger<MacOSPauseReminderService> _logger;
        private Timer? _reminderTimer;
        private Timer? _autoResumeTimer;
        private bool _disposed;

        private DateTime? _pauseStartTime;
        private string _pauseReason = string.Empty;
        private int _remindersShown;
        private bool _isPaused;
        private bool _isMonitoring;

        // Configurable settings
        private TimeSpan _reminderInterval = TimeSpan.FromHours(1);
        private TimeSpan _maxPauseDuration = TimeSpan.FromHours(8);

        public MacOSPauseReminderService(ILogger<MacOSPauseReminderService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<PauseReminderEventArgs>? PauseReminderShown;
        public event EventHandler<AutoResumeEventArgs>? AutoResumeTriggered;

        public Task InitializeAsync()
        {
            _logger.LogInformation("MacOS pause reminder service initialized");
            return Task.CompletedTask;
        }

        public Task StartMonitoringAsync()
        {
            _isMonitoring = true;
            _logger.LogInformation("Pause reminder monitoring started");
            return Task.CompletedTask;
        }

        public Task StopMonitoringAsync()
        {
            _isMonitoring = false;
            StopTimers();
            _logger.LogInformation("Pause reminder monitoring stopped");
            return Task.CompletedTask;
        }

        public Task OnTimersPausedAsync(string reason)
        {
            if (!_isMonitoring)
            {
                _logger.LogDebug("Not monitoring, ignoring pause event");
                return Task.CompletedTask;
            }

            _isPaused = true;
            _pauseStartTime = DateTime.UtcNow;
            _pauseReason = reason;
            _remindersShown = 0;

            _logger.LogInformation("Timers paused: {Reason}", reason);

            // Start reminder timer
            _reminderTimer?.Dispose();
            _reminderTimer = new Timer(
                OnReminderTick,
                null,
                _reminderInterval,
                _reminderInterval);

            // Start auto-resume safety timer
            _autoResumeTimer?.Dispose();
            _autoResumeTimer = new Timer(
                OnAutoResumeTick,
                null,
                _maxPauseDuration,
                Timeout.InfiniteTimeSpan);

            return Task.CompletedTask;
        }

        public Task OnTimersResumedAsync()
        {
            _isPaused = false;
            _pauseStartTime = null;
            _pauseReason = string.Empty;
            _remindersShown = 0;

            StopTimers();

            _logger.LogInformation("Timers resumed, pause reminder monitoring reset");
            return Task.CompletedTask;
        }

        public Task<bool> ShowPauseConfirmationAsync(string reason)
        {
            // On macOS, we could show a native alert dialog.
            // For now, auto-confirm (will be enhanced in Phase 6 with Avalonia dialogs).
            _logger.LogDebug("Pause confirmation requested for reason: {Reason}. Auto-confirming.", reason);
            return Task.FromResult(true);
        }

        public async Task ForceResumeAfterMaxPauseAsync()
        {
            if (!_isPaused) return;

            var totalDuration = _pauseStartTime.HasValue
                ? DateTime.UtcNow - _pauseStartTime.Value
                : TimeSpan.Zero;

            _logger.LogWarning(
                "Force resuming after max pause duration: {Duration:F1} hours",
                totalDuration.TotalHours);

            AutoResumeTriggered?.Invoke(this, new AutoResumeEventArgs
            {
                TotalPauseDuration = totalDuration,
                PauseReason = _pauseReason,
                ResumeTime = DateTime.UtcNow,
                WasForced = true
            });

            await OnTimersResumedAsync();
        }

        public PauseStatus GetCurrentPauseStatus()
        {
            var pauseDuration = _pauseStartTime.HasValue
                ? DateTime.UtcNow - _pauseStartTime.Value
                : TimeSpan.Zero;

            var timeUntilAutoResume = _pauseStartTime.HasValue
                ? _maxPauseDuration - pauseDuration
                : TimeSpan.Zero;

            if (timeUntilAutoResume < TimeSpan.Zero)
                timeUntilAutoResume = TimeSpan.Zero;

            return new PauseStatus
            {
                IsPaused = _isPaused,
                PauseStartTime = _pauseStartTime,
                PauseDuration = pauseDuration,
                PauseReason = _pauseReason,
                RemindersShown = _remindersShown,
                NextReminderTime = _isPaused && _pauseStartTime.HasValue
                    ? _pauseStartTime.Value + _reminderInterval * (_remindersShown + 1)
                    : null,
                TimeUntilAutoResume = timeUntilAutoResume,
                IsNearingAutoResume = timeUntilAutoResume <= TimeSpan.FromMinutes(30)
            };
        }

        public Task ShowPauseReminderToastAsync(TimeSpan pauseDuration, string reason)
        {
            try
            {
                PostNotification(
                    "Eye Rest - Timers Paused",
                    $"Timers have been paused for {FormatDuration(pauseDuration)}. Reason: {reason}",
                    $"pause-reminder-{_remindersShown}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show pause reminder toast");
            }

            return Task.CompletedTask;
        }

        public Task ShowRecoveryNotificationAsync(string recoveryType, string details)
        {
            try
            {
                PostNotification(
                    $"Eye Rest - {recoveryType}",
                    details,
                    $"recovery-{Guid.NewGuid():N}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show recovery notification");
            }

            return Task.CompletedTask;
        }

        #region Private Helpers

        private void OnReminderTick(object? state)
        {
            if (!_isPaused || !_isMonitoring) return;

            _remindersShown++;
            var duration = _pauseStartTime.HasValue
                ? DateTime.UtcNow - _pauseStartTime.Value
                : TimeSpan.Zero;

            _logger.LogInformation(
                "Pause reminder #{Count}: paused for {Duration:F1} minutes",
                _remindersShown, duration.TotalMinutes);

            PauseReminderShown?.Invoke(this, new PauseReminderEventArgs
            {
                PauseDuration = duration,
                PauseReason = _pauseReason,
                ReminderTime = DateTime.UtcNow,
                ReminderCount = _remindersShown
            });

            _ = ShowPauseReminderToastAsync(duration, _pauseReason);
        }

        private void OnAutoResumeTick(object? state)
        {
            if (!_isPaused) return;

            _logger.LogWarning("Auto-resume timer fired after max pause duration");
            _ = ForceResumeAfterMaxPauseAsync();
        }

        private void StopTimers()
        {
            _reminderTimer?.Dispose();
            _reminderTimer = null;

            _autoResumeTimer?.Dispose();
            _autoResumeTimer = null;
        }

        private void PostNotification(string title, string body, string identifier)
        {
            try
            {
                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    var center = UserNotifications.GetCurrentNotificationCenter();
                    if (center == IntPtr.Zero)
                    {
                        _logger.LogWarning("UNUserNotificationCenter not available for pause reminder");
                        return;
                    }

                    UserNotifications.RequestAuthorization(
                        center, UserNotifications.UNAuthorizationOptionAlertSoundBadge);

                    var content = UserNotifications.CreateNotificationContent(title, body);
                    if (content == IntPtr.Zero) return;

                    var trigger = UserNotifications.CreateTimeIntervalTrigger(1.0, false);
                    var request = UserNotifications.CreateNotificationRequest(identifier, content, trigger);
                    if (request == IntPtr.Zero) return;

                    UserNotifications.AddNotification(center, request);
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post notification: {Title}", title);
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            return $"{duration.Minutes}m";
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopTimers();
            _logger.LogInformation("MacOSPauseReminderService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
