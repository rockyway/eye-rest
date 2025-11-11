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
            
            // ENHANCED: Update large minute and second displays for visibility from distance
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
            System.Diagnostics.Debug.WriteLine("🔥 BreakPopup.ShowCompletionState: Break completed successfully - showing completion state");
            System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup.ShowCompletionState: _requireConfirmationAfterBreak={_requireConfirmationAfterBreak}");

            // ENHANCEMENT: Change background to light blue for Done screen
            var border = (Border)Content;
            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(173, 216, 230));  // Light blue

            // ENHANCEMENT: Update title to "Break Complete – Continue when ready"
            MainMessageText.Text = "Break Complete – Continue when ready";

            TimeRemainingText.Text = "Break complete! Great job!";

            // Hide action buttons
            DelayOneMinuteButton.Visibility = Visibility.Collapsed;
            DelayFiveMinutesButton.Visibility = Visibility.Collapsed;
            SkipButton.Visibility = Visibility.Collapsed;

            if (_requireConfirmationAfterBreak)
            {
                // CRITICAL FIX: Show confirmation button and wait for user to click it
                // DO NOT fire the Completed event immediately - wait for user confirmation
                _waitingForConfirmation = true;  // Set flag to prevent window from closing
                ConfirmationButton.Visibility = Visibility.Visible;
                ReturnInstructionText.Visibility = Visibility.Visible;

                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: RequireConfirmationAfterBreak enabled - showing confirmation UI and waiting for user to click");
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: _waitingForConfirmation=true - popup MUST NOT AUTO-CLOSE");

                // ENHANCEMENT: Record Done screen start time for forward timer
                _doneScreenStartTime = DateTime.Now;

                // ENHANCEMENT: Start forward timer after 10-second initial wait
                var initialWaitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };

                initialWaitTimer.Tick += (s, e) =>
                {
                    initialWaitTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: 10-second wait completed - starting forward timer");
                    StartForwardTimer();
                };

                initialWaitTimer.Start();

                // CRITICAL FIX: Force parent window to foreground when showing confirmation
                EnsureConfirmationButtonVisible();

                // FIXED: Don't fire any event here - let the user click the confirmation button
                // The ConfirmCompletion_Click handler will fire BreakAction.ConfirmedAfterCompletion
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
                    System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Auto-closing after completion, firing ActionSelected.Completed");
                    ActionSelected?.Invoke(this, BreakAction.Completed);
                };

                closeTimer.Start();
            }
        }

        /// <summary>
        /// ENHANCEMENT: Start forward timer on Done screen showing elapsed time since Done was shown
        /// Timer starts at 0:10 and counts upward
        /// </summary>
        private void StartForwardTimer()
        {
            if (_isShowingForwardTimer)
            {
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Forward timer already running");
                return;
            }

            _isShowingForwardTimer = true;
            System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Starting forward timer display");

            // Update label to show we're counting up
            TimerLabelText.Text = "time extended";

            // Initialize with 0:10
            MinutesDisplay.Text = "0";
            SecondsDisplay.Text = "10";
            TimeRemainingText.Text = "0 minutes 10 seconds extended";

            _forwardTimerDisplay = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)  // Update 10 times per second for smooth display
            };

            _forwardTimerDisplay.Tick += (s, e) =>
            {
                if (_doneScreenStartTime == DateTime.MinValue)
                {
                    return;  // Safety check
                }

                // Calculate elapsed time since Done screen was shown (plus 10 seconds)
                var elapsedSinceDone = DateTime.Now - _doneScreenStartTime;
                var totalExtendedTime = elapsedSinceDone.Add(TimeSpan.FromSeconds(10));

                var minutes = (int)totalExtendedTime.TotalMinutes;
                var seconds = (int)totalExtendedTime.Seconds;

                // Update display
                MinutesDisplay.Text = minutes.ToString();
                SecondsDisplay.Text = seconds.ToString("00");

                // Update text
                var minuteLabel = minutes == 1 ? "minute" : "minutes";
                var secondLabel = seconds == 1 ? "second" : "seconds";
                TimeRemainingText.Text = $"{minutes} {minuteLabel} {seconds} {secondLabel} extended";

                System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup: Forward timer: {minutes}:{seconds:00}");
            };

            _forwardTimerDisplay.Start();
        }

        /// <summary>
        /// ENHANCEMENT: Stop forward timer when user clicks Done
        /// </summary>
        private void StopForwardTimer()
        {
            if (_forwardTimerDisplay != null)
            {
                _forwardTimerDisplay.Stop();
                _forwardTimerDisplay = null;
                _isShowingForwardTimer = false;
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Forward timer stopped");
            }
        }

        private void UpdateTimeDisplay()
        {
            // ENHANCED: Initialize large displays with full duration for visibility from distance
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
                System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup.StopCountdown: Timer stopped after {elapsed.TotalMinutes:F1} minutes (expected {_duration.TotalMinutes} minutes)");

                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick; // CRITICAL FIX: Remove event handler to prevent memory leaks
                _progressTimer = null; // CRITICAL FIX: Null the timer to prevent reuse
            }

            // ENHANCEMENT: Also stop forward timer if running
            StopForwardTimer();
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

        /// <summary>
        /// CRITICAL FIX: Ensure confirmation button is visible and parent window is in foreground
        /// Essential for post-resume popup visibility
        /// </summary>
        private void EnsureConfirmationButtonVisible()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Ensuring confirmation button visibility");
                
                // Make sure UI elements are visible
                ConfirmationButton.Visibility = Visibility.Visible;
                ReturnInstructionText.Visibility = Visibility.Visible;
                
                // Force parent window to foreground when showing confirmation
                var parentWindow = Window.GetWindow(this);
                if (parentWindow is BasePopupWindow basePopup)
                {
                    System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Forcing parent BasePopupWindow to foreground");
                    
                    // Use the new ForceToForeground method to handle post-resume visibility issues
                    basePopup.ForceToForeground();
                }
                else if (parentWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Activating parent window (not BasePopupWindow)");
                    
                    // Fallback for other window types
                    parentWindow.Activate();
                    parentWindow.Focus();
                    parentWindow.Topmost = true;
                }
                
                // Also ensure the confirmation button itself can receive focus
                ConfirmationButton.Focus();
                
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Confirmation visibility ensured");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup: Error ensuring confirmation visibility: {ex.Message}");
            }
        }

        private void ConfirmCompletion_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔥 BreakPopup.ConfirmCompletion_Click: User confirmed break completion");

            // ENHANCEMENT: Stop forward timer before closing
            StopForwardTimer();

            _waitingForConfirmation = false;  // Clear flag to allow window to close
            _forceClose = true;  // CRITICAL FIX: Force the window to close when user confirms

            // CRITICAL FIX: Directly close the parent window to ensure it actually closes
            var parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Directly closing parent window");
                try
                {
                    // Fire the event first to notify listeners
                    ActionSelected?.Invoke(this, BreakAction.ConfirmedAfterCompletion);

                    // Then immediately close the window
                    parentWindow.Close();
                    System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: Parent window Close() called");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"🔥 BreakPopup: Error closing parent window: {ex.Message}");
                    // Still fire the event even if close fails
                    if (ActionSelected != null)
                    {
                        ActionSelected.Invoke(this, BreakAction.ConfirmedAfterCompletion);
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("🔥 BreakPopup: No parent window found, just firing event");
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