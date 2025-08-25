using System;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Tests.Fakes
{
    /// <summary>
    /// Fake timer implementation for testing that can be controlled manually
    /// </summary>
    public class FakeTimer : ITimer
    {
        private bool _disposed = false;
        private bool _isEnabled = false;

        public TimeSpan Interval { get; set; }
        public bool IsEnabled => _isEnabled && !_disposed;
        
        /// <summary>
        /// Gets the total number of times this timer has been started
        /// </summary>
        public int StartCount { get; private set; }
        
        /// <summary>
        /// Gets the total number of times this timer has been stopped
        /// </summary>
        public int StopCount { get; private set; }
        
        /// <summary>
        /// Gets the total number of times Tick has been fired manually
        /// </summary>
        public int TickCount { get; private set; }

        public event EventHandler<EventArgs>? Tick;

        public void Start()
        {
            if (!_disposed)
            {
                _isEnabled = true;
                StartCount++;
            }
        }

        public void Stop()
        {
            if (!_disposed)
            {
                _isEnabled = false;
                StopCount++;
            }
        }

        /// <summary>
        /// Manually fires the Tick event for testing purposes
        /// </summary>
        public void FireTick()
        {
            if (IsEnabled)
            {
                TickCount++;
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Simulates multiple timer ticks
        /// </summary>
        /// <param name="count">Number of ticks to fire</param>
        public void FireTicks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                FireTick();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}