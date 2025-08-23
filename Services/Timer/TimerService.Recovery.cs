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
        private System.Windows.Threading.DispatcherTimer? _healthMonitorTimer;
        
        private void InitializeHealthMonitor()
        {
            if (_healthMonitorTimer == null)
            {
                _healthMonitorTimer = new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromMinutes(1) // Check every minute
                };
                
                _healthMonitorTimer.Tick += OnHealthMonitorTick;
                
                _logger.LogInformation("❤️ Health monitor initialized - checking timer health every minute");
            }
        }

        private void OnHealthMonitorTick(object? sender, EventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var timeSinceLastHeartbeat = now - _lastHeartbeat;
                
                _logger.LogCritical($"❤️ HEALTH CHECK at {now:HH:mm:ss.fff} - Last heartbeat: {timeSinceLastHeartbeat.TotalSeconds:F1}s ago");
                
                // Log current process and memory status
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    _logger.LogCritical($"❤️ SERVICE STATUS: Running={IsRunning}, Paused={IsPaused}, SmartPaused={IsSmartPaused}");
                    _logger.LogCritical($"❤️ TIMER STATUS: EyeRest={_eyeRestTimer?.IsEnabled}, Break={_breakTimer?.IsEnabled}");
                    _logger.LogCritical($"❤️ PROCESS STATUS: ID={process.Id}, Memory={process.WorkingSet64 / 1024 / 1024}MB");
                }
                
                // Check if timers haven't fired for too long
                if (timeSinceLastHeartbeat.TotalMinutes > 3)
                {
                    _logger.LogCritical($"🚨 TIMER HANG DETECTED - No heartbeat for {timeSinceLastHeartbeat.TotalMinutes:F1} minutes!");
                    
                    // ENHANCED: Auto-recovery after 3 minutes of no heartbeat
                    if (timeSinceLastHeartbeat.TotalMinutes > 3)
                    {
                        _logger.LogCritical($"🔧 INITIATING TIMER RECOVERY - Attempting to fix timer hang after {timeSinceLastHeartbeat.TotalMinutes:F1} minutes");
                        RecoverTimersFromHang();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitor tick");
            }
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
                
                // STEP 2.5: CRITICAL: Clear any zombie popup references during recovery
                // This fixes the "17 seconds forever" issue where popups persist after timer recovery
                try
                {
                    _logger.LogCritical("🧟 ZOMBIE FIX: Clearing popup references during timer recovery");
                    _notificationService?.HideAllNotifications();
                    _logger.LogCritical("🧟 ZOMBIE FIX: Popup references cleared during recovery");
                }
                catch (Exception popupEx)
                {
                    _logger.LogError(popupEx, "🧟 ZOMBIE FIX: Error clearing popups during recovery");
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
                
                // CRITICAL FIX: Prevent recovery during initial startup to avoid race condition
                if (!_hasCompletedInitialStartup)
                {
                    _logger.LogInformation($"🔄 Skipping recovery during initial startup - {reason}");
                    return;
                }
                
                // Store current timer states before recovery
                var wasRunning = IsRunning;
                var wasPaused = IsPaused;
                var wasSmartPaused = IsSmartPaused;
                var wasManuallyPaused = IsManuallyPaused;
                var currentPauseReason = PauseReason;
                
                var eyeRestElapsed = _eyeRestTimer?.IsEnabled == true ? 
                    DateTime.Now - _eyeRestStartTime : TimeSpan.Zero;
                var breakElapsed = _breakTimer?.IsEnabled == true ? 
                    DateTime.Now - _breakStartTime : TimeSpan.Zero;
                
                _logger.LogCritical($"🔄 Pre-recovery state: Running={wasRunning}, Paused={wasPaused}, SmartPaused={wasSmartPaused}, ManuallyPaused={wasManuallyPaused}");
                _logger.LogCritical($"🔄 Timer elapsed times - EyeRest: {eyeRestElapsed.TotalSeconds:F1}s, Break: {breakElapsed.TotalSeconds:F1}s");
                
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
                
                // Check if this qualifies as an extended away period requiring smart session reset
                if (timeSincePause.TotalMinutes >= extendedAwayThresholdMinutes && config.UserPresence.EnableSmartSessionReset)
                {
                    _logger.LogCritical($"🌅 EXTENDED AWAY DETECTED: {timeSincePause.TotalMinutes:F1} minutes (threshold: {extendedAwayThresholdMinutes} min)");
                    _logger.LogCritical($"🌅 Treating as NEW WORKING SESSION after overnight/extended standby");
                    
                    // Clear all pause states
                    IsManuallyPaused = false;
                    IsPaused = false;
                    IsSmartPaused = false;
                    _pauseReason = string.Empty;
                    _manualPauseStartTime = DateTime.MinValue;
                    _pauseStartTime = DateTime.MinValue;
                    
                    // Perform smart session reset for fresh start
                    await SmartSessionResetAsync($"Extended away ({timeSincePause.TotalMinutes:F0}min) - new working session after standby");
                    
                    _logger.LogCritical($"✅ NEW SESSION STARTED: Fresh timers after extended standby");
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

        private async Task<(bool IsWorking, string Issue)> TestTimerFunctionality()
        {
            try
            {
                var testTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                
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
                // Adjust timer intervals to compensate for time passed during sleep
                if (_eyeRestTimer != null && eyeRestElapsed > TimeSpan.Zero)
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
                        _logger.LogInformation($"🔄 Eye rest was due during sleep - triggering now");
                        TriggerEyeRest();
                    }
                }
                
                if (_breakTimer != null && breakElapsed > TimeSpan.Zero)
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