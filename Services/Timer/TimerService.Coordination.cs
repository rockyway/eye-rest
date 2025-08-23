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
                var elapsed = DateTime.Now - _eyeRestStartTime;
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
            if (_eyeRestTimerPausedForBreak && !_isBreakNotificationActive)
            {
                _logger.LogInformation("🔄 Smart coordination: Resuming eye rest timer after break completion");
                _eyeRestTimerPausedForBreak = false;
                
                // Restore timer with remaining time
                if (_eyeRestRemainingTime > TimeSpan.Zero)
                {
                    _eyeRestTimer.Interval = _eyeRestRemainingTime;
                    _eyeRestTimer.Start();
                    _eyeRestStartTime = DateTime.Now;
                    _logger.LogInformation($"🔄 Eye rest timer resumed with {_eyeRestRemainingTime.TotalMinutes:F1} minutes remaining");
                }
                else
                {
                    // No remaining time, reset to full interval
                    _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes) - 
                                     TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                    _eyeRestTimer.Interval = _eyeRestInterval;
                    _eyeRestTimer.Start();
                    _eyeRestStartTime = DateTime.Now;
                    _logger.LogInformation($"🔄 Eye rest timer reset to full interval: {_eyeRestInterval.TotalMinutes:F1} minutes");
                }
                
                _eyeRestRemainingTime = TimeSpan.Zero;
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
                var elapsed = DateTime.Now - _breakStartTime;
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
                    _breakTimer.Interval = _breakRemainingTime;
                    _breakTimer.Start();
                    _breakStartTime = DateTime.Now;
                    _logger.LogInformation($"🔄 Break timer resumed with {_breakRemainingTime.TotalMinutes:F1} minutes remaining");
                }
                else
                {
                    // No remaining time, reset to full interval
                    _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes) - 
                                   TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                    _breakTimer.Interval = _breakInterval;
                    _breakTimer.Start();
                    _breakStartTime = DateTime.Now;
                    _logger.LogInformation($"🔄 Break timer reset to full interval: {_breakInterval.TotalMinutes:F1} minutes");
                }
                
                _breakRemainingTime = TimeSpan.Zero;
            }
        }
        
        #endregion
    }
}