using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS timer factory that creates <see cref="MacOSTimer"/> instances
    /// backed by System.Threading.Timer instead of WPF DispatcherTimer.
    /// </summary>
    public class MacOSTimerFactory : ITimerFactory
    {
        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            return new MacOSTimer();
        }
    }
}
