using System.Windows.Threading;
using EyeRest.Services.Abstractions;

namespace EyeRest.Services.Implementation
{
    /// <summary>
    /// Production implementation of ITimerFactory that creates DispatcherTimer-based timers
    /// </summary>
    public class ProductionTimerFactory : ITimerFactory
    {
        public ITimer CreateTimer(DispatcherPriority priority = DispatcherPriority.Normal)
        {
            return new ProductionTimer(priority);
        }
    }
}