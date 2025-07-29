using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace EyeRest.Views
{
    public partial class EyeRestWarningPopup : UserControl
    {
        private DispatcherTimer? _countdownTimer;
        private int _remainingSeconds;
        private int _totalSeconds;

        public event EventHandler? WarningCompleted;

        public EyeRestWarningPopup()
        {
            InitializeComponent();
            
            // Allow ESC key to close popup
            Loaded += (s, e) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.PreviewKeyDown += Window_PreviewKeyDown;
                }
            };
        }

        public void StartCountdown(int seconds)
        {
            // CRITICAL FIX: Clean up existing timer before creating new one
            StopCountdown();
            
            _totalSeconds = seconds;
            _remainingSeconds = seconds;
            _totalDuration = TimeSpan.FromSeconds(seconds);
            _startTime = DateTime.Now;
            
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup.StartCountdown: Starting {seconds} second warning");
            
            UpdateDisplay();
            
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Update 10 times per second for smooth animation
            };
            
            _countdownTimer.Tick += OnCountdownTick;
            _countdownTimer.Start();
        }

        private DateTime _startTime;
        private TimeSpan _totalDuration;

        private void OnCountdownTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var remaining = _totalDuration - elapsed;
            
            if (remaining <= TimeSpan.Zero)
            {
                // Warning period complete
                System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: Warning period complete, stopping timer");
                _countdownTimer?.Stop();
                if (_countdownTimer != null)
                {
                    _countdownTimer.Tick -= OnCountdownTick; // Prevent memory leaks
                }
                _countdownTimer = null;
                
                var completionAnimation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = 0, // Warning bar goes to 0% when complete (falling down effect)
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completionAnimation);
                CountdownText.Text = "Eye rest starting now!";
                
                System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: Firing WarningCompleted event");
                WarningCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }
            
            // Update display with smooth progress
            var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            CountdownText.Text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
            
            // Update progress bar with smooth animation (100% to 0% as time progresses - falling down effect)
            var progressPercent = 100 - ((elapsed.TotalMilliseconds / _totalDuration.TotalMilliseconds) * 100);
            var targetValue = Math.Max(progressPercent, 0);
            
            // Animate the progress bar value for smooth visual feedback
            var animation = new DoubleAnimation
            {
                From = ProgressBar.Value,
                To = targetValue,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
        }

        private void UpdateDisplay()
        {
            var totalSeconds = (int)_totalDuration.TotalSeconds;
            CountdownText.Text = $"{totalSeconds} second{(totalSeconds != 1 ? "s" : "")}";
            ProgressBar.Value = 100; // Start at 100% and will fall down to 0%
        }

        public void StopCountdown()
        {
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Tick -= OnCountdownTick; // CRITICAL FIX: Remove event handler to prevent memory leaks
                _countdownTimer = null; // CRITICAL FIX: Null the timer to prevent reuse
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                StopCountdown();
                WarningCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            StopCountdown();
            WarningCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}