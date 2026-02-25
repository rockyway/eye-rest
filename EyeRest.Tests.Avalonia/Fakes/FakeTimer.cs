using System;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Tests.Avalonia.Fakes
{
    /// <summary>
    /// Fake timer for testing that allows manual tick firing without real time delays.
    /// Tracks start/stop calls and interval changes for assertion in tests.
    /// </summary>
    public class FakeTimer : ITimer
    {
        private bool _isEnabled;
        private TimeSpan _interval;
        public bool IsDisposed { get; private set; }

        public TimeSpan Interval
        {
            get => _interval;
            set => _interval = value;
        }

        public bool IsEnabled => _isEnabled;

        public event EventHandler<EventArgs>? Tick;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start()
        {
            _isEnabled = true;
            StartCount++;
        }

        public void Stop()
        {
            _isEnabled = false;
            StopCount++;
        }

        /// <summary>
        /// Manually fires the Tick event, simulating a timer tick without real time passing.
        /// </summary>
        public void FireTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            IsDisposed = true;
            _isEnabled = false;
        }
    }
}
