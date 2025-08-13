using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using EyeRest.Services;

namespace EyeRest.Views
{
    public partial class BreakPopup : UserControl
    {
        private DispatcherTimer? _progressTimer;
        private TimeSpan _duration;
        private DateTime _startTime;
        private IProgress<double>? _progress;

        public event EventHandler<BreakAction>? ActionSelected;

        public BreakPopup()
        {
            InitializeComponent();
        }

        public void StartCountdown(TimeSpan duration, IProgress<double>? progress = null)
        {
            // CRITICAL FIX: Clean up existing timer before creating new one (same fix as EyeRestPopup)
            StopCountdown();
            
            _duration = duration;
            _startTime = DateTime.Now;
            _progress = progress;
            
            System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup.StartCountdown: Starting {duration.TotalMinutes:F1} minute break countdown");
            
            UpdateTimeDisplay();
            
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Update 10 times per second for smooth animation
            };
            
            _progressTimer.Tick += OnProgressTimerTick;
            _progressTimer.Start();
            
            System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup.StartCountdown: Timer started successfully, should run for {duration.TotalMinutes:F1} minutes");
        }

        private void OnProgressTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var remaining = _duration - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                // Break complete - animate to 100%
                _progressTimer?.Stop();
                var completionAnimation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = 100,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completionAnimation);
                _progress?.Report(1.0);
                
                // Show completion state
                ShowCompletionState();
                return;
            }

            // Update progress bar with smooth animation
            var progressPercent = (elapsed.TotalMilliseconds / _duration.TotalMilliseconds) * 100;
            var targetValue = Math.Min(progressPercent, 100);
            
            // Animate the progress bar value for smooth visual feedback
            var animation = new DoubleAnimation
            {
                From = ProgressBar.Value,
                To = targetValue,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
            _progress?.Report(progressPercent / 100.0);
            
            // ENHANCED: Update time display to show both minutes and seconds throughout duration
            var remainingMinutes = (int)remaining.Minutes;
            var remainingSeconds = (int)remaining.Seconds;
            
            // Always show minutes and seconds for better user experience
            if (remaining.TotalMinutes >= 1)
            {
                TimeRemainingText.Text = $"{remainingMinutes}m {remainingSeconds}s remaining";
            }
            else
            {
                TimeRemainingText.Text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")} remaining";
            }
        }

        private void ShowCompletionState()
        {
            System.Diagnostics.Debug.WriteLine("🔥 BreakPopup.ShowCompletionState: Break completed successfully - showing completion state");
            
            // Change background to green for positive reinforcement
            var border = (Border)Content;
            border.Background = System.Windows.Media.Brushes.LightGreen;
            
            TimeRemainingText.Text = "Break complete! Great job!";
            
            // Hide action buttons
            DelayOneMinuteButton.Visibility = Visibility.Collapsed;
            DelayFiveMinutesButton.Visibility = Visibility.Collapsed;
            SkipButton.Visibility = Visibility.Collapsed;
            
            // Auto-close after 2 seconds
            var closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Auto-closing after completion, firing ActionSelected.Completed");
                ActionSelected?.Invoke(this, BreakAction.Completed);
            };
            
            closeTimer.Start();
        }

        private void UpdateTimeDisplay()
        {
            // ENHANCED: Show both minutes and seconds from the start
            var totalMinutes = (int)_duration.Minutes;
            var totalSeconds = (int)_duration.Seconds;
            
            if (_duration.TotalMinutes >= 1)
            {
                TimeRemainingText.Text = $"{totalMinutes}m {totalSeconds}s remaining";
            }
            else
            {
                TimeRemainingText.Text = $"{totalSeconds} second{(totalSeconds != 1 ? "s" : "")} remaining";
            }
        }

        public void StopCountdown()
        {
            if (_progressTimer != null)
            {
                var elapsed = DateTime.Now - _startTime;
                System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup.StopCountdown: Timer stopped after {elapsed.TotalMinutes:F1} minutes (expected {_duration.TotalMinutes} minutes)");
                
                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick; // CRITICAL FIX: Remove event handler to prevent memory leaks
                _progressTimer = null; // CRITICAL FIX: Null the timer to prevent reuse
            }
        }

        private void StretchingResource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    // Log error but don't crash the application
                    System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
                }
            }
        }

        private void DelayOneMinute_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.DelayOneMinute);
        }

        private void DelayFiveMinutes_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.DelayFiveMinutes);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.Skipped);
        }

    }
}