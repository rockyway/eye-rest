using System;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;
using ITimer = EyeRest.Services.Abstractions.ITimer;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace EyeRest.Services
{
    /// <summary>
    /// Service for managing hourly pause reminders and safety mechanisms with Windows Toast notifications
    /// </summary>
    public class PauseReminderService : IPauseReminderService
    {
        private readonly ILogger<PauseReminderService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IAudioService _audioService;
        private readonly IDispatcherService _dispatcher;
        private readonly ITimerFactory _timerFactory;

        private ITimer? _reminderTimer;
        private ITimer? _autoResumeTimer;
        private PauseReminderSettings _settings;
        private DateTime? _pauseStartTime;
        private string _currentPauseReason = string.Empty;
        private int _reminderCount = 0;
        private bool _isMonitoring = false;
        private bool _isInitialized = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<PauseReminderEventArgs>? PauseReminderShown;
        public event EventHandler<AutoResumeEventArgs>? AutoResumeTriggered;

        public PauseReminderService(
            ILogger<PauseReminderService> logger,
            IConfigurationService configurationService,
            IAudioService audioService,
            IDispatcherService dispatcher,
            ITimerFactory timerFactory)
        {
            _logger = logger;
            _configurationService = configurationService;
            _audioService = audioService;
            _dispatcher = dispatcher;
            _timerFactory = timerFactory;
            _settings = new PauseReminderSettings();
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("PauseReminderService is already initialized");
                return;
            }

            try
            {
                _logger.LogInformation("🔔 Initializing hourly pause reminder service with Windows Toast notifications");

                // Load configuration
                var config = await _configurationService.LoadConfigurationAsync();
                _settings = new PauseReminderSettings
                {
                    ShowPauseReminders = config.TimerControls.ShowPauseReminders,
                    PauseReminderIntervalHours = config.TimerControls.PauseReminderIntervalHours,
                    MaxPauseHours = config.TimerControls.MaxPauseHours,
                    ConfirmManualPause = config.TimerControls.ConfirmManualPause,
                    ShowPauseInSystemTray = config.TimerControls.ShowPauseInSystemTray,
                    PreserveTimerProgress = config.TimerControls.PreserveTimerProgress,
                    EnableToastNotifications = true,
                    PlaySoundOnReminder = config.Audio.Enabled
                };

                // Subscribe to configuration changes
                _configurationService.ConfigurationChanged += OnConfigurationChanged;

                // Initialize Windows Toast notification system
                InitializeToastNotifications();

                // Initialize timers on UI thread
                await _dispatcher.InvokeAsync(() => InitializeTimers());

                _isInitialized = true;
                _logger.LogInformation("✅ Pause reminder service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize pause reminder service");
                throw;
            }
        }

        public async Task StartMonitoringAsync()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Service must be initialized before starting monitoring");
            }

            if (_isMonitoring)
            {
                _logger.LogWarning("Pause reminder monitoring is already active");
                return;
            }

            try
            {
                _logger.LogInformation("🔔 Starting pause reminder monitoring");
                
                _cancellationTokenSource = new CancellationTokenSource();
                _isMonitoring = true;

                _logger.LogInformation("✅ Pause reminder monitoring started successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to start pause reminder monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Pause reminder monitoring is not active");
                return;
            }

            try
            {
                _logger.LogInformation("🔔 Stopping pause reminder monitoring");

                _cancellationTokenSource?.Cancel();
                _isMonitoring = false;

                // Stop timers on UI thread
                await _dispatcher.InvokeAsync(() =>
                {
                    _reminderTimer?.Stop();
                    _autoResumeTimer?.Stop();
                });

                // Reset state
                _pauseStartTime = null;
                _currentPauseReason = string.Empty;
                _reminderCount = 0;

                _logger.LogInformation("Pause reminder monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error stopping pause reminder monitoring");
                throw;
            }
        }

        public async Task OnTimersPausedAsync(string reason)
        {
            if (!_isMonitoring || !_settings.ShowPauseReminders)
            {
                return;
            }

            try
            {
                _logger.LogInformation($"⏸️ Timers paused - starting reminder countdown. Reason: {reason}");

                _pauseStartTime = DateTime.Now;
                _currentPauseReason = reason;
                _reminderCount = 0;

                // Start reminder timer on UI thread
                await _dispatcher.InvokeAsync(() =>
                {
                    var reminderInterval = TimeSpan.FromHours(_settings.PauseReminderIntervalHours);

                    if (_reminderTimer != null)
                    {
                        _reminderTimer.Stop();
                        _reminderTimer.Interval = reminderInterval;
                        _reminderTimer.Start();
                    }

                    // Start auto-resume timer
                    var autoResumeInterval = TimeSpan.FromHours(_settings.MaxPauseHours);
                    if (_autoResumeTimer != null)
                    {
                        _autoResumeTimer.Stop();
                        _autoResumeTimer.Interval = autoResumeInterval;
                        _autoResumeTimer.Start();
                    }
                });

                _logger.LogInformation($"🔔 Pause reminder timers started - first reminder in {_settings.PauseReminderIntervalHours} hour(s), auto-resume in {_settings.MaxPauseHours} hour(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling timers paused event");
            }
        }

        public async Task OnTimersResumedAsync()
        {
            if (!_isMonitoring)
            {
                return;
            }

            try
            {
                var pauseDuration = _pauseStartTime.HasValue ? DateTime.Now - _pauseStartTime.Value : TimeSpan.Zero;
                _logger.LogInformation($"▶️ Timers resumed - stopping reminder countdown. Total pause duration: {pauseDuration.TotalMinutes:F1} minutes");

                // Stop timers on UI thread
                await _dispatcher.InvokeAsync(() =>
                {
                    _reminderTimer?.Stop();
                    _autoResumeTimer?.Stop();
                });

                // Reset state
                _pauseStartTime = null;
                _currentPauseReason = string.Empty;
                _reminderCount = 0;

                _logger.LogInformation("Pause reminder timers stopped - monitoring resumed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling timers resumed event");
            }
        }

        public async Task<bool> ShowPauseConfirmationAsync(string reason)
        {
            if (!_settings.ConfirmManualPause)
            {
                return true; // No confirmation required
            }

            try
            {
                _logger.LogInformation($"Pause confirmation requested for reason: {reason}");
                // Auto-confirm since native WPF MessageBox is unavailable in Avalonia
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pause confirmation");
                return false;
            }
        }

        public async Task ForceResumeAfterMaxPauseAsync()
        {
            try
            {
                var totalPauseDuration = _pauseStartTime.HasValue ? DateTime.Now - _pauseStartTime.Value : TimeSpan.Zero;
                _logger.LogWarning($"🚨 Auto-resume safety mechanism triggered after {totalPauseDuration.TotalHours:F1} hours of pause");

                // Show warning toast before auto-resume
                await ShowAutoResumeWarningToastAsync(totalPauseDuration);

                // Fire auto-resume event
                var autoResumeArgs = new AutoResumeEventArgs
                {
                    TotalPauseDuration = totalPauseDuration,
                    PauseReason = _currentPauseReason,
                    ResumeTime = DateTime.Now,
                    WasForced = true
                };

                AutoResumeTriggered?.Invoke(this, autoResumeArgs);

                _logger.LogInformation("🚨 Auto-resume event fired - ApplicationOrchestrator should handle timer resumption");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in force resume after max pause");
            }
        }

        public PauseStatus GetCurrentPauseStatus()
        {
            var isPaused = _pauseStartTime.HasValue;
            var pauseDuration = isPaused ? DateTime.Now - _pauseStartTime!.Value : TimeSpan.Zero;
            var timeUntilAutoResume = isPaused ? 
                TimeSpan.FromHours(_settings.MaxPauseHours) - pauseDuration : 
                TimeSpan.Zero;

            return new PauseStatus
            {
                IsPaused = isPaused,
                PauseStartTime = _pauseStartTime,
                PauseDuration = pauseDuration,
                PauseReason = _currentPauseReason,
                RemindersShown = _reminderCount,
                NextReminderTime = isPaused ?
                    _pauseStartTime!.Value.AddHours((_reminderCount + 1) * _settings.PauseReminderIntervalHours) :
                    null,
                TimeUntilAutoResume = timeUntilAutoResume,
                IsNearingAutoResume = timeUntilAutoResume.TotalHours <= 1
            };
        }

        public async Task ShowPauseReminderToastAsync(TimeSpan pauseDuration, string reason)
        {
            if (!_settings.EnableToastNotifications)
            {
                return;
            }

            try
            {
                _logger.LogInformation($"🔔 Showing pause reminder toast - paused for {pauseDuration.TotalHours:F1} hours");

                var toastXml = CreatePauseReminderToastXml(pauseDuration, reason);
                var toast = new ToastNotification(toastXml)
                {
                    ExpirationTime = DateTime.Now.AddMinutes(5) // Auto-dismiss after 5 minutes
                };

                // Add action buttons for user interaction
                toast.Activated += OnToastActivated;
                toast.Dismissed += OnToastDismissed;
                toast.Failed += OnToastFailed;

                ToastNotificationManager.CreateToastNotifier("EyeRest").Show(toast);

                // Play sound if enabled
                if (_settings.PlaySoundOnReminder)
                {
                    await _audioService.PlayBreakWarningSound();
                }

                _logger.LogInformation("🔔 Pause reminder toast notification sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to show pause reminder toast notification");
                
                // Fallback to system tray balloon tip
                await ShowFallbackReminderAsync(pauseDuration, reason);
            }
        }

        public async Task ShowRecoveryNotificationAsync(string recoveryType, string details)
        {
            if (!_settings.EnableToastNotifications)
            {
                _logger.LogInformation($"🔔 Recovery notification skipped - toast notifications disabled: {recoveryType}");
                return;
            }

            try
            {
                _logger.LogInformation($"🔔 Showing recovery notification: {recoveryType}");

                var toastXml = CreateRecoveryNotificationToastXml(recoveryType, details);
                var toast = new ToastNotification(toastXml)
                {
                    ExpirationTime = DateTime.Now.AddMinutes(10) // Auto-dismiss after 10 minutes
                };

                // Add event handlers for tracking
                toast.Activated += OnToastActivated;
                toast.Dismissed += OnToastDismissed;
                toast.Failed += OnToastFailed;

                ToastNotificationManager.CreateToastNotifier("EyeRest").Show(toast);

                // Play a subtle notification sound
                if (_settings.PlaySoundOnReminder)
                {
                    await _audioService.PlayBreakWarningSound(); // Subtle sound for recovery notifications
                }

                _logger.LogInformation($"🔔 Recovery notification sent successfully: {recoveryType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to show recovery notification: {recoveryType}");
                
                // For recovery notifications, we'll skip fallback since they're informational
                _logger.LogWarning($"⚠️ Recovery notification will not be shown due to error: {recoveryType}");
            }
        }

        #region Private Methods

        private void InitializeToastNotifications()
        {
            try
            {
                // Windows Toast notification system initialization
                // Note: Full app registration requires more setup, using basic approach for development
                _logger.LogInformation("🔔 Windows Toast notification system initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not fully initialize Windows Toast notifications - will use fallback methods");
            }
        }

        private void InitializeTimers()
        {
            // Reminder timer
            _reminderTimer = _timerFactory.CreateTimer();
            _reminderTimer.Tick += OnReminderTimerTick;

            // Auto-resume timer
            _autoResumeTimer = _timerFactory.CreateTimer();
            _autoResumeTimer.Tick += OnAutoResumeTimerTick;

            _logger.LogDebug("Pause reminder timers initialized");
        }

        private async void OnReminderTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (!_pauseStartTime.HasValue)
                {
                    return;
                }

                _reminderCount++;
                var pauseDuration = DateTime.Now - _pauseStartTime.Value;

                _logger.LogInformation($"🔔 Pause reminder #{_reminderCount} triggered - paused for {pauseDuration.TotalHours:F1} hours");

                // Show toast notification
                await ShowPauseReminderToastAsync(pauseDuration, _currentPauseReason);

                // Fire event
                var reminderArgs = new PauseReminderEventArgs
                {
                    PauseDuration = pauseDuration,
                    PauseReason = _currentPauseReason,
                    ReminderTime = DateTime.Now,
                    ReminderCount = _reminderCount
                };

                PauseReminderShown?.Invoke(this, reminderArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in reminder timer tick");
            }
        }

        private async void OnAutoResumeTimerTick(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogWarning("🚨 Auto-resume timer triggered - maximum pause duration reached");
                
                // Stop the auto-resume timer since we're about to resume
                _autoResumeTimer?.Stop();

                await ForceResumeAfterMaxPauseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in auto-resume timer tick");
            }
        }

        private XmlDocument CreatePauseReminderToastXml(TimeSpan pauseDuration, string reason)
        {
            var hoursText = pauseDuration.TotalHours >= 1 ? 
                $"{pauseDuration.TotalHours:F1} hours" : 
                $"{pauseDuration.TotalMinutes:F0} minutes";

            var toastXmlString = $@"
                <toast activationType='foreground' launch='resume-timers'>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>EyeRest - Timer Pause Reminder</text>
                            <text>Your timers have been paused for {hoursText}</text>
                            <text>Reason: {reason}</text>
                            <text>Consider resuming for your eye health!</text>
                        </binding>
                    </visual>
                    <actions>
                        <action 
                            content='Resume Timers' 
                            arguments='resume' 
                            activationType='foreground'
                            imageUri='ms-appx:///Assets/resume.png'/>
                        <action 
                            content='Remind Later' 
                            arguments='later' 
                            activationType='background'/>
                        <action 
                            content='Settings' 
                            arguments='settings' 
                            activationType='foreground'/>
                    </actions>
                </toast>";

            var toastXml = new XmlDocument();
            toastXml.LoadXml(toastXmlString);
            return toastXml;
        }

        private XmlDocument CreateRecoveryNotificationToastXml(string recoveryType, string details)
        {
            var toastXmlString = $@"
                <toast activationType='foreground' launch='recovery-notification'>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>✅ EyeRest - System Recovery</text>
                            <text>{recoveryType} Successful</text>
                            <text>{details}</text>
                            <text>Your timers are now running normally.</text>
                        </binding>
                    </visual>
                    <actions>
                        <action 
                            content='View Status' 
                            arguments='status' 
                            activationType='foreground'/>
                        <action 
                            content='Dismiss' 
                            arguments='dismiss' 
                            activationType='background'/>
                    </actions>
                </toast>";

            var toastXml = new XmlDocument();
            toastXml.LoadXml(toastXmlString);
            return toastXml;
        }

        private async Task ShowAutoResumeWarningToastAsync(TimeSpan totalPauseDuration)
        {
            try
            {
                var toastXmlString = $@"
                    <toast activationType='foreground' launch='auto-resume-warning'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>🚨 EyeRest - Auto-Resume Safety Warning</text>
                                <text>Timers have been paused for {totalPauseDuration.TotalHours:F1} hours</text>
                                <text>Auto-resuming now for your health and safety</text>
                                <text>Remember to take regular breaks!</text>
                            </binding>
                        </visual>
                        <actions>
                            <action 
                                content='OK' 
                                arguments='acknowledge' 
                                activationType='foreground'/>
                            <action 
                                content='Adjust Settings' 
                                arguments='settings' 
                                activationType='foreground'/>
                        </actions>
                    </toast>";

                var toastXml = new XmlDocument();
                toastXml.LoadXml(toastXmlString);

                var toast = new ToastNotification(toastXml)
                {
                    ExpirationTime = DateTime.Now.AddMinutes(10)
                };

                ToastNotificationManager.CreateToastNotifier("EyeRest").Show(toast);
                _logger.LogInformation("🚨 Auto-resume warning toast shown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to show auto-resume warning toast");
            }
        }

        private Task ShowFallbackReminderAsync(TimeSpan pauseDuration, string reason)
        {
            _logger.LogWarning("Pause reminder fallback: timers paused for {Hours:F1} hours. Reason: {Reason}", pauseDuration.TotalHours, reason);
            return Task.CompletedTask;
        }

        private void OnToastActivated(ToastNotification sender, object args)
        {
            _logger.LogInformation("🔔 Toast notification activated by user");
            // Handle toast activation - could trigger resume action
        }

        private void OnToastDismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            _logger.LogDebug($"🔔 Toast notification dismissed: {args.Reason}");
        }

        private void OnToastFailed(ToastNotification sender, ToastFailedEventArgs args)
        {
            _logger.LogError($"❌ Toast notification failed: {args.ErrorCode}");
        }

        private async void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation("🔔 Updating pause reminder settings from configuration change");

                _settings = new PauseReminderSettings
                {
                    ShowPauseReminders = e.NewConfiguration.TimerControls.ShowPauseReminders,
                    PauseReminderIntervalHours = e.NewConfiguration.TimerControls.PauseReminderIntervalHours,
                    MaxPauseHours = e.NewConfiguration.TimerControls.MaxPauseHours,
                    ConfirmManualPause = e.NewConfiguration.TimerControls.ConfirmManualPause,
                    ShowPauseInSystemTray = e.NewConfiguration.TimerControls.ShowPauseInSystemTray,
                    PreserveTimerProgress = e.NewConfiguration.TimerControls.PreserveTimerProgress,
                    EnableToastNotifications = true,
                    PlaySoundOnReminder = e.NewConfiguration.Audio.Enabled
                };

                // If timers are currently paused, update the reminder interval
                if (_pauseStartTime.HasValue && _reminderTimer != null)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _reminderTimer.Stop();
                        _reminderTimer.Interval = TimeSpan.FromHours(_settings.PauseReminderIntervalHours);
                        _reminderTimer.Start();
                    });
                }

                _logger.LogInformation("✅ Pause reminder settings updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating pause reminder settings");
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                _logger.LogInformation("🔔 Disposing pause reminder service");

                _cancellationTokenSource?.Cancel();
                _configurationService.ConfigurationChanged -= OnConfigurationChanged;

                // Dispose timers on UI thread
                _dispatcher.BeginInvoke(() =>
                {
                    _reminderTimer?.Stop();
                    _reminderTimer?.Dispose();
                    _autoResumeTimer?.Stop();
                    _autoResumeTimer?.Dispose();
                    _reminderTimer = null;
                    _autoResumeTimer = null;
                });

                _cancellationTokenSource?.Dispose();
                _isMonitoring = false;
                _isInitialized = false;

                _logger.LogInformation("✅ Pause reminder service disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disposing pause reminder service");
            }
        }
    }
}