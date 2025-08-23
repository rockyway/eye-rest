using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all timer event handlers and event triggering logic
    /// </summary>
    public partial class TimerService
    {
        #region Timer Event Handlers

        private void OnEyeRestTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("👁️ Eye rest timer tick - triggering eye rest warning");
                UpdateHeartbeat();
                
                _eyeRestTimer?.Stop();
                StartEyeRestWarningTimerInternal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in eye rest timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _eyeRestTimer?.Stop();
                    _eyeRestTimer?.Start();
                    _logger.LogInformation("Eye rest timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "Failed to recover eye rest timer");
                }
            }
        }

        private void OnBreakTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("☕ Break timer tick - triggering break warning");
                UpdateHeartbeat();
                
                _breakTimer?.Stop();
                StartBreakWarningTimerInternal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in break timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _breakTimer?.Stop();
                    _breakTimer?.Start();
                    _logger.LogInformation("Break timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "Failed to recover break timer");
                }
            }
        }

        #endregion

        #region Event Triggering Methods

        private void TriggerEyeRestWarning(TimeSpan warningDuration)
        {
            try
            {
                _logger.LogInformation("⚠️ Triggering eye rest warning - {Seconds} seconds remaining", 
                    warningDuration.TotalSeconds);
                
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = warningDuration,
                    Type = TimerType.EyeRestWarning
                };
                
                EyeRestWarning?.Invoke(this, eventArgs);
                
                _ = _analyticsService.RecordEyeRestEventAsync(RestEventType.EyeRest, UserAction.Completed, warningDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering eye rest warning");
            }
        }

        private void TriggerEyeRest()
        {
            try
            {
                _logger.LogInformation("👁️ Triggering eye rest");
                
                // Set notification active state
                _isEyeRestNotificationActive = true;
                
                // Stop warning timer
                _eyeRestWarningTimer?.Stop();
                
                var duration = TimeSpan.FromSeconds(_configuration.EyeRest.DurationSeconds);
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = duration,
                    Type = TimerType.EyeRest
                };
                
                EyeRestDue?.Invoke(this, eventArgs);
                
                _ = _analyticsService.RecordEyeRestEventAsync(RestEventType.EyeRest, UserAction.Completed, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering eye rest");
                _isEyeRestNotificationActive = false;
            }
        }

        private void TriggerBreakWarning(TimeSpan warningDuration)
        {
            try
            {
                _logger.LogInformation("⚠️ Triggering break warning - {Seconds} seconds remaining", 
                    warningDuration.TotalSeconds);
                
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = warningDuration,
                    Type = TimerType.BreakWarning
                };
                
                BreakWarning?.Invoke(this, eventArgs);
                
                _ = _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, warningDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering break warning");
            }
        }

        private void TriggerBreak()
        {
            try
            {
                _logger.LogInformation("☕ Triggering break");
                
                // Set notification active state
                _isBreakNotificationActive = true;
                
                // Stop warning timer
                _breakWarningTimer?.Stop();
                
                var duration = TimeSpan.FromMinutes(_configuration.Break.DurationMinutes);
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = duration,
                    Type = TimerType.Break
                };
                
                BreakDue?.Invoke(this, eventArgs);
                
                _ = _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering break");
                _isBreakNotificationActive = false;
            }
        }

        #endregion

        #region Warning Timer Methods

        public void StartEyeRestWarningTimer()
        {
            _ = Task.Run(() => StartEyeRestWarningTimerInternal());
        }

        public void StartBreakWarningTimer()
        {
            _ = Task.Run(() => StartBreakWarningTimerInternal());
        }

        private void StartEyeRestWarningTimerInternal()
        {
            if (_eyeRestWarningTimer != null && _notificationService != null)
            {
                // CRITICAL FIX: Recreate timer to ensure clean state
                InitializeEyeRestWarningTimer();
                
                var warningDuration = TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                _eyeRestWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler warningTickHandler = (sender, e) =>
                {
                    if (hasTriggered) return; // Prevent multiple executions
                    
                    var elapsed = DateTime.Now - startTime;
                    var remaining = warningDuration - elapsed;
                    
                    if (remaining <= TimeSpan.Zero)
                    {
                        hasTriggered = true; // Mark as triggered
                        
                        // Warning period complete - stop timer and trigger eye rest
                        _eyeRestWarningTimer.Stop();
                        
                        _logger.LogInformation("⏰ Eye rest warning period complete - triggering eye rest NOW");
                        TriggerEyeRest();
                    }
                    else
                    {
                        // Update notification service with remaining time
                        _notificationService.UpdateEyeRestWarningCountdown(remaining);
                    }
                };
                
                // Clear any existing handlers and add the new one
                _eyeRestWarningTimer.Tick += warningTickHandler;
                
                // Initial trigger of warning event
                TriggerEyeRestWarning(warningDuration);
                
                // Start the countdown timer
                _eyeRestWarningTimer.Start();
                
                _logger.LogInformation("Eye rest warning timer started - {Seconds}s countdown", warningDuration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Cannot start eye rest warning timer - timer or notification service is null");
            }
        }

        private void StartBreakWarningTimerInternal()
        {
            if (_breakWarningTimer != null && _notificationService != null)
            {
                // CRITICAL FIX: Recreate timer to ensure clean state
                InitializeBreakWarningTimer();
                
                var warningDuration = TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                _breakWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler warningTickHandler = (sender, e) =>
                {
                    if (hasTriggered) return; // Prevent multiple executions
                    
                    var elapsed = DateTime.Now - startTime;
                    var remaining = warningDuration - elapsed;
                    
                    if (remaining <= TimeSpan.Zero)
                    {
                        hasTriggered = true; // Mark as triggered
                        
                        // Warning period complete - stop timer and trigger break
                        _breakWarningTimer.Stop();
                        
                        _logger.LogInformation("⏰ Break warning period complete - triggering break NOW");
                        TriggerBreak();
                    }
                    else
                    {
                        // Update notification service with remaining time
                        _notificationService.UpdateBreakWarningCountdown(remaining);
                    }
                };
                
                // Clear any existing handlers and add the new one
                _breakWarningTimer.Tick += warningTickHandler;
                
                // Initial trigger of warning event
                TriggerBreakWarning(warningDuration);
                
                // Start the countdown timer
                _breakWarningTimer.Start();
                
                _logger.LogInformation("Break warning timer started - {Seconds}s countdown", warningDuration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning("Cannot start break warning timer - timer or notification service is null");
            }
        }

        #endregion
    }
}