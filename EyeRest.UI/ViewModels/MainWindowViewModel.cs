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
        private readonly IDonationService _donationService;
        private readonly IAnalyticsService? _analyticsService;
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
        private int _eyeRestWarningSeconds = 15;

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

        // Analytics KPI Summary
        private string _analyticsComplianceRateText = "--";
        private double _analyticsComplianceRateValue = 0;
        private string _analyticsTotalBreaksCompleted = "--";
        private string _analyticsCompletedPercentageText = "";
        private string _analyticsTotalBreaksSkipped = "--";
        private string _analyticsSkippedPercentageText = "";
        private string _analyticsTotalActiveTimeText = "--";
        private string _analyticsDailyAverageText = "";
        private string _analyticsHealthScoreText = "--";
        private string _analyticsHealthStatusText = "";
        private bool _isAnalyticsLoading = false;
        private bool _hasAnalyticsData = false;

        // UI State
        private int _selectedTabIndex = 0;
        private bool _isConfigurationMode = false;

        // Donation
        private bool _isDonationBannerVisible;
        private bool _isDonor;
        private bool _donationBannerDismissedThisSession;

        // Timer Status
        private string _timerStatusText = "Stopped";
        private string _timerStatusColor = "#F44336";
        private string _windowTitle = "Eye-rest Settings - Stopped";

        // Countdown Properties
        private string _timeUntilNextBreak = "--";
        private string _timeUntilNextEyeRest = "--";
        private string _dualCountdownText = "Timers not running";
        private bool _isRunning = false;

        // Visual countdown properties for redesigned status card
        private string _eyeRestCountdownText = "--";
        private string _breakCountdownText = "--";
        private double _eyeRestProgressPercent = 0.0;
        private double _breakProgressPercent = 0.0;

        // Error Indicators
        private string _errorMessage = "";
        private bool _hasValidationErrors = false;
        private string _infoMessage = "";

        // Save State
        private bool _isSaving = false;
        // Start as true to block all debounced saves until first config load completes.
        // UpdatePropertiesFromConfiguration() will set this to false after loading.
        private bool _isLoadingConfiguration = true;

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
            IDonationService donationService,
            ILogger<MainWindowViewModel> logger,
            IAnalyticsService? analyticsService = null)
        {
            _configurationService = configurationService;
            _timerConfigurationService = timerConfigurationService;
            _uiConfigurationService = uiConfigurationService;
            _timerService = timerService;
            _startupManager = startupManager;
            _notificationService = notificationService;
            _screenOverlayService = screenOverlayService;
            _donationService = donationService;
            _logger = logger;
            _analyticsService = analyticsService;

            _configuration = new AppConfiguration();
            _originalConfiguration = new AppConfiguration();

            // Initialize commands using CrossPlatformRelayCommand (no WPF CommandManager dependency)
            RestoreDefaultsCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await RestoreDefaults());
            StartTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await StartTimers(), () => !_timerService.IsRunning);
            StopTimersCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(async () => await StopTimers(), () => _timerService.IsRunning);

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

            // Mode toggle command
            ToggleConfigurationModeCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(() => IsConfigurationMode = !IsConfigurationMode);

            // Donation commands
            OpenDonationLinkCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(() => OpenDonationLink());
            DismissDonationCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(() => DismissDonation());
            EnterDonationCodeCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(() => ShowDonationCodeDialog());

            // Subscribe to donation events
            _donationService.DonorStatusChanged += (_, _) => UpdateDonationState();
            _donationService.PromptVisibilityChanged += (_, _) => UpdateDonationState();

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
                if (value != _eyeRestIntervalMinutes)
                {
                    _logger.LogWarning($"🔍 EyeRestIntervalMinutes CHANGING: {_eyeRestIntervalMinutes} → {value} (isLoading={_isLoadingConfiguration}) | Stack: {Environment.StackTrace.Substring(0, Math.Min(500, Environment.StackTrace.Length))}");
                }
                if (SetProperty(ref _eyeRestIntervalMinutes, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _eyeRestDurationSeconds, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _eyeRestStartSoundEnabled, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _eyeRestEndSoundEnabled, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _eyeRestWarningEnabled, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _eyeRestWarningSeconds, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _breakIntervalMinutes, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _breakDurationMinutes, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _breakWarningEnabled, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _breakWarningSeconds, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _overlayOpacityPercent, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _audioEnabled, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _audioVolume, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _customSoundPath, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _startWithWindows, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _minimizeToTray, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _startMinimized, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _showTrayNotifications, value) && !_isLoadingConfiguration)
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
                if (SetProperty(ref _showInTaskbar, value) && !_isLoadingConfiguration)
                {
                    CheckForChanges();
                    _ = Task.Run(async () => await SaveShowInTaskbarAsync(value));
                }
            }
        }

        public bool IsConfigurationMode
        {
            get => _isConfigurationMode;
            set
            {
                if (SetProperty(ref _isConfigurationMode, value))
                {
                    OnPropertyChanged(nameof(IsNotConfigurationMode));
                }
            }
        }

        public bool IsNotConfigurationMode => !_isConfigurationMode;

        private ThemeMode _selectedThemeMode = ThemeMode.Auto;
        public ThemeMode SelectedThemeMode
        {
            get => _selectedThemeMode;
            set
            {
                if (SetProperty(ref _selectedThemeMode, value))
                {
                    ApplyThemeFromMode(value);
                    if (!_isLoadingConfiguration)
                    {
                        CheckForChanges();
                        _ = Task.Run(async () => await SaveThemeModeAsync(value));
                    }
                }
            }
        }

        /// <summary>
        /// Available theme modes for the ComboBox.
        /// </summary>
        public ThemeMode[] ThemeModes { get; } = { ThemeMode.Auto, ThemeMode.Light, ThemeMode.Dark };

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

        // Analytics KPI Summary Properties
        public string AnalyticsComplianceRateText
        {
            get => _analyticsComplianceRateText;
            private set => SetProperty(ref _analyticsComplianceRateText, value);
        }

        public double AnalyticsComplianceRateValue
        {
            get => _analyticsComplianceRateValue;
            private set => SetProperty(ref _analyticsComplianceRateValue, value);
        }

        public string AnalyticsTotalBreaksCompleted
        {
            get => _analyticsTotalBreaksCompleted;
            private set => SetProperty(ref _analyticsTotalBreaksCompleted, value);
        }

        public string AnalyticsCompletedPercentageText
        {
            get => _analyticsCompletedPercentageText;
            private set => SetProperty(ref _analyticsCompletedPercentageText, value);
        }

        public string AnalyticsTotalBreaksSkipped
        {
            get => _analyticsTotalBreaksSkipped;
            private set => SetProperty(ref _analyticsTotalBreaksSkipped, value);
        }

        public string AnalyticsSkippedPercentageText
        {
            get => _analyticsSkippedPercentageText;
            private set => SetProperty(ref _analyticsSkippedPercentageText, value);
        }

        public string AnalyticsTotalActiveTimeText
        {
            get => _analyticsTotalActiveTimeText;
            private set => SetProperty(ref _analyticsTotalActiveTimeText, value);
        }

        public string AnalyticsDailyAverageText
        {
            get => _analyticsDailyAverageText;
            private set => SetProperty(ref _analyticsDailyAverageText, value);
        }

        public string AnalyticsHealthScoreText
        {
            get => _analyticsHealthScoreText;
            private set => SetProperty(ref _analyticsHealthScoreText, value);
        }

        public string AnalyticsHealthStatusText
        {
            get => _analyticsHealthStatusText;
            private set => SetProperty(ref _analyticsHealthStatusText, value);
        }

        public bool IsAnalyticsLoading
        {
            get => _isAnalyticsLoading;
            private set => SetProperty(ref _isAnalyticsLoading, value);
        }

        public bool HasAnalyticsData
        {
            get => _hasAnalyticsData;
            private set => SetProperty(ref _hasAnalyticsData, value);
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
                    // Load analytics summary when tab is selected
                    if (value == 3)
                    {
                        _ = LoadAnalyticsSummaryAsync();
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

        // Visual status card properties
        public string EyeRestCountdownText
        {
            get => _eyeRestCountdownText;
            private set => SetProperty(ref _eyeRestCountdownText, value);
        }

        public string BreakCountdownText
        {
            get => _breakCountdownText;
            private set => SetProperty(ref _breakCountdownText, value);
        }

        public string EyeRestTimerTooltip
        {
            get
            {
                var interval = _configuration?.EyeRest?.IntervalMinutes ?? 20;
                var warnSec = _configuration?.EyeRest?.WarningSeconds ?? 15;
                var warnOn = _configuration?.EyeRest?.WarningEnabled ?? true;
                if (warnOn && warnSec > 0)
                {
                    var effectiveSec = interval * 60 - warnSec;
                    var effMin = effectiveSec / 60;
                    var effSec = effectiveSec % 60;
                    var effDisplay = effSec > 0 ? $"{effMin}m {effSec}s" : $"{effMin}m";
                    return $"Interval: {interval}min\nWarning starts {warnSec}s before\nCountdown shows {effDisplay}";
                }
                return $"Interval: {interval}min";
            }
        }

        public string BreakTimerTooltip
        {
            get
            {
                var interval = _configuration?.Break?.IntervalMinutes ?? 55;
                var warnSec = _configuration?.Break?.WarningSeconds ?? 30;
                var warnOn = _configuration?.Break?.WarningEnabled ?? true;
                if (warnOn && warnSec > 0)
                {
                    var effectiveSec = interval * 60 - warnSec;
                    var effMin = effectiveSec / 60;
                    var effSec = effectiveSec % 60;
                    var effDisplay = effSec > 0 ? $"{effMin}m {effSec}s" : $"{effMin}m";
                    return $"Interval: {interval}min\nWarning starts {warnSec}s before\nCountdown shows {effDisplay}";
                }
                return $"Interval: {interval}min";
            }
        }

        public double EyeRestProgressPercent
        {
            get => _eyeRestProgressPercent;
            private set => SetProperty(ref _eyeRestProgressPercent, value);
        }

        public double BreakProgressPercent
        {
            get => _breakProgressPercent;
            private set => SetProperty(ref _breakProgressPercent, value);
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

        public string InfoMessage
        {
            get => _infoMessage;
            private set
            {
                if (SetProperty(ref _infoMessage, value))
                    OnPropertyChanged(nameof(HasInfoMessage));
            }
        }

        public bool HasInfoMessage => !string.IsNullOrEmpty(_infoMessage);

        private void ShowInfoMessage(string message, int durationMs = 5000)
        {
            InfoMessage = message;
            _ = Task.Run(async () =>
            {
                await Task.Delay(durationMs);
                Dispatcher.UIThread.Post(() =>
                {
                    if (InfoMessage == message)
                        InfoMessage = "";
                });
            });
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

        // Mode toggle command
        public ICommand ToggleConfigurationModeCommand { get; }

        // Donation commands
        public ICommand OpenDonationLinkCommand { get; }
        public ICommand DismissDonationCommand { get; }
        public ICommand EnterDonationCodeCommand { get; }

        public bool IsDonationBannerVisible
        {
            get => _isDonationBannerVisible;
            set => SetProperty(ref _isDonationBannerVisible, value);
        }

        public bool IsDonor
        {
            get => _isDonor;
            set => SetProperty(ref _isDonor, value);
        }

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

                // Update donation banner visibility
                UpdateDonationState();
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

                // Update donation banner visibility after config is loaded
                UpdateDonationState();

                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");

                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();

                // Still check donation state even on config load failure
                UpdateDonationState();

                _logger.LogWarning("Failed to load configuration. Using default values.");
            }
        }

        private void UpdatePropertiesFromConfiguration()
        {
            _settingsDebounceTimer?.Stop(); // Cancel any pending save before loading
            _isLoadingConfiguration = true;
            try
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

            // Apply theme mode setting and update UI property
            var themeMode = _configuration.Application.ThemeMode;
            _selectedThemeMode = themeMode;
            ApplyThemeFromMode(themeMode);
            OnPropertyChanged(nameof(SelectedThemeMode));

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
            finally
            {
                _isLoadingConfiguration = false;
                _settingsDebounceTimer?.Stop(); // Cancel any save queued by Slider write-back during load
                OnPropertyChanged(nameof(EyeRestTimerTooltip));
                OnPropertyChanged(nameof(BreakTimerTooltip));
            }
        }

        /// <summary>
        /// Forcefully re-apply all Slider-bound properties from the loaded configuration.
        /// Called from MainWindow.OnOpened after the UI has fully rendered, to overwrite
        /// any Slider midpoint write-backs that may have occurred during XAML initialization.
        /// </summary>
        public void ReapplyConfigurationValues()
        {
            if (_configuration == null) return;

            _isLoadingConfiguration = true;
            try
            {
                // Re-apply all Slider-bound values from the authoritative config
                EyeRestIntervalMinutes = _configuration.EyeRest.IntervalMinutes;
                EyeRestDurationSeconds = _configuration.EyeRest.DurationSeconds;
                EyeRestWarningSeconds = _configuration.EyeRest.WarningSeconds;
                BreakIntervalMinutes = _configuration.Break.IntervalMinutes;
                BreakDurationMinutes = _configuration.Break.DurationMinutes;
                BreakWarningSeconds = _configuration.Break.WarningSeconds;
                OverlayOpacityPercent = _configuration.Break.OverlayOpacityPercent;
                AudioVolume = _configuration.Audio.Volume;
                IdleTimeoutMinutes = _configuration.UserPresence.IdleTimeoutMinutes;

                _logger.LogInformation(
                    $"🛡️ CONFIG RE-APPLY: EyeRest={EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, " +
                    $"Break={BreakIntervalMinutes}min/{BreakDurationMinutes}min, Theme={SelectedThemeMode}");
            }
            finally
            {
                _isLoadingConfiguration = false;
                _settingsDebounceTimer?.Stop(); // Cancel any save triggered during re-apply
            }
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
                _configuration.Application.ThemeMode = SelectedThemeMode;
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Analytics.AutoOpenDashboard = value;
                    _configuration.Analytics.AutoOpenDashboard = value;
                });

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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                    _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Audio.Volume = AudioVolume;
                    _configuration.Audio.Volume = AudioVolume;
                });
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

        private async Task SaveThemeModeAsync(ThemeMode themeMode)
        {
            try
            {
                _logger.LogInformation($"SaveThemeModeAsync called with value: {themeMode}");

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.ThemeMode = themeMode;
                    _configuration.Application.ThemeMode = themeMode;
                });
                _originalConfiguration.Application.ThemeMode = themeMode;

                _logger.LogInformation($"Auto-saved theme mode: {themeMode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save theme mode setting");
                ErrorMessage = "Failed to save theme setting";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (ErrorMessage.Contains("theme"))
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

                await _configurationService.ReplaceConfigurationAsync(_configuration);
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
                // Flush any pending debounced save so StartAsync reads the latest values from disk
                if (_settingsDebounceTimer?.IsEnabled == true)
                {
                    _settingsDebounceTimer.Stop();
                    await SaveTimerSettingAsync();
                    _logger.LogInformation("Flushed pending debounced save before starting timers");
                }

                await _timerService.StartAsync();
                UpdateTimerStatus();

                // Show info toast explaining warning time deduction
                var eyeWarn = _configuration?.EyeRest?.WarningEnabled == true ? _configuration.EyeRest.WarningSeconds : 0;
                var breakWarn = _configuration?.Break?.WarningEnabled == true ? _configuration.Break.WarningSeconds : 0;
                if (eyeWarn > 0 || breakWarn > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (eyeWarn > 0)
                        parts.Add($"Eye Rest {_configuration!.EyeRest.IntervalMinutes}min (warns {eyeWarn}s early)");
                    if (breakWarn > 0)
                        parts.Add($"Break {_configuration!.Break.IntervalMinutes}min (warns {breakWarn}s early)");
                    ShowInfoMessage($"Timers started: {string.Join(", ", parts)}");
                }

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
                await _notificationService.HideAllNotifications();
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
                await _notificationService.HideAllNotifications();
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
            (StartTimersCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
            (StopTimersCommand as EyeRest.ViewModels.CrossPlatformRelayCommand)?.RaiseCanExecuteChanged();
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
                // Marshal to UI thread — TimerService.PropertyChanged can fire from background
                // threads (e.g. UserPresenceService idle detection), and RefreshCanExecuteStates
                // triggers Avalonia Button.CanExecuteChanged which requires UI thread access.
                if (Dispatcher.UIThread.CheckAccess())
                {
                    UpdateTimerStatus();
                    RefreshCanExecuteStates();
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateTimerStatus();
                        RefreshCanExecuteStates();
                    });
                }
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
                            EyeRestCountdownText = "Paused";
                            BreakCountdownText = "Paused";
                        }
                        else
                        {
                            TimeUntilNextEyeRest = "Meeting pause (manual)";
                            TimeUntilNextBreak = "Meeting pause (manual)";
                            DualCountdownText = "Meeting pause active (manual control)";
                            EyeRestCountdownText = "Paused";
                            BreakCountdownText = "Paused";
                        }
                        EyeRestProgressPercent = 0;
                        BreakProgressPercent = 0;
                    }
                    else if (_timerService.IsPaused)
                    {
                        TimeUntilNextEyeRest = "Timers paused (manual)";
                        TimeUntilNextBreak = "Timers paused (manual)";
                        DualCountdownText = "Timers paused manually";
                        EyeRestCountdownText = "Paused";
                        BreakCountdownText = "Paused";
                        EyeRestProgressPercent = 0;
                        BreakProgressPercent = 0;
                    }
                    else if (_timerService.IsSmartPaused)
                    {
                        var reason = _timerService.PauseReason ?? "Auto";
                        var pauseText = $"Smart paused ({reason})";
                        TimeUntilNextEyeRest = pauseText;
                        TimeUntilNextBreak = pauseText;
                        DualCountdownText = $"{pauseText} - will auto-resume";
                        EyeRestCountdownText = "Paused";
                        BreakCountdownText = "Paused";
                        EyeRestProgressPercent = 0;
                        BreakProgressPercent = 0;
                    }
                    else
                    {
                        var eyeRestTime = _timerService.TimeUntilNextEyeRest;
                        var breakTime = _timerService.TimeUntilNextBreak;

                        TimeUntilNextEyeRest = $"Next eye rest: {FormatTimeSpan(eyeRestTime)}";
                        TimeUntilNextBreak = $"Next break: {FormatTimeSpan(breakTime)}";
                        DualCountdownText = $"Next eye rest: {FormatTimeSpan(eyeRestTime)} | Next break: {FormatTimeSpan(breakTime)}";

                        // Update visual status card properties
                        EyeRestCountdownText = FormatTimeSpan(eyeRestTime);
                        BreakCountdownText = FormatTimeSpan(breakTime);

                        // Update progress in sync with the displayed countdown text:
                        // - Under 2 minutes: second-based (text shows "1m 30s", "45s")
                        // - 2+ minutes: minute-based (text shows "18m")
                        var eyeRestTotalSec = _configuration.EyeRest.IntervalMinutes * 60.0;
                        var breakTotalSec = _configuration.Break.IntervalMinutes * 60.0;

                        var eyeRestElapsed = eyeRestTime.TotalMinutes < 2
                            ? eyeRestTotalSec - eyeRestTime.TotalSeconds
                            : eyeRestTotalSec - Math.Ceiling(eyeRestTime.TotalMinutes) * 60.0;
                        var breakElapsed = breakTime.TotalMinutes < 2
                            ? breakTotalSec - breakTime.TotalSeconds
                            : breakTotalSec - Math.Ceiling(breakTime.TotalMinutes) * 60.0;

                        EyeRestProgressPercent = eyeRestTotalSec > 0
                            ? Math.Clamp(eyeRestElapsed / eyeRestTotalSec * 100.0, 0, 100)
                            : 0;
                        BreakProgressPercent = breakTotalSec > 0
                            ? Math.Clamp(breakElapsed / breakTotalSec * 100.0, 0, 100)
                            : 0;
                    }
                }
                else
                {
                    TimeUntilNextEyeRest = "Timers not running";
                    TimeUntilNextBreak = "Timers not running";
                    DualCountdownText = "Timers not running";
                    EyeRestCountdownText = "--";
                    BreakCountdownText = "--";
                    EyeRestProgressPercent = 0;
                    BreakProgressPercent = 0;
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
            else if (timeSpan.TotalMinutes < 2)
            {
                // Under 2 minutes: show seconds for urgency
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else if (timeSpan.TotalHours < 1)
            {
                // 2+ minutes: show minutes only (reduces UI updates to once per minute)
                return $"{timeSpan.Minutes}m";
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

            if (BreakWarningSeconds < 10 || BreakWarningSeconds > 300)
                errors.Add("Break warning time must be between 10 and 300 seconds");

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
                    ThemeMode = config.Application.ThemeMode
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
                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.MeetingDetection.DetectionMethod = MeetingDetectionMethod;
                    _configuration.MeetingDetection.DetectionMethod = MeetingDetectionMethod;
                });
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

        private async Task LoadAnalyticsSummaryAsync()
        {
            if (_analyticsService == null) return;

            try
            {
                IsAnalyticsLoading = true;

                var endDate = DateTime.Now;
                var startDate = endDate.AddDays(-7);

                var healthMetrics = await _analyticsService.GetHealthMetricsAsync(startDate, endDate);

                var totalBreaks = healthMetrics.BreaksCompleted + healthMetrics.BreaksSkipped;

                AnalyticsComplianceRateText = $"{healthMetrics.ComplianceRate:P1}";
                AnalyticsComplianceRateValue = healthMetrics.ComplianceRate * 100;
                AnalyticsTotalBreaksCompleted = healthMetrics.BreaksCompleted.ToString();
                AnalyticsCompletedPercentageText = totalBreaks > 0
                    ? $"{(double)healthMetrics.BreaksCompleted / totalBreaks:P0} of total"
                    : "No data";
                AnalyticsTotalBreaksSkipped = healthMetrics.BreaksSkipped.ToString();
                AnalyticsSkippedPercentageText = totalBreaks > 0
                    ? $"{(double)healthMetrics.BreaksSkipped / totalBreaks:P0} of total"
                    : "No data";
                AnalyticsTotalActiveTimeText = $"{healthMetrics.TotalActiveTime.TotalHours:F1}h";
                AnalyticsDailyAverageText = $"~{healthMetrics.TotalActiveTime.TotalHours / 7:F1}h/day avg";

                // Health score logic
                var hasMinimalData = totalBreaks < 5 || healthMetrics.TotalActiveTime.TotalHours < 2;
                if (hasMinimalData)
                    AnalyticsHealthScoreText = "Getting Started";
                else if (healthMetrics.ComplianceRate >= 0.9)
                    AnalyticsHealthScoreText = "Excellent";
                else if (healthMetrics.ComplianceRate >= 0.8)
                    AnalyticsHealthScoreText = "Good";
                else if (healthMetrics.ComplianceRate >= 0.7)
                    AnalyticsHealthScoreText = "Fair";
                else if (healthMetrics.ComplianceRate >= 0.6)
                    AnalyticsHealthScoreText = "Improving";
                else
                    AnalyticsHealthScoreText = "Building Habits";

                AnalyticsHealthStatusText = AnalyticsHealthScoreText;
                HasAnalyticsData = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load analytics summary");
            }
            finally
            {
                IsAnalyticsLoading = false;
            }
        }

        /// <summary>
        /// Apply theme based on ThemeMode (Auto detects OS preference).
        /// </summary>
        private void ApplyThemeFromMode(ThemeMode mode)
        {
            bool isDark;
            switch (mode)
            {
                case ThemeMode.Dark:
                    isDark = true;
                    UnsubscribeFromSystemThemeChanges();
                    break;
                case ThemeMode.Light:
                    isDark = false;
                    UnsubscribeFromSystemThemeChanges();
                    break;
                default: // Auto
                    isDark = DetectSystemDarkMode();
                    SubscribeToSystemThemeChanges();
                    break;
            }

            ApplyThemeGlobally(isDark);
        }

        /// <summary>
        /// Detect whether the OS is currently in dark mode.
        /// </summary>
        private bool DetectSystemDarkMode()
        {
            try
            {
                var app = Avalonia.Application.Current;
                if (app?.PlatformSettings != null)
                {
                    var colorValues = app.PlatformSettings.GetColorValues();
                    return colorValues.ThemeVariant == Avalonia.Platform.PlatformThemeVariant.Dark
                        || colorValues.ThemeVariant.ToString() == "Dark";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect system theme, defaulting to light");
            }
            return false;
        }

        private bool _isSubscribedToSystemTheme;

        private void SubscribeToSystemThemeChanges()
        {
            if (_isSubscribedToSystemTheme) return;

            var app = Avalonia.Application.Current;
            if (app?.PlatformSettings != null)
            {
                app.PlatformSettings.ColorValuesChanged += OnSystemThemeChanged;
                _isSubscribedToSystemTheme = true;
            }
        }

        private void UnsubscribeFromSystemThemeChanges()
        {
            if (!_isSubscribedToSystemTheme) return;

            var app = Avalonia.Application.Current;
            if (app?.PlatformSettings != null)
            {
                app.PlatformSettings.ColorValuesChanged -= OnSystemThemeChanged;
                _isSubscribedToSystemTheme = false;
            }
        }

        private void OnSystemThemeChanged(object? sender, Avalonia.Platform.PlatformColorValues e)
        {
            if (_selectedThemeMode != ThemeMode.Auto) return;

            var isDark = e.ThemeVariant == Avalonia.Platform.PlatformThemeVariant.Dark
                      || e.ThemeVariant.ToString() == "Dark";

            Dispatcher.UIThread.Post(() => ApplyThemeGlobally(isDark));
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
                    var oldStyleInclude = app.Styles.OfType<Avalonia.Markup.Xaml.Styling.StyleInclude>()
                        .FirstOrDefault(s => s.Source != null &&
                            (s.Source.ToString().Contains("LightTheme") || s.Source.ToString().Contains("DarkTheme")));

                    if (oldStyleInclude != null)
                    {
                        app.Styles.Remove(oldStyleInclude);
                    }

                    // Add the new theme as a StyleInclude
                    var themeUri = isDarkMode
                        ? new Uri("avares://EyeRest/Resources/DarkTheme.axaml")
                        : new Uri("avares://EyeRest/Resources/LightTheme.axaml");

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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.StartWithWindows = startWithWindows;
                    _configuration.Application.StartWithWindows = startWithWindows;
                });

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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.MinimizeToTray = minimizeToTray;
                    _configuration.Application.MinimizeToTray = minimizeToTray;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.ShowInTaskbar = showInTaskbar;
                    _configuration.Application.ShowInTaskbar = showInTaskbar;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.StartMinimized = startMinimized;
                    _configuration.Application.StartMinimized = startMinimized;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Application.ShowTrayNotifications = showTrayNotifications;
                    _configuration.Application.ShowTrayNotifications = showTrayNotifications;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Audio.Enabled = audioEnabled;
                    _configuration.Audio.Enabled = audioEnabled;
                });
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

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.Audio.CustomSoundPath = customSoundPath;
                    _configuration.Audio.CustomSoundPath = customSoundPath;
                });
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
            if (_isLoadingConfiguration) return;

            _settingsDebounceTimer?.Stop();
            _settingsDebounceTimer?.Start();
            _logger.LogInformation("Settings changed - debouncing timer restart...");
        }

        private async Task SaveTimerSettingAsync()
        {
            try
            {
                _logger.LogInformation("SaveTimerSettingAsync called - auto-saving timer configuration");

                await _configurationService.UpdateConfigurationAsync(config =>
                {
                    config.EyeRest.IntervalMinutes = EyeRestIntervalMinutes;
                    config.EyeRest.DurationSeconds = EyeRestDurationSeconds;
                    config.EyeRest.StartSoundEnabled = EyeRestStartSoundEnabled;
                    config.EyeRest.EndSoundEnabled = EyeRestEndSoundEnabled;
                    config.EyeRest.WarningEnabled = EyeRestWarningEnabled;
                    config.EyeRest.WarningSeconds = EyeRestWarningSeconds;
                    config.Break.IntervalMinutes = BreakIntervalMinutes;
                    config.Break.DurationMinutes = BreakDurationMinutes;
                    config.Break.WarningEnabled = BreakWarningEnabled;
                    config.Break.WarningSeconds = BreakWarningSeconds;
                    config.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                    config.Break.RequireConfirmationAfterBreak = RequireConfirmationAfterBreak;
                    config.Break.ResetTimersOnBreakConfirmation = ResetTimersOnBreakConfirmation;
                    _configuration = config;
                });
                _originalConfiguration = CloneConfiguration(_configuration);

                // Push updated intervals into the running timer service immediately
                _timerService.UpdateConfiguration(_configuration);

                OnPropertyChanged(nameof(EyeRestTimerTooltip));
                OnPropertyChanged(nameof(BreakTimerTooltip));
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

        #region Donation

        private void UpdateDonationState()
        {
            IsDonor = _donationService.IsDonor;

            var shouldShow = _donationService.ShouldShowDonationPrompt;
            var forceShow = App.ForceShowDonationBanner;
            var dismissed = _donationBannerDismissedThisSession;

            _logger.LogInformation(
                "Donation state: IsDonor={IsDonor}, ShouldShow={ShouldShow}, ForceShow={ForceShow}, DismissedThisSession={Dismissed}",
                IsDonor, shouldShow, forceShow, dismissed);

            IsDonationBannerVisible = !dismissed && (forceShow || shouldShow);

            _logger.LogInformation("Donation banner visible: {Visible}", IsDonationBannerVisible);

            if (IsDonationBannerVisible)
                _donationService.RecordPromptShown();
        }

        private void OpenDonationLink()
        {
            try
            {
                var url = _donationService.DonationUrl;
                var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open donation URL");
            }
        }

        private void DismissDonation()
        {
            _donationBannerDismissedThisSession = true;
            IsDonationBannerVisible = false;
            _donationService.RecordPromptDismissed();
        }

        private void ShowDonationCodeDialog()
        {
            try
            {
                var dialog = new Views.DonationCodeDialog();
                var topLevel = GetTopLevel();
                if (topLevel is Avalonia.Controls.Window owner)
                    dialog.ShowDialog(owner);
                else
                    dialog.Show();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show donation code dialog");
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
                    UnsubscribeFromSystemThemeChanges();

                    if (_timerService != null)
                    {
                        _timerService.PropertyChanged -= OnTimerServicePropertyChanged;
                    }

                    if (_donationService != null)
                    {
                        _donationService.DonorStatusChanged -= (_, _) => UpdateDonationState();
                        _donationService.PromptVisibilityChanged -= (_, _) => UpdateDonationState();
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
            var mainWindow = parentWindow as Views.MainWindow;

            if (mainWindow != null && mainWindow.IsHiddenToTray)
            {
                await dialog.ShowDialog<object?>(null!);
            }
            else
            {
                mainWindow?.ShowDimOverlay();
                try
                {
                    await dialog.ShowDialog(parentWindow);
                }
                finally
                {
                    mainWindow?.HideDimOverlay();
                }
            }

            return dialog.DialogResult;
        }

        #endregion
    }
}
