using System;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing smart timer coordination methods
    /// </summary>
    public partial class TimerService
    {
        #region Smart Timer Coordination
        
        /// <summary>
        /// CRITICAL: Smart coordination to prevent conflicts between eye rest and break notifications
        /// Automatically pauses eye rest timer when break notification is active
        /// </summary>
        public void SmartPauseEyeRestTimerForBreak()
        {
            if (_eyeRestTimer?.IsEnabled == true && !_eyeRestTimerPausedForBreak)
            {
                _logger.LogInformation("🔄 Smart coordination: Pausing eye rest timer during break notification");
                _eyeRestTimerPausedForBreak = true;
                
                // Calculate and store remaining time
                var elapsed = _clock.Now - _eyeRestStartTime;
                _eyeRestRemainingTime = _eyeRestInterval - elapsed;
                
                // Only store positive remaining time
                if (_eyeRestRemainingTime < TimeSpan.Zero)
                {
                    _eyeRestRemainingTime = TimeSpan.Zero;
                }
                
                _eyeRestTimer.Stop();
                _logger.LogInformation($"🔄 Eye rest timer paused with {_eyeRestRemainingTime.TotalMinutes:F1} minutes remaining");
            }
        }
        
        /// <summary>
        /// Resume eye rest timer after break notification completes
        /// </summary>
        public void SmartResumeEyeRestTimerAfterBreak()
        {
            // CRITICAL FIX: Check for ALL break-related activity to ensure proper resumption
            if (_eyeRestTimerPausedForBreak && !_isBreakNotificationActive &&
                !_isBreakWarningProcessing && !_isAnyBreakWarningProcessing &&
                !_isBreakEventProcessing && !_isAnyBreakEventProcessing)
            {
                _logger.LogInformation("🔄 Smart coordination: Resuming eye rest timer after break completion");
                _eyeRestTimerPausedForBreak = false;

                // BREAK PRIORITY FIX: Always reset eye rest timer to full interval after break to prevent conflicts
                // Use shared calculation method for consistency
                var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();

                _eyeRestInterval = interval;
                _eyeRestTimer!.Interval = _eyeRestInterval;
                _eyeRestTimer!.Start();
                _eyeRestStartTime = _clock.Now;

                if (isReduced)
                {
                    _logger.LogInformation("🔄 Eye rest timer reset after break - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                        _eyeRestInterval.TotalMinutes, warningSeconds, totalMinutes);
                }
                else
                {
                    _logger.LogInformation("🔄 Eye rest timer reset after break - FULL interval: {IntervalMinutes:F1}m (no warning)",
                        _eyeRestInterval.TotalMinutes);
                }

                _eyeRestRemainingTime = TimeSpan.Zero;
            }
            else if (_eyeRestTimerPausedForBreak)
            {
                _logger.LogDebug("🔄 Eye rest timer resume blocked - break activity still ongoing. Break states: NotificationActive={BreakNotification}, WarningProcessing={WarningProcessing}, GlobalWarning={GlobalWarning}, EventProcessing={EventProcessing}, GlobalEvent={GlobalEvent}",
                    _isBreakNotificationActive, _isBreakWarningProcessing, _isAnyBreakWarningProcessing, _isBreakEventProcessing, _isAnyBreakEventProcessing);
            }
        }
        
        /// <summary>
        /// Smart pause break timer during eye rest notification
        /// </summary>
        public void SmartPauseBreakTimerForEyeRest()
        {
            if (_breakTimer?.IsEnabled == true && !_breakTimerPausedForEyeRest)
            {
                _logger.LogInformation("🔄 Smart coordination: Pausing break timer during eye rest notification");
                _breakTimerPausedForEyeRest = true;
                
                // Calculate and store remaining time
                var elapsed = _clock.Now - _breakStartTime;
                _breakRemainingTime = _breakInterval - elapsed;
                
                // Only store positive remaining time
                if (_breakRemainingTime < TimeSpan.Zero)
                {
                    _breakRemainingTime = TimeSpan.Zero;
                }
                
                _breakTimer.Stop();
                _logger.LogInformation($"🔄 Break timer paused with {_breakRemainingTime.TotalMinutes:F1} minutes remaining");
            }
        }
        
        /// <summary>
        /// Resume break timer after eye rest notification completes
        /// </summary>
        public void SmartResumeBreakTimerAfterEyeRest()
        {
            if (_breakTimerPausedForEyeRest && !_isEyeRestNotificationActive)
            {
                _logger.LogInformation("🔄 Smart coordination: Resuming break timer after eye rest completion");
                _breakTimerPausedForEyeRest = false;

                // Restore timer with remaining time
                if (_breakRemainingTime > TimeSpan.Zero)
                {
                    _breakInterval = _breakRemainingTime;
                    _breakTimer!.Interval = _breakRemainingTime;
                    _breakTimer!.Start();
                    _breakStartTime = _clock.Now;
                    _logger.LogInformation($"🔄 Break timer resumed with {_breakRemainingTime.TotalMinutes:F1} minutes remaining");
                }
                else
                {
                    // No remaining time, reset to full interval using shared calculation method
                    var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();
                    _breakInterval = interval;
                    _breakTimer!.Interval = _breakInterval;
                    _breakTimer!.Start();
                    _breakStartTime = _clock.Now;

                    if (isReduced)
                    {
                        _logger.LogInformation("🔄 Break timer reset after eye rest - REDUCED interval: {IntervalMinutes:F1}m (triggers warning {WarningSeconds}s before {TotalMinutes}min target)",
                            _breakInterval.TotalMinutes, warningSeconds, totalMinutes);
                    }
                    else
                    {
                        _logger.LogInformation("🔄 Break timer reset after eye rest - FULL interval: {IntervalMinutes:F1}m (no warning)",
                            _breakInterval.TotalMinutes);
                    }
                }

                _breakRemainingTime = TimeSpan.Zero;
            }
        }
        
        #endregion
    }
}