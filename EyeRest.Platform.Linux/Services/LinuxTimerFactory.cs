using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux timer factory that creates <see cref="LinuxTimer"/> instances
    /// backed by System.Threading.Timer.
    /// </summary>
    public class LinuxTimerFactory : ITimerFactory
    {
        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            return new LinuxTimer();
        }
    }
}
