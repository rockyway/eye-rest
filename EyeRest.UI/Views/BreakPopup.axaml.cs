using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EyeRest.Services;

namespace EyeRest.UI.Views
{
    public partial class BreakPopup : UserControl
    {
        private DispatcherTimer? _progressTimer;
        private DispatcherTimer? _forwardTimerDisplay;  // Forward timer on Done screen
        private TimeSpan _duration;
        private DateTime _startTime;
        private DateTime _doneScreenStartTime;  // When Done screen was shown
        private IProgress<double>? _progress;
        private bool _requireConfirmationAfterBreak;
        private bool _resetTimersOnConfirmation;
        private bool _waitingForConfirmation = false;  // Track if waiting for user confirmation
        private bool _forceClose = false;  // Allow forcing the window to close when user confirms
        private bool _isShowingForwardTimer = false;  // Track if forward timer is active

        public event EventHandler<BreakAction>? ActionSelected;

        public BreakPopup()
        {
            InitializeComponent();

            // Wire up button click handlers
            StretchingResource1.Click += StretchingResource_Click;
            DelayOneMinuteButton.Click += DelayOneMinute_Click;
            DelayFiveMinutesButton.Click += DelayFiveMinutes_Click;
            SkipButton.Click += Skip_Click;
            ConfirmationButton.Click += ConfirmCompletion_Click;
        }

        public void SetConfiguration(bool requireConfirmationAfterBreak, bool resetTimersOnConfirmation)
        {
            _requireConfirmationAfterBreak = requireConfirmationAfterBreak;
            _resetTimersOnConfirmation = resetTimersOnConfirmation;

            // Update confirmation button text based on reset timer setting
            if (_resetTimersOnConfirmation)
            {
                ConfirmationButton.Content = "Done - Start Fresh Session";
                ReturnInstructionText.Text = "Click 'Done' when you return to start a fresh session";
            }
            else
            {
                ConfirmationButton.Content = "Done - Resume Timers";
                ReturnInstructionText.Text = "Click 'Done' when you return to resume your timers";
            }
        }

        /// <summary>
        /// Configures the "LAST" chip + tooltip on the delay buttons. When the next delay click would
        /// be the final allowed one before the limit forces a session reset, both delay buttons
        /// surface a red "LAST" chip and a descriptive tooltip. Pass maxDelays = 0 to disable
        /// (e.g. test mode or unlimited-delay configuration).
        /// </summary>
        public void SetDelayChipState(int consecutiveDelayCount, int maxDelays)
        {
            // maxDelays == 0 means "unlimited" — no chip, default tooltip
            // We show "LAST" when this delay click would be the final one before the limit kicks in.
            // The limit fires when count *exceeds* maxDelays, so the click that takes count from
            // (maxDelays - 1) → maxDelays is the last allowed one.
            bool isLast = maxDelays > 0 && consecutiveDelayCount >= maxDelays - 1;

            DelayOneMinuteLastChip.IsVisible = isLast;
            DelayFiveMinutesLastChip.IsVisible = isLast;

            string oneMinTip, fiveMinTip;
            if (isLast)
            {
                oneMinTip = $"LAST CHANCE — this is your final allowed delay ({consecutiveDelayCount + 1}/{maxDelays}). After this, the next delay attempt will force a fresh session reset.";
                fiveMinTip = oneMinTip;
            }
            else if (maxDelays > 0)
            {
                int remaining = maxDelays - consecutiveDelayCount;
                oneMinTip = $"Delay this break by 1 minute. ({remaining} delays remaining out of {maxDelays} max)";
                fiveMinTip = $"Delay this break by 5 minutes. ({remaining} delays remaining out of {maxDelays} max)";
            }
            else
            {
                oneMinTip = "Delay this break by 1 minute.";
                fiveMinTip = "Delay this break by 5 minutes.";
            }

            ToolTip.SetTip(DelayOneMinuteButton, oneMinTip);
            ToolTip.SetTip(DelayFiveMinutesButton, fiveMinTip);
        }

        public void StartCountdown(TimeSpan duration, IProgress<double>? progress = null)
        {
            // Clean up existing timer before creating new one
            StopCountdown();

            _duration = duration;
            _startTime = DateTime.Now;
            _progress = progress;

            Debug.WriteLine($"BreakPopup.StartCountdown: Starting {duration.TotalMinutes:F1} minute break countdown");

            UpdateTimeDisplay();

            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // Update 10 times per second for smooth animation
            };

            _progressTimer.Tick += OnProgressTimerTick;
            _progressTimer.Start();

            Debug.WriteLine($"BreakPopup.StartCountdown: Timer started successfully, should run for {duration.TotalMinutes:F1} minutes");
        }

        private void OnProgressTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            var remaining = _duration - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                // Break complete - set to 100%
                _progressTimer?.Stop();
                if (_progressTimer != null)
                {
                    _progressTimer.Tick -= OnProgressTimerTick;
                    _progressTimer = null;
                }

                ProgressBar.Value = 100;
                _progress?.Report(1.0);

                // Show completion state
                ShowCompletionState();
                return;
            }

            // Update progress bar
            var progressPercent = (elapsed.TotalMilliseconds / _duration.TotalMilliseconds) * 100;
            var targetValue = Math.Min(progressPercent, 100);
            ProgressBar.Value = targetValue;
            _progress?.Report(progressPercent / 100.0);

            // Update large minute and second displays for visibility from distance
            var remainingMinutes = (int)remaining.TotalMinutes;
            var remainingSeconds = (int)remaining.Seconds;

            // Update the large displays
            MinutesDisplay.Text = remainingMinutes.ToString();
            SecondsDisplay.Text = remainingSeconds.ToString("00");

            // Also update the smaller text display for consistency
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
            Debug.WriteLine("BreakPopup.ShowCompletionState: Break completed successfully - showing completion state");
            Debug.WriteLine($"BreakPopup.ShowCompletionState: _requireConfirmationAfterBreak={_requireConfirmationAfterBreak}");

            // Change background to semi-transparent green tint for Done screen (preserves glass-morphism)
            OuterBorder.Background = new SolidColorBrush(Color.FromArgb(30, 34, 197, 94));  // Subtle green glass tint

            // Update title to "Break Complete - Continue when ready"
            MainMessageText.Text = "Break Complete \u2013 Continue when ready";

            TimeRemainingText.Text = "Break complete! Great job!";

            // Hide action buttons
            DelayOneMinuteButton.IsVisible = false;
            DelayFiveMinutesButton.IsVisible = false;
            SkipButton.IsVisible = false;

            if (_requireConfirmationAfterBreak)
            {
                // Show confirmation button and wait for user to click it
                _waitingForConfirmation = true;
                ConfirmationButton.IsVisible = true;
                ReturnInstructionText.IsVisible = true;

                Debug.WriteLine("BreakPopup: RequireConfirmationAfterBreak enabled - showing confirmation UI and waiting for user to click");
                Debug.WriteLine("BreakPopup: _waitingForConfirmation=true - popup MUST NOT AUTO-CLOSE");

                // Record Done screen start time for forward timer
                _doneScreenStartTime = DateTime.Now;

                // Start forward timer after 10-second initial wait
                var initialWaitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };

                initialWaitTimer.Tick += (s, e) =>
                {
                    initialWaitTimer.Stop();
                    Debug.WriteLine("BreakPopup: 10-second wait completed - starting forward timer");
                    StartForwardTimer();
                };

                initialWaitTimer.Start();

                // Force parent window to foreground when showing confirmation
                EnsureConfirmationButtonVisible();
            }
            else
            {
                // Original behavior - auto-close after 2 seconds
                var closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };

                closeTimer.Tick += (s, e) =>
                {
                    closeTimer.Stop();
                    Debug.WriteLine("BreakPopup: Auto-closing after completion, firing ActionSelected.Completed");
                    ActionSelected?.Invoke(this, BreakAction.Completed);
                };

                closeTimer.Start();
            }
        }

        /// <summary>
        /// Start forward timer on Done screen showing elapsed time since Done was shown.
        /// Timer starts at 0:10 and counts upward.
        /// </summary>
        private void StartForwardTimer()
        {
            if (_isShowingForwardTimer)
            {
                Debug.WriteLine("BreakPopup: Forward timer already running");
                return;
            }

            _isShowingForwardTimer = true;
            Debug.WriteLine("BreakPopup: Starting forward timer display");

            // Update label to show we're counting up
            TimerLabelText.Text = "time extended";

            // Initialize with 0:10
            MinutesDisplay.Text = "0";
            SecondsDisplay.Text = "10";
            TimeRemainingText.Text = "0 minutes 10 seconds extended";

            _forwardTimerDisplay = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)  // Update frequently for smooth display
            };

            _forwardTimerDisplay.Tick += (s, e) =>
            {
                if (_doneScreenStartTime == DateTime.MinValue)
                {
                    return;  // Safety check
                }

                var elapsedSinceDone = DateTime.Now - _doneScreenStartTime;

                var minutes = (int)elapsedSinceDone.TotalMinutes;
                var seconds = elapsedSinceDone.Seconds;

                // Update display
                MinutesDisplay.Text = minutes.ToString();
                SecondsDisplay.Text = seconds.ToString("00");

                // Update text
                var minuteLabel = minutes == 1 ? "minute" : "minutes";
                var secondLabel = seconds == 1 ? "second" : "seconds";
                TimeRemainingText.Text = $"{minutes} {minuteLabel} {seconds} {secondLabel} extended";

                Debug.WriteLine($"BreakPopup: Forward timer: {minutes}:{seconds:00}");
            };

            _forwardTimerDisplay.Start();
        }

        /// <summary>
        /// Stop forward timer when user clicks Done
        /// </summary>
        private void StopForwardTimer()
        {
            if (_forwardTimerDisplay != null)
            {
                _forwardTimerDisplay.Stop();
                _forwardTimerDisplay = null;
                _isShowingForwardTimer = false;
                Debug.WriteLine("BreakPopup: Forward timer stopped");
            }
        }

        private void UpdateTimeDisplay()
        {
            // Initialize large displays with full duration for visibility from distance
            var totalMinutes = (int)_duration.TotalMinutes;
            var totalSeconds = (int)_duration.Seconds;

            // Set initial large display values
            MinutesDisplay.Text = totalMinutes.ToString();
            SecondsDisplay.Text = totalSeconds.ToString("00");

            // Also set the smaller text display
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
                Debug.WriteLine($"BreakPopup.StopCountdown: Timer stopped after {elapsed.TotalMinutes:F1} minutes (expected {_duration.TotalMinutes} minutes)");

                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick;
                _progressTimer = null;
            }

            // Also stop forward timer if running
            StopForwardTimer();
        }

        private void StretchingResource_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open URL: {ex.Message}");
                }
            }
        }

        private void DelayOneMinute_Click(object? sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.DelayOneMinute);
        }

        private void DelayFiveMinutes_Click(object? sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.DelayFiveMinutes);
        }

        private void Skip_Click(object? sender, RoutedEventArgs e)
        {
            StopCountdown();
            ActionSelected?.Invoke(this, BreakAction.Skipped);
        }

        /// <summary>
        /// Ensure confirmation button is visible and parent window is in foreground.
        /// Essential for post-resume popup visibility.
        /// </summary>
        private void EnsureConfirmationButtonVisible()
        {
            try
            {
                Debug.WriteLine("BreakPopup: Ensuring confirmation button visibility");

                // Make sure UI elements are visible
                ConfirmationButton.IsVisible = true;
                ReturnInstructionText.IsVisible = true;

                // Force parent window to foreground when showing confirmation
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is Window window)
                {
                    Debug.WriteLine("BreakPopup: Activating parent window");
                    window.Activate();
                    window.Topmost = true;
                }

                // Also ensure the confirmation button itself can receive focus
                ConfirmationButton.Focus();

                Debug.WriteLine("BreakPopup: Confirmation visibility ensured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BreakPopup: Error ensuring confirmation visibility: {ex.Message}");
            }
        }

        private void ConfirmCompletion_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("BreakPopup.ConfirmCompletion_Click: User confirmed break completion");

            // Stop forward timer before closing
            StopForwardTimer();

            _waitingForConfirmation = false;  // Clear flag to allow window to close
            _forceClose = true;  // Force the window to close when user confirms

            // Directly close the parent window to ensure it actually closes
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                Debug.WriteLine("BreakPopup: Directly closing parent window");
                try
                {
                    // Fire the event first to notify listeners
                    ActionSelected?.Invoke(this, BreakAction.ConfirmedAfterCompletion);

                    // Then immediately close the window
                    window.Close();
                    Debug.WriteLine("BreakPopup: Parent window Close() called");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BreakPopup: Error closing parent window: {ex.Message}");
                    // Still fire the event even if close fails
                    ActionSelected?.Invoke(this, BreakAction.ConfirmedAfterCompletion);
                }
            }
            else
            {
                Debug.WriteLine("BreakPopup: No parent window found, just firing event");
                ActionSelected?.Invoke(this, BreakAction.ConfirmedAfterCompletion);
            }
        }

        // Method to check if popup can be closed
        public bool CanClose()
        {
            // Allow closing if force close is set (user clicked Done) or not waiting for confirmation
            return _forceClose || !_waitingForConfirmation;
        }
    }
}
