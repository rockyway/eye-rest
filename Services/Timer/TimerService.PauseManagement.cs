using System;
using System.Threading.Tasks;
using System.Windows.Threading;
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
                
                // Clear manual pause if active
                if (IsManuallyPaused)
                {
                    IsManuallyPaused = false;
                    _manualPauseStartTime = DateTime.MinValue;
                    _manualPauseDuration = TimeSpan.Zero;
                    
                    // Stop and clean up manual pause timer
                    if (_manualPauseTimer != null)
                    {
                        _manualPauseTimer.Stop();
                        _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                        _manualPauseTimer = null;
                    }
                }
                
                IsPaused = false;
                _pauseStartTime = DateTime.MinValue; // Clear pause start time
                
                if (!IsSmartPaused)
                {
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    _breakTimerStartTime = DateTime.Now; // Track when break timer started
                    
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
                
                IsSmartPaused = true;
                _pauseStartTime = DateTime.Now; // Track when pause started for extended away detection
                
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
                _logger.LogInformation("🧠 Smart resuming timer service");
                
                // CRITICAL FIX: Check if any notifications are active and handle them properly
                if (_isEyeRestNotificationActive || _isBreakNotificationActive)
                {
                    _logger.LogWarning("🧠 Cannot smart resume - notification is active. Clearing notification states.");
                    _isEyeRestNotificationActive = false;
                    _isBreakNotificationActive = false;
                }
                
                IsSmartPaused = false;
                _pauseStartTime = DateTime.MinValue; // Clear pause start time
                
                if (!IsPaused)
                {
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    _breakTimerStartTime = DateTime.Now; // Track when break timer started
                    
                    _logger.LogCritical($"🧠 Smart resume conditions - Timers started");
                    
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
                _logger.LogCritical($"🔥 SMART SESSION RESET INITIATED - Reason: {reason}");
                _logger.LogCritical($"🔥 Starting fresh {_configuration.EyeRest.IntervalMinutes}min/{_configuration.Break.IntervalMinutes}min cycle");
                
                // Calculate current remaining times for logging
                var eyeRestRemainingBefore = TimeUntilNextEyeRest;
                var breakRemainingBefore = TimeUntilNextBreak;
                
                _logger.LogCritical($"🔥 BEFORE RESET - Eye rest remaining: {eyeRestRemainingBefore}, Break remaining: {breakRemainingBefore}");
                
                // Stop all timers
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                _eyeRestWarningTimer?.Stop();
                _breakWarningTimer?.Stop();
                _eyeRestFallbackTimer?.Stop();
                _breakFallbackTimer?.Stop();
                
                // Reset all timer states for fresh session
                IsSmartPaused = false;
                IsPaused = false;
                IsManuallyPaused = false;
                _pauseReason = string.Empty;
                
                // Reset cross-timer coordination state
                _isEyeRestNotificationActive = false;
                _isBreakNotificationActive = false;
                _eyeRestTimerPausedForBreak = false;
                _breakTimerPausedForEyeRest = false;
                
                // Reset timer start times for fresh session
                _eyeRestStartTime = DateTime.Now;
                _breakStartTime = DateTime.Now;
                _breakTimerStartTime = DateTime.Now;
                
                // Clear any remaining time tracking
                _eyeRestRemainingTime = TimeSpan.Zero;
                _breakRemainingTime = TimeSpan.Zero;
                
                // Reset delay status
                IsBreakDelayed = false;
                
                // Set timers to full intervals
                _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes) - 
                                 TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes) - 
                               TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                
                _eyeRestTimer.Interval = _eyeRestInterval;
                _breakTimer.Interval = _breakInterval;
                
                // Start timers for fresh session
                _eyeRestTimer.Start();
                _breakTimer.Start();
                
                // Calculate new remaining times for logging
                var eyeRestRemainingAfter = TimeUntilNextEyeRest;
                var breakRemainingAfter = TimeUntilNextBreak;
                
                _logger.LogCritical($"🔥 AFTER RESET - Eye rest remaining: {eyeRestRemainingAfter}, Break remaining: {breakRemainingAfter}");
                _logger.LogCritical($"🔥 TIMER CONFIG - Eye rest: {_configuration.EyeRest.IntervalMinutes}min, Break: {_configuration.Break.IntervalMinutes}min, Break warning: {_configuration.Break.WarningSeconds}s");
                _logger.LogCritical($"🔥 TIMER INTERVALS - Break timer actual interval: {_breakTimer?.Interval.TotalMinutes:F1}min, Break display interval: {_breakInterval.TotalMinutes:F1}min");
                
                // Record analytics event
                await _analyticsService.RecordResumeEventAsync(ResumeReason.NewWorkingSession);
                
                _logger.LogCritical($"✅ SMART SESSION RESET COMPLETED - fresh {_configuration.EyeRest.IntervalMinutes}min/{_configuration.Break.IntervalMinutes}min cycle started");
                
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
                _logger.LogInformation("⏰ Manual pause duration expired - auto-resuming");
                
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
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                    _breakTimerStartTime = DateTime.Now;
                    
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