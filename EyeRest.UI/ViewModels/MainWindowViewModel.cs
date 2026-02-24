using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace EyeRest.UI.ViewModels
{
    public class MainWindowViewModel : EyeRest.ViewModels.ViewModelBase, IDisposable
    {
        private readonly IConfigurationService _configurationService;
        private readonly ITimerConfigurationService _timerConfigurationService;
        private readonly IUIConfigurationService _uiConfigurationService;
        private readonly ITimerService _timerService;
        private readonly IStartupManager _startupManager;
        private readonly INotificationService _notificationService;
        private readonly IScreenOverlayService _screenOverlayService;
        private readonly ILogger<MainWindowViewModel> _logger;

        private AppConfiguration _configuration;
        private AppConfiguration _originalConfiguration;

        // Debounced timer restart mechanism
        private DispatcherTimer? _settingsDebounceTimer;
        private const int SETTINGS_DEBOUNCE_DELAY_MS = 1500; // 1.5 seconds delay

        // Eye Rest Settings
        private int _eyeRestIntervalMinutes = 20;
        private int _eyeRestDurationSeconds = 20;
        private bool _eyeRestStartSoundEnabled = true;
        private bool _eyeRestEndSoundEnabled = true;
        private bool _eyeRestWarningEnabled = true;
        private int _eyeRestWarningSeconds = 30;

        // Break Settings
        private int _breakIntervalMinutes = 55;
        private int _breakDurationMinutes = 5;
        private bool _breakWarningEnabled = true;
        private int _breakWarningSeconds = 30;
        private int _overlayOpacityPercent = 50;
        private bool _requireConfirmationAfterBreak = true;
        private bool _resetTimersOnBreakConfirmation = true;

        // Audio Settings
        private bool _audioEnabled = true;
        private int _audioVolume = 50;
        private string? _customSoundPath;

        // Application Settings
        private bool _startWithWindows = false;
        private bool _minimizeToTray = true;
        private bool _startMinimized = false;
        private bool _showTrayNotifications = true;
        private bool _showInTaskbar = false;
        private bool _autoOpenDashboard = false;

        // UI State
        private int _selectedTabIndex = 0;

        // Timer Status
        private string _timerStatusText = "Stopped";
        private string _timerStatusColor = "#F44336";
        private string _windowTitle = "Eye-rest Settings - Stopped";

        // Countdown Properties
        private string _timeUntilNextBreak = "--";
        private string _timeUntilNextEyeRest = "--";
        private string _dualCountdownText = "Timers not running";
        private bool _isRunning = false;

        // Error Indicators
        private string _errorMessage = "";
        private bool _hasValidationErrors = false;

        // Save State
        private bool _isSaving = false;

        // Meeting Detection Properties
        private MeetingDetectionMethod _meetingDetectionMethod = MeetingDetectionMethod.WindowBased;
        private bool _logDetectionActivity = false;
        private bool _enableFallbackDetection = true;
        private string _meetingDetectionStatus = "Initializing...";
        private string _meetingDetectionStatusColor = "#FFA500";
        private string _meetingDetectionStatusText = "Loading...";

        // User Presence Properties
        private bool _pauseOnScreenLock = true;
        private bool _pauseOnMonitorOff = true;
        private bool _pauseOnIdle = true;
        private int _idleTimeoutMinutes = 15;

        public MainWindowViewModel(
            IConfigurationService configurationService,
            ITimerConfigurationService timerConfigurationService,
            IUIConfigurationService uiConfigurationService,
            ITimerService timerService,
            IStartupManager startupManager,
            INotificationService notificationService,
            IScreenOverlayService screenOverlayService,
            ILogger<MainWindowViewModel> logger)
        {
            _configurationService = configurationService;
            _timerConfigurationService = timerConfigurationService;
            _uiConfigurationService = uiConfigurationService;
            _timerService = timerService;
            _startupManager = startupManager;
            _notificationService = notificationService;
            _screenOverlayService = screenOverlayService;
            _logger = logger;

            _configuration = new AppConfiguration();
            _originalConfiguration = new AppConfiguration();

            // Initialize commands using CrossPlatformRelayCommand (no WPF CommandManager dependency)
            RestoreDefaultsCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await RestoreDefaults());
            StartTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await StartTimers());
            StopTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await StopTimers());

            PauseTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await PauseTimers(), () => CanPauseTimers);
            ResumeTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await ResumeTimers(), () => CanResumeTimers);
            PauseForMeetingCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await PauseForMeeting(), () => CanPauseMeeting);
            PauseForMeeting1hCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await PauseForMeeting1h(), () => CanPauseMeeting);

            ExitApplicationCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(ExitApplication);
            ShowAnalyticsCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(ShowAnalyticsWindow);
            BrowseCustomAudioCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(BrowseCustomAudio);
            TestCustomAudioCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestAudio());

            // Test commands
            TestWarningCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestWarningPopup());
            TestPopupCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestEyeRestPopup());
            TestBreakWarningCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestBreakWarningPopup());
            TestBreakCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestBreakPopup());

            // Advanced feature commands
            TestMeetingDetectionCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await TestMeetingDetection());
            SwitchDetectionMethodCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await SwitchDetectionMethod());

            // Subscribe to timer service events to update status
            _timerService.PropertyChanged += OnTimerServicePropertyChanged;

            // Initialize debounce timer for graceful timer restarts
            InitializeDebounceTimer();

            // Load configuration asynchronously
            try
            {
                _logger.LogInformation("Starting async configuration loading");
                LoadConfigurationAsync();
                _logger.LogInformation("Configuration loading started - UI will update when loaded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start configuration loading");
            }
        }

        #region Properties

        // Eye Rest Properties
        public int EyeRestIntervalMinutes
        {
            get => _eyeRestIntervalMinutes;
            set
            {
                if (SetProperty(ref _eyeRestIntervalMinutes, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public int EyeRestDurationSeconds
        {
            get => _eyeRestDurationSeconds;
            set
            {
                if (SetProperty(ref _eyeRestDurationSeconds, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public bool EyeRestStartSoundEnabled
        {
            get => _eyeRestStartSoundEnabled;
            set
            {
                if (SetProperty(ref _eyeRestStartSoundEnabled, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public bool EyeRestEndSoundEnabled
        {
            get => _eyeRestEndSoundEnabled;
            set
            {
                if (SetProperty(ref _eyeRestEndSoundEnabled, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public bool EyeRestWarningEnabled
        {
            get => _eyeRestWarningEnabled;
            set
            {
                if (SetProperty(ref _eyeRestWarningEnabled, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public int EyeRestWarningSeconds
        {
            get => _eyeRestWarningSeconds;
            set
            {
                if (SetProperty(ref _eyeRestWarningSeconds, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        // Break Properties
        public int BreakIntervalMinutes
        {
            get => _breakIntervalMinutes;
            set
            {
                if (SetProperty(ref _breakIntervalMinutes, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public int BreakDurationMinutes
        {
            get => _breakDurationMinutes;
            set
            {
                if (SetProperty(ref _breakDurationMinutes, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public bool BreakWarningEnabled
        {
            get => _breakWarningEnabled;
            set
            {
                if (SetProperty(ref _breakWarningEnabled, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public int BreakWarningSeconds
        {
            get => _breakWarningSeconds;
            set
            {
                if (SetProperty(ref _breakWarningSeconds, value))
                {
                    DebouncedSaveTimerSetting();
                }
            }
        }

        public int OverlayOpacityPercent
        {
            get => _overlayOpacityPercent;
            set
            {
                if (SetProperty(ref _overlayOpacityPercent, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveOverlayOpacityAsync());
                }
            }
        }

        public bool RequireConfirmationAfterBreak
        {
            get => _requireConfirmationAfterBreak;
            set
            {
                if (SetProperty(ref _requireConfirmationAfterBreak, value))
                {
                    CheckForChanges();
                }
            }
        }

        public bool ResetTimersOnBreakConfirmation
        {
            get => _resetTimersOnBreakConfirmation;
            set
            {
                if (SetProperty(ref _resetTimersOnBreakConfirmation, value))
                {
                    CheckForChanges();
                }
            }
        }

        // Audio Properties
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                if (SetProperty(ref _audioEnabled, value))
                {
                    _ = Task.Run(async () => await SaveAudioEnabledAsync(value));
                }
            }
        }

        public int AudioVolume
        {
            get => _audioVolume;
            set
            {
                if (SetProperty(ref _audioVolume, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveAudioVolumeAsync());
                }
            }
        }

        public string? CustomSoundPath
        {
            get => _customSoundPath;
            set
            {
                if (SetProperty(ref _customSoundPath, value))
                {
                    _ = Task.Run(async () => await SaveCustomSoundPathAsync(value));
                }
            }
        }

        // Application Properties
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (SetProperty(ref _startWithWindows, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveStartupSettingAsync(value));
                }
            }
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (SetProperty(ref _minimizeToTray, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveMinimizeToTrayAsync(value));
                }
            }
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set
            {
                if (SetProperty(ref _startMinimized, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveStartMinimizedAsync(value));
                }
            }
        }

        public bool ShowTrayNotifications
        {
            get => _showTrayNotifications;
            set
            {
                if (SetProperty(ref _showTrayNotifications, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveShowTrayNotificationsAsync(value));
                }
            }
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set
            {
                if (SetProperty(ref _showInTaskbar, value))
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveShowInTaskbarAsync(value));
                }
            }
        }

        private bool _isDarkMode = false;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    CheckForChanges();
                    ApplyThemeGlobally(value);
                    _ = Task.Run(async () => await SaveDarkModeAsync(value));
                }
            }
        }

        public bool AutoOpenDashboard
        {
            get => _autoOpenDashboard;
            set
            {
                if (SetProperty(ref _autoOpenDashboard, value))
                {
                    CheckForChanges();
                    _ = SaveAutoOpenSettingAsync(value);
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    // If Analytics tab is selected (index 3) and auto-open is enabled
                    if (value == 3 && AutoOpenDashboard)
                    {
                        ShowAnalyticsWindow();
                    }
                }
            }
        }

        // Timer Status Properties
        public string TimerStatusText
        {
            get => _timerStatusText;
            private set => SetProperty(ref _timerStatusText, value);
        }

        public string TimerStatusColor
        {
            get => _timerStatusColor;
            private set => SetProperty(ref _timerStatusColor, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            private set => SetProperty(ref _windowTitle, value);
        }

        // Countdown Properties
        public string TimeUntilNextBreak
        {
            get => _timeUntilNextBreak;
            private set => SetProperty(ref _timeUntilNextBreak, value);
        }

        public string TimeUntilNextEyeRest
        {
            get => _timeUntilNextEyeRest;
            private set => SetProperty(ref _timeUntilNextEyeRest, value);
        }

        public string DualCountdownText
        {
            get => _dualCountdownText;
            private set => SetProperty(ref _dualCountdownText, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        // Can-Execute Properties for Pause/Resume Commands
        public bool CanPauseTimers => _timerService.IsRunning && !_timerService.IsPaused && !_timerService.IsSmartPaused && !_timerService.IsManuallyPaused;

        public bool CanResumeTimers => _timerService.IsRunning && (_timerService.IsPaused || _timerService.IsManuallyPaused);

        public bool CanPauseMeeting => _timerService.IsRunning && !_timerService.IsManuallyPaused;

        // Tooltip Properties for Control Buttons
        public string StartButtonTooltip => _timerService.IsRunning
            ? "Timers are already running"
            : "Start the eye rest and break timers";

        public string StopButtonTooltip => !_timerService.IsRunning
            ? "Timers are not running"
            : "Stop all timers";

        public string PauseButtonTooltip => !CanPauseTimers
            ? (_timerService.IsPaused || _timerService.IsManuallyPaused
                ? "Timers are already paused"
                : !_timerService.IsRunning
                    ? "Timers are not running"
                    : "Cannot pause timers")
            : "Pause timers manually (can be resumed anytime)";

        public string ResumeButtonTooltip => !CanResumeTimers
            ? (!_timerService.IsRunning
                ? "Timers are not running"
                : "Timers are not paused")
            : "Resume paused timers";

        public string Meeting30mButtonTooltip => !CanPauseMeeting
            ? (!_timerService.IsRunning
                ? "Timers are not running"
                : "Meeting pause is already active")
            : "Pause timers for 30 minutes for meetings (auto-resumes)";

        public string Meeting1hButtonTooltip => !CanPauseMeeting
            ? (!_timerService.IsRunning
                ? "Timers are not running"
                : "Meeting pause is already active")
            : "Pause timers for 1 hour for meetings (auto-resumes)";

        // Error Indicator Properties
        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            private set => SetProperty(ref _hasValidationErrors, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            private set => SetProperty(ref _isSaving, value);
        }

        // Meeting Detection Properties
        public MeetingDetectionMethod MeetingDetectionMethod
        {
            get => _meetingDetectionMethod;
            set
            {
                if (SetProperty(ref _meetingDetectionMethod, value))
                {
                    OnPropertyChanged(nameof(IsWindowBasedDetection));
                    OnPropertyChanged(nameof(IsNetworkBasedDetection));
                    OnPropertyChanged(nameof(IsHybridDetection));
                    CheckForChanges();
                }
            }
        }

        public bool IsWindowBasedDetection
        {
            get => _meetingDetectionMethod == MeetingDetectionMethod.WindowBased;
            set
            {
                if (value && _meetingDetectionMethod != MeetingDetectionMethod.WindowBased)
                {
                    MeetingDetectionMethod = MeetingDetectionMethod.WindowBased;
                }
            }
        }

        public bool IsNetworkBasedDetection
        {
            get => _meetingDetectionMethod == MeetingDetectionMethod.NetworkBased;
            set
            {
                if (value && _meetingDetectionMethod != MeetingDetectionMethod.NetworkBased)
                {
                    MeetingDetectionMethod = MeetingDetectionMethod.NetworkBased;
                }
            }
        }

        public bool IsHybridDetection
        {
            get => _meetingDetectionMethod == MeetingDetectionMethod.Hybrid;
            set
            {
                if (value && _meetingDetectionMethod != MeetingDetectionMethod.Hybrid)
                {
                    MeetingDetectionMethod = MeetingDetectionMethod.Hybrid;
                }
            }
        }

        public bool LogDetectionActivity
        {
            get => _logDetectionActivity;
            set
            {
                if (SetProperty(ref _logDetectionActivity, value))
                {
                    CheckForChanges();
                }
            }
        }

        public bool EnableFallbackDetection
        {
            get => _enableFallbackDetection;
            set
            {
                if (SetProperty(ref _enableFallbackDetection, value))
                {
                    CheckForChanges();
                }
            }
        }

        public string MeetingDetectionStatus
        {
            get => _meetingDetectionStatus;
            private set => SetProperty(ref _meetingDetectionStatus, value);
        }

        public string MeetingDetectionStatusColor
        {
            get => _meetingDetectionStatusColor;
            private set => SetProperty(ref _meetingDetectionStatusColor, value);
        }

        public string MeetingDetectionStatusText
        {
            get => _meetingDetectionStatusText;
            private set => SetProperty(ref _meetingDetectionStatusText, value);
        }

        public bool PauseOnScreenLock
        {
            get => _pauseOnScreenLock;
            set
            {
                if (SetProperty(ref _pauseOnScreenLock, value))
                {
                    CheckForChanges();
                }
            }
        }

        public bool PauseOnMonitorOff
        {
            get => _pauseOnMonitorOff;
            set
            {
                if (SetProperty(ref _pauseOnMonitorOff, value))
                {
                    CheckForChanges();
                }
            }
        }

        public bool PauseOnIdle
        {
            get => _pauseOnIdle;
            set
            {
                if (SetProperty(ref _pauseOnIdle, value))
                {
                    CheckForChanges();
                }
            }
        }

        public int IdleTimeoutMinutes
        {
            get => _idleTimeoutMinutes;
            set
            {
                if (SetProperty(ref _idleTimeoutMinutes, value))
                {
                    CheckForChanges();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand RestoreDefaultsCommand { get; }
        public ICommand StartTimersCommand { get; }
        public ICommand StopTimersCommand { get; }

        public ICommand PauseTimersCommand { get; }
        public ICommand ResumeTimersCommand { get; }
        public ICommand PauseForMeetingCommand { get; }
        public ICommand PauseForMeeting1hCommand { get; }

        public ICommand ExitApplicationCommand { get; }
        public ICommand ShowAnalyticsCommand { get; }
        public ICommand BrowseCustomAudioCommand { get; }
        public ICommand TestCustomAudioCommand { get; }

        // Test commands
        public ICommand TestWarningCommand { get; }
        public ICommand TestPopupCommand { get; }
        public ICommand TestBreakWarningCommand { get; }
        public ICommand TestBreakCommand { get; }

        // Advanced feature commands
        public ICommand TestMeetingDetectionCommand { get; }
        public ICommand SwitchDetectionMethodCommand { get; }

        #endregion

        #region Methods

        public async Task LoadConfigurationImmediatelyAsync()
        {
            try
            {
                _logger.LogInformation("LoadConfigurationImmediatelyAsync called - loading config to update UI ASAP");

                _configuration = await _configurationService.LoadConfigurationAsync();

                // Sync startup setting with actual startup status
                _configuration.Application.StartWithWindows = _startupManager.IsStartupEnabled();

                _originalConfiguration = CloneConfiguration(_configuration);

                // Update UI properties from loaded configuration
                UpdatePropertiesFromConfiguration();

                _logger.LogInformation("Configuration loaded immediately on window load - UI updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration immediately");

                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();

                _logger.LogWarning("Failed to load configuration. Using default values.");
            }
        }

        private async void LoadConfigurationAsync()
        {
            try
            {
                _configuration = await _configurationService.LoadConfigurationAsync();

                // Sync startup setting with actual startup status
                _configuration.Application.StartWithWindows = _startupManager.IsStartupEnabled();

                _originalConfiguration = CloneConfiguration(_configuration);

                // Update UI properties from loaded configuration
                UpdatePropertiesFromConfiguration();

                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");

                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();

                _logger.LogWarning("Failed to load configuration. Using default values.");
            }
        }

        private void UpdatePropertiesFromConfiguration()
        {
            _logger.LogInformation($"BEFORE UPDATE: Current UI values - EyeRest {EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, Break {BreakIntervalMinutes}min/{BreakDurationMinutes}min");
            _logger.LogInformation($"FROM CONFIG: Loading values - EyeRest {_configuration.EyeRest.IntervalMinutes}min/{_configuration.EyeRest.DurationSeconds}sec, Break {_configuration.Break.IntervalMinutes}min/{_configuration.Break.DurationMinutes}min");

            // Eye Rest
            EyeRestIntervalMinutes = _configuration.EyeRest.IntervalMinutes;
            EyeRestDurationSeconds = _configuration.EyeRest.DurationSeconds;
            EyeRestStartSoundEnabled = _configuration.EyeRest.StartSoundEnabled;
            EyeRestEndSoundEnabled = _configuration.EyeRest.EndSoundEnabled;
            EyeRestWarningEnabled = _configuration.EyeRest.WarningEnabled;
            EyeRestWarningSeconds = _configuration.EyeRest.WarningSeconds;

            // Break
            BreakIntervalMinutes = _configuration.Break.IntervalMinutes;
            BreakDurationMinutes = _configuration.Break.DurationMinutes;
            BreakWarningEnabled = _configuration.Break.WarningEnabled;
            BreakWarningSeconds = _configuration.Break.WarningSeconds;
            OverlayOpacityPercent = _configuration.Break.OverlayOpacityPercent;
            RequireConfirmationAfterBreak = _configuration.Break.RequireConfirmationAfterBreak;
            ResetTimersOnBreakConfirmation = _configuration.Break.ResetTimersOnBreakConfirmation;

            // Audio
            AudioEnabled = _configuration.Audio.Enabled;
            AudioVolume = _configuration.Audio.Volume;
            CustomSoundPath = _configuration.Audio.CustomSoundPath;

            // Application
            StartWithWindows = _configuration.Application.StartWithWindows;
            MinimizeToTray = _configuration.Application.MinimizeToTray;
            StartMinimized = _configuration.Application.StartMinimized;
            ShowTrayNotifications = _configuration.Application.ShowTrayNotifications;
            ShowInTaskbar = _configuration.Application.ShowInTaskbar;

            // Apply dark mode setting and update UI property
            var isDarkMode = _configuration.Application.IsDarkMode;
            _isDarkMode = isDarkMode;
            ApplyThemeGlobally(isDarkMode);
            OnPropertyChanged(nameof(IsDarkMode));

            // Analytics
            AutoOpenDashboard = _configuration.Analytics.AutoOpenDashboard;

            // Meeting Detection
            MeetingDetectionMethod = _configuration.MeetingDetection.DetectionMethod;
            LogDetectionActivity = _configuration.MeetingDetection.LogDetectionActivity;
            EnableFallbackDetection = _configuration.MeetingDetection.EnableFallbackDetection;

            // User Presence
            PauseOnScreenLock = _configuration.UserPresence.PauseOnScreenLock;
            PauseOnMonitorOff = _configuration.UserPresence.PauseOnMonitorOff;
            PauseOnIdle = _configuration.UserPresence.PauseOnIdle;
            IdleTimeoutMinutes = _configuration.UserPresence.IdleTimeoutMinutes;

            _logger.LogInformation($"AFTER UPDATE: UI now shows - EyeRest {EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, Break {BreakIntervalMinutes}min/{BreakDurationMinutes}min");
        }

        private void UpdateConfigurationFromProperties()
        {
            if (_configuration == null) return;

            // Eye Rest
            if (_configuration.EyeRest != null)
            {
                _configuration.EyeRest.IntervalMinutes = EyeRestIntervalMinutes;
                _configuration.EyeRest.DurationSeconds = EyeRestDurationSeconds;
                _configuration.EyeRest.StartSoundEnabled = EyeRestStartSoundEnabled;
                _configuration.EyeRest.EndSoundEnabled = EyeRestEndSoundEnabled;
                _configuration.EyeRest.WarningEnabled = EyeRestWarningEnabled;
                _configuration.EyeRest.WarningSeconds = EyeRestWarningSeconds;
            }

            // Break
            if (_configuration.Break != null)
            {
                _configuration.Break.IntervalMinutes = BreakIntervalMinutes;
                _configuration.Break.DurationMinutes = BreakDurationMinutes;
                _configuration.Break.WarningEnabled = BreakWarningEnabled;
                _configuration.Break.WarningSeconds = BreakWarningSeconds;
                _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                _configuration.Break.RequireConfirmationAfterBreak = RequireConfirmationAfterBreak;
                _configuration.Break.ResetTimersOnBreakConfirmation = ResetTimersOnBreakConfirmation;
            }

            // Audio
            if (_configuration.Audio != null)
            {
                _configuration.Audio.Enabled = AudioEnabled;
                _configuration.Audio.Volume = AudioVolume;
                _configuration.Audio.CustomSoundPath = CustomSoundPath;
            }

            // Application
            if (_configuration.Application != null)
            {
                _configuration.Application.StartWithWindows = StartWithWindows;
                _configuration.Application.MinimizeToTray = MinimizeToTray;
                _configuration.Application.StartMinimized = StartMinimized;
                _configuration.Application.ShowTrayNotifications = ShowTrayNotifications;
                _configuration.Application.ShowInTaskbar = ShowInTaskbar;
                _configuration.Application.IsDarkMode = IsDarkMode;
            }

            // Analytics
            if (_configuration.Analytics != null)
            {
                _configuration.Analytics.AutoOpenDashboard = AutoOpenDashboard;
            }

            // Meeting Detection
            if (_configuration.MeetingDetection != null)
            {
                _configuration.MeetingDetection.DetectionMethod = MeetingDetectionMethod;
                _configuration.MeetingDetection.LogDetectionActivity = LogDetectionActivity;
                _configuration.MeetingDetection.EnableFallbackDetection = EnableFallbackDetection;
            }

            // User Presence
            if (_configuration.UserPresence != null)
            {
                _configuration.UserPresence.PauseOnScreenLock = PauseOnScreenLock;
                _configuration.UserPresence.PauseOnMonitorOff = PauseOnMonitorOff;
                _configuration.UserPresence.PauseOnIdle = PauseOnIdle;
                _configuration.UserPresence.IdleTimeoutMinutes = IdleTimeoutMinutes;
            }
        }

        private async Task SaveAutoOpenSettingAsync(bool value)
        {
            try
            {
                _logger.LogInformation($"SaveAutoOpenSettingAsync called with value: {value}");

                _configuration.Analytics.AutoOpenDashboard = value;
                await _configurationService.SaveConfigurationAsync(_configuration);

                _originalConfiguration.Analytics.AutoOpenDashboard = value;

                ErrorMessage = value ? "Auto-open enabled" : "Auto-open disabled";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    if (ErrorMessage.Contains("Auto-open"))
                    {
                        ErrorMessage = "";
                    }
                });

                _logger.LogInformation($"Auto-open dashboard setting successfully saved: {value}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save auto-open dashboard setting");
                ErrorMessage = "Failed to save auto-open setting";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (ErrorMessage.Contains("Failed to save"))
                    {
                        ErrorMessage = "";
                    }
                });
            }
        }

        private async Task SaveOverlayOpacityAsync()
        {
            try
            {
                _logger.LogInformation($"SaveOverlayOpacityAsync called with value: {OverlayOpacityPercent}%");

                UpdateConfigurationFromProperties();
                _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Break.OverlayOpacityPercent = OverlayOpacityPercent;

                _logger.LogInformation($"Auto-saved overlay opacity to {OverlayOpacityPercent}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save overlay opacity setting");
                ErrorMessage = "Failed to save overlay opacity";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (ErrorMessage.Contains("overlay opacity"))
                    {
                        ErrorMessage = "";
                    }
                });
            }
        }

        private async Task SaveAudioVolumeAsync()
        {
            try
            {
                _logger.LogInformation($"SaveAudioVolumeAsync called with value: {AudioVolume}");

                _configuration.Audio.Volume = AudioVolume;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Audio.Volume = AudioVolume;

                _logger.LogInformation($"Auto-saved audio volume: {AudioVolume}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save audio volume setting");
                ErrorMessage = "Failed to save audio volume";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (ErrorMessage.Contains("audio volume"))
                    {
                        ErrorMessage = "";
                    }
                });
            }
        }

        private async Task SaveDarkModeAsync(bool isDarkMode)
        {
            try
            {
                _logger.LogInformation($"SaveDarkModeAsync called with value: {isDarkMode}");

                _configuration.Application.IsDarkMode = isDarkMode;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.IsDarkMode = isDarkMode;

                _logger.LogInformation($"Auto-saved dark mode: {isDarkMode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save dark mode setting");
                ErrorMessage = "Failed to save dark mode";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (ErrorMessage.Contains("dark mode"))
                    {
                        ErrorMessage = "";
                    }
                });
            }
        }

        private async Task RestoreDefaults()
        {
            try
            {
                // Show confirmation dialog before restoring defaults
                var confirmed = await ShowConfirmationDialogAsync(
                    "Restore Defaults",
                    "Are you sure you want to restore all settings to their default values?");
                if (!confirmed) return;

                _logger.LogInformation("Restoring defaults");

                var defaultConfig = await _configurationService.GetDefaultConfiguration();
                _configuration = defaultConfig;
                UpdatePropertiesFromConfiguration();
                CheckForChanges();

                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration = CloneConfiguration(_configuration);

                _logger.LogInformation("Settings restored to defaults and saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore default settings");
            }
        }

        private async Task StartTimers()
        {
            try
            {
                await _timerService.StartAsync();
                UpdateTimerStatus();
                _logger.LogInformation("Timers started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start timers");
            }
        }

        private async Task StopTimers()
        {
            try
            {
                await _timerService.StopAsync();
                UpdateTimerStatus();
                _logger.LogInformation("Timers stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop timers");
            }
        }

        private async Task PauseTimers()
        {
            try
            {
                await _timerService.PauseAsync();
                UpdateTimerStatus();
                RefreshCanExecuteStates();
                _logger.LogInformation("Timers paused manually from UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause timers");
            }
        }

        private async Task ResumeTimers()
        {
            try
            {
                await _timerService.ResumeAsync();
                UpdateTimerStatus();
                RefreshCanExecuteStates();
                _logger.LogInformation("Timers resumed manually from UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume timers");
            }
        }

        private async Task PauseForMeeting()
        {
            try
            {
                await _timerService.PauseForDurationAsync(TimeSpan.FromMinutes(30), "Manual meeting pause from UI");
                UpdateTimerStatus();
                RefreshCanExecuteStates();
                _logger.LogInformation("Timers paused for 30-minute meeting from UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause timers for meeting");
            }
        }

        private async Task PauseForMeeting1h()
        {
            try
            {
                await _timerService.PauseForDurationAsync(TimeSpan.FromMinutes(60), "Manual 1-hour meeting pause from UI");
                UpdateTimerStatus();
                RefreshCanExecuteStates();
                _logger.LogInformation("Timers paused for 60-minute meeting from UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause timers for 1-hour meeting");
            }
        }

        private void RefreshCanExecuteStates()
        {
            OnPropertyChanged(nameof(CanPauseTimers));
            OnPropertyChanged(nameof(CanResumeTimers));
            OnPropertyChanged(nameof(CanPauseMeeting));

            OnPropertyChanged(nameof(StartButtonTooltip));
            OnPropertyChanged(nameof(StopButtonTooltip));
            OnPropertyChanged(nameof(PauseButtonTooltip));
            OnPropertyChanged(nameof(ResumeButtonTooltip));
            OnPropertyChanged(nameof(Meeting30mButtonTooltip));
            OnPropertyChanged(nameof(Meeting1hButtonTooltip));

            // Notify Avalonia that command CanExecute has changed (it caches the initial result)
            (PauseTimersCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
            (ResumeTimersCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
            (PauseForMeetingCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
            (PauseForMeeting1hCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void OnTimerServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ITimerService.IsRunning) ||
                e.PropertyName == nameof(ITimerService.IsBreakDelayed) ||
                e.PropertyName == nameof(ITimerService.IsPaused) ||
                e.PropertyName == nameof(ITimerService.IsSmartPaused) ||
                e.PropertyName == nameof(ITimerService.IsManuallyPaused))
            {
                UpdateTimerStatus();
                RefreshCanExecuteStates();
            }
        }

        private void UpdateTimerStatus()
        {
            var isRunning = _timerService.IsRunning;
            var isBreakDelayed = _timerService.IsBreakDelayed;

            if (isRunning)
            {
                if (_timerService.IsManuallyPaused)
                {
                    var remaining = _timerService.ManualPauseRemaining;
                    if (remaining.HasValue && remaining.Value > TimeSpan.Zero)
                    {
                        var minutes = (int)remaining.Value.TotalMinutes;
                        var seconds = remaining.Value.Seconds;
                        TimerStatusText = $"Meeting Pause ({minutes}m {seconds}s left)";
                    }
                    else
                    {
                        TimerStatusText = "Meeting Pause (Manual)";
                    }
                    TimerStatusColor = "#FFB74D";
                    WindowTitle = "Eye-rest Settings - Meeting Pause";
                }
                else if (_timerService.IsPaused)
                {
                    TimerStatusText = "Paused (Manual)";
                    TimerStatusColor = "#FFC107";
                    WindowTitle = "Eye-rest Settings - Paused";
                }
                else if (_timerService.IsSmartPaused)
                {
                    var reason = _timerService.PauseReason ?? "Auto";
                    TimerStatusText = $"Smart Paused ({reason})";
                    TimerStatusColor = "#FF9800";
                    WindowTitle = "Eye-rest Settings - Smart Paused";
                }
                else if (isBreakDelayed)
                {
                    var remainingDelay = _timerService.DelayRemaining;
                    var delayMinutes = (int)Math.Ceiling(remainingDelay.TotalMinutes);
                    TimerStatusText = $"Running (Break delayed {delayMinutes}m)";
                    TimerStatusColor = "#FF9800";
                    WindowTitle = "Eye-rest Settings - Running (Break delayed)";
                }
                else
                {
                    TimerStatusText = "Running";
                    TimerStatusColor = "#4CAF50";
                    WindowTitle = "Eye-rest Settings - Running";
                }
            }
            else
            {
                TimerStatusText = "Stopped";
                TimerStatusColor = "#F44336";
                WindowTitle = "Eye-rest Settings - Stopped";
            }
        }

        public void UpdateCountdown()
        {
            if (_timerService != null)
            {
                IsRunning = _timerService.IsRunning;

                if (_timerService.IsRunning)
                {
                    if (_timerService.IsManuallyPaused)
                    {
                        var remaining = _timerService.ManualPauseRemaining;
                        if (remaining.HasValue && remaining.Value > TimeSpan.Zero)
                        {
                            var pauseText = $"Meeting pause: {FormatTimeSpan(remaining.Value)} remaining";
                            TimeUntilNextEyeRest = pauseText;
                            TimeUntilNextBreak = pauseText;
                            DualCountdownText = $"{pauseText} (timers will auto-resume)";
                        }
                        else
                        {
                            TimeUntilNextEyeRest = "Meeting pause (manual)";
                            TimeUntilNextBreak = "Meeting pause (manual)";
                            DualCountdownText = "Meeting pause active (manual control)";
                        }
                    }
                    else if (_timerService.IsPaused)
                    {
                        TimeUntilNextEyeRest = "Timers paused (manual)";
                        TimeUntilNextBreak = "Timers paused (manual)";
                        DualCountdownText = "Timers paused manually";
                    }
                    else if (_timerService.IsSmartPaused)
                    {
                        var reason = _timerService.PauseReason ?? "Auto";
                        var pauseText = $"Smart paused ({reason})";
                        TimeUntilNextEyeRest = pauseText;
                        TimeUntilNextBreak = pauseText;
                        DualCountdownText = $"{pauseText} - will auto-resume";
                    }
                    else
                    {
                        var eyeRestTime = _timerService.TimeUntilNextEyeRest;
                        var breakTime = _timerService.TimeUntilNextBreak;

                        TimeUntilNextEyeRest = $"Next eye rest: {FormatTimeSpan(eyeRestTime)}";
                        TimeUntilNextBreak = $"Next break: {FormatTimeSpan(breakTime)}";
                        DualCountdownText = $"Next eye rest: {FormatTimeSpan(eyeRestTime)} | Next break: {FormatTimeSpan(breakTime)}";
                    }
                }
                else
                {
                    TimeUntilNextEyeRest = "Timers not running";
                    TimeUntilNextBreak = "Timers not running";
                    DualCountdownText = "Timers not running";
                }
            }

            UpdateTimerStatus();
        }

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds <= 0)
            {
                return "Due now";
            }
            else if (timeSpan.TotalSeconds < 2)
            {
                return "1s";
            }
            else if (timeSpan.TotalMinutes < 1)
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

        private void CheckForChanges()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            var errors = new List<string>();

            if (EyeRestIntervalMinutes < 1 || EyeRestIntervalMinutes > 120)
                errors.Add("Eye rest interval must be between 1 and 120 minutes");

            if (EyeRestDurationSeconds < 5 || EyeRestDurationSeconds > 300)
                errors.Add("Eye rest duration must be between 5 and 300 seconds");

            if (EyeRestWarningSeconds < 10 || EyeRestWarningSeconds > 120)
                errors.Add("Eye rest warning time must be between 10 and 120 seconds");

            if (BreakIntervalMinutes < 1 || BreakIntervalMinutes > 240)
                errors.Add("Break interval must be between 1 and 240 minutes");

            if (BreakDurationMinutes < 1 || BreakDurationMinutes > 30)
                errors.Add("Break duration must be between 1 and 30 minutes");

            if (BreakWarningSeconds < 10 || BreakWarningSeconds > 120)
                errors.Add("Break warning time must be between 10 and 120 seconds");

            if (OverlayOpacityPercent < 0 || OverlayOpacityPercent > 100)
                errors.Add("Overlay opacity must be between 0 and 100 percent");

            if (AudioVolume < 0 || AudioVolume > 100)
                errors.Add("Audio volume must be between 0 and 100");

            HasValidationErrors = errors.Count > 0;
            ErrorMessage = HasValidationErrors ? string.Join("; ", errors) : "";
        }

        private static AppConfiguration CloneConfiguration(AppConfiguration config)
        {
            if (config == null)
                return new AppConfiguration();

            return new AppConfiguration
            {
                EyeRest = config.EyeRest != null ? new EyeRestSettings
                {
                    IntervalMinutes = config.EyeRest.IntervalMinutes,
                    DurationSeconds = config.EyeRest.DurationSeconds,
                    StartSoundEnabled = config.EyeRest.StartSoundEnabled,
                    EndSoundEnabled = config.EyeRest.EndSoundEnabled,
                    WarningEnabled = config.EyeRest.WarningEnabled,
                    WarningSeconds = config.EyeRest.WarningSeconds
                } : new EyeRestSettings(),
                Break = config.Break != null ? new BreakSettings
                {
                    IntervalMinutes = config.Break.IntervalMinutes,
                    DurationMinutes = config.Break.DurationMinutes,
                    WarningEnabled = config.Break.WarningEnabled,
                    WarningSeconds = config.Break.WarningSeconds,
                    OverlayOpacityPercent = config.Break.OverlayOpacityPercent,
                    RequireConfirmationAfterBreak = config.Break.RequireConfirmationAfterBreak,
                    ResetTimersOnBreakConfirmation = config.Break.ResetTimersOnBreakConfirmation
                } : new BreakSettings(),
                Audio = config.Audio != null ? new AudioSettings
                {
                    Enabled = config.Audio.Enabled,
                    Volume = config.Audio.Volume,
                    CustomSoundPath = config.Audio.CustomSoundPath
                } : new AudioSettings(),
                Application = config.Application != null ? new ApplicationSettings
                {
                    StartWithWindows = config.Application.StartWithWindows,
                    MinimizeToTray = config.Application.MinimizeToTray,
                    StartMinimized = config.Application.StartMinimized,
                    ShowTrayNotifications = config.Application.ShowTrayNotifications,
                    ShowInTaskbar = config.Application.ShowInTaskbar,
                    IsDarkMode = config.Application.IsDarkMode
                } : new ApplicationSettings(),
                Analytics = config.Analytics != null ? new AnalyticsSettings
                {
                    Enabled = config.Analytics.Enabled,
                    AutoOpenDashboard = config.Analytics.AutoOpenDashboard,
                    DataRetentionDays = config.Analytics.DataRetentionDays,
                    TrackBreakEvents = config.Analytics.TrackBreakEvents,
                    TrackPresenceChanges = config.Analytics.TrackPresenceChanges,
                    TrackMeetingEvents = config.Analytics.TrackMeetingEvents,
                    TrackUserSessions = config.Analytics.TrackUserSessions,
                    AllowDataExport = config.Analytics.AllowDataExport,
                    ExportFormat = config.Analytics.ExportFormat,
                    AutoCleanupOldData = config.Analytics.AutoCleanupOldData,
                    DatabaseMaintenanceIntervalDays = config.Analytics.DatabaseMaintenanceIntervalDays
                } : new AnalyticsSettings(),
                UserPresence = config.UserPresence ?? new UserPresenceSettings(),
                MeetingDetection = config.MeetingDetection ?? new EyeRest.Models.MeetingDetectionSettings(),
                TimerControls = config.TimerControls ?? new TimerControlSettings()
            };
        }

        #endregion

        #region Test Methods

        private async Task TestWarningPopup()
        {
            try
            {
                _logger.LogInformation("TestWarningPopup called - manually testing warning popup");
                await _notificationService.ShowEyeRestWarningTestAsync(TimeSpan.FromSeconds(10));
                _logger.LogInformation("TestWarningPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestWarningPopup failed");
            }
        }

        private async Task TestEyeRestPopup()
        {
            try
            {
                var testDuration = TimeSpan.FromSeconds(EyeRestDurationSeconds);
                _logger.LogInformation($"TestEyeRestPopup called - testing with {testDuration.TotalSeconds}s duration");
                await _notificationService.ShowEyeRestReminderTestAsync(testDuration);
                _logger.LogInformation("TestEyeRestPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestEyeRestPopup failed");
            }
        }

        private async Task TestBreakWarningPopup()
        {
            try
            {
                _logger.LogInformation("TestBreakWarningPopup called - manually testing break warning popup");
                await _notificationService.ShowBreakWarningTestAsync(TimeSpan.FromSeconds(30));
                _logger.LogInformation("TestBreakWarningPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestBreakWarningPopup failed");
            }
        }

        private async Task TestBreakPopup()
        {
            try
            {
                _logger.LogInformation("TestBreakPopup called - manually testing break popup");
                var progress = new Progress<double>();
                var breakDuration = TimeSpan.FromMinutes(BreakDurationMinutes);
                var result = await _notificationService.ShowBreakReminderTestAsync(breakDuration, progress);
                _logger.LogInformation($"TestBreakPopup completed successfully with result: {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestBreakPopup failed");
            }
        }

        private async Task TestMeetingDetection()
        {
            try
            {
                _logger.LogInformation("Testing meeting detection with current method");
                // Meeting detection is currently disabled in the orchestrator.
                // When re-enabled, this would call IMeetingDetectionManager.TestDetectionAsync().
                MeetingDetectionStatus = "Feature disabled";
                MeetingDetectionStatusColor = "#FFA500";
                MeetingDetectionStatusText = "Meeting detection is currently disabled";
                _logger.LogInformation("Meeting detection feature is disabled - needs improvement and testing in future");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meeting detection test failed");
            }
        }

        private async Task SwitchDetectionMethod()
        {
            try
            {
                _logger.LogInformation($"Switching detection method to: {MeetingDetectionMethod}");
                // Meeting detection is currently disabled in the orchestrator.
                // When re-enabled, this would call IMeetingDetectionManager.SwitchMethodAsync().
                _logger.LogInformation("Meeting detection method switch saved to config (feature disabled at runtime)");
                _configuration.MeetingDetection.DetectionMethod = MeetingDetectionMethod;
                await _configurationService.SaveConfigurationAsync(_configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch detection method");
            }
        }

        #endregion

        #region Application Lifecycle

        private async void ExitApplication()
        {
            try
            {
                _logger.LogInformation("Exit application requested");

                var confirmed = await ShowConfirmationDialogAsync(
                    "Exit Application",
                    "Are you sure you want to exit Eye-rest? Timers will stop.");
                if (!confirmed) return;

                var app = Avalonia.Application.Current;
                if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application exit");
                var app = Avalonia.Application.Current;
                if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
        }

        private void ShowAnalyticsWindow()
        {
            try
            {
                _logger.LogInformation("Opening analytics dashboard window");

                var analyticsVm = App.Services?.GetService<AnalyticsDashboardViewModel>();
                if (analyticsVm != null)
                {
                    var analyticsWindow = new EyeRest.UI.Views.AnalyticsWindow(analyticsVm);
                    analyticsWindow.Show();
                }
                else
                {
                    _logger.LogWarning("Could not resolve AnalyticsDashboardViewModel from DI");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open analytics dashboard window");
            }
        }

        /// <summary>
        /// Apply theme changes globally using Avalonia's RequestedThemeVariant
        /// </summary>
        private void ApplyThemeGlobally(bool isDarkMode)
        {
            try
            {
                _logger.LogInformation($"Applying {(isDarkMode ? "Dark" : "Light")} theme globally");

                var app = Avalonia.Application.Current;
                if (app != null)
                {
                    app.RequestedThemeVariant = isDarkMode
                        ? Avalonia.Styling.ThemeVariant.Dark
                        : Avalonia.Styling.ThemeVariant.Light;

                    // Swap custom theme StyleInclude in app.Styles
                    // Our theme files are now <Styles> (not ResourceDictionary), loaded via StyleInclude
                    var oldStyleInclude = app.Styles.OfType<Avalonia.Markup.Xaml.Styling.StyleInclude>()
                        .FirstOrDefault(s => s.Source != null &&
                            (s.Source.ToString().Contains("LightTheme") || s.Source.ToString().Contains("DarkTheme")));

                    if (oldStyleInclude != null)
                    {
                        app.Styles.Remove(oldStyleInclude);
                    }

                    // Add the new theme as a StyleInclude
                    var themeUri = isDarkMode
                        ? new Uri("avares://EyeRest.UI/Resources/DarkTheme.axaml")
                        : new Uri("avares://EyeRest.UI/Resources/LightTheme.axaml");

                    var newTheme = new Avalonia.Markup.Xaml.Styling.StyleInclude(themeUri)
                    {
                        Source = themeUri
                    };
                    app.Styles.Add(newTheme);
                }

                _logger.LogInformation($"{(isDarkMode ? "Dark" : "Light")} theme applied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme globally");
            }
        }

        /// <summary>
        /// Browse and select a custom audio file - cross-platform placeholder
        /// </summary>
        private async void BrowseCustomAudio()
        {
            try
            {
                _logger.LogInformation("Browse for custom audio file requested");

                var topLevel = GetTopLevel();
                if (topLevel == null)
                {
                    _logger.LogWarning("Cannot open file picker - no top-level window available");
                    return;
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                    new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "Select Custom Audio File",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("Audio Files")
                            {
                                Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.flac", "*.m4a" }
                            },
                            new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                            {
                                Patterns = new[] { "*.*" }
                            }
                        }
                    });

                if (files.Count > 0)
                {
                    var path = files[0].Path.LocalPath;
                    CustomSoundPath = path;
                    _logger.LogInformation("Custom audio file selected: {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to browse for custom audio file");
            }
        }

        private async Task TestAudio()
        {
            try
            {
                _logger.LogInformation("Test audio requested");
                var audioService = App.Services?.GetService<IAudioService>();
                if (audioService != null)
                {
                    // Run on background thread to avoid UI thread deadlock
                    // (MacOSAudioService.IsAudioEnabled blocks with .GetAwaiter().GetResult())
                    var soundPath = CustomSoundPath;
                    await Task.Run(async () =>
                    {
                        if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                        {
                            await audioService.PlayCustomSoundTestAsync();
                        }
                        else
                        {
                            await audioService.TestEyeRestAudioAsync();
                        }
                    });
                    _logger.LogInformation("Audio test completed");
                }
                else
                {
                    _logger.LogWarning("IAudioService not available for audio test");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio test failed");
            }
        }

        #endregion

        #region Auto-Save Methods

        private async Task SaveStartupSettingAsync(bool startWithWindows)
        {
            try
            {
                _logger.LogInformation($"SaveStartupSettingAsync called with value: {startWithWindows}");

                _configuration.Application.StartWithWindows = startWithWindows;
                await _configurationService.SaveConfigurationAsync(_configuration);

                if (startWithWindows)
                {
                    _startupManager.EnableStartup(_configuration.Application.StartMinimized);
                    _logger.LogInformation($"Startup enabled with minimized flag: {_configuration.Application.StartMinimized}");
                }
                else
                {
                    _startupManager.DisableStartup();
                }

                _originalConfiguration.Application.StartWithWindows = startWithWindows;
                _logger.LogInformation($"Auto-saved startup setting: {startWithWindows}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save startup setting");
            }
        }

        private async Task SaveMinimizeToTrayAsync(bool minimizeToTray)
        {
            try
            {
                _logger.LogInformation($"SaveMinimizeToTrayAsync called with value: {minimizeToTray}");

                _configuration.Application.MinimizeToTray = minimizeToTray;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.MinimizeToTray = minimizeToTray;

                _logger.LogInformation($"Auto-saved minimize to tray: {minimizeToTray}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save minimize to tray setting");
            }
        }

        private async Task SaveShowInTaskbarAsync(bool showInTaskbar)
        {
            try
            {
                _logger.LogInformation($"SaveShowInTaskbarAsync called with value: {showInTaskbar}");

                _configuration.Application.ShowInTaskbar = showInTaskbar;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.ShowInTaskbar = showInTaskbar;

                _logger.LogInformation($"Auto-saved show in taskbar: {showInTaskbar}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save show in taskbar setting");
            }
        }

        private async Task SaveStartMinimizedAsync(bool startMinimized)
        {
            try
            {
                _logger.LogInformation($"SaveStartMinimizedAsync called with value: {startMinimized}");

                _configuration.Application.StartMinimized = startMinimized;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.StartMinimized = startMinimized;

                if (_configuration.Application.StartWithWindows)
                {
                    _startupManager.EnableStartup(startMinimized);
                    _logger.LogInformation($"Updated startup registry with minimized flag: {startMinimized}");
                }

                _logger.LogInformation($"Auto-saved start minimized: {startMinimized}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save start minimized setting");
            }
        }

        private async Task SaveShowTrayNotificationsAsync(bool showTrayNotifications)
        {
            try
            {
                _logger.LogInformation($"SaveShowTrayNotificationsAsync called with value: {showTrayNotifications}");

                _configuration.Application.ShowTrayNotifications = showTrayNotifications;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.ShowTrayNotifications = showTrayNotifications;

                _logger.LogInformation($"Auto-saved show tray notifications: {showTrayNotifications}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save show tray notifications setting");
            }
        }

        private async Task SaveAudioEnabledAsync(bool audioEnabled)
        {
            try
            {
                _logger.LogInformation($"SaveAudioEnabledAsync called with value: {audioEnabled}");

                _configuration.Audio.Enabled = audioEnabled;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Audio.Enabled = audioEnabled;

                _logger.LogInformation($"Auto-saved audio enabled: {audioEnabled}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save audio enabled setting");
            }
        }

        private async Task SaveCustomSoundPathAsync(string? customSoundPath)
        {
            try
            {
                _logger.LogInformation($"SaveCustomSoundPathAsync called with value: {customSoundPath}");

                _configuration.Audio.CustomSoundPath = customSoundPath;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Audio.CustomSoundPath = customSoundPath;

                _logger.LogInformation($"Auto-saved custom sound path: {customSoundPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save custom sound path");
            }
        }

        #endregion

        #region Debounced Timer Settings

        private void InitializeDebounceTimer()
        {
            _settingsDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SETTINGS_DEBOUNCE_DELAY_MS)
            };
            _settingsDebounceTimer.Tick += async (sender, e) =>
            {
                _settingsDebounceTimer.Stop();
                await SaveTimerSettingAsync();
                _logger.LogInformation("Debounced timer restart completed after user finished editing");
            };
        }

        private void DebouncedSaveTimerSetting()
        {
            _settingsDebounceTimer?.Stop();
            _settingsDebounceTimer?.Start();
            _logger.LogInformation("Settings changed - debouncing timer restart...");
        }

        private async Task SaveTimerSettingAsync()
        {
            try
            {
                _logger.LogInformation("SaveTimerSettingAsync called - auto-saving timer configuration");

                UpdateConfigurationFromProperties();
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration = CloneConfiguration(_configuration);

                _logger.LogInformation("Auto-saved timer settings to main config");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save timer settings");
            }
        }

        #endregion

        #region Meeting Detection Status

        private void UpdateMeetingDetectionStatus(DetectionServiceStatus status)
        {
            MeetingDetectionStatus = $"Method: {status.CurrentMethod} | Monitoring: {status.IsMonitoring} | Active: {status.IsMeetingActive}";

            if (status.HasErrors)
            {
                MeetingDetectionStatusColor = "#F44336";
                MeetingDetectionStatusText = $"Error: {status.ErrorMessage}";
            }
            else if (status.IsMeetingActive)
            {
                MeetingDetectionStatusColor = "#FF9800";
                MeetingDetectionStatusText = $"Meeting detected ({status.DetectedMeetingsCount} active)";
            }
            else if (status.IsMonitoring)
            {
                MeetingDetectionStatusColor = "#4CAF50";
                MeetingDetectionStatusText = "Monitoring active";
            }
            else
            {
                MeetingDetectionStatusColor = "#9E9E9E";
                MeetingDetectionStatusText = "Not monitoring";
            }
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _settingsDebounceTimer?.Stop();
                    _settingsDebounceTimer = null;

                    if (_timerService != null)
                    {
                        _timerService.PropertyChanged -= OnTimerServicePropertyChanged;
                    }
                }
                _disposed = true;
            }
        }

        #endregion

        #region Helper Methods

        private static Avalonia.Controls.TopLevel? GetTopLevel()
        {
            var app = Avalonia.Application.Current;
            if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        private static async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var topLevel = GetTopLevel();
            if (topLevel is not Avalonia.Controls.Window parentWindow)
                return true; // If no window, proceed without confirmation

            var dialog = new Views.ConfirmDialog(title, message);

            if (parentWindow is Views.MainWindow mw && mw.IsHiddenToTray)
                await dialog.ShowDialog<object?>(null!);
            else
                await dialog.ShowDialog(parentWindow);

            return dialog.DialogResult;
        }

        #endregion
    }
}
