using System;
using System.Threading;
using System.Windows.Threading;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services.Implementation
{
    /// <summary>
    /// Hybrid timer implementation using System.Threading.Timer + Dispatcher.BeginInvoke
    /// This solves the DispatcherTimer corruption issue after system sleep/hibernation
    /// </summary>
    internal class HybridTimer : EyeRest.Services.Abstractions.ITimer
    {
        private readonly Dispatcher _dispatcher;
        private readonly ILogger? _logger;
        private System.Threading.Timer? _systemTimer;
        private TimeSpan _interval = TimeSpan.FromSeconds(1);
        private volatile bool _isEnabled = false;
        private volatile bool _disposed = false;

        public HybridTimer(Dispatcher dispatcher, ILogger? logger = null)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logger = logger;
        }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentException("Interval must be greater than zero", nameof(value));
                    
                _interval = value;
                
                // If timer is running, restart with new interval
                if (_isEnabled && _systemTimer != null)
                {
                    _systemTimer.Change(_interval, _interval);
                }
            }
        }

        public bool IsEnabled => _isEnabled && !_disposed;

        public event EventHandler<EventArgs>? Tick;

        public void Start()
        {
            if (_disposed)
                return;

            if (_isEnabled)
                return; // Already started

            _isEnabled = true;

            // Create new System.Threading.Timer that doesn't suffer from DispatcherTimer issues
            _systemTimer = new System.Threading.Timer(OnSystemTimerTick, null, _interval, _interval);
            
            _logger?.LogDebug("HybridTimer started with interval {Interval}", _interval);
        }

        public void Stop()
        {
            if (_disposed)
                return;

            _isEnabled = false;

            // Stop the system timer
            _systemTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _systemTimer?.Dispose();
            _systemTimer = null;
            
            _logger?.LogDebug("HybridTimer stopped");
        }

        private void OnSystemTimerTick(object? state)
        {
            if (!_isEnabled || _disposed)
                return;

            try
            {
                // Marshal to UI thread using Dispatcher.BeginInvoke
                // This is the key difference from DispatcherTimer - we use System.Threading.Timer
                // for reliability and only use Dispatcher for UI thread marshalling
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isEnabled && !_disposed)
                    {
                        Tick?.Invoke(this, EventArgs.Empty);
                    }
                }), DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in HybridTimer tick handler");
                // Don't let timer exceptions break the timer - this improves reliability
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isEnabled = false;

            _systemTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _systemTimer?.Dispose();
            _systemTimer = null;

            _logger?.LogDebug("HybridTimer disposed");
        }
    }
}