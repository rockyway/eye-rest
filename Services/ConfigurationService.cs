using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger<ConfigurationService> _logger;
        private readonly string _configFilePath;
        private AppConfiguration? _currentConfiguration;

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ConfigurationService(ILogger<ConfigurationService> logger)
        {
            _logger = logger;
            
            // Store configuration in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "EyeRest");
            
            // Ensure directory exists
            Directory.CreateDirectory(appFolder);
            
            _configFilePath = Path.Combine(appFolder, "config.json");
        }

        public async Task<AppConfiguration> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogInformation("Configuration file not found, creating default configuration");
                    var defaultConfig = await GetDefaultConfiguration();
                    await SaveConfigurationAsync(defaultConfig);
                    return defaultConfig;
                }

                using (var stream = File.OpenRead(_configFilePath))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    };

                    var configuration = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, options);

                    if (configuration == null)
                    {
                        _logger.LogWarning("Failed to deserialize configuration, using defaults");
                        return await GetDefaultConfiguration();
                    }

                    // Validate configuration
                    var validatedConfig = ValidateConfiguration(configuration);
                    _currentConfiguration = validatedConfig;

                    _logger.LogInformation("Configuration loaded successfully");
                    return validatedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration, using defaults");
                return await GetDefaultConfiguration();
            }
        }

        public async Task SaveConfigurationAsync(AppConfiguration config)
        {
            try
            {
                var validatedConfig = ValidateConfiguration(config);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                using (var stream = File.Create(_configFilePath))
                {
                    await JsonSerializer.SerializeAsync(stream, validatedConfig, options);
                }

                var oldConfig = _currentConfiguration;
                _currentConfiguration = validatedConfig;

                // Raise configuration changed event
                if (oldConfig != null)
                {
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
                    {
                        NewConfiguration = validatedConfig,
                        OldConfiguration = oldConfig
                    });
                }

                _logger.LogInformation("Configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration");
                throw;
            }
        }

        public Task<AppConfiguration> GetDefaultConfiguration()
        {
            var defaultConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    StartSoundEnabled = true,
                    EndSoundEnabled = true,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 10,
                    DurationMinutes = 2,
                    WarningEnabled = true,
                    WarningSeconds = 30,
                    OverlayOpacityPercent = 50
                },
                Audio = new AudioSettings
                {
                    Enabled = true,
                    CustomSoundPath = null,
                    Volume = 50
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false,
                    IsDarkMode = false
                },
                Analytics = new AnalyticsSettings
                {
                    Enabled = true,
                    AutoOpenDashboard = false,
                    DataRetentionDays = 90,
                    TrackBreakEvents = true,
                    TrackPresenceChanges = true,
                    TrackMeetingEvents = true,
                    TrackUserSessions = true,
                    AllowDataExport = true,
                    ExportFormat = "JSON",
                    AutoCleanupOldData = true,
                    DatabaseMaintenanceIntervalDays = 7
                },
                UserPresence = new UserPresenceSettings(),
                MeetingDetection = new Models.MeetingDetectionSettings(),
                TimerControls = new TimerControlSettings()
            };

            return Task.FromResult(defaultConfig);
        }

        private AppConfiguration ValidateConfiguration(AppConfiguration config)
        {
            // Validate and correct any invalid values
            
            // Eye rest validation - Allow very short intervals for testing
            if (config.EyeRest.IntervalMinutes < 1 || config.EyeRest.IntervalMinutes > 120)
            {
                _logger.LogWarning($"Invalid eye rest interval: {config.EyeRest.IntervalMinutes}, using default");
                config.EyeRest.IntervalMinutes = 20;
            }

            if (config.EyeRest.DurationSeconds < 5 || config.EyeRest.DurationSeconds > 300)
            {
                _logger.LogWarning($"Invalid eye rest duration: {config.EyeRest.DurationSeconds}, using default");
                config.EyeRest.DurationSeconds = 20;
            }
            
            if (config.EyeRest.WarningSeconds < 10 || config.EyeRest.WarningSeconds > 120)
            {
                _logger.LogWarning($"Invalid eye rest warning seconds: {config.EyeRest.WarningSeconds}, using default");
                config.EyeRest.WarningSeconds = 30;
            }

            // Break validation - Allow shorter intervals for testing (1-240 minutes)
            if (config.Break.IntervalMinutes < 1 || config.Break.IntervalMinutes > 240)
            {
                _logger.LogWarning($"Invalid break interval: {config.Break.IntervalMinutes}, using default");
                config.Break.IntervalMinutes = 10;
            }

            if (config.Break.DurationMinutes < 1 || config.Break.DurationMinutes > 30)
            {
                _logger.LogWarning($"Invalid break duration: {config.Break.DurationMinutes}, using default");
                config.Break.DurationMinutes = 2;
            }

            if (config.Break.WarningSeconds < 10 || config.Break.WarningSeconds > 120)
            {
                _logger.LogWarning($"Invalid warning seconds: {config.Break.WarningSeconds}, using default");
                config.Break.WarningSeconds = 30;
            }

            // Validate overlay opacity
            if (config.Break.OverlayOpacityPercent < 0 || config.Break.OverlayOpacityPercent > 100)
            {
                _logger.LogWarning($"Invalid overlay opacity: {config.Break.OverlayOpacityPercent}, using default");
                config.Break.OverlayOpacityPercent = 50;
            }

            // Audio validation
            if (config.Audio.Volume < 0 || config.Audio.Volume > 100)
            {
                _logger.LogWarning($"Invalid volume: {config.Audio.Volume}, using default");
                config.Audio.Volume = 50;
            }

            // Validate custom sound path if provided
            if (!string.IsNullOrEmpty(config.Audio.CustomSoundPath) && !File.Exists(config.Audio.CustomSoundPath))
            {
                _logger.LogWarning($"Custom sound file not found: {config.Audio.CustomSoundPath}, clearing path");
                config.Audio.CustomSoundPath = null;
            }

            return config;
        }
    }
}