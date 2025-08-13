using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class UIConfigurationService : IUIConfigurationService
    {
        private readonly ILogger<UIConfigurationService> _logger;
        private readonly string _configFilePath;
        private UIConfiguration? _currentConfiguration;

        public event EventHandler<UIConfigurationChangedEventArgs>? ConfigurationChanged;

        public UIConfigurationService(ILogger<UIConfigurationService> logger)
        {
            _logger = logger;
            
            // Store configuration in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "EyeRest");
            
            // Ensure directory exists
            Directory.CreateDirectory(appFolder);
            
            _configFilePath = Path.Combine(appFolder, "ui-config.json");
        }

        public async Task<UIConfiguration> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogInformation("UI configuration file not found, creating default configuration");
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

                    var configuration = await JsonSerializer.DeserializeAsync<UIConfiguration>(stream, options);

                    if (configuration == null)
                    {
                        _logger.LogWarning("Failed to deserialize UI configuration, using defaults");
                        return await GetDefaultConfiguration();
                    }

                    // Validate configuration
                    var validatedConfig = ValidateConfiguration(configuration);
                    _currentConfiguration = validatedConfig;

                    _logger.LogInformation("UI configuration loaded successfully");
                    return validatedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading UI configuration, using defaults");
                return await GetDefaultConfiguration();
            }
        }

        public async Task SaveConfigurationAsync(UIConfiguration config)
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
                    ConfigurationChanged?.Invoke(this, new UIConfigurationChangedEventArgs
                    {
                        NewConfiguration = validatedConfig,
                        OldConfiguration = oldConfig
                    });
                }

                _logger.LogInformation("UI configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving UI configuration");
                throw;
            }
        }

        public Task<UIConfiguration> GetDefaultConfiguration()
        {
            var defaultConfig = new UIConfiguration
            {
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
                }
            };

            return Task.FromResult(defaultConfig);
        }

        // Auto-save specific settings immediately
        public async Task SaveDarkModeAsync(bool isDarkMode)
        {
            try
            {
                var config = _currentConfiguration ?? await GetDefaultConfiguration();
                config.Application.IsDarkMode = isDarkMode;
                await SaveConfigurationAsync(config);
                _logger.LogInformation($"Dark mode auto-saved: {isDarkMode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save dark mode setting");
                throw;
            }
        }

        public async Task SaveVolumeAsync(int volume)
        {
            try
            {
                var config = _currentConfiguration ?? await GetDefaultConfiguration();
                config.Audio.Volume = volume;
                await SaveConfigurationAsync(config);
                _logger.LogInformation($"Volume auto-saved: {volume}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save volume setting");
                throw;
            }
        }

        public async Task SaveAutoOpenDashboardAsync(bool autoOpen)
        {
            try
            {
                var config = _currentConfiguration ?? await GetDefaultConfiguration();
                config.Analytics.AutoOpenDashboard = autoOpen;
                await SaveConfigurationAsync(config);
                _logger.LogInformation($"Auto-open dashboard auto-saved: {autoOpen}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-save auto-open dashboard setting");
                throw;
            }
        }

        private UIConfiguration ValidateConfiguration(UIConfiguration config)
        {
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