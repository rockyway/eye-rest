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
    
    /// <summary>
    /// Production implementation using DispatcherTimer
    /// </summary>
    public class DispatcherTimerWrapper : ITimerWrapper
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer;
        
        public DispatcherTimerWrapper()
        {
            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Tick += (s, e) => Tick?.Invoke(s, e);
        }
        
        public TimeSpan Interval 
        { 
            get => _timer.Interval; 
            set => _timer.Interval = value; 
        }
        
        public bool IsEnabled => _timer.IsEnabled;
        
        public event EventHandler? Tick;
        
        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
    }
    
    /// <summary>
    /// Test implementation that allows manual triggering
    /// </summary>
    public class TestTimerWrapper : ITimerWrapper
    {
        public TimeSpan Interval { get; set; }
        public bool IsEnabled { get; private set; }
        public event EventHandler? Tick;
        
        public void Start() => IsEnabled = true;
        public void Stop() => IsEnabled = false;
        
        /// <summary>
        /// Manually trigger the timer tick for testing
        /// </summary>
        public void TriggerTick()
        {
            if (IsEnabled)
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}