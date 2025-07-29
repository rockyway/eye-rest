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
        
        private DispatcherTimer? _eyeRestTimer;
        private DispatcherTimer? _eyeRestWarningTimer;
        private DispatcherTimer? _breakTimer;
        private DispatcherTimer? _breakWarningTimer;
        
        private AppConfiguration _configuration;
        private bool _isStarted;
        private DateTime _eyeRestStartTime;
        private DateTime _breakStartTime;
        private TimeSpan _eyeRestInterval;
        private TimeSpan _breakInterval;
        private bool _isBreakDelayed;
        private DateTime _delayStartTime;
        private TimeSpan _delayDuration;

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

        public TimeSpan TimeUntilNextEyeRest
        {
            get
            {
                if (!IsRunning) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _eyeRestStartTime;
                var remaining = _eyeRestInterval - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public TimeSpan TimeUntilNextBreak
        {
            get
            {
                if (!IsRunning) return TimeSpan.Zero;
                var elapsed = DateTime.Now - _breakStartTime;
                var remaining = _breakInterval - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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
                    return $"Next eye rest: {FormatTimeSpan(eyeRestTime)}";
                }
                else
                {
                    return $"Next break: {FormatTimeSpan(breakTime)}";
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

        public TimerService(ILogger<TimerService> logger, IConfigurationService configurationService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _configuration = new AppConfiguration(); // Will be loaded on start
            
            // Subscribe to configuration changes
            _configurationService.ConfigurationChanged += OnConfigurationChanged;
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
                
                _logger.LogInformation($"🔄 Starting with configuration: EyeRest={_configuration.EyeRest.IntervalMinutes}min, Break={_configuration.Break.IntervalMinutes}min");
                
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
                _logger.LogInformation($"✅ Timer service started successfully");
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
                    _eyeRestWarningTimer.Tick -= OnEyeRestWarningTimerTick; // CRITICAL: Prevent memory leak
                }
                
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    _breakTimer.Tick -= OnBreakTimerTick; // CRITICAL: Prevent memory leak
                }
                
                if (_breakWarningTimer != null)
                {
                    _breakWarningTimer.Stop();
                    _breakWarningTimer.Tick -= OnBreakWarningTimerTick; // CRITICAL: Prevent memory leak
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
                
                // Clear delay status since we're starting a fresh break cycle
                IsBreakDelayed = false;
                
                // Reset the break start time for next cycle
                _breakStartTime = DateTime.Now;
                
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
                    
                    // Start timer for the remaining warning period
                    StartEyeRestWarningTimer();
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

        private void StartEyeRestWarningTimer()
        {
            if (_eyeRestWarningTimer != null)
            {
                _eyeRestWarningTimer.Interval = TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
                _eyeRestWarningTimer.Tick += OnEyeRestWarningTimerTick;
                _eyeRestWarningTimer.Start();

                _logger.LogInformation($"Eye rest warning timer started - will fire warning in {_configuration.EyeRest.WarningSeconds} seconds");
            }
        }

        private void OnEyeRestWarningTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _eyeRestWarningTimer?.Stop();
                _eyeRestWarningTimer!.Tick -= OnEyeRestWarningTimerTick;
                
                _logger.LogInformation("⏰ Warning period complete - triggering eye rest NOW");
                
                // Warning period is over, trigger the actual eye rest
                TriggerEyeRest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in eye rest warning timer tick");
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
            
            // CRITICAL FIX: Do NOT restart timer here - wait for eye rest completion
            // The timer should only restart after the eye rest popup is closed
            _logger.LogInformation("👁 Eye rest triggered - timer will restart after popup closes");
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
                    
                    // Start timer for the remaining warning period
                    StartBreakWarningTimer();
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

        private void StartBreakWarningTimer()
        {
            if (_breakWarningTimer != null)
            {
                _breakWarningTimer.Interval = TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
                _breakWarningTimer.Tick += OnBreakWarningTimerTick;
                _breakWarningTimer.Start();

                _logger.LogInformation($"Break warning timer started - will trigger break in {_configuration.Break.WarningSeconds} seconds");
            }
        }

        private void OnBreakWarningTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("🔥 OnBreakWarningTimerTick STARTED - this should trigger the break popup");
                
                _breakWarningTimer?.Stop();
                _breakWarningTimer!.Tick -= OnBreakWarningTimerTick;
                
                _logger.LogInformation("⏰ Break warning period complete - triggering break NOW");
                
                // Warning period is over, trigger the actual break
                TriggerBreak();
                
                _logger.LogInformation("🔥 OnBreakWarningTimerTick COMPLETED - TriggerBreak() called");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 CRITICAL ERROR in break warning timer tick - this prevents break popup from showing");
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
                
                // CRITICAL FIX: Do NOT restart timer here - wait for break completion
                // The timer should only restart after the break popup is closed
                _logger.LogInformation("🔥 Break triggered - timer will restart after popup closes");
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

        public void Dispose()
        {
            try
            {
                // CRITICAL FIX: Unsubscribe event handlers before disposing timers
                if (_eyeRestTimer != null)
                {
                    _eyeRestTimer.Stop();
                    _eyeRestTimer.Tick -= OnEyeRestTimerTick; // CRITICAL: Prevent memory leak
                    _eyeRestTimer = null;
                }
                
                if (_eyeRestWarningTimer != null)
                {
                    _eyeRestWarningTimer.Stop();
                    _eyeRestWarningTimer.Tick -= OnEyeRestWarningTimerTick; // CRITICAL: Prevent memory leak  
                    _eyeRestWarningTimer = null;
                }
                
                if (_breakTimer != null)
                {
                    _breakTimer.Stop();
                    _breakTimer.Tick -= OnBreakTimerTick; // CRITICAL: Prevent memory leak
                    _breakTimer = null;
                }
                
                if (_breakWarningTimer != null)
                {
                    _breakWarningTimer.Stop();
                    _breakWarningTimer.Tick -= OnBreakWarningTimerTick; // CRITICAL: Prevent memory leak
                    _breakWarningTimer = null;
                }
                
                _configurationService.ConfigurationChanged -= OnConfigurationChanged;
                
                IsRunning = false;
                
                _logger.LogInformation("✅ TimerService disposed properly - all event handlers unsubscribed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing timer service");
            }
        }

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 1)
            {
                return $"{timeSpan.Seconds}s";
            }
            else if (timeSpan.TotalHours < 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
        }
    }
}