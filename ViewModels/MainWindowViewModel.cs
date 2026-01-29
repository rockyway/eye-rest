using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // For service provider access

namespace EyeRest.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
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
        private int _breakIntervalMinutes = 55; // FIXED: Correct PRD default (55 minutes)
        private int _breakDurationMinutes = 5;  // FIXED: Correct PRD default (5 minutes)
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

        // Analytics Dashboard
        private AnalyticsDashboardViewModel? _analyticsDashboardViewModel;

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

        public MainWindowViewModel(
            IConfigurationService configurationService,
            ITimerConfigurationService timerConfigurationService,
            IUIConfigurationService uiConfigurationService,
            ITimerService timerService,
            IStartupManager startupManager,
            INotificationService notificationService,
            IScreenOverlayService screenOverlayService,
            AnalyticsDashboardViewModel analyticsDashboardViewModel,
            ILogger<MainWindowViewModel> logger)
        {
            _configurationService = configurationService;
            _timerConfigurationService = timerConfigurationService;
            _uiConfigurationService = uiConfigurationService;
            _timerService = timerService;
            _startupManager = startupManager;
            _notificationService = notificationService;
            _screenOverlayService = screenOverlayService;
            AnalyticsDashboardViewModel = analyticsDashboardViewModel;
            _logger = logger;
            
            _configuration = new AppConfiguration();
            _originalConfiguration = new AppConfiguration();

            // Initialize commands
            RestoreDefaultsCommand = new RelayCommand(async () => await RestoreDefaults());
            StartTimersCommand = new RelayCommand(async () => await StartTimers());
            StopTimersCommand = new RelayCommand(async () => await StopTimers());
            
            // NEW: Pause/Resume Commands
            PauseTimersCommand = new RelayCommand(async () => await PauseTimers(), () => CanPauseTimers);
            ResumeTimersCommand = new RelayCommand(async () => await ResumeTimers(), () => CanResumeTimers);
            PauseForMeetingCommand = new RelayCommand(async () => await PauseForMeeting(), () => CanPauseMeeting);
            PauseForMeeting1hCommand = new RelayCommand(async () => await PauseForMeeting1h(), () => CanPauseMeeting);
            
            ExitApplicationCommand = new RelayCommand(ExitApplication);
            ShowAnalyticsCommand = new RelayCommand(ShowAnalyticsWindow);
            BrowseCustomAudioCommand = new RelayCommand(BrowseCustomAudio);
            TestCustomAudioCommand = new RelayCommand(async () => await TestAudio());
            
            // DEBUG: Test commands for popup debugging
            TestWarningCommand = new RelayCommand(async () => await TestWarningPopup());
            TestPopupCommand = new RelayCommand(async () => await TestEyeRestPopup());
            TestBreakWarningCommand = new RelayCommand(async () => await TestBreakWarningPopup());
            TestBreakCommand = new RelayCommand(async () => await TestBreakPopup());
            
            // Advanced feature commands
            TestMeetingDetectionCommand = new RelayCommand(async () => await TestMeetingDetection());
            SwitchDetectionMethodCommand = new RelayCommand(async () => await SwitchDetectionMethod());

            // Subscribe to timer service events to update status
            _timerService.PropertyChanged += OnTimerServicePropertyChanged;

            // Initialize debounce timer for graceful timer restarts
            InitializeDebounceTimer();
            
            // CRITICAL FIX: Load configuration asynchronously but immediately on window loaded
            // This prevents deadlock while ensuring UI shows correct values ASAP
            try
            {
                _logger.LogInformation("🔧 Starting async configuration loading to prevent deadlock");
                
                // Load configuration asynchronously - UI will update when ready
                LoadConfigurationAsync();
                
                _logger.LogInformation("🔧 Configuration loading started - UI will update when loaded");
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Debounced timer restart - waits for user to finish editing
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
                    // Auto-save overlay opacity immediately without restarting timers
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
                    // Auto-save audio settings immediately
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
                    // Auto-save volume immediately using UI configuration service
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
                    // Auto-save custom sound path immediately
                    _ = Task.Run(async () => await SaveCustomSoundPathAsync(value));
                    // Refresh all command states through WPF's command manager
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
                    // Auto-save startup setting immediately
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
                    // Auto-save minimize to tray setting immediately
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
                    // Auto-save start minimized setting immediately
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
                    // Auto-save show tray notifications setting immediately
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
                    // Auto-save show in taskbar setting immediately
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
                    // Apply theme changes globally to all windows
                    ApplyThemeGlobally(value);
                    // Auto-save dark mode setting immediately
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
                    // Update the configuration and trigger change detection
                    CheckForChanges();
                    // Save auto-open setting immediately without triggering timer reset
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

        // NEW: Can-Execute Properties for Pause/Resume Commands
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

        public AnalyticsDashboardViewModel? AnalyticsDashboardViewModel
        {
            get => _analyticsDashboardViewModel;
            private set => SetProperty(ref _analyticsDashboardViewModel, value);
        }

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
        
        // NEW: Pause/Resume Commands
        public ICommand PauseTimersCommand { get; }
        public ICommand ResumeTimersCommand { get; }
        public ICommand PauseForMeetingCommand { get; }
        public ICommand PauseForMeeting1hCommand { get; }
        
        public ICommand ExitApplicationCommand { get; }
        public ICommand ShowAnalyticsCommand { get; }
        public ICommand BrowseCustomAudioCommand { get; }
        public ICommand TestCustomAudioCommand { get; }
        
        // DEBUG: Test commands for popup debugging
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
                _logger.LogInformation("🔧 LoadConfigurationImmediatelyAsync called - loading config to update UI ASAP");
                
                _configuration = await _configurationService.LoadConfigurationAsync();
                
                // Sync startup setting with actual Windows startup status
                _configuration.Application.StartWithWindows = _startupManager.IsStartupEnabled();
                
                _originalConfiguration = CloneConfiguration(_configuration);
                
                // Update UI properties from loaded configuration
                UpdatePropertiesFromConfiguration();
                
                _logger.LogInformation("🔧 ✅ Configuration loaded immediately on window load - UI updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration immediately");
                
                // If loading fails, ensure we have default configuration
                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();
                                
                MessageBox.Show("Failed to load configuration. Using default values.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void LoadConfigurationAsync()
        {
            try
            {
                _configuration = await _configurationService.LoadConfigurationAsync();
                
                // Sync startup setting with actual Windows startup status
                _configuration.Application.StartWithWindows = _startupManager.IsStartupEnabled();
                
                _originalConfiguration = CloneConfiguration(_configuration);
                
                // Update UI properties from loaded configuration
                UpdatePropertiesFromConfiguration();
                                
                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");
                
                // If loading fails, ensure we have default configuration
                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();
                                
                MessageBox.Show("Failed to load configuration. Using default values.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdatePropertiesFromConfiguration()
        {
            _logger.LogInformation($"🔧 BEFORE UPDATE: Current UI values - EyeRest {EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, Break {BreakIntervalMinutes}min/{BreakDurationMinutes}min");
            _logger.LogInformation($"🔧 FROM CONFIG: Loading values - EyeRest {_configuration.EyeRest.IntervalMinutes}min/{_configuration.EyeRest.DurationSeconds}sec, Break {_configuration.Break.IntervalMinutes}min/{_configuration.Break.DurationMinutes}min");
            
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
            _isDarkMode = isDarkMode; // Set backing field directly to avoid triggering property setter
            ApplyThemeGlobally(isDarkMode); // Apply theme immediately
            OnPropertyChanged(nameof(IsDarkMode)); // Notify UI of the change
            
            // Analytics
            AutoOpenDashboard = _configuration.Analytics.AutoOpenDashboard;
            
            // Meeting Detection
            MeetingDetectionMethod = _configuration.MeetingDetection.DetectionMethod;
            LogDetectionActivity = _configuration.MeetingDetection.LogDetectionActivity;
            EnableFallbackDetection = _configuration.MeetingDetection.EnableFallbackDetection;
            
            // User Presence - use the actual settings from configuration
            PauseOnScreenLock = _configuration.UserPresence.PauseOnScreenLock;
            PauseOnMonitorOff = _configuration.UserPresence.PauseOnMonitorOff;
            PauseOnIdle = _configuration.UserPresence.PauseOnIdle;
            IdleTimeoutMinutes = _configuration.UserPresence.IdleTimeoutMinutes;
            
            _logger.LogInformation($"🔧 AFTER UPDATE: UI now shows - EyeRest {EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, Break {BreakIntervalMinutes}min/{BreakDurationMinutes}min");
            _logger.LogInformation($"🔧 ✅ All UI properties updated from configuration - PropertyChanged notifications sent");
        }

        private void UpdateConfigurationFromProperties()
        {
            // Ensure configuration is initialized
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


        /// <summary>
        /// Save only the auto-open analytics dashboard setting without timer reset or success popup
        /// </summary>
        private async Task SaveAutoOpenSettingAsync(bool value)
        {
            try
            {
                _logger.LogInformation($"SaveAutoOpenSettingAsync called with value: {value}");
                
                // CRITICAL FIX: Update the main configuration and save to config.json
                // This was saving to ui-config.json but loading from config.json
                _configuration.Analytics.AutoOpenDashboard = value;
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update the original configuration to prevent false unsaved changes
                _originalConfiguration.Analytics.AutoOpenDashboard = value;
                
                // Show light indicator instead of popup
                ErrorMessage = value ? "✅ Auto-open enabled" : "✅ Auto-open disabled";
                
                // Clear message after 2 seconds
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    if (ErrorMessage.Contains("Auto-open"))
                    {
                        ErrorMessage = "";
                    }
                });
                
                _logger.LogInformation($"Auto-open dashboard setting successfully saved to main config: {value}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save auto-open dashboard setting");
                ErrorMessage = "❌ Failed to save auto-open setting";
                
                // Clear error message after 3 seconds
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
                
                // Update the configuration from current UI properties to ensure we have latest values
                UpdateConfigurationFromProperties();
                
                // Explicitly set the overlay opacity value again to be sure
                _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                
                // Save configuration without restarting timers
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update the original configuration to prevent false unsaved changes
                _originalConfiguration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                
                _logger.LogInformation($"Auto-saved overlay opacity to {OverlayOpacityPercent}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save overlay opacity setting");
                ErrorMessage = "❌ Failed to save overlay opacity";
                
                // Clear error message after 3 seconds
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

        /// <summary>
        /// Save only the audio volume setting without timer reset or success popup
        /// </summary>
        private async Task SaveAudioVolumeAsync()
        {
            try
            {
                _logger.LogInformation($"SaveAudioVolumeAsync called with value: {AudioVolume}");
                
                // CRITICAL FIX: Update the main configuration and save to config.json
                // This was saving to ui-config.json but loading from config.json
                _configuration.Audio.Volume = AudioVolume;
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update the original configuration to prevent false unsaved changes
                _originalConfiguration.Audio.Volume = AudioVolume;
                
                _logger.LogInformation($"Auto-saved audio volume to main config: {AudioVolume}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save audio volume setting");
                ErrorMessage = "❌ Failed to save audio volume";
                
                // Clear error message after 3 seconds
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

        /// <summary>
        /// Save only the dark mode setting immediately without timer reset or success popup
        /// </summary>
        private async Task SaveDarkModeAsync(bool isDarkMode)
        {
            try
            {
                _logger.LogInformation($"SaveDarkModeAsync called with value: {isDarkMode}");
                
                // CRITICAL FIX: Update the main configuration and save to config.json
                // This was saving to ui-config.json but loading from config.json
                _configuration.Application.IsDarkMode = isDarkMode;
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update the original configuration to prevent false unsaved changes
                _originalConfiguration.Application.IsDarkMode = isDarkMode;
                
                _logger.LogInformation($"Auto-saved dark mode to main config: {isDarkMode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save dark mode setting");
                ErrorMessage = "❌ Failed to save dark mode";
                
                // Clear error message after 3 seconds
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
                var result = MessageBox.Show(
                    "Are you sure you want to restore all settings to defaults? This cannot be undone.",
                    "Restore Defaults",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _logger.LogInformation("User cancelled restore defaults");
                    return;
                }

                var defaultConfig = await _configurationService.GetDefaultConfiguration();
                _configuration = defaultConfig;
                UpdatePropertiesFromConfiguration();
                CheckForChanges();

                // Save the restored defaults
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration = CloneConfiguration(_configuration);

                _logger.LogInformation("Settings restored to defaults and saved");
                MessageBox.Show("All settings have been restored to their default values.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore default settings");
                MessageBox.Show("Failed to restore default settings.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Failed to start timers.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Failed to stop timers.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW: Pause/Resume Timer Command Methods
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
                MessageBox.Show("Failed to pause timers.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Failed to resume timers.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Show confirmation to user
                MessageBox.Show("Timers paused for 30 minutes.\nThey will automatically resume when the time is up.",
                    "Meeting Pause Active", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause timers for meeting");
                MessageBox.Show("Failed to pause timers for meeting.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Show confirmation to user
                MessageBox.Show("Timers paused for 1 hour.\nThey will automatically resume when the time is up.",
                    "Meeting Pause Active", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause timers for 1-hour meeting");
                MessageBox.Show("Failed to pause timers for meeting.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshCanExecuteStates()
        {
            OnPropertyChanged(nameof(CanPauseTimers));
            OnPropertyChanged(nameof(CanResumeTimers));
            OnPropertyChanged(nameof(CanPauseMeeting));

            // Also refresh tooltips since they depend on state
            OnPropertyChanged(nameof(StartButtonTooltip));
            OnPropertyChanged(nameof(StopButtonTooltip));
            OnPropertyChanged(nameof(PauseButtonTooltip));
            OnPropertyChanged(nameof(ResumeButtonTooltip));
            OnPropertyChanged(nameof(Meeting30mButtonTooltip));
            OnPropertyChanged(nameof(Meeting1hButtonTooltip));
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
                RefreshCanExecuteStates(); // NEW: Update button states when timer state changes
            }
        }

        private void UpdateTimerStatus()
        {
            var isRunning = _timerService.IsRunning;
            var isBreakDelayed = _timerService.IsBreakDelayed;

            if (isRunning)
            {
                // ENHANCED: Check for different pause states
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
                    TimerStatusColor = "#FFB74D"; // Yellow-orange for manual pause
                    WindowTitle = "Eye-rest Settings - Meeting Pause";
                }
                else if (_timerService.IsPaused)
                {
                    TimerStatusText = "Paused (Manual)";
                    TimerStatusColor = "#FFC107"; // Amber for manual pause
                    WindowTitle = "Eye-rest Settings - Paused";
                }
                else if (_timerService.IsSmartPaused)
                {
                    var reason = _timerService.PauseReason ?? "Auto";
                    TimerStatusText = $"Smart Paused ({reason})";
                    TimerStatusColor = "#FF9800"; // Orange for smart pause
                    WindowTitle = "Eye-rest Settings - Smart Paused";
                }
                else if (isBreakDelayed)
                {
                    var remainingDelay = _timerService.DelayRemaining;
                    var delayMinutes = (int)Math.Ceiling(remainingDelay.TotalMinutes);
                    TimerStatusText = $"Running (Break delayed {delayMinutes}m)";
                    TimerStatusColor = "#FF9800"; // Orange
                    WindowTitle = "Eye-rest Settings - Running (Break delayed)";
                }
                else
                {
                    TimerStatusText = "Running";
                    TimerStatusColor = "#4CAF50"; // Green
                    WindowTitle = "Eye-rest Settings - Running";
                }
            }
            else
            {
                TimerStatusText = "Stopped";
                TimerStatusColor = "#F44336"; // Red
                WindowTitle = "Eye-rest Settings - Stopped";
            }
        }

        public void UpdateCountdown()
        {
            // Update countdown timers from timer service
            if (_timerService != null)
            {
                IsRunning = _timerService.IsRunning;
                
                if (_timerService.IsRunning)
                {
                    // ENHANCED: Check for pause states first
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
                        // Normal running state - show countdowns
                        var eyeRestTime = _timerService.TimeUntilNextEyeRest;
                        var breakTime = _timerService.TimeUntilNextBreak;
                        
                        // Format individual countdowns
                        TimeUntilNextEyeRest = $"Next eye rest: {FormatTimeSpan(eyeRestTime)}";
                        TimeUntilNextBreak = $"Next break: {FormatTimeSpan(breakTime)}";
                        
                        // Create dual countdown display
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
            
            // Update timer status as well (includes delay status)
            UpdateTimerStatus();
        }
        
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            // CRITICAL FIX: Properly handle zero/negative time to prevent "1s" stuck state
            if (timeSpan.TotalSeconds <= 0)
            {
                return "Due now"; // Timer has expired - should trigger popup
            }
            else if (timeSpan.TotalSeconds < 2)
            {
                return "1s"; // Only show 1s when actually 1 second remaining
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
            // Auto-save eliminates need for unsaved changes tracking
            // All settings auto-save immediately, only validation needed
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            var errors = new List<string>();

            // Eye Rest validation
            if (EyeRestIntervalMinutes < 1 || EyeRestIntervalMinutes > 120)
                errors.Add("Eye rest interval must be between 1 and 120 minutes");
            
            if (EyeRestDurationSeconds < 5 || EyeRestDurationSeconds > 300)
                errors.Add("Eye rest duration must be between 5 and 300 seconds");
            
            if (EyeRestWarningSeconds < 10 || EyeRestWarningSeconds > 120)
                errors.Add("Eye rest warning time must be between 10 and 120 seconds");

            // Break validation 
            if (BreakIntervalMinutes < 1 || BreakIntervalMinutes > 240)
                errors.Add("Break interval must be between 1 and 240 minutes");
            
            if (BreakDurationMinutes < 1 || BreakDurationMinutes > 30)
                errors.Add("Break duration must be between 1 and 30 minutes");
            
            if (BreakWarningSeconds < 10 || BreakWarningSeconds > 120)
                errors.Add("Break warning time must be between 10 and 120 seconds");
            
            if (OverlayOpacityPercent < 0 || OverlayOpacityPercent > 100)
                errors.Add("Overlay opacity must be between 0 and 100 percent");

            // Audio validation
            if (AudioVolume < 0 || AudioVolume > 100)
                errors.Add("Audio volume must be between 0 and 100");

            // Update error state
            HasValidationErrors = errors.Count > 0;
            ErrorMessage = HasValidationErrors ? string.Join("; ", errors) : "";
        }

        private static AppConfiguration CloneConfiguration(AppConfiguration config)
        {
            // Handle null configuration safely
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

        #region DEBUG: Test Methods for Popup Debugging

        /// <summary>
        /// DEBUG: Manually test eye rest warning popup
        /// </summary>
        private async Task TestWarningPopup()
        {
            try
            {
                _logger.LogInformation("🧪 DEBUG: TestWarningPopup called - manually testing warning popup (analytics disabled)");
                await _notificationService.ShowEyeRestWarningTestAsync(TimeSpan.FromSeconds(10));
                _logger.LogInformation("🧪 DEBUG: TestWarningPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: TestWarningPopup failed");
                MessageBox.Show($"Warning popup test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// DEBUG: Manually test eye rest popup using current user configuration
        /// </summary>
        private async Task TestEyeRestPopup()
        {
            try
            {
                var testDuration = TimeSpan.FromSeconds(EyeRestDurationSeconds);
                _logger.LogInformation($"🧪 DEBUG: TestEyeRestPopup called - testing with {testDuration.TotalSeconds}s duration from configuration (analytics disabled)");
                await _notificationService.ShowEyeRestReminderTestAsync(testDuration);
                _logger.LogInformation("🧪 DEBUG: TestEyeRestPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: TestEyeRestPopup failed");
                MessageBox.Show($"Eye rest popup test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // DEBUG: Test break warning popup manually
        private async Task TestBreakWarningPopup()
        {
            try
            {
                _logger.LogInformation("🧪 DEBUG: TestBreakWarningPopup called - manually testing break warning popup (analytics disabled)");
                await _notificationService.ShowBreakWarningTestAsync(TimeSpan.FromSeconds(30));
                _logger.LogInformation("🧪 DEBUG: TestBreakWarningPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: TestBreakWarningPopup failed");
                MessageBox.Show($"Break warning popup test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // DEBUG: Test break popup manually
        private async Task TestBreakPopup()
        {
            try
            {
                _logger.LogInformation("🧪 DEBUG: TestBreakPopup called - manually testing break popup (analytics disabled)");
                var progress = new Progress<double>();
                var breakDuration = TimeSpan.FromMinutes(BreakDurationMinutes);
                var result = await _notificationService.ShowBreakReminderTestAsync(breakDuration, progress);
                _logger.LogInformation($"🧪 DEBUG: TestBreakPopup completed successfully with result: {result}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: TestBreakPopup failed");
                MessageBox.Show($"Break popup test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TestMeetingDetection()
        {
            try
            {
                _logger.LogInformation("Testing meeting detection with current method");
                
                // Get the meeting detection manager from service provider
                var serviceProvider = App.ServiceProvider;
                if (serviceProvider == null)
                {
                    MessageBox.Show("Application services not available", "Test Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var meetingDetectionManager = serviceProvider.GetService<IMeetingDetectionManager>();
                if (meetingDetectionManager == null)
                {
                    MessageBox.Show("Meeting detection manager not available", "Test Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var status = meetingDetectionManager.GetStatus();
                var message = $"Detection Method: {status.CurrentMethod}\n" +
                             $"Is Monitoring: {status.IsMonitoring}\n" +
                             $"Meeting Active: {status.IsMeetingActive}\n" +
                             $"Detected Meetings: {status.DetectedMeetingsCount}\n" +
                             $"Status: {status.StatusMessage}\n" +
                             $"Last Change: {status.LastStateChange?.ToString("HH:mm:ss") ?? "Never"}";

                if (status.HasErrors)
                {
                    message += $"\nError: {status.ErrorMessage}";
                }

                MessageBox.Show(message, "Meeting Detection Test", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.LogInformation("Meeting detection test completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Meeting detection test failed");
                MessageBox.Show($"Meeting detection test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SwitchDetectionMethod()
        {
            try
            {
                _logger.LogInformation($"Switching detection method to: {MeetingDetectionMethod}");
                
                // Get the meeting detection manager from service provider
                var serviceProvider = App.ServiceProvider;
                if (serviceProvider == null)
                {
                    MessageBox.Show("Application services not available", "Switch Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var meetingDetectionManager = serviceProvider.GetService<IMeetingDetectionManager>();
                if (meetingDetectionManager == null)
                {
                    MessageBox.Show("Meeting detection manager not available", "Switch Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Check if the method is available
                var isAvailable = await meetingDetectionManager.ValidateMethodAvailabilityAsync(MeetingDetectionMethod);
                if (!isAvailable)
                {
                    MessageBox.Show($"Detection method '{MeetingDetectionMethod}' is not available on this system", 
                        "Method Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Switch to the new method
                await meetingDetectionManager.SwitchDetectionMethodAsync(MeetingDetectionMethod);
                
                // Update configuration
                UpdateConfigurationFromProperties();
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update status
                UpdateMeetingDetectionStatus(meetingDetectionManager.GetStatus());
                
                MessageBox.Show($"Successfully switched to {MeetingDetectionMethod} detection method", 
                    "Detection Method Changed", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.LogInformation($"Successfully switched to {MeetingDetectionMethod} detection method");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch detection method");
                MessageBox.Show($"Failed to switch detection method: {ex.Message}", "Switch Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateMeetingDetectionStatus(DetectionServiceStatus status)
        {
            MeetingDetectionStatus = $"Method: {status.CurrentMethod} | Monitoring: {status.IsMonitoring} | Active: {status.IsMeetingActive}";
            
            if (status.HasErrors)
            {
                MeetingDetectionStatusColor = "#F44336"; // Red
                MeetingDetectionStatusText = $"Error: {status.ErrorMessage}";
            }
            else if (status.IsMeetingActive)
            {
                MeetingDetectionStatusColor = "#FF9800"; // Orange  
                MeetingDetectionStatusText = $"Meeting detected ({status.DetectedMeetingsCount} active)";
            }
            else if (status.IsMonitoring)
            {
                MeetingDetectionStatusColor = "#4CAF50"; // Green
                MeetingDetectionStatusText = "Monitoring active";
            }
            else
            {
                MeetingDetectionStatusColor = "#9E9E9E"; // Gray
                MeetingDetectionStatusText = "Not monitoring";
            }
        }

        // Exit application with confirmation
        private void ExitApplication()
        {
            try
            {
                _logger.LogInformation("Exit application requested");
                
                var result = MessageBox.Show(
                    "Are you sure you want to exit the Eye-rest application?\n\nThis will stop all eye rest and break reminders.",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);
                
                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("User confirmed exit - shutting down application");
                    Application.Current.Shutdown();
                }
                else
                {
                    _logger.LogInformation("User cancelled exit request");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application exit");
                // Force shutdown if there's an error
                Application.Current.Shutdown();
            }
        }

        // Show analytics dashboard in dedicated popup window
        private void ShowAnalyticsWindow()
        {
            try
            {
                _logger.LogInformation("📊 Opening analytics dashboard window");
                
                if (AnalyticsDashboardViewModel != null)
                {
                    // Ensure the dashboard view model has the current theme state
                    AnalyticsDashboardViewModel.IsDarkMode = IsDarkMode;
                    
                    // Create and configure the analytics window
                    var analyticsWindow = new Views.AnalyticsWindow(AnalyticsDashboardViewModel);
                    
                    // Set owner to maintain proper window relationship
                    if (Application.Current.MainWindow != null)
                    {
                        analyticsWindow.Owner = Application.Current.MainWindow;
                    }
                    
                    // Show the window
                    analyticsWindow.Show();
                    
                    _logger.LogInformation("✅ Analytics dashboard window opened successfully");
                }
                else
                {
                    _logger.LogWarning("❌ Analytics dashboard view model is null - cannot open window");
                    MessageBox.Show(
                        "Analytics dashboard is not available at this time.\n\n" +
                        "This may be due to initialization issues or service configuration problems.\n" +
                        "Please check the application logs for more details.",
                        "Analytics Dashboard Unavailable", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to open analytics dashboard window");
                
                var errorMessage = "Failed to open analytics dashboard.\n\n" +
                                 $"Error: {ex.Message}\n\n" +
                                 "Possible causes:\n" +
                                 "• Database initialization issues\n" +
                                 "• Service configuration problems\n" +
                                 "• Theme system errors\n\n" +
                                 "Please check the application logs and try again.";
                
                MessageBox.Show(errorMessage, "Analytics Dashboard Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Apply theme changes globally to all windows and UI components
        /// </summary>
        private void ApplyThemeGlobally(bool isDarkMode)
        {
            try
            {
                _logger.LogInformation($"🎨 Applying {(isDarkMode ? "Dark" : "Light")} theme globally to entire application");

                // Apply ModernWpf theme (Windows 11 styling)
                ModernWpf.ThemeManager.Current.ApplicationTheme = isDarkMode
                    ? ModernWpf.ApplicationTheme.Dark
                    : ModernWpf.ApplicationTheme.Light;

                // Create new resource dictionary for the selected theme
                var themeDict = new ResourceDictionary();
                string themeUri = isDarkMode
                    ? "pack://application:,,,/Resources/Themes/DarkTheme.xaml"
                    : "pack://application:,,,/Resources/Themes/LightTheme.xaml";

                themeDict.Source = new Uri(themeUri);

                // Find and replace the custom theme dictionary (don't clear ModernWpf resources)
                var appResources = Application.Current.Resources.MergedDictionaries;

                // Remove only the old custom theme, keep ModernWpf resources
                var oldTheme = appResources.FirstOrDefault(d =>
                    d.Source != null &&
                    (d.Source.ToString().Contains("LightTheme.xaml") ||
                     d.Source.ToString().Contains("DarkTheme.xaml")));

                if (oldTheme != null)
                {
                    appResources.Remove(oldTheme);
                }

                // Add the new custom theme
                appResources.Add(themeDict);

                // Also add common converters back if needed
                if (!Application.Current.Resources.Contains("BooleanToVisibilityConverter"))
                {
                    Application.Current.Resources["BooleanToVisibilityConverter"] = new EyeRest.Converters.BooleanToVisibilityConverter();
                }

                // Update AnalyticsDashboardViewModel for dashboard UI
                if (AnalyticsDashboardViewModel != null)
                {
                    AnalyticsDashboardViewModel.IsDarkMode = isDarkMode;
                    _logger.LogInformation($"🎨 Updated AnalyticsDashboardViewModel.IsDarkMode to {isDarkMode}");
                }

                // Force visual refresh for all windows (without clearing resources)
                foreach (Window window in Application.Current.Windows)
                {
                    try
                    {
                        // Special handling for AnalyticsWindow
                        if (window is EyeRest.Views.AnalyticsWindow analyticsWindow)
                        {
                            analyticsWindow.ApplyCurrentTheme();
                            _logger.LogInformation($"🎨 Applied theme resources to AnalyticsWindow: {window.Title}");
                        }

                        // Force visual tree refresh without clearing resources
                        window.InvalidateVisual();
                        window.InvalidateMeasure();
                        window.InvalidateArrange();
                        window.UpdateLayout();

                        _logger.LogInformation($"🎨 Refreshed window: {window.Title}");
                    }
                    catch (Exception windowEx)
                    {
                        _logger.LogWarning(windowEx, $"🎨 Failed to refresh window: {window.Title}");
                    }
                }

                _logger.LogInformation($"🎨 ✅ {(isDarkMode ? "Dark" : "Light")} theme applied successfully to entire application");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎨 ❌ Failed to apply theme globally");

                // Show user-friendly error message
                MessageBox.Show($"Failed to apply {(isDarkMode ? "dark" : "light")} theme: {ex.Message}",
                    "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Browse and select a custom audio file for notifications
        /// </summary>
        private void BrowseCustomAudio()
        {
            try
            {
                _logger.LogInformation("Opening file dialog to browse for custom audio file");
                
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Custom Audio File",
                    Filter = "Audio Files (*.wav, *.mp3, *.wma)|*.wav;*.mp3;*.wma|WAV Files (*.wav)|*.wav|MP3 Files (*.mp3)|*.mp3|WMA Files (*.wma)|*.wma|All Files (*.*)|*.*",
                    DefaultExt = ".wav",
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    CustomSoundPath = openFileDialog.FileName;
                    _logger.LogInformation($"Custom audio file selected: {CustomSoundPath}");
                    
                    // Show confirmation message
                    MessageBox.Show($"Custom audio file selected:\n{System.IO.Path.GetFileName(CustomSoundPath)}\n\nClick 'Test Sound' to preview the audio.", 
                        "Audio File Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogInformation("User cancelled audio file selection");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to browse for custom audio file");
                MessageBox.Show("Failed to open file browser. Please try again.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Test play audio - either custom audio file or default eye rest sounds
        /// </summary>
        private async Task TestAudio()
        {
            try
            {
                // Get the AudioService
                var audioService = App.ServiceProvider?.GetService(typeof(IAudioService)) as IAudioService;
                if (audioService == null)
                {
                    throw new InvalidOperationException("AudioService not available");
                }

                if (!string.IsNullOrEmpty(CustomSoundPath))
                {
                    // Test custom audio
                    _logger.LogInformation("🔊 Testing custom audio file through AudioService...");
                    await audioService.PlayCustomSoundTestAsync();

                    // Show success message for custom audio
                    var fileName = System.IO.Path.GetFileName(CustomSoundPath);
                    MessageBox.Show($"✅ Custom audio test successful!\n\nFile: {fileName}\nVolume: {AudioVolume}%\n\nThis is the same sound that will be used for app notifications.",
                        "Audio Test Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                    _logger.LogInformation("🔊 ✅ Custom audio test completed successfully");
                }
                else
                {
                    // Test default eye rest sounds
                    _logger.LogInformation("🔊 Testing default eye rest sounds through AudioService...");
                    await audioService.TestEyeRestAudioAsync();

                    // Show success message for default audio
                    MessageBox.Show($"✅ Default audio test successful!\n\nPlayed eye rest start and end sounds\nVolume: {AudioVolume}%\n\nThese are the same sounds used when eye rest popup shows and closes.",
                        "Audio Test Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                    _logger.LogInformation("🔊 ✅ Default eye rest audio test completed successfully");
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "🔊 Audio test failed - configuration issue");
                MessageBox.Show($"⚠️ {ex.Message}", "Audio Test Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "🔊 Audio test failed - file not found");
                MessageBox.Show($"❌ {ex.Message}\n\nPlease select a different audio file.", "Audio File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);

                // Clear the invalid path
                CustomSoundPath = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 Audio test failed");
                string message = !string.IsNullOrEmpty(CustomSoundPath)
                    ? $"❌ Failed to play the selected audio file:\n{ex.Message}\n\nPlease try a different audio file or check that the file format is supported (WAV, MP3, WMA)."
                    : $"❌ Failed to play default eye rest sounds:\n{ex.Message}\n\nPlease check your audio settings and ensure system sounds are not muted.";
                MessageBox.Show(message, "Audio Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Auto-save startup setting immediately without timer reset or popup
        /// </summary>
        private async Task SaveStartupSettingAsync(bool startWithWindows)
        {
            try
            {
                _logger.LogInformation($"SaveStartupSettingAsync called with value: {startWithWindows}");

                // Update configuration and save
                _configuration.Application.StartWithWindows = startWithWindows;
                await _configurationService.SaveConfigurationAsync(_configuration);

                // Handle Windows startup registry setting
                if (startWithWindows)
                {
                    // Pass the StartMinimized setting to the startup manager
                    _startupManager.EnableStartup(_configuration.Application.StartMinimized);
                    _logger.LogInformation($"Startup enabled with minimized flag: {_configuration.Application.StartMinimized}");
                }
                else
                {
                    _startupManager.DisableStartup();
                }

                // Update original configuration to prevent false unsaved changes
                _originalConfiguration.Application.StartWithWindows = startWithWindows;

                _logger.LogInformation($"Auto-saved startup setting: {startWithWindows}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save startup setting");
            }
        }

        /// <summary>
        /// Auto-save minimize to tray setting immediately
        /// </summary>
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

        /// <summary>
        /// Auto-save show in taskbar setting immediately
        /// </summary>
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

        /// <summary>
        /// Auto-save start minimized setting immediately
        /// </summary>
        private async Task SaveStartMinimizedAsync(bool startMinimized)
        {
            try
            {
                _logger.LogInformation($"SaveStartMinimizedAsync called with value: {startMinimized}");

                _configuration.Application.StartMinimized = startMinimized;
                await _configurationService.SaveConfigurationAsync(_configuration);
                _originalConfiguration.Application.StartMinimized = startMinimized;

                // If StartWithWindows is enabled, update the registry to reflect the new minimized state
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

        /// <summary>
        /// Auto-save show tray notifications setting immediately
        /// </summary>
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

        /// <summary>
        /// Initialize the debounce timer for graceful timer restarts
        /// </summary>
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
                _logger.LogInformation("🔧 Debounced timer restart completed after user finished editing");
            };
        }

        /// <summary>
        /// Debounced timer settings save - waits for user to finish editing before restarting timers
        /// </summary>
        private void DebouncedSaveTimerSetting()
        {
            // Reset the timer - this delays the actual save until user stops typing
            _settingsDebounceTimer?.Stop();
            _settingsDebounceTimer?.Start();
            _logger.LogInformation("🔧 Settings changed - debouncing timer restart...");
        }

        /// <summary>
        /// Auto-save timer settings immediately using TimerConfigurationService
        /// </summary>
        private async Task SaveTimerSettingAsync()
        {
            try
            {
                _logger.LogInformation("SaveTimerSettingAsync called - auto-saving timer configuration");
                
                // Update configuration from current UI properties
                UpdateConfigurationFromProperties();
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Update original configuration to prevent false unsaved changes
                _originalConfiguration = CloneConfiguration(_configuration);
                
                _logger.LogInformation("Auto-saved timer settings to main config");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save timer settings");
            }
        }

        /// <summary>
        /// Auto-save audio enabled setting immediately
        /// </summary>
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

        /// <summary>
        /// Auto-save custom sound path immediately
        /// </summary>
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
                    // Clean up the debounce timer
                    _settingsDebounceTimer?.Stop();
                    _settingsDebounceTimer = null;

                    // Unsubscribe from timer service events
                    if (_timerService != null)
                    {
                        _timerService.PropertyChanged -= OnTimerServicePropertyChanged;
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
}