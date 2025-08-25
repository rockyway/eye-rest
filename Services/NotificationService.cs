using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EyeRest.Views;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly Dispatcher _dispatcher;
        private readonly IScreenOverlayService _screenOverlayService;
        private readonly IConfigurationService _configurationService;
        private BasePopupWindow? _currentPopup;
        private readonly object _lockObject = new object();
        private readonly object _popupLock = new object(); // POPUP FIX: Dedicated lock for popup instance management
        private bool _isClosing = false; // Track if we're in the process of closing
        private bool _overlayVisible = false; // Track if overlay is currently visible
        private bool _isTestMode = false; // Track if we're in test mode to prevent analytics recording
        
        // References to active warning popups for external countdown control
        private EyeRestWarningPopup? _activeEyeRestWarningPopup;
        private BreakWarningPopup? _activeBreakWarningPopup;
        private ITimerService? _timerService; // Injected later to avoid circular dependency
        
        // POPUP FIX: Thread-safe status check properties
        public bool IsBreakWarningActive 
        { 
            get 
            {
                lock (_popupLock)
                {
                    return _activeBreakWarningPopup != null;
                }
            }
        }
        
        public bool IsEyeRestWarningActive 
        { 
            get 
            {
                lock (_popupLock)
                {
                    return _activeEyeRestWarningPopup != null;
                }
            }
        }
        
        // POPUP FIX: Helper method to check if any popup is currently active
        public bool IsAnyPopupActive => IsEyeRestWarningActive || IsBreakWarningActive;

        public NotificationService(ILogger<NotificationService> logger, Dispatcher dispatcher, IScreenOverlayService screenOverlayService, IConfigurationService configurationService)
        {
            _logger = logger;
            _dispatcher = dispatcher;
            _screenOverlayService = screenOverlayService;
            _configurationService = configurationService;
            
            // CRITICAL FIX: Defensive initialization cleanup to prevent stale popup references
            // This prevents zombie popup issues from persisting across app restarts
            _activeBreakWarningPopup = null;
            _activeEyeRestWarningPopup = null;
            
            _logger.LogInformation("🧟 ZOMBIE FIX: NotificationService initialized with clean popup references");
        }

        // Inject TimerService after construction to avoid circular dependency
        public void SetTimerService(ITimerService timerService)
        {
            _timerService = timerService;
        }

        public async Task ShowEyeRestWarningAsync(TimeSpan timeUntilBreak)
        {
            try
            {
                _logger.LogInformation($"ShowEyeRestWarningAsync called with timeUntilBreak: {timeUntilBreak.TotalSeconds} seconds");
                
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        _logger.LogInformation("Entered dispatcher invoke for eye rest warning");
                        
                        // Close any existing popup
                        CloseCurrentPopup();

                        // CRITICAL FIX: Always create fresh popup to prevent stale event handlers
                        _logger.LogInformation("Creating fresh EyeRestWarningPopup to prevent stale event handlers");
                        var eyeRestWarningPopup = new EyeRestWarningPopup();
                        _activeEyeRestWarningPopup = eyeRestWarningPopup; // Store reference for external countdown control
                        _logger.LogInformation("Fresh EyeRestWarningPopup created and stored");
                        
                        // Create popup window with error handling
                        BasePopupWindow popupWindow;
                        try
                        {
                            _logger.LogInformation("Creating BasePopupWindow");
                            popupWindow = new BasePopupWindow();
                            _logger.LogInformation("BasePopupWindow created successfully");
                            
                            _logger.LogInformation("Setting content on popup window");
                            popupWindow.SetContent(eyeRestWarningPopup);
                            _logger.LogInformation("Content set successfully");
                            
                            _currentPopup = popupWindow;
                            _logger.LogInformation("Popup window assigned to _currentPopup");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create or configure eye rest warning window");
                            throw;
                        }

                        // Handle completion
                        eyeRestWarningPopup.WarningCompleted += (s, e) =>
                        {
                            _logger.LogInformation("Eye rest warning completed event fired");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // CRITICAL FIX: Immediate cleanup - no delays to prevent race conditions
                                // Backup trigger system has been removed, so no race conditions possible
                                _logger.LogInformation("🧹 POPUP FIX: Immediate cleanup starting - clearing popup reference");
                                _activeEyeRestWarningPopup = null; // Clear reference immediately
                                CloseCurrentPopup();
                                _logger.LogInformation("🧹 POPUP FIX: Immediate cleanup completed successfully");
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            try
                            {
                                _logger.LogInformation("🧹 POPUP FIX: Eye rest warning popup closed event fired");
                                
                                // CRITICAL FIX: Immediate cleanup - no delays to prevent race conditions
                                // Backup trigger system removed, so immediate cleanup is safe
                                _logger.LogInformation("🧹 POPUP FIX: Immediate popup close cleanup starting");
                                _activeEyeRestWarningPopup = null; // Clear reference immediately
                                eyeRestWarningPopup.StopCountdown();
                                _logger.LogInformation("🧹 POPUP FIX: Eye rest warning popup reference cleared successfully");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "🧹 POPUP FIX: Error in eye rest warning popup closed event handler");
                                // Force clear reference even if error occurred
                                _activeEyeRestWarningPopup = null;
                            }
                        };

                        // Show popup and start countdown with error handling
                        try
                        {
                            _logger.LogInformation("About to call popupWindow.Show()");
                            popupWindow.Show();
                            _logger.LogInformation($"popupWindow.Show() completed. Window IsVisible: {popupWindow.IsVisible}, WindowState: {popupWindow.WindowState}");
                            
                            _logger.LogInformation($"Starting countdown for {(int)timeUntilBreak.TotalSeconds} seconds");
                            eyeRestWarningPopup.StartCountdown((int)timeUntilBreak.TotalSeconds);
                            _logger.LogInformation("Countdown started successfully");
                            
                            // CRITICAL FIX: Start timer countdown ONLY if not already running
                            // This prevents the UI desync issue while ensuring countdown works
                            if (_timerService != null)
                            {
                                // Check if we need to start the countdown timer
                                var timerServiceType = _timerService.GetType();
                                var isEyeRestWarningTimerEnabledField = timerServiceType.GetField("_eyeRestWarningTimer", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var eyeRestWarningTimer = isEyeRestWarningTimerEnabledField?.GetValue(_timerService) as System.Windows.Threading.DispatcherTimer;
                                
                                if (eyeRestWarningTimer?.IsEnabled != true)
                                {
                                    _logger.LogInformation("Starting eye rest warning countdown timer - not currently running");
                                    _timerService.StartEyeRestWarningTimer();
                                }
                                else
                                {
                                    _logger.LogInformation("Eye rest warning timer already running - skipping duplicate start");
                                }
                            }
                            
                            _logger.LogInformation($"Eye rest warning shown for {timeUntilBreak.TotalSeconds} seconds");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to show eye rest warning popup");
                            CloseCurrentPopup(); // Clean up on failure
                            throw;
                        }
                    }
                });
                
                _logger.LogInformation("ShowEyeRestWarningAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing eye rest warning");
                throw;
            }
        }

        public async Task ShowEyeRestReminderAsync(TimeSpan duration)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            try
            {
                _logger.LogInformation($"ShowEyeRestReminderAsync called with duration: {duration.TotalSeconds} seconds");
                
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        _logger.LogInformation("Entered dispatcher invoke for eye rest reminder");
                        
                        // Close any existing popup
                        CloseCurrentPopup();
                        
                        // CRITICAL FIX: Add small delay to ensure previous popup is fully closed
                        System.Threading.Thread.Sleep(100);

                        // CRITICAL FIX: Always create fresh popup to prevent stale event handlers
                        _logger.LogInformation("Creating fresh EyeRestPopup to prevent stale event handlers");
                        var eyeRestPopup = new EyeRestPopup();
                        _logger.LogInformation("Fresh EyeRestPopup created");
                        
                        _logger.LogInformation("Creating BasePopupWindow for eye rest");
                        var popupWindow = new BasePopupWindow();
                        _logger.LogInformation("BasePopupWindow created for eye rest");
                        
                        _logger.LogInformation("Setting content on eye rest popup window");
                        popupWindow.SetContent(eyeRestPopup);
                        _logger.LogInformation("Content set on eye rest popup window");
                        
                        _currentPopup = popupWindow;

                        // Handle completion - this will complete the Task when popup is actually closed
                        eyeRestPopup.Completed += (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup completed event fired");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                CloseCurrentPopup();
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(true); // Signal that the eye rest is actually complete
                                }
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup window closed event fired");
                            eyeRestPopup.StopCountdown();
                            if (!tcs.Task.IsCompleted)
                            {
                                tcs.SetResult(true); // Ensure task completes even if closed by other means
                            }
                        };

                        // Show popup and start countdown
                        _logger.LogInformation("About to show eye rest popup window");
                        popupWindow.Show();
                        _logger.LogInformation($"Eye rest popup shown. IsVisible: {popupWindow.IsVisible}, WindowState: {popupWindow.WindowState}");
                        
                        _logger.LogInformation($"Starting eye rest countdown for {duration.TotalSeconds} seconds");
                        eyeRestPopup.StartCountdown(duration);
                        _logger.LogInformation("Eye rest countdown started");
                        
                        _logger.LogInformation($"Eye rest reminder shown for {duration.TotalSeconds} seconds");
                    }
                });
                
                // Wait for the popup to be actually completed by the user
                await tcs.Task;
                _logger.LogInformation("ShowEyeRestReminderAsync completed - eye rest was actually finished by user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing eye rest reminder");
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetException(ex);
                }
                throw;
            }
        }

        public async Task ShowBreakWarningAsync(TimeSpan timeUntilBreak)
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        // Close any existing popup
                        CloseCurrentPopup();

                        // CRITICAL FIX: Always create fresh popup to prevent stale event handlers
                        _logger.LogInformation("Creating fresh BreakWarningPopup to prevent stale event handlers");
                        var breakWarningPopup = new BreakWarningPopup();
                        _activeBreakWarningPopup = breakWarningPopup; // Store reference for external countdown control
                        _logger.LogInformation("Fresh BreakWarningPopup created and stored");
                        
                        // Create popup window with error handling
                        BasePopupWindow popupWindow;
                        try
                        {
                            popupWindow = new BasePopupWindow();
                            popupWindow.SetContent(breakWarningPopup);
                            _currentPopup = popupWindow;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create or configure break warning window");
                            throw;
                        }

                        // Handle completion
                        breakWarningPopup.Completed += (s, e) =>
                        {
                            _logger.LogInformation("🟠 BreakWarningPopup Completed event fired");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // CRITICAL FIX: Immediate cleanup - no delays to prevent race conditions
                                // Backup trigger system has been removed, so no race conditions possible
                                _logger.LogInformation("🧹 POPUP FIX: Break warning completed - immediate cleanup starting");
                                _activeBreakWarningPopup = null; // Clear reference immediately
                                
                                // Only close if this warning popup is still the current popup
                                // If break popup has already been shown, don't interfere
                                var currentContent = (_currentPopup as BasePopupWindow)?.ContentArea?.Content;
                                if (_currentPopup != null && currentContent == breakWarningPopup)
                                {
                                    _logger.LogInformation("🟠 Closing warning popup because it's still current");
                                    CloseCurrentPopup();
                                }
                                else
                                {
                                    _logger.LogInformation($"🟠 WARNING: BreakWarningPopup completed but break popup is already shown - NOT closing. Current content: {currentContent?.GetType().Name}");
                                }
                                _logger.LogInformation("🧹 POPUP FIX: Break warning cleanup completed successfully");
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            try
                            {
                                _logger.LogInformation("🧹 POPUP FIX: Break warning popup closed event fired");
                                
                                // CRITICAL FIX: Immediate cleanup - no delays to prevent race conditions
                                // Backup trigger system removed, so immediate cleanup is safe
                                _logger.LogInformation("🧹 POPUP FIX: Break warning popup close cleanup starting");
                                _activeBreakWarningPopup = null; // Clear reference immediately
                                breakWarningPopup.StopCountdown();
                                _logger.LogInformation("🧹 POPUP FIX: Break warning popup reference cleared successfully");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "🧹 POPUP FIX: Error in break warning popup closed event handler");
                                // Force clear reference even if error occurred
                                _activeBreakWarningPopup = null;
                            }
                        };

                        // Show popup and start countdown with error handling
                        try
                        {
                            popupWindow.Show();
                            breakWarningPopup.StartCountdown(timeUntilBreak);
                            
                            // CRITICAL FIX: Remove duplicate timer start that causes UI desync
                            // The timer is already started by TimerService.OnBreakTimerTick()
                            // This duplicate call was causing the countdown to reset back to original time
                            _logger.LogInformation("Break warning popup created - timer already running from TimerService");
                            
                            _logger.LogInformation($"Break warning shown for {timeUntilBreak.TotalSeconds} seconds");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to show break warning popup");
                            CloseCurrentPopup(); // Clean up on failure
                            throw;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing break warning");
                throw;
            }
        }

        public async Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress)
        {
            var tcs = new TaskCompletionSource<BreakAction>();

            try
            {
                _logger.LogInformation($"🎯 ShowBreakReminderAsync START - Duration: {duration.TotalMinutes} minutes, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                
                // Load configuration before entering the lock
                var breakConfig = await _configurationService.LoadConfigurationAsync();
                _logger.LogInformation($"🎯 Configuration loaded - RequireConfirmation: {breakConfig.Break.RequireConfirmationAfterBreak}, ResetTimers: {breakConfig.Break.ResetTimersOnBreakConfirmation}");
                
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        _logger.LogInformation($"🎯 Entered dispatcher invoke for break reminder, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                        
                        // Close any existing popup
                        _logger.LogInformation("🎯 Calling CloseCurrentPopup before creating break popup");
                        CloseCurrentPopup();
                        
                        // CRITICAL FIX: Add small delay to ensure previous popup is fully closed
                        _logger.LogInformation("🎯 Sleeping 100ms to ensure previous popup is closed");
                        System.Threading.Thread.Sleep(100);

                        // CRITICAL FIX: Always create fresh popup to prevent stale event handlers
                        _logger.LogInformation("🎯 Creating fresh BreakPopup instance");
                        var breakPopup = new BreakPopup();
                        _logger.LogInformation($"🎯 BreakPopup created - HashCode: {breakPopup.GetHashCode()}");
                        
                        // Configure break popup with current settings
                        breakPopup.SetConfiguration(
                            breakConfig.Break.RequireConfirmationAfterBreak, 
                            breakConfig.Break.ResetTimersOnBreakConfirmation);
                        _logger.LogInformation($"🎯 BreakPopup configured - RequireConfirmation: {breakConfig.Break.RequireConfirmationAfterBreak}, ResetTimers: {breakConfig.Break.ResetTimersOnBreakConfirmation}");
                        
                        // Create popup window with error handling
                        BasePopupWindow popupWindow;
                        try
                        {
                            _logger.LogInformation("🎯 Creating BasePopupWindow for break reminder");
                            popupWindow = new BasePopupWindow(); // Use base popup window for consistency
                            _logger.LogInformation($"🎯 BasePopupWindow created - HashCode: {popupWindow.GetHashCode()}");
                            
                            _logger.LogInformation("🎯 Setting BreakPopup as content of BasePopupWindow");
                            popupWindow.SetContent(breakPopup);
                            _logger.LogInformation("🎯 Content set successfully");
                            
                            _logger.LogInformation("🎯 Assigning popup window to _currentPopup field");
                            _currentPopup = popupWindow;
                            _logger.LogInformation($"🎯 _currentPopup assigned - HashCode: {_currentPopup.GetHashCode()}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "🎯 ERROR: Failed to create or configure break reminder window");
                            throw;
                        }

                        // Handle action selection
                        _logger.LogInformation("🎯 Subscribing to ActionSelected event");
                        breakPopup.ActionSelected += async (s, action) =>
                        {
                            _logger.LogInformation($"🎯 ActionSelected event fired with action: {action}");
                            
                            if (action == BreakAction.ConfirmedAfterCompletion)
                            {
                                // User confirmed after break completion
                                Application.Current.Dispatcher.Invoke(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("🎯 User confirmed break completion");
                                        CloseCurrentPopup();
                                        
                                        if (_timerService != null)
                                        {
                                            if (breakConfig.Break.ResetTimersOnBreakConfirmation)
                                            {
                                                // Reset timers for fresh session
                                                await _timerService.SmartSessionResetAsync("User confirmed break completion");
                                                _logger.LogInformation("🎯 Timers reset for fresh session after break confirmation");
                                            }
                                            else
                                            {
                                                // Just resume timers
                                                await _timerService.ResumeAsync();
                                                _logger.LogInformation("🎯 Timers resumed after break confirmation");
                                            }
                                        }
                                        
                                        tcs.SetResult(BreakAction.ConfirmedAfterCompletion);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "🎯 Error handling break confirmation");
                                        tcs.SetResult(BreakAction.ConfirmedAfterCompletion);
                                    }
                                });
                            }
                            else
                            {
                                // Handle other actions (DelayOneMinute, DelayFiveMinutes, Skipped, or Completed without confirmation)
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _logger.LogInformation($"🎯 In ActionSelected dispatcher - calling CloseCurrentPopup for action: {action}");
                                    CloseCurrentPopup();
                                    _logger.LogInformation($"🎯 Setting TaskCompletionSource result to: {action}");
                                    tcs.SetResult(action);
                                });
                            }
                        };

                        // Handle window closed
                        _logger.LogInformation("🎯 Subscribing to PopupClosed event");
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            _logger.LogInformation("🎯 PopupClosed event fired");
                            _logger.LogInformation("🎯 Stopping countdown from PopupClosed event");
                            breakPopup.StopCountdown();
                            if (!tcs.Task.IsCompleted)
                            {
                                _logger.LogInformation("🎯 Task not completed, setting result to Skipped");
                                tcs.SetResult(BreakAction.Skipped);
                            }
                            else
                            {
                                _logger.LogInformation($"🎯 Task already completed with result: {tcs.Task.Result}");
                            }
                        };

                        // Show popup and start countdown with error handling
                        try
                        {
                            _logger.LogInformation("🎯 About to call popupWindow.Show()");
                            popupWindow.Show();
                            _logger.LogInformation($"🎯 popupWindow.Show() completed - IsVisible: {popupWindow.IsVisible}, WindowState: {popupWindow.WindowState}");
                            
                            _logger.LogInformation($"🎯 Calling StartCountdown with duration: {duration.TotalMinutes} minutes");
                            
                            // Create a progress callback that handles break completion for confirmation mode
                            var progressWithCompletion = new Progress<double>(value =>
                            {
                                progress?.Report(value);
                                
                                // CRITICAL: When break completes (progress = 1.0) and confirmation is required, pause timers
                                if (value >= 1.0 && breakConfig.Break.RequireConfirmationAfterBreak)
                                {
                                    // Break countdown finished - pause timers while waiting for user confirmation
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            if (_timerService != null)
                                            {
                                                await _timerService.PauseAsync();
                                                _logger.LogInformation("🎯 Break completed - timers paused while waiting for user confirmation");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "🎯 Error pausing timers after break completion");
                                        }
                                    });
                                }
                            });
                            
                            breakPopup.StartCountdown(duration, progressWithCompletion);
                            _logger.LogInformation("🎯 StartCountdown called successfully");
                            
                            _logger.LogInformation($"🎯 Break reminder setup complete - should run for {duration.TotalMinutes} minutes");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "🎯 ERROR: Failed to show break reminder popup");
                            CloseCurrentPopup(); // Clean up on failure
                            throw;
                        }
                    }
                });
                
                // Show overlay on all screens when break popup shows (outside lock statement)
                _logger.LogInformation("🎯 Showing overlay on all screens for break");
                try
                {
                    var config = await _configurationService.LoadConfigurationAsync();
                    var opacityPercent = config.Break.OverlayOpacityPercent;
                    var opacity = opacityPercent / 100.0; // Convert to 0.0-1.0 range
                    
                    await _screenOverlayService.ShowOverlayAsync(opacity);
                    _overlayVisible = true;
                    _logger.LogInformation($"🎯 Successfully showed overlay with {opacityPercent}% opacity");
                }
                catch (Exception overlayEx)
                {
                    _logger.LogWarning(overlayEx, "🎯 Failed to show overlay, continuing with break popup");
                }
                
                _logger.LogInformation("🎯 Waiting for break action result...");
                var result = await tcs.Task;
                _logger.LogInformation($"🎯 ShowBreakReminderAsync COMPLETE - Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 ERROR in ShowBreakReminderAsync");
                tcs.SetException(ex);
                return await tcs.Task;
            }
        }

        public async Task HideAllNotifications()
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        CloseCurrentPopup();
                        
                        // CRITICAL FIX: Force clear stale popup references to prevent zombie state
                        // This fixes the "25 seconds forever" break warning issue caused by stale references
                        if (_activeBreakWarningPopup != null)
                        {
                            _logger.LogCritical("🧟 ZOMBIE FIX: Force clearing stale _activeBreakWarningPopup reference");
                            try
                            {
                                _activeBreakWarningPopup.StopCountdown();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Exception stopping stale break warning popup: {ex.Message}");
                            }
                            _activeBreakWarningPopup = null;
                        }
                        
                        if (_activeEyeRestWarningPopup != null)
                        {
                            _logger.LogCritical("🧟 ZOMBIE FIX: Force clearing stale _activeEyeRestWarningPopup reference");
                            try
                            {
                                _activeEyeRestWarningPopup.StopCountdown();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Exception stopping stale eye rest warning popup: {ex.Message}");
                            }
                            _activeEyeRestWarningPopup = null;
                        }
                        
                        _logger.LogInformation("✅ ZOMBIE FIX: All popup references cleared and validated");
                    }
                });
                
                _logger.LogInformation("All notifications hidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding notifications");
                throw;
            }
        }

        private void CloseCurrentPopup()
        {
            // Log stack trace to see what's calling this
            var stackTrace = new System.Diagnostics.StackTrace(true);
            _logger.LogInformation($"🔴 CloseCurrentPopup called from:\n{stackTrace}");
            
            if (_currentPopup != null && !_isClosing)
            {
                _logger.LogInformation($"🔴 Closing popup - HashCode: {_currentPopup.GetHashCode()}, IsLoaded: {_currentPopup.IsLoaded}, IsVisible: {_currentPopup.IsVisible}");
                
                _isClosing = true;
                var popupToClose = _currentPopup;
                _currentPopup = null; // Clear reference immediately to prevent race conditions
                
                // Hide overlay if it was visible
                if (_overlayVisible)
                {
                    _logger.LogInformation("🔴 Hiding overlay after break popup close");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _screenOverlayService.HideOverlayAsync();
                            _overlayVisible = false;
                            _logger.LogInformation("🔴 Successfully hid overlay");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "🔴 Failed to hide overlay");
                        }
                    });
                }
                
                try
                {
                    // Check if window is still valid before closing
                    if (popupToClose.IsLoaded && popupToClose.IsVisible)
                    {
                        _logger.LogInformation($"🔴 Popup is loaded and visible, proceeding to close: {popupToClose.GetType().Name}");
                        
                        // Use dispatcher to ensure clean close on UI thread
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (popupToClose.IsLoaded)
                                {
                                    _logger.LogInformation($"🔴 Calling Close() on popup window");
                                    popupToClose.Close();
                                    _logger.LogInformation("🔴 Popup Close() called successfully");
                                }
                                else
                                {
                                    _logger.LogInformation("🔴 Popup no longer loaded in dispatcher invoke");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "🔴 ERROR closing popup window");
                            }
                            finally
                            {
                                _isClosing = false;
                                _logger.LogInformation("🔴 CloseCurrentPopup completed, _isClosing reset to false");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    else
                    {
                        _logger.LogInformation($"🔴 Popup already closed or not loaded - IsLoaded: {popupToClose.IsLoaded}, IsVisible: {popupToClose.IsVisible}");
                        _isClosing = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔴 ERROR occurred while closing popup window");
                    _isClosing = false;
                }
            }
            else
            {
                _logger.LogInformation($"🔴 CloseCurrentPopup - No popup to close (_currentPopup: {_currentPopup != null}, _isClosing: {_isClosing})");
            }
        }

        // External countdown control methods for warning popups
        public void UpdateEyeRestWarningCountdown(TimeSpan remaining)
        {
            if (_activeEyeRestWarningPopup != null)
            {
                _dispatcher.InvokeAsync(() =>
                {
                    _activeEyeRestWarningPopup?.UpdateCountdown(remaining);
                });
            }
        }

        public void UpdateBreakWarningCountdown(TimeSpan remaining)
        {
            if (_activeBreakWarningPopup != null)
            {
                _dispatcher.InvokeAsync(() =>
                {
                    _activeBreakWarningPopup?.UpdateCountdown(remaining);
                });
            }
        }

        public void StartEyeRestWarningCountdown(TimeSpan duration)
        {
            // CRITICAL FIX: Remove duplicate timer start that causes UI desync
            // The timer should already be started by TimerService.OnEyeRestTimerTick()
            // This duplicate call was causing countdown resets
            _logger.LogInformation($"Eye rest warning countdown requested for {duration.TotalSeconds}s - timer should already be running");
        }

        public void StartBreakWarningCountdown(TimeSpan duration)
        {
            // CRITICAL FIX: Remove duplicate timer start that causes UI desync
            // The timer should already be started by TimerService.OnBreakTimerTick()
            // This duplicate call was causing countdown resets
            _logger.LogInformation($"Break warning countdown requested for {duration.TotalSeconds}s - timer should already be running");
        }

        #region Test Mode Methods - Do Not Record Analytics
        
        /// <summary>
        /// Test mode - Show eye rest warning without recording analytics
        /// </summary>
        public async Task ShowEyeRestWarningTestAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = true;
            try
            {
                _logger.LogInformation("🧪 TEST MODE: Showing eye rest warning popup (analytics disabled)");
                await ShowEyeRestWarningAsync(timeUntilBreak);
            }
            finally
            {
                _isTestMode = false;
            }
        }

        /// <summary>
        /// Test mode - Show eye rest reminder without recording analytics
        /// </summary>
        public async Task ShowEyeRestReminderTestAsync(TimeSpan duration)
        {
            _isTestMode = true;
            try
            {
                _logger.LogInformation("🧪 TEST MODE: Showing eye rest reminder popup (analytics disabled)");
                await ShowEyeRestReminderAsync(duration);
            }
            finally
            {
                _isTestMode = false;
            }
        }

        /// <summary>
        /// Test mode - Show break warning without recording analytics
        /// </summary>
        public async Task ShowBreakWarningTestAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = true;
            try
            {
                _logger.LogInformation("🧪 TEST MODE: Showing break warning popup (analytics disabled)");
                await ShowBreakWarningAsync(timeUntilBreak);
            }
            finally
            {
                _isTestMode = false;
            }
        }

        /// <summary>
        /// Test mode - Show break reminder without recording analytics
        /// </summary>
        public async Task<BreakAction> ShowBreakReminderTestAsync(TimeSpan duration, IProgress<double> progress)
        {
            _isTestMode = true;
            try
            {
                _logger.LogInformation("🧪 TEST MODE: Showing break reminder popup (analytics disabled)");
                return await ShowBreakReminderAsync(duration, progress);
            }
            finally
            {
                _isTestMode = false;
            }
        }

        /// <summary>
        /// Check if current popup is in test mode (prevents analytics recording)
        /// </summary>
        public bool IsTestMode => _isTestMode;
        
        #endregion
    }

}