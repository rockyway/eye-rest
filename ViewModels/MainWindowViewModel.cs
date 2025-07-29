using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;

namespace EyeRest.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IConfigurationService _configurationService;
        private readonly ITimerService _timerService;
        private readonly IStartupManager _startupManager;
        private readonly INotificationService _notificationService;
        private readonly IScreenOverlayService _screenOverlayService;
        private readonly ILogger<MainWindowViewModel> _logger;
        
        private AppConfiguration _configuration;
        private AppConfiguration _originalConfiguration;
        private bool _hasUnsavedChanges;

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

        // Audio Settings
        private bool _audioEnabled = true;
        private int _audioVolume = 50;
        private string? _customSoundPath;

        // Application Settings
        private bool _startWithWindows = false;
        private bool _minimizeToTray = true;
        private bool _showInTaskbar = false;

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
            ITimerService timerService,
            IStartupManager startupManager,
            INotificationService notificationService,
            IScreenOverlayService screenOverlayService,
            ILogger<MainWindowViewModel> logger)
        {
            _configurationService = configurationService;
            _timerService = timerService;
            _startupManager = startupManager;
            _notificationService = notificationService;
            _screenOverlayService = screenOverlayService;
            _logger = logger;
            
            _configuration = new AppConfiguration();
            _originalConfiguration = new AppConfiguration();

            // Initialize commands
            SaveCommand = new RelayCommand(async () => await SaveSettings(), () => HasUnsavedChanges && !IsSaving);
            CancelCommand = new RelayCommand(CancelChanges, () => HasUnsavedChanges);
            RestoreDefaultsCommand = new RelayCommand(async () => await RestoreDefaults());
            StartTimersCommand = new RelayCommand(async () => await StartTimers());
            StopTimersCommand = new RelayCommand(async () => await StopTimers());
            ExitApplicationCommand = new RelayCommand(ExitApplication);
            
            // DEBUG: Test commands for popup debugging
            TestWarningCommand = new RelayCommand(async () => await TestWarningPopup());
            TestPopupCommand = new RelayCommand(async () => await TestEyeRestPopup());
            TestBreakWarningCommand = new RelayCommand(async () => await TestBreakWarningPopup());
            TestBreakCommand = new RelayCommand(async () => await TestBreakPopup());

            // Subscribe to timer service events to update status
            _timerService.PropertyChanged += OnTimerServicePropertyChanged;

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

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set => SetProperty(ref _hasUnsavedChanges, value);
        }

        // Eye Rest Properties
        public int EyeRestIntervalMinutes
        {
            get => _eyeRestIntervalMinutes;
            set
            {
                if (SetProperty(ref _eyeRestIntervalMinutes, value))
                {
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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
                    CheckForChanges();
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

        // Audio Properties
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                if (SetProperty(ref _audioEnabled, value))
                {
                    CheckForChanges();
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
                    CheckForChanges();
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

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RestoreDefaultsCommand { get; }
        public ICommand StartTimersCommand { get; }
        public ICommand StopTimersCommand { get; }
        public ICommand ExitApplicationCommand { get; }
        
        // DEBUG: Test commands for popup debugging
        public ICommand TestWarningCommand { get; }
        public ICommand TestPopupCommand { get; }
        public ICommand TestBreakWarningCommand { get; }
        public ICommand TestBreakCommand { get; }

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
                HasUnsavedChanges = false;
                
                _logger.LogInformation("🔧 ✅ Configuration loaded immediately on window load - UI updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration immediately");
                
                // If loading fails, ensure we have default configuration
                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();
                HasUnsavedChanges = false;
                
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
                HasUnsavedChanges = false;
                
                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration");
                
                // If loading fails, ensure we have default configuration
                _configuration = await _configurationService.GetDefaultConfiguration();
                _originalConfiguration = CloneConfiguration(_configuration);
                UpdatePropertiesFromConfiguration();
                HasUnsavedChanges = false;
                
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

            // Audio
            AudioEnabled = _configuration.Audio.Enabled;
            AudioVolume = _configuration.Audio.Volume;
            CustomSoundPath = _configuration.Audio.CustomSoundPath;

            // Application
            StartWithWindows = _configuration.Application.StartWithWindows;
            MinimizeToTray = _configuration.Application.MinimizeToTray;
            ShowInTaskbar = _configuration.Application.ShowInTaskbar;
            
            _logger.LogInformation($"🔧 AFTER UPDATE: UI now shows - EyeRest {EyeRestIntervalMinutes}min/{EyeRestDurationSeconds}sec, Break {BreakIntervalMinutes}min/{BreakDurationMinutes}min");
            _logger.LogInformation($"🔧 ✅ All UI properties updated from configuration - PropertyChanged notifications sent");
        }

        private void UpdateConfigurationFromProperties()
        {
            // Eye Rest
            _configuration.EyeRest.IntervalMinutes = EyeRestIntervalMinutes;
            _configuration.EyeRest.DurationSeconds = EyeRestDurationSeconds;
            _configuration.EyeRest.StartSoundEnabled = EyeRestStartSoundEnabled;
            _configuration.EyeRest.EndSoundEnabled = EyeRestEndSoundEnabled;
            _configuration.EyeRest.WarningEnabled = EyeRestWarningEnabled;
            _configuration.EyeRest.WarningSeconds = EyeRestWarningSeconds;

            // Break
            _configuration.Break.IntervalMinutes = BreakIntervalMinutes;
            _configuration.Break.DurationMinutes = BreakDurationMinutes;
            _configuration.Break.WarningEnabled = BreakWarningEnabled;
            _configuration.Break.WarningSeconds = BreakWarningSeconds;
            _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;

            // Audio
            _configuration.Audio.Enabled = AudioEnabled;
            _configuration.Audio.Volume = AudioVolume;
            _configuration.Audio.CustomSoundPath = CustomSoundPath;

            // Application
            _configuration.Application.StartWithWindows = StartWithWindows;
            _configuration.Application.MinimizeToTray = MinimizeToTray;
            _configuration.Application.ShowInTaskbar = ShowInTaskbar;
        }

        private async Task SaveSettings()
        {
            if (IsSaving) return; // Prevent multiple simultaneous saves
            
            try
            {
                IsSaving = true;
                UpdateConfigurationFromProperties();
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                // Handle Windows startup setting
                if (_configuration.Application.StartWithWindows)
                {
                    _startupManager.EnableStartup();
                }
                else
                {
                    _startupManager.DisableStartup();
                }
                
                _originalConfiguration = CloneConfiguration(_configuration);
                HasUnsavedChanges = false;
                
                // Restart timers with new settings if they're currently running
                if (_timerService.IsRunning)
                {
                    _logger.LogInformation("Restarting timers with new settings");
                    await _timerService.StopAsync();
                    await _timerService.StartAsync();
                    _logger.LogInformation("Timers restarted successfully with new settings");
                }
                
                _logger.LogInformation("Settings saved successfully");
                MessageBox.Show("Settings saved successfully!\n\nTimers have been restarted with the new settings.", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                MessageBox.Show("Failed to save settings. Please try again.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task SaveOverlayOpacityAsync()
        {
            try
            {
                // Update only the overlay opacity setting without affecting other settings or timers
                _configuration.Break.OverlayOpacityPercent = OverlayOpacityPercent;
                
                // Save configuration without restarting timers
                await _configurationService.SaveConfigurationAsync(_configuration);
                
                _logger.LogInformation($"Auto-saved overlay opacity to {OverlayOpacityPercent}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save overlay opacity setting");
            }
        }

        private void CancelChanges()
        {
            _configuration = CloneConfiguration(_originalConfiguration);
            UpdatePropertiesFromConfiguration();
            HasUnsavedChanges = false;
        }

        private async Task RestoreDefaults()
        {
            try
            {
                var defaultConfig = await _configurationService.GetDefaultConfiguration();
                _configuration = defaultConfig;
                UpdatePropertiesFromConfiguration();
                CheckForChanges();
                
                _logger.LogInformation("Settings restored to defaults");
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

        private void OnTimerServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ITimerService.IsRunning) || 
                e.PropertyName == nameof(ITimerService.IsBreakDelayed))
            {
                UpdateTimerStatus();
            }
        }

        private void UpdateTimerStatus()
        {
            var isRunning = _timerService.IsRunning;
            var isBreakDelayed = _timerService.IsBreakDelayed;

            if (isRunning)
            {
                if (isBreakDelayed)
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
                    // Get individual countdown times
                    var eyeRestTime = _timerService.TimeUntilNextEyeRest;
                    var breakTime = _timerService.TimeUntilNextBreak;
                    
                    // Format individual countdowns
                    TimeUntilNextEyeRest = $"Next eye rest: {FormatTimeSpan(eyeRestTime)}";
                    TimeUntilNextBreak = $"Next break: {FormatTimeSpan(breakTime)}";
                    
                    // Create dual countdown display
                    DualCountdownText = $"Next eye rest: {FormatTimeSpan(eyeRestTime)} | Next break: {FormatTimeSpan(breakTime)}";
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
            if (timeSpan.TotalMinutes < 1)
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
            UpdateConfigurationFromProperties();
            HasUnsavedChanges = !ConfigurationsEqual(_configuration, _originalConfiguration);
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
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = config.EyeRest.IntervalMinutes,
                    DurationSeconds = config.EyeRest.DurationSeconds,
                    StartSoundEnabled = config.EyeRest.StartSoundEnabled,
                    EndSoundEnabled = config.EyeRest.EndSoundEnabled,
                    WarningEnabled = config.EyeRest.WarningEnabled,
                    WarningSeconds = config.EyeRest.WarningSeconds
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = config.Break.IntervalMinutes,
                    DurationMinutes = config.Break.DurationMinutes,
                    WarningEnabled = config.Break.WarningEnabled,
                    WarningSeconds = config.Break.WarningSeconds
                },
                Audio = new AudioSettings
                {
                    Enabled = config.Audio.Enabled,
                    Volume = config.Audio.Volume,
                    CustomSoundPath = config.Audio.CustomSoundPath
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = config.Application.StartWithWindows,
                    MinimizeToTray = config.Application.MinimizeToTray,
                    ShowInTaskbar = config.Application.ShowInTaskbar
                }
            };
        }

        private static bool ConfigurationsEqual(AppConfiguration config1, AppConfiguration config2)
        {
            return config1.EyeRest.IntervalMinutes == config2.EyeRest.IntervalMinutes &&
                   config1.EyeRest.DurationSeconds == config2.EyeRest.DurationSeconds &&
                   config1.EyeRest.StartSoundEnabled == config2.EyeRest.StartSoundEnabled &&
                   config1.EyeRest.EndSoundEnabled == config2.EyeRest.EndSoundEnabled &&
                   config1.EyeRest.WarningEnabled == config2.EyeRest.WarningEnabled &&
                   config1.EyeRest.WarningSeconds == config2.EyeRest.WarningSeconds &&
                   config1.Break.IntervalMinutes == config2.Break.IntervalMinutes &&
                   config1.Break.DurationMinutes == config2.Break.DurationMinutes &&
                   config1.Break.WarningEnabled == config2.Break.WarningEnabled &&
                   config1.Break.WarningSeconds == config2.Break.WarningSeconds &&
                   config1.Audio.Enabled == config2.Audio.Enabled &&
                   config1.Audio.Volume == config2.Audio.Volume &&
                   config1.Audio.CustomSoundPath == config2.Audio.CustomSoundPath &&
                   config1.Application.StartWithWindows == config2.Application.StartWithWindows &&
                   config1.Application.MinimizeToTray == config2.Application.MinimizeToTray &&
                   config1.Application.ShowInTaskbar == config2.Application.ShowInTaskbar;
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
                _logger.LogInformation("DEBUG: TestWarningPopup called - manually testing warning popup");
                await _notificationService.ShowEyeRestWarningAsync(TimeSpan.FromSeconds(10));
                _logger.LogInformation("DEBUG: TestWarningPopup completed successfully");
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
                _logger.LogInformation($"DEBUG: TestEyeRestPopup called - testing with {testDuration.TotalSeconds}s duration from configuration");
                await _notificationService.ShowEyeRestReminderAsync(testDuration);
                _logger.LogInformation("DEBUG: TestEyeRestPopup completed successfully");
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
                _logger.LogInformation("DEBUG: TestBreakWarningPopup called - manually testing break warning popup");
                await _notificationService.ShowBreakWarningAsync(TimeSpan.FromSeconds(30));
                _logger.LogInformation("DEBUG: TestBreakWarningPopup completed successfully");
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
                _logger.LogInformation("DEBUG: TestBreakPopup called - manually testing break popup");
                var progress = new Progress<double>();
                var breakDuration = TimeSpan.FromMinutes(BreakDurationMinutes);
                await _notificationService.ShowBreakReminderAsync(breakDuration, progress);
                _logger.LogInformation("DEBUG: TestBreakPopup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG: TestBreakPopup failed");
                MessageBox.Show($"Break popup test failed: {ex.Message}", "Test Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion
    }
}