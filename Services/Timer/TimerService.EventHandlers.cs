using System;
using System.Threading.Tasks;
using System.Windows;
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
                _logger.LogCritical($"👁️ TIMER EVENT: Eye rest timer tick fired at {DateTime.Now:HH:mm:ss.fff}");
                
                // CRITICAL FIX: Validate timer state before processing
                if (!IsRunning || IsPaused || IsManuallyPaused)
                {
                    _logger.LogInformation($"👁️ TIMER EVENT: Skipping eye rest tick - not running or paused (Running={IsRunning}, Paused={IsPaused}, ManuallyPaused={IsManuallyPaused})");
                    return;
                }
                
                // CRITICAL FIX: Ensure we're on UI thread for all timer operations
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() != true)
                {
                    _logger.LogCritical("👁️ TIMER EVENT: Not on UI thread - invoking on UI thread");
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => OnEyeRestTimerTick(sender, e)));
                    return;
                }
                
                UpdateHeartbeatFromOperation("OnEyeRestTimerTick");
                
                // CRITICAL FIX: Verify timer timing is correct
                var elapsed = DateTime.Now - _eyeRestStartTime;
                var expectedInterval = _eyeRestInterval;
                _logger.LogCritical("👁️ TIMER VERIFICATION: Timer elapsed={ElapsedMinutes:F2}m, expected={ExpectedMinutes:F2}m", 
                    elapsed.TotalMinutes, expectedInterval.TotalMinutes);
                
                // Log if timer fired significantly early or late
                if (Math.Abs((elapsed - expectedInterval).TotalSeconds) > 5)
                {
                    _logger.LogWarning("⚠️ TIMER ACCURACY: Timer fired {TimingDiff:F1}s {Direction} expected time", 
                        Math.Abs((elapsed - expectedInterval).TotalSeconds),
                        elapsed < expectedInterval ? "before" : "after");
                }
                
                _logger.LogCritical("👁️ TIMER EVENT: Stopping eye rest timer and starting warning timer");
                _eyeRestTimer?.Stop();
                
                // Use public method to ensure proper thread safety
                StartEyeRestWarningTimer();
                
                _logger.LogCritical("👁️ TIMER EVENT: Eye rest timer tick completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "👁️ TIMER EVENT: Error in eye rest timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _eyeRestTimer?.Stop();
                    _eyeRestTimer?.Start();
                    _logger.LogInformation("👁️ TIMER EVENT: Eye rest timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "👁️ TIMER EVENT: Failed to recover eye rest timer");
                }
            }
        }

        private void OnBreakTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogCritical($"☕ TIMER EVENT: Break timer tick fired at {DateTime.Now:HH:mm:ss.fff}");
                
                // CRITICAL FIX: Validate timer state before processing
                if (!IsRunning || IsPaused || IsManuallyPaused)
                {
                    _logger.LogInformation($"☕ TIMER EVENT: Skipping break tick - not running or paused (Running={IsRunning}, Paused={IsPaused}, ManuallyPaused={IsManuallyPaused})");
                    return;
                }
                
                // CRITICAL FIX: Ensure we're on UI thread for all timer operations
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() != true)
                {
                    _logger.LogCritical("☕ TIMER EVENT: Not on UI thread - invoking on UI thread");
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => OnBreakTimerTick(sender, e)));
                    return;
                }
                
                UpdateHeartbeatFromOperation("OnBreakTimerTick");
                
                _logger.LogCritical("☕ TIMER EVENT: Stopping break timer and starting warning timer");
                _breakTimer?.Stop();
                
                // Use public method to ensure proper thread safety
                StartBreakWarningTimer();
                
                _logger.LogCritical("☕ TIMER EVENT: Break timer tick completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "☕ TIMER EVENT: Error in break timer tick - attempting recovery");
                
                // Attempt to recover by restarting the timer
                try
                {
                    _breakTimer?.Stop();
                    _breakTimer?.Start();
                    _logger.LogInformation("☕ TIMER EVENT: Break timer recovered successfully");
                }
                catch (Exception recoveryEx)
                {
                    _logger.LogError(recoveryEx, "☕ TIMER EVENT: Failed to recover break timer");
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
                _logger.LogCritical("👁️ TRIGGER EYE REST: Starting popup at {Time}", DateTime.Now.ToString("HH:mm:ss.fff"));
                
                // CRITICAL FIX: Verify notification service is available
                if (_notificationService == null)
                {
                    _logger.LogError("👁️ TRIGGER ERROR: NotificationService is null - cannot show popup!");
                    return;
                }
                
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
                
                // CRITICAL FIX: Log before and after event trigger
                _logger.LogCritical("👁️ TRIGGER EYE REST: Invoking EyeRestDue event to show popup");
                EyeRestDue?.Invoke(this, eventArgs);
                _logger.LogCritical("👁️ TRIGGER EYE REST: EyeRestDue event invoked successfully");
                
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
            // CRITICAL FIX: DispatcherTimer must be created/manipulated on UI thread
            // Remove Task.Run() that was causing thread safety violations and infinite loops
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                // Already on UI thread
                StartEyeRestWarningTimerInternal();
            }
            else
            {
                // Invoke on UI thread
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => 
                {
                    StartEyeRestWarningTimerInternal();
                }));
            }
        }

        public void StartBreakWarningTimer()
        {
            // CRITICAL FIX: DispatcherTimer must be created/manipulated on UI thread
            // Remove Task.Run() that was causing thread safety violations and infinite loops
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                // Already on UI thread
                StartBreakWarningTimerInternal();
            }
            else
            {
                // Invoke on UI thread
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => 
                {
                    StartBreakWarningTimerInternal();
                }));
            }
        }

        private void StartEyeRestWarningTimerInternal()
        {
            // CRITICAL FIX: Prevent duplicate warning timer starts that cause infinite loops and UI desync
            if (_eyeRestWarningTimer?.IsEnabled == true)
            {
                _logger.LogWarning("⚠️ Eye rest warning timer already running - skipping duplicate start to prevent infinite loop and UI countdown reset");
                return;
            }
            
            // Additional safety check: prevent multiple active notification states
            if (_isEyeRestNotificationActive)
            {
                _logger.LogWarning("⚠️ Eye rest notification already active - preventing duplicate timer start");
                return;
            }
            
            if (_eyeRestWarningTimer != null && _notificationService != null)
            {
                // CRITICAL FIX: Recreate timer to ensure clean state
                InitializeEyeRestWarningTimer();
                
                var warningDuration = TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                _eyeRestWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
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
            // CRITICAL FIX: Prevent duplicate warning timer starts that cause infinite loops and UI desync
            if (_breakWarningTimer?.IsEnabled == true)
            {
                _logger.LogWarning("⚠️ Break warning timer already running - skipping duplicate start to prevent infinite loop and UI countdown reset");
                return;
            }
            
            // Additional safety check: prevent multiple active notification states
            if (_isBreakNotificationActive)
            {
                _logger.LogWarning("⚠️ Break notification already active - preventing duplicate timer start");
                return;
            }
            
            if (_breakWarningTimer != null && _notificationService != null)
            {
                // CRITICAL FIX: Recreate timer to ensure clean state
                InitializeBreakWarningTimer();
                
                var warningDuration = TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                var startTime = DateTime.Now;
                var hasTriggered = false; // Prevent multiple triggers
                
                _breakWarningTimer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100ms for smooth countdown
                
                // Create a new event handler to avoid accumulating handlers
                EventHandler<EventArgs> warningTickHandler = (sender, e) =>
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