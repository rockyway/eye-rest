using System;
using System.Windows.Threading;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all state management and properties for TimerService
    /// </summary>
    public partial class TimerService
    {
        #region Core Timer Objects
        
        private ITimer? _eyeRestTimer;
        private ITimer? _eyeRestWarningTimer;
        private ITimer? _breakTimer;
        private ITimer? _breakWarningTimer;
        
        // Fallback timers to prevent stuck state
        private ITimer? _eyeRestFallbackTimer;
        private ITimer? _breakFallbackTimer;
        
        // Manual pause timer
        private ITimer? _manualPauseTimer;
        
        // Health monitoring timer
        private ITimer? _healthMonitorTimer;
        
        #endregion

        #region State Fields
        
        private bool _isStarted;
        private bool _isPaused;
        private bool _isSmartPaused;
        private string _smartPauseReason = string.Empty;
        
        // Manual pause functionality
        private bool _isManuallyPaused;
        private DateTime _manualPauseStartTime;
        private TimeSpan _manualPauseDuration;
        private string _pauseReason = string.Empty;
        
        // Timer tracking
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
        private DateTime _breakTimerStartTime;
        
        // Cross-timer coordination state
        private bool _isEyeRestNotificationActive;
        private bool _isBreakNotificationActive;
        private bool _eyeRestTimerPausedForBreak;
        private bool _breakTimerPausedForEyeRest;
        
        #endregion

        #region Public Properties
        
        public bool IsRunning 
        { 
            get => _isStarted; 
            private set 
            { 
                if (_isStarted != value)
                {
                    _isStarted = value;
                    OnPropertyChanged(nameof(IsRunning));
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
                    OnPropertyChanged(nameof(IsPaused));
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
                    OnPropertyChanged(nameof(IsSmartPaused));
                }
            }
        }

        public bool IsManuallyPaused
        {
            get => _isManuallyPaused;
            private set
            {
                if (_isManuallyPaused != value)
                {
                    _isManuallyPaused = value;
                    OnPropertyChanged(nameof(IsManuallyPaused));
                }
            }
        }

        public TimeSpan? ManualPauseRemaining
        {
            get
            {
                if (!IsManuallyPaused || _manualPauseDuration == TimeSpan.Zero)
                    return null;
                    
                var elapsed = DateTime.Now - _manualPauseStartTime;
                var remaining = _manualPauseDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public string? PauseReason => _isManuallyPaused || _isSmartPaused || _isPaused ? _pauseReason : null;

        public TimeSpan TimeUntilNextEyeRest
        {
            get
            {
                if (!IsRunning)
                    return TimeSpan.Zero;
                    
                if (_isEyeRestNotificationActive)
                    return TimeSpan.Zero;
                    
                if (IsPaused || IsSmartPaused || IsManuallyPaused)
                {
                    return _eyeRestRemainingTime > TimeSpan.Zero ? _eyeRestRemainingTime :
                           _eyeRestInterval > TimeSpan.Zero ? _eyeRestInterval :
                           TimeSpan.FromMinutes(_configuration?.EyeRest?.IntervalMinutes ?? 20);
                }
                
                if (_eyeRestTimer?.IsEnabled == true)
                {
                    var elapsed = DateTime.Now - _eyeRestStartTime;
                    var remaining = _eyeRestInterval - elapsed;
                    
                    // CRITICAL FIX: Log when timer is overdue to help debug stuck state
                    if (remaining <= TimeSpan.Zero)
                    {
                        _logger?.LogWarning("👁️ Eye rest timer is overdue by {OverdueSeconds}s - event should have fired!", 
                            Math.Abs(remaining.TotalSeconds));
                        return TimeSpan.Zero;
                    }
                    return remaining;
                }
                
                return TimeSpan.Zero;
            }
        }

        public TimeSpan TimeUntilNextBreak
        {
            get
            {
                if (!IsRunning)
                    return TimeSpan.Zero;
                    
                if (_isBreakNotificationActive)
                    return TimeSpan.Zero;
                    
                if (IsPaused || IsSmartPaused || IsManuallyPaused)
                {
                    return _breakRemainingTime > TimeSpan.Zero ? _breakRemainingTime :
                           _breakInterval > TimeSpan.Zero ? _breakInterval :
                           TimeSpan.FromMinutes(_configuration?.Break?.IntervalMinutes ?? 55);
                }
                
                if (IsBreakDelayed)
                {
                    var delayElapsed = DateTime.Now - _delayStartTime;
                    var delayRemaining = _delayDuration - delayElapsed;
                    
                    if (delayRemaining > TimeSpan.Zero)
                    {
                        return delayRemaining;
                    }
                    else
                    {
                        IsBreakDelayed = false;
                    }
                }
                
                if (_breakTimer?.IsEnabled == true)
                {
                    var elapsed = DateTime.Now - _breakStartTime;
                    var remaining = _breakInterval - elapsed;
                    
                    // CRITICAL FIX: Log when break timer is overdue to help debug stuck state
                    if (remaining <= TimeSpan.Zero)
                    {
                        _logger?.LogWarning("☕ Break timer is overdue by {OverdueSeconds}s - event should have fired!", 
                            Math.Abs(remaining.TotalSeconds));
                        return TimeSpan.Zero;
                    }
                    return remaining;
                }
                
                return TimeSpan.Zero;
            }
        }

        public string NextEventDescription
        {
            get
            {
                if (!IsRunning)
                    return "Timer not running";
                    
                if (IsManuallyPaused)
                    return $"Paused: {PauseReason ?? "Manual pause"}";
                    
                if (IsSmartPaused)
                    return $"Smart paused: {PauseReason ?? "Auto-detected"}";
                    
                if (IsPaused)
                    return "Paused";
                    
                var eyeRestTime = TimeUntilNextEyeRest;
                var breakTime = TimeUntilNextBreak;
                
                if (eyeRestTime <= breakTime && eyeRestTime > TimeSpan.Zero)
                    return $"Eye rest in {FormatTimeSpan(eyeRestTime)}";
                else if (breakTime > TimeSpan.Zero)
                    return $"Break in {FormatTimeSpan(breakTime)}";
                else
                    return "Next event pending";
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
                    OnPropertyChanged(nameof(IsBreakDelayed));
                }
            }
        }

        public TimeSpan DelayRemaining
        {
            get
            {
                if (!IsBreakDelayed)
                    return TimeSpan.Zero;
                    
                var elapsed = DateTime.Now - _delayStartTime;
                var remaining = _delayDuration - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public bool IsAnyNotificationActive => _isEyeRestNotificationActive || _isBreakNotificationActive;
        
        #endregion

        #region Helper Methods
        
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
            else
                return $"{timeSpan.Seconds}s";
        }
        
        #endregion
    }
}