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
        /// Buffer added to the eye-rest occupancy window when deciding whether to coalesce
        /// an eye-rest tick into the upcoming break. A small positive value avoids edge cases
        /// where the break is just *slightly* outside the occupancy window but would still
        /// fire visually back-to-back with the eye-rest popup.
        /// </summary>
        private const int COALESCE_BUFFER_SECONDS = 5;

        /// <summary>
        /// Returns true when the current eye-rest tick should be skipped because a break is
        /// imminent enough that running both back-to-back would interrupt the user twice.
        ///
        /// <para>
        /// Trigger conditions (ALL must hold):
        /// <list type="bullet">
        ///   <item>The break timer is currently running (not paused, delayed, or in a notification)</item>
        ///   <item>TimeUntilNextBreak ≤ eye-rest occupancy + COALESCE_BUFFER_SECONDS</item>
        /// </list>
        /// where occupancy = warning seconds (if enabled) + duration seconds.
        /// </para>
        ///
        /// <para>
        /// Rationale: in a configuration like (eyeRestInterval=20m, breakInterval=60m),
        /// the third eye-rest tick collides with the break tick. Running eye rest first
        /// pauses the break timer, the eye-rest popup runs ~20s, then the break resumes
        /// with ~0s remaining and fires immediately. The user sees both popups back-to-back.
        /// Skipping the eye rest lets the break fire on its own schedule, and the
        /// post-break SmartSessionResetAsync re-arms the eye rest fresh.
        /// </para>
        ///
        /// <para>
        /// Side-effect note: this method is a pure predicate. The caller is responsible
        /// for stopping/re-arming the eye-rest timer.
        /// </para>
        /// </summary>
        private bool ShouldCoalesceEyeRestIntoBreak()
        {
            // Break must be live-ticking. If it's paused or disabled, no collision risk.
            if (_breakTimer?.IsEnabled != true) return false;

            // Don't coalesce if break is already in some active state — the existing
            // SmartPause/Resume coordination handles those cases on its own.
            if (_isBreakNotificationActive) return false;
            if (_isBreakEventProcessing) return false;
            if (IsBreakDelayed) return false;
            if (_breakTimerPausedForEyeRest) return false; // defensive: shouldn't be at this point

            // Don't coalesce if service-level pause states are set (the eye-rest tick handler
            // already filters these out earlier, but being explicit prevents future drift).
            if (IsPaused || IsManuallyPaused || IsSmartPaused) return false;

            // Compute the eye-rest occupancy window.
            var warningSec = (_configuration?.EyeRest?.WarningEnabled == true)
                ? (_configuration.EyeRest.WarningSeconds)
                : 0;
            var durationSec = _configuration?.EyeRest?.DurationSeconds ?? 20;
            var threshold = TimeSpan.FromSeconds(warningSec + durationSec + COALESCE_BUFFER_SECONDS);

            var breakRemaining = TimeUntilNextBreak;

            // Negative or zero means break is already due — definitely coalesce.
            // Positive but within threshold means break is imminent — coalesce.
            // Beyond threshold → no coalesce, normal eye-rest flow.
            if (breakRemaining > threshold) return false;

            _logger.LogInformation(
                "🔀 COALESCE: break is {BreakRemainingSec:F1}s away (≤ threshold {ThresholdSec:F0}s = warning {Warning}s + duration {Duration}s + buffer {Buffer}s) — skipping eye-rest tick",
                breakRemaining.TotalSeconds, threshold.TotalSeconds, warningSec, durationSec, COALESCE_BUFFER_SECONDS);

            return true;
        }

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