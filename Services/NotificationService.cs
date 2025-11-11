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
        private readonly IPauseReminderService _pauseReminderService;
        private readonly IAudioService _audioService;
        private BasePopupWindow? _currentPopup;
        private readonly object _lockObject = new object();
        private readonly object _popupLock = new object(); // POPUP FIX: Dedicated lock for popup instance management
        private bool _isClosing = false; // Track if we're in the process of closing
        private bool _overlayVisible = false; // Track if overlay is currently visible
        private bool _isTestMode = false; // Track if we're in test mode to prevent analytics recording
        private bool _isBreakCompletionInProgress = false; // CRITICAL FIX: Prevent race conditions in break completion
        private bool _isBreakPopupActive = false; // Track if break popup is active
        private bool _isWaitingForBreakConfirmation = false; // Track if waiting for user confirmation after break
        private bool _userTookBreakAction = false; // Track if user clicked any action button
        
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
        
        // GLOBAL POPUP MUTEX: Comprehensive check for ANY active popup
        public bool IsAnyPopupActive => _currentPopup != null || IsEyeRestWarningActive || IsBreakWarningActive || _isBreakPopupActive || _isWaitingForBreakConfirmation;
        
        // Track if break popup is active (including waiting for confirmation)
        public bool IsBreakActive => _isBreakPopupActive || _isWaitingForBreakConfirmation;

        public NotificationService(ILogger<NotificationService> logger, Dispatcher dispatcher, IScreenOverlayService screenOverlayService, IConfigurationService configurationService, IPauseReminderService pauseReminderService, IAudioService audioService)
        {
            _logger = logger;
            _dispatcher = dispatcher;
            _screenOverlayService = screenOverlayService;
            _configurationService = configurationService;
            _pauseReminderService = pauseReminderService;
            _audioService = audioService;
            
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

                // GLOBAL POPUP MUTEX: Block if any other popup is already active
                if (IsAnyPopupActive && _currentPopup != null)
                {
                    _logger.LogWarning("🚫 GLOBAL POPUP MUTEX: Eye rest warning blocked - another popup is already active. Current popup type: {PopupType}", _currentPopup.GetType().Name);
                    return;
                }
                
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        _logger.LogInformation("Entered dispatcher invoke for eye rest warning");

                        // CRITICAL FIX: Only close warning popups, NOT active break popups
                        // This prevents the eye rest warning from killing the running break popup
                        if (!_isBreakPopupActive)
                        {
                            _logger.LogInformation("No active break popup - safe to close current popup");
                            CloseCurrentPopup();
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ BREAK POPUP PROTECTION: Active break popup detected - NOT closing current popup to preserve break session");
                        }

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

                // GLOBAL POPUP MUTEX: Block if break popup is already active (eye rest has lower priority than breaks)
                if (_isBreakPopupActive || _isWaitingForBreakConfirmation)
                {
                    _logger.LogWarning("🚫 GLOBAL POPUP MUTEX: Eye rest reminder blocked - break popup has priority and is already active");
                    return;
                }
                
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

                        // CRITICAL FIX: Store event handlers so we can unregister them later
                        EventHandler? completedHandler = null;
                        EventHandler? closedHandler = null;

                        // Handle completion - this will complete the Task when popup is actually closed
                        completedHandler = (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup completed event fired");

                            // CRITICAL FIX: Defensive check - only act if this popup is still current
                            if (_currentPopup == popupWindow)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // Unregister handlers BEFORE closing to prevent re-entry
                                    if (completedHandler != null)
                                    {
                                        eyeRestPopup.Completed -= completedHandler;
                                        _logger.LogDebug("🔧 Unregistered eye rest Completed handler");
                                    }
                                    if (closedHandler != null && popupWindow != null)
                                    {
                                        popupWindow.PopupClosed -= closedHandler;
                                        _logger.LogDebug("🔧 Unregistered eye rest PopupClosed handler");
                                    }

                                    CloseCurrentPopup();
                                    if (!tcs.Task.IsCompleted)
                                    {
                                        tcs.SetResult(true); // Signal that the eye rest is actually complete
                                    }
                                });
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ STALE HANDLER: Eye rest completed handler fired but popup is no longer current");
                                _logger.LogCritical("🔧 STALE HANDLER FIX: Completing task anyway to prevent stuck global lock and allow flag clearing");
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(true); // CRITICAL: Complete task to allow await to return and flags to be cleared
                                }
                            }
                        };

                        // Handle window closed
                        closedHandler = (s, e) =>
                        {
                            _logger.LogInformation("Eye rest popup window closed event fired");

                            // CRITICAL FIX: Defensive check - only act if this popup is still current
                            if (_currentPopup == popupWindow)
                            {
                                // Unregister handlers to prevent future stale events
                                if (completedHandler != null)
                                {
                                    eyeRestPopup.Completed -= completedHandler;
                                    _logger.LogDebug("🔧 Unregistered eye rest Completed handler from PopupClosed");
                                }
                                if (closedHandler != null)
                                {
                                    popupWindow.PopupClosed -= closedHandler;
                                    _logger.LogDebug("🔧 Unregistered eye rest PopupClosed handler");
                                }

                                eyeRestPopup.StopCountdown();
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(true); // Ensure task completes even if closed by other means
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ STALE HANDLER: Eye rest PopupClosed handler fired but popup is no longer current");
                                _logger.LogCritical("🔧 STALE HANDLER FIX: Completing task anyway to prevent stuck global lock and allow flag clearing");
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(true); // CRITICAL: Complete task to allow await to return and flags to be cleared
                                }
                            }
                        };

                        eyeRestPopup.Completed += completedHandler;
                        popupWindow.PopupClosed += closedHandler;

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
            // GLOBAL POPUP MUTEX: Block if any other popup is already active
            if (IsAnyPopupActive && _currentPopup != null)
            {
                _logger.LogWarning("🚫 GLOBAL POPUP MUTEX: Break warning blocked - another popup is already active. Current popup type: {PopupType}", _currentPopup.GetType().Name);
                return;
            }

            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    lock (_lockObject)
                    {
                        // CRITICAL FIX: Only close warning popups, NOT active break popups
                        // This prevents the break warning from killing the running break popup
                        if (!_isBreakPopupActive)
                        {
                            _logger.LogInformation("No active break popup - safe to close current popup");
                            CloseCurrentPopup();
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ BREAK POPUP PROTECTION: Active break popup detected - NOT closing current popup to preserve break session");
                        }

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
            // GLOBAL POPUP MUTEX: Block if any other popup is already active (except break warnings which this supersedes)
            if (IsAnyPopupActive && _currentPopup != null && !IsBreakWarningActive)
            {
                _logger.LogWarning("🚫 GLOBAL POPUP MUTEX: Break reminder blocked - another non-warning popup is already active. Current popup type: {PopupType}", _currentPopup.GetType().Name);
                return BreakAction.Skipped; // Return appropriate action for break reminder
            }

            var tcs = new TaskCompletionSource<BreakAction>();

            try
            {
                _logger.LogInformation($"🎯 ShowBreakReminderAsync START - Duration: {duration.TotalMinutes} minutes, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                _logger.LogCritical($"🔄 POPUP LIFECYCLE: Break popup initiated - IsBreakActive: {IsBreakActive}, PopupCount: {(_currentPopup != null ? 1 : 0)}");
                
                // Mark break as active and reset user action flag
                _isBreakPopupActive = true;
                _userTookBreakAction = false;  // Reset flag at start
                _logger.LogCritical($"🔄 POPUP LIFECYCLE: Break state initialized - IsBreakPopupActive: {_isBreakPopupActive}, UserTookBreakAction: {_userTookBreakAction}");
                
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

                        // CRITICAL FIX: Store event handlers so we can unregister them and add defensive checks
                        EventHandler<BreakAction>? actionHandler = null;
                        EventHandler? closedHandler = null;

                        // Handle action selection
                        _logger.LogInformation("🎯 Subscribing to ActionSelected event");
                        actionHandler = async (s, action) =>
                        {
                            _logger.LogInformation($"🎯 ActionSelected event fired with action: {action}");

                            // CRITICAL FIX: Defensive check - only act if this popup is still current
                            if (_currentPopup != popupWindow)
                            {
                                _logger.LogWarning($"⚠️ STALE HANDLER: Break ActionSelected handler fired but popup is no longer current - action: {action}");
                                _logger.LogCritical("🔧 STALE HANDLER FIX: Completing task anyway to prevent stuck global lock and allow flag clearing");
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(action); // CRITICAL: Complete task to allow await to return and flags to be cleared
                                }
                                return;
                            }

                            if (action == BreakAction.ConfirmedAfterCompletion)
                            {
                                // User confirmed after break completion - let ApplicationOrchestrator handle timer logic
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("🎯 User confirmed break completion - closing popup and letting ApplicationOrchestrator handle timer restart");

                                        // Unregister handlers BEFORE closing
                                        if (actionHandler != null)
                                        {
                                            breakPopup.ActionSelected -= actionHandler;
                                            _logger.LogDebug("🔧 Unregistered break ActionSelected handler");
                                        }
                                        if (closedHandler != null)
                                        {
                                            popupWindow.PopupClosed -= closedHandler;
                                            _logger.LogDebug("🔧 Unregistered break PopupClosed handler");
                                        }

                                        _userTookBreakAction = true;  // User took action
                                        _isWaitingForBreakConfirmation = false;
                                        _isBreakPopupActive = false;
                                        CloseCurrentPopup();

                                        // CRITICAL FIX: Check if task is already completed before setting result
                                        if (!tcs.Task.IsCompleted)
                                        {
                                            tcs.SetResult(BreakAction.ConfirmedAfterCompletion);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("🎯 Task already completed - skipping SetResult for ConfirmedAfterCompletion");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "🎯 Error handling break confirmation");
                                        // CRITICAL FIX: Only set result if not already completed
                                        if (!tcs.Task.IsCompleted)
                                        {
                                            tcs.SetResult(BreakAction.ConfirmedAfterCompletion);
                                        }
                                    }
                                });
                            }
                            else
                            {
                                // Handle other actions (DelayOneMinute, DelayFiveMinutes, Skipped, or Completed without confirmation)
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _logger.LogInformation($"🎯 In ActionSelected dispatcher - calling CloseCurrentPopup for action: {action}");

                                    // Unregister handlers BEFORE closing
                                    if (actionHandler != null)
                                    {
                                        breakPopup.ActionSelected -= actionHandler;
                                        _logger.LogDebug("🔧 Unregistered break ActionSelected handler");
                                    }
                                    if (closedHandler != null)
                                    {
                                        popupWindow.PopupClosed -= closedHandler;
                                        _logger.LogDebug("🔧 Unregistered break PopupClosed handler");
                                    }

                                    _userTookBreakAction = true;  // User took action
                                    _isWaitingForBreakConfirmation = false;
                                    _isBreakPopupActive = false;
                                    CloseCurrentPopup();
                                    _logger.LogInformation($"🎯 Setting TaskCompletionSource result to: {action}");

                                    // CRITICAL FIX: Check if task is already completed before setting result
                                    if (!tcs.Task.IsCompleted)
                                    {
                                        tcs.SetResult(action);
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"🎯 Task already completed - skipping SetResult for action: {action}");
                                    }
                                });
                            }
                        };
                        breakPopup.ActionSelected += actionHandler;

                        // Handle window closed
                        _logger.LogInformation("🎯 Subscribing to PopupClosed event");
                        closedHandler = (s, e) =>
                        {
                            _logger.LogInformation("🎯 PopupClosed event fired");

                            // CRITICAL FIX: Defensive check - only act if this popup is still current
                            if (_currentPopup != popupWindow)
                            {
                                _logger.LogWarning("⚠️ STALE HANDLER: Break PopupClosed handler fired but popup is no longer current");
                                _logger.LogCritical("🔧 STALE HANDLER FIX: Completing task anyway to prevent stuck global lock and allow flag clearing");
                                if (!tcs.Task.IsCompleted)
                                {
                                    tcs.SetResult(BreakAction.Skipped); // CRITICAL: Complete task to allow await to return and flags to be cleared
                                }
                                return;
                            }

                            _logger.LogInformation("🎯 Stopping countdown from PopupClosed event");
                            breakPopup.StopCountdown();

                            // Unregister handlers to prevent future stale events
                            if (actionHandler != null)
                            {
                                breakPopup.ActionSelected -= actionHandler;
                                _logger.LogDebug("🔧 Unregistered break ActionSelected handler from PopupClosed");
                            }
                            if (closedHandler != null)
                            {
                                popupWindow.PopupClosed -= closedHandler;
                                _logger.LogDebug("🔧 Unregistered break PopupClosed handler");
                            }

                            // CRITICAL FIX: Don't automatically return Skipped if waiting for confirmation
                            if (_isWaitingForBreakConfirmation)
                            {
                                _logger.LogInformation("🎯 CRITICAL: Popup closed while waiting for confirmation - not setting result (window close prevented)");
                                return;  // Don't set result - window close should be prevented
                            }

                            // Only set result to Skipped if task not completed and user didn't take any action
                            if (!tcs.Task.IsCompleted && !_userTookBreakAction)
                            {
                                _logger.LogInformation("🎯 Task not completed and no user action - setting result to Skipped");
                                tcs.SetResult(BreakAction.Skipped);
                            }
                            else
                            {
                                _logger.LogInformation($"🎯 Task already completed with result: {tcs.Task.Result} or user took action");
                            }
                        };
                        popupWindow.PopupClosed += closedHandler;

                        // Show popup and start countdown with error handling
                        try
                        {
                            _logger.LogInformation("🎯 About to call popupWindow.Show()");
                            popupWindow.Show();
                            _logger.LogInformation($"🎯 popupWindow.Show() completed - IsVisible: {popupWindow.IsVisible}, WindowState: {popupWindow.WindowState}");

                            // CRITICAL FIX: Ensure the break popup is brought to foreground and activated
                            _logger.LogInformation("🎯 Activating and focusing break popup window");
                            popupWindow.Activate();
                            popupWindow.Focus();
                            _logger.LogInformation($"🎯 Break popup activated - IsActive: {popupWindow.IsActive}, IsFocused: {popupWindow.IsFocused}");

                            _logger.LogInformation($"🎯 Calling StartCountdown with duration: {duration.TotalMinutes} minutes");
                            
                            // Create a progress callback that handles break completion for confirmation mode
                            var progressWithCompletion = new Progress<double>(value =>
                            {
                                progress?.Report(value);

                                // CRITICAL FIX: Enhanced break completion handling with race condition protection
                                if (value >= 1.0 && breakConfig.Break.RequireConfirmationAfterBreak)
                                {
                                    // CRITICAL P0 FIX: Verify popup is still active before triggering completion
                                    // Prevents orphaned timers from triggering smart pause after popup force-closed during session reset
                                    // Without this check, a timer that wasn't stopped during CloseCurrentPopup could fire 5+ minutes later
                                    // and trigger SmartPause with no visible popup, leaving the system stuck in "Waiting for break confirmation"
                                    if (_currentPopup == null || !(_currentPopup is BasePopupWindow baseWindow && baseWindow.ContentArea?.Content is BreakPopup))
                                    {
                                        _logger.LogWarning("🎯 ORPHANED EVENT IGNORED: Break completion callback fired but popup no longer active (likely force-closed during session reset) - ignoring to prevent stuck state");
                                        return;
                                    }

                                    // CRITICAL FIX: Prevent multiple completion triggers during recovery scenarios
                                    lock (_lockObject)
                                    {
                                        if (_isBreakCompletionInProgress)
                                        {
                                            _logger.LogWarning("🎯 Break completion already in progress - skipping duplicate trigger");
                                            return;
                                        }
                                        _isBreakCompletionInProgress = true;
                                    }

                                    // P1 FIX: Set flag SYNCHRONOUSLY before Done screen appears to prevent recovery routines
                                    // from closing the popup during the async task delay window
                                    _isWaitingForBreakConfirmation = true;
                                    _logger.LogCritical("🎯 P1 FIX: Set _isWaitingForBreakConfirmation=true SYNCHRONOUSLY to prevent Done screen auto-close");

                                    // Break countdown finished - pause timers while waiting for user confirmation
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            // CRITICAL FIX: Add delay to prevent race conditions with system resume recovery
                                            await Task.Delay(100); // Brief delay to ensure proper sequencing

                                            if (_timerService != null)
                                            {
                                                // Use SmartPause instead of regular pause to properly track state
                                                await _timerService.SmartPauseAsync("Waiting for break confirmation");
                                                _logger.LogInformation("🎯 Break completed - timers smart-paused while waiting for user confirmation");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "🎯 Error pausing timers after break completion");
                                        }
                                        finally
                                        {
                                            lock (_lockObject)
                                            {
                                                _isBreakCompletionInProgress = false;
                                            }
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

                    // CRITICAL FIX: Ensure the break popup is on top of the overlay
                    await _dispatcher.InvokeAsync(() =>
                    {
                        if (_currentPopup != null)
                        {
                            _logger.LogInformation("🎯 Bringing break popup to front after overlay shown");
                            _currentPopup.Topmost = true;  // Ensure it's on top
                            _currentPopup.Activate();
                            _currentPopup.Focus();
                            _logger.LogInformation($"🎯 Break popup re-activated - Topmost: {_currentPopup.Topmost}, IsActive: {_currentPopup.IsActive}");
                        }
                    });
                }
                catch (Exception overlayEx)
                {
                    _logger.LogWarning(overlayEx, "🎯 Failed to show overlay, continuing with break popup");
                }
                
                _logger.LogInformation("🎯 Waiting for break action result...");

                // P0 FIX: Wait INDEFINITELY for user to click Done - no timeout!
                // The Done screen must remain visible until explicit user confirmation
                _logger.LogCritical("🎯 P0 FIX: Done screen waiting indefinitely for user confirmation - NO TIMEOUT");

                // Wait for user to click Done button - no timeout, no auto-close
                BreakAction result;
                _logger.LogCritical($"🔄 POPUP LIFECYCLE: Waiting for user action on break popup (indefinite wait)...");
                result = await tcs.Task;
                _logger.LogCritical($"🔄 POPUP LIFECYCLE: User action received - Result: {result}");
                
                // Clear break active flags when done
                _logger.LogCritical($"🔄 POPUP LIFECYCLE: Clearing break state - IsBreakPopupActive: {_isBreakPopupActive} → false, IsWaitingForBreakConfirmation: {_isWaitingForBreakConfirmation} → false");
                _isBreakPopupActive = false;
                _isWaitingForBreakConfirmation = false;
                
                _logger.LogInformation($"🎯 ShowBreakReminderAsync COMPLETE - Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 ERROR in ShowBreakReminderAsync");
                
                // Clear break active flags on error
                _logger.LogCritical($"🔄 POPUP LIFECYCLE ERROR: Clearing break state due to exception - IsBreakPopupActive: {_isBreakPopupActive} → false, IsWaitingForBreakConfirmation: {_isWaitingForBreakConfirmation} → false");
                _isBreakPopupActive = false;
                _isWaitingForBreakConfirmation = false;
                
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
            // Log stack trace at debug level for troubleshooting
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var stackTrace = new System.Diagnostics.StackTrace(true);
                _logger.LogDebug($"🔴 CloseCurrentPopup called from:\n{stackTrace}");
            }
            
            if (_currentPopup != null && !_isClosing)
            {
                _logger.LogDebug($"🔴 Closing popup - HashCode: {_currentPopup.GetHashCode()}, IsLoaded: {_currentPopup.IsLoaded}, IsVisible: {_currentPopup.IsVisible}");
                
                _isClosing = true;
                var popupToClose = _currentPopup;
                // DON'T clear reference yet - wait until after close
                
                // Hide overlay if it was visible
                if (_overlayVisible)
                {
                    _logger.LogDebug("🔴 Hiding overlay after break popup close");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _screenOverlayService.HideOverlayAsync();
                            _overlayVisible = false;
                            _logger.LogDebug("🔴 Successfully hid overlay");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "🔴 Failed to hide overlay");
                        }
                    });
                }
                
                try
                {
                    // CRITICAL FIX: If this is a BreakPopup, ensure force close is set
                    if (popupToClose is BasePopupWindow baseWindow && baseWindow.ContentArea?.Content is BreakPopup breakPopup)
                    {
                        _logger.LogDebug("🔴 Setting force close on BreakPopup before closing");
                        // Use reflection or make a public method to set force close
                        var forceCloseField = typeof(BreakPopup).GetField("_forceClose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (forceCloseField != null)
                        {
                            forceCloseField.SetValue(breakPopup, true);
                            _logger.LogDebug("🔴 Force close flag set successfully");
                        }

                        // CRITICAL P0 FIX: Stop BreakPopup countdown timer before closing window
                        // Without this, the timer continues running and fires orphaned completion callback 5 minutes later
                        // This was causing system to get stuck in "Smart Paused (Waiting for break confirmation)" with no visible popup
                        try
                        {
                            breakPopup.StopCountdown();
                            _logger.LogCritical("🔴 CRITICAL P0 FIX: Stopped BreakPopup countdown timer before force close to prevent orphaned completion events");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "🔴 Exception stopping BreakPopup countdown (non-critical)");
                        }
                    }
                    
                    // Check if window is still valid before closing
                    if (popupToClose.IsLoaded && popupToClose.IsVisible)
                    {
                        _logger.LogDebug($"🔴 Popup is loaded and visible, proceeding to close: {popupToClose.GetType().Name}");
                        
                        // CRITICAL FIX: Use Invoke instead of BeginInvoke for synchronous execution
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            try
                            {
                                if (popupToClose.IsLoaded)
                                {
                                    _logger.LogDebug($"🔴 Calling Close() on popup window");
                                    popupToClose.Close();
                                    _logger.LogDebug("🔴 Popup Close() called successfully");
                                }
                                else
                                {
                                    _logger.LogDebug("🔴 Popup no longer loaded in dispatcher invoke");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "🔴 ERROR closing popup window");
                            }
                            finally
                            {
                                _currentPopup = null; // NOW clear the reference after close
                                _isClosing = false;
                                _logger.LogDebug("🔴 CloseCurrentPopup completed, references cleared");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Send); // Use Send for immediate execution
                    }
                    else
                    {
                        _logger.LogDebug($"🔴 Popup already closed or not loaded - IsLoaded: {popupToClose.IsLoaded}, IsVisible: {popupToClose.IsVisible}");
                        _currentPopup = null;
                        _isClosing = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔴 ERROR occurred while closing popup window");
                    _currentPopup = null;
                    _isClosing = false;
                }
            }
            else
            {
                _logger.LogDebug($"🔴 CloseCurrentPopup - No popup to close (_currentPopup: {_currentPopup != null}, _isClosing: {_isClosing})");
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