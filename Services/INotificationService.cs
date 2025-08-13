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
        
        // External countdown control for warning popups
        void UpdateEyeRestWarningCountdown(TimeSpan remaining);
        void UpdateBreakWarningCountdown(TimeSpan remaining);
        void StartEyeRestWarningCountdown(TimeSpan duration);
        void StartBreakWarningCountdown(TimeSpan duration);
        
        // Injection method to avoid circular dependency
        void SetTimerService(ITimerService timerService);
    }

    public enum BreakAction
    {
        Completed,
        DelayOneMinute,
        DelayFiveMinutes,
        Skipped
    }
}