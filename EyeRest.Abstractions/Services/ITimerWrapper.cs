using System;

namespace EyeRest.Services
{
    /// <summary>
    /// Abstraction for timer functionality to enable testing
    /// </summary>
    public interface ITimerWrapper
    {
        TimeSpan Interval { get; set; }
        bool IsEnabled { get; }
        event EventHandler Tick;

        void Start();
        void Stop();
    }
}
