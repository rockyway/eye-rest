using System;
using System.Threading;
using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS timer implementation using System.Threading.Timer.
    /// Fires tick events directly on the thread pool thread to avoid
    /// being throttled by macOS App Nap when the app is in the background.
    /// Event handlers are responsible for marshaling to the UI thread
    /// for any UI operations (via IDispatcherService).
    /// </summary>
    public class MacOSTimer : ITimer
    {
        private System.Threading.Timer? _timer;
        private bool _disposed;

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
            // Fire directly on thread pool — don't marshal through SynchronizationContext.
            // macOS throttles SyncContext.Post delivery when the app is in the background
            // (menu bar / system tray), causing timer ticks to be delayed indefinitely.
            // The timer event handlers (OnBreakTimerTick, OnEyeRestTimerTick) handle their
            // own UI thread marshaling via IDispatcherService for UI-specific operations.
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
