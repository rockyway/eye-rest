using System;
using System.Threading.Tasks;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public interface IApplicationOrchestrator
    {
        Task InitializeAsync();
        Task ShutdownAsync();
    }

    public class ApplicationOrchestrator : IApplicationOrchestrator
    {
        private readonly ITimerService _timerService;
        private readonly INotificationService _notificationService;
        private readonly IAudioService _audioService;
        private readonly ISystemTrayService _systemTrayService;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly IConfigurationService _configurationService; // CRITICAL FIX: Add configuration service
        private readonly ILogger<ApplicationOrchestrator> _logger;

        public ApplicationOrchestrator(
            ITimerService timerService,
            INotificationService notificationService,
            IAudioService audioService,
            ISystemTrayService systemTrayService,
            IPerformanceMonitor performanceMonitor,
            IConfigurationService configurationService, // CRITICAL FIX: Inject configuration service
            ILogger<ApplicationOrchestrator> logger)
        {
            _timerService = timerService;
            _notificationService = notificationService;
            _audioService = audioService;
            _systemTrayService = systemTrayService;
            _performanceMonitor = performanceMonitor;
            _configurationService = configurationService; // CRITICAL FIX: Store configuration service
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing application orchestrator - subscribing to timer events");

                // Wire up timer events to notifications and audio
                _timerService.EyeRestWarning += OnEyeRestWarning;
                _logger.LogInformation("✅ EyeRestWarning event subscribed");
                
                _timerService.EyeRestDue += OnEyeRestDue;
                _logger.LogInformation("✅ EyeRestDue event subscribed");
                
                _timerService.BreakWarning += OnBreakWarning;
                _logger.LogInformation("✅ BreakWarning event subscribed");
                
                _timerService.BreakDue += OnBreakDue;
                _logger.LogInformation("✅ BreakDue event subscribed");

                // Update system tray icon based on timer state
                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);

                _logger.LogInformation("🎯 Application orchestrator initialized successfully - ALL EVENTS SUBSCRIBED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize application orchestrator");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
                throw;
            }
        }

        public async Task ShutdownAsync()
        {
            try
            {
                _logger.LogInformation("Shutting down application orchestrator");

                // Unsubscribe from events
                _timerService.EyeRestWarning -= OnEyeRestWarning;
                _timerService.EyeRestDue -= OnEyeRestDue;
                _timerService.BreakWarning -= OnBreakWarning;
                _timerService.BreakDue -= OnBreakDue;

                // Hide all notifications
                await _notificationService.HideAllNotifications();

                // Stop timer service
                await _timerService.StopAsync();

                // Log final performance metrics
                _performanceMonitor.LogPerformanceMetrics();

                _logger.LogInformation("Application orchestrator shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application orchestrator shutdown");
            }
        }

        private async void OnEyeRestWarning(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogInformation("🚨 EYE REST WARNING EVENT FIRED! TimerService event received by ApplicationOrchestrator");
                _logger.LogInformation($"Warning details - TriggeredAt: {e.TriggeredAt}, NextInterval: {e.NextInterval.TotalSeconds} seconds, Type: {e.Type}");

                // Show eye rest warning
                await _notificationService.ShowEyeRestWarningAsync(e.NextInterval);

                _logger.LogInformation("🚨 Eye rest warning completed - popup should be visible");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling eye rest warning");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }

        private async void OnEyeRestDue(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogInformation("Eye rest reminder triggered");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Break);

                // Play start sound
                await _audioService.PlayEyeRestStartSound();

                // Show eye rest notification with actual configuration duration
                var config = await _configurationService.LoadConfigurationAsync();
                var duration = TimeSpan.FromSeconds(config.EyeRest.DurationSeconds); // CRITICAL FIX: Use actual config
                await _notificationService.ShowEyeRestReminderAsync(duration);

                // Play end sound
                await _audioService.PlayEyeRestEndSound();

                // CRITICAL FIX: Restart the timer after eye rest completes
                await _timerService.RestartEyeRestTimerAfterCompletion();

                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _logger.LogInformation("Eye rest reminder completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling eye rest reminder");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }

        private async void OnBreakWarning(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogInformation("Break warning triggered");

                // Play warning sound
                await _audioService.PlayBreakWarningSound();

                // Show break warning
                await _notificationService.ShowBreakWarningAsync(e.NextInterval);

                _logger.LogInformation("Break warning completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling break warning");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }

        private async void OnBreakDue(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🟢 OnBreakDue EVENT FIRED! TriggeredAt: {e.TriggeredAt}, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Break);

                // Show break notification with progress tracking and actual configuration duration
                var progress = new Progress<double>();
                var config = await _configurationService.LoadConfigurationAsync();
                var duration = TimeSpan.FromMinutes(config.Break.DurationMinutes); // CRITICAL FIX: Use actual config
                _logger.LogInformation($"🟢 Break duration from config: {duration.TotalMinutes} minutes");
                
                _logger.LogInformation("🟢 Calling NotificationService.ShowBreakReminderAsync");
                var result = await _notificationService.ShowBreakReminderAsync(duration, progress);
                _logger.LogInformation($"🟢 ShowBreakReminderAsync returned with result: {result}");

                // Handle user action
                switch (result)
                {
                    case BreakAction.Completed:
                        _logger.LogInformation("🟢 Break completed successfully");
                        // CRITICAL FIX: Restart the timer after break completes
                        await _timerService.RestartBreakTimerAfterCompletion();
                        break;
                    case BreakAction.DelayOneMinute:
                        _logger.LogInformation("🟢 Break delayed by 1 minute");
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(1));
                        break;
                    case BreakAction.DelayFiveMinutes:
                        _logger.LogInformation("🟢 Break delayed by 5 minutes");
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(5));
                        break;
                    case BreakAction.Skipped:
                        _logger.LogInformation("🟢 Break skipped by user");
                        // CRITICAL FIX: Restart the timer after skip
                        await _timerService.RestartBreakTimerAfterCompletion();
                        break;
                }

                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _logger.LogInformation("🟢 Break reminder completed - OnBreakDue handler finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🟢 ERROR handling break reminder in OnBreakDue");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }
    }
}