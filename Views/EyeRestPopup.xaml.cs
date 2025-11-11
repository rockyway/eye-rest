using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace EyeRest.Views
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
            
            // CRITICAL FIX: Robust input handling with multiple fallback mechanisms
            Loaded += (s, e) =>
            {
                try
                {
                    // Primary: Window-level key handling
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.PreviewKeyDown += Window_PreviewKeyDown;
                        System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: Window key handler attached to {window.GetType().Name}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"🚨 EyeRestPopup: Window.GetWindow returned null - input handling may be compromised");
                    }
                    
                    // BACKUP: Direct UserControl key handling as fallback
                    this.PreviewKeyDown += UserControl_PreviewKeyDown;
                    this.KeyDown += UserControl_KeyDown;
                    
                    // CRITICAL: Ensure focus and input capability
                    this.Focusable = true;
                    this.IsTabStop = true;
                    this.Focus();
                    
                    System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: Comprehensive input handling initialized");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"🚨 EyeRestPopup: Error setting up input handling: {ex.Message}");
                }
            };
        }

        public void StartCountdown(TimeSpan duration)
        {
            // CRITICAL FIX: Clean up existing timer before creating new one
            StopCountdown();
            
            _duration = duration;
            _startTime = DateTime.Now;
            
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestPopup.StartCountdown: Starting {duration.TotalSeconds} second eye rest");
            System.Diagnostics.Debug.WriteLine($"👁 EyeRestPopup: Timer should complete at {DateTime.Now.Add(duration):HH:mm:ss}");
            
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
                // Countdown complete - animate to 100%
                // CRITICAL FIX: Properly stop and cleanup timer to prevent multiple events
                if (_progressTimer != null)
                {
                    _progressTimer.Stop();
                    _progressTimer.Tick -= OnProgressTimerTick; // Remove event handler to prevent memory leaks
                    _progressTimer = null; // Null the timer to prevent reuse
                }
                
                var completionAnimation = new DoubleAnimation
                {
                    From = ProgressBar.Value,
                    To = 100,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, completionAnimation);
                TimeRemainingText.Text = "Eye rest complete!";
                
                System.Diagnostics.Debug.WriteLine($"👁 EyeRestPopup: Timer completed successfully after {_duration.TotalSeconds} seconds");
                Completed?.Invoke(this, EventArgs.Empty);
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
                System.Diagnostics.Debug.WriteLine($"👁 EyeRestPopup.StopCountdown: Timer stopped after {elapsed.TotalSeconds:F1} seconds (expected {_duration.TotalSeconds} seconds)");
                
                _progressTimer.Stop();
                _progressTimer.Tick -= OnProgressTimerTick; // CRITICAL FIX: Remove event handler to prevent memory leaks
                _progressTimer = null; // CRITICAL FIX: Null the timer to prevent reuse
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: ESC key pressed via Window_PreviewKeyDown");
                HandleEscapeKey();
                e.Handled = true;
            }
        }
        
        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: ESC key pressed via UserControl_PreviewKeyDown");
                HandleEscapeKey();
                e.Handled = true;
            }
        }
        
        private void UserControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: ESC key pressed via UserControl_KeyDown");
                HandleEscapeKey();
                e.Handled = true;
            }
        }
        
        private void HandleEscapeKey()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: HandleEscapeKey called - stopping countdown and closing");
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🚨 EyeRestPopup: Error handling escape key: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔑 EyeRestPopup: Close button clicked - stopping countdown and closing");
                StopCountdown();
                Completed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🚨 EyeRestPopup: Error handling close button click: {ex.Message}");
            }
        }
    }
}