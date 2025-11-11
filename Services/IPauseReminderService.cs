using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    /// <summary>
    /// Service interface for managing hourly pause reminders and safety mechanisms
    /// </summary>
    public interface IPauseReminderService : IDisposable
    {
        /// <summary>
        /// Event fired when pause reminder notification is shown
        /// </summary>
        event EventHandler<PauseReminderEventArgs>? PauseReminderShown;
        
        /// <summary>
        /// Event fired when auto-resume safety mechanism activates
        /// </summary>
        event EventHandler<AutoResumeEventArgs>? AutoResumeTriggered;

        /// <summary>
        /// Initialize the pause reminder monitoring system
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Start monitoring for pause duration and sending reminders
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stop pause reminder monitoring
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Record when timers are paused (starts reminder countdown)
        /// </summary>
        /// <param name="reason">Reason for pause (manual, meeting, away, etc.)</param>
        Task OnTimersPausedAsync(string reason);

        /// <summary>
        /// Record when timers are resumed (stops reminder countdown)
        /// </summary>
        Task OnTimersResumedAsync();

        /// <summary>
        /// Show confirmation dialog for manual pause actions
        /// </summary>
        /// <param name="reason">Reason for pause request</param>
        /// <returns>True if user confirms pause, false if cancelled</returns>
        Task<bool> ShowPauseConfirmationAsync(string reason);

        /// <summary>
        /// Force resume timers after maximum pause duration (safety mechanism)
        /// </summary>
        Task ForceResumeAfterMaxPauseAsync();

        /// <summary>
        /// Get current pause status and duration
        /// </summary>
        /// <returns>Pause status information</returns>
        PauseStatus GetCurrentPauseStatus();

        /// <summary>
        /// Show Windows toast notification for pause reminder
        /// </summary>
        /// <param name="pauseDuration">How long timers have been paused</param>
        /// <param name="reason">Reason timers were paused</param>
        Task ShowPauseReminderToastAsync(TimeSpan pauseDuration, string reason);
        
        /// <summary>
        /// Show Windows toast notification for automatic recovery events
        /// </summary>
        /// <param name="recoveryType">Type of recovery (e.g., "Zombie Popup Recovery", "Timer Recovery")</param>
        /// <param name="details">Additional details about the recovery</param>
        Task ShowRecoveryNotificationAsync(string recoveryType, string details);
    }

    /// <summary>
    /// Event arguments for pause reminder notifications
    /// </summary>
    public class PauseReminderEventArgs : EventArgs
    {
        public TimeSpan PauseDuration { get; set; }
        public string PauseReason { get; set; } = string.Empty;
        public DateTime ReminderTime { get; set; }
        public int ReminderCount { get; set; }
    }

    /// <summary>
    /// Event arguments for auto-resume safety mechanism
    /// </summary>
    public class AutoResumeEventArgs : EventArgs
    {
        public TimeSpan TotalPauseDuration { get; set; }
        public string PauseReason { get; set; } = string.Empty;
        public DateTime ResumeTime { get; set; }
        public bool WasForced { get; set; }
    }

    /// <summary>
    /// Current pause status information
    /// </summary>
    public class PauseStatus
    {
        public bool IsPaused { get; set; }
        public DateTime? PauseStartTime { get; set; }
        public TimeSpan PauseDuration { get; set; }
        public string PauseReason { get; set; } = string.Empty;
        public int RemindersShown { get; set; }
        public DateTime? NextReminderTime { get; set; }
        public TimeSpan TimeUntilAutoResume { get; set; }
        public bool IsNearingAutoResume { get; set; }
    }

    /// <summary>
    /// Pause reminder configuration settings
    /// </summary>
    public class PauseReminderSettings
    {
        /// <summary>
        /// Enable hourly pause reminders
        /// </summary>
        public bool ShowPauseReminders { get; set; } = true;

        /// <summary>
        /// Interval between pause reminders in hours
        /// </summary>
        public int PauseReminderIntervalHours { get; set; } = 1;

        /// <summary>
        /// Maximum pause duration before auto-resume in hours
        /// </summary>
        public int MaxPauseHours { get; set; } = 8;

        /// <summary>
        /// Show confirmation dialog for manual pause actions
        /// </summary>
        public bool ConfirmManualPause { get; set; } = true;

        /// <summary>
        /// Show pause status in system tray tooltip
        /// </summary>
        public bool ShowPauseInSystemTray { get; set; } = true;

        /// <summary>
        /// Preserve timer progress when pausing/resuming
        /// </summary>
        public bool PreserveTimerProgress { get; set; } = true;

        /// <summary>
        /// Enable Windows toast notifications for pause reminders
        /// </summary>
        public bool EnableToastNotifications { get; set; } = true;

        /// <summary>
        /// Sound notification when showing pause reminders
        /// </summary>
        public bool PlaySoundOnReminder { get; set; } = false;
    }
}