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
        private bool _isClosing = false; // Track if we're in the process of closing
        private bool _overlayVisible = false; // Track if overlay is currently visible

        public NotificationService(ILogger<NotificationService> logger, Dispatcher dispatcher, IScreenOverlayService screenOverlayService, IConfigurationService configurationService)
        {
            _logger = logger;
            _dispatcher = dispatcher;
            _screenOverlayService = screenOverlayService;
            _configurationService = configurationService;
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
                        _logger.LogInformation("Fresh EyeRestWarningPopup created");
                        
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
                                CloseCurrentPopup();
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            _logger.LogInformation("Popup window closed event fired");
                            eyeRestWarningPopup.StopCountdown();
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

                        // Handle completion
                        eyeRestPopup.Completed += (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup completed event fired");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                CloseCurrentPopup();
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup window closed event fired");
                            eyeRestPopup.StopCountdown();
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
                
                _logger.LogInformation("ShowEyeRestReminderAsync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing eye rest reminder");
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
                        _logger.LogInformation("Fresh BreakWarningPopup created");
                        
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
                                // CRITICAL FIX: Only close if this warning popup is still the current popup
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
                            });
                        };

                        // Handle window closed
                        popupWindow.PopupClosed += (s, e) =>
                        {
                            breakWarningPopup.StopCountdown();
                        };

                        // Show popup and start countdown with error handling
                        try
                        {
                            popupWindow.Show();
                            breakWarningPopup.StartCountdown(timeUntilBreak);
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
                        breakPopup.ActionSelected += (s, action) =>
                        {
                            _logger.LogInformation($"🎯 ActionSelected event fired with action: {action}");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _logger.LogInformation($"🎯 In ActionSelected dispatcher - calling CloseCurrentPopup for action: {action}");
                                CloseCurrentPopup();
                                _logger.LogInformation($"🎯 Setting TaskCompletionSource result to: {action}");
                                tcs.SetResult(action);
                            });
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
                            breakPopup.StartCountdown(duration, progress);
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
    }

}