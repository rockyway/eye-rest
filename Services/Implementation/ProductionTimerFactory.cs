using System.Windows.Threading;
using EyeRest.Services.Abstractions;

namespace EyeRest.Services.Implementation
{
    /// <summary>
    /// Production implementation of ITimerFactory that creates DispatcherTimer-based timers
    /// </summary>
    public class ProductionTimerFactory : ITimerFactory
    {
        public ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal)
        {
            var wpfPriority = priority switch
            {
                TimerPriority.Background => DispatcherPriority.Background,
                TimerPriority.Render => DispatcherPriority.Render,
                _ => DispatcherPriority.Normal
            };
            return new ProductionTimer(wpfPriority);
        }
    }
}
