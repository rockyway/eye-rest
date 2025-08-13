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
        private TimeSpan _totalDuration;

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
            _totalDuration = TimeSpan.FromSeconds(seconds);
            
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: Starting display-only countdown for {seconds} seconds");
            
            // Initialize display - no internal timer
            UpdateDisplay(TimeSpan.FromSeconds(seconds));
            ProgressBar.Value = 100; // Start at 100%
        }

        /// <summary>
        /// Update the countdown display externally (called by TimerService)
        /// </summary>
        public void UpdateCountdown(TimeSpan remaining)
        {
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: UpdateCountdown called with {remaining.TotalSeconds} seconds remaining");
            
            if (remaining <= TimeSpan.Zero)
            {
                // Final state - animate completion
                var completionAnimation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = 0, // Warning bar goes to 0% when complete
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completionAnimation);
                CountdownText.Text = "Eye rest starting now!";
                
                System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: Countdown complete - firing WarningCompleted event");
                WarningCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            UpdateDisplay(remaining);
        }

        private void UpdateDisplay(TimeSpan remaining)
        {
            // Update countdown text
            var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            CountdownText.Text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
            
            // Update progress bar - calculate percentage based on total duration
            if (_totalDuration.TotalSeconds > 0)
            {
                var progressPercent = (remaining.TotalSeconds / _totalDuration.TotalSeconds) * 100;
                var targetValue = Math.Max(progressPercent, 0);
                
                // Smooth animation to new progress value
                var animation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = targetValue,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
            }
        }

        public void StopCountdown()
        {
            // No internal timer to stop - this is now display-only
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: StopCountdown called");
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