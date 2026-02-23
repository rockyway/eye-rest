using System;
using System.Windows.Threading;
using EyeRest.Services.Abstractions;

namespace EyeRest.Services.Implementation
{
    /// <summary>
    /// Production implementation of ITimer that wraps DispatcherTimer
    /// </summary>
    internal class ProductionTimer : ITimer
    {
        private readonly DispatcherTimer _dispatcherTimer;
        private bool _disposed = false;

        public ProductionTimer(DispatcherPriority priority = DispatcherPriority.Normal)
        {
            _dispatcherTimer = new DispatcherTimer(priority);
            _dispatcherTimer.Tick += OnDispatcherTimerTick;
        }

        public TimeSpan Interval 
        { 
            get => _dispatcherTimer.Interval; 
            set => _dispatcherTimer.Interval = value; 
        }

        public bool IsEnabled => _dispatcherTimer.IsEnabled;

        public event EventHandler<EventArgs>? Tick;

        public void Start()
        {
            if (!_disposed)
            {
                _dispatcherTimer.Start();
            }
        }

        public void Stop()
        {
            if (!_disposed)
            {
                _dispatcherTimer.Stop();
            }
        }

        private void OnDispatcherTimerTick(object? sender, EventArgs e)
        {
            Tick?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _dispatcherTimer.Tick -= OnDispatcherTimerTick;
                _dispatcherTimer.Stop();
                _disposed = true;
            }
        }
    }
}