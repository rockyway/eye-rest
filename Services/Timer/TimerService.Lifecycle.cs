using System;
using System.Threading.Tasks;
using System.Windows.Threading;
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
                
                // Initialize fallback timers
                InitializeEyeRestFallbackTimer();
                InitializeBreakFallbackTimer();
                
                // Initialize health monitoring
                InitializeHealthMonitor();
                
                // CRITICAL FIX: Timer intervals should be FULL intervals, not reduced by warning time
                // The warning is handled separately by the warning timer system
                _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes);
                _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes);
                
                // CRITICAL FIX: Validate intervals don't exceed DispatcherTimer maximum capacity
                var maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
                if (_eyeRestInterval > maxInterval)
                {
                    _logger.LogWarning("⚠️ Eye rest interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m", 
                        _eyeRestInterval.TotalMinutes, maxInterval.TotalMinutes);
                    _eyeRestInterval = maxInterval;
                }
                if (_breakInterval > maxInterval)
                {
                    _logger.LogWarning("⚠️ Break interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m", 
                        _breakInterval.TotalMinutes, maxInterval.TotalMinutes);
                    _breakInterval = maxInterval;
                }
                
                _eyeRestTimer.Interval = _eyeRestInterval;
                _breakTimer.Interval = _breakInterval;
                
                // Start timers
                _eyeRestTimer.Start();
                _breakTimer.Start();
                _eyeRestStartTime = DateTime.Now;
                _breakStartTime = DateTime.Now;
                _breakTimerStartTime = DateTime.Now;
                
                // Start health monitor
                _healthMonitorTimer?.Start();
                UpdateHeartbeatFromOperation("StartAsync");
                
                IsRunning = true;
                
                // Mark initial startup as complete
                _hasCompletedInitialStartup = true;
                
                await _analyticsService.RecordSessionStartAsync();
                
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
                
                // Stop all timers
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                _eyeRestWarningTimer?.Stop();
                _breakWarningTimer?.Stop();
                _eyeRestFallbackTimer?.Stop();
                _breakFallbackTimer?.Stop();
                _healthMonitorTimer?.Stop();
                
                // Stop manual pause timer if active
                if (_manualPauseTimer != null)
                {
                    _manualPauseTimer.Stop();
                    _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                    _manualPauseTimer = null;
                }
                
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
            try
            {
                _logger.LogInformation("🔄 Resetting eye rest timer");
                
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    
                    // Clear notification state
                    _isEyeRestNotificationActive = false;
                    
                    // CRITICAL FIX: Use FULL interval consistently - warning is handled by separate warning timer
                    _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes);
                    _eyeRestTimer.Interval = _eyeRestInterval;
                    
                    // Clear any remaining time
                    _eyeRestRemainingTime = TimeSpan.Zero;
                    
                    // Start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                    {
                        _eyeRestTimer.Start();
                        _eyeRestStartTime = DateTime.Now;
                    }
                    else
                    {
                        // Store the full interval as remaining time for when we resume
                        _eyeRestRemainingTime = _eyeRestInterval;
                    }
                    
                    _logger.LogInformation("Eye rest timer reset to {Minutes} minutes", _configuration.EyeRest.IntervalMinutes);
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
            try
            {
                _logger.LogInformation("🔄 Resetting break timer");
                
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    
                    // Clear notification and delay states
                    _isBreakNotificationActive = false;
                    IsBreakDelayed = false;
                    
                    // CRITICAL FIX: Use FULL interval consistently - warning is handled by separate warning timer
                    _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes);
                    _breakTimer.Interval = _breakInterval;
                    
                    // Clear any remaining time
                    _breakRemainingTime = TimeSpan.Zero;
                    
                    // Start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                    {
                        _breakTimer.Start();
                        _breakStartTime = DateTime.Now;
                        _breakTimerStartTime = DateTime.Now;
                    }
                    else
                    {
                        // Store the full interval as remaining time for when we resume
                        _breakRemainingTime = _breakInterval;
                    }
                    
                    _logger.LogInformation("Break timer reset to {Minutes} minutes", _configuration.Break.IntervalMinutes);
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
            try
            {
                _logger.LogInformation("♻️ Restarting eye rest timer after completion");
                
                // Clear notification state
                _isEyeRestNotificationActive = false;
                
                if (_eyeRestTimer != null)
                {
                    // Reset to full interval consistently
                    _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes);
                    _eyeRestTimer.Interval = _eyeRestInterval;
                    
                    // Only start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused && !_eyeRestTimerPausedForBreak)
                    {
                        _eyeRestTimer.Start();
                        _eyeRestStartTime = DateTime.Now;
                        _logger.LogInformation("Eye rest timer restarted - next in {Minutes} minutes", 
                            _configuration.EyeRest.IntervalMinutes);
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
            try
            {
                _logger.LogInformation("♻️ Restarting break timer after completion");
                
                // Clear notification state
                _isBreakNotificationActive = false;
                
                if (_breakTimer != null)
                {
                    // Reset to full interval consistently
                    _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes);
                    _breakTimer.Interval = _breakInterval;
                    
                    // Only start if not paused
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused && !_breakTimerPausedForEyeRest)
                    {
                        _breakTimer.Start();
                        _breakStartTime = DateTime.Now;
                        _breakTimerStartTime = DateTime.Now;
                        _logger.LogInformation("Break timer restarted - next in {Minutes} minutes", 
                            _configuration.Break.IntervalMinutes);
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
                _logger.LogInformation("⏳ Delaying break for {Minutes} minutes", delay.TotalMinutes);
                
                IsBreakDelayed = true;
                _delayStartTime = DateTime.Now;
                _delayDuration = delay;
                
                // Stop the break timer during delay
                _breakTimer?.Stop();
                
                // Set timer to resume after delay
                var delayTimer = _timerFactory.CreateTimer();
                delayTimer.Interval = delay;
                
                delayTimer.Tick += async (s, e) =>
                {
                    delayTimer.Stop();
                    IsBreakDelayed = false;
                    
                    if (IsRunning && !IsPaused && !IsSmartPaused && !IsManuallyPaused)
                    {
                        // Trigger break immediately after delay
                        _logger.LogInformation("Delay period ended - triggering break now");
                        TriggerBreak();
                    }
                };
                
                delayTimer.Start();
                
                // Record the delay as a break event with appropriate action
                var delayAction = delay.TotalMinutes >= 5 ? UserAction.Delayed5Min : UserAction.Delayed1Min;
                await _analyticsService.RecordBreakEventAsync(RestEventType.Break, delayAction, delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delaying break");
                IsBreakDelayed = false;
            }
        }
    }
}