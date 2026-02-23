using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EyeRest.UI.Views
{
    public partial class EyeRestPopup : UserControl
    {
        private DispatcherTimer? _progressTimer;
        private TimeSpan _duration;
        private DateTime _startTime;

        public event EventHandler? Completed;

        public EyeRestPopup()
        {
            InitializeComponent();

            // ESC key handling using tunnel strategy (equivalent to WPF PreviewKeyDown)
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

            Loaded += OnLoaded;
            CloseButton.Click += CloseButton_Click;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Primary: Window-level key handling
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is Window window)
                {
                    window.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
                    Debug.WriteLine($"EyeRestPopup: Window key handler attached to {window.GetType().Name}");
                }
                else
                {
                    Debug.WriteLine("EyeRestPopup: TopLevel returned null - input handling may be compromised");
                }

                // Ensure focus and input capability
                Focusable = true;
                Focus();

                Debug.WriteLine("EyeRestPopup: Comprehensive input handling initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EyeRestPopup: Error setting up input handling: {ex.Message}");
            }
        }

        public void StartCountdown(TimeSpan duration)
        {
            // Clean up existing timer before creating new one
            StopCountdown();

            _duration = duration;
            _startTime = DateTime.Now;

            Debug.WriteLine($"EyeRestPopup.StartCountdown: Starting {duration.TotalSeconds} second eye rest");
            Debug.WriteLine($"EyeRestPopup: Timer should complete at {DateTime.Now.Add(duration):HH:mm:ss}");

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
                // Countdown complete
                if (_progressTimer != null)
                {
                    _progressTimer.Stop();
                    _progressTimer.Tick -= OnProgressTimerTick;
                    _progressTimer = null;
                }

                ProgressBar.Value = 100;
                TimeRemainingText.Text = "Eye rest complete!";

                Debug.WriteLine($"EyeRestPopup: Timer completed successfully after {_duration.TotalSeconds} seconds");
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Update progress bar
            var progressPercent = (elapsed.TotalMilliseconds / _duration.TotalMilliseconds) * 100;
            var targetValue = Math.Min(progressPercent, 100);
            ProgressBar.Value = targetValue;

            // Update time display
            var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            TimeRemainingText.Text = $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")} remaining";
        }

        private void UpdateTimeDisplay()
        {
            var totalSeconds = (int)_duration.TotalSeconds;
            TimeRemainingText.Text = $"{totalSeconds} second{(totalSeconds != 1 ? "s" : "")} remaining";
        }

        public void StopCountdown()
        {
            if (_progressTimer != null)
            {
                var elapsed = DateTime.Now - _startTime;
                Debug.WriteLine($"EyeRestPopup.StopCountdown: Timer stopped after {elapsed.TotalSeconds:F1} seconds (expected {_duration.TotalSeconds} seconds)");

                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick;
                _progressTimer = null;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Debug.WriteLine("EyeRestPopup: ESC key pressed");
                HandleEscapeKey();
                e.Handled = true;
            }
        }

        private void HandleEscapeKey()
        {
            try
            {
                Debug.WriteLine("EyeRestPopup: HandleEscapeKey called - stopping countdown and closing");
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EyeRestPopup: Error handling escape key: {ex.Message}");
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("EyeRestPopup: Close button clicked - stopping countdown and closing");
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EyeRestPopup: Error handling close button click: {ex.Message}");
            }
        }
    }
}
