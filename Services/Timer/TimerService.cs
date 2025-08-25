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
        
        // Notification service injected later to avoid circular dependency
        private INotificationService? _notificationService;
        
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
            ITimerFactory timerFactory)
        {
            _logger = logger;
            _configurationService = configurationService;
            _analyticsService = analyticsService;
            _timerFactory = timerFactory;
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
        /// Helper method to invoke property changed events
        /// </summary>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

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
                
                // Dispose all timers
                DisposeAllTimers();
                
                // Dispose health monitor
                _healthMonitorTimer?.Stop();
                _healthMonitorTimer = null;
                
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
            
            // Dispose fallback timers
            _eyeRestFallbackTimer?.Stop();
            _eyeRestFallbackTimer = null;
            
            _breakFallbackTimer?.Stop();
            _breakFallbackTimer = null;
            
            // Dispose pause timer
            _manualPauseTimer?.Stop();
            _manualPauseTimer = null;
        }
    }
}