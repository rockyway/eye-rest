using System;
using System.Threading.Tasks;

namespace EyeRest.Services.Abstractions
{
    /// <summary>
    /// Platform-level lifecycle hooks for an always-on desktop app:
    /// opt out of OS-level throttling (App Nap on macOS) and observe system
    /// suspend/resume so timer state can be reset cleanly when the OS
    /// returns the app to active scheduling.
    ///
    /// <para>
    /// macOS implementation calls <c>NSProcessInfo.beginActivity</c> to
    /// suppress App Nap and registers <c>NSWorkspaceDidWakeNotification</c>
    /// / <c>NSWorkspaceWillSleepNotification</c> observers. Other platforms
    /// can implement no-op or analogous hooks (e.g. Windows
    /// <c>SystemEvents.PowerModeChanged</c>).
    /// </para>
    /// </summary>
    public interface IAppLifecycleService
    {
        /// <summary>
        /// Raised after the OS reports the system has resumed from sleep
        /// or returned the app from suspension. Subscribers should reset
        /// any time-based state that may be stale.
        /// </summary>
        event Action? SystemAwoke;

        /// <summary>
        /// Raised before the OS reports the system is about to sleep.
        /// Useful for preemptively persisting state or stopping work.
        /// </summary>
        event Action? SystemWillSleep;

        /// <summary>
        /// Acquire the activity token (App Nap opt-out) and start observing
        /// system suspend/resume events. Idempotent.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Release the activity token and stop observing events. Idempotent.
        /// </summary>
        Task StopAsync();
    }
}
