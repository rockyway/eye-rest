using EyeRest.Services.Abstractions;
using ITimer = EyeRest.Services.Abstractions.ITimer;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux timer implementation using System.Threading.Timer (same design as
    /// <c>MacOSTimer</c>). Fires tick events directly on the thread pool thread;
    /// event handlers are responsible for marshaling to the UI thread for any
    /// UI operations (via IDispatcherService).
    /// </summary>
    public class LinuxTimer : ITimer
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
