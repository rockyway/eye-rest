using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface ITimerService : System.ComponentModel.INotifyPropertyChanged
    {
        event EventHandler<TimerEventArgs> EyeRestWarning;
        event EventHandler<TimerEventArgs> EyeRestDue;
        event EventHandler<TimerEventArgs> BreakWarning;
        event EventHandler<TimerEventArgs> BreakDue;
        
        bool IsRunning { get; }
        bool IsPaused { get; }
        bool IsSmartPaused { get; }
        bool IsManuallyPaused { get; } // NEW: Manual pause (e.g., for meetings)
        TimeSpan TimeUntilNextEyeRest { get; }
        TimeSpan TimeUntilNextBreak { get; }
        string NextEventDescription { get; }
        bool IsBreakDelayed { get; }
        TimeSpan DelayRemaining { get; }
        TimeSpan? ManualPauseRemaining { get; } // NEW: Remaining manual pause time
        string? PauseReason { get; } // NEW: Reason for current pause
        bool IsAnyNotificationActive { get; } // NEW: Check if either notification is active
        
        Task StartAsync();
        Task StopAsync();
        Task PauseAsync();
        Task ResumeAsync();
        Task SmartPauseAsync(string reason);
        Task SmartResumeAsync();
        Task SmartResumeAsync(string reason);
        Task SmartSessionResetAsync(string reason); // NEW: Reset timers for fresh working session
        Task PauseForDurationAsync(TimeSpan duration, string reason); // NEW: Manual pause for specific duration
        Task ResetEyeRestTimer();
        Task ResetBreakTimer();
        Task DelayBreak(TimeSpan delay);
        Task RestartEyeRestTimerAfterCompletion();
        Task RestartBreakTimerAfterCompletion();
        
        // Injection methods to avoid circular dependency
        void SetNotificationService(INotificationService notificationService);
        void SetUserPresenceService(IUserPresenceService userPresenceService);
        void UpdateConfiguration(EyeRest.Models.AppConfiguration config); // Sync latest user settings without restart

        // Methods for NotificationService to start/stop countdown timers after popup creation
        void StartEyeRestWarningTimer();
        void StartBreakWarningTimer();
        void StopEyeRestWarningTimer(); // Stop warning timer when popup is dismissed early
        void StopBreakWarningTimer(); // Stop warning timer when popup is dismissed early
        
        // Smart coordination methods
        void SmartResumeBreakTimerAfterEyeRest();
        void SmartResumeEyeRestTimerAfterBreak();
        
        // Power management methods
        Task RecoverFromSystemResumeAsync(string reason); // NEW: Recover timers after system resume from standby/hibernate
        
        // Emergency recovery methods
        Task<bool> ForceTimerRecoveryAsync(string reason = "Timer events not firing"); // NEW: Emergency recovery when timer events fail to fire

        // Processing flag management for synchronization
        void ClearEyeRestProcessingFlag(); // SYNC FIX: Clear processing flag after popup completion
        void ClearBreakProcessingFlag(); // SYNC FIX: Clear processing flag after popup completion
        void ClearEyeRestWarningProcessingFlag(); // SYNC FIX: Clear warning processing flag after warning completion
        void ClearBreakWarningProcessingFlag(); // SYNC FIX: Clear warning processing flag after warning completion
    }

    public class TimerEventArgs : EventArgs
    {
        public DateTime TriggeredAt { get; set; }
        public TimeSpan NextInterval { get; set; }
        public TimerType Type { get; set; }
    }

    public enum TimerType
    {
        EyeRestWarning,
        EyeRest,
        BreakWarning,
        Break
    }
}