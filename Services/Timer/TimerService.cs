using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using EyeRest.Models;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Main timer service orchestrator that manages eye rest and break timers
    /// This partial class contains the core structure and public interface
    /// </summary>
    public partial class TimerService : ITimerService, IDisposable
    {
        private readonly ILogger<TimerService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ITimerFactory _timerFactory;
        private readonly IPauseReminderService _pauseReminderService;
        
        // Notification service injected later to avoid circular dependency
        private INotificationService? _notificationService;

        // User presence service injected later to avoid circular dependency
        private IUserPresenceService? _userPresenceService;

        // Configuration
        private AppConfiguration _configuration;
        
        // CRITICAL FIX: Track startup completion to prevent recovery interference
        private bool _hasCompletedInitialStartup = false;

        // Events
        public event EventHandler<TimerEventArgs>? EyeRestWarning;
        public event EventHandler<TimerEventArgs>? EyeRestDue;
        public event EventHandler<TimerEventArgs>? BreakWarning;
        public event EventHandler<TimerEventArgs>? BreakDue;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public TimerService(
            ILogger<TimerService> logger,
            IConfigurationService configurationService,
            IAnalyticsService analyticsService,
            ITimerFactory timerFactory,
            IPauseReminderService pauseReminderService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _analyticsService = analyticsService;
            _timerFactory = timerFactory;
            _pauseReminderService = pauseReminderService;
            _configuration = new AppConfiguration(); // Will be loaded in StartAsync
            
            _logger.LogInformation("TimerService initialized with testable timer factory");
        }

        /// <summary>
        /// Sets the notification service to avoid circular dependency during DI
        /// </summary>
        public void SetNotificationService(INotificationService notificationService)
        {
            _notificationService = notificationService;
            _logger.LogInformation("NotificationService injected into TimerService");
        }

        /// <summary>
        /// Sets the user presence service to avoid circular dependency during DI
        /// </summary>
        public void SetUserPresenceService(IUserPresenceService userPresenceService)
        {
            _userPresenceService = userPresenceService;
            _logger.LogInformation("UserPresenceService injected into TimerService");
        }

        /// <summary>
        /// Helper method to invoke property changed events
        /// </summary>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        #region Shared Timer Calculation Methods

        /// <summary>
        /// Calculates the eye rest timer interval using consistent logic for both initialization and restart
        /// </summary>
        /// <returns>Tuple containing (calculatedInterval, totalMinutes, warningSeconds, warningEnabled, isReduced)</returns>
        private (TimeSpan interval, int totalMinutes, int warningSeconds, bool warningEnabled, bool isReduced) CalculateEyeRestTimerInterval()
        {
            var totalMinutes = _configuration?.EyeRest?.IntervalMinutes ?? 20;
            var warningSeconds = _configuration?.EyeRest?.WarningSeconds ?? 15;
            var warningEnabled = _configuration?.EyeRest?.WarningEnabled ?? true;

            // Calculate reduced interval: total interval minus warning time
            var totalInterval = TimeSpan.FromMinutes(totalMinutes);
            var warningInterval = TimeSpan.FromSeconds(warningSeconds);
            var interval = warningEnabled && warningInterval < totalInterval
                ? totalInterval - warningInterval  // Reduced interval: triggers warning at correct time
                : totalInterval;                   // Full interval: no warning or warning disabled

            // Validate interval doesn't exceed DispatcherTimer maximum capacity
            var maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
            if (interval > maxInterval)
            {
                _logger.LogWarning("⚠️ Eye rest interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m",
                    interval.TotalMinutes, maxInterval.TotalMinutes);
                interval = maxInterval;
            }

            var isReduced = warningEnabled && interval < totalInterval;
            return (interval, totalMinutes, warningSeconds, warningEnabled, isReduced);
        }

        /// <summary>
        /// Calculates the break timer interval using consistent logic for both initialization and restart
        /// </summary>
        /// <returns>Tuple containing (calculatedInterval, totalMinutes, warningSeconds, warningEnabled, isReduced)</returns>
        private (TimeSpan interval, int totalMinutes, int warningSeconds, bool warningEnabled, bool isReduced) CalculateBreakTimerInterval()
        {
            var totalMinutes = _configuration?.Break?.IntervalMinutes ?? 55;
            var warningSeconds = _configuration?.Break?.WarningSeconds ?? 30;
            var warningEnabled = _configuration?.Break?.WarningEnabled ?? true;

            // Calculate reduced interval: total interval minus warning time
            var totalInterval = TimeSpan.FromMinutes(totalMinutes);
            var warningInterval = TimeSpan.FromSeconds(warningSeconds);
            var interval = warningEnabled && warningInterval < totalInterval
                ? totalInterval - warningInterval  // Reduced interval: triggers warning at correct time
                : totalInterval;                   // Full interval: no warning or warning disabled

            // Validate interval doesn't exceed DispatcherTimer maximum capacity
            var maxInterval = TimeSpan.FromMilliseconds(int.MaxValue);
            if (interval > maxInterval)
            {
                _logger.LogWarning("⚠️ Break interval {TotalMinutes}m exceeds DispatcherTimer max capacity. Clamping to {MaxMinutes}m",
                    interval.TotalMinutes, maxInterval.TotalMinutes);
                interval = maxInterval;
            }

            var isReduced = warningEnabled && interval < totalInterval;
            return (interval, totalMinutes, warningSeconds, warningEnabled, isReduced);
        }

        /// <summary>
        /// TIMELINE FIX: Validates if enough time has passed since the last eye rest trigger to allow fallback/recovery
        /// </summary>
        /// <returns>True if fallback should be allowed, false if too soon</returns>
        private bool ShouldAllowEyeRestFallback()
        {
            if (_lastEyeRestTriggeredTime == DateTime.MinValue)
            {
                // No previous trigger recorded, allow fallback (startup scenario)
                return true;
            }

            // Get the current calculated interval
            var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateEyeRestTimerInterval();

            var timeSinceLastTrigger = DateTime.Now - _lastEyeRestTriggeredTime;
            var minimumInterval = interval; // Use the actual timer interval (4.8 min for 5min/15s config)

            var shouldAllow = timeSinceLastTrigger >= minimumInterval;

            if (!shouldAllow)
            {
                var remainingTime = minimumInterval - timeSinceLastTrigger;
                _logger.LogInformation("🕒 TIMELINE PROTECTION: Eye rest fallback blocked - {RemainingMinutes:F1}m remaining until next allowed trigger (last: {LastTrigger}, interval: {IntervalMinutes:F1}m)",
                    remainingTime.TotalMinutes, _lastEyeRestTriggeredTime.ToString("HH:mm:ss"), interval.TotalMinutes);
            }

            return shouldAllow;
        }

        /// <summary>
        /// TIMELINE FIX: Validates if enough time has passed since the last break trigger to allow fallback/recovery
        /// </summary>
        /// <returns>True if fallback should be allowed, false if too soon</returns>
        private bool ShouldAllowBreakFallback()
        {
            if (_lastBreakTriggeredTime == DateTime.MinValue)
            {
                // No previous trigger recorded, allow fallback (startup scenario)
                return true;
            }

            // Get the current calculated interval
            var (interval, totalMinutes, warningSeconds, warningEnabled, isReduced) = CalculateBreakTimerInterval();

            var timeSinceLastTrigger = DateTime.Now - _lastBreakTriggeredTime;
            var minimumInterval = interval; // Use the actual timer interval (14.5 min for 15min/30s config)

            var shouldAllow = timeSinceLastTrigger >= minimumInterval;

            if (!shouldAllow)
            {
                var remainingTime = minimumInterval - timeSinceLastTrigger;
                _logger.LogInformation("🕒 TIMELINE PROTECTION: Break fallback blocked - {RemainingMinutes:F1}m remaining until next allowed trigger (last: {LastTrigger}, interval: {IntervalMinutes:F1}m)",
                    remainingTime.TotalMinutes, _lastBreakTriggeredTime.ToString("HH:mm:ss"), interval.TotalMinutes);
            }

            return shouldAllow;
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _logger.LogInformation("Disposing TimerService");
                
                // Dispose all timers including health monitor
                DisposeAllTimers();
                
                _logger.LogInformation("TimerService disposed");
            }
        }

        private void DisposeAllTimers()
        {
            // Dispose main timers
            _eyeRestTimer?.Stop();
            _eyeRestTimer = null;
            
            _breakTimer?.Stop();
            _breakTimer = null;
            
            // Dispose warning timers
            _eyeRestWarningTimer?.Stop();
            _eyeRestWarningTimer = null;

            _breakWarningTimer?.Stop();
            _breakWarningTimer = null;

            // CRITICAL FIX: Dispose warning fallback timers to prevent ghost timers
            _eyeRestWarningFallbackTimer?.Stop();
            _eyeRestWarningFallbackTimer = null;

            _breakWarningFallbackTimer?.Stop();
            _breakWarningFallbackTimer = null;

            // Dispose fallback timers with event handler detachment
            if (_eyeRestFallbackTimer != null)
            {
                _eyeRestFallbackTimer.Stop();
                _eyeRestFallbackTimer.Tick -= OnEyeRestFallbackTimerTick;
                _eyeRestFallbackTimer = null;
            }

            if (_breakFallbackTimer != null)
            {
                _breakFallbackTimer.Stop();
                _breakFallbackTimer.Tick -= OnBreakFallbackTimerTick;
                _breakFallbackTimer = null;
            }

            // Dispose pause timer with event handler detachment
            if (_manualPauseTimer != null)
            {
                _manualPauseTimer.Stop();
                _manualPauseTimer.Tick -= OnManualPauseTimerTick;
                _manualPauseTimer = null;
            }

            // Dispose health monitor timer
            _healthMonitorTimer?.Stop();
            _healthMonitorTimer = null;
        }

        #region Processing Flag Management

        /// <summary>
        /// Clears the eye rest event processing flag - called after popup completion
        /// </summary>
        public void ClearEyeRestProcessingFlag()
        {
            _isEyeRestEventProcessing = false;

            // THREAD SAFETY: Clear global processing flag
            lock (_globalEyeRestLock)
            {
                _isAnyEyeRestEventProcessing = false;
            }

            _logger.LogDebug("🔄 Eye rest processing flags cleared (instance + global)");
        }

        /// <summary>
        /// Clears the break event processing flag - called after popup completion
        /// </summary>
        public void ClearBreakProcessingFlag()
        {
            _isBreakEventProcessing = false;

            // THREAD SAFETY: Clear global processing flag
            lock (_globalBreakLock)
            {
                _isAnyBreakEventProcessing = false;
            }

            _logger.LogDebug("🔄 Break processing flags cleared (instance + global)");
        }

        /// <summary>
        /// Clears the eye rest warning processing flag - called after warning completion
        /// </summary>
        public void ClearEyeRestWarningProcessingFlag()
        {
            _isEyeRestWarningProcessing = false;

            // THREAD SAFETY: Clear global warning processing flag
            lock (_globalEyeRestWarningLock)
            {
                _isAnyEyeRestWarningProcessing = false;
            }

            _logger.LogDebug("🔄 Eye rest warning processing flags cleared (instance + global)");
        }

        /// <summary>
        /// Clears the break warning processing flag - called after warning completion
        /// </summary>
        public void ClearBreakWarningProcessingFlag()
        {
            _isBreakWarningProcessing = false;

            // THREAD SAFETY: Clear global warning processing flag
            lock (_globalBreakWarningLock)
            {
                _isAnyBreakWarningProcessing = false;
            }

            _logger.LogDebug("🔄 Break warning processing flags cleared (instance + global)");
        }

        #endregion
    }
}