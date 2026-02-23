using System;
using System.Threading;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS timer implementation using System.Threading.Timer.
    /// Marshals tick events back to the captured SynchronizationContext
    /// (typically the Avalonia UI thread) if one is available.
    /// </summary>
    public class MacOSTimer : ITimer
    {
        private System.Threading.Timer? _timer;
        private readonly SynchronizationContext? _syncContext;
        private bool _disposed;

        public MacOSTimer()
        {
            _syncContext = SynchronizationContext.Current;
        }

        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public event EventHandler<EventArgs>? Tick;

        public void Start()
        {
            if (_disposed) return;
            _timer?.Dispose();
            _timer = new System.Threading.Timer(OnTick, null, Interval, Interval);
            IsEnabled = true;
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            IsEnabled = false;
        }

        private void OnTick(object? state)
        {
            if (_syncContext != null)
                _syncContext.Post(_ => Tick?.Invoke(this, EventArgs.Empty), null);
            else
                Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer?.Dispose();
                _timer = null;
                _disposed = true;
            }
        }
    }
}
