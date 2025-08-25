using System.Windows.Threading;

namespace EyeRest.Services.Abstractions
{
    /// <summary>
    /// Factory for creating timer instances that can be production or test implementations
    /// </summary>
    public interface ITimerFactory
    {
        /// <summary>
        /// Creates a new timer instance
        /// </summary>
        /// <param name="priority">Optional dispatcher priority for WPF timers</param>
        /// <returns>A new timer instance</returns>
        ITimer CreateTimer(DispatcherPriority priority = DispatcherPriority.Normal);
    }
}