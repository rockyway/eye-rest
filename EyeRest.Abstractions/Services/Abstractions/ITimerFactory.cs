namespace EyeRest.Services.Abstractions
{
    /// <summary>
    /// Timer dispatch priority levels (platform-agnostic replacement for WPF DispatcherPriority)
    /// </summary>
    public enum TimerPriority
    {
        Background = 0,
        Normal = 1,
        Render = 2
    }

    /// <summary>
    /// Factory for creating timer instances that can be production or test implementations
    /// </summary>
    public interface ITimerFactory
    {
        /// <summary>
        /// Creates a new timer instance
        /// </summary>
        /// <param name="priority">Optional timer priority</param>
        /// <returns>A new timer instance</returns>
        ITimer CreateTimer(TimerPriority priority = TimerPriority.Normal);
    }
}
