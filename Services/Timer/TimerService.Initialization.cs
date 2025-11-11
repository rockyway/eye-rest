using System;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all timer initialization methods
    /// </summary>
    public partial class TimerService
    {
        #region Timer Initialization

        private void InitializeEyeRestTimer()
        {
            if (_eyeRestTimer == null)
            {
                _eyeRestTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
                _eyeRestTimer.Tick += OnEyeRestTimerTick;

                // Use shared calculation method to ensure consistency with restart logic
                var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();

                _eyeRestTimer.Interval = interval;
                _eyeRestInterval = interval; // Store calculated interval

                if (isReduced)
                {
                    _logger.LogInformation("🔧 Eye rest timer initialized - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                        interval.TotalMinutes, warningSeconds, totalMinutes);
                }
                else
                {
                    _logger.LogInformation("🔧 Eye rest timer initialized - FULL interval: {IntervalMinutes:F1}m (warnings disabled or invalid)",
                        interval.TotalMinutes);
                }
            }
        }

        private void InitializeEyeRestWarningTimer()
        {
            // Always recreate to ensure clean state
            if (_eyeRestWarningTimer != null)
            {
                _eyeRestWarningTimer.Stop();
                _eyeRestWarningTimer = null;
            }
            
            _eyeRestWarningTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        private void InitializeBreakTimer()
        {
            if (_breakTimer == null)
            {
                _breakTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
                _breakTimer.Tick += OnBreakTimerTick;
                
                // Use shared calculation method to ensure consistency with restart logic
                var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();

                _breakTimer.Interval = interval;

                if (isReduced)
                {
                    _logger.LogInformation("🔧 Break timer initialized - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                        interval.TotalMinutes, warningSeconds, totalMinutes);
                }
                else
                {
                    _logger.LogInformation("🔧 Break timer initialized - FULL interval: {IntervalMinutes:F1}m (warnings disabled or invalid)",
                        interval.TotalMinutes);
                }
                
                _logger.LogInformation("🔧 Break timer: Enabled={IsEnabled}, Interval={Interval:F1}m, Event handlers registered", 
                    _breakTimer.IsEnabled, _breakTimer.Interval.TotalMinutes);
            }
        }

        private void InitializeBreakWarningTimer()
        {
            // Always recreate to ensure clean state
            if (_breakWarningTimer != null)
            {
                _breakWarningTimer.Stop();
                _breakWarningTimer = null;
            }
            
            _breakWarningTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        #endregion

        #region Fallback Timer Initialization

        private void InitializeEyeRestFallbackTimer()
        {
            if (_eyeRestFallbackTimer == null)
            {
                _eyeRestFallbackTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
                
                // Set to trigger 5 seconds after the expected time
                var totalMinutes = _configuration?.EyeRest?.IntervalMinutes ?? 20;
                var fallbackInterval = TimeSpan.FromMinutes(totalMinutes).Add(TimeSpan.FromSeconds(5));
                
                // CRITICAL FIX: Validate interval doesn't exceed DispatcherTimer maximum capacity
                var maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
                if (fallbackInterval > maxInterval)
                {
                    _logger.LogWarning("⚠️ Eye rest fallback interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m", 
                        fallbackInterval.TotalMinutes, maxInterval.TotalMinutes);
                    fallbackInterval = maxInterval;
                }
                
                _eyeRestFallbackTimer.Interval = fallbackInterval;
                _eyeRestFallbackTimer.Tick += OnEyeRestFallbackTimerTick;
                
                _logger.LogInformation("👁️ Eye rest fallback timer initialized - will trigger at {Minutes}m + 5s if primary fails", 
                    totalMinutes);
            }
        }

        private void InitializeBreakFallbackTimer()
        {
            if (_breakFallbackTimer == null)
            {
                _breakFallbackTimer = _timerFactory.CreateTimer(DispatcherPriority.Normal);
                
                // Set to trigger 5 seconds after the expected time
                var totalMinutes = _configuration?.Break?.IntervalMinutes ?? 55;
                var fallbackInterval = TimeSpan.FromMinutes(totalMinutes).Add(TimeSpan.FromSeconds(5));
                
                // CRITICAL FIX: Validate interval doesn't exceed DispatcherTimer maximum capacity
                var maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
                if (fallbackInterval > maxInterval)
                {
                    _logger.LogWarning("⚠️ Break fallback interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m", 
                        fallbackInterval.TotalMinutes, maxInterval.TotalMinutes);
                    fallbackInterval = maxInterval;
                }
                
                _breakFallbackTimer.Interval = fallbackInterval;
                _breakFallbackTimer.Tick += OnBreakFallbackTimerTick;
                
                _logger.LogInformation("☕ Break fallback timer initialized - will trigger at {Minutes}m + 5s if primary fails", 
                    totalMinutes);
            }
        }

        private void OnEyeRestFallbackTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogWarning("⚠️ FALLBACK: Eye rest timer didn't fire on time - forcing trigger");
                UpdateHeartbeatFromOperation("EyeRestFallback");
                
                _eyeRestFallbackTimer?.Stop();
                
                // Force trigger eye rest if not already active
                if (!_isEyeRestNotificationActive)
                {
                    // TIMELINE FIX: Check if enough time has passed since last trigger
                    if (!ShouldAllowEyeRestFallback())
                    {
                        _logger.LogInformation("🕒 INIT FALLBACK BLOCKED: Eye rest initialization fallback blocked - insufficient time since last trigger");
                    }
                    // BREAK PRIORITY FIX: Check break priority before initialization fallback trigger
                    else if (_isBreakNotificationActive || _isBreakWarningProcessing || _isAnyBreakWarningProcessing ||
                        _isBreakEventProcessing || _isAnyBreakEventProcessing)
                    {
                        _logger.LogInformation("🔄 INIT FALLBACK BLOCKED: Eye rest initialization fallback blocked - break event has priority. Pausing eye rest timer.");
                        SmartPauseEyeRestTimerForBreak();
                    }
                    else
                    {
                        TriggerEyeRest();
                        _logger.LogInformation("✅ FALLBACK: Eye rest triggered successfully");
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️ FALLBACK: Eye rest already active, skipping");
                }
                
                // Reset fallback timer for next cycle
                _eyeRestFallbackTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in eye rest fallback timer");
            }
        }

        private void OnBreakFallbackTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogWarning("⚠️ FALLBACK: Break timer didn't fire on time - forcing trigger");
                UpdateHeartbeatFromOperation("BreakFallback");
                
                _breakFallbackTimer?.Stop();
                
                // Force trigger break if not already active
                if (!_isBreakNotificationActive && !IsBreakDelayed)
                {
                    TriggerBreak();
                    _logger.LogInformation("✅ FALLBACK: Break triggered successfully");
                }
                else
                {
                    _logger.LogInformation("ℹ️ FALLBACK: Break already active or delayed, skipping");
                }
                
                // Reset fallback timer for next cycle
                _breakFallbackTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in break fallback timer");
            }
        }

        #endregion
    }
}