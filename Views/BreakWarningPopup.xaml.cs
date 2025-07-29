using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace EyeRest.Views
{
    public partial class BreakWarningPopup : UserControl
    {
        private DispatcherTimer? _progressTimer;
        private TimeSpan _duration;
        private DateTime _startTime;

        public event EventHandler? Completed;

        public BreakWarningPopup()
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

        public void StartCountdown(TimeSpan timeUntilBreak)
        {
            // CRITICAL FIX: Clean up existing timer before creating new one
            StopCountdown();
            
            _duration = timeUntilBreak;
            _startTime = DateTime.Now;
            
            UpdateTimeDisplay();
            
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Update 10 times per second for smooth animation
            };
            
            _progressTimer.Tick += OnProgressTimerTick;
            _progressTimer.Start();
        }

        private void OnProgressTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var remaining = _duration - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                // Warning period complete - animate to final state
                System.Diagnostics.Debug.WriteLine($"🟠 BreakWarningPopup: Warning period complete, stopping timer");
                _progressTimer?.Stop();
                var completionAnimation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = 0, // Warning bar goes to 0% when complete
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completionAnimation);
                CountdownText.Text = "Break starting now!";
                
                System.Diagnostics.Debug.WriteLine($"🟠 BreakWarningPopup: Firing Completed event");
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Update progress bar with smooth animation (inverted - starts at 100% and goes to 0%)
            var progressPercent = 100 - ((elapsed.TotalMilliseconds / _duration.TotalMilliseconds) * 100);
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
            
            // Update countdown display
            var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            CountdownText.Text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
        }

        private void UpdateTimeDisplay()
        {
            var totalSeconds = (int)_duration.TotalSeconds;
            CountdownText.Text = $"{totalSeconds} second{(totalSeconds != 1 ? "s" : "")}";
        }

        public void StopCountdown()
        {
            if (_progressTimer != null)
            {
                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick; // CRITICAL FIX: Remove event handler to prevent memory leaks
                _progressTimer = null; // CRITICAL FIX: Null the timer to prevent reuse
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            StopCountdown();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}