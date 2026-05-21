using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing lifecycle management operations (Start, Stop, Reset)
    /// </summary>
    public partial class TimerService
    {
        public async Task StartAsync()
        {
            if (IsRunning)
            {
                _logger.LogWarning("Timer service is already running");
                return;
            }

            try
            {
                _logger.LogInformation("🚀 Starting timer service...");
                
                // Load configuration
                _configuration = await _configurationService.LoadConfigurationAsync();
                _logger.LogInformation("Configuration loaded - Eye rest: {EyeRestInterval} min/{EyeRestDuration} sec, Break: {BreakInterval} min/{BreakDuration} min",
                    _configuration.EyeRest.IntervalMinutes,
                    _configuration.EyeRest.DurationSeconds,
                    _configuration.Break.IntervalMinutes,
                    _configuration.Break.DurationMinutes);
                
                // Initialize timers if not already done
                InitializeEyeRestTimer();
                InitializeBreakTimer();
                InitializeEyeRestWarningTimer();
                InitializeBreakWarningTimer();
                
                // Initialize health monitoring
                InitializeHealthMonitor();
                
                // CRITICAL FIX: Use the intervals that were already calculated during initialization
                // The timers already have the correct intervals set (reduced for warnings if enabled)
                // Store the timer intervals for reference, but don't override them
                _eyeRestInterval = _eyeRestTimer!.Interval;
                _breakInterval = _breakTimer!.Interval;

                // Log the actual intervals being used
                _logger.LogInformation("📊 Timer intervals after initialization - Eye rest: {EyeRestInterval:F1}m, Break: {BreakInterval:F1}m",
                    _eyeRestInterval.TotalMinutes, _breakInterval.TotalMinutes);
                
                // CRITICAL FIX: Set start times BEFORE starting timers to prevent immediate triggers
                _eyeRestStartTime = _clock.Now;
                _breakStartTime = _clock.Now;
                _breakTimerStartTime = _clock.Now;
                
                // CRITICAL FIX: Clear any lingering pause states during startup for proper coordination
                // This ensures UI and internal state are consistent when restarting during recovery
                var hadPauseStates = IsManuallyPaused || IsPaused || IsSmartPaused;
                if (hadPauseStates)
                {
                    _logger.LogWarning($"🔧 STARTUP COORDINATION: Clearing lingering pause states - Manual={IsManuallyPaused}, Paused={IsPaused}, Smart={IsSmartPaused}");
                    IsManuallyPaused = false;
                    IsPaused = false;
                    IsSmartPaused = false;
                    _pauseReason = string.Empty;
                    _manualPauseStartTime = DateTime.MinValue;
                    _pauseStartTime = DateTime.MinValue;
                    _manualPauseDuration = TimeSpan.Zero;
                    _logger.LogWarning($"🔧 STARTUP COORDINATION: All pause states cleared - service will show as Running");
                }
                
                // Start timers (start times already set above)
                _eyeRestTimer.Start();
                _breakTimer.Start();
                
                // Start health monitor
                _healthMonitorTimer?.Start();
                UpdateHeartbeatFromOperation("StartAsync");
                
                IsRunning = true;
                
                // Mark initial startup as complete (set after timers are running)
                _hasCompletedInitialStartup = true;
                
                // CONSOLIDATION FIX: Removed redundant fallback timers (_eyeRestFallbackTimer, _breakFallbackTimer)
                // These were causing duplicate event triggers because they fired 5 seconds after main timers.
                // Protection is now provided by:
                // 1. Warning fallback timers (fire 2s after warning period if warning timer fails)
                // 2. Health monitor (emergency backup for truly stuck timers)
                // This eliminates the "7+ timer systems" race condition.
                
                _logger.LogInformation("✅ Timer service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting timer service");
                IsRunning = false;
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Timer service is not running");
                return;
            }

            try
            {
                _logger.LogInformation("⏹️ Stopping timer service...");
                
                // CRITICAL FIX: Dispose all timers completely to prevent ghost timer instances
                // This ensures fallback timers and their event handlers are fully cleaned up
                DisposeAllTimers();
                
                // Reset states
                IsRunning = false;
                IsPaused = false;
                IsSmartPaused = false;
                IsManuallyPaused = false;
                _pauseReason = string.Empty;
                IsBreakDelayed = false;
                
                // Reset notification states
                _isEyeRestNotificationActive = false;
                _isBreakNotificationActive = false;
                _eyeRestTimerPausedForBreak = false;
                _breakTimerPausedForEyeRest = false;
                
                // Clear remaining times
                _eyeRestRemainingTime = TimeSpan.Zero;
                _breakRemainingTime = TimeSpan.Zero;
                
                await _analyticsService.RecordSessionEndAsync();
                
                _logger.LogInformation("Timer service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping timer service");
                throw;
            }
        }

        public async Task ResetEyeRestTimer()
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("🔄 Resetting eye rest timer");

                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();

                    // Clear notification state
                    _isEyeRestNotificationActive = false;

                    // CRITICAL FIX: Use shared calculation method for consistency with initialization
                    // This ensures warning time is properly subtracted so the timer fires at the correct time
                    var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();
                    _eyeRestInterval = interval;
                    _eyeRestTimer.Interval = _eyeRestInterval;

                    // Clear any remaining time
                    _eyeRestRemainingTime = TimeSpan.Zero;

                    // Start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                    {
                        _eyeRestTimer.Start();
                        _eyeRestStartTime = _clock.Now;
                    }
                    else
                    {
                        // Store the calculated interval as remaining time for when we resume
                        _eyeRestRemainingTime = _eyeRestInterval;
                    }

                    if (isReduced)
                    {
                        _logger.LogInformation("Eye rest timer reset - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                            _eyeRestInterval.TotalMinutes, warningSeconds, totalMinutes);
                    }
                    else
                    {
                        _logger.LogInformation("Eye rest timer reset - FULL interval: {IntervalMinutes:F1}m (warnings disabled)",
                            _eyeRestInterval.TotalMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting eye rest timer");
                throw;
            }
        }

        public async Task ResetBreakTimer()
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("🔄 Resetting break timer");

                if (_breakTimer != null)
                {
                    _breakTimer.Stop();

                    // Clear notification and delay states
                    _isBreakNotificationActive = false;
                    IsBreakDelayed = false;

                    // CRITICAL FIX: Use shared calculation method for consistency with initialization
                    // This ensures warning time is properly subtracted so the timer fires at the correct time
                    var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();
                    _breakInterval = interval;
                    _breakTimer.Interval = _breakInterval;

                    // Clear any remaining time
                    _breakRemainingTime = TimeSpan.Zero;

                    // Start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                    {
                        _breakTimer.Start();
                        _breakStartTime = _clock.Now;
                        _breakTimerStartTime = _clock.Now;
                    }
                    else
                    {
                        // Store the calculated interval as remaining time for when we resume
                        _breakRemainingTime = _breakInterval;
                    }

                    if (isReduced)
                    {
                        _logger.LogInformation("Break timer reset - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                            _breakInterval.TotalMinutes, warningSeconds, totalMinutes);
                    }
                    else
                    {
                        _logger.LogInformation("Break timer reset - FULL interval: {IntervalMinutes:F1}m (warnings disabled)",
                            _breakInterval.TotalMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting break timer");
                throw;
            }
        }

        public async Task RestartEyeRestTimerAfterCompletion()
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("♻️ Restarting eye rest timer after completion");
                
                // Clear notification state
                _isEyeRestNotificationActive = false;
                
                if (_eyeRestTimer != null)
                {
                    // Use shared calculation method to ensure consistency with initialization
                    var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();

                    _eyeRestInterval = interval;
                    _eyeRestTimer.Interval = _eyeRestInterval;
                    
                    // Only start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused && !_eyeRestTimerPausedForBreak)
                    {
                        _eyeRestTimer.Start();
                        _eyeRestStartTime = _clock.Now;

                        if (isReduced)
                        {
                            _logger.LogInformation("Eye rest timer restarted - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                                _eyeRestInterval.TotalMinutes, warningSeconds, totalMinutes);
                        }
                        else
                        {
                            _logger.LogInformation("Eye rest timer restarted - FULL interval: {IntervalMinutes:F1}m (no warning)",
                                _eyeRestInterval.TotalMinutes);
                        }
                    }
                    else
                    {
                        _eyeRestRemainingTime = _eyeRestInterval;
                        _logger.LogInformation("Eye rest timer ready but paused - will start when resumed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting eye rest timer after completion");
            }
        }

        public async Task RestartBreakTimerAfterCompletion()
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("♻️ Restarting break timer after completion");
                _consecutiveBreakDelayCount = 0;

                // Clear notification state
                _isBreakNotificationActive = false;
                
                if (_breakTimer != null)
                {
                    // Use shared calculation method to ensure consistency with initialization
                    var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();

                    _breakInterval = interval;
                    _breakTimer.Interval = _breakInterval;

                    // Only start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused && !_breakTimerPausedForEyeRest)
                    {
                        _breakTimer.Start();
                        _breakStartTime = _clock.Now;
                        _breakTimerStartTime = _clock.Now;

                        if (isReduced)
                        {
                            _logger.LogInformation("Break timer restarted - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                                _breakInterval.TotalMinutes, warningSeconds, totalMinutes);
                        }
                        else
                        {
                            _logger.LogInformation("Break timer restarted - FULL interval: {IntervalMinutes:F1}m (no warning)",
                                _breakInterval.TotalMinutes);
                        }
                    }
                    else
                    {
                        _breakRemainingTime = _breakInterval;
                        _logger.LogInformation("Break timer ready but paused - will start when resumed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting break timer after completion");
            }
        }

        public async Task DelayBreak(TimeSpan delay)
        {
            try
            {
                _consecutiveBreakDelayCount++;
                var maxDelays = _configuration?.Break?.MaxBreakDelayCount ?? 3;

                if (maxDelays > 0 && _consecutiveBreakDelayCount > maxDelays)
                {
                    _logger.LogWarning("🚫 BREAK DELAY LIMIT REACHED: {Count}/{Max} consecutive delays — forcing session reset instead of further delay",
                        _consecutiveBreakDelayCount, maxDelays);
                    _consecutiveBreakDelayCount = 0;
                    await SmartSessionResetAsync($"Break delay limit reached ({maxDelays} consecutive delays) — forcing fresh session");
                    return;
                }

                _logger.LogInformation("⏳ Delaying break for {Minutes} minutes (delay {Count}/{Max})",
                    delay.TotalMinutes, _consecutiveBreakDelayCount, maxDelays);

                IsBreakDelayed = true;
                _delayStartTime = _clock.Now;
                _delayDuration = delay;

                // Stop the break timer during delay
                _breakTimer?.Stop();

                // CRITICAL FIX: UNCONDITIONALLY stop ALL eye rest timers during break delay
                // We must stop them regardless of IsEnabled state because they might be in any state
                // The break priority system may have already stopped some but not all timers

                // Stop main eye rest timer and store remaining time (ALWAYS, not conditionally)
                if (_eyeRestTimer != null)
                {
                    var wasEnabled = _eyeRestTimer.IsEnabled;
                    if (wasEnabled)
                    {
                        var elapsed = _clock.Now - _eyeRestStartTime;
                        _eyeRestRemainingTime = _eyeRestInterval - elapsed;
                    }
                    _eyeRestTimer.Stop();
                    _logger.LogInformation("🔧 Eye rest timer FORCE-STOPPED during break delay (was {State}, remaining: {RemainingMinutes:F1}m)",
                        wasEnabled ? "running" : "stopped", _eyeRestRemainingTime.TotalMinutes);
                }

                // CRITICAL FIX: FORCE stop warning timer (ALWAYS, not conditionally)
                if (_eyeRestWarningTimer != null)
                {
                    var wasEnabled = _eyeRestWarningTimer.IsEnabled;
                    _eyeRestWarningTimer.Stop();
                    _logger.LogInformation("🔧 Eye rest WARNING timer FORCE-STOPPED during break delay (was {State})", wasEnabled ? "running" : "stopped");
                }

                // CONSOLIDATION: _eyeRestFallbackTimer removed (was causing race conditions)
                // Warning fallback timer still needed to protect warning phase
                if (_eyeRestWarningFallbackTimer != null)
                {
                    var wasEnabled = _eyeRestWarningFallbackTimer.IsEnabled;
                    _eyeRestWarningFallbackTimer.Stop();
                    _logger.LogInformation("🔧 Eye rest warning FALLBACK timer FORCE-STOPPED during break delay (was {State})", wasEnabled ? "running" : "stopped");
                }

                // CRITICAL FIX: Stop break warning timers during delay (was missing, causing popup to reappear)
                // The break warning timer countdown must be stopped to prevent it from firing during the delay period
                if (_breakWarningTimer != null)
                {
                    var wasEnabled = _breakWarningTimer.IsEnabled;
                    _breakWarningTimer.Stop();
                    _logger.LogInformation("🔧 Break WARNING timer FORCE-STOPPED during break delay (was {State})", wasEnabled ? "running" : "stopped");
                }

                // CRITICAL FIX: Stop break warning fallback timer during delay (was missing, causing popup to reappear)
                if (_breakWarningFallbackTimer != null)
                {
                    var wasEnabled = _breakWarningFallbackTimer.IsEnabled;
                    _breakWarningFallbackTimer.Stop();
                    _logger.LogInformation("🔧 Break warning FALLBACK timer FORCE-STOPPED during break delay (was {State})", wasEnabled ? "running" : "stopped");
                }

                // CRITICAL FIX: Clear all eye rest processing flags to prevent state conflicts
                _isEyeRestWarningProcessing = false;
                _isAnyEyeRestWarningProcessing = false;
                _isEyeRestEventProcessing = false;
                _isAnyEyeRestEventProcessing = false;
                _logger.LogInformation("🔧 Eye rest processing flags cleared during break delay");

                // CRITICAL FIX: Clear break processing flags to prevent stale break handlers from interfering with delay
                _isBreakWarningProcessing = false;
                _isAnyBreakWarningProcessing = false;
                _isBreakEventProcessing = false;
                lock (_globalBreakLock)
                {
                    _isAnyBreakEventProcessing = false;
                }
                _logger.LogInformation("🔧 Break processing flags cleared during break delay");

                // CRITICAL FIX: Set the flag to prevent eye rest timer from restarting during delay
                // This prevents RestartEyeRestTimerAfterCompletion from restarting the timer
                _eyeRestTimerPausedForBreak = true;
                _logger.LogInformation("🔧 Eye rest timer pause flag set during break delay to prevent auto-restart");

                // CRITICAL FIX: Close any currently showing eye rest popups during break delay
                // This prevents eye rest popups from completing during the delay period
                if (_isEyeRestNotificationActive)
                {
                    _logger.LogInformation("🔧 Closing active eye rest popup during break delay");
                    _notificationService?.HideAllNotifications();
                    _isEyeRestNotificationActive = false;
                }

                // CRITICAL FIX: Stop and dispose any existing delay timer to prevent multiple timers
                if (_breakDelayTimer != null)
                {
                    _breakDelayTimer.Stop();
                    _breakDelayTimer.Tick -= OnBreakDelayTimerTick;
                    _breakDelayTimer.Dispose();
                    _logger.LogInformation("🔧 Stopped and disposed previous break delay timer to prevent conflicts");
                }

                // Set timer to resume after delay
                _breakDelayTimer = _timerFactory.CreateTimer();
                _breakDelayTimer.Interval = delay;

                _breakDelayTimer.Tick += OnBreakDelayTimerTick;

                _breakDelayTimer.Start();
                _logger.LogInformation("🔧 Break delay timer started for {Minutes} minute(s)", delay.TotalMinutes);

                // Delay event recording lives in ApplicationOrchestrator.OnBreakDue
                // (case DelayOneMinute / DelayFiveMinutes); recording it here too
                // double-counted delays and suppressed compliance.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delaying break");
                IsBreakDelayed = false;
            }
        }
        /// <summary>
        /// Named event handler for break delay timer tick, replacing anonymous lambda to allow proper unsubscription.
        /// </summary>
        private async void OnBreakDelayTimerTick(object? sender, EventArgs e)
        {
            await Task.CompletedTask;
            _breakDelayTimer?.Stop();
            IsBreakDelayed = false;

            if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
            {
                // CRITICAL FIX: Use warning flow instead of direct TriggerBreak()
                // This ensures users get the 30-second warning before break starts,
                // maintaining consistent user experience regardless of how break was initiated
                _logger.LogInformation("Delay period ended - starting break warning flow for consistent user experience");

                // Check if warnings are enabled
                var warningEnabled = _configuration?.Break?.WarningEnabled ?? true;
                var warningSeconds = _configuration?.Break?.WarningSeconds ?? 30;

                if (warningEnabled && warningSeconds > 0)
                {
                    // Use the proper warning flow
                    StartBreakWarningTimer();
                }
                else
                {
                    // Warnings disabled - trigger break directly
                    _logger.LogInformation("Break warnings disabled - triggering break directly after delay");
                    TriggerBreak();
                }

                // Note: Eye rest timer coordination is handled by SmartPauseEyeRestTimerForBreak()
                // which is called in TriggerBreakWarning() and TriggerBreak()
            }
        }
    }
}