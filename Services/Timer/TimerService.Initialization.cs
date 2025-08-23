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
                _eyeRestTimer = new DispatcherTimer(DispatcherPriority.Normal);
                _eyeRestTimer.Tick += OnEyeRestTimerTick;
                
                // Calculate interval (total time minus warning period)
                var totalMinutes = _configuration?.EyeRest?.IntervalMinutes ?? 20;
                var warningSeconds = _configuration?.EyeRest?.WarningSeconds ?? 15;
                var interval = TimeSpan.FromMinutes(totalMinutes) - TimeSpan.FromSeconds(warningSeconds);
                
                _eyeRestTimer.Interval = interval;
                _logger.LogInformation("Eye rest timer initialized - interval: {TotalSeconds}s (warning at {WarningSeconds}s before {TotalMinutes}min target)", 
                    interval.TotalSeconds, warningSeconds, totalMinutes);
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
            
            _eyeRestWarningTimer = new DispatcherTimer(DispatcherPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        private void InitializeBreakTimer()
        {
            if (_breakTimer == null)
            {
                _breakTimer = new DispatcherTimer(DispatcherPriority.Normal);
                _breakTimer.Tick += OnBreakTimerTick;
                
                // Calculate interval (total time minus warning period)
                var totalMinutes = _configuration?.Break?.IntervalMinutes ?? 55;
                var warningSeconds = _configuration?.Break?.WarningSeconds ?? 30;
                var interval = TimeSpan.FromMinutes(totalMinutes) - TimeSpan.FromSeconds(warningSeconds);
                
                _breakTimer.Interval = interval;
                _logger.LogInformation("🔧 Break timer initialized - interval: {TotalMinutes}m (warning at {WarningSeconds}s before {ConfiguredMinutes}min target)", 
                    interval.TotalMinutes, warningSeconds, totalMinutes);
                _logger.LogInformation("🔧 Break timer: Enabled={IsEnabled}, Interval={Interval}m, Event handlers registered", 
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
            
            _breakWarningTimer = new DispatcherTimer(DispatcherPriority.Normal);
            // Event handler will be attached when the timer is started
        }

        #endregion

        #region Fallback Timer Initialization

        private void InitializeEyeRestFallbackTimer()
        {
            if (_eyeRestFallbackTimer == null)
            {
                _eyeRestFallbackTimer = new DispatcherTimer(DispatcherPriority.Normal);
                
                // Set to trigger 5 seconds after the expected time
                var totalMinutes = _configuration?.EyeRest?.IntervalMinutes ?? 20;
                var fallbackInterval = TimeSpan.FromMinutes(totalMinutes).Add(TimeSpan.FromSeconds(5));
                
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
                _breakFallbackTimer = new DispatcherTimer(DispatcherPriority.Normal);
                
                // Set to trigger 5 seconds after the expected time
                var totalMinutes = _configuration?.Break?.IntervalMinutes ?? 55;
                var fallbackInterval = TimeSpan.FromMinutes(totalMinutes).Add(TimeSpan.FromSeconds(5));
                
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
                UpdateHeartbeat();
                
                _eyeRestFallbackTimer?.Stop();
                
                // Force trigger eye rest if not already active
                if (!_isEyeRestNotificationActive)
                {
                    TriggerEyeRest();
                    _logger.LogInformation("✅ FALLBACK: Eye rest triggered successfully");
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
                UpdateHeartbeat();
                
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