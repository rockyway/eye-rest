using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all pause and resume operations
    /// </summary>
    public partial class TimerService
    {
        public async Task PauseAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Cannot pause - timer service is not running");
                return;
            }
            
            if (IsPaused)
            {
                _logger.LogWarning("Timer service is already paused");
                return;
            }

            try
            {
                _logger.LogInformation("⏸️ Manually pausing timer service");
                
                IsPaused = true;
                _pauseStartTime = DateTime.Now; // Track when pause started for extended away detection
                
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                
                await _analyticsService.RecordPauseEventAsync(EyeRest.Services.PauseReason.Manual);
                
                _logger.LogInformation("Timer service paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing timer service");
                IsPaused = false;
                throw;
            }
        }
        
        public async Task ResumeAsync()
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Cannot resume - timer service is not running");
                return;
            }
            
            if (!IsPaused && !IsManuallyPaused)
            {
                _logger.LogWarning("Timer service is not paused");
                return;
            }

            try
            {
                _logger.LogInformation("▶️ Manually resuming timer service");
                
                // CRITICAL FIX: Clear manual pause if active with comprehensive cleanup
                if (IsManuallyPaused)
                {
                    _logger.LogInformation("🔧 MANUAL PAUSE CLEANUP: Clearing manual pause state during resume");
                    IsManuallyPaused = false;
                    _manualPauseStartTime = DateTime.MinValue;
                    _manualPauseDuration = TimeSpan.Zero;
                    
                    // Stop and clean up manual pause timer
                    if (_manualPauseTimer != null)
                    {
                        _manualPauseTimer.Stop();
                        _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                        _manualPauseTimer = null;
                        _logger.LogInformation("🔧 MANUAL PAUSE CLEANUP: Manual pause timer disposed successfully");
                    }
                }
                
                IsPaused = false;
                _pauseStartTime = DateTime.MinValue; // Clear pause start time
                
                if (!IsSmartPaused)
                {
                    // Reset start times so TimeUntilNext* doesn't include the paused period
                    _eyeRestStartTime = DateTime.Now;
                    _breakStartTime = DateTime.Now;
                    _breakTimerStartTime = DateTime.Now;

                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    UpdateHeartbeatFromOperation("ManualResume");

                    await _analyticsService.RecordResumeEventAsync(ResumeReason.Manual);

                    _logger.LogInformation("Timer service resumed successfully");
                }
                else
                {
                    _logger.LogInformation("Timer service manual pause cleared but still smart paused");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming timer service");
                throw;
            }
        }
        
        public async Task SmartPauseAsync(string reason)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Cannot smart pause - timer service is not running");
                return;
            }
            
            if (IsSmartPaused)
            {
                _logger.LogWarning("Timer service is already smart paused");
                return;
            }

            try
            {
                _logger.LogInformation("🧠 Smart pausing timer service - reason: {Reason}", reason);

                // CRITICAL FIX: Preserve remaining timer times BEFORE setting IsSmartPaused.
                // The TimeUntilNext* getters short-circuit when IsSmartPaused is true,
                // returning the old _eyeRestRemainingTime/_breakRemainingTime instead of
                // calculating from elapsed time. By preserving first, we capture accurate values.
                if (_eyeRestTimer?.IsEnabled == true && _eyeRestStartTime != DateTime.MinValue)
                {
                    var elapsed = DateTime.Now - _eyeRestStartTime;
                    var remaining = _eyeRestInterval - elapsed;
                    _eyeRestRemainingTime = remaining > TimeSpan.Zero ? remaining : _eyeRestInterval;
                    _logger.LogInformation($"🔧 SMART PAUSE: Preserved eye rest remaining time: {_eyeRestRemainingTime.TotalMinutes:F1} minutes");
                }

                if (_breakTimer?.IsEnabled == true && _breakStartTime != DateTime.MinValue)
                {
                    var elapsed = DateTime.Now - _breakStartTime;
                    var remaining = _breakInterval - elapsed;
                    _breakRemainingTime = remaining > TimeSpan.Zero ? remaining : _breakInterval;
                    _logger.LogInformation($"🔧 SMART PAUSE: Preserved break remaining time: {_breakRemainingTime.TotalMinutes:F1} minutes");
                }

                IsSmartPaused = true;
                _pauseReason = reason;
                _pauseStartTime = DateTime.Now; // Track when pause started for extended away detection
                _logger.LogInformation($"🔄 PAUSE LIFECYCLE: Smart pause activated - Reason: '{reason}', PauseStartTime: {_pauseStartTime:HH:mm:ss}");
                
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                
                await _analyticsService.RecordPauseEventAsync(EyeRest.Services.PauseReason.SmartDetection);
                
                _logger.LogInformation("Timer service smart paused successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error smart pausing timer service");
                IsSmartPaused = false;
                throw;
            }
        }
        
        public async Task SmartResumeAsync()
        {
            await SmartResumeAsync("User request");
        }
        
        public async Task SmartResumeAsync(string reason)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Cannot smart resume - timer service is not running");
                return;
            }
            
            if (!IsSmartPaused)
            {
                _logger.LogWarning("Timer service is not smart paused");
                return;
            }

            try
            {
                _logger.LogInformation($"🧠 Smart resuming timer service - reason: {reason}");
                
                // CRITICAL FIX: Check if any notifications are active and handle them properly
                if (_isEyeRestNotificationActive || _isBreakNotificationActive)
                {
                    _logger.LogWarning("🧠 Cannot smart resume - notification is active. Clearing notification states.");
                    _isEyeRestNotificationActive = false;
                    _isBreakNotificationActive = false;
                }
                
                IsSmartPaused = false;

                // CRITICAL FIX: Save pause start time BEFORE clearing it — the idle duration
                // calculation below needs the original value to detect natural rest/break periods.
                // Previously, clearing _pauseStartTime here caused idleDuration to always be zero,
                // which prevented detection of natural eye rest (>20s) and natural break (>5min),
                // leading to the health monitor's backup trigger firing break popups immediately
                // after the user returned from a long idle period.
                var savedPauseStartTime = _pauseStartTime;
                _pauseStartTime = DateTime.MinValue; // Clear pause start time
                _logger.LogInformation($"🔄 PAUSE LIFECYCLE: Smart pause cleared - Reason: '{reason}', PauseReason cleared: '{_pauseReason}' → ''");

                if (!IsPaused)
                {
                    // Ensure timers are actually created before starting
                    if (_eyeRestTimer == null)
                    {
                        _logger.LogWarning($"🔧 SMART RESUME FIX: Eye rest timer is null - recreating");
                        InitializeEyeRestTimer();
                    }

                    if (_breakTimer == null)
                    {
                        _logger.LogWarning($"🔧 SMART RESUME FIX: Break timer is null - recreating");
                        InitializeBreakTimer();
                    }

                    // Calculate how long the user was idle/away
                    var idleDuration = savedPauseStartTime != DateTime.MinValue
                        ? DateTime.Now - savedPauseStartTime
                        : TimeSpan.Zero;
                    var eyeRestDurationSeconds = _configuration?.EyeRest?.DurationSeconds ?? 20;
                    var eyeRestIntervalMinutes = _configuration?.EyeRest?.IntervalMinutes ?? 20;

                    _logger.LogInformation($"🧠 SMART RESUME: Idle duration={idleDuration.TotalMinutes:F1}min, EyeRest duration={eyeRestDurationSeconds}s, EyeRest interval={eyeRestIntervalMinutes}min");

                    // If user was idle longer than the eye rest duration (e.g., 20s), they already
                    // rested their eyes naturally — reset eye rest timer to full interval
                    // If idle longer than break duration, reset break timer too
                    var breakDurationMinutes = _configuration?.Break?.DurationMinutes ?? 5;

                    if (idleDuration.TotalSeconds >= eyeRestDurationSeconds)
                    {
                        // User was idle long enough to count as a natural eye rest — reset to full interval
                        var (erInterval, _, _, _, _) = CalculateEyeRestTimerInterval();
                        _eyeRestInterval = erInterval;
                        _eyeRestTimer!.Interval = _eyeRestInterval;
                        _eyeRestStartTime = DateTime.Now;
                        _eyeRestRemainingTime = TimeSpan.Zero;
                        _logger.LogInformation($"🧠 SMART RESUME: User was idle {idleDuration.TotalMinutes:F1}min (>= {eyeRestDurationSeconds}s eye rest) — reset eye rest to full {_eyeRestInterval.TotalMinutes:F1}min");
                    }
                    else if (_eyeRestRemainingTime > TimeSpan.Zero)
                    {
                        _eyeRestInterval = _eyeRestRemainingTime;
                        _eyeRestTimer!.Interval = _eyeRestRemainingTime;
                        _eyeRestStartTime = DateTime.Now;
                        _logger.LogInformation($"🔧 SMART RESUME: Restored eye rest remaining {_eyeRestRemainingTime.TotalMinutes:F1}min");
                    }

                    if (idleDuration.TotalMinutes >= breakDurationMinutes)
                    {
                        // User was idle long enough to count as a natural break — reset to full interval
                        var (brInterval, _, _, _, _) = CalculateBreakTimerInterval();
                        _breakInterval = brInterval;
                        _breakTimer!.Interval = _breakInterval;
                        _breakStartTime = DateTime.Now;
                        _breakTimerStartTime = DateTime.Now;
                        _breakRemainingTime = TimeSpan.Zero;
                        _logger.LogInformation($"🧠 SMART RESUME: User was idle {idleDuration.TotalMinutes:F1}min (>= {breakDurationMinutes}min break) — reset break to full {_breakInterval.TotalMinutes:F1}min");
                    }
                    else if (_breakRemainingTime > TimeSpan.Zero)
                    {
                        _breakInterval = _breakRemainingTime;
                        _breakTimer!.Interval = _breakRemainingTime;
                        _breakStartTime = DateTime.Now;
                        _breakTimerStartTime = DateTime.Now;
                        _logger.LogInformation($"🔧 SMART RESUME: Restored break remaining {_breakRemainingTime.TotalMinutes:F1}min");
                    }
                    else
                    {
                        // No preserved remaining time — just reset start times to now
                        _breakStartTime = DateTime.Now;
                        _breakTimerStartTime = DateTime.Now;
                    }

                    // Always reset start times if they weren't set above (fallback safety)
                    if (_eyeRestStartTime == DateTime.MinValue)
                        _eyeRestStartTime = DateTime.Now;

                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    UpdateHeartbeatFromOperation("SmartResume");

                    _logger.LogInformation($"🧠 Smart resume conditions - Timers started: EyeRest={_eyeRestTimer?.IsEnabled}, Break={_breakTimer?.IsEnabled}");

                    await _analyticsService.RecordResumeEventAsync(ResumeReason.SmartDetection);

                    _logger.LogInformation("Timer service smart resumed successfully");
                }
                else
                {
                    _logger.LogInformation("Timer service smart pause cleared but still manually paused");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error smart resuming timer service");
                throw;
            }
        }

        public async Task SmartSessionResetAsync(string reason)
        {
            try
            {
                // Reload configuration to ensure the latest user settings are applied
                _configuration = await _configurationService.LoadConfigurationAsync();

                _logger.LogInformation($"🔥 SMART SESSION RESET INITIATED - Reason: {reason}");
                _logger.LogInformation($"🔥 Starting fresh {_configuration.EyeRest.IntervalMinutes}min/{_configuration.Break.IntervalMinutes}min cycle");
                
                // Calculate current remaining times for logging
                var eyeRestRemainingBefore = TimeUntilNextEyeRest;
                var breakRemainingBefore = TimeUntilNextBreak;
                
                _logger.LogInformation($"🔥 BEFORE RESET - Eye rest remaining: {eyeRestRemainingBefore}, Break remaining: {breakRemainingBefore}");
                
                // Stop all timers
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                _eyeRestWarningTimer?.Stop();
                _breakWarningTimer?.Stop();
                _eyeRestFallbackTimer?.Stop();
                _breakFallbackTimer?.Stop();

                // CRITICAL FIX: Stop and dispose warning fallback timers to prevent orphaned handlers
                // These timers may be running with captured state from old warning handlers
                _logger.LogInformation("🧹 SESSION RESET: Disposing warning fallback timers to prevent orphaned handlers");
                try
                {
                    if (_eyeRestWarningFallbackTimer != null)
                    {
                        _eyeRestWarningFallbackTimer.Stop();
                        _eyeRestWarningFallbackTimer = null;
                        _logger.LogInformation("🧹 SESSION RESET: Eye rest warning fallback timer disposed");
                    }
                    if (_breakWarningFallbackTimer != null)
                    {
                        _breakWarningFallbackTimer.Stop();
                        _breakWarningFallbackTimer = null;
                        _logger.LogInformation("🧹 SESSION RESET: Break warning fallback timer disposed");
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "🧹 SESSION RESET: Warning when disposing fallback timers (non-critical)");
                }

                // CRITICAL FIX: Force complete any active warning popups to prevent frozen countdowns
                // This prevents orphaned warning handlers from continuing to update stale popup references
                _logger.LogInformation("🧹 SESSION RESET: Forcing completion of any active warning popups");
                try
                {
                    // Get the notification service type to access private warning popup fields
                    var notificationServiceType = _notificationService?.GetType();
                    if (notificationServiceType != null)
                    {
                        // Get reference to active eye rest warning popup
                        var activeEyeRestWarningField = notificationServiceType.GetField(
                            "_activeEyeRestWarningPopup",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        var activeEyeRestWarningPopup = activeEyeRestWarningField?.GetValue(_notificationService);
                        if (activeEyeRestWarningPopup != null)
                        {
                            _logger.LogInformation("🧹 SESSION RESET: Active eye rest warning popup found - forcing completion");
                            try
                            {
                                // Get the WarningCompleted event
                                var warningCompletedEvent = activeEyeRestWarningPopup.GetType()
                                    .GetEvent("WarningCompleted",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (warningCompletedEvent != null)
                                {
                                    // Invoke the event using reflection
                                    var raiseMethod = warningCompletedEvent.GetRaiseMethod(true);
                                    if (raiseMethod != null)
                                    {
                                        raiseMethod.Invoke(activeEyeRestWarningPopup,
                                            new object[] { activeEyeRestWarningPopup, EventArgs.Empty });
                                        _logger.LogInformation("🧹 SESSION RESET: Eye rest warning popup completion event forced");
                                    }
                                }
                            }
                            catch (Exception warnEx)
                            {
                                _logger.LogWarning(warnEx, "🧹 SESSION RESET: Could not force eye rest warning completion (non-critical)");
                            }
                        }

                        // Get reference to active break warning popup
                        var activeBreakWarningField = notificationServiceType.GetField(
                            "_activeBreakWarningPopup",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        var activeBreakWarningPopup = activeBreakWarningField?.GetValue(_notificationService);
                        if (activeBreakWarningPopup != null)
                        {
                            _logger.LogInformation("🧹 SESSION RESET: Active break warning popup found - forcing completion");
                            try
                            {
                                // Get the WarningCompleted event
                                var warningCompletedEvent = activeBreakWarningPopup.GetType()
                                    .GetEvent("WarningCompleted",
                                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (warningCompletedEvent != null)
                                {
                                    // Invoke the event using reflection
                                    var raiseMethod = warningCompletedEvent.GetRaiseMethod(true);
                                    if (raiseMethod != null)
                                    {
                                        raiseMethod.Invoke(activeBreakWarningPopup,
                                            new object[] { activeBreakWarningPopup, EventArgs.Empty });
                                        _logger.LogInformation("🧹 SESSION RESET: Break warning popup completion event forced");
                                    }
                                }
                            }
                            catch (Exception warnEx)
                            {
                                _logger.LogWarning(warnEx, "🧹 SESSION RESET: Could not force break warning completion (non-critical)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🧹 SESSION RESET: Error forcing warning popup completion (non-critical)");
                }

                // CRITICAL FIX: Reset all timer states for fresh session with manual pause cleanup
                IsSmartPaused = false;
                IsPaused = false;
                IsManuallyPaused = false;
                _pauseReason = string.Empty;
                _manualPauseStartTime = DateTime.MinValue;
                _manualPauseDuration = TimeSpan.Zero;
                
                // CRITICAL FIX: Stop and clean up manual pause timer during session reset
                if (_manualPauseTimer != null)
                {
                    _manualPauseTimer.Stop();
                    _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                    _manualPauseTimer = null;
                    _logger.LogInformation("🔧 SESSION RESET: Manual pause timer disposed during smart reset");
                }

                // CRITICAL FIX: Clear all popup windows before resetting to prevent stale popups
                // This prevents race condition where old popups remain after session reset
                try
                {
                    _logger.LogInformation("🧹 SESSION RESET: Clearing all popup windows for fresh session");
                    _notificationService?.HideAllNotifications();

                    // Force clear notification service popup state using reflection
                    var notificationServiceType = _notificationService?.GetType();
                    var activeEyeRestField = notificationServiceType?.GetField("_activeEyeRestWarningPopup",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var activeBreakField = notificationServiceType?.GetField("_activeBreakWarningPopup",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    activeEyeRestField?.SetValue(_notificationService, null);
                    activeBreakField?.SetValue(_notificationService, null);

                    _logger.LogInformation("🧹 SESSION RESET: All popup references cleared successfully");
                }
                catch (Exception popupEx)
                {
                    _logger.LogError(popupEx, "🧹 SESSION RESET: Error clearing popups during session reset");
                }

                // Reset cross-timer coordination state
                _isEyeRestNotificationActive = false;
                _isBreakNotificationActive = false;
                _eyeRestTimerPausedForBreak = false;
                _breakTimerPausedForEyeRest = false;

                // CRITICAL FIX: Clear all event processing flags to prevent stale lock state
                // This prevents "GLOBAL LOCK PREVENTION" from blocking popups after session reset
                ClearEyeRestProcessingFlag();
                ClearBreakProcessingFlag();
                ClearEyeRestWarningProcessingFlag();
                ClearBreakWarningProcessingFlag();
                _logger.LogInformation("🔄 SESSION RESET: Cleared all event processing flags to prevent stale lock state");

                // CRITICAL P0 FIX: Clear break completion state to prevent orphaned completion events
                // This prevents force-closed break popups from triggering smart pause later if their orphaned timer fires
                // Without this, a break popup closed during session reset could still have a running timer that fires
                // 5+ minutes later and triggers "Waiting for break confirmation" smart pause with no visible popup
                try
                {
                    var notificationServiceType = _notificationService?.GetType();
                    var waitingForBreakField = notificationServiceType?.GetField(
                        "_isWaitingForBreakConfirmation",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var completionInProgressField = notificationServiceType?.GetField(
                        "_isBreakCompletionInProgress",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (waitingForBreakField != null)
                    {
                        waitingForBreakField.SetValue(_notificationService, false);
                        _logger.LogInformation("🔄 SESSION RESET: Cleared _isWaitingForBreakConfirmation flag to prevent orphaned completion events");
                    }
                    if (completionInProgressField != null)
                    {
                        completionInProgressField.SetValue(_notificationService, false);
                        _logger.LogInformation("🔄 SESSION RESET: Cleared _isBreakCompletionInProgress flag to prevent orphaned completion events");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔄 SESSION RESET: Error clearing break completion state (non-critical) - this may cause stuck state if orphaned timers fire");
                }

                // CRITICAL P0 FIX: Clear UserPresenceService idle tracking state
                // Prevents stale _idleStartTime from causing incorrect extended idle detection after session reset
                // Without this, a session reset during idle period could later trigger extended away detection
                // when user returns, using a stale idle start time from before the reset
                try
                {
                    var userPresenceServiceType = _userPresenceService?.GetType();
                    var idleStartTimeField = userPresenceServiceType?.GetField(
                        "_idleStartTime",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var hasBeenAwayExtendedField = userPresenceServiceType?.GetField(
                        "_hasBeenAwayExtended",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (idleStartTimeField != null)
                    {
                        idleStartTimeField.SetValue(_userPresenceService, default(DateTime));
                        _logger.LogInformation("🔄 P0 FIX - SESSION RESET: Cleared _idleStartTime tracking to prevent stale idle detection");
                    }
                    if (hasBeenAwayExtendedField != null)
                    {
                        hasBeenAwayExtendedField.SetValue(_userPresenceService, false);
                        _logger.LogInformation("🔄 P0 FIX - SESSION RESET: Reset _hasBeenAwayExtended flag for fresh session");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔄 P0 FIX - SESSION RESET: Error clearing idle tracking state (non-critical) - may cause incorrect extended away detection");
                }

                // Reset timer start times for fresh session
                _eyeRestStartTime = DateTime.Now;
                _breakStartTime = DateTime.Now;
                _breakTimerStartTime = DateTime.Now;
                
                // Clear any remaining time tracking
                _eyeRestRemainingTime = TimeSpan.Zero;
                _breakRemainingTime = TimeSpan.Zero;
                
                // Reset delay status
                IsBreakDelayed = false;
                
                // CRITICAL: Clear clock jump detection timestamps for fresh session
                _lastEyeRestTick = DateTime.MinValue;
                _lastBreakTick = DateTime.MinValue;
                _lastSystemCheck = DateTime.Now;
                _logger.LogInformation("🔥 CLOCK JUMP DETECTION: Timestamps cleared for fresh session");
                
                // Set timers to full intervals using shared calculation (respects WarningEnabled)
                var (eyeRestInterval, _, _, _, _) = CalculateEyeRestTimerInterval();
                var (breakInterval, _, _, _, _) = CalculateBreakTimerInterval();
                _eyeRestInterval = eyeRestInterval;
                _breakInterval = breakInterval;
                
                // CRITICAL FIX: Ensure timers exist and are properly initialized before starting
                if (_eyeRestTimer == null)
                {
                    _logger.LogWarning("🔥 SMART SESSION RESET FIX: Eye rest timer is null - creating new timer");
                    InitializeEyeRestTimer();
                }
                
                if (_breakTimer == null)
                {
                    _logger.LogWarning("🔥 SMART SESSION RESET FIX: Break timer is null - creating new timer");
                    InitializeBreakTimer();
                }
                
                // Set intervals and start timers
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Interval = _eyeRestInterval;
                    _eyeRestTimer.Start();
                }
                else
                {
                    _logger.LogError("🔥 Eye rest timer is still null after initialization attempt!");
                }
                
                if (_breakTimer != null)
                {
                    _breakTimer.Interval = _breakInterval;
                    _breakTimer.Start();
                }
                else
                {
                    _logger.LogError("🔥 Break timer is still null after initialization attempt!");
                }
                
                // Calculate new remaining times for logging
                var eyeRestRemainingAfter = TimeUntilNextEyeRest;
                var breakRemainingAfter = TimeUntilNextBreak;
                
                _logger.LogInformation($"🔥 AFTER RESET - Eye rest remaining: {eyeRestRemainingAfter}, Break remaining: {breakRemainingAfter}");
                _logger.LogInformation($"🔥 TIMER CONFIG - Eye rest: {_configuration.EyeRest.IntervalMinutes}min, Break: {_configuration.Break.IntervalMinutes}min, Break warning: {_configuration.Break.WarningSeconds}s");
                _logger.LogInformation($"🔥 TIMER INTERVALS - Break timer actual interval: {_breakTimer?.Interval.TotalMinutes:F1}min, Break display interval: {_breakInterval.TotalMinutes:F1}min");
                
                // Record analytics event
                await _analyticsService.RecordResumeEventAsync(ResumeReason.NewWorkingSession);
                
                _logger.LogInformation($"✅ SMART SESSION RESET COMPLETED - fresh {_configuration.EyeRest.IntervalMinutes}min/{_configuration.Break.IntervalMinutes}min cycle started");
                
                // Notify property changes for UI updates
                OnPropertyChanged(nameof(TimeUntilNextEyeRest));
                OnPropertyChanged(nameof(TimeUntilNextBreak));
                OnPropertyChanged(nameof(NextEventDescription));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing smart session reset");
                throw;
            }
        }

        public async Task PauseForDurationAsync(TimeSpan duration, string reason)
        {
            if (!IsRunning)
            {
                _logger.LogWarning("Cannot pause for duration - timer service is not running");
                return;
            }

            try
            {
                _logger.LogInformation("⏸️ Pausing timer service for {Minutes} minutes - reason: {Reason}", 
                    duration.TotalMinutes, reason);
                
                // Preserve remaining time BEFORE setting pause flag
                // (TimeUntilNext* getters short-circuit when paused)
                if (_eyeRestTimer?.IsEnabled == true && _eyeRestStartTime != DateTime.MinValue)
                {
                    var eyeElapsed = DateTime.Now - _eyeRestStartTime;
                    var eyeRemaining = _eyeRestInterval - eyeElapsed;
                    _eyeRestRemainingTime = eyeRemaining > TimeSpan.Zero ? eyeRemaining : _eyeRestInterval;
                }
                if (_breakTimer?.IsEnabled == true && _breakStartTime != DateTime.MinValue)
                {
                    var brElapsed = DateTime.Now - _breakStartTime;
                    var brRemaining = _breakInterval - brElapsed;
                    _breakRemainingTime = brRemaining > TimeSpan.Zero ? brRemaining : _breakInterval;
                }

                // Set manual pause state
                IsManuallyPaused = true;
                _manualPauseStartTime = DateTime.Now;
                _manualPauseDuration = duration;
                _pauseReason = reason;

                // Stop timers
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                
                // Create timer for auto-resume
                _manualPauseTimer = _timerFactory.CreateTimer();
                _manualPauseTimer.Interval = duration;
                
                _manualPauseTimer.Tick += OnManualPauseTimerTick;
                _manualPauseTimer.Start();
                
                await _analyticsService.RecordPauseEventAsync(EyeRest.Services.PauseReason.Manual);
                
                _logger.LogInformation("Timer service paused for {Minutes} minutes successfully", duration.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing timer service for duration");
                IsManuallyPaused = false;
                throw;
            }
        }

        private async void OnManualPauseTimerTick(object? sender, EventArgs e)
        {
            try
            {
                // BUGFIX: Save pause duration BEFORE clearing state (was reading zero after clear)
                var pauseDuration = _manualPauseDuration;
                _logger.LogInformation("⏰ Manual pause duration expired ({PauseMins:F0}min) - auto-resuming", pauseDuration.TotalMinutes);

                // Stop the timer
                _manualPauseTimer?.Stop();
                _manualPauseTimer = null;

                // Clear manual pause state
                IsManuallyPaused = false;
                _manualPauseStartTime = DateTime.MinValue;
                _manualPauseDuration = TimeSpan.Zero;
                _pauseReason = string.Empty;

                // Resume if not otherwise paused
                if (IsRunning && !IsPaused && !IsSmartPaused)
                {
                    var eyeRestDurationSeconds = _configuration?.EyeRest?.DurationSeconds ?? 20;
                    var breakDurationMinutes = _configuration?.Break?.DurationMinutes ?? 5;

                    // If pause >= eye rest duration, user already rested — reset to full interval
                    if (pauseDuration.TotalSeconds >= eyeRestDurationSeconds)
                    {
                        var (erInterval, _, _, _, _) = CalculateEyeRestTimerInterval();
                        _eyeRestInterval = erInterval;
                        _eyeRestTimer!.Interval = _eyeRestInterval;
                        _eyeRestRemainingTime = TimeSpan.Zero;
                        _logger.LogInformation("⏰ Meeting pause >= eye rest duration — reset eye rest to full {Interval:F1}min", _eyeRestInterval.TotalMinutes);
                    }
                    else if (_eyeRestRemainingTime > TimeSpan.Zero)
                    {
                        _eyeRestTimer!.Interval = _eyeRestRemainingTime;
                        _logger.LogInformation("⏰ Restoring eye rest remaining: {Remaining:F1}min", _eyeRestRemainingTime.TotalMinutes);
                    }

                    // If pause >= break duration, user already took a break — reset to full interval
                    if (pauseDuration.TotalMinutes >= breakDurationMinutes)
                    {
                        var (brInterval, _, _, _, _) = CalculateBreakTimerInterval();
                        _breakInterval = brInterval;
                        _breakTimer!.Interval = _breakInterval;
                        _breakRemainingTime = TimeSpan.Zero;
                        _logger.LogInformation("⏰ Meeting pause >= break duration — reset break to full {Interval:F1}min", _breakInterval.TotalMinutes);
                    }
                    else if (_breakRemainingTime > TimeSpan.Zero)
                    {
                        _breakTimer!.Interval = _breakRemainingTime;
                        _logger.LogInformation("⏰ Restoring break remaining: {Remaining:F1}min", _breakRemainingTime.TotalMinutes);
                    }

                    // Reset start times and start timers
                    _eyeRestStartTime = DateTime.Now;
                    _breakStartTime = DateTime.Now;
                    _breakTimerStartTime = DateTime.Now;
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    UpdateHeartbeatFromOperation("ManualPauseAutoResume");

                    await _analyticsService.RecordResumeEventAsync(ResumeReason.AutoResumeAfterDuration);

                    _logger.LogInformation("Timer service auto-resumed after manual pause duration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual pause timer tick");
            }
        }
    }
}