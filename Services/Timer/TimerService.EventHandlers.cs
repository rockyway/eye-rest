using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all timer event handlers and event triggering logic
    /// </summary>
    public partial class TimerService
    {
        #region Timer Event Handlers

        private void OnEyeRestTimerTick(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                _logger.LogCritical($"👁️ TIMER EVENT: Eye rest timer tick fired at {now:HH:mm:ss.fff}");
                
                // ENHANCED CLOCK JUMP DETECTION: Check time since last tick AND system resume
                if (_lastEyeRestTick != DateTime.MinValue)
                {
                    var timeSinceLastTick = now - _lastEyeRestTick;
                    
                    // CRITICAL FIX: Enhanced system wake detection for extended sleep periods
                    // Check if system was likely asleep based on multiple indicators
                    bool likelySystemWake = false;
                    string wakeReason = "";
                    
                    // 1. Traditional clock jump (> 2 hours)
                    if (timeSinceLastTick > TimeSpan.FromHours(2))
                    {
                        likelySystemWake = true;
                        wakeReason = $"Large clock jump: {timeSinceLastTick.TotalHours:F1} hours";
                    }
                    // 2. NEW: Check for overnight/extended sleep patterns (30 minutes to 2 hours)
                    else if (timeSinceLastTick > TimeSpan.FromMinutes(30))
                    {
                        // Additional indicators of system sleep/wake:
                        // - Timer was due but took much longer than expected to fire
                        // - System has been idle for extended period
                        var expectedTickInterval = TimeSpan.FromSeconds(1); // DispatcherTimer interval
                        var actualDelay = timeSinceLastTick - expectedTickInterval;
                        
                        if (actualDelay > TimeSpan.FromMinutes(30))
                        {
                            likelySystemWake = true;
                            wakeReason = $"Extended tick delay: {timeSinceLastTick.TotalMinutes:F1}min (expected ~1s)";
                        }
                    }
                    
                    if (likelySystemWake)
                    {
                        _logger.LogCritical($"⏰ SYSTEM WAKE DETECTED: {wakeReason}");
                        _logger.LogCritical($"⏰ System likely woke from sleep/hibernation - initiating smart session reset");
                        
                        // Reset session instead of triggering eye rest
                        _ = Task.Run(async () =>
                        {
                            await SmartSessionResetAsync($"System wake detected - {wakeReason}");
                        });
                        
                        // Update last tick and return
                        _lastEyeRestTick = now;
                        return;
                    }
                }
                _lastEyeRestTick = now;
                
                // CRITICAL FIX: Add startup grace period to prevent immediate triggers
                var timeSinceStart = now - _eyeRestStartTime;
                if (timeSinceStart < TimeSpan.FromSeconds(5))
                {
                    _logger.LogWarning($"👁️ TIMER EVENT: Ignoring early tick during startup grace period (elapsed={timeSinceStart.TotalSeconds:F1}s)");
                    return;
                }
                
                // CRITICAL FIX: Validate timer state before processing
                if (!IsRunning || IsPaused || IsManuallyPaused || IsSmartPaused)
                {
                    _logger.LogInformation($"👁️ TIMER EVENT: Skipping eye rest tick - not running or paused (Running={IsRunning}, Paused={IsPaused}, ManuallyPaused={IsManuallyPaused}, SmartPaused={IsSmartPaused})");
                    return;
                }
                
                // CRITICAL FIX: Ensure we're on UI thread for all timer operations
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() != true)
                {
                    _logger.LogCritical("👁️ TIMER EVENT: Not on UI thread - invoking on UI thread");
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => OnEyeRestTimerTick(sender, e)));
                    return;
                }
                
                UpdateHeartbeatFromOperation("OnEyeRestTimerTick");
                
                // CRITICAL FIX: Verify timer timing is correct
                var elapsed = now - _eyeRestStartTime;
                var expectedInterval = _eyeRestInterval;
                _logger.LogCritical("👁️ TIMER VERIFICATION: Timer elapsed={ElapsedMinutes:F2}m, expected={ExpectedMinutes:F2}m", 
                    elapsed.TotalMinutes, expectedInterval.TotalMinutes);
                
                // CLOCK JUMP DETECTION: Check if elapsed time indicates system sleep
                // If elapsed > 2x expected interval, system likely slept
                if (elapsed > TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 2.0))
                {
                    _logger.LogCritical($"⏰ CLOCK JUMP DETECTED: Elapsed {elapsed.TotalMinutes:F1}min > 2x expected {expectedInterval.TotalMinutes:F1}min");
                    _logger.LogCritical($"⏰ System likely woke from sleep - initiating smart session reset");
                    
                    // Reset session instead of triggering eye rest
                    _ = Task.Run(async () =>
                    {
                        await SmartSessionResetAsync("Clock jump detected - excessive elapsed time");
                    });
                    return;
                }
                
                // Additional safety: Don't trigger if elapsed time is way too short (less than 50% of expected)
                if (elapsed < TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 0.5))
                {
                    _logger.LogWarning($"👁️ TIMER EVENT: Elapsed time too short ({elapsed.TotalMinutes:F1}m < {expectedInterval.TotalMinutes * 0.5:F1}m), ignoring tick");
                    return;
                }
                
                // Log if timer fired significantly early or late
                if (Math.Abs((elapsed - expectedInterval).TotalSeconds) > 5)
                {
                    _logger.LogWarning("⚠️ TIMER ACCURACY: Timer fired {TimingDiff:F1}s {Direction} expected time", 
                        Math.Abs((elapsed - expectedInterval).TotalSeconds),
                        elapsed < expectedInterval ? "before" : "after");
                }
                
                _logger.LogCritical("👁️ TIMER EVENT: Stopping eye rest timer and starting warning timer");
                _eyeRestTimer?.Stop();
                
                // Use public method to ensure proper thread safety
                StartEyeRestWarningTimer();
                
                _logger.LogCritical("👁️ TIMER EVENT: Eye rest timer tick completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "👁️ TIMER EVENT: Error in eye rest timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _eyeRestTimer?.Stop();
                    _eyeRestTimer?.Start();
                    _logger.LogInformation("👁️ TIMER EVENT: Eye rest timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "👁️ TIMER EVENT: Failed to recover eye rest timer");
                }
            }
        }

        private void OnBreakTimerTick(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                _logger.LogCritical($"☕ TIMER EVENT: Break timer tick fired at {now:HH:mm:ss.fff}");
                
                // ENHANCED CLOCK JUMP DETECTION: Check time since last tick AND system resume
                if (_lastBreakTick != DateTime.MinValue)
                {
                    var timeSinceLastTick = now - _lastBreakTick;
                    
                    // CRITICAL FIX: Enhanced system wake detection for extended sleep periods
                    // Check if system was likely asleep based on multiple indicators
                    bool likelySystemWake = false;
                    string wakeReason = "";
                    
                    // 1. Traditional clock jump (> 2 hours)
                    if (timeSinceLastTick > TimeSpan.FromHours(2))
                    {
                        likelySystemWake = true;
                        wakeReason = $"Large clock jump: {timeSinceLastTick.TotalHours:F1} hours";
                    }
                    // 2. NEW: Check for overnight/extended sleep patterns (30 minutes to 2 hours)
                    else if (timeSinceLastTick > TimeSpan.FromMinutes(30))
                    {
                        // Additional indicators of system sleep/wake:
                        // - Timer was due but took much longer than expected to fire
                        // - System has been idle for extended period
                        var expectedTickInterval = TimeSpan.FromSeconds(1); // DispatcherTimer interval
                        var actualDelay = timeSinceLastTick - expectedTickInterval;
                        
                        if (actualDelay > TimeSpan.FromMinutes(30))
                        {
                            likelySystemWake = true;
                            wakeReason = $"Extended tick delay: {timeSinceLastTick.TotalMinutes:F1}min (expected ~1s)";
                        }
                    }
                    
                    if (likelySystemWake)
                    {
                        _logger.LogCritical($"⏰ SYSTEM WAKE DETECTED: {wakeReason}");
                        _logger.LogCritical($"⏰ System likely woke from sleep/hibernation - initiating smart session reset");
                        
                        // Reset session instead of triggering break
                        _ = Task.Run(async () =>
                        {
                            await SmartSessionResetAsync($"System wake detected - {wakeReason}");
                        });
                        
                        // Update last tick and return
                        _lastBreakTick = now;
                        return;
                    }
                }
                _lastBreakTick = now;
                
                // CRITICAL FIX: Add startup grace period to prevent immediate triggers
                var timeSinceStart = now - _breakStartTime;
                if (timeSinceStart < TimeSpan.FromSeconds(5))
                {
                    _logger.LogWarning($"☕ TIMER EVENT: Ignoring early tick during startup grace period (elapsed={timeSinceStart.TotalSeconds:F1}s)");
                    return;
                }
                
                // CRITICAL FIX: Validate timer state before processing
                if (!IsRunning || IsPaused || IsManuallyPaused || IsSmartPaused)
                {
                    _logger.LogInformation($"☕ TIMER EVENT: Skipping break tick - not running or paused (Running={IsRunning}, Paused={IsPaused}, ManuallyPaused={IsManuallyPaused}, SmartPaused={IsSmartPaused})");
                    return;
                }
                
                // CRITICAL FIX: Ensure we're on UI thread for all timer operations
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() != true)
                {
                    _logger.LogCritical("☕ TIMER EVENT: Not on UI thread - invoking on UI thread");
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => OnBreakTimerTick(sender, e)));
                    return;
                }
                
                UpdateHeartbeatFromOperation("OnBreakTimerTick");
                
                // Additional safety: Check if enough time has elapsed since break start
                var elapsed = now - _breakStartTime;
                var expectedInterval = _breakInterval;
                
                // CLOCK JUMP DETECTION: Check if elapsed time indicates system sleep
                // If elapsed > 2x expected interval, system likely slept
                if (elapsed > TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 2.0))
                {
                    _logger.LogCritical($"⏰ CLOCK JUMP DETECTED: Elapsed {elapsed.TotalMinutes:F1}min > 2x expected {expectedInterval.TotalMinutes:F1}min");
                    _logger.LogCritical($"⏰ System likely woke from sleep - initiating smart session reset");
                    
                    // Reset session instead of triggering break
                    _ = Task.Run(async () =>
                    {
                        await SmartSessionResetAsync("Clock jump detected - excessive elapsed time");
                    });
                    return;
                }
                
                if (elapsed < TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 0.5))
                {
                    _logger.LogWarning($"☕ TIMER EVENT: Elapsed time too short ({elapsed.TotalMinutes:F1}m < {expectedInterval.TotalMinutes * 0.5:F1}m), ignoring tick");
                    return;
                }
                
                _logger.LogCritical("☕ TIMER EVENT: Stopping break timer and starting warning timer");
                _breakTimer?.Stop();
                
                // Use public method to ensure proper thread safety
                StartBreakWarningTimer();
                
                _logger.LogCritical("☕ TIMER EVENT: Break timer tick completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "☕ TIMER EVENT: Error in break timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _breakTimer?.Stop();
                    _breakTimer?.Start();
                    _logger.LogInformation("☕ TIMER EVENT: Break timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "☕ TIMER EVENT: Failed to recover break timer");
                }
            }
        }

        #endregion

        #region Event Triggering Methods

        private void TriggerEyeRestWarning(TimeSpan warningDuration)
        {
            try
            {
                // BREAK PRIORITY: Check if any break event is active or processing
                if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                    _isBreakEventProcessing || _isAnyBreakEventProcessing)
                {
                    _logger.LogInformation("🔄 BREAK PRIORITY: Eye rest warning blocked - break event has priority. Pausing eye rest timer.");

                    // Pause eye rest timer and reset after break completes
                    SmartPauseEyeRestTimerForBreak();
                    return;
                }

                // THREAD SAFETY: Global lock to prevent ALL warning systems from interfering
                lock (_globalEyeRestWarningLock)
                {
                    if (_isAnyEyeRestWarningProcessing)
                    {
                        _logger.LogWarning("⚠️ GLOBAL LOCK PREVENTION: Eye rest warning already processing globally - ignoring duplicate trigger");
                        return;
                    }
                    _isAnyEyeRestWarningProcessing = true;
                }

                // SYNC FIX: Set instance warning processing flag to prevent backup trigger race conditions
                if (_isEyeRestWarningProcessing)
                {
                    _logger.LogWarning("⚠️ INSTANCE LOCK PREVENTION: Eye rest warning already processing - ignoring duplicate trigger");
                    lock (_globalEyeRestWarningLock)
                    {
                        _isAnyEyeRestWarningProcessing = false;
                    }
                    return;
                }
                _isEyeRestWarningProcessing = true;

                _logger.LogInformation("⚠️ Triggering eye rest warning - {Seconds} seconds remaining",
                    warningDuration.TotalSeconds);

                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = warningDuration,
                    Type = TimerType.EyeRestWarning
                };

                EyeRestWarning?.Invoke(this, eventArgs);

                // CRITICAL FIX: Don't record analytics here - warnings are not completed events
                // Analytics should only be recorded based on actual user actions in the popup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering eye rest warning");

                // CRITICAL FIX: Clear processing flags on error to prevent stuck state
                _isEyeRestWarningProcessing = false;
                lock (_globalEyeRestWarningLock)
                {
                    _isAnyEyeRestWarningProcessing = false;
                }
            }
        }

        private void TriggerEyeRest()
        {
            try
            {
                // BREAK PRIORITY: Check if any break event is active or processing
                if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                    _isBreakEventProcessing || _isAnyBreakEventProcessing)
                {
                    _logger.LogInformation("🔄 BREAK PRIORITY: Eye rest popup blocked - break event has priority. Pausing eye rest timer.");

                    // Pause eye rest timer and reset after break completes
                    SmartPauseEyeRestTimerForBreak();
                    return;
                }

                // THREAD SAFETY: Global lock to prevent ALL timer systems from interfering
                lock (_globalEyeRestLock)
                {
                    if (_isAnyEyeRestEventProcessing)
                    {
                        _logger.LogWarning("👁️ GLOBAL LOCK PREVENTION: Eye rest event already processing globally - ignoring duplicate trigger");
                        return;
                    }
                    _isAnyEyeRestEventProcessing = true;
                }

                // SYNC FIX: Set instance processing flag to prevent backup trigger race conditions
                if (_isEyeRestEventProcessing)
                {
                    _logger.LogWarning("👁️ INSTANCE LOCK PREVENTION: Eye rest event already processing - ignoring duplicate trigger");
                    lock (_globalEyeRestLock)
                    {
                        _isAnyEyeRestEventProcessing = false;
                    }
                    return;
                }
                _isEyeRestEventProcessing = true;

                // TIMELINE FIX: Record when main timer actually triggered
                _lastEyeRestTriggeredTime = DateTime.Now;

                _logger.LogCritical("👁️ TRIGGER EYE REST: Starting popup at {Time}", DateTime.Now.ToString("HH:mm:ss.fff"));

                // CRITICAL FIX: Verify notification service is available
                if (_notificationService == null)
                {
                    _logger.LogError("👁️ TRIGGER ERROR: NotificationService is null - cannot show popup!");
                    _isEyeRestEventProcessing = false; // Clear flag on error
                    return;
                }

                // CRITICAL FIX: Don't set _isEyeRestNotificationActive here - it should already be set when warning started
                // This prevents duplicate state setting that can cause blocking issues
                
                // Stop warning timer
                _eyeRestWarningTimer?.Stop();
                
                var duration = TimeSpan.FromSeconds(_configuration.EyeRest.DurationSeconds);
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = duration,
                    Type = TimerType.EyeRest
                };
                
                // CRITICAL FIX: Log before and after event trigger
                _logger.LogCritical("👁️ TRIGGER EYE REST: Invoking EyeRestDue event to show popup");
                EyeRestDue?.Invoke(this, eventArgs);
                _logger.LogCritical("👁️ TRIGGER EYE REST: EyeRestDue event invoked successfully");

                // CRITICAL FIX: Don't record analytics here - triggering an event is not completing it
                // Analytics should only be recorded based on actual user actions in the popup

                // Note: _isEyeRestEventProcessing flag will be cleared by ApplicationOrchestrator after popup completes
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering eye rest");
                _isEyeRestNotificationActive = false;
                _isEyeRestEventProcessing = false; // SYNC FIX: Clear instance processing flag on error

                // THREAD SAFETY: Clear global processing flag on error
                lock (_globalEyeRestLock)
                {
                    _isAnyEyeRestEventProcessing = false;
                }
            }
        }

        private void TriggerBreakWarning(TimeSpan warningDuration)
        {
            try
            {
                // CRITICAL FIX: Check if break popup is already active to prevent duplicate warnings
                if (_isBreakNotificationActive)
                {
                    _logger.LogWarning("⚠️ BREAK POPUP PROTECTION: Break warning blocked - break popup already active. Ignoring duplicate break warning trigger.");
                    return;
                }

                // THREAD SAFETY: Global lock to prevent ALL warning systems from interfering
                lock (_globalBreakWarningLock)
                {
                    if (_isAnyBreakWarningProcessing)
                    {
                        _logger.LogWarning("⚠️ GLOBAL LOCK PREVENTION: Break warning already processing globally - ignoring duplicate trigger");
                        return;
                    }
                    _isAnyBreakWarningProcessing = true;
                }

                // SYNC FIX: Set instance warning processing flag to prevent backup trigger race conditions
                if (_isBreakWarningProcessing)
                {
                    _logger.LogWarning("⚠️ INSTANCE LOCK PREVENTION: Break warning already processing - ignoring duplicate trigger");
                    lock (_globalBreakWarningLock)
                    {
                        _isAnyBreakWarningProcessing = false;
                    }
                    return;
                }
                _isBreakWarningProcessing = true;

                // BREAK PRIORITY: Pause eye rest timer when break warning starts
                _logger.LogInformation("🔄 BREAK PRIORITY: Pausing eye rest timer during break warning");
                SmartPauseEyeRestTimerForBreak();

                _logger.LogInformation("⚠️ Triggering break warning - {Seconds} seconds remaining",
                    warningDuration.TotalSeconds);

                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = warningDuration,
                    Type = TimerType.BreakWarning
                };

                BreakWarning?.Invoke(this, eventArgs);

                // CRITICAL FIX: Don't record analytics here - warnings are not completed events
                // This was causing infinite loop by recording fake "30s completed" during warning phase
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering break warning");

                // CRITICAL FIX: Clear processing flags on error to prevent stuck state
                _isBreakWarningProcessing = false;
                lock (_globalBreakWarningLock)
                {
                    _isAnyBreakWarningProcessing = false;
                }
            }
        }

        private void TriggerBreak()
        {
            try
            {
                // THREAD SAFETY: Global lock to prevent ALL timer systems from interfering
                lock (_globalBreakLock)
                {
                    if (_isAnyBreakEventProcessing)
                    {
                        _logger.LogWarning("☕ GLOBAL LOCK PREVENTION: Break event already processing globally - ignoring duplicate trigger");
                        return;
                    }
                    _isAnyBreakEventProcessing = true;
                }

                // SYNC FIX: Set instance processing flag to prevent backup trigger race conditions
                if (_isBreakEventProcessing)
                {
                    _logger.LogWarning("☕ INSTANCE LOCK PREVENTION: Break event already processing - ignoring duplicate trigger");
                    lock (_globalBreakLock)
                    {
                        _isAnyBreakEventProcessing = false;
                    }
                    return;
                }
                _isBreakEventProcessing = true;

                // TIMELINE FIX: Record when main break timer actually triggered
                _lastBreakTriggeredTime = DateTime.Now;

                _logger.LogInformation("☕ Triggering break");

                // BREAK PRIORITY: Ensure eye rest timer is paused during break popup
                _logger.LogInformation("🔄 BREAK PRIORITY: Ensuring eye rest timer is paused during break popup");
                SmartPauseEyeRestTimerForBreak();

                // CRITICAL FIX: Don't set _isBreakNotificationActive here - it should already be set when warning started
                // This prevents duplicate state setting that can cause blocking issues

                // Stop warning timer
                _breakWarningTimer?.Stop();
                
                var duration = TimeSpan.FromMinutes(_configuration.Break.DurationMinutes);
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = duration,
                    Type = TimerType.Break
                };
                
                BreakDue?.Invoke(this, eventArgs);

                // CRITICAL FIX: Don't record analytics here - triggering an event is not completing it
                // Analytics should only be recorded based on actual user actions in the popup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering break");
                _isBreakNotificationActive = false;
                _isBreakEventProcessing = false; // SYNC FIX: Clear instance processing flag on error

                // THREAD SAFETY: Clear global processing flag on error
                lock (_globalBreakLock)
                {
                    _isAnyBreakEventProcessing = false;
                }
            }
        }

        #endregion

        #region Warning Timer Methods

        public void StartEyeRestWarningTimer()
        {
            // CRITICAL FIX: DispatcherTimer must be created/manipulated on UI thread
            // Remove Task.Run() that was causing thread safety violations and infinite loops
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                // Already on UI thread
                StartEyeRestWarningTimerInternal();
            }
            else
            {
                // Invoke on UI thread
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => 
                {
                    StartEyeRestWarningTimerInternal();
                }));
            }
        }

        public void StartBreakWarningTimer()
        {
            // CRITICAL FIX: DispatcherTimer must be created/manipulated on UI thread
            // Remove Task.Run() that was causing thread safety violations and infinite loops
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                // Already on UI thread
                StartBreakWarningTimerInternal();
            }
            else
            {
                // Invoke on UI thread
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => 
                {
                    StartBreakWarningTimerInternal();
                }));
            }
        }

        private void StartEyeRestWarningTimerInternal()
        {
            // CRITICAL FIX: Prevent duplicate warning timer starts that cause infinite loops and UI desync
            if (_eyeRestWarningTimer?.IsEnabled == true)
            {
                _logger.LogWarning("⚠️ Eye rest warning timer already running - skipping duplicate start to prevent infinite loop and UI countdown reset");
                return;
            }

            // CRITICAL FIX: Set notification active state when WARNING starts, not when eye rest triggers
            // This prevents the warning from being blocked by stale state
            _logger.LogInformation("⚠️ Starting eye rest warning - setting notification active state");
            _isEyeRestNotificationActive = true;
            
            if (_eyeRestWarningTimer != null && _notificationService != null)
            {
                
                var warningDuration = TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                _eyeRestWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
                {
                    try
                    {
                        if (hasTriggered) return; // Prevent multiple executions

                        var elapsed = DateTime.Now - startTime;
                        var remaining = warningDuration - elapsed;

                        // CRITICAL FIX: Use more tolerant timing check to handle precision issues
                        // If remaining time is <= 50ms or negative, consider warning complete
                        if (remaining.TotalMilliseconds <= 50)
                        {
                            hasTriggered = true; // Mark as triggered

                            // Warning period complete - stop timer and trigger eye rest
                            _eyeRestWarningTimer?.Stop();

                            _logger.LogInformation($"⏰ Eye rest warning period complete (remaining: {remaining.TotalMilliseconds:F1}ms) - triggering eye rest NOW");

                            // SYNC FIX: Clear warning processing flags before triggering main event
                            ClearEyeRestWarningProcessingFlag();

                            // CRITICAL FIX: Ensure TriggerEyeRest is called on UI thread
                            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                            {
                                TriggerEyeRest();
                            }
                            else
                            {
                                _logger.LogWarning("⏰ Eye rest warning timer handler not on UI thread - invoking TriggerEyeRest on UI thread");
                                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerEyeRest()));
                            }
                        }
                        else
                        {
                            // Update notification service with remaining time
                            _notificationService?.UpdateEyeRestWarningCountdown(remaining);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "⏰ ERROR in eye rest warning timer handler - forcing eye rest trigger");

                        // Force trigger eye rest on error to prevent stuck warning
                        if (!hasTriggered)
                        {
                            hasTriggered = true;
                            _eyeRestWarningTimer?.Stop();

                            try
                            {
                                // SYNC FIX: Clear warning processing flags before error recovery trigger
                                ClearEyeRestWarningProcessingFlag();

                                // TIMELINE FIX: Check if enough time has passed since last trigger
                                if (!ShouldAllowEyeRestFallback())
                                {
                                    _logger.LogInformation("🕒 ERROR RECOVERY BLOCKED: Eye rest error recovery blocked - insufficient time since last trigger");
                                    return;
                                }

                                // BREAK PRIORITY FIX: Check break priority before error recovery trigger
                                if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                                    _isBreakEventProcessing || _isAnyBreakEventProcessing)
                                {
                                    _logger.LogInformation("🔄 ERROR RECOVERY BLOCKED: Eye rest error recovery blocked - break event has priority. Pausing eye rest timer.");
                                    SmartPauseEyeRestTimerForBreak();
                                    return;
                                }

                                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                                {
                                    TriggerEyeRest();
                                }
                                else
                                {
                                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerEyeRest()));
                                }
                            }
                            catch (Exception triggerEx)
                            {
                                _logger.LogError(triggerEx, "⏰ CRITICAL: Failed to trigger eye rest even after error recovery");
                            }
                        }
                    }
                };
                
                // CRITICAL FIX: Dispose and recreate timer to clear ALL existing handlers
                _eyeRestWarningTimer.Stop();
                var oldTimer = _eyeRestWarningTimer;
                InitializeEyeRestWarningTimer(); // Creates fresh timer with no handlers
                oldTimer?.Dispose(); // Dispose old timer to prevent memory leaks
                
                // Now add the handler to the fresh timer
                _eyeRestWarningTimer.Tick += warningTickHandler;

                // CRITICAL FIX: Add fallback timer to prevent stuck eye rest warnings
                // This ensures eye rest is triggered even if the main warning timer fails
                // IMPORTANT: Stop and dispose any existing fallback timer to prevent ghost timers
                _eyeRestWarningFallbackTimer?.Stop();
                _eyeRestWarningFallbackTimer = null;

                _eyeRestWarningFallbackTimer = new System.Windows.Threading.DispatcherTimer();
                _eyeRestWarningFallbackTimer.Interval = warningDuration.Add(TimeSpan.FromSeconds(2)); // Warning duration + 2 second safety margin
                _eyeRestWarningFallbackTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        _eyeRestWarningFallbackTimer?.Stop();

                        if (!hasTriggered)
                        {
                            _logger.LogWarning($"⏰ FALLBACK: Eye rest warning timer failed to complete after {warningDuration.TotalSeconds + 2}s - checking if fallback trigger allowed");
                            _eyeRestWarningTimer?.Stop();

                            // SYNC FIX: Clear warning processing flags before fallback trigger
                            ClearEyeRestWarningProcessingFlag();

                            // TIMELINE FIX: Check if enough time has passed since last trigger
                            if (!ShouldAllowEyeRestFallback())
                            {
                                _logger.LogInformation("🕒 FALLBACK BLOCKED: Eye rest fallback blocked - insufficient time since last trigger");
                                return;
                            }

                            // BREAK PRIORITY FIX: Check break priority before fallback trigger
                            if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                                _isBreakEventProcessing || _isAnyBreakEventProcessing)
                            {
                                _logger.LogInformation("🔄 FALLBACK BLOCKED: Eye rest fallback trigger blocked - break event has priority. Pausing eye rest timer.");
                                SmartPauseEyeRestTimerForBreak();
                                return;
                            }

                            _logger.LogWarning("⏰ FALLBACK ALLOWED: Forcing eye rest trigger after validation");

                            // Force trigger eye rest as fallback
                            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                            {
                                TriggerEyeRest();
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerEyeRest()));
                            }
                        }
                        else
                        {
                            _logger.LogDebug("⏰ FALLBACK: Eye rest warning completed normally - fallback timer not needed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "⏰ ERROR in eye rest fallback timer - this should not happen");
                    }
                };

                // Initial trigger of warning event
                TriggerEyeRestWarning(warningDuration);

                // Start both timers
                _eyeRestWarningTimer.Start();
                _eyeRestWarningFallbackTimer.Start();

                _logger.LogInformation("Eye rest warning timer started - {Seconds}s countdown with fallback protection", warningDuration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Cannot start eye rest warning timer - timer or notification service is null");
            }
        }

        private void StartBreakWarningTimerInternal()
        {
            // CRITICAL FIX: Prevent duplicate warning timer starts that cause infinite loops and UI desync
            if (_breakWarningTimer?.IsEnabled == true)
            {
                _logger.LogWarning("⚠️ Break warning timer already running - skipping duplicate start to prevent infinite loop and UI countdown reset");
                return;
            }

            // Note: Don't set _isBreakNotificationActive here - that's only for the actual break popup
            // The warning should be allowed to show before the break popup
            _logger.LogInformation("⚠️ Starting break warning timer");
            
            if (_breakWarningTimer != null && _notificationService != null)
            {
                var warningDuration = TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
                {
                    try
                    {
                        if (hasTriggered) return; // Prevent multiple executions

                        var elapsed = DateTime.Now - startTime;
                        var remaining = warningDuration - elapsed;

                        // CRITICAL FIX: Use more tolerant timing check to handle precision issues
                        // If remaining time is <= 50ms or negative, consider warning complete
                        if (remaining.TotalMilliseconds <= 50)
                        {
                            hasTriggered = true; // Mark as triggered

                            // Warning period complete - stop timer and trigger break
                            _breakWarningTimer?.Stop();

                            _logger.LogInformation($"⏰ Break warning period complete (remaining: {remaining.TotalMilliseconds:F1}ms) - triggering break NOW");

                            // SYNC FIX: Clear warning processing flags before triggering main event
                            ClearBreakWarningProcessingFlag();

                            // CRITICAL FIX: Clear global break processing flag to allow main break trigger
                            // This prevents emergency recovery interference with normal break flow
                            lock (_globalBreakLock)
                            {
                                _isAnyBreakEventProcessing = false;
                                _logger.LogInformation("🔄 BREAK WARNING COMPLETE: Cleared global break processing flag to allow main break trigger");
                            }

                            // CRITICAL FIX: Ensure TriggerBreak is called on UI thread
                            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                            {
                                TriggerBreak();
                            }
                            else
                            {
                                _logger.LogWarning("⏰ Warning timer handler not on UI thread - invoking TriggerBreak on UI thread");
                                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerBreak()));
                            }
                        }
                        else
                        {
                            // Update notification service with remaining time
                            _notificationService?.UpdateBreakWarningCountdown(remaining);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "⏰ ERROR in break warning timer handler - forcing break trigger");

                        // Force trigger break on error to prevent stuck warning
                        if (!hasTriggered)
                        {
                            hasTriggered = true;
                            _breakWarningTimer?.Stop();

                            try
                            {
                                // SYNC FIX: Clear warning processing flags before error recovery trigger
                                ClearBreakWarningProcessingFlag();

                                // CRITICAL FIX: Clear global break processing flag to allow main break trigger
                                // This prevents emergency recovery interference with error recovery
                                lock (_globalBreakLock)
                                {
                                    _isAnyBreakEventProcessing = false;
                                    _logger.LogInformation("🔄 BREAK ERROR RECOVERY: Cleared global break processing flag to allow break trigger");
                                }

                                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                                {
                                    TriggerBreak();
                                }
                                else
                                {
                                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerBreak()));
                                }
                            }
                            catch (Exception triggerEx)
                            {
                                _logger.LogError(triggerEx, "⏰ CRITICAL: Failed to trigger break even after error recovery");
                            }
                        }
                    }
                };
                
                // CRITICAL FIX: Dispose and recreate timer to clear ALL existing handlers
                _breakWarningTimer.Stop();
                var oldTimer = _breakWarningTimer;
                InitializeBreakWarningTimer(); // Creates fresh timer with no handlers
                oldTimer?.Dispose(); // Dispose old timer to prevent memory leaks
                
                // Set interval and add handler to the fresh timer
                _breakWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                _breakWarningTimer.Tick += warningTickHandler;

                // CRITICAL FIX: Add fallback timer to prevent stuck break warnings
                // This ensures break is triggered even if the main warning timer fails
                // IMPORTANT: Stop and dispose any existing fallback timer to prevent ghost timers
                _breakWarningFallbackTimer?.Stop();
                _breakWarningFallbackTimer = null;

                _breakWarningFallbackTimer = new System.Windows.Threading.DispatcherTimer();
                _breakWarningFallbackTimer.Interval = warningDuration.Add(TimeSpan.FromSeconds(2)); // Warning duration + 2 second safety margin
                _breakWarningFallbackTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        _breakWarningFallbackTimer?.Stop();

                        if (!hasTriggered)
                        {
                            _logger.LogWarning($"⏰ FALLBACK: Break warning timer failed to complete after {warningDuration.TotalSeconds + 2}s - forcing break trigger");
                            _breakWarningTimer?.Stop();

                            // SYNC FIX: Clear warning processing flags before fallback trigger
                            ClearBreakWarningProcessingFlag();

                            // CRITICAL FIX: Clear global break processing flag to allow main break trigger
                            // This prevents emergency recovery interference with fallback trigger
                            lock (_globalBreakLock)
                            {
                                _isAnyBreakEventProcessing = false;
                                _logger.LogInformation("🔄 BREAK FALLBACK: Cleared global break processing flag to allow break trigger");
                            }

                            // Force trigger break as fallback
                            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                            {
                                TriggerBreak();
                            }
                            else
                            {
                                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => TriggerBreak()));
                            }
                        }
                        else
                        {
                            _logger.LogDebug("⏰ FALLBACK: Break warning completed normally - fallback timer not needed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "⏰ ERROR in fallback timer - this should not happen");
                    }
                };

                // Initial trigger of warning event
                TriggerBreakWarning(warningDuration);

                // Start both timers
                _breakWarningTimer.Start();
                _breakWarningFallbackTimer.Start();

                _logger.LogInformation("Break warning timer started - {Seconds}s countdown with fallback protection", warningDuration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Cannot start break warning timer - timer or notification service is null");
            }
        }

        #endregion
    }
}