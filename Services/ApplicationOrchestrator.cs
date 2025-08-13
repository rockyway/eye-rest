using System;
using System.Threading.Tasks;
using EyeRest.Services;
using EyeRest.Models;
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
        private readonly IConfigurationService _configurationService;
        private readonly IUserPresenceService _userPresenceService;
        private readonly IMeetingDetectionService _meetingDetectionService;
        private readonly IAnalyticsService _analyticsService;
        private readonly IPauseReminderService _pauseReminderService;
        private readonly ILogger<ApplicationOrchestrator> _logger;
        
        // State tracking
        private bool _isInitialized = false;
        
        // ENHANCED: Timer for updating system tray tooltip with timer details
        private System.Windows.Threading.DispatcherTimer? _trayUpdateTimer;

        public ApplicationOrchestrator(
            ITimerService timerService,
            INotificationService notificationService,
            IAudioService audioService,
            ISystemTrayService systemTrayService,
            IPerformanceMonitor performanceMonitor,
            IConfigurationService configurationService,
            IUserPresenceService userPresenceService,
            IMeetingDetectionService meetingDetectionService,
            IAnalyticsService analyticsService,
            IPauseReminderService pauseReminderService,
            ILogger<ApplicationOrchestrator> logger)
        {
            _timerService = timerService;
            _notificationService = notificationService;
            _audioService = audioService;
            _systemTrayService = systemTrayService;
            _performanceMonitor = performanceMonitor;
            _configurationService = configurationService;
            _userPresenceService = userPresenceService;
            _meetingDetectionService = meetingDetectionService;
            _analyticsService = analyticsService;
            _pauseReminderService = pauseReminderService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("Application orchestrator is already initialized");
                return;
            }

            try
            {
                _logger.LogInformation("🚀 Initializing comprehensive application orchestrator with advanced features");

                // Initialize analytics database
                if (!await _analyticsService.IsDatabaseInitializedAsync())
                {
                    await _analyticsService.InitializeDatabaseAsync();
                }
                await _analyticsService.RecordSessionStartAsync();

                // Wire up core timer events
                _timerService.EyeRestWarning += OnEyeRestWarning;
                _timerService.EyeRestDue += OnEyeRestDue;
                _timerService.BreakWarning += OnBreakWarning;
                _timerService.BreakDue += OnBreakDue;
                _logger.LogInformation("✅ Core timer events subscribed");
                
                // Inject services into each other for bidirectional communication
                _timerService.SetNotificationService(_notificationService);
                _notificationService.SetTimerService(_timerService);
                _logger.LogInformation("✅ Bidirectional service injection completed for external countdown control");

                // Wire up advanced service events
                _userPresenceService.UserPresenceChanged += OnUserPresenceChanged;
                _meetingDetectionService.MeetingStateChanged += OnMeetingStateChanged;
                _pauseReminderService.PauseReminderShown += OnPauseReminderShown;
                _pauseReminderService.AutoResumeTriggered += OnAutoResumeTriggered;
                _logger.LogInformation("✅ Advanced service events subscribed");

                // Wire up system tray events
                _systemTrayService.PauseTimersRequested += OnPauseTimersRequested;
                _systemTrayService.ResumeTimersRequested += OnResumeTimersRequested;
                _systemTrayService.ShowTimerStatusRequested += OnShowTimerStatusRequested;
                _systemTrayService.ShowAnalyticsRequested += OnShowAnalyticsRequested;
                _logger.LogInformation("✅ System tray events subscribed");

                // Start advanced services
                await _userPresenceService.StartMonitoringAsync();
                await _meetingDetectionService.StartMonitoringAsync();
                await _pauseReminderService.InitializeAsync();
                await _pauseReminderService.StartMonitoringAsync();
                _logger.LogInformation("✅ Advanced monitoring services started");

                // Update system tray with initial state
                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _systemTrayService.UpdateTimerStatus("Ready");
                
                // ENHANCED: Start periodic timer details update for system tray tooltip
                StartTrayUpdateTimer();

                _isInitialized = true;
                _logger.LogInformation("🎯 Comprehensive application orchestrator initialized successfully - ALL ADVANCED FEATURES ACTIVE");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize application orchestrator");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
                throw;
            }
        }

        public async Task ShutdownAsync()
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Application orchestrator is not initialized");
                return;
            }

            try
            {
                _logger.LogInformation("🛑 Shutting down comprehensive application orchestrator");

                // Record session end
                await _analyticsService.RecordSessionEndAsync();

                // Unsubscribe from core timer events
                _timerService.EyeRestWarning -= OnEyeRestWarning;
                _timerService.EyeRestDue -= OnEyeRestDue;
                _timerService.BreakWarning -= OnBreakWarning;
                _timerService.BreakDue -= OnBreakDue;

                // Unsubscribe from advanced service events
                _userPresenceService.UserPresenceChanged -= OnUserPresenceChanged;
                _meetingDetectionService.MeetingStateChanged -= OnMeetingStateChanged;
                _pauseReminderService.PauseReminderShown -= OnPauseReminderShown;
                _pauseReminderService.AutoResumeTriggered -= OnAutoResumeTriggered;

                // Unsubscribe from system tray events
                _systemTrayService.PauseTimersRequested -= OnPauseTimersRequested;
                _systemTrayService.ResumeTimersRequested -= OnResumeTimersRequested;
                _systemTrayService.ShowTimerStatusRequested -= OnShowTimerStatusRequested;
                _systemTrayService.ShowAnalyticsRequested -= OnShowAnalyticsRequested;

                // Stop advanced services
                await _userPresenceService.StopMonitoringAsync();
                await _meetingDetectionService.StopMonitoringAsync();
                await _pauseReminderService.StopMonitoringAsync();

                // Hide all notifications
                await _notificationService.HideAllNotifications();
                
                // ENHANCED: Stop tray update timer
                StopTrayUpdateTimer();

                // Stop timer service
                await _timerService.StopAsync();

                // Log final performance metrics
                _performanceMonitor.LogPerformanceMetrics();

                // Dispose services
                _userPresenceService.Dispose();
                _meetingDetectionService.Dispose();
                _analyticsService.Dispose();
                _pauseReminderService.Dispose();

                _isInitialized = false;
                _logger.LogInformation("🛑 Comprehensive application orchestrator shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during application orchestrator shutdown");
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
                _logger.LogInformation("👁 Eye rest reminder triggered");
                _systemTrayService.UpdateTrayIcon(TrayIconState.EyeRest);
                _systemTrayService.UpdateTimerStatus("Eye Rest Active");

                // Play start sound
                await _audioService.PlayEyeRestStartSound();

                // Show eye rest notification with actual configuration duration
                var config = await _configurationService.LoadConfigurationAsync();
                var duration = TimeSpan.FromSeconds(config.EyeRest.DurationSeconds);
                var startTime = DateTime.Now;
                
                await _notificationService.ShowEyeRestReminderAsync(duration);
                
                var actualDuration = DateTime.Now - startTime;

                // Play end sound
                await _audioService.PlayEyeRestEndSound();

                // Record analytics event
                await _analyticsService.RecordEyeRestEventAsync(RestEventType.EyeRest, UserAction.Completed, actualDuration);

                // Restart the timer after eye rest completes
                await _timerService.RestartEyeRestTimerAfterCompletion();

                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _systemTrayService.UpdateTimerStatus("Running");
                _logger.LogInformation("👁 Eye rest reminder completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling eye rest reminder");
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

                // Handle user action and record analytics
                var actualDuration = DateTime.Now - DateTime.Now.Subtract(duration); // Approximate duration
                switch (result)
                {
                    case BreakAction.Completed:
                        _logger.LogInformation("🟢 Break completed successfully");
                        await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, duration);
                        await _timerService.RestartBreakTimerAfterCompletion();
                        break;
                    case BreakAction.DelayOneMinute:
                        _logger.LogInformation("🟢 Break delayed by 1 minute");
                        await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Delayed1Min, TimeSpan.Zero);
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(1));
                        break;
                    case BreakAction.DelayFiveMinutes:
                        _logger.LogInformation("🟢 Break delayed by 5 minutes");
                        await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Delayed5Min, TimeSpan.Zero);
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(5));
                        break;
                    case BreakAction.Skipped:
                        _logger.LogInformation("🟢 Break skipped by user");
                        await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Skipped, TimeSpan.Zero);
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

        #region Advanced Service Event Handlers

        private async void OnUserPresenceChanged(object? sender, UserPresenceEventArgs e)
        {
            try
            {
                _logger.LogInformation($"👤 User presence changed: {e.PreviousState} → {e.CurrentState}");
                
                // Record presence change for analytics
                await _analyticsService.RecordPresenceChangeAsync(e.PreviousState, e.CurrentState, e.IdleDuration);
                
                // Handle auto-pause/resume based on presence
                switch (e.CurrentState)
                {
                    case UserPresenceState.Away:
                    case UserPresenceState.SystemSleep:
                        if (_timerService.IsRunning && !_timerService.IsSmartPaused)
                        {
                            var reason = $"User {e.CurrentState.ToString().ToLower()}";
                            await _timerService.SmartPauseAsync(reason);
                            _systemTrayService.UpdateTrayIcon(TrayIconState.UserAway);
                            _systemTrayService.UpdateTimerStatus($"Paused ({reason})");
                            
                            // Notify pause reminder service
                            await _pauseReminderService.OnTimersPausedAsync(reason);
                        }
                        break;
                        
                    case UserPresenceState.Present:
                        if (_timerService.IsSmartPaused)
                        {
                            await _timerService.SmartResumeAsync();
                            _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                            _systemTrayService.UpdateTimerStatus("Running");
                            
                            // Notify pause reminder service
                            await _pauseReminderService.OnTimersResumedAsync();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user presence change");
            }
        }

        private async void OnMeetingStateChanged(object? sender, MeetingStateEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🎥 Meeting state changed - Active: {e.IsMeetingActive}, Meetings: {e.ActiveMeetings.Count}");
                
                if (e.IsMeetingActive && e.ActiveMeetings.Count > 0)
                {
                    var primaryMeeting = e.ActiveMeetings[0];
                    
                    // Record meeting event
                    await _analyticsService.RecordMeetingEventAsync(primaryMeeting, true);
                    
                    // Auto-pause timers during meetings
                    if (_timerService.IsRunning && !_timerService.IsSmartPaused)
                    {
                        var reason = $"Meeting detected ({primaryMeeting.Type})";
                        await _timerService.SmartPauseAsync(reason);
                        _systemTrayService.SetMeetingMode(true, primaryMeeting.Type.ToString());
                        _systemTrayService.UpdateTrayIcon(TrayIconState.MeetingMode); // ENHANCED: Update icon to meeting mode
                        _systemTrayService.UpdateTimerStatus($"Paused (Meeting - {primaryMeeting.Type})");
                        
                        // Notify pause reminder service
                        await _pauseReminderService.OnTimersPausedAsync(reason);
                    }
                }
                else
                {
                    // Meeting ended - resume timers
                    if (_timerService.IsSmartPaused)
                    {
                        await _timerService.SmartResumeAsync();
                        _systemTrayService.SetMeetingMode(false);
                        _systemTrayService.UpdateTimerStatus("Running");
                        
                        // Notify pause reminder service
                        await _pauseReminderService.OnTimersResumedAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling meeting state change");
            }
        }

        private async void OnPauseReminderShown(object? sender, PauseReminderEventArgs e)
        {
            try
            {
                _logger.LogInformation($"🔔 Pause reminder #{e.ReminderCount} shown - paused for {e.PauseDuration.TotalHours:F1} hours, reason: {e.PauseReason}");
                
                // Update system tray to show pause reminder status
                var status = $"Paused {e.PauseDuration.TotalHours:F1}h ({e.PauseReason})";
                _systemTrayService.UpdateTimerStatus(status);
                
                // Could also show additional system tray notification if desired
                var balloonTitle = $"Pause Reminder #{e.ReminderCount}";
                var balloonText = $"Timers paused for {e.PauseDuration.TotalHours:F1} hours\\nReason: {e.PauseReason}\\nConsider resuming for your health!";
                _systemTrayService.ShowBalloonTip(balloonTitle, balloonText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling pause reminder shown event");
            }
        }

        private async void OnAutoResumeTriggered(object? sender, AutoResumeEventArgs e)
        {
            try
            {
                _logger.LogWarning($"🚨 Auto-resume triggered after {e.TotalPauseDuration.TotalHours:F1} hours - forcing timer resume for safety");
                
                // Force resume timers
                if (_timerService.IsRunning && (_timerService.IsPaused || _timerService.IsSmartPaused))
                {
                    await _timerService.ResumeAsync();
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                    _systemTrayService.UpdateTimerStatus("Running (Auto-resumed)");
                    
                    // Show system tray notification about auto-resume
                    _systemTrayService.ShowBalloonTip(
                        "🚨 Safety Auto-Resume", 
                        $"Timers were automatically resumed after {e.TotalPauseDuration.TotalHours:F1} hours for your health and safety.");
                }
                
                _logger.LogInformation("🚨 Auto-resume completed - timers should now be running");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling auto-resume triggered event");
            }
        }

        #endregion

        #region System Tray Event Handlers

        private async void OnPauseTimersRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("⏸️ Manual pause requested from system tray");
                
                if (_timerService.IsRunning && !_timerService.IsPaused)
                {
                    // Show confirmation dialog if enabled
                    var confirmed = await _pauseReminderService.ShowPauseConfirmationAsync("Manual pause from system tray");
                    if (!confirmed)
                    {
                        _logger.LogInformation("⏸️ Manual pause cancelled by user");
                        return;
                    }

                    await _timerService.PauseAsync();
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Paused);
                    _systemTrayService.UpdateTimerStatus("Paused (Manual)");
                    _systemTrayService.ShowBalloonTip("Timers Paused", "Eye rest and break timers have been paused manually.");
                    
                    // Notify pause reminder service
                    await _pauseReminderService.OnTimersPausedAsync("Manual pause from system tray");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling pause timers request");
                _systemTrayService.ShowBalloonTip("Error", "Failed to pause timers. Please try again.");
            }
        }

        private async void OnResumeTimersRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("▶️ Manual resume requested from system tray");
                
                if (_timerService.IsRunning && (_timerService.IsPaused || _timerService.IsSmartPaused))
                {
                    await _timerService.ResumeAsync();
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                    _systemTrayService.UpdateTimerStatus("Running");
                    _systemTrayService.ShowBalloonTip("Timers Resumed", "Eye rest and break timers have been resumed.");
                    
                    // Notify pause reminder service
                    await _pauseReminderService.OnTimersResumedAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling resume timers request");
                _systemTrayService.ShowBalloonTip("Error", "Failed to resume timers. Please try again.");
            }
        }

        private void OnShowTimerStatusRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("📊 Timer status requested from system tray");
                
                var eyeRestTime = _timerService.TimeUntilNextEyeRest;
                var breakTime = _timerService.TimeUntilNextBreak;
                var status = _timerService.IsRunning ? 
                    (_timerService.IsPaused ? "Paused" : 
                     _timerService.IsSmartPaused ? "Smart Paused" : "Running") : "Stopped";
                
                var message = $"Status: {status}\\n" +
                             $"Next Eye Rest: {eyeRestTime:mm\\\\:ss}\\n" +
                             $"Next Break: {breakTime:mm\\\\:ss}";
                
                _systemTrayService.ShowBalloonTip("Timer Status", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing timer status");
                _systemTrayService.ShowBalloonTip("Error", "Failed to retrieve timer status.");
            }
        }

        private async void OnShowAnalyticsRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("📈 Analytics requested from system tray");
                
                // Generate a quick health report
                var healthMetrics = await _analyticsService.GetHealthMetricsAsync(
                    DateTime.Now.AddDays(-7), DateTime.Now);
                
                var message = $"Last 7 Days:\\n" +
                             $"Compliance Rate: {healthMetrics.ComplianceRate:P1}\\n" +
                             $"Breaks Completed: {healthMetrics.BreaksCompleted}\\n" +
                             $"Active Time: {healthMetrics.TotalActiveTime.TotalHours:F1}h";
                
                _systemTrayService.ShowBalloonTip("Health Analytics", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing analytics");
                _systemTrayService.ShowBalloonTip("Error", "Failed to retrieve analytics data.");
            }
        }

        #endregion
        
        #region Enhanced System Tray Timer Updates
        
        /// <summary>
        /// ENHANCED: Start periodic timer to update system tray tooltip with timer details
        /// </summary>
        private void StartTrayUpdateTimer()
        {
            try
            {
                _trayUpdateTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5) // Update every 5 seconds
                };
                
                _trayUpdateTimer.Tick += UpdateTrayTimerDetails;
                _trayUpdateTimer.Start();
                
                // Initial update
                UpdateTrayTimerDetails(null, EventArgs.Empty);
                
                _logger.LogInformation("🎛️ System tray timer update started - tooltip will show live timer details");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎛️ Failed to start system tray timer update");
            }
        }
        
        /// <summary>
        /// Update system tray tooltip with current timer details and accurate status
        /// </summary>
        private void UpdateTrayTimerDetails(object? sender, EventArgs e)
        {
            try
            {
                if (!_isInitialized || _timerService == null)
                    return;
                
                // Get current timer values
                var eyeRestRemaining = _timerService.TimeUntilNextEyeRest;
                var breakRemaining = _timerService.TimeUntilNextBreak;
                
                // ENHANCED: Update status based on actual timer state
                UpdateTimerStatusBasedOnState();
                
                // Update system tray with timer details
                _systemTrayService.UpdateTimerDetails(eyeRestRemaining, breakRemaining);
                
                _logger.LogDebug($"🎛️ Tray tooltip updated - Eye rest: {eyeRestRemaining:mm\\:ss}, Break: {breakRemaining:mm\\:ss}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎛️ Error updating tray timer details");
            }
        }
        
        /// <summary>
        /// Update timer status based on actual timer service state
        /// </summary>
        private void UpdateTimerStatusBasedOnState()
        {
            try
            {
                if (_timerService == null) return;
                
                string newStatus;
                
                if (!_timerService.IsRunning)
                {
                    newStatus = "Stopped";
                }
                else if (_timerService.IsPaused)
                {
                    newStatus = "Paused (Manual)";
                }
                else if (_timerService.IsSmartPaused)
                {
                    newStatus = "Smart Paused";
                }
                else
                {
                    // Timers are running normally
                    newStatus = "Running";
                }
                
                // Only update if status has changed to avoid unnecessary logging
                _systemTrayService.UpdateTimerStatus(newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎛️ Error updating timer status based on state");
            }
        }
        
        /// <summary>
        /// Stop the tray update timer during shutdown
        /// </summary>
        private void StopTrayUpdateTimer()
        {
            try
            {
                if (_trayUpdateTimer != null)
                {
                    _trayUpdateTimer.Stop();
                    _trayUpdateTimer.Tick -= UpdateTrayTimerDetails;
                    _trayUpdateTimer = null;
                    
                    _logger.LogInformation("🎛️ System tray timer update stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎛️ Error stopping tray timer update");
            }
        }
        
        #endregion
    }
}