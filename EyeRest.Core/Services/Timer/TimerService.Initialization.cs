using System;
using Microsoft.Extensions.Logging;
using EyeRest.Services.Abstractions;

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
                _eyeRestTimer = _timerFactory.CreateTimer(TimerPriority.Normal);
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
            
            _eyeRestWarningTimer = _timerFactory.CreateTimer(TimerPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        private void InitializeBreakTimer()
        {
            if (_breakTimer == null)
            {
                _breakTimer = _timerFactory.CreateTimer(TimerPriority.Normal);
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
            
            _breakWarningTimer = _timerFactory.CreateTimer(TimerPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        #endregion

        #region Fallback Timer Initialization (DEPRECATED)

        // CONSOLIDATION FIX: These fallback timers have been deprecated/removed.
        // They were causing duplicate event triggers by firing 5 seconds after main timers.
        //
        // Timer protection is now provided by:
        // 1. Warning fallback timers (_eyeRestWarningFallbackTimer, _breakWarningFallbackTimer)
        //    - Fire 2s after warning period if warning timer fails
        // 2. Health monitor (OnHealthMonitorTick)
        //    - Emergency backup for truly stuck timers (>10 minutes without heartbeat)
        //
        // Methods kept for backward compatibility but are no-ops:

        private void InitializeEyeRestFallbackTimer()
        {
            // DEPRECATED: No longer creates fallback timer to prevent race conditions
            _logger.LogDebug("👁️ Eye rest fallback timer initialization skipped (consolidated into warning fallback + health monitor)");
        }

        private void InitializeBreakFallbackTimer()
        {
            // DEPRECATED: No longer creates fallback timer to prevent race conditions
            _logger.LogDebug("☕ Break fallback timer initialization skipped (consolidated into warning fallback + health monitor)");
        }

        // Event handlers removed - no longer needed

        #endregion
    }
}