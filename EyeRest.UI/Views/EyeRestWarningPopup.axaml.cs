using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EyeRest.UI.Views
{
    public partial class EyeRestWarningPopup : UserControl
    {
        private TimeSpan _totalDuration;
        private DispatcherTimer? _smoothAnimationTimer;
        private double _targetProgressValue;

        public event EventHandler? WarningCompleted;

        public EyeRestWarningPopup()
        {
            InitializeComponent();

            // ESC key handling using tunnel strategy
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            Loaded += OnLoaded;
            CloseButton.Click += CloseButton_Click;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Also attach to the top-level window for ESC handling
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                window.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
                Debug.WriteLine("EyeRestWarningPopup: Window key handler attached");
            }
        }

        public void StartCountdown(int seconds)
        {
            _totalDuration = TimeSpan.FromSeconds(seconds);

            Debug.WriteLine($"EyeRestWarningPopup: Starting display-only countdown for {seconds} seconds");

            // Initialize display - no internal timer
            UpdateDisplay(TimeSpan.FromSeconds(seconds));
            ProgressBar.Value = 100; // Start at 100%
        }

        /// <summary>
        /// Update the countdown display externally (called by TimerService)
        /// </summary>
        public void UpdateCountdown(TimeSpan remaining)
        {
            Debug.WriteLine($"EyeRestWarningPopup: UpdateCountdown called with {remaining.TotalSeconds} seconds remaining");

            if (remaining <= TimeSpan.Zero)
            {
                // Final state - animate to 0
                AnimateProgressTo(0, TimeSpan.FromMilliseconds(200));
                CountdownText.Text = "Eye rest starting now!";

                Debug.WriteLine("EyeRestWarningPopup: Countdown complete - firing WarningCompleted event");
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
                AnimateProgressTo(targetValue, TimeSpan.FromMilliseconds(100));
            }
        }

        /// <summary>
        /// Animate progress bar value smoothly using DispatcherTimer
        /// </summary>
        private void AnimateProgressTo(double targetValue, TimeSpan duration)
        {
            _smoothAnimationTimer?.Stop();
            _targetProgressValue = targetValue;

            var startValue = ProgressBar.Value;
            var startTime = DateTime.Now;

            _smoothAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };

            _smoothAnimationTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - startTime;
                var progress = Math.Min(elapsed.TotalMilliseconds / duration.TotalMilliseconds, 1.0);

                // Ease-out quadratic
                progress = 1.0 - (1.0 - progress) * (1.0 - progress);

                ProgressBar.Value = startValue + (targetValue - startValue) * progress;

                if (progress >= 1.0)
                {
                    _smoothAnimationTimer?.Stop();
                    _smoothAnimationTimer = null;
                    ProgressBar.Value = targetValue;
                }
            };

            _smoothAnimationTimer.Start();
        }

        public void StopCountdown()
        {
            _smoothAnimationTimer?.Stop();
            _smoothAnimationTimer = null;
            Debug.WriteLine("EyeRestWarningPopup: StopCountdown called");
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                StopCountdown();
                WarningCompleted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            StopCountdown();
            WarningCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
