using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EyeRest.UI.Views
{
    public partial class BreakWarningPopup : UserControl
    {
        private TimeSpan _totalDuration;
        private DispatcherTimer? _smoothAnimationTimer;
        private DispatcherTimer? _selfCountdownTimer;
        private DateTime _countdownStartTime;
        private double _targetProgressValue;
        private double _animStartValue;
        private DateTime _animStartTime;
        private TimeSpan _animDuration;
        private Window? _parentWindow;

        public event EventHandler? Completed;

        public BreakWarningPopup()
        {
            InitializeComponent();

            // ESC key handling using tunnel strategy
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            Loaded += OnLoaded;
            CloseButton.Click += CloseButton_Click;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                _parentWindow = window;
                _parentWindow.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            }

            // Subscribe to Unloaded for cleanup
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                _parentWindow.RemoveHandler(KeyDownEvent, OnKeyDown);
                _parentWindow = null;
            }
            this.Unloaded -= OnUnloaded;
        }

        public void StartCountdown(TimeSpan timeUntilBreak)
        {
            _totalDuration = timeUntilBreak;

            Debug.WriteLine($"BreakWarningPopup: Starting countdown for {timeUntilBreak.TotalSeconds} seconds");

            // Initialize display
            UpdateDisplay(timeUntilBreak);
            ProgressBar.Value = 100; // Start at 100%

            // Start self-driving countdown timer so the popup counts down
            // even in test mode (when TimerService doesn't drive updates)
            StopSelfCountdown();
            _countdownStartTime = DateTime.UtcNow;
            _selfCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _selfCountdownTimer.Tick += OnSelfCountdownTick;
            _selfCountdownTimer.Start();
        }

        private void OnSelfCountdownTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow - _countdownStartTime;
            var remaining = _totalDuration - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                StopSelfCountdown();
                UpdateCountdown(TimeSpan.Zero);
                return;
            }

            UpdateDisplay(remaining);
        }

        private void StopSelfCountdown()
        {
            if (_selfCountdownTimer != null)
            {
                _selfCountdownTimer.Stop();
                _selfCountdownTimer.Tick -= OnSelfCountdownTick;
                _selfCountdownTimer = null;
            }
        }

        /// <summary>
        /// Update the countdown display externally (called by TimerService).
        /// Stops the self-driving timer since TimerService is now driving updates.
        /// </summary>
        public void UpdateCountdown(TimeSpan remaining)
        {
            StopSelfCountdown();

            Debug.WriteLine($"BreakWarningPopup: UpdateCountdown called with {remaining.TotalSeconds} seconds remaining");

            if (remaining <= TimeSpan.Zero)
            {
                // Final state - animate to 0
                AnimateProgressTo(0, TimeSpan.FromMilliseconds(200));
                CountdownText.Text = "Break starting now!";

                Debug.WriteLine("BreakWarningPopup: Countdown complete - firing Completed event");
                Completed?.Invoke(this, EventArgs.Empty);
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
        /// Animate progress bar value smoothly using a single reusable DispatcherTimer
        /// </summary>
        private void AnimateProgressTo(double targetValue, TimeSpan duration)
        {
            _targetProgressValue = targetValue;
            _animStartValue = ProgressBar.Value;
            _animStartTime = DateTime.Now;
            _animDuration = duration;

            if (_smoothAnimationTimer == null)
            {
                _smoothAnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60fps
                };
                _smoothAnimationTimer.Tick += OnSmoothAnimationTick;
            }
            else
            {
                _smoothAnimationTimer.Stop();
            }

            _smoothAnimationTimer.Start();
        }

        private void OnSmoothAnimationTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _animStartTime;
            var progress = Math.Min(elapsed.TotalMilliseconds / _animDuration.TotalMilliseconds, 1.0);

            // Ease-out quadratic
            progress = 1.0 - (1.0 - progress) * (1.0 - progress);

            ProgressBar.Value = _animStartValue + (_targetProgressValue - _animStartValue) * progress;

            if (progress >= 1.0)
            {
                _smoothAnimationTimer?.Stop();
                ProgressBar.Value = _targetProgressValue;
            }
        }

        public void StopCountdown()
        {
            StopSelfCountdown();

            if (_smoothAnimationTimer != null)
            {
                _smoothAnimationTimer.Stop();
                _smoothAnimationTimer.Tick -= OnSmoothAnimationTick;
                _smoothAnimationTimer = null;
            }

            // Clean up window key handler
            if (_parentWindow != null)
            {
                _parentWindow.RemoveHandler(KeyDownEvent, OnKeyDown);
                _parentWindow = null;
            }

            Debug.WriteLine("BreakWarningPopup: StopCountdown called");
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            StopCountdown();
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
