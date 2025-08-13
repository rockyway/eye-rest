using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class TimerService : ITimerService, IDisposable
    {
        private readonly ILogger<TimerService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IAnalyticsService _analyticsService;
        private INotificationService? _notificationService; // Injected later to avoid circular dependency
        
        private DispatcherTimer? _eyeRestTimer;
        private DispatcherTimer? _eyeRestWarningTimer;
        private DispatcherTimer? _breakTimer;
        private DispatcherTimer? _breakWarningTimer;
        
        // ENHANCED: Fallback timers to prevent stuck state
        private DispatcherTimer? _eyeRestFallbackTimer;
        private DispatcherTimer? _breakFallbackTimer;
        
        private AppConfiguration _configuration;
        private bool _isStarted;
        private bool _isPaused;
        private bool _isSmartPaused;
        private string _smartPauseReason = string.Empty;
        private DateTime _eyeRestStartTime;
        private DateTime _breakStartTime;
        private TimeSpan _eyeRestInterval;
        private TimeSpan _breakInterval;
        private bool _isBreakDelayed;
        private DateTime _delayStartTime;
        private TimeSpan _delayDuration;
        
        // State preservation for pause/resume
        private TimeSpan _eyeRestRemainingTime;
        private TimeSpan _breakRemainingTime;
        private DateTime _pauseStartTime;

        public event EventHandler<TimerEventArgs>? EyeRestWarning;
        public event EventHandler<TimerEventArgs>? EyeRestDue;
        public event EventHandler<TimerEventArgs>? BreakWarning;
        public event EventHandler<TimerEventArgs>? BreakDue;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public bool IsRunning 
        { 
            get => _isStarted; 
            private set 
            { 
                if (_isStarted != value)
                {
                    _isStarted = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsRunning)));
                }
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPaused)));
                }
            }
        }

        public bool IsSmartPaused
        {
            get => _isSmartPaused;
            private set
            {
                if (_isSmartPaused != value)
                {
                    _isSmartPaused = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSmartPaused)));
                }
            }
        }

        public TimeSpan TimeUntilNextEyeRest
        {
            get
            {
                if (!IsRunning) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _eyeRestStartTime;
                var remaining = _eyeRestInterval - elapsed;
                // CRITICAL FIX: Auto-reset timer when it becomes due to prevent stuck state
                if (remaining <= TimeSpan.Zero)
                {
                    // Timer is due - check if we need to auto-restart it
                    if (remaining.TotalSeconds < -30) // If timer has been due for more than 30 seconds
                    {
                        _logger.LogWarning($"👁 Eye rest timer was overdue by {Math.Abs(remaining.TotalSeconds):F1}s - auto-restarting");
                        _eyeRestStartTime = DateTime.Now; // Reset start time
                        return _eyeRestInterval; // Return full interval
                    }
                    return TimeSpan.FromSeconds(1); // Show "1s" instead of "0s" when eye rest is due
                }
                return remaining;
            }
        }

        public TimeSpan TimeUntilNextBreak
        {
            get
            {
                if (!IsRunning) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _breakStartTime;
                var remaining = _breakInterval - elapsed;
                // CRITICAL FIX: Auto-reset timer when it becomes due to prevent stuck state
                if (remaining <= TimeSpan.Zero)
                {
                    // Timer is due - check if we need to auto-restart it
                    if (remaining.TotalSeconds < -30) // If timer has been due for more than 30 seconds
                    {
                        _logger.LogWarning($"🛑 Break timer was overdue by {Math.Abs(remaining.TotalSeconds):F1}s - auto-restarting");
                        _breakStartTime = DateTime.Now; // Reset start time
                        return _breakInterval; // Return full interval
                    }
                    return TimeSpan.FromSeconds(1); // Show "1s" instead of "0s" when break is due
                }
                return remaining;
            }
        }

        public string NextEventDescription
        {
            get
            {
                if (!IsRunning) return "Timers not running";
                
                var eyeRestTime = TimeUntilNextEyeRest;
                var breakTime = TimeUntilNextBreak;
                
                if (eyeRestTime <= breakTime)
                {
                    return $"Next eye rest: {eyeRestTime:mm\\:ss}";
                }
                else
                {
                    return $"Next break: {breakTime:mm\\:ss}";
                }
            }
        }

        public bool IsBreakDelayed
        {
            get => _isBreakDelayed;
            private set
            {
                if (_isBreakDelayed != value)
                {
                    _isBreakDelayed = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsBreakDelayed)));
                }
            }
        }

        public TimeSpan DelayRemaining
        {
            get
            {
                if (!_isBreakDelayed) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _delayStartTime;
                var remaining = _delayDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public TimerService(ILogger<TimerService> logger, IConfigurationService configurationService, IAnalyticsService analyticsService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _analyticsService = analyticsService;
            _configuration = new AppConfiguration(); // Will be loaded on start
            
            // Subscribe to configuration changes
            _configurationService.ConfigurationChanged += OnConfigurationChanged;
        }

        // Inject NotificationService after construction to avoid circular dependency
        public void SetNotificationService(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Public methods for NotificationService to start countdown timers after popup creation
        public void StartEyeRestWarningTimer()
        {
            StartEyeRestWarningTimerInternal();
        }

        public void StartBreakWarningTimer()
        {
            StartBreakWarningTimerInternal();
        }

        public async Task StartAsync()
        {
            if (_isStarted)
            {
                _logger.LogWarning("Timer service is already started");
                return;
            }

            try
            {
                // Load current configuration
                _configuration = await _configurationService.LoadConfigurationAsync();
                
                // VALIDATION: Ensure popup timing matches configuration
                ValidateTimerConfiguration();
                
                _logger.LogInformation($"🔄 Starting with validated configuration: EyeRest={_configuration.EyeRest.IntervalMinutes}min ({_configuration.EyeRest.DurationSeconds}s duration), Break={_configuration.Break.IntervalMinutes}min ({_configuration.Break.DurationMinutes}min duration)");
                
                // Set intervals and start times
                _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes);
                _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes);
                _eyeRestStartTime = DateTime.Now;
                _breakStartTime = DateTime.Now;
                
                // Initialize timers
                InitializeEyeRestTimer();
                InitializeEyeRestWarningTimer();
                InitializeBreakTimer();
                InitializeBreakWarningTimer();
                
                // Start timers
                _eyeRestTimer?.Start();
                _breakTimer?.Start();
                
                IsRunning = true;
                _logger.LogInformation($"✅ Timer service started successfully with validated configuration");
                _logger.LogInformation($"📊 Eye rest timer: IsEnabled={_eyeRestTimer?.IsEnabled}, Interval={_eyeRestTimer?.Interval}");
                _logger.LogInformation($"📊 Break timer: IsEnabled={_breakTimer?.IsEnabled}, Interval={_breakTimer?.Interval}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start timer service");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isStarted)
            {
                _logger.LogWarning("Timer service is not started");
                return;
            }

            try
            {
                // CRITICAL FIX: Properly cleanup timers and unsubscribe event handlers
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    _eyeRestTimer.Tick -= OnEyeRestTimerTick; // CRITICAL: Prevent memory leak
                }
                
                if (_eyeRestWarningTimer != null)
                {
                    _eyeRestWarningTimer.Stop();
                    // Timer handlers are managed inline now
                }
                
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    _breakTimer.Tick -= OnBreakTimerTick; // CRITICAL: Prevent memory leak
                }
                
                if (_breakWarningTimer != null)
                {
                    _breakWarningTimer.Stop();
                    // Timer handlers are managed inline now
                }
                
                IsRunning = false;
                _logger.LogInformation("✅ Timer service stopped successfully - all event handlers unsubscribed");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping timer service");
                throw;
            }
        }

        public async Task ResetEyeRestTimer()
        {
            try
            {
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    
                    // Update the display interval for countdown calculations
                    _eyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes);
                    
                    // Calculate the timer interval MINUS warning time (same logic as InitializeEyeRestTimer)
                    var intervalMinutes = _configuration.EyeRest.IntervalMinutes;
                    var warningSeconds = _configuration.EyeRest.WarningEnabled ? _configuration.EyeRest.WarningSeconds : 0;
                    var timerInterval = TimeSpan.FromMinutes(intervalMinutes) - TimeSpan.FromSeconds(warningSeconds);
                    
                    _eyeRestTimer.Interval = timerInterval;
                    _eyeRestStartTime = DateTime.Now;
                    
                    if (IsRunning)
                    {
                        _eyeRestTimer.Start();
                    }
                    
                    _logger.LogInformation($"Eye rest timer reset - display interval: {intervalMinutes}min, actual timer interval: {timerInterval.TotalSeconds}s (warning at {warningSeconds}s before target)");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting eye rest timer");
                throw;
            }
        }

        public async Task ResetBreakTimer()
        {
            try
            {
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    
                    // Update the display interval for countdown calculations
                    _breakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes);
                    
                    // Calculate the timer interval MINUS warning time (same logic as InitializeBreakTimer)
                    var intervalMinutes = _configuration.Break.IntervalMinutes;
                    var warningSeconds = _configuration.Break.WarningEnabled ? _configuration.Break.WarningSeconds : 0;
                    var timerInterval = TimeSpan.FromMinutes(intervalMinutes) - TimeSpan.FromSeconds(warningSeconds);
                    
                    _breakTimer.Interval = timerInterval;
                    _breakStartTime = DateTime.Now;
                    
                    if (IsRunning)
                    {
                        _breakTimer.Start();
                    }
                    
                    _logger.LogInformation($"Break timer reset - display interval: {intervalMinutes}min, actual timer interval: {timerInterval.TotalMinutes:F1}min (warning at {warningSeconds}s before target)");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting break timer");
                throw;
            }
        }

        public async Task DelayBreak(TimeSpan delay)
        {
            try
            {
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    _breakTimer.Interval = delay;
                    if (IsRunning)
                    {
                        _breakTimer.Start();
                    }
                }
                
                // Track delay status for UI indicators
                IsBreakDelayed = true;
                _delayStartTime = DateTime.Now;
                _delayDuration = delay;
                
                _logger.LogInformation($"Break delayed by {delay.TotalMinutes} minutes");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delaying break");
                throw;
            }
        }

        public async Task RestartEyeRestTimerAfterCompletion()
        {
            try
            {
                _logger.LogInformation("👁 Restarting eye rest timer after popup completion");
                
                // ENHANCED: Stop fallback timer since popup completed successfully
                _eyeRestFallbackTimer?.Stop();
                
                // Reset the eye rest start time for next cycle
                _eyeRestStartTime = DateTime.Now;
                
                // Reinitialize timer with full interval
                InitializeEyeRestTimer();
                
                if (IsRunning && _eyeRestTimer != null)
                {
                    _eyeRestTimer.Start();
                    _logger.LogInformation($"👁 Eye rest timer restarted - next eye rest in {_configuration.EyeRest.IntervalMinutes} minutes");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting eye rest timer");
                throw;
            }
        }

        public async Task RestartBreakTimerAfterCompletion()
        {
            try
            {
                _logger.LogInformation("🔥 Restarting break timer after popup completion");
                
                // ENHANCED: Stop fallback timer since popup completed successfully
                _breakFallbackTimer?.Stop();
                
                // Clear delay status since we're starting a fresh break cycle
                IsBreakDelayed = false;
                
                // Reset the break start time for next cycle
                _breakStartTime = DateTime.Now;
                
                // SMART RECALCULATION: After a break, user has fresh eyes - restart eye rest timer too
                await SmartRecalculateEyeRestAfterBreak();
                
                // Reinitialize timer with full interval
                InitializeBreakTimer();
                
                if (IsRunning && _breakTimer != null)
                {
                    _breakTimer.Start();
                    _logger.LogInformation($"🔥 Break timer restarted - next break in {_configuration.Break.IntervalMinutes} minutes");
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting break timer");
                throw;
            }
        }

        private void InitializeEyeRestTimer()
        {
            // CRITICAL FIX: Cleanup existing timer before creating new one
            if (_eyeRestTimer != null)
            {
                _eyeRestTimer.Stop();
                _eyeRestTimer.Tick -= OnEyeRestTimerTick; // Prevent multiple subscriptions
                _eyeRestTimer = null;
            }
            
            // Calculate the interval MINUS warning time so warning fires at correct time
            var intervalMinutes = _configuration.EyeRest.IntervalMinutes;
            var warningSeconds = _configuration.EyeRest.WarningEnabled ? _configuration.EyeRest.WarningSeconds : 0;
            var timerInterval = TimeSpan.FromMinutes(intervalMinutes) - TimeSpan.FromSeconds(warningSeconds);
            
            _eyeRestTimer = new DispatcherTimer
            {
                Interval = timerInterval
            };
            
            _eyeRestTimer.Tick += OnEyeRestTimerTick;
            
            _logger.LogInformation($"Eye rest timer initialized - interval: {timerInterval.TotalSeconds}s (warning at {warningSeconds}s before {intervalMinutes}min target)");
        }

        private void InitializeEyeRestWarningTimer()
        {
            // CRITICAL FIX: Cleanup existing timer before creating new one
            if (_eyeRestWarningTimer != null)
            {
                _eyeRestWarningTimer.Stop();
                _eyeRestWarningTimer = null;
            }
            
            _eyeRestWarningTimer = new DispatcherTimer();
            // This timer will be started dynamically before eye rest
        }

        private void InitializeBreakTimer()
        {
            // CRITICAL FIX: Cleanup existing timer before creating new one
            if (_breakTimer != null)
            {
                _breakTimer.Stop();
                _breakTimer.Tick -= OnBreakTimerTick; // Prevent multiple subscriptions
                _breakTimer = null;
            }
            
            // Calculate the interval MINUS warning time so warning fires at correct time
            var intervalMinutes = _configuration.Break.IntervalMinutes;
            var warningSeconds = _configuration.Break.WarningEnabled ? _configuration.Break.WarningSeconds : 0;
            var timerInterval = TimeSpan.FromMinutes(intervalMinutes) - TimeSpan.FromSeconds(warningSeconds);
            
            _breakTimer = new DispatcherTimer
            {
                Interval = timerInterval
            };
            
            _breakTimer.Tick += OnBreakTimerTick;
            
            _logger.LogInformation($"Break timer initialized - interval: {timerInterval.TotalMinutes:F1}m (warning at {warningSeconds}s before {intervalMinutes}min target)");
        }

        private void InitializeBreakWarningTimer()
        {
            // CRITICAL FIX: Cleanup existing timer before creating new one
            if (_breakWarningTimer != null)
            {
                _breakWarningTimer.Stop();
                _breakWarningTimer = null;
            }
            
            _breakWarningTimer = new DispatcherTimer();
            // This timer will be started dynamically before breaks
        }


        private void OnEyeRestTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("🔔 OnEyeRestTimerTick FIRED - this is the method that should execute after 30s");
                
                // Stop the main timer
                _eyeRestTimer?.Stop();

                // Fire warning event immediately (this is the warning time!)
                if (_configuration.EyeRest.WarningEnabled)
                {
                    var warningEventArgs = new TimerEventArgs
                    {
                        TriggeredAt = DateTime.Now,
                        NextInterval = TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds),
                        Type = TimerType.EyeRestWarning
                    };

                    _logger.LogInformation($"🚨 Eye rest WARNING fired! {_configuration.EyeRest.WarningSeconds} seconds until eye rest");
                    EyeRestWarning?.Invoke(this, warningEventArgs);
                    
                    // Timer will be started by NotificationService after popup is created
                }
                else
                {
                    // Trigger eye rest immediately if warning is disabled
                    TriggerEyeRest();
                }
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
                        // Update warning popup countdown
                        _notificationService.UpdateEyeRestWarningCountdown(remaining);
                    }
                };
                
                _eyeRestWarningTimer.Tick += warningTickHandler;
                _eyeRestWarningTimer.Start();
                _logger.LogInformation($"Eye rest warning timer started with external countdown control - duration: {warningDuration.TotalSeconds}s");
            }
        }


        private void TriggerEyeRest()
        {
            var eventArgs = new TimerEventArgs
            {
                TriggeredAt = DateTime.Now,
                NextInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes),
                Type = TimerType.EyeRest
            };

            _logger.LogInformation("👁 EYE REST DUE - triggering eye rest popup");
            EyeRestDue?.Invoke(this, eventArgs);
            
            // ENHANCED: Add fallback timer to auto-restart if popup doesn't complete
            StartEyeRestFallbackTimer();
            
            _logger.LogInformation("👁 Eye rest triggered - timer will restart after popup closes or fallback timeout");
        }

        private void OnBreakTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("🔔 OnBreakTimerTick FIRED - this is the break warning time");
                
                // Stop the main timer
                _breakTimer?.Stop();

                // Fire warning event immediately (this is the warning time!)
                if (_configuration.Break.WarningEnabled)
                {
                    var warningEventArgs = new TimerEventArgs
                    {
                        TriggeredAt = DateTime.Now,
                        NextInterval = TimeSpan.FromSeconds(_configuration.Break.WarningSeconds),
                        Type = TimerType.BreakWarning
                    };

                    _logger.LogInformation($"🚨 Break WARNING fired! {_configuration.Break.WarningSeconds} seconds until break");
                    BreakWarning?.Invoke(this, warningEventArgs);
                    
                    // Timer will be started by NotificationService after popup is created
                }
                else
                {
                    // Trigger break immediately if warning is disabled
                    TriggerBreak();
                }
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
                        // Update warning popup countdown
                        _notificationService.UpdateBreakWarningCountdown(remaining);
                    }
                };
                
                _breakWarningTimer.Tick += warningTickHandler;
                _breakWarningTimer.Start();
                _logger.LogInformation($"Break warning timer started with external countdown control - duration: {warningDuration.TotalSeconds}s");
            }
        }


        private void TriggerBreak()
        {
            try
            {
                _logger.LogInformation("🔥 TriggerBreak STARTED - about to fire BreakDue event");
                
                // Clear delay status since break is now triggering
                IsBreakDelayed = false;
                
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes),
                    Type = TimerType.Break
                };

                _logger.LogInformation("🛑 BREAK DUE - triggering break popup");
                
                if (BreakDue != null)
                {
                    _logger.LogInformation($"🔥 BreakDue event has {BreakDue.GetInvocationList().Length} subscribers - firing event now");
                    BreakDue.Invoke(this, eventArgs);
                    _logger.LogInformation("🔥 BreakDue event fired successfully - break popup should show now");
                }
                else
                {
                    _logger.LogError("🚨 CRITICAL: BreakDue event is NULL - no subscribers! Break popup will not show!");
                }
                
                // ENHANCED: Add fallback timer to auto-restart if popup doesn't complete
                StartBreakFallbackTimer();
                
                _logger.LogInformation("🔥 Break triggered - timer will restart after popup closes or fallback timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 CRITICAL ERROR in TriggerBreak - this prevents break popup from showing");
            }
        }

        private async void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            try
            {
                _configuration = e.NewConfiguration;
                
                // Update timer intervals if they changed
                if (e.OldConfiguration.EyeRest.IntervalMinutes != e.NewConfiguration.EyeRest.IntervalMinutes)
                {
                    await ResetEyeRestTimer();
                }
                
                if (e.OldConfiguration.Break.IntervalMinutes != e.NewConfiguration.Break.IntervalMinutes)
                {
                    await ResetBreakTimer();
                }
                
                // Notify UI of property changes
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TimeUntilNextEyeRest)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TimeUntilNextBreak)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NextEventDescription)));
                
                _logger.LogInformation("Timer service updated with new configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timer service configuration");
            }
        }

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
                
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                
                await _analyticsService.RecordPauseEventAsync(PauseReason.Manual);
                
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
            
            if (!IsPaused)
            {
                _logger.LogWarning("Timer service is not paused");
                return;
            }

            try
            {
                _logger.LogInformation("▶️ Manually resuming timer service");
                
                IsPaused = false;
                
                if (!IsSmartPaused)
                {
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                }
                
                await _analyticsService.RecordResumeEventAsync(ResumeReason.Manual);
                
                _logger.LogInformation("Timer service resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming timer service");
                IsPaused = true;
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
                
                _eyeRestTimer?.Stop();
                _breakTimer?.Stop();
                
                await _analyticsService.RecordPauseEventAsync(PauseReason.SmartDetection);
                
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
                
                IsSmartPaused = false;
                
                if (!IsPaused)
                {
                    _eyeRestTimer?.Start();
                    _breakTimer?.Start();
                }
                
                await _analyticsService.RecordResumeEventAsync(ResumeReason.SmartDetection);
                
                _logger.LogInformation("Timer service smart resumed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error smart resuming timer service");
                IsSmartPaused = true;
                throw;
            }
        }

        /// <summary>
        /// Validates timer configuration to ensure popups show according to configured time settings
        /// </summary>
        private void ValidateTimerConfiguration()
        {
            try
            {
                // Validate eye rest settings
                if (_configuration.EyeRest.IntervalMinutes < 1 || _configuration.EyeRest.IntervalMinutes > 120)
                {
                    throw new InvalidOperationException($"Eye rest interval must be between 1 and 120 minutes, got {_configuration.EyeRest.IntervalMinutes}");
                }
                
                if (_configuration.EyeRest.DurationSeconds < 5 || _configuration.EyeRest.DurationSeconds > 300)
                {
                    throw new InvalidOperationException($"Eye rest duration must be between 5 and 300 seconds, got {_configuration.EyeRest.DurationSeconds}");
                }
                
                // Validate break settings
                if (_configuration.Break.IntervalMinutes < 1 || _configuration.Break.IntervalMinutes > 240)
                {
                    throw new InvalidOperationException($"Break interval must be between 1 and 240 minutes, got {_configuration.Break.IntervalMinutes}");
                }
                
                if (_configuration.Break.DurationMinutes < 1 || _configuration.Break.DurationMinutes > 30)
                {
                    throw new InvalidOperationException($"Break duration must be between 1 and 30 minutes, got {_configuration.Break.DurationMinutes}");
                }
                
                // Validate warning settings
                if (_configuration.EyeRest.WarningEnabled && (_configuration.EyeRest.WarningSeconds < 10 || _configuration.EyeRest.WarningSeconds > 120))
                {
                    throw new InvalidOperationException($"Eye rest warning must be between 10 and 120 seconds, got {_configuration.EyeRest.WarningSeconds}");
                }
                
                if (_configuration.Break.WarningEnabled && (_configuration.Break.WarningSeconds < 10 || _configuration.Break.WarningSeconds > 120))
                {
                    throw new InvalidOperationException($"Break warning must be between 10 and 120 seconds, got {_configuration.Break.WarningSeconds}");
                }
                
                _logger.LogInformation("✅ Timer configuration validated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timer configuration validation failed");
                throw;
            }
        }

        /// <summary>
        /// Smart recalculation of eye rest timer after break completion.
        /// When user completes a break, they have fresh eyes so eye rest timer should be reset.
        /// </summary>
        private async Task SmartRecalculateEyeRestAfterBreak()
        {
            try
            {
                _logger.LogInformation("👁 🧠 Smart recalculating eye rest timer after break - user has fresh eyes");
                
                // Reset eye rest start time since user has fresh eyes after break
                _eyeRestStartTime = DateTime.Now;
                
                // Reinitialize eye rest timer with full interval
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    InitializeEyeRestTimer();
                    
                    if (IsRunning)
                    {
                        _eyeRestTimer.Start();
                        _logger.LogInformation($"👁 🧠 Eye rest timer smartly recalculated - next eye rest in {_configuration.EyeRest.IntervalMinutes} minutes");
                    }
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in smart eye rest recalculation");
                throw;
            }
        }

        #region Fallback Timer Methods
        
        /// <summary>
        /// Start fallback timer for eye rest - auto-restarts if popup doesn't complete within timeout
        /// </summary>
        private void StartEyeRestFallbackTimer()
        {
            try
            {
                // Stop any existing fallback timer
                _eyeRestFallbackTimer?.Stop();
                
                // Create new fallback timer with timeout (eye rest duration + 30 seconds buffer)
                var fallbackTimeout = TimeSpan.FromSeconds(_configuration.EyeRest.DurationSeconds + 30);
                
                _eyeRestFallbackTimer = new DispatcherTimer
                {
                    Interval = fallbackTimeout
                };
                
                _eyeRestFallbackTimer.Tick += (sender, e) =>
                {
                    _logger.LogWarning($"👁 ⚠️ Eye rest fallback timer triggered after {fallbackTimeout.TotalSeconds}s - auto-restarting timer");
                    
                    // Stop fallback timer
                    _eyeRestFallbackTimer?.Stop();
                    
                    // Force restart eye rest timer
                    _ = Task.Run(async () => await RestartEyeRestTimerAfterCompletion());
                };
                
                _eyeRestFallbackTimer.Start();
                _logger.LogInformation($"👁 🔧 Eye rest fallback timer started - will auto-restart in {fallbackTimeout.TotalSeconds}s if needed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting eye rest fallback timer");
            }
        }
        
        /// <summary>
        /// Start fallback timer for break - auto-restarts if popup doesn't complete within timeout
        /// </summary>
        private void StartBreakFallbackTimer()
        {
            try
            {
                // Stop any existing fallback timer
                _breakFallbackTimer?.Stop();
                
                // Create new fallback timer with timeout (break duration + 60 seconds buffer)
                var fallbackTimeout = TimeSpan.FromMinutes(_configuration.Break.DurationMinutes + 1);
                
                _breakFallbackTimer = new DispatcherTimer
                {
                    Interval = fallbackTimeout
                };
                
                _breakFallbackTimer.Tick += (sender, e) =>
                {
                    _logger.LogWarning($"🛑 ⚠️ Break fallback timer triggered after {fallbackTimeout.TotalMinutes}min - auto-restarting timer");
                    
                    // Stop fallback timer
                    _breakFallbackTimer?.Stop();
                    
                    // Force restart break timer
                    _ = Task.Run(async () => await RestartBreakTimerAfterCompletion());
                };
                
                _breakFallbackTimer.Start();
                _logger.LogInformation($"🛑 🔧 Break fallback timer started - will auto-restart in {fallbackTimeout.TotalMinutes}min if needed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting break fallback timer");
            }
        }
        
        #endregion

        public void Dispose()
        {
            _logger.LogInformation("Disposing TimerService...");
            
            _eyeRestTimer?.Stop();
            _breakTimer?.Stop();
            _eyeRestWarningTimer?.Stop();
            _breakWarningTimer?.Stop();
            
            // ENHANCED: Clean up fallback timers
            _eyeRestFallbackTimer?.Stop();
            _breakFallbackTimer?.Stop();
            
            _eyeRestTimer = null;
            _breakTimer = null;
            _eyeRestWarningTimer = null;
            _breakWarningTimer = null;
            _eyeRestFallbackTimer = null;
            _breakFallbackTimer = null;
            
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;
            
            _logger.LogInformation("🎯 TimerService disposed successfully");
        }

    }
}
