using System;
using System.Linq;
using System.Threading.Tasks;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
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
        // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
        // private readonly IMeetingDetectionManager _meetingDetectionManager;
        private readonly IAnalyticsService _analyticsService;
        private readonly IPauseReminderService _pauseReminderService;
        private readonly IDonationService _donationService;
        private readonly ITimerFactory _timerFactory;
        private readonly ILogger<ApplicationOrchestrator> _logger;

        // State tracking
        private bool _isInitialized = false;

        // Cached configuration to avoid repeated JSON deserialization on every timer event
        private AppConfiguration? _cachedConfig;

        // ENHANCED: Timer for updating system tray tooltip with timer details
        private ITimer? _trayUpdateTimer;

        // NEW: Timer for validating session activity tracking integrity
        private ITimer? _sessionValidationTimer;

        // Donation usage tracking timer (5-minute interval)
        private ITimer? _usageTrackingTimer;

        public ApplicationOrchestrator(
            ITimerService timerService,
            INotificationService notificationService,
            IAudioService audioService,
            ISystemTrayService systemTrayService,
            IPerformanceMonitor performanceMonitor,
            IConfigurationService configurationService,
            IUserPresenceService userPresenceService,
            // IMeetingDetectionManager meetingDetectionManager, // DISABLED: Meeting detection needs improvement
            IAnalyticsService analyticsService,
            IPauseReminderService pauseReminderService,
            IDonationService donationService,
            ITimerFactory timerFactory,
            ILogger<ApplicationOrchestrator> logger)
        {
            _timerService = timerService;
            _notificationService = notificationService;
            _audioService = audioService;
            _systemTrayService = systemTrayService;
            _performanceMonitor = performanceMonitor;
            _configurationService = configurationService;
            _userPresenceService = userPresenceService;
            // _meetingDetectionManager = meetingDetectionManager; // DISABLED: Meeting detection needs improvement
            _analyticsService = analyticsService;
            _pauseReminderService = pauseReminderService;
            _donationService = donationService;
            _timerFactory = timerFactory;
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
                _logger.LogCritical($"🚀 ORCHESTRATOR INITIALIZATION STARTED at {DateTime.Now:HH:mm:ss.fff} - Process ID: {Environment.ProcessId}");
                _logger.LogInformation("🚀 Initializing comprehensive application orchestrator with advanced features");

                // Initialize analytics database (always run - uses IF NOT EXISTS for safety,
                // ensures any newly added tables are created in existing databases)
                await _analyticsService.InitializeDatabaseAsync();
                await _analyticsService.RecordSessionStartAsync();

                // Cache configuration once at startup and subscribe to changes
                _cachedConfig = await _configurationService.LoadConfigurationAsync();
                _configurationService.ConfigurationChanged += OnConfigurationChanged;

                // Wire up core timer events
                _timerService.EyeRestWarning += OnEyeRestWarning;
                _timerService.EyeRestDue += OnEyeRestDue;
                _timerService.BreakWarning += OnBreakWarning;
                _timerService.BreakDue += OnBreakDue;
                
                // Subscribe to timer state changes for tray icon updates
                _timerService.PropertyChanged += OnTimerServicePropertyChanged;
                
                _logger.LogInformation("✅ Core timer events subscribed");
                
                // Inject services into each other for bidirectional communication
                _timerService.SetNotificationService(_notificationService);
                _timerService.SetUserPresenceService(_userPresenceService); // FIX: Enable extended idle detection
                _notificationService.SetTimerService(_timerService);
                _userPresenceService.SetTimerService(_timerService); // NEW: Enable timer recovery after system resume
                _logger.LogInformation("✅ Bidirectional service injection completed for external countdown control, timer recovery, and extended idle detection");

                // Wire up advanced service events
                _userPresenceService.UserPresenceChanged += OnUserPresenceChanged;
                _userPresenceService.ExtendedAwaySessionDetected += OnExtendedAwaySessionDetected; // NEW: Smart session reset
                // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
                // _meetingDetectionManager.MeetingStateChanged += OnMeetingStateChanged;
                _pauseReminderService.PauseReminderShown += OnPauseReminderShown;
                _pauseReminderService.AutoResumeTriggered += OnAutoResumeTriggered;
                _logger.LogInformation("✅ Advanced service events subscribed");

                // Wire up system tray events
                _systemTrayService.PauseTimersRequested += OnPauseTimersRequested;
                _systemTrayService.ResumeTimersRequested += OnResumeTimersRequested;
                _systemTrayService.PauseForMeetingRequested += OnPauseForMeetingRequested; // NEW
                _systemTrayService.ShowTimerStatusRequested += OnShowTimerStatusRequested;
                _systemTrayService.ShowAnalyticsRequested += OnShowAnalyticsRequested;
                _logger.LogInformation("✅ System tray events subscribed");

                // Start advanced services
                await _userPresenceService.StartMonitoringAsync();
                
                // DISABLED: Meeting detection initialization - not working reliably, needs improvement and testing in future
                // var config = await _configurationService.LoadConfigurationAsync();
                // await _meetingDetectionManager.InitializeAsync(
                //     config.MeetingDetection.DetectionMethod, 
                //     config.MeetingDetection);
                
                await _pauseReminderService.InitializeAsync();
                await _pauseReminderService.StartMonitoringAsync();
                _logger.LogInformation("✅ Advanced monitoring services started");

                // Update system tray with initial state
                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _systemTrayService.UpdateTimerStatus("Ready");
                
                // CRITICAL: Start the timer service - this was missing!
                _logger.LogCritical($"🎯 Starting timer service at {DateTime.Now:HH:mm:ss.fff}");
                await _timerService.StartAsync();
                _logger.LogCritical($"✅ Timer service started successfully - timers are now active");
                
                // ENHANCED: Start periodic timer details update for system tray tooltip
                StartTrayUpdateTimer();
                
                // NEW: Start session validation timer for tracking integrity monitoring
                StartSessionValidationTimer();

                // Initialize donation service and start usage tracking
                await _donationService.InitializeAsync();
                _donationService.IncrementSessionCount();
                StartUsageTrackingTimer();

                _isInitialized = true;
                _logger.LogCritical($"✅ ORCHESTRATOR INITIALIZATION COMPLETED at {DateTime.Now:HH:mm:ss.fff} - ALL SERVICES ACTIVE");
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
                
                // Unsubscribe from timer state changes
                _timerService.PropertyChanged -= OnTimerServicePropertyChanged;

                // Unsubscribe from configuration changes
                _configurationService.ConfigurationChanged -= OnConfigurationChanged;

                // Unsubscribe from advanced service events
                _userPresenceService.UserPresenceChanged -= OnUserPresenceChanged;
                _userPresenceService.ExtendedAwaySessionDetected -= OnExtendedAwaySessionDetected; // NEW: Smart session reset
                // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
                // _meetingDetectionManager.MeetingStateChanged -= OnMeetingStateChanged;
                _pauseReminderService.PauseReminderShown -= OnPauseReminderShown;
                _pauseReminderService.AutoResumeTriggered -= OnAutoResumeTriggered;

                // Unsubscribe from system tray events
                _systemTrayService.PauseTimersRequested -= OnPauseTimersRequested;
                _systemTrayService.ResumeTimersRequested -= OnResumeTimersRequested;
                _systemTrayService.PauseForMeetingRequested -= OnPauseForMeetingRequested; // NEW
                _systemTrayService.ShowTimerStatusRequested -= OnShowTimerStatusRequested;
                _systemTrayService.ShowAnalyticsRequested -= OnShowAnalyticsRequested;

                // Stop advanced services
                await _userPresenceService.StopMonitoringAsync();
                // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
                // await _meetingDetectionManager.ShutdownAsync();
                await _pauseReminderService.StopMonitoringAsync();

                // Hide all notifications
                await _notificationService.HideAllNotifications();
                
                // ENHANCED: Stop tray update timer
                StopTrayUpdateTimer();
                
                // NEW: Stop session validation timer
                StopSessionValidationTimer();

                // Stop usage tracking timer
                _usageTrackingTimer?.Stop();
                _usageTrackingTimer = null;

                // Stop timer service
                await _timerService.StopAsync();

                // Log final performance metrics
                _performanceMonitor.LogPerformanceMetrics();

                // Dispose services
                _userPresenceService.Dispose();
                // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
                // _meetingDetectionManager.Dispose();
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

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            _cachedConfig = e.NewConfiguration;
            _logger.LogDebug("Cached configuration updated from ConfigurationChanged event");
        }

        private async void OnEyeRestWarning(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogInformation("🚨 EYE REST WARNING EVENT FIRED! TimerService event received by ApplicationOrchestrator");
                _logger.LogInformation($"Warning details - TriggeredAt: {e.TriggeredAt}, NextInterval: {e.NextInterval.TotalSeconds} seconds, Type: {e.Type}");
                _logger.LogInformation($"🧠 Smart coordination active: Break timer is paused during eye rest warning to prevent conflicts");
                _timerService.SmartPauseBreakTimerForEyeRest();

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
                _logger.LogInformation("👁 Eye rest reminder triggered with smart timer coordination");
                _logger.LogInformation($"🧠 Smart coordination: Break timer is paused during eye rest to prevent conflicts");
                _systemTrayService.UpdateTrayIcon(TrayIconState.EyeRest);
                _systemTrayService.UpdateTimerStatus("Eye Rest Active");

                // Play start sound
                await _audioService.PlayEyeRestStartSound();

                // Show eye rest notification with actual configuration duration
                var config = _cachedConfig ?? await _configurationService.LoadConfigurationAsync();
                var duration = TimeSpan.FromSeconds(config.EyeRest.DurationSeconds);
                var startTime = DateTime.Now;
                
                await _notificationService.ShowEyeRestReminderAsync(duration);
                
                var actualDuration = DateTime.Now - startTime;

                // Play end sound
                await _audioService.PlayEyeRestEndSound();

                // Record analytics event (skip if in test mode)
                if (!_notificationService.IsTestMode)
                {
                    await _analyticsService.RecordEyeRestEventAsync(RestEventType.EyeRest, UserAction.Completed, actualDuration);
                }
                else
                {
                    _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for eye rest event");
                }

                // Restart the timer after eye rest completes
                await _timerService.RestartEyeRestTimerAfterCompletion();

                // CRITICAL FIX: Resume break timer that was paused during eye rest
                _timerService.SmartResumeBreakTimerAfterEyeRest();

                // SYNC FIX: Clear processing flag to allow backup triggers again
                _timerService.ClearEyeRestProcessingFlag();

                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _systemTrayService.UpdateTimerStatus("Running");
                _logger.LogInformation("👁 Eye rest reminder completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling eye rest reminder");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);

                // SYNC FIX: Clear processing flag on error to prevent permanent blocking
                _timerService.ClearEyeRestProcessingFlag();
            }
        }

        private async void OnBreakWarning(object? sender, TimerEventArgs e)
        {
            try
            {
                _logger.LogCritical("🚨🚨🚨 BREAK WARNING RECEIVED BY ORCHESTRATOR! 🚨🚨🚨");
                _logger.LogCritical($"Break warning triggered with smart timer coordination at {DateTime.Now:HH:mm:ss}");
                _logger.LogInformation($"🧠 Smart coordination: Eye rest timer is paused during break warning to prevent conflicts");

                // Play warning sound
                await _audioService.PlayBreakWarningSound();

                // Show break warning
                _logger.LogCritical("🚨 CALLING NotificationService.ShowBreakWarningAsync...");
                await _notificationService.ShowBreakWarningAsync(e.NextInterval);
                _logger.LogCritical("🚨 NotificationService.ShowBreakWarningAsync COMPLETED");

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
                _logger.LogInformation($"🟢 OnBreakDue EVENT FIRED with smart timer coordination! TriggeredAt: {e.TriggeredAt}, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                _logger.LogInformation($"🧠 Smart coordination: Eye rest timer is paused during break to prevent conflicts");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Break);

                // Show break notification with progress tracking and actual configuration duration
                var progress = new Progress<double>();
                var config = _cachedConfig ?? await _configurationService.LoadConfigurationAsync();
                var duration = TimeSpan.FromMinutes(config.Break.DurationMinutes); // CRITICAL FIX: Use actual config
                _logger.LogInformation($"🟢 Break duration from config: {duration.TotalMinutes} minutes");
                
                _logger.LogInformation("🟢 Calling NotificationService.ShowBreakReminderAsync");
                var result = await _notificationService.ShowBreakReminderAsync(duration, progress);
                _logger.LogInformation($"🟢 ShowBreakReminderAsync returned with result: {result}");

                // Handle user action and record analytics (skip if in test mode)
                var actualDuration = DateTime.Now - DateTime.Now.Subtract(duration); // Approximate duration
                switch (result)
                {
                    case BreakAction.Completed:
                        _logger.LogInformation("🟢 Break completed successfully");
                        if (!_notificationService.IsTestMode)
                        {
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, duration);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break completed event");
                        }

                        // FRESH SESSION: Always reset all timers after break for a fresh working session
                        _logger.LogInformation("🔄 FRESH SESSION: Resetting all timers after break completion");
                        await _timerService.SmartSessionResetAsync("Break completed - starting fresh session");
                        break;
                    case BreakAction.DelayOneMinute:
                        _logger.LogInformation("🟢 Break delayed by 1 minute");
                        if (!_notificationService.IsTestMode)
                        {
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Delayed1Min, TimeSpan.Zero);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break delay 1min event");
                        }
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(1));
                        break;
                    case BreakAction.DelayFiveMinutes:
                        _logger.LogInformation("🟢 Break delayed by 5 minutes");
                        if (!_notificationService.IsTestMode)
                        {
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Delayed5Min, TimeSpan.Zero);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break delay 5min event");
                        }
                        await _timerService.DelayBreak(TimeSpan.FromMinutes(5));
                        break;
                    case BreakAction.Skipped:
                        _logger.LogInformation("🟢 Break skipped by user");
                        if (!_notificationService.IsTestMode)
                        {
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Skipped, TimeSpan.Zero);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break skipped event");
                        }

                        // FRESH SESSION: Always reset all timers after break skip for a fresh working session
                        _logger.LogInformation("🔄 FRESH SESSION: Resetting all timers after break skip");
                        await _timerService.SmartSessionResetAsync("Break skipped - starting fresh session");
                        break;
                    case BreakAction.ConfirmedAfterCompletion:
                        _logger.LogInformation("🟢 Break completion confirmed by user");
                        if (!_notificationService.IsTestMode)
                        {
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, duration);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break confirmed completion event");
                        }

                        // Resume from smart pause first (was paused while waiting for confirmation)
                        if (_timerService.IsSmartPaused)
                        {
                            _logger.LogInformation("🟢 Resuming from smart pause before handling timer restart");
                            await _timerService.SmartResumeAsync();
                        }

                        // FRESH SESSION: Always reset all timers after break confirmation for a fresh working session
                        _logger.LogInformation("🔄 FRESH SESSION: Resetting all timers after break confirmation");
                        await _timerService.SmartSessionResetAsync("User confirmed break completion - starting fresh session");
                        break;
                    case BreakAction.CompletedWithoutConfirmation:
                        _logger.LogInformation("🟡 Break auto-completed without user confirmation (timeout)");
                        if (!_notificationService.IsTestMode)
                        {
                            // Record as completed but with auto-timeout indicator
                            await _analyticsService.RecordBreakEventAsync(RestEventType.Break, UserAction.Completed, duration);
                        }
                        else
                        {
                            _logger.LogInformation("🧪 TEST MODE: Skipping analytics recording for break auto-completion event");
                        }

                        // Resume from smart pause first (was paused while waiting for confirmation)
                        if (_timerService.IsSmartPaused)
                        {
                            _logger.LogInformation("🟡 Resuming from smart pause after auto-timeout");
                            await _timerService.SmartResumeAsync();
                        }

                        // FRESH SESSION: Always reset all timers after break auto-completion for a fresh working session
                        _logger.LogInformation("🔄 FRESH SESSION: Resetting all timers after break auto-completion");
                        await _timerService.SmartSessionResetAsync("Break auto-completed - starting fresh session");
                        break;
                    default:
                        _logger.LogWarning($"🟠 Unhandled break action: {result}");
                        // FRESH SESSION: Reset all timers for any unhandled action
                        _logger.LogInformation("🔄 FRESH SESSION: Resetting all timers for unhandled break action");
                        await _timerService.SmartSessionResetAsync("Break action completed - starting fresh session");
                        break;
                }

                // SYNC FIX: Clear processing flag to allow backup triggers again
                _timerService.ClearBreakProcessingFlag();

                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _logger.LogInformation("🟢 Break reminder completed - OnBreakDue handler finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🟢 ERROR handling break reminder in OnBreakDue");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);

                // SYNC FIX: Clear processing flag on error to prevent permanent blocking
                _timerService.ClearBreakProcessingFlag();
            }
        }

        /// <summary>
        /// Handles timer service property changes to update system tray icon consistently
        /// This ensures tray icon updates when UI buttons change timer states
        /// </summary>
        private void OnTimerServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                // Only update tray icon for state-related property changes
                if (e.PropertyName == nameof(ITimerService.IsPaused) ||
                    e.PropertyName == nameof(ITimerService.IsSmartPaused) ||
                    e.PropertyName == nameof(ITimerService.IsManuallyPaused) ||
                    e.PropertyName == nameof(ITimerService.IsRunning))
                {
                    UpdateTrayIconBasedOnTimerState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tray icon based on timer state changes");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }

        /// <summary>
        /// Updates the system tray icon based on current timer service state
        /// </summary>
        private void UpdateTrayIconBasedOnTimerState()
        {
            try
            {
                if (!_timerService.IsRunning)
                {
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Paused);
                    _systemTrayService.UpdateTimerStatus("Stopped");
                }
                else if (_timerService.IsManuallyPaused)
                {
                    _systemTrayService.UpdateTrayIcon(TrayIconState.ManuallyPaused);
                    
                    // Show remaining time if available
                    var remainingTime = _timerService.ManualPauseRemaining;
                    if (remainingTime.HasValue)
                    {
                        var minutes = (int)remainingTime.Value.TotalMinutes;
                        var seconds = remainingTime.Value.Seconds;
                        _systemTrayService.UpdateTimerStatus($"Meeting Pause ({minutes:D2}:{seconds:D2} remaining)");
                    }
                    else
                    {
                        _systemTrayService.UpdateTimerStatus($"Paused ({_timerService.PauseReason ?? "Manual"})");
                    }
                }
                else if (_timerService.IsPaused)
                {
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Paused);
                    _systemTrayService.UpdateTimerStatus($"Paused ({_timerService.PauseReason ?? "Manual"})");
                }
                else if (_timerService.IsSmartPaused)
                {
                    _systemTrayService.UpdateTrayIcon(TrayIconState.UserAway);
                    _systemTrayService.UpdateTimerStatus($"Paused ({_timerService.PauseReason ?? "User Away"})");
                }
                else
                {
                    _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                    _systemTrayService.UpdateTimerStatus("Running");
                }

                _logger.LogInformation($"🔄 Tray icon updated: Running={_timerService.IsRunning}, Paused={_timerService.IsPaused}, SmartPaused={_timerService.IsSmartPaused}, ManuallyPaused={_timerService.IsManuallyPaused}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray icon based on timer state");
                _systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            }
        }

        #region Advanced Service Event Handlers

        private async void OnUserPresenceChanged(object? sender, UserPresenceEventArgs e)
        {
            try
            {
                _logger.LogCritical($"👤 USER PRESENCE: Changed from {e.PreviousState} → {e.CurrentState} at {DateTime.Now:HH:mm:ss.fff}");
                
                // CRITICAL FIX: Validate state transition to prevent invalid presence changes
                if (e.PreviousState == e.CurrentState)
                {
                    _logger.LogWarning($"👤 USER PRESENCE: Invalid state transition - previous and current state are the same ({e.CurrentState})");
                    return;
                }
                
                // Record presence change for analytics
                await _analyticsService.RecordPresenceChangeAsync(e.PreviousState, e.CurrentState, e.IdleDuration);
                
                // CRITICAL FIX: Clear any active popups on presence state change
                // This prevents popups from staying visible when user is away
                // IMPORTANT: Don't close break popups that require user confirmation
                if (e.CurrentState != UserPresenceState.Present)
                {
                    // Check if we have an active break popup waiting for confirmation
                    if (_notificationService.IsBreakActive)
                    {
                        _logger.LogCritical($"👤 USER PRESENCE: User no longer present but break is active - keeping popup open for confirmation");
                    }
                    else
                    {
                        _logger.LogCritical($"👤 USER PRESENCE: User no longer present - clearing active popups");
                        await _notificationService.HideAllNotifications();
                    }
                }
                
                // ENHANCED: Handle session activity tracking based on presence
                switch (e.CurrentState)
                {
                    case UserPresenceState.Away:
                    case UserPresenceState.SystemSleep:
                    case UserPresenceState.Idle:
                        // Pause analytics session tracking when user becomes inactive
                        var pauseReason = $"User {e.CurrentState.ToString().ToLower()}";
                        await _analyticsService.PauseSessionAsync(e.CurrentState, pauseReason);
                        
                        // CRITICAL FIX: Coordinate smart pause with timer events
                        if (_timerService.IsRunning && !_timerService.IsSmartPaused)
                        {
                            await _timerService.SmartPauseAsync(pauseReason);
                            _systemTrayService.UpdateTrayIcon(TrayIconState.UserAway);
                            _systemTrayService.UpdateTimerStatus($"Paused ({pauseReason})");
                            
                            // Notify pause reminder service
                            await _pauseReminderService.OnTimersPausedAsync(pauseReason);
                        }
                        break;
                        
                    case UserPresenceState.Present:
                        // Resume analytics session tracking when user returns
                        var resumeReason = "User returned";
                        await _analyticsService.ResumeSessionAsync(resumeReason);
                        
                        // CRITICAL FIX: Also check for manual pause state and clear it when user returns
                        if (_timerService.IsManuallyPaused)
                        {
                            _logger.LogCritical($"👤 USER PRESENT FIX: Manual pause active when user returned - clearing manual pause state");
                            
                            // Clear manual pause state explicitly when user returns
                            // This prevents the "Paused (Manual)" UI display bug after extended away periods
                            try
                            {
                                await _timerService.ResumeAsync(); // This will clear manual pause and restart service
                                _logger.LogCritical($"👤 MANUAL PAUSE CLEARED: Timer service resumed, IsRunning={_timerService.IsRunning}, ManuallyPaused={_timerService.IsManuallyPaused}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "👤 Failed to clear manual pause on user return");
                            }
                        }
                        // Also resume timers if they were smart-paused
                        else if (_timerService.IsSmartPaused)
                        {
                            await _timerService.SmartResumeAsync();
                            _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                            _systemTrayService.UpdateTimerStatus("Running");
                            
                            // Notify pause reminder service
                            await _pauseReminderService.OnTimersResumedAsync();
                        }
                        // CRITICAL FIX: Ensure service is running even if no pause states detected
                        else if (!_timerService.IsRunning)
                        {
                            _logger.LogCritical($"👤 USER PRESENT FIX: Timer service stopped with no pause states - ensuring service restart");
                            try
                            {
                                await _timerService.StartAsync();
                                _logger.LogCritical($"👤 SERVICE RESTART: Timer service started, IsRunning={_timerService.IsRunning}");
                                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                                _systemTrayService.UpdateTimerStatus("Running");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "👤 Failed to start timer service on user return");
                            }
                        }
                        
                        // ENHANCED: Log current session metrics and validate tracking integrity
                        var metrics = _analyticsService.GetCurrentSessionMetrics();
                        var validation = _analyticsService.ValidateSessionTracking();
                        
                        _logger.LogInformation($"📊 {metrics.FormattedActivitySummary}");
                        
                        if (!validation.IsValid)
                        {
                            _logger.LogWarning($"⚠️ Session tracking validation issues detected:\n{validation.FormattedReport}");
                        }
                        else
                        {
                            var lastMessage = validation.ValidationMessages?.LastOrDefault() ?? "No validation messages";
                            _logger.LogDebug($"📊 Session validation passed: {lastMessage}");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling user presence change");
            }
        }

        private async void OnExtendedAwaySessionDetected(object? sender, ExtendedAwayEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    _logger.LogWarning("Extended away session event received with null arguments");
                    return;
                }

                var config = _cachedConfig ?? await _configurationService.LoadConfigurationAsync();

                _logger.LogCritical($"🔥 EXTENDED AWAY SESSION DETECTED!");
                _logger.LogCritical($"🔥 Away duration: {e.TotalAwayTime.TotalMinutes:F1} minutes");
                _logger.LogCritical($"🔥 Away start: {e.AwayStartTime:HH:mm:ss}, Return: {e.ReturnTime:HH:mm:ss}");
                _logger.LogCritical($"🔥 Config - EnableSmartSessionReset: {config.UserPresence.EnableSmartSessionReset}");
                _logger.LogCritical($"🔥 Config - ExtendedAwayThresholdMinutes: {config.UserPresence.ExtendedAwayThresholdMinutes}");
                _logger.LogCritical($"🔥 Config - ShowSessionResetNotification: {config.UserPresence.ShowSessionResetNotification}");

                // Check if smart session reset is enabled
                if (!config.UserPresence.EnableSmartSessionReset)
                {
                    _logger.LogCritical($"🚨 SMART SESSION RESET DISABLED - Extended away session detected ({e.TotalAwayTime.TotalMinutes:F1} min) but smart session reset is disabled in config");
                    return;
                }

                // P0 FIX: Unconditionally reset to fresh session when extended away detected
                // Even if timer events were due before absence, clear them and start fresh per requirements
                _logger.LogInformation($"⚡ Extended away session detected: {e.TotalAwayTime.TotalMinutes:F1} minutes away - initiating smart session reset");

                var reason = $"Extended away ({e.TotalAwayTime.TotalMinutes:F0}min) - fresh session";

                // Perform smart session reset unconditionally per P0 requirement
                await _timerService.SmartSessionResetAsync(reason);
                
                // Update system tray to show fresh session
                _systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                _systemTrayService.UpdateTimerStatus("Fresh Session");
                
                // Record analytics event
                await _analyticsService.RecordPresenceChangeAsync(e.AwayState, UserPresenceState.Present, e.TotalAwayTime);
                
                // Show optional notification if enabled
                if (config.UserPresence.ShowSessionResetNotification)
                {
                    _systemTrayService.ShowBalloonTip(
                        "Eye-rest Session Reset", 
                        $"Welcome back! Starting fresh {config.EyeRest.IntervalMinutes}min/{config.Break.IntervalMinutes}min cycle after {e.TotalAwayTime.TotalMinutes:F0}min away.");
                }
                
                _logger.LogInformation($"✅ Smart session reset completed - fresh working session started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling extended away session detection");
            }
        }

        /* DISABLED: Meeting detection not working reliably - needs improvement and testing in future
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
        */

        private async void OnPauseReminderShown(object? sender, PauseReminderEventArgs e)
        {
            await Task.CompletedTask;
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
                
                if (_timerService.IsRunning && (_timerService.IsPaused || _timerService.IsSmartPaused || _timerService.IsManuallyPaused))
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

        // NEW: Handle pause for meeting request
        private async void OnPauseForMeetingRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("🎥 30-minute meeting pause requested from system tray");
                
                if (_timerService.IsRunning && !_timerService.IsManuallyPaused)
                {
                    // Pause for 30 minutes for meeting
                    await _timerService.PauseForDurationAsync(TimeSpan.FromMinutes(30), "Manual meeting pause");
                    _systemTrayService.UpdateTrayIcon(TrayIconState.ManuallyPaused);
                    _systemTrayService.UpdateTimerStatus("Meeting Pause (30 min)");
                    _systemTrayService.ShowBalloonTip("Meeting Pause Active", "Timers paused for 30 minutes. They will auto-resume when the time is up.");
                    
                    // Notify pause reminder service
                    await _pauseReminderService.OnTimersPausedAsync("Manual meeting pause (30 min)");
                }
                else if (_timerService.IsManuallyPaused)
                {
                    _systemTrayService.ShowBalloonTip("Already Paused", "Timers are already paused for a meeting. Use 'Resume Timers' to restart immediately.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling pause for meeting request");
                _systemTrayService.ShowBalloonTip("Error", "Failed to pause timers for meeting. Please try again.");
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
                
                var message = $"Status: {status}\n" +
                             $"Next Eye Rest: {eyeRestTime:mm\\:ss}\n" +
                             $"Next Break: {breakTime:mm\\:ss}";
                
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
                _trayUpdateTimer = _timerFactory.CreateTimer();
                _trayUpdateTimer.Interval = TimeSpan.FromSeconds(5); // Update every 5 seconds

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
                else if (_timerService.IsManuallyPaused)
                {
                    // ENHANCED: Show remaining time for manual pause
                    var remaining = _timerService.ManualPauseRemaining;
                    if (remaining.HasValue && remaining.Value > TimeSpan.Zero)
                    {
                        var minutes = (int)remaining.Value.TotalMinutes;
                        var seconds = remaining.Value.Seconds;
                        newStatus = $"Meeting Pause ({minutes}m {seconds}s left)";
                    }
                    else
                    {
                        newStatus = "Meeting Pause (Manual)";
                    }
                }
                else if (_timerService.IsPaused)
                {
                    newStatus = "Paused (Manual)";
                }
                else if (_timerService.IsSmartPaused)
                {
                    var reason = _timerService.PauseReason ?? "Auto";
                    newStatus = $"Smart Paused ({reason})";
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
        
        /// <summary>
        /// NEW: Start session validation timer for integrity monitoring
        /// </summary>
        private void StartSessionValidationTimer()
        {
            try
            {
                _sessionValidationTimer = _timerFactory.CreateTimer();
                _sessionValidationTimer.Interval = TimeSpan.FromMinutes(15); // Validate every 15 minutes

                _sessionValidationTimer.Tick += async (sender, e) =>
                {
                    try
                    {
                        // CRITICAL FIX: Enhanced session validation with user presence coordination
                        _logger.LogDebug("🔍 VALIDATION: Running periodic session validation");
                        
                        var validation = _analyticsService.ValidateSessionTracking();
                        var metrics = _analyticsService.GetCurrentSessionMetrics();
                        var userPresent = _userPresenceService.IsUserPresent;
                        var timerState = _timerService.IsRunning;
                        
                        // ENHANCED: Validate coordination between services with manual pause state detection
                        var isSmartPaused = _timerService.IsSmartPaused;
                        var isPaused = _timerService.IsPaused;
                        
                        if (userPresent && !timerState)
                        {
                            if (!_timerService.IsManuallyPaused && !isSmartPaused && !isPaused)
                            {
                                _logger.LogCritical($"🚨 MANUAL PAUSE COORDINATION ISSUE DETECTED: User present, timers stopped, but no pause states active");
                                _logger.LogCritical($"🚨 This indicates manual pause was cleared during recovery but timer service coordination failed");
                                _logger.LogCritical($"🚨 State: IsRunning={timerState}, IsManuallyPaused={_timerService.IsManuallyPaused}, IsSmartPaused={isSmartPaused}, IsPaused={isPaused}");
                                
                                // Direct restart instead of recovery to avoid complex recovery logic
                                try
                                {
                                    _logger.LogCritical($"🔧 MANUAL PAUSE COORDINATION FIX: Attempting direct timer service restart");
                                    await _timerService.StartAsync();
                                    _logger.LogCritical($"🔧 MANUAL PAUSE COORDINATION SUCCESS: Timer service restarted after clearing pause states: IsRunning={_timerService.IsRunning}");
                                }
                                catch (Exception startEx)
                                {
                                    _logger.LogError(startEx, "🔧 COORDINATION FIX: Direct restart failed, trying recovery");
                                    
                                    // Fallback to recovery if direct start fails
                                    try
                                    {
                                        await _timerService.RecoverFromSystemResumeAsync("Orchestrator coordination fix - direct start failed");
                                        _logger.LogInformation($"🔧 COORDINATION FIX: Recovery fallback completed");
                                    }
                                    catch (Exception recoveryEx)
                                    {
                                        _logger.LogError(recoveryEx, "🔧 COORDINATION FIX: Both direct start and recovery failed");
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"🔍 VALIDATION: User present but timers stopped due to pause state - Manual={_timerService.IsManuallyPaused}, Smart={isSmartPaused}, Paused={isPaused}");
                            }
                        }
                        
                        if (!validation.IsValid)
                        {
                            _logger.LogWarning($"⚠️ VALIDATION: Periodic session validation failed:\n{validation.FormattedReport}");
                        }
                        else
                        {
                            _logger.LogInformation($"📊 Periodic validation: {metrics.FormattedActivitySummary}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during periodic session validation");
                    }
                };
                
                _sessionValidationTimer.Start();
                _logger.LogInformation("⚙️ Session validation timer started (15-minute intervals)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting session validation timer");
            }
        }
        
        private void StartUsageTrackingTimer()
        {
            try
            {
                _usageTrackingTimer = _timerFactory.CreateTimer();
                _usageTrackingTimer.Interval = TimeSpan.FromMinutes(5);
                _usageTrackingTimer.Tick += (_, _) =>
                {
                    if (_timerService.IsRunning)
                        _donationService.AddUsageMinutes(5);
                };
                _usageTrackingTimer.Start();
                _logger.LogDebug("Usage tracking timer started (5-minute intervals)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start usage tracking timer");
            }
        }

        /// <summary>
        /// NEW: Stop session validation timer
        /// </summary>
        private void StopSessionValidationTimer()
        {
            try
            {
                if (_sessionValidationTimer != null)
                {
                    _sessionValidationTimer.Stop();
                    _sessionValidationTimer.Dispose();
                    _sessionValidationTimer = null;
                    _logger.LogInformation("⚙️ Session validation timer stopped and disposed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping session validation timer");
            }
        }
        
        #endregion
    }
}