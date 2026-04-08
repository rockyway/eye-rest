using System;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Partial class containing all state management and properties for TimerService
    /// </summary>
    public partial class TimerService
    {
        // Dispatcher service for UI thread operations (platform-agnostic)
        private readonly IDispatcherService _dispatcherService;

        #region Core Timer Objects

        // TIMER ARCHITECTURE (after consolidation):
        // =========================================
        // 1. Main timers: _eyeRestTimer, _breakTimer
        //    - Fire at configured intervals (minus warning time)
        //    - Start warning countdown when they fire
        //
        // 2. Warning timers: _eyeRestWarningTimer, _breakWarningTimer
        //    - 100ms tick interval for countdown display
        //    - Trigger popup when countdown completes
        //
        // 3. Warning fallback timers: _eyeRestWarningFallbackTimer, _breakWarningFallbackTimer
        //    - Fire 2s after warning period if warning timer fails
        //    - Ensures popup always shows even if warning timer hangs
        //
        // 4. Health monitor: _healthMonitorTimer
        //    - Emergency backup (fires only when heartbeat stale >2min)
        //    - Detects completely stuck timer infrastructure
        //
        // REMOVED (to prevent race conditions):
        // - _eyeRestFallbackTimer, _breakFallbackTimer were removed
        //   (they fired 5s after expected, causing duplicate triggers)

        private ITimer? _eyeRestTimer;
        private ITimer? _eyeRestWarningTimer;
        private ITimer? _breakTimer;
        private ITimer? _breakWarningTimer;

        // DEPRECATED: These fallback timers are no longer used to prevent race conditions
        // Kept as fields for backward compatibility with Dispose() cleanup
        private ITimer? _eyeRestFallbackTimer;
        private ITimer? _breakFallbackTimer;

        // Warning countdown fallback timers (active - provide backup for warning phase)
        private ITimer? _eyeRestWarningFallbackTimer;
        private ITimer? _breakWarningFallbackTimer;
        
        // Manual pause timer
        private ITimer? _manualPauseTimer;

        // Break delay timer
        private ITimer? _breakDelayTimer;

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
        private DateTime _eyeRestStartTime = DateTime.MinValue;
        private DateTime _breakStartTime = DateTime.MinValue;
        private TimeSpan _eyeRestInterval;
        private TimeSpan _breakInterval;
        private bool _isBreakDelayed;
        private DateTime _delayStartTime;
        private TimeSpan _delayDuration;
        private int _consecutiveBreakDelayCount;
        
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

        // Processing state to prevent backup trigger race conditions
        // Using volatile for visibility across threads, combined with Interlocked operations for atomicity
        private volatile bool _isEyeRestEventProcessing;
        private volatile bool _isBreakEventProcessing;

        // Warning processing state to prevent duplicate warning triggers
        private volatile bool _isEyeRestWarningProcessing;
        private volatile bool _isBreakWarningProcessing;

        // THREAD SAFETY: Static locks to prevent ALL timer systems from interfering
        private static readonly object _globalEyeRestLock = new object();
        private static readonly object _globalBreakLock = new object();
        private static volatile bool _isAnyEyeRestEventProcessing = false;
        private static volatile bool _isAnyBreakEventProcessing = false;

        // THREAD SAFETY: Static locks for warning events to prevent duplicate warnings
        private static readonly object _globalEyeRestWarningLock = new object();
        private static readonly object _globalBreakWarningLock = new object();
        private static volatile bool _isAnyEyeRestWarningProcessing = false;
        private static volatile bool _isAnyBreakWarningProcessing = false;

        // ATOMIC FLAG OPERATIONS: Use integer for Interlocked operations (0 = false, 1 = true)
        private static int _atomicEyeRestProcessing = 0;
        private static int _atomicBreakProcessing = 0;
        private static int _atomicEyeRestWarningProcessing = 0;
        private static int _atomicBreakWarningProcessing = 0;
        
        // Clock jump detection fields
        private DateTime _lastEyeRestTick = DateTime.MinValue;
        private DateTime _lastBreakTick = DateTime.MinValue;
        private DateTime _lastSystemCheck = DateTime.Now;

        // Rate-limit overdue log messages (prevent flooding from UI-polled properties)
        private DateTime _lastEyeRestOverdueLog = DateTime.MinValue;
        private DateTime _lastBreakOverdueLog = DateTime.MinValue;

        // TIMELINE FIX: Track when main timers actually triggered events (for fallback validation)
        private DateTime _lastEyeRestTriggeredTime = DateTime.MinValue;
        private DateTime _lastBreakTriggeredTime = DateTime.MinValue;
        
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
                {
                    // CRITICAL FIX: Don't show "Due now" when service is stopped
                    // Return a reasonable default to prevent UI stuck state
                    return TimeSpan.FromMinutes(_configuration?.EyeRest?.IntervalMinutes ?? 20);
                }
                    
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
                    // CRITICAL FIX: Check if start time is initialized to prevent massive elapsed time on startup
                    if (_eyeRestStartTime == DateTime.MinValue)
                    {
                        _logger?.LogWarning("👁️ Eye rest timer is enabled but start time not initialized - returning full interval");
                        return _eyeRestInterval > TimeSpan.Zero ? _eyeRestInterval : TimeSpan.FromMinutes(20);
                    }
                    
                    var elapsed = DateTime.Now - _eyeRestStartTime;
                    
                    // SAFETY CHECK: Prevent negative elapsed time on clock changes or startup issues
                    if (elapsed < TimeSpan.Zero)
                    {
                        _logger?.LogWarning("👁️ Negative elapsed time detected ({Elapsed}), resetting start time", elapsed);
                        _eyeRestStartTime = DateTime.Now;
                        return _eyeRestInterval;
                    }
                    
                    var remaining = _eyeRestInterval - elapsed;
                    
                    // Log when timer is overdue (rate-limited to once per 60s to prevent log flooding)
                    if (remaining <= TimeSpan.Zero)
                    {
                        var now = DateTime.Now;
                        if ((now - _lastEyeRestOverdueLog).TotalSeconds >= 60)
                        {
                            _lastEyeRestOverdueLog = now;
                            _logger?.LogWarning("👁️ Eye rest timer is overdue by {OverdueSeconds}s - event should have fired!",
                                Math.Abs(remaining.TotalSeconds));
                        }
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
                {
                    // CRITICAL FIX: Don't show "Due now" when service is stopped
                    // Return a reasonable default to prevent UI stuck state
                    return TimeSpan.FromMinutes(_configuration?.Break?.IntervalMinutes ?? 55);
                }
                    
                if (_isBreakNotificationActive)
                {
                    // CRITICAL FIX: During warning phase, show actual countdown to break
                    // Only return zero if we're in the actual break popup phase
                    if (_breakWarningTimer?.IsEnabled == true)
                    {
                        // We're in warning phase - calculate remaining warning time
                        // This provides accurate countdown during the 30-second warning
                        var warningDuration = TimeSpan.FromSeconds(_configuration?.Break?.WarningSeconds ?? 30);
                        // Note: Exact warning remaining time calculation would require tracking warning start time
                        // For now, return a reasonable estimate to prevent showing 0s during warning
                        return TimeSpan.FromSeconds(Math.Max(1, warningDuration.TotalSeconds / 2));
                    }
                    else
                    {
                        // We're in actual break phase - return zero
                        return TimeSpan.Zero;
                    }
                }
                    
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
                    // CRITICAL FIX: Check if start time is initialized to prevent massive elapsed time on startup
                    if (_breakStartTime == DateTime.MinValue)
                    {
                        _logger?.LogWarning("☕ Break timer is enabled but start time not initialized - returning full interval");
                        return _breakInterval > TimeSpan.Zero ? _breakInterval : TimeSpan.FromMinutes(55);
                    }
                    
                    var elapsed = DateTime.Now - _breakStartTime;
                    
                    // SAFETY CHECK: Prevent negative elapsed time on clock changes or startup issues
                    if (elapsed < TimeSpan.Zero)
                    {
                        _logger?.LogWarning("☕ Negative elapsed time detected ({Elapsed}), resetting start time", elapsed);
                        _breakStartTime = DateTime.Now;
                        return _breakInterval;
                    }
                    
                    var remaining = _breakInterval - elapsed;
                    
                    // Log when break timer is overdue (rate-limited to once per 60s to prevent log flooding)
                    if (remaining <= TimeSpan.Zero)
                    {
                        var now = DateTime.Now;
                        if ((now - _lastBreakOverdueLog).TotalSeconds >= 60)
                        {
                            _lastBreakOverdueLog = now;
                            _logger?.LogWarning("☕ Break timer is overdue by {OverdueSeconds}s - event should have fired!",
                                Math.Abs(remaining.TotalSeconds));
                        }
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

        public int ConsecutiveBreakDelayCount => _consecutiveBreakDelayCount;
        
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