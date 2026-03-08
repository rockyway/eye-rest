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
        private const double CompactTransitionSeconds = 30.0;
        private const int FadeSteps = 15; // ~250ms at 16ms per tick

        private TimeSpan _totalDuration;
        private DispatcherTimer? _smoothAnimationTimer;
        private DispatcherTimer? _selfCountdownTimer;
        private DateTime _countdownStartTime;
        private double _targetProgressValue;
        private double _animStartValue;
        private DateTime _animStartTime;
        private TimeSpan _animDuration;
        private Window? _parentWindow;
        private bool _isCompact;
        private DispatcherTimer? _fadeTimer;
        private int _fadeStep;
        private bool _fadingOut; // true = fading out full, false = fading in compact

        public event EventHandler? Completed;

        public BreakWarningPopup()
        {
            InitializeComponent();

            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            Loaded += OnLoaded;
            CloseButton.Click += CloseButton_Click;
            CompactCloseButton.Click += CloseButton_Click;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                _parentWindow = window;
                _parentWindow.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            }

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
            _isCompact = false;

            Debug.WriteLine($"BreakWarningPopup: Starting countdown for {timeUntilBreak.TotalSeconds} seconds");

            // Ensure full layout is visible
            FullLayout.IsVisible = true;
            FullLayout.Opacity = 1;
            CompactLayout.IsVisible = false;
            CompactLayout.Opacity = 0;

            UpdateDisplay(timeUntilBreak);
            ProgressBar.Value = 100;

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

            // Check if we should transition to compact mode
            CheckCompactTransition(elapsed);

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
        /// </summary>
        public void UpdateCountdown(TimeSpan remaining)
        {
            StopSelfCountdown();

            Debug.WriteLine($"BreakWarningPopup: UpdateCountdown called with {remaining.TotalSeconds} seconds remaining");

            if (remaining <= TimeSpan.Zero)
            {
                AnimateProgressTo(0, TimeSpan.FromMilliseconds(200));
                if (_isCompact)
                    CompactCountdownText.Text = "Starting now!";
                else
                    CountdownText.Text = "Break starting now!";

                Debug.WriteLine("BreakWarningPopup: Countdown complete - firing Completed event");
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var elapsed = _totalDuration - remaining;
            CheckCompactTransition(elapsed);

            UpdateDisplay(remaining);
        }

        private void CheckCompactTransition(TimeSpan elapsed)
        {
            if (!_isCompact && _totalDuration.TotalSeconds > CompactTransitionSeconds
                && elapsed.TotalSeconds >= CompactTransitionSeconds)
            {
                TransitionToCompact();
            }
        }

        private void TransitionToCompact()
        {
            if (_isCompact) return;
            _isCompact = true;

            Debug.WriteLine("BreakWarningPopup: Transitioning to compact mode");

            // Sync compact progress bar with current value
            CompactProgressBar.Value = ProgressBar.Value;

            // Start fade-out of full layout
            _fadingOut = true;
            _fadeStep = 0;
            StopFadeTimer();
            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _fadeTimer.Tick += OnFadeTick;
            _fadeTimer.Start();
        }

        private void OnFadeTick(object? sender, EventArgs e)
        {
            _fadeStep++;
            var progress = Math.Min((double)_fadeStep / FadeSteps, 1.0);

            if (_fadingOut)
            {
                // Fade out full layout
                FullLayout.Opacity = 1.0 - progress;

                if (progress >= 1.0)
                {
                    FullLayout.IsVisible = false;
                    CompactLayout.IsVisible = true;
                    CompactLayout.Opacity = 0;

                    // Switch to fade-in
                    _fadingOut = false;
                    _fadeStep = 0;
                }
            }
            else
            {
                // Fade in compact layout
                CompactLayout.Opacity = progress;

                if (progress >= 1.0)
                {
                    StopFadeTimer();
                }
            }
        }

        private void StopFadeTimer()
        {
            if (_fadeTimer != null)
            {
                _fadeTimer.Stop();
                _fadeTimer.Tick -= OnFadeTick;
                _fadeTimer = null;
            }
        }

        private void UpdateDisplay(TimeSpan remaining)
        {
            var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            var text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";

            if (_isCompact)
            {
                CompactCountdownText.Text = text;
            }
            else
            {
                CountdownText.Text = text;
            }

            if (_totalDuration.TotalSeconds > 0)
            {
                var progressPercent = (remaining.TotalSeconds / _totalDuration.TotalSeconds) * 100;
                var targetValue = Math.Max(progressPercent, 0);

                AnimateProgressTo(targetValue, TimeSpan.FromMilliseconds(100));
            }
        }

        private void AnimateProgressTo(double targetValue, TimeSpan duration)
        {
            _targetProgressValue = targetValue;
            _animStartValue = _isCompact ? CompactProgressBar.Value : ProgressBar.Value;
            _animStartTime = DateTime.Now;
            _animDuration = duration;

            if (_smoothAnimationTimer == null)
            {
                _smoothAnimationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
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

            var value = _animStartValue + (_targetProgressValue - _animStartValue) * progress;

            if (_isCompact)
                CompactProgressBar.Value = value;
            else
                ProgressBar.Value = value;

            if (progress >= 1.0)
            {
                _smoothAnimationTimer?.Stop();
                if (_isCompact)
                    CompactProgressBar.Value = _targetProgressValue;
                else
                    ProgressBar.Value = _targetProgressValue;
            }
        }

        public void StopCountdown()
        {
            StopSelfCountdown();
            StopFadeTimer();

            if (_smoothAnimationTimer != null)
            {
                _smoothAnimationTimer.Stop();
                _smoothAnimationTimer.Tick -= OnSmoothAnimationTick;
                _smoothAnimationTimer = null;
            }

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
