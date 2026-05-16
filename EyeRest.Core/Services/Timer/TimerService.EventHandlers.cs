using System;
using System.Threading.Tasks;
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
                var now = _clock.Now;
                _logger.LogInformation($"👁️ TIMER EVENT: Eye rest timer tick fired at {now:HH:mm:ss.fff}");
                
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
                        _logger.LogWarning($"⏰ SYSTEM WAKE DETECTED: {wakeReason}");
                        _logger.LogWarning($"⏰ System likely woke from sleep/hibernation - initiating smart session reset");

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
                
                // State processing is thread-safe (volatile fields, Interlocked ops).
                // Don't re-dispatch entire handler to UI thread — on macOS, the UI thread
                // is throttled when the app is in the background, causing timer ticks to
                // never be processed. StartEyeRestWarningTimer() handles its own UI marshaling.

                UpdateHeartbeatFromOperation("OnEyeRestTimerTick");
                
                // CRITICAL FIX: Verify timer timing is correct
                var elapsed = now - _eyeRestStartTime;
                var expectedInterval = _eyeRestInterval;
                _logger.LogInformation("👁️ TIMER VERIFICATION: Timer elapsed={ElapsedMinutes:F2}m, expected={ExpectedMinutes:F2}m",
                    elapsed.TotalMinutes, expectedInterval.TotalMinutes);
                
                // CLOCK JUMP DETECTION: Check if elapsed time indicates system sleep
                // If elapsed > 2x expected interval, system likely slept
                if (elapsed > TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 2.0))
                {
                    _logger.LogWarning($"⏰ CLOCK JUMP DETECTED: Elapsed {elapsed.TotalMinutes:F1}min > 2x expected {expectedInterval.TotalMinutes:F1}min");
                    _logger.LogWarning($"⏰ System likely woke from sleep - initiating smart session reset");

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

                // SMART COALESCE: When the break is about to fire within the eye-rest occupancy window,
                // skip this eye-rest tick entirely. Running eye rest first would pause the break,
                // play out the eye-rest popup, then resume the break with ~0s remaining, causing
                // back-to-back popups. By skipping, we let the break fire naturally; the post-break
                // SmartSessionResetAsync will re-arm the eye rest timer fresh.
                //
                // The coalesce predicate is a side-effect-free check. We re-arm the eye-rest timer
                // afterwards so it will fire ~20m later (a safety net in case the break is cancelled
                // before firing — rare but possible if the user immediately pauses or stops timers).
                // SmartSessionResetAsync will overwrite the start time again on break completion.
                if (ShouldCoalesceEyeRestIntoBreak())
                {
                    _logger.LogInformation("👁️ COALESCE: skipping eye-rest tick to allow imminent break to fire alone");
                    _eyeRestTimer?.Stop();
                    // Restarting a DispatcherTimer synchronously from inside its own Tick handler
                    // can wedge Avalonia's entire timer queue (observed in production: a single
                    // coalesce tick stopped every DispatcherTimer in the app). Defer the re-arm
                    // so this tick fully unwinds before _eyeRestTimer.Start() runs again.
                    _dispatcherService.BeginInvoke(() => _ = RestartEyeRestTimerAfterCompletion());
                    return;
                }

                _logger.LogInformation("👁️ TIMER EVENT: Stopping eye rest timer and starting warning timer");
                _eyeRestTimer?.Stop();

                // Use public method to ensure proper thread safety
                StartEyeRestWarningTimer();
                
                _logger.LogInformation("👁️ TIMER EVENT: Eye rest timer tick completed successfully");
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
                var now = _clock.Now;
                _logger.LogInformation($"☕ TIMER EVENT: Break timer tick fired at {now:HH:mm:ss.fff}");
                
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
                        _logger.LogWarning($"⏰ SYSTEM WAKE DETECTED: {wakeReason}");
                        _logger.LogWarning($"⏰ System likely woke from sleep/hibernation - initiating smart session reset");

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
                
                // State processing is thread-safe (volatile fields, Interlocked ops).
                // Don't re-dispatch entire handler to UI thread — on macOS, the UI thread
                // is throttled when the app is in the background, causing timer ticks to
                // never be processed. StartBreakWarningTimer() handles its own UI marshaling.

                UpdateHeartbeatFromOperation("OnBreakTimerTick");
                
                // Additional safety: Check if enough time has elapsed since break start
                var elapsed = now - _breakStartTime;
                var expectedInterval = _breakInterval;
                
                // CLOCK JUMP DETECTION: Check if elapsed time indicates system sleep
                // If elapsed > 2x expected interval, system likely slept
                if (elapsed > TimeSpan.FromMinutes(expectedInterval.TotalMinutes * 2.0))
                {
                    _logger.LogWarning($"⏰ CLOCK JUMP DETECTED: Elapsed {elapsed.TotalMinutes:F1}min > 2x expected {expectedInterval.TotalMinutes:F1}min");
                    _logger.LogWarning($"⏰ System likely woke from sleep - initiating smart session reset");

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
                
                _logger.LogInformation("☕ TIMER EVENT: Stopping break timer and starting warning timer");
                _breakTimer?.Stop();
                
                // Use public method to ensure proper thread safety
                StartBreakWarningTimer();
                
                _logger.LogInformation("☕ TIMER EVENT: Break timer tick completed successfully");
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

                // ATOMIC FLAG OPERATION: Use Interlocked.CompareExchange for race-free check-and-set
                var previousValue = System.Threading.Interlocked.CompareExchange(ref _atomicEyeRestWarningProcessing, 1, 0);
                if (previousValue == 1)
                {
                    _logger.LogWarning("⚠️ ATOMIC LOCK PREVENTION: Eye rest warning already processing - ignoring duplicate trigger");
                    return;
                }

                // Also update legacy flags for backward compatibility
                lock (_globalEyeRestWarningLock)
                {
                    _isAnyEyeRestWarningProcessing = true;
                }
                _isEyeRestWarningProcessing = true;

                _logger.LogInformation("⚠️ Triggering eye rest warning - {Seconds} seconds remaining",
                    warningDuration.TotalSeconds);

                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = _clock.Now,
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

                // CRITICAL FIX: Clear all processing flags on error to prevent stuck state
                _isEyeRestWarningProcessing = false;
                lock (_globalEyeRestWarningLock)
                {
                    _isAnyEyeRestWarningProcessing = false;
                }

                // ATOMIC FLAG: Clear atomic flag on error
                System.Threading.Interlocked.Exchange(ref _atomicEyeRestWarningProcessing, 0);
            }
        }

        private void TriggerEyeRest()
        {
            try
            {
                // Guard: Don't show eye rest popup if timers were paused during the warning countdown
                if (IsPaused || IsManuallyPaused || IsSmartPaused)
                {
                    _logger.LogInformation("👁️ TriggerEyeRest blocked — service is paused (Manual={Manual}, Smart={Smart}, Paused={Paused})",
                        IsManuallyPaused, IsSmartPaused, IsPaused);
                    return;
                }

                // BREAK PRIORITY: Check if any break event is active or processing
                if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                    _isBreakEventProcessing || _isAnyBreakEventProcessing)
                {
                    _logger.LogInformation("🔄 BREAK PRIORITY: Eye rest popup blocked - break event has priority. Pausing eye rest timer.");

                    // Pause eye rest timer and reset after break completes
                    SmartPauseEyeRestTimerForBreak();
                    return;
                }

                // ATOMIC FLAG OPERATION: Use Interlocked.CompareExchange to atomically check and set
                // This eliminates the race window between checking and setting the flag
                var previousValue = System.Threading.Interlocked.CompareExchange(ref _atomicEyeRestProcessing, 1, 0);
                if (previousValue == 1)
                {
                    _logger.LogWarning("👁️ ATOMIC LOCK PREVENTION: Eye rest event already processing - ignoring duplicate trigger");
                    return;
                }

                // Also update the legacy flags for backward compatibility with other code paths
                lock (_globalEyeRestLock)
                {
                    _isAnyEyeRestEventProcessing = true;
                }
                _isEyeRestEventProcessing = true;

                // TIMELINE FIX: Record when main timer actually triggered
                _lastEyeRestTriggeredTime = _clock.Now;

                _logger.LogInformation("👁️ TRIGGER EYE REST: Starting popup at {Time}", _clock.Now.ToString("HH:mm:ss.fff"));

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
                    TriggeredAt = _clock.Now,
                    NextInterval = duration,
                    Type = TimerType.EyeRest
                };
                
                // CRITICAL FIX: Log before and after event trigger
                _logger.LogInformation("👁️ TRIGGER EYE REST: Invoking EyeRestDue event to show popup");
                EyeRestDue?.Invoke(this, eventArgs);
                _logger.LogInformation("👁️ TRIGGER EYE REST: EyeRestDue event invoked successfully");

                // CRITICAL FIX: Don't record analytics here - triggering an event is not completing it
                // Analytics should only be recorded based on actual user actions in the popup

                // Note: _isEyeRestEventProcessing flag will be cleared by ApplicationOrchestrator after popup completes
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering eye rest");
                _isEyeRestNotificationActive = false;
                _isEyeRestEventProcessing = false;

                // THREAD SAFETY: Clear all processing flags on error
                lock (_globalEyeRestLock)
                {
                    _isAnyEyeRestEventProcessing = false;
                }

                // ATOMIC FLAG: Clear atomic flag on error
                System.Threading.Interlocked.Exchange(ref _atomicEyeRestProcessing, 0);
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

                // ATOMIC FLAG OPERATION: Use Interlocked.CompareExchange for race-free check-and-set
                var previousValue = System.Threading.Interlocked.CompareExchange(ref _atomicBreakWarningProcessing, 1, 0);
                if (previousValue == 1)
                {
                    _logger.LogWarning("⚠️ ATOMIC LOCK PREVENTION: Break warning already processing - ignoring duplicate trigger");
                    return;
                }

                // Also update legacy flags for backward compatibility
                lock (_globalBreakWarningLock)
                {
                    _isAnyBreakWarningProcessing = true;
                }
                _isBreakWarningProcessing = true;

                // BREAK PRIORITY: Pause eye rest timer when break warning starts
                _logger.LogInformation("🔄 BREAK PRIORITY: Pausing eye rest timer during break warning");
                SmartPauseEyeRestTimerForBreak();

                _logger.LogInformation("⚠️ Triggering break warning - {Seconds} seconds remaining",
                    warningDuration.TotalSeconds);

                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = _clock.Now,
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

                // CRITICAL FIX: Clear all processing flags on error to prevent stuck state
                _isBreakWarningProcessing = false;
                lock (_globalBreakWarningLock)
                {
                    _isAnyBreakWarningProcessing = false;
                }

                // ATOMIC FLAG: Clear atomic flag on error
                System.Threading.Interlocked.Exchange(ref _atomicBreakWarningProcessing, 0);
            }
        }

        public Task TriggerImmediateBreakAsync()
        {
            _logger.LogInformation("☕ Manual break requested by user");
            if (!IsRunning)
            {
                _logger.LogInformation("☕ Manual break ignored — timer service is not running");
                return Task.CompletedTask;
            }
            if (IsAnyNotificationActive)
            {
                _logger.LogInformation("☕ Manual break ignored — popup already active");
                return Task.CompletedTask;
            }
            _dispatcherService.BeginInvoke(() => TriggerBreak(BreakTriggerSource.Manual));
            return Task.CompletedTask;
        }

        private void TriggerBreak(BreakTriggerSource source = BreakTriggerSource.Automatic)
        {
            try
            {
                // Guard: Don't show break popup if timers were paused during the warning countdown.
                // Manual triggers explicitly bypass this guard since the user has requested the break.
                bool ignorePauseGuard = source == BreakTriggerSource.Manual;
                if (!ignorePauseGuard && (IsPaused || IsManuallyPaused || IsSmartPaused))
                {
                    _logger.LogInformation("☕ TriggerBreak blocked — service is paused (Manual={Manual}, Smart={Smart}, Paused={Paused})",
                        IsManuallyPaused, IsSmartPaused, IsPaused);
                    return;
                }

                // ATOMIC FLAG OPERATION: Use Interlocked.CompareExchange to atomically check and set
                // This eliminates the race window between checking and setting the flag
                var previousValue = System.Threading.Interlocked.CompareExchange(ref _atomicBreakProcessing, 1, 0);
                if (previousValue == 1)
                {
                    _logger.LogWarning("☕ ATOMIC LOCK PREVENTION: Break event already processing - ignoring duplicate trigger");
                    return;
                }

                // Also update the legacy flags for backward compatibility with other code paths
                lock (_globalBreakLock)
                {
                    _isAnyBreakEventProcessing = true;
                }
                _isBreakEventProcessing = true;

                // TIMELINE FIX: Record when main break timer actually triggered
                _lastBreakTriggeredTime = _clock.Now;

                _logger.LogInformation("☕ Triggering break");

                // BREAK PRIORITY: Ensure eye rest timer is paused during break popup
                _logger.LogInformation("🔄 BREAK PRIORITY: Ensuring eye rest timer is paused during break popup");
                SmartPauseEyeRestTimerForBreak();

                // CRITICAL FIX: Stop ALL warning timers during break popup to prevent any other popup from firing
                // This ensures no eye rest reminder interrupts the break
                _logger.LogInformation("🔄 BREAK PRIORITY: Stopping all warning timers during break popup");
                _eyeRestWarningTimer?.Stop();
                _eyeRestWarningFallbackTimer?.Stop();
                _eyeRestWarningFallbackTimer = null;
                _isEyeRestNotificationActive = false;
                ClearEyeRestWarningProcessingFlag();
                _logger.LogInformation("🔄 BREAK PRIORITY: Eye rest warning timers stopped - no interruptions during break");

                // Mark break notification as active to prevent health check recovery from
                // restarting timers, and to guard against duplicate break triggers
                _isBreakNotificationActive = true;

                // Stop break timer — it must not run during the break popup
                _breakTimer?.Stop();

                // Stop break warning timer
                _breakWarningTimer?.Stop();
                
                var duration = TimeSpan.FromMinutes(_configuration.Break.DurationMinutes);
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = _clock.Now,
                    NextInterval = duration,
                    Type = TimerType.Break,
                    Source = source
                };

                BreakDue?.Invoke(this, eventArgs);

                // CRITICAL FIX: Don't record analytics here - triggering an event is not completing it
                // Analytics should only be recorded based on actual user actions in the popup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering break");
                _isBreakNotificationActive = false;
                _isBreakEventProcessing = false;

                // THREAD SAFETY: Clear all processing flags on error
                lock (_globalBreakLock)
                {
                    _isAnyBreakEventProcessing = false;
                }

                // ATOMIC FLAG: Clear atomic flag on error
                System.Threading.Interlocked.Exchange(ref _atomicBreakProcessing, 0);
            }
        }

        #endregion

        #region Warning Timer Methods

        public void StartEyeRestWarningTimer()
        {
            // CRITICAL FIX: Timer must be created/manipulated on UI thread
            if (_dispatcherService.CheckAccess())
            {
                StartEyeRestWarningTimerInternal();
            }
            else
            {
                _dispatcherService.BeginInvoke(() => StartEyeRestWarningTimerInternal());
            }
        }

        public void StartBreakWarningTimer()
        {
            // CRITICAL FIX: Timer must be created/manipulated on UI thread
            if (_dispatcherService.CheckAccess())
            {
                StartBreakWarningTimerInternal();
            }
            else
            {
                _dispatcherService.BeginInvoke(() => StartBreakWarningTimerInternal());
            }
        }

        /// <summary>
        /// Stops the eye rest warning timer when user dismisses the warning popup early.
        /// CRITICAL: This prevents the infinite loop where dismissed warning still triggers main popup.
        /// </summary>
        public void StopEyeRestWarningTimer()
        {
            if (_dispatcherService.CheckAccess())
            {
                StopEyeRestWarningTimerInternal();
            }
            else
            {
                _dispatcherService.BeginInvoke(() => StopEyeRestWarningTimerInternal());
            }
        }

        private void StopEyeRestWarningTimerInternal()
        {
            _logger.LogInformation("⏹️ Stopping eye rest warning timer - popup dismissed early");

            // Stop the main warning timer
            _eyeRestWarningTimer?.Stop();

            // Stop the fallback timer
            _eyeRestWarningFallbackTimer?.Stop();
            _eyeRestWarningFallbackTimer = null;

            // Clear notification active state
            _isEyeRestNotificationActive = false;

            // Clear processing flags
            ClearEyeRestWarningProcessingFlag();

            _logger.LogInformation("⏹️ Eye rest warning timer stopped - user dismissed popup before warning period completed");
        }

        /// <summary>
        /// Stops the break warning timer when user dismisses the warning popup early.
        /// CRITICAL: This prevents the infinite loop where dismissed warning still triggers main popup.
        /// </summary>
        public void StopBreakWarningTimer()
        {
            if (_dispatcherService.CheckAccess())
            {
                StopBreakWarningTimerInternal();
            }
            else
            {
                _dispatcherService.BeginInvoke(() => StopBreakWarningTimerInternal());
            }
        }

        private void StopBreakWarningTimerInternal()
        {
            _logger.LogInformation("⏹️ Stopping break warning timer - popup dismissed early");

            // Stop the main warning timer
            _breakWarningTimer?.Stop();

            // Stop the fallback timer
            _breakWarningFallbackTimer?.Stop();
            _breakWarningFallbackTimer = null;

            // Note: Don't clear _isBreakNotificationActive here as that's for the actual break popup, not warning

            // Clear processing flags
            ClearBreakWarningProcessingFlag();

            _logger.LogInformation("⏹️ Break warning timer stopped - user dismissed popup before warning period completed");
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
                var startTime = _clock.Now;
                var hasTriggered = false; // Prevent multiple triggers

                // NOTE: Interval is set AFTER timer recreation below (line ~810)

                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
                {
                    try
                    {
                        if (hasTriggered) return; // Prevent multiple executions

                        var elapsed = _clock.Now - startTime;
                        var remaining = warningDuration - elapsed;

                        // CRITICAL FIX: Detect orphaned warning handler (stale state after session reset)
                        // If remaining time is significantly negative (more than 1 second overdue), this indicates
                        // that the handler is still running with a captured startTime from BEFORE session reset
                        if (remaining.TotalSeconds < -1)
                        {
                            _logger.LogWarning($"🚨 ORPHANED HANDLER DETECTED: Eye rest warning shows {remaining.TotalSeconds:F1}s remaining (negative). Session likely reset!");
                            _logger.LogWarning($"🚨 Handler startTime: {startTime:HH:mm:ss.fff}, Now: {_clock.Now:HH:mm:ss.fff}, Elapsed: {elapsed.TotalSeconds:F1}s");
                            _logger.LogWarning($"🚨 Aborting orphaned handler execution - session has been reset and this handler is stale");
                            hasTriggered = true; // Mark as triggered to prevent further execution
                            return;
                        }

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
                            if (_dispatcherService.CheckAccess())
                            {
                                TriggerEyeRest();
                            }
                            else
                            {
                                _logger.LogWarning("⏰ Eye rest warning timer handler not on UI thread - invoking TriggerEyeRest on UI thread");
                                _dispatcherService.BeginInvoke(() => TriggerEyeRest());
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

                                if (_dispatcherService.CheckAccess())
                                {
                                    TriggerEyeRest();
                                }
                                else
                                {
                                    _dispatcherService.BeginInvoke(() => TriggerEyeRest());
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
                
                // Set interval and add handler to the fresh timer
                _eyeRestWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // 100ms for smooth countdown
                _eyeRestWarningTimer.Tick += warningTickHandler;

                // CRITICAL FIX: Add fallback timer to prevent stuck eye rest warnings
                // This ensures eye rest is triggered even if the main warning timer fails
                // IMPORTANT: Stop and dispose any existing fallback timer to prevent ghost timers
                _eyeRestWarningFallbackTimer?.Stop();
                _eyeRestWarningFallbackTimer?.Dispose();
                _eyeRestWarningFallbackTimer = null;

                _eyeRestWarningFallbackTimer = _timerFactory.CreateTimer();
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
                            if (_dispatcherService.CheckAccess())
                            {
                                TriggerEyeRest();
                            }
                            else
                            {
                                _dispatcherService.BeginInvoke(() => TriggerEyeRest());
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
                var startTime = _clock.Now;
                var hasTriggered = false; // Prevent multiple triggers

                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
                {
                    try
                    {
                        if (hasTriggered) return; // Prevent multiple executions

                        var elapsed = _clock.Now - startTime;
                        var remaining = warningDuration - elapsed;

                        // CRITICAL FIX: Detect orphaned warning handler (stale state after session reset)
                        // If remaining time is significantly negative (more than 1 second overdue), this indicates
                        // that the handler is still running with a captured startTime from BEFORE session reset
                        if (remaining.TotalSeconds < -1)
                        {
                            _logger.LogWarning($"🚨 ORPHANED HANDLER DETECTED: Break warning shows {remaining.TotalSeconds:F1}s remaining (negative). Session likely reset!");
                            _logger.LogWarning($"🚨 Handler startTime: {startTime:HH:mm:ss.fff}, Now: {_clock.Now:HH:mm:ss.fff}, Elapsed: {elapsed.TotalSeconds:F1}s");
                            _logger.LogWarning($"🚨 Aborting orphaned handler execution - session has been reset and this handler is stale");
                            hasTriggered = true; // Mark as triggered to prevent further execution
                            return;
                        }

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
                            if (_dispatcherService.CheckAccess())
                            {
                                TriggerBreak();
                            }
                            else
                            {
                                _logger.LogWarning("⏰ Warning timer handler not on UI thread - invoking TriggerBreak on UI thread");
                                _dispatcherService.BeginInvoke(() => TriggerBreak());
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

                                if (_dispatcherService.CheckAccess())
                                {
                                    TriggerBreak();
                                }
                                else
                                {
                                    _dispatcherService.BeginInvoke(() => TriggerBreak());
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
                _breakWarningFallbackTimer?.Dispose();
                _breakWarningFallbackTimer = null;

                _breakWarningFallbackTimer = _timerFactory.CreateTimer();
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
                            if (_dispatcherService.CheckAccess())
                            {
                                TriggerBreak();
                            }
                            else
                            {
                                _dispatcherService.BeginInvoke(() => TriggerBreak());
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