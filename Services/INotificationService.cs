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
    }

    public enum BreakAction
    {
        Completed,
        DelayOneMinute,
        DelayFiveMinutes,
        Skipped
    }
}