using System;

namespace EyeRest.Services.Abstractions
{
    /// <summary>
    /// Abstraction for timer functionality that can be mocked for testing
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// Gets or sets the timer interval
        /// </summary>
        TimeSpan Interval { get; set; }
        
        /// <summary>
        /// Gets whether the timer is enabled and running
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Occurs when the timer interval has elapsed
        /// </summary>
        event EventHandler<EventArgs> Tick;
        
        /// <summary>
        /// Starts the timer
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stops the timer
        /// </summary>
        void Stop();
    }
}