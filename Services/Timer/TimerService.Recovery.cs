using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing system recovery and health monitoring functionality
    /// </summary>
    public partial class TimerService
    {
        #region Health Monitoring

        // Health monitoring fields
        private DateTime _lastHeartbeat = DateTime.Now;
        // Health monitor timer is declared in TimerService.State.cs

        // Recovery debouncing fields (Fix #4: Prevent duplicate recovery attempts)
        private DateTime _lastRecoveryAttempt = DateTime.MinValue;
        private const int RECOVERY_DEBOUNCE_SECONDS = 5;
        
        private void InitializeHealthMonitor()
        {
            if (_healthMonitorTimer == null)
            {
                _healthMonitorTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
                _healthMonitorTimer.Interval = TimeSpan.FromMinutes(1); // Check every minute
                
                _healthMonitorTimer.Tick += OnHealthMonitorTick;
                
                _logger.LogInformation("❤️ Health monitor initialized - checking timer health every minute");
            }
        }
        
        /// <summary>
        /// Update heartbeat more frequently to prevent false recovery triggers
        /// Call this from various timer operations, not just timer events
        /// </summary>
        public void UpdateHeartbeatFromOperation(string operation)
        {
            UpdateHeartbeat();
            _logger.LogDebug($"❤️ Heartbeat updated from {operation}");
        }

        private void OnHealthMonitorTick(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var timeSinceLastHeartbeat = now - _lastHeartbeat;
                
                _logger.LogCritical($"❤️ HEALTH CHECK at {now:HH:mm:ss.fff} - Last heartbeat: {timeSinceLastHeartbeat.TotalSeconds:F1}s ago");
                
                // CRITICAL FIX: Skip health checks during initial startup to prevent false positives
                if (!_hasCompletedInitialStartup)
                {
                    _logger.LogInformation("❤️ Skipping health check - initial startup not yet complete");
                    UpdateHeartbeat(); // Update heartbeat to prevent false hang detection
                    return;
                }
                
                // Log current process and memory status
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    _logger.LogCritical($"❤️ SERVICE STATUS: Running={IsRunning}, Paused={IsPaused}, SmartPaused={IsSmartPaused}");
                    _logger.LogCritical($"❤️ TIMER STATUS: EyeRest={_eyeRestTimer?.IsEnabled}, Break={_breakTimer?.IsEnabled}");
                    _logger.LogCritical($"❤️ PROCESS STATUS: ID={process.Id}, Memory={process.WorkingSet64 / 1024 / 1024}MB");
                }
                
                // SOLID FIX: Calculate dynamic heartbeat threshold based on current timer intervals
                var dynamicThresholdMinutes = CalculateDynamicHeartbeatThreshold();
                _logger.LogCritical($"❤️ DYNAMIC THRESHOLD: {dynamicThresholdMinutes:F1} minutes (based on current timer intervals)");
                
                // ENHANCED: Check for multiple hang conditions
                bool hangDetected = false;
                string hangReason = "";
                
                // Standard threshold check
                if (timeSinceLastHeartbeat.TotalMinutes > dynamicThresholdMinutes)
                {
                    hangDetected = true;
                    hangReason = $"No heartbeat for {timeSinceLastHeartbeat.TotalMinutes:F1} minutes (threshold: {dynamicThresholdMinutes:F1}min)";
                }
                
                // CRITICAL: Aggressive detection for disabled timers with due events
                // Check actual timer state instead of TimeUntil* properties since they now return defaults when !IsRunning
                var eyeRestOverdue = !(_eyeRestTimer?.IsEnabled ?? false) && !IsRunning && 
                                   (_eyeRestStartTime != DateTime.MinValue && DateTime.Now - _eyeRestStartTime >= _eyeRestInterval);
                var breakOverdue = !(_breakTimer?.IsEnabled ?? false) && !IsRunning && 
                                 (_breakStartTime != DateTime.MinValue && DateTime.Now - _breakStartTime >= _breakInterval);
                var hasDisabledTimersDue = eyeRestOverdue || breakOverdue;
                
                if (hasDisabledTimersDue && timeSinceLastHeartbeat.TotalMinutes >= 5.0)
                {
                    hangDetected = true;
                    hangReason = $"Disabled timer(s) with due events - EyeRestOverdue={eyeRestOverdue}, BreakOverdue={breakOverdue}, IsRunning={IsRunning}";
                }
                
                // CRITICAL: Stuck timer detection - same overdue time for extended period
                if ((eyeRestOverdue || breakOverdue) && timeSinceLastHeartbeat.TotalMinutes >= 3.0)
                {
                    hangDetected = true;
                    hangReason = $"Timer(s) overdue with no progress - EyeRestOverdue={eyeRestOverdue}, BreakOverdue={breakOverdue}, NoHeartbeat={timeSinceLastHeartbeat.TotalMinutes:F1}min";
                }
                
                // CRITICAL FIX: Manual pause state coordination validation
                var manualPauseCleared = !IsManuallyPaused;
                var manualPauseTimeCleared = _manualPauseStartTime == DateTime.MinValue;
                var timersRunning = IsRunning;
                
                // Detect manual pause cleared but timers not running - coordination failure
                if (manualPauseCleared && manualPauseTimeCleared && !timersRunning && !IsSmartPaused && !IsPaused && timeSinceLastHeartbeat.TotalMinutes >= 1.0)
                {
                    hangDetected = true;
                    hangReason = $"Manual pause cleared but timer service stopped - state coordination failure detected (heartbeat: {timeSinceLastHeartbeat.TotalMinutes:F1}min)";
                    _logger.LogCritical($"🚨 COORDINATION FAILURE: Manual pause cleared ({manualPauseCleared}) but service stopped ({timersRunning}) - this causes UI 'Paused (Manual)' display bug");
                }
                
                // CRITICAL FIX: Timer state validation - service running but timers disabled
                var serviceRunningButTimersDisabled = IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused &&
                                                    (_eyeRestTimer?.IsEnabled != true || _breakTimer?.IsEnabled != true) &&
                                                    timeSinceLastHeartbeat.TotalMinutes >= 2.0;
                
                if (serviceRunningButTimersDisabled && !hangDetected)
                {
                    hangDetected = true;
                    hangReason = $"Service running but timers disabled - EyeRest enabled: {_eyeRestTimer?.IsEnabled}, Break enabled: {_breakTimer?.IsEnabled} (heartbeat: {timeSinceLastHeartbeat.TotalMinutes:F1}min)";
                    _logger.LogCritical($"🚨 TIMER STATE FAILURE: Service={IsRunning}, Paused={IsPaused}, SmartPaused={IsSmartPaused}, ManualPaused={IsManuallyPaused}");
                    _logger.LogCritical($"🚨 TIMER ENABLED STATE: EyeRest={_eyeRestTimer?.IsEnabled}, Break={_breakTimer?.IsEnabled}");
                }
                
                if (hangDetected)
                {
                    _logger.LogCritical($"🚨 TIMER HANG DETECTED - {hangReason}!");
                    
                    // CRITICAL FIX: Special handling for manual pause coordination failures
                    if (hangReason.Contains("Manual pause cleared but timer service stopped"))
                    {
                        _logger.LogCritical($"🔧 COORDINATION RECOVERY: Attempting direct timer service restart for manual pause issue");
                        
                        // Use Task.Run since health monitor tick can't be async
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await StartAsync(); // Direct restart instead of full recovery
                                _logger.LogCritical($"🔧 COORDINATION FIX: Timer service restarted, IsRunning={IsRunning}");
                                
                                // CRITICAL FIX: Force UI synchronization after coordination repair
                                OnPropertyChanged(nameof(IsRunning));
                                OnPropertyChanged(nameof(IsPaused));
                                OnPropertyChanged(nameof(IsManuallyPaused));
                                OnPropertyChanged(nameof(IsSmartPaused));
                                _logger.LogCritical($"🔧 COORDINATION FIX: UI state synchronized after manual pause coordination repair");
                            }
                            catch (Exception restartEx)
                            {
                                _logger.LogError(restartEx, "🔧 COORDINATION FIX: Failed direct restart, falling back to full recovery");
                                RecoverTimersFromHang(); // Fallback to full recovery
                            }
                        });
                    }
                    // CRITICAL FIX: Special handling for timer state failures (service running but timers disabled)
                    else if (hangReason.Contains("Service running but timers disabled"))
                    {
                        _logger.LogCritical($"🔧 TIMER STATE RECOVERY: Attempting to re-enable disabled timers");
                        
                        // Use Task.Run since health monitor tick can't be async
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Try to recreate and restart timers without full service restart
                                if (_eyeRestTimer == null)
                                {
                                    _logger.LogCritical($"🔧 TIMER STATE FIX: Recreating null eye rest timer");
                                    InitializeEyeRestTimer();
                                }
                                if (_breakTimer == null)
                                {
                                    _logger.LogCritical($"🔧 TIMER STATE FIX: Recreating null break timer");
                                    InitializeBreakTimer();
                                }
                                
                                // Start the timers
                                _eyeRestTimer?.Start();
                                _breakTimer?.Start();
                                UpdateHeartbeatFromOperation("Timer state recovery");
                                
                                _logger.LogCritical($"🔧 TIMER STATE FIX: Timers restarted - EyeRest={_eyeRestTimer?.IsEnabled}, Break={_breakTimer?.IsEnabled}");
                                
                                // Force UI updates
                                OnPropertyChanged(nameof(TimeUntilNextEyeRest));
                                OnPropertyChanged(nameof(TimeUntilNextBreak));
                                OnPropertyChanged(nameof(NextEventDescription));
                            }
                            catch (Exception stateEx)
                            {
                                _logger.LogError(stateEx, "🔧 TIMER STATE FIX: Failed to fix timer state, falling back to full recovery");
                                RecoverTimersFromHang(); // Fallback to full recovery
                            }
                        });
                    }
                    else
                    {
                        _logger.LogCritical($"🔧 INITIATING TIMER RECOVERY - Attempting to fix timer hang");
                        RecoverTimersFromHang();
                    }
                }
                
                // ZOMBIE POPUP DETECTION: Check for stuck break confirmation popups
                if (!hangDetected && IsSmartPaused && _pauseReason?.Contains("Waiting for break confirmation") == true)
                {
                    var timeSinceSmartPause = now - _pauseStartTime;
                    const int stuckPopupThresholdMinutes = 15; // Slightly longer than NotificationService timeout (10min)
                    
                    if (timeSinceSmartPause.TotalMinutes > stuckPopupThresholdMinutes)
                    {
                        _logger.LogCritical($"🚨 ZOMBIE POPUP DETECTED: Smart paused for break confirmation for {timeSinceSmartPause.TotalMinutes:F1} minutes (threshold: {stuckPopupThresholdMinutes}min)");
                        _logger.LogCritical($"🚨 ZOMBIE POPUP STATE: PauseReason='{_pauseReason}', PauseStartTime={_pauseStartTime:HH:mm:ss}");
                        
                        // Force recovery by clearing smart pause state
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                _logger.LogCritical($"🔧 ZOMBIE POPUP RECOVERY: Force resuming timers from stuck break confirmation");
                                await SmartResumeAsync("Health Monitor: Zombie popup detection - force recovery");
                                _logger.LogCritical($"✅ ZOMBIE POPUP RECOVERY: Successfully resumed timers, IsSmartPaused={IsSmartPaused}");
                                
                                // Show user notification about the recovery
                                try
                                {
                                    await _pauseReminderService.ShowRecoveryNotificationAsync(
                                        "Timer System Recovery", 
                                        $"Detected stuck break confirmation for {timeSinceSmartPause.TotalMinutes:F0} minutes and automatically recovered."
                                    );
                                }
                                catch (Exception notifyEx)
                                {
                                    _logger.LogError(notifyEx, "Failed to show recovery notification for zombie popup detection");
                                }
                            }
                            catch (Exception recoveryEx)
                            {
                                _logger.LogError(recoveryEx, "❌ ZOMBIE POPUP RECOVERY FAILED: Could not force resume from stuck break confirmation");
                                // If smart resume fails, try full timer recovery as fallback
                                RecoverTimersFromHang();
                            }
                        });
                        
                        // Skip remaining health checks since we're handling recovery
                        return;
                    }
                    else
                    {
                        _logger.LogInformation($"⏳ Break confirmation waiting: {timeSinceSmartPause.TotalMinutes:F1}min (threshold: {stuckPopupThresholdMinutes}min)");
                    }
                }
                
                // BACKUP TRIGGER SYSTEM: Fire overdue events if no active popups
                // This catches cases where timer recovery doesn't help but events still need to fire
                // CRITICAL FIX: Also trigger when timer service is stopped (!IsRunning) with overdue events
                // Use direct timer state check instead of TimeUntil* since those now return defaults when !IsRunning
                var serviceStoppedWithDueEvents = !IsRunning && (eyeRestOverdue || breakOverdue);
                var serviceRunningButNotPaused = IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused && !IsBreakDelayed;

                if (!hangDetected && (serviceRunningButNotPaused || serviceStoppedWithDueEvents))
                {
                    var hasActivePopups = _notificationService?.IsAnyPopupActive ?? false;
                    // For running service, check TimeUntil* properties. For stopped service, check direct overdue state
                    // THREAD SAFETY: Check both instance and global processing flags to prevent ALL race conditions
                    // ENHANCED: Also check warning processing flags to prevent interference with warning systems
                    // CRITICAL FIX: Account for warning periods when determining if fallback trigger is needed
                    var needsEyeRestTrigger = IsRunning ? (IsEyeRestTrulyOverdue() && !_isEyeRestNotificationActive && !_isEyeRestEventProcessing && !_isAnyEyeRestEventProcessing && !_isEyeRestWarningProcessing && !_isAnyEyeRestWarningProcessing) :
                                             (eyeRestOverdue && !_isEyeRestNotificationActive && !_isEyeRestEventProcessing && !_isAnyEyeRestEventProcessing && !_isEyeRestWarningProcessing && !_isAnyEyeRestWarningProcessing);
                    var needsBreakTrigger = IsRunning ? (IsBreakTrulyOverdue() && !_isBreakNotificationActive && !_isBreakEventProcessing && !_isAnyBreakEventProcessing && !_isBreakWarningProcessing && !_isAnyBreakWarningProcessing) :
                                           (breakOverdue && !_isBreakNotificationActive && !_isBreakEventProcessing && !_isAnyBreakEventProcessing && !_isBreakWarningProcessing && !_isAnyBreakWarningProcessing);
                    
                    // CRITICAL FIX: Add startup grace period check to prevent early triggers
                    var serviceUptime = DateTime.Now - (_eyeRestStartTime != DateTime.MinValue ? _eyeRestStartTime : DateTime.Now);
                    if (serviceUptime < TimeSpan.FromSeconds(30))
                    {
                        _logger.LogInformation($"🛡️ STARTUP PROTECTION: Ignoring backup triggers during startup grace period (uptime={serviceUptime.TotalSeconds:F1}s)");
                        return;
                    }
                    
                    if ((needsEyeRestTrigger || needsBreakTrigger) && !hasActivePopups)
                    {
                        var triggerReason = serviceStoppedWithDueEvents ? "SERVICE_STOPPED_WITH_DUE_EVENTS" : "SERVICE_RUNNING_BUT_NO_POPUPS";
                        _logger.LogCritical($"🔥 BACKUP TRIGGER SYSTEM ({triggerReason}): Firing overdue events - EyeRest={needsEyeRestTrigger}, Break={needsBreakTrigger}, IsRunning={IsRunning}");
                        
                        if (needsEyeRestTrigger)
                        {
                            // TIMELINE FIX: Check if enough time has passed since last trigger
                            if (!ShouldAllowEyeRestFallback())
                            {
                                _logger.LogInformation("🕒 RECOVERY BLOCKED: Eye rest recovery blocked - insufficient time since last trigger");
                            }
                            // BREAK PRIORITY FIX: Check break priority before recovery trigger
                            else if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                                _isBreakEventProcessing || _isAnyBreakEventProcessing)
                            {
                                _logger.LogInformation("🔄 RECOVERY BLOCKED: Eye rest recovery trigger blocked - break event has priority. Pausing eye rest timer.");
                                SmartPauseEyeRestTimerForBreak();
                            }
                            else
                            {
                                _logger.LogCritical($"🔥 Backup triggering overdue eye rest event");
                                TriggerEyeRest();

                                // CRITICAL FIX: Reset eye rest timer to prevent normal timer from also firing
                                // This prevents double triggers when backup system manually triggers
                                var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();
                                _eyeRestInterval = interval;
                                _eyeRestTimer.Interval = _eyeRestInterval;
                                _eyeRestTimer.Start();
                                _eyeRestStartTime = DateTime.Now;

                                _logger.LogCritical("🔄 BACKUP RESET: Eye rest timer reset after backup trigger - interval: {IntervalMinutes:F1}m, next trigger: {NextTime}",
                                    _eyeRestInterval.TotalMinutes, DateTime.Now.Add(_eyeRestInterval).ToString("HH:mm:ss"));
                            }
                        }
                        
                        if (needsBreakTrigger)
                        {
                            _logger.LogCritical($"🔥 Backup triggering overdue break event");
                            TriggerBreak();

                            // CRITICAL FIX: Reset break timer to prevent normal timer from also firing
                            // This prevents double triggers when backup system manually triggers
                            var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();
                            _breakInterval = interval;
                            _breakTimer.Interval = _breakInterval;
                            _breakTimer.Start();
                            _breakStartTime = DateTime.Now;

                            _logger.LogCritical("🔄 BACKUP RESET: Break timer reset after backup trigger - interval: {IntervalMinutes:F1}m, next trigger: {NextTime}",
                                _breakInterval.TotalMinutes, DateTime.Now.Add(_breakInterval).ToString("HH:mm:ss"));
                        }
                        
                        // ADDITIONAL FIX: If service is stopped, attempt to restart it after firing backup triggers
                        if (serviceStoppedWithDueEvents)
                        {
                            _logger.LogCritical($"🔄 ATTEMPTING SERVICE RESTART: Timer service is stopped with due events");
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(1000); // Brief delay to let backup triggers complete
                                    _logger.LogCritical($"🔄 RESTART ATTEMPT: Starting timer service");
                                    await StartAsync();
                                    _logger.LogCritical($"🔄 RESTART SUCCESS: Timer service restarted, IsRunning={IsRunning}");
                                }
                                catch (Exception restartEx)
                                {
                                    _logger.LogError(restartEx, "🔄 RESTART FAILED: Could not restart timer service");
                                }
                            });
                        }
                    }
                }
                
                // CRITICAL FIX: Additional check for overdue timer events (timer events not firing despite being overdue)
                // This handles the case where timers exist and are enabled but Tick events don't fire
                if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                {
                    // CRITICAL FIX: Skip overdue checks during break delay period
                    // When break is delayed, eye rest timers are intentionally stopped and should not trigger recovery
                    if (IsBreakDelayed)
                    {
                        _logger.LogInformation($"🔄 HEALTH CHECK: Skipping overdue checks during break delay period (remaining: {DelayRemaining.TotalSeconds:F1}s)");
                        return;
                    }

                    // CRITICAL FIX: Check if notifications are active before declaring timers overdue
                    // When popups are showing, TimeUntilNextEyeRest/Break returns 0 or negative by design
                    if (_isEyeRestNotificationActive || _isBreakNotificationActive)
                    {
                        // Popups are active, timers are working correctly
                        return;
                    }

                    var eyeRestRemaining = TimeUntilNextEyeRest;
                    var breakRemaining = TimeUntilNextBreak;

                    if (eyeRestRemaining <= TimeSpan.Zero || breakRemaining <= TimeSpan.Zero)
                    {
                        var overdueDescription = "";
                        if (eyeRestRemaining <= TimeSpan.Zero && !_isEyeRestNotificationActive)
                            overdueDescription += $"Eye rest overdue by {Math.Abs(eyeRestRemaining.TotalSeconds):F1}s ";
                        if (breakRemaining <= TimeSpan.Zero && !_isBreakNotificationActive)
                            overdueDescription += $"Break overdue by {Math.Abs(breakRemaining.TotalSeconds):F1}s ";

                        // Only trigger recovery if we have actual overdue events (not just active popups)
                        if (string.IsNullOrWhiteSpace(overdueDescription))
                        {
                            return;
                        }

                        _logger.LogCritical($"🚨 OVERDUE TIMER EVENTS: {overdueDescription.Trim()} - timer events are not firing!");
                        
                        // Use Task.Run since health monitor tick can't be async
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var success = await ForceTimerRecoveryAsync("Overdue timer events detected by health monitor");
                                if (success)
                                {
                                    _logger.LogCritical($"🚨 EMERGENCY RECOVERY SUCCESS: Timer events should now be firing correctly");
                                }
                                else
                                {
                                    _logger.LogCritical($"🚨 EMERGENCY RECOVERY FAILED: Manual application restart may be required");
                                }
                            }
                            catch (Exception recoveryEx)
                            {
                                _logger.LogError(recoveryEx, $"🚨 EMERGENCY RECOVERY failed with exception");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitor tick");
            }
        }
        
        /// <summary>
        /// SOLID SOLUTION: Calculate dynamic heartbeat threshold based on current timer configurations
        /// Ensures recovery threshold is always longer than the longest configured timer interval
        /// </summary>
        private double CalculateDynamicHeartbeatThreshold()
        {
            try
            {
                // Get current timer intervals (in minutes)
                var eyeRestMinutes = _eyeRestTimer?.Interval.TotalMinutes ?? 20.0; // Default 20 minutes
                var breakMinutes = _breakTimer?.Interval.TotalMinutes ?? 55.0;     // Default 55 minutes
                
                // Find the longest timer interval
                var longestIntervalMinutes = Math.Max(eyeRestMinutes, breakMinutes);
                
                // Set threshold to 125% of longest interval + 5 minute buffer
                // This ensures recovery only triggers when timers are truly stuck
                var dynamicThreshold = (longestIntervalMinutes * 1.25) + 5.0;
                
                // Minimum threshold of 10 minutes (for very short custom intervals)
                // Maximum threshold of 120 minutes (for very long custom intervals)
                var clampedThreshold = Math.Max(10.0, Math.Min(120.0, dynamicThreshold));
                
                _logger.LogDebug($"🧮 THRESHOLD CALCULATION: EyeRest={eyeRestMinutes:F1}min, Break={breakMinutes:F1}min, " +
                                $"Longest={longestIntervalMinutes:F1}min, Dynamic={dynamicThreshold:F1}min, Final={clampedThreshold:F1}min");
                
                return clampedThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating dynamic heartbeat threshold, using fallback");
                return 30.0; // Safe fallback: 30 minutes
            }
        }

        /// <summary>
        /// CRITICAL FIX: Determines if eye rest is truly overdue, accounting for warning periods
        /// This prevents fallback triggers during normal warning countdown
        /// </summary>
        private bool IsEyeRestTrulyOverdue()
        {
            // If notification is active (warning or main popup), not overdue
            if (_isEyeRestNotificationActive || _isEyeRestWarningProcessing || _isAnyEyeRestWarningProcessing)
                return false;

            // If eye rest timer not running, use direct time calculation
            if (_eyeRestTimer?.IsEnabled != true || _eyeRestStartTime == DateTime.MinValue)
                return false;

            var elapsed = DateTime.Now - _eyeRestStartTime;
            var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();

            // Calculate the TOTAL interval (including warning time) to determine true overdue state
            var totalInterval = TimeSpan.FromMinutes(totalMinutes); // Full interval (e.g., 5 minutes)

            // Only consider truly overdue if elapsed time exceeds the FULL interval
            // This prevents fallback triggers during the warning period
            var isOverdue = elapsed > totalInterval;

            if (isOverdue)
            {
                _logger.LogWarning("👁️ FALLBACK CHECK: Eye rest is truly overdue - elapsed {ElapsedMinutes:F1}m > total {TotalMinutes:F1}m",
                    elapsed.TotalMinutes, totalInterval.TotalMinutes);
            }

            return isOverdue;
        }

        /// <summary>
        /// CRITICAL FIX: Determines if break is truly overdue, accounting for warning periods
        /// This prevents fallback triggers during normal warning countdown
        /// </summary>
        private bool IsBreakTrulyOverdue()
        {
            // If notification is active (warning or main popup), not overdue
            if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing)
                return false;

            // If break timer not running, use direct time calculation
            if (_breakTimer?.IsEnabled != true || _breakStartTime == DateTime.MinValue)
                return false;

            var elapsed = DateTime.Now - _breakStartTime;
            var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();

            // Calculate the TOTAL interval (including warning time) to determine true overdue state
            var totalInterval = TimeSpan.FromMinutes(totalMinutes); // Full interval (e.g., 15 minutes)

            // Only consider truly overdue if elapsed time exceeds the FULL interval
            // This prevents fallback triggers during the warning period
            var isOverdue = elapsed > totalInterval;

            if (isOverdue)
            {
                _logger.LogWarning("☕ FALLBACK CHECK: Break is truly overdue - elapsed {ElapsedMinutes:F1}m > total {TotalMinutes:F1}m",
                    elapsed.TotalMinutes, totalInterval.TotalMinutes);
            }

            return isOverdue;
        }

        private void UpdateHeartbeat()
        {
            _lastHeartbeat = DateTime.Now;
        }
        
        /// <summary>
        /// CRITICAL: Recovery system for DispatcherTimer hang after system resume
        /// Recreates all timer instances to fix disconnected UI thread issues
        /// </summary>
        private void RecoverTimersFromHang()
        {
            try
            {
                _logger.LogCritical($"🔧 TIMER RECOVERY INITIATED at {DateTime.Now:HH:mm:ss.fff}");
                
                // Store current timer states before recovery
                var eyeRestEnabled = _eyeRestTimer?.IsEnabled ?? false;
                var breakEnabled = _breakTimer?.IsEnabled ?? false;
                var eyeRestInterval = _eyeRestTimer?.Interval ?? TimeSpan.Zero;
                var breakInterval = _breakTimer?.Interval ?? TimeSpan.Zero;
                
                _logger.LogCritical($"🔍 PRE-RECOVERY STATE: EyeRest={eyeRestEnabled}({eyeRestInterval.TotalMinutes:F1}m), Break={breakEnabled}({breakInterval.TotalMinutes:F1}m)");
                
                // STEP 1: Stop and dispose all existing timers
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    _eyeRestTimer.Tick -= OnEyeRestTimerTick;
                    _eyeRestTimer = null;
                    _logger.LogCritical("🔧 Eye rest timer disposed");
                }
                
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    _breakTimer.Tick -= OnBreakTimerTick;
                    _breakTimer = null;
                    _logger.LogCritical("🔧 Break timer disposed");
                }
                
                if (_eyeRestWarningTimer != null)
                {
                    _eyeRestWarningTimer.Stop();
                    _eyeRestWarningTimer = null;
                    _logger.LogCritical("🔧 Eye rest warning timer disposed");
                }
                
                if (_breakWarningTimer != null)
                {
                    _breakWarningTimer.Stop();
                    _breakWarningTimer = null;
                    _logger.LogCritical("🔧 Break warning timer disposed");
                }
                
                // STEP 2: Force garbage collection to clean up timer resources
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // STEP 2.5: CRITICAL FIX: Check for due timer events before clearing popups during recovery
                try
                {
                    // Check if timer events are currently due before clearing popups
                    var eyeRestTime = TimeUntilNextEyeRest;
                    var breakTime = TimeUntilNextBreak;
                    var hasTimerEventsDue = eyeRestTime <= TimeSpan.Zero || breakTime <= TimeSpan.Zero;
                    var hasActivePopups = _notificationService?.IsAnyPopupActive == true;

                    _logger.LogCritical($"🔍 HANG RECOVERY CHECK: EyeRest={eyeRestTime.TotalSeconds:F1}s, Break={breakTime.TotalSeconds:F1}s, AnyDue={hasTimerEventsDue}, ActivePopups={hasActivePopups}");

                    // P1 FIX: Add explicit check to prevent closing Done screen that's waiting for user confirmation
                    var isWaitingForConfirmationField = _notificationService?.GetType()?.GetField("_isWaitingForBreakConfirmation",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var isWaitingForConfirmation = isWaitingForConfirmationField?.GetValue(_notificationService) as bool? ?? false;

                    if (isWaitingForConfirmation)
                    {
                        _logger.LogCritical("🚨 P1 FIX: HANG RECOVERY BLOCKED - Done screen is waiting for user confirmation, cannot close popup!");
                        _logger.LogCritical("🚨 Skipping hang recovery popup clearing to prevent Done screen auto-close");

                        // Skip popup clearing - user is waiting to click Done button
                        // Timer recreation will still fix the hang issue without disrupting popups
                    }
                    else if (hasTimerEventsDue && hasActivePopups)
                    {
                        _logger.LogCritical("🚨 HANG RECOVERY: Timer events DUE with active popups - PRESERVING user interaction!");
                        _logger.LogCritical("🚨 Skipping popup clearing during hang recovery to prevent auto-close issue");

                        // Skip popup clearing to preserve user interaction with due timer events
                        // Timer recreation will still fix the hang issue without disrupting popups
                    }
                    else
                    {
                        _logger.LogCritical("🧹 HANG RECOVERY: No due events, active popups, or confirmation waiting - safe to clear popup references");
                        _notificationService?.HideAllNotifications();
                        
                        // Force clear all popup state in notification service
                        var notificationServiceType = _notificationService?.GetType();
                        var activeEyeRestField = notificationServiceType?.GetField("_activeEyeRestWarningPopup", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var activeBreakField = notificationServiceType?.GetField("_activeBreakWarningPopup", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        activeEyeRestField?.SetValue(_notificationService, null);
                        activeBreakField?.SetValue(_notificationService, null);
                        
                        _logger.LogCritical("🧹 HANG RECOVERY: All popup references cleared safely");
                    }
                }
                catch (Exception popupEx)
                {
                    _logger.LogError(popupEx, "🧹 HANG RECOVERY: Error in popup clearing logic during recovery");
                }
                
                // STEP 3: Recreate all timers with fresh DispatcherTimer instances
                _logger.LogCritical("🔧 Recreating all timers with fresh instances...");
                InitializeEyeRestTimer();
                InitializeEyeRestWarningTimer();
                InitializeBreakTimer();
                InitializeBreakWarningTimer();
                
                // STEP 4: Restart timers if they were previously enabled
                if (IsRunning && !IsPaused)
                {
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    _logger.LogCritical("🔧 Timers restarted after recovery");
                }
                
                // STEP 5: Reset heartbeat to mark recovery success
                UpdateHeartbeat();
                
                // STEP 6: Verify recovery success
                _logger.LogCritical($"✅ TIMER RECOVERY COMPLETED at {DateTime.Now:HH:mm:ss.fff}");
                _logger.LogCritical($"🔍 POST-RECOVERY STATE: EyeRest={_eyeRestTimer?.IsEnabled}({_eyeRestTimer?.Interval.TotalMinutes:F1}m), Break={_breakTimer?.IsEnabled}({_breakTimer?.Interval.TotalMinutes:F1}m)");
                _logger.LogCritical($"✅ Recovery successful - timers should now fire properly");
                
                // ENHANCED: Log expected next trigger times
                if (_eyeRestTimer != null && _eyeRestTimer.IsEnabled)
                {
                    var nextEyeRest = DateTime.Now.Add(_eyeRestTimer.Interval);
                    _logger.LogCritical($"👁️ Next eye rest timer event expected at: {nextEyeRest:HH:mm:ss}");
                }
                
                if (_breakTimer != null && _breakTimer.IsEnabled)
                {
                    var nextBreak = DateTime.Now.Add(_breakTimer.Interval);
                    _logger.LogCritical($"🚨 Next break timer event expected at: {nextBreak:HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔧 CRITICAL ERROR during timer recovery from hang");
            }
        }
        
        #endregion

        #region System Resume Recovery

        /// <summary>
        /// CRITICAL FIX: Recover timer functionality after system resume from standby/hibernate
        /// Addresses the issue where DispatcherTimer internal state becomes corrupted after system resume,
        /// causing timer events to stop firing while the timer appears to be running normally.
        /// </summary>
        public async Task RecoverFromSystemResumeAsync(string reason)
        {
            try
            {
                _logger.LogCritical($"🔄 SYSTEM RESUME RECOVERY: Attempting timer recovery - {reason}");

                // FIX #4: Recovery debouncing to prevent duplicate recovery attempts within 5 seconds
                var timeSinceLastRecovery = DateTime.Now - _lastRecoveryAttempt;
                if (timeSinceLastRecovery.TotalSeconds < RECOVERY_DEBOUNCE_SECONDS)
                {
                    _logger.LogInformation($"🔄 DEBOUNCE: Skipping duplicate recovery - last attempt {timeSinceLastRecovery.TotalSeconds:F1}s ago (threshold: {RECOVERY_DEBOUNCE_SECONDS}s)");
                    return;
                }
                _lastRecoveryAttempt = DateTime.Now;
                _logger.LogDebug($"🔄 Recovery debounce check passed - proceeding with recovery");

                // CRITICAL FIX: Prevent recovery during initial startup to avoid race condition
                if (!_hasCompletedInitialStartup)
                {
                    _logger.LogInformation($"🔄 Skipping recovery during initial startup - {reason}");
                    return;
                }

                // ADDITIONAL SAFETY: Add grace period after startup completion
                var timeSinceStartup = DateTime.Now - (_eyeRestStartTime != DateTime.MinValue ? _eyeRestStartTime : DateTime.Now);
                if (timeSinceStartup < TimeSpan.FromSeconds(30))
                {
                    _logger.LogInformation($"🛡️ STARTUP PROTECTION: Ignoring recovery during grace period (uptime={timeSinceStartup.TotalSeconds:F1}s) - {reason}");
                    return;
                }
                
                // Store current timer states before recovery
                var wasRunning = IsRunning;
                var wasPaused = IsPaused;
                var wasSmartPaused = IsSmartPaused;
                var wasManuallyPaused = IsManuallyPaused;
                var currentPauseReason = PauseReason;
                
                // CRITICAL FIX: Check if start times are initialized before calculating elapsed time
                // When app first starts, these are DateTime.MinValue which would result in massive elapsed times
                var eyeRestElapsed = (_eyeRestTimer?.IsEnabled == true && _eyeRestStartTime != DateTime.MinValue) ?
                    DateTime.Now - _eyeRestStartTime : TimeSpan.Zero;
                var breakElapsed = (_breakTimer?.IsEnabled == true && _breakStartTime != DateTime.MinValue) ?
                    DateTime.Now - _breakStartTime : TimeSpan.Zero;

                _logger.LogCritical($"🔄 Pre-recovery state: Running={wasRunning}, Paused={wasPaused}, SmartPaused={wasSmartPaused}, ManuallyPaused={wasManuallyPaused}");
                _logger.LogCritical($"🔄 Timer start times - EyeRest: {_eyeRestStartTime:HH:mm:ss}, Break: {_breakStartTime:HH:mm:ss}");
                _logger.LogCritical($"🔄 Timer elapsed times - EyeRest: {eyeRestElapsed.TotalSeconds:F1}s, Break: {breakElapsed.TotalSeconds:F1}s");
                
                // CRITICAL FIX: If timer start times are not initialized, treat as fresh session
                // This happens after extended standby when timers lose their state
                if (_eyeRestStartTime == DateTime.MinValue || _breakStartTime == DateTime.MinValue)
                {
                    _logger.LogCritical($"🔄 UNINITIALIZED TIMERS DETECTED: Timer start times lost during standby");
                    _logger.LogCritical($"🔄 Treating as fresh session to prevent immediate popup triggers");

                    // Reset timer states for a fresh start
                    _eyeRestStartTime = DateTime.Now;
                    _breakStartTime = DateTime.Now;
                    _breakTimerStartTime = DateTime.Now;

                    // Reset intervals to full configured values
                    var (eyeRestInterval, _, _, _, _) = CalculateEyeRestTimerInterval();
                    var (breakInterval, _, _, _, _) = CalculateBreakTimerInterval();
                    _eyeRestInterval = eyeRestInterval;
                    _breakInterval = breakInterval;

                    if (_eyeRestTimer != null)
                    {
                        _eyeRestTimer.Interval = _eyeRestInterval;
                        _eyeRestTimer.Start();
                    }

                    if (_breakTimer != null)
                    {
                        _breakTimer.Interval = _breakInterval;
                        _breakTimer.Start();
                    }

                    _logger.LogCritical($"✅ FRESH SESSION STARTED: Timers reset to full intervals");
                    _logger.LogCritical($"👁️ Next eye rest in: {_eyeRestInterval.TotalMinutes:F1} minutes");
                    _logger.LogCritical($"☕ Next break in: {_breakInterval.TotalMinutes:F1} minutes");

                    // Clear any lingering pause states
                    IsManuallyPaused = false;
                    IsPaused = false;
                    IsSmartPaused = false;
                    _pauseReason = string.Empty;
                    _manualPauseStartTime = DateTime.MinValue;
                    _pauseStartTime = DateTime.MinValue;

                    // CRITICAL FIX: Clear any stale event processing flags from previous session
                    // This prevents "GLOBAL LOCK PREVENTION" blocking popups after system resume
                    ClearEyeRestProcessingFlag();
                    ClearBreakProcessingFlag();
                    _logger.LogCritical($"🔄 RECOVERY: Cleared all event processing flags to prevent stale lock state");

                    // Ensure service is marked as running
                    if (!IsRunning)
                    {
                        IsRunning = true;
                    }

                    // Update heartbeat and notify UI
                    UpdateHeartbeatFromOperation("Fresh session after standby");
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(TimeUntilNextEyeRest));
                    OnPropertyChanged(nameof(TimeUntilNextBreak));

                    return; // Exit early - fresh session handles everything
                }

                // CRITICAL FIX: Check for extended away period (overnight standby)
                // If system was paused/away for extended period, treat as new working session
                var config = await _configurationService.LoadConfigurationAsync();
                var extendedAwayThresholdMinutes = config.UserPresence.ExtendedAwayThresholdMinutes;

                // Calculate time since pause/away started
                var timeSincePause = TimeSpan.Zero;
                if (wasManuallyPaused && _manualPauseStartTime != DateTime.MinValue)
                {
                    timeSincePause = DateTime.Now - _manualPauseStartTime;
                    _logger.LogCritical($"🔄 Time since manual pause: {timeSincePause.TotalMinutes:F1} minutes");
                }
                else if ((wasPaused || wasSmartPaused) && _pauseStartTime != DateTime.MinValue)
                {
                    timeSincePause = DateTime.Now - _pauseStartTime;
                    _logger.LogCritical($"🔄 Time since pause: {timeSincePause.TotalMinutes:F1} minutes");
                }

                // FIX #2: Query UserPresenceService for actual away/idle time (PRIMARY CHECK)
                TimeSpan userPresenceAwayTime = TimeSpan.Zero;
                if (_userPresenceService != null)
                {
                    userPresenceAwayTime = _userPresenceService.GetLastAwayDuration();
                    if (userPresenceAwayTime > TimeSpan.Zero)
                    {
                        _logger.LogCritical($"🔍 UserPresenceService reports: User was away for {userPresenceAwayTime.TotalMinutes:F1} minutes");
                        // Use UserPresenceService data if it's more accurate than pause tracking
                        if (userPresenceAwayTime > timeSincePause)
                        {
                            timeSincePause = userPresenceAwayTime;
                            _logger.LogCritical($"🔍 Using UserPresenceService away time as primary indicator");
                        }
                    }
                }

                // FIX #5: Check stale heartbeat as extended away indicator (SECONDARY CHECK)
                var heartbeatStaleness = DateTime.Now - _lastHeartbeat;
                if (heartbeatStaleness.TotalMinutes >= extendedAwayThresholdMinutes)
                {
                    _logger.LogCritical($"🔍 STALE HEARTBEAT DETECTED: {heartbeatStaleness.TotalMinutes:F1} minutes (threshold: {extendedAwayThresholdMinutes} min)");
                    // Use heartbeat staleness if it's greater than other indicators
                    if (heartbeatStaleness > timeSincePause)
                    {
                        timeSincePause = heartbeatStaleness;
                        _logger.LogCritical($"🔍 Using stale heartbeat duration as extended away indicator");
                    }
                }

                // FIX #3: Also check timer elapsed times for overnight standby detection (FALLBACK CHECK)
                // FIXED: Lower threshold from 2x to 1x (was 60min, now 30min)
                // If timers have been running for extended period (e.g., overnight), treat as fresh session
                // This handles the case where user closes laptop mid-session without explicit pause
                var maxTimerElapsed = Math.Max(eyeRestElapsed.TotalMinutes, breakElapsed.TotalMinutes);
                var shouldResetDueToExtendedElapsed = maxTimerElapsed >= extendedAwayThresholdMinutes; // FIXED: Changed from 2x to 1x

                if (shouldResetDueToExtendedElapsed)
                {
                    _logger.LogCritical($"🌙 OVERNIGHT STANDBY DETECTED: Timer elapsed {maxTimerElapsed:F1} minutes (threshold: {extendedAwayThresholdMinutes} min)");
                    _logger.LogCritical($"🌙 System was NOT paused before standby, but timer elapsed time indicates overnight gap");
                    // Use elapsed time if it's greater than other indicators
                    if (maxTimerElapsed > timeSincePause.TotalMinutes)
                    {
                        timeSincePause = TimeSpan.FromMinutes(maxTimerElapsed);
                        _logger.LogCritical($"🌙 Using timer elapsed time as extended away indicator");
                    }
                }

                // Log all detection methods for debugging
                _logger.LogCritical($"📊 EXTENDED AWAY DETECTION SUMMARY:");
                _logger.LogCritical($"  • Explicit pause time: {(wasManuallyPaused || wasPaused || wasSmartPaused ? timeSincePause.TotalMinutes : 0):F1} min");
                _logger.LogCritical($"  • UserPresence away time: {userPresenceAwayTime.TotalMinutes:F1} min");
                _logger.LogCritical($"  • Heartbeat staleness: {heartbeatStaleness.TotalMinutes:F1} min");
                _logger.LogCritical($"  • Timer elapsed (max): {maxTimerElapsed:F1} min");
                _logger.LogCritical($"  • Final detection time: {timeSincePause.TotalMinutes:F1} min");
                _logger.LogCritical($"  • Threshold: {extendedAwayThresholdMinutes} min");

                // ENHANCED: Check for extended away period (overnight standby)
                // Consider any pause/away period >= 30 minutes as extended away requiring fresh session
                if (timeSincePause.TotalMinutes >= extendedAwayThresholdMinutes && config.UserPresence.EnableSmartSessionReset)
                {
                    _logger.LogCritical($"🌅 EXTENDED AWAY DETECTED: {timeSincePause.TotalMinutes:F1} minutes (threshold: {extendedAwayThresholdMinutes} min)");
                    _logger.LogCritical($"🌅 Treating as NEW WORKING SESSION after overnight/extended standby");

                    // P0 REQUIREMENT: ALWAYS reset to fresh session when extended away detected
                    // Even if timer events were due before sleep, clear them and start fresh
                    var eyeRestTime = TimeUntilNextEyeRest;
                    var breakTime = TimeUntilNextBreak;
                    var hasTimerEventsDue = eyeRestTime <= TimeSpan.Zero || breakTime <= TimeSpan.Zero;

                    _logger.LogCritical($"🔍 DUE EVENTS CHECK: EyeRest={eyeRestTime.TotalSeconds:F1}s, Break={breakTime.TotalSeconds:F1}s, AnyDue={hasTimerEventsDue}");

                    if (hasTimerEventsDue)
                    {
                        _logger.LogCritical($"🚨 P0 FIX: Timer events are DUE but extended away detected - CLEARING due events for fresh session!");
                        _logger.LogCritical($"🚨 User was away {timeSincePause.TotalMinutes:F1}min - resetting timers regardless of due state");
                    }
                    else
                    {
                        _logger.LogCritical($"✅ No timer events due - proceeding with clean session reset");
                    }
                    
                    // CRITICAL FIX: Clear all pause states INCLUDING manual pause timer cleanup
                    _logger.LogCritical($"🔧 EXTENDED AWAY FIX: Clearing all pause states and cleaning up manual pause resources");
                    IsManuallyPaused = false;
                    IsPaused = false;
                    IsSmartPaused = false;
                    _pauseReason = string.Empty;
                    _manualPauseStartTime = DateTime.MinValue;
                    _pauseStartTime = DateTime.MinValue;

                    // CRITICAL FIX: Stop and dispose manual pause timer during extended away reset
                    if (_manualPauseTimer != null)
                    {
                        _manualPauseTimer.Stop();
                        _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                        _manualPauseTimer = null;
                        _logger.LogCritical($"🔧 EXTENDED AWAY FIX: Manual pause timer disposed during session reset");
                    }

                    // CRITICAL FIX: Clear any stale event processing flags from extended idle
                    // This prevents "GLOBAL LOCK PREVENTION" blocking popups after returning from idle
                    ClearEyeRestProcessingFlag();
                    ClearBreakProcessingFlag();
                    _logger.LogCritical($"🔄 EXTENDED AWAY: Cleared all event processing flags to prevent stale lock state");

                    // Perform smart session reset for fresh start
                    await SmartSessionResetAsync($"Extended away ({timeSincePause.TotalMinutes:F0}min) - new working session after standby");
                    
                    _logger.LogCritical($"✅ NEW SESSION STARTED: Fresh timers after extended standby with complete manual pause cleanup");
                    return; // Exit early - session reset handles everything
                }
                
                // Test if timers are actually working by checking if Tick events fire
                var timerTestResult = await TestTimerFunctionality();
                if (timerTestResult.IsWorking)
                {
                    _logger.LogInformation($"🔄 Timer functionality test PASSED - timers are working correctly, no recovery needed");
                    return;
                }
                
                _logger.LogWarning($"🔄 Timer functionality test FAILED - {timerTestResult.Issue}, proceeding with recovery");
                
                // Stop and dispose potentially corrupted timers
                await StopAndDisposeAllTimers();
                
                // Recreate fresh timer instances
                RecreateTimerInstances();
                
                // Restore timer states with elapsed time compensation
                if (wasRunning)
                {
                    if (wasManuallyPaused)
                    {
                        // Calculate remaining manual pause time
                        var manualPauseElapsed = DateTime.Now - _manualPauseStartTime;
                        var remainingPauseDuration = _manualPauseDuration - manualPauseElapsed;
                        
                        if (remainingPauseDuration > TimeSpan.Zero)
                        {
                            _logger.LogCritical($"🔄 Restoring manual pause with {remainingPauseDuration.TotalMinutes:F1} minutes remaining");
                            await PauseForDurationAsync(remainingPauseDuration, currentPauseReason ?? "System resume recovery");
                        }
                        else
                        {
                            _logger.LogCritical($"🔄 Manual pause expired during system sleep, resuming timers");
                            await StartAsync();
                        }
                    }
                    else if (wasPaused || wasSmartPaused)
                    {
                        _logger.LogCritical($"🔄 Restoring paused state - reason: {currentPauseReason}");
                        await StartAsync();
                        if (wasSmartPaused)
                        {
                            await SmartPauseAsync(currentPauseReason ?? "System resume recovery");
                        }
                        else
                        {
                            await PauseAsync();
                        }
                    }
                    else
                    {
                        // Normal running state - restart with elapsed time compensation
                        _logger.LogCritical($"🔄 Restoring running state with time compensation");
                        await StartAsync();
                        
                        // Compensate for elapsed time during system sleep
                        CompensateElapsedTime(eyeRestElapsed, breakElapsed);
                    }
                }
                else
                {
                    _logger.LogCritical($"🔄 Timers were stopped before system sleep, keeping stopped");
                }
                
                _logger.LogCritical($"🔄 SYSTEM RESUME RECOVERY COMPLETED SUCCESSFULLY");
                
                // FINAL SAFETY CHECK: Ensure due timer events are triggered after recovery
                await Task.Delay(100); // Brief delay to let recovery settle
                var finalEyeRestTime = TimeUntilNextEyeRest;
                var finalBreakTime = TimeUntilNextBreak;
                var hasFinalDueEvents = finalEyeRestTime <= TimeSpan.Zero || finalBreakTime <= TimeSpan.Zero;
                var hasFinalActivePopups = _notificationService?.IsAnyPopupActive == true;
                
                _logger.LogCritical($"🔍 FINAL RECOVERY CHECK: EyeRest={finalEyeRestTime.TotalSeconds:F1}s, Break={finalBreakTime.TotalSeconds:F1}s, AnyDue={hasFinalDueEvents}, ActivePopups={hasFinalActivePopups}");
                
                // CRITICAL FIX: Remove IsRunning requirement - allow final safety triggers even if service stopped
                if (hasFinalDueEvents && !hasFinalActivePopups)
                {
                    _logger.LogCritical($"🔥 FINAL SAFETY TRIGGER: Due events still pending after recovery - ensuring service runs before triggers");
                    
                    // CRITICAL FIX: Ensure service is running before triggering final safety events
                    if (!IsRunning)
                    {
                        _logger.LogCritical($"🔧 FINAL SAFETY: Service stopped with due events - attempting restart");
                        try
                        {
                            await StartAsync();
                            _logger.LogCritical($"🔧 Final safety service restart successful: IsRunning={IsRunning}");
                            
                            // CRITICAL FIX: Ensure UI synchronization after final safety restart
                            OnPropertyChanged(nameof(IsRunning));
                            OnPropertyChanged(nameof(IsPaused));
                            OnPropertyChanged(nameof(IsManuallyPaused));
                            OnPropertyChanged(nameof(IsSmartPaused));
                            _logger.LogCritical($"🔧 Final safety UI sync: All state properties notified after service restart");
                        }
                        catch (Exception startEx)
                        {
                            _logger.LogError(startEx, "🔧 Failed to restart service for final safety triggers");
                            return; // Can't trigger events if service won't start
                        }
                    }
                    
                    // CRITICAL FIX: Add race condition protection for final safety triggers
                    if (finalEyeRestTime <= TimeSpan.Zero && !_isEyeRestNotificationActive)
                    {
                        // TIMELINE FIX: Check if enough time has passed since last trigger
                        if (!ShouldAllowEyeRestFallback())
                        {
                            _logger.LogInformation("🕒 FINAL RECOVERY BLOCKED: Eye rest final recovery blocked - insufficient time since last trigger");
                        }
                        // BREAK PRIORITY FIX: Check break priority before final recovery trigger
                        else if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                            _isBreakEventProcessing || _isAnyBreakEventProcessing)
                        {
                            _logger.LogInformation("🔄 FINAL RECOVERY BLOCKED: Eye rest final recovery blocked - break event has priority. Pausing eye rest timer.");
                            SmartPauseEyeRestTimerForBreak();
                        }
                        else
                        {
                            _logger.LogCritical($"🔥 Final trigger for overdue eye rest event (verified not active)");
                            TriggerEyeRest();
                        }
                    }
                    if (finalBreakTime <= TimeSpan.Zero && !_isBreakNotificationActive)
                    {
                        _logger.LogCritical($"🔥 Final trigger for overdue break event (verified not active)");
                        TriggerBreak();
                    }
                }
                
                // Notify orchestrator of recovery completion through property change
                OnPropertyChanged(nameof(IsRunning));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"🔄 CRITICAL ERROR during system resume recovery");
                
                // Emergency fallback: try to restart normally
                try
                {
                    _logger.LogCritical($"🔄 EMERGENCY FALLBACK: Attempting normal restart");
                    await StopAsync();
                    await StartAsync();
                    _logger.LogCritical($"🔄 EMERGENCY FALLBACK: Restart completed");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, $"🔄 EMERGENCY FALLBACK FAILED - manual intervention required");
                }
            }
        }

        /// <summary>
        /// EMERGENCY: Force immediate timer recovery and trigger overdue events
        /// Call this when timer events are not firing despite timers being overdue
        /// </summary>
        public async Task<bool> ForceTimerRecoveryAsync(string reason = "Timer events not firing")
        {
            try
            {
                _logger.LogCritical($"🚨 EMERGENCY TIMER RECOVERY INITIATED: {reason}");
                
                // Test if timer infrastructure is fundamentally broken
                var (isWorking, issue) = await TestTimerFunctionality();
                if (!isWorking)
                {
                    _logger.LogCritical($"🚨 TIMER INFRASTRUCTURE BROKEN: {issue} - recreating all timers");
                    
                    // Force complete timer recreation
                    RecreateTimerInstances();
                    
                    // Test again after recreation
                    var (isWorkingAfter, issueAfter) = await TestTimerFunctionality();
                    if (!isWorkingAfter)
                    {
                        _logger.LogCritical($"🚨 TIMER RECOVERY FAILED: {issueAfter}");
                        _logger.LogCritical($"🆘 DispatcherTimer system completely broken - activating emergency fallback");
                        ActivateEmergencyFallbackTimer();
                        return true; // Return true because fallback is now active
                    }
                }
                
                // Check for overdue events and trigger them immediately
                await TriggerOverdueEventsAsync();
                
                _logger.LogCritical($"✅ EMERGENCY TIMER RECOVERY COMPLETED SUCCESSFULLY");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"🚨 EMERGENCY TIMER RECOVERY FAILED");
                return false;
            }
        }

        /// <summary>
        /// Check for overdue timer events and trigger them immediately
        /// </summary>
        private async Task TriggerOverdueEventsAsync()
        {
            if (!IsRunning) return;

            var now = DateTime.Now;

            // CRITICAL FIX: Don't trigger "overdue" events if popups are already active
            // When popups are showing, TimeUntilNext returns 0 or negative by design

            // Check eye rest timer
            var eyeRestRemaining = TimeUntilNextEyeRest;
            if (eyeRestRemaining <= TimeSpan.Zero && !_isEyeRestNotificationActive)
            {
                _logger.LogCritical($"🚨 TRIGGERING OVERDUE EYE REST (overdue by {Math.Abs(eyeRestRemaining.TotalSeconds):F1}s)");
                // Manually call the timer event handler
                OnEyeRestTimerTick(this, EventArgs.Empty);
            }

            // Check break timer
            var breakRemaining = TimeUntilNextBreak;
            if (breakRemaining <= TimeSpan.Zero && !_isBreakNotificationActive)
            {
                _logger.LogCritical($"🚨 TRIGGERING OVERDUE BREAK (overdue by {Math.Abs(breakRemaining.TotalSeconds):F1}s)");
                // Manually call the timer event handler
                OnBreakTimerTick(this, EventArgs.Empty);
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Emergency fallback timer using System.Threading.Timer when DispatcherTimer fails
        /// </summary>
        private System.Threading.Timer? _emergencyFallbackTimer;
        
        /// <summary>
        /// Counter for emergency fallback timer ticks to track recovery attempts
        /// </summary>
        private int _emergencyFallbackTickCount = 0;
        
        /// <summary>
        /// Activate emergency fallback timer system when DispatcherTimer completely fails
        /// Uses System.Threading.Timer + Dispatcher.BeginInvoke to bypass broken DispatcherTimer
        /// </summary>
        private void ActivateEmergencyFallbackTimer()
        {
            try
            {
                _logger.LogCritical("🆘 ACTIVATING EMERGENCY FALLBACK TIMER - DispatcherTimer system completely broken");
                
                // Stop any existing fallback timer
                _emergencyFallbackTimer?.Dispose();
                
                // Create System.Threading.Timer that fires every 10 seconds
                _emergencyFallbackTimer = new System.Threading.Timer(OnEmergencyFallbackTick, null, 
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
                    
                _logger.LogCritical("🆘 Emergency fallback timer activated - will check for overdue events every 10 seconds");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🆘 Failed to activate emergency fallback timer");
            }
        }
        
        /// <summary>
        /// Emergency fallback timer callback - runs on background thread
        /// </summary>
        private void OnEmergencyFallbackTick(object? state)
        {
            try
            {
                // Use Dispatcher.BeginInvoke to safely access UI thread objects
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _logger.LogCritical("🆘 EMERGENCY FALLBACK: Checking for overdue timer events");
                        
                        if (!IsRunning) return;
                        
                        var eyeRestRemaining = TimeUntilNextEyeRest;
                        var breakRemaining = TimeUntilNextBreak;
                        
                        // Trigger overdue eye rest events
                        if (eyeRestRemaining <= TimeSpan.Zero && !_isEyeRestNotificationActive && _eyeRestWarningTimer?.IsEnabled != true)
                        {
                            _logger.LogCritical($"🆘 FALLBACK: Triggering overdue eye rest (overdue by {Math.Abs(eyeRestRemaining.TotalSeconds):F1}s)");
                            OnEyeRestTimerTick(this, EventArgs.Empty);
                        }
                        
                        // Trigger overdue break events  
                        if (breakRemaining <= TimeSpan.Zero && !_isBreakNotificationActive && _breakWarningTimer?.IsEnabled != true)
                        {
                            _logger.LogCritical($"🆘 FALLBACK: Triggering overdue break (overdue by {Math.Abs(breakRemaining.TotalSeconds):F1}s)");
                            OnBreakTimerTick(this, EventArgs.Empty);
                        }
                        
                        // Update heartbeat to show fallback system is working
                        UpdateHeartbeat();
                        _logger.LogCritical("🆘 FALLBACK: Heartbeat updated, system functioning via emergency timer");
                        
                        // RECOVERY ATTEMPT: Try to restore DispatcherTimer functionality every 5 minutes (30 ticks)
                        _emergencyFallbackTickCount++;
                        if (_emergencyFallbackTickCount >= 30) // 30 ticks * 10 seconds = 5 minutes
                        {
                            _emergencyFallbackTickCount = 0;
                            _logger.LogCritical("🔧 FALLBACK RECOVERY: Attempting to restore DispatcherTimer functionality after 5 minutes");
                            
                            // Try to restore normal timer operation in background
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var (isWorking, issue) = await TestTimerFunctionality();
                                    if (isWorking)
                                    {
                                        _logger.LogCritical("✅ FALLBACK RECOVERY SUCCESS: DispatcherTimer system restored - deactivating emergency fallback");
                                        
                                        // Dispose emergency fallback timer
                                        _emergencyFallbackTimer?.Dispose();
                                        _emergencyFallbackTimer = null;
                                        
                                        // Recreate and start normal timers
                                        InitializeEyeRestTimer();
                                        InitializeBreakTimer();
                                        _logger.LogCritical("✅ FALLBACK RECOVERY COMPLETE: Normal timer operation restored");
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"🔧 FALLBACK RECOVERY FAILED: {issue} - will retry in 5 minutes");
                                    }
                                }
                                catch (Exception recoveryEx)
                                {
                                    _logger.LogError(recoveryEx, "🔧 FALLBACK RECOVERY ERROR: Exception during recovery attempt");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "🆘 Error in emergency fallback timer callback");
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🆘 Error invoking emergency fallback on UI thread");
            }
        }

        private async Task<(bool IsWorking, string Issue)> TestTimerFunctionality()
        {
            try
            {
                var testTimer = _timerFactory.CreateTimer();
                testTimer.Interval = TimeSpan.FromMilliseconds(100);
                
                var timerFired = false;
                testTimer.Tick += (s, e) =>
                {
                    timerFired = true;
                    testTimer.Stop();
                };
                
                testTimer.Start();
                await Task.Delay(500); // Wait for timer to fire
                testTimer.Stop();
                
                if (!timerFired)
                {
                    return (false, "Test timer did not fire within 500ms");
                }
                
                // Test if main timers have correct intervals
                if (_eyeRestTimer?.Interval.TotalMinutes < 1)
                {
                    return (false, $"Eye rest timer interval too short: {_eyeRestTimer.Interval.TotalSeconds}s");
                }
                
                if (_breakTimer?.Interval.TotalMinutes < 1)
                {
                    return (false, $"Break timer interval too short: {_breakTimer.Interval.TotalSeconds}s");
                }
                
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Test failed with exception: {ex.Message}");
            }
        }

        private async Task StopAndDisposeAllTimers()
        {
            _logger.LogInformation($"🔄 Stopping and disposing all timers");
            
            // Stop all timers
            _eyeRestTimer?.Stop();
            _breakTimer?.Stop();
            _eyeRestWarningTimer?.Stop();
            _breakWarningTimer?.Stop();
            _eyeRestFallbackTimer?.Stop();
            _breakFallbackTimer?.Stop();
            
            // Stop emergency fallback timer
            _emergencyFallbackTimer?.Dispose();
            _emergencyFallbackTimer = null;
            
            // Remove event handlers
            if (_eyeRestTimer != null)
            {
                _eyeRestTimer.Tick -= OnEyeRestTimerTick;
            }
            if (_breakTimer != null)
            {
                _breakTimer.Tick -= OnBreakTimerTick;
            }
            
            // Null out references
            _eyeRestTimer = null;
            _breakTimer = null;
            _eyeRestWarningTimer = null;
            _breakWarningTimer = null;
            _eyeRestFallbackTimer = null;
            _breakFallbackTimer = null;
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            await Task.Delay(100); // Brief delay to ensure cleanup
        }

        private void RecreateTimerInstances()
        {
            _logger.LogInformation($"🔄 Recreating fresh timer instances");
            
            // Recreate all timers
            InitializeEyeRestTimer();
            InitializeBreakTimer();
            InitializeEyeRestWarningTimer();
            InitializeBreakWarningTimer();
            InitializeEyeRestFallbackTimer();
            InitializeBreakFallbackTimer();
        }

        private void CompensateElapsedTime(TimeSpan eyeRestElapsed, TimeSpan breakElapsed)
        {
            try
            {
                // CRITICAL FIX: Additional safety check - don't compensate if start times are not initialized
                if (_eyeRestStartTime == DateTime.MinValue && _breakStartTime == DateTime.MinValue)
                {
                    _logger.LogWarning("🔄 Skipping elapsed time compensation - start times not initialized (likely initial startup)");
                    return;
                }
                
                // Adjust timer intervals to compensate for time passed during sleep
                if (_eyeRestTimer != null && eyeRestElapsed > TimeSpan.Zero && _eyeRestStartTime != DateTime.MinValue)
                {
                    var remainingTime = _eyeRestInterval - eyeRestElapsed;
                    if (remainingTime > TimeSpan.Zero)
                    {
                        _eyeRestTimer.Interval = remainingTime;
                        _logger.LogInformation($"🔄 Eye rest timer compensated - {remainingTime.TotalMinutes:F1} minutes remaining");
                    }
                    else
                    {
                        // Timer should have fired during sleep - trigger immediately
                        // TIMELINE FIX: Check if enough time has passed since last trigger
                        if (!ShouldAllowEyeRestFallback())
                        {
                            _logger.LogInformation("🕒 SLEEP RECOVERY BLOCKED: Eye rest sleep recovery blocked - insufficient time since last trigger");
                        }
                        // BREAK PRIORITY FIX: Check break priority before sleep recovery trigger
                        else if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                            _isBreakEventProcessing || _isAnyBreakEventProcessing)
                        {
                            _logger.LogInformation("🔄 SLEEP RECOVERY BLOCKED: Eye rest sleep recovery blocked - break event has priority. Pausing eye rest timer.");
                            SmartPauseEyeRestTimerForBreak();
                        }
                        else
                        {
                            _logger.LogInformation($"🔄 Eye rest was due during sleep - triggering now");
                            TriggerEyeRest();
                        }
                    }
                }
                
                if (_breakTimer != null && breakElapsed > TimeSpan.Zero && _breakStartTime != DateTime.MinValue)
                {
                    var remainingTime = _breakInterval - breakElapsed;
                    if (remainingTime > TimeSpan.Zero)
                    {
                        _breakTimer.Interval = remainingTime;
                        _logger.LogInformation($"🔄 Break timer compensated - {remainingTime.TotalMinutes:F1} minutes remaining");
                    }
                    else
                    {
                        // Timer should have fired during sleep - trigger immediately
                        _logger.LogInformation($"🔄 Break was due during sleep - triggering now");
                        TriggerBreak();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compensating elapsed time");
            }
        }

        #endregion
    }
}