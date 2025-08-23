using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface INotificationService
    {
        Task ShowEyeRestWarningAsync(TimeSpan timeUntilBreak);
        Task ShowEyeRestReminderAsync(TimeSpan duration);
        Task ShowBreakWarningAsync(TimeSpan timeUntilBreak);
        Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress);
        Task HideAllNotifications();
        
        // Test mode methods - these don't record analytics
        Task ShowEyeRestWarningTestAsync(TimeSpan timeUntilBreak);
        Task ShowEyeRestReminderTestAsync(TimeSpan duration);
        Task ShowBreakWarningTestAsync(TimeSpan timeUntilBreak);
        Task<BreakAction> ShowBreakReminderTestAsync(TimeSpan duration, IProgress<double> progress);
        bool IsTestMode { get; }
        
        // External countdown control for warning popups
        void UpdateEyeRestWarningCountdown(TimeSpan remaining);
        void UpdateBreakWarningCountdown(TimeSpan remaining);
        void StartEyeRestWarningCountdown(TimeSpan duration);
        void StartBreakWarningCountdown(TimeSpan duration);
        
        // Injection method to avoid circular dependency
        void SetTimerService(ITimerService timerService);
        
        // Status check properties for backup trigger coordination
        bool IsBreakWarningActive { get; }
        bool IsEyeRestWarningActive { get; }
    }

    public enum BreakAction
    {
        Completed,
        DelayOneMinute,
        DelayFiveMinutes,
        Skipped,
        ConfirmedAfterCompletion  // User confirmed after break completion (when RequireConfirmationAfterBreak is enabled)
    }
}