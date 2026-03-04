using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class TimerConfigurationService : ITimerConfigurationService
    {
        private readonly ILogger<TimerConfigurationService> _logger;
        private readonly string _configFilePath;
        private TimerConfiguration? _currentConfiguration;

        public event EventHandler<TimerConfigurationChangedEventArgs>? ConfigurationChanged;

        public TimerConfigurationService(ILogger<TimerConfigurationService> logger)
        {
            _logger = logger;
            
            // Store configuration in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "EyeRest");
            
            // Ensure directory exists
            Directory.CreateDirectory(appFolder);
            
            _configFilePath = Path.Combine(appFolder, "timer-config.json");
        }

        public async Task<TimerConfiguration> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogInformation("Timer configuration file not found, creating default configuration");
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

                    var configuration = await JsonSerializer.DeserializeAsync<TimerConfiguration>(stream, options);

                    if (configuration == null)
                    {
                        _logger.LogWarning("Failed to deserialize timer configuration, using defaults");
                        return await GetDefaultConfiguration();
                    }

                    // Validate configuration
                    var validatedConfig = ValidateConfiguration(configuration);
                    _currentConfiguration = validatedConfig;

                    _logger.LogInformation("Timer configuration loaded successfully");
                    return validatedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading timer configuration, using defaults");
                return await GetDefaultConfiguration();
            }
        }

        public async Task SaveConfigurationAsync(TimerConfiguration config)
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
                    ConfigurationChanged?.Invoke(this, new TimerConfigurationChangedEventArgs
                    {
                        NewConfiguration = validatedConfig,
                        OldConfiguration = oldConfig
                    });
                }

                _logger.LogInformation("Timer configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving timer configuration");
                throw;
            }
        }

        public Task<TimerConfiguration> GetDefaultConfiguration()
        {
            var defaultConfig = new TimerConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    StartSoundEnabled = true,
                    EndSoundEnabled = true,
                    WarningEnabled = true,
                    WarningSeconds = 15
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,  // Correct PRD default
                    DurationMinutes = 5,   // Correct PRD default
                    WarningEnabled = true,
                    WarningSeconds = 30,
                    OverlayOpacityPercent = 50
                }
            };

            return Task.FromResult(defaultConfig);
        }

        private TimerConfiguration ValidateConfiguration(TimerConfiguration config)
        {
            // Validate and correct any invalid values
            
            // Eye rest validation
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
                config.EyeRest.WarningSeconds = 15;
            }

            // Break validation
            if (config.Break.IntervalMinutes < 1 || config.Break.IntervalMinutes > 240)
            {
                _logger.LogWarning($"Invalid break interval: {config.Break.IntervalMinutes}, using default");
                config.Break.IntervalMinutes = 55;
            }

            if (config.Break.DurationMinutes < 1 || config.Break.DurationMinutes > 30)
            {
                _logger.LogWarning($"Invalid break duration: {config.Break.DurationMinutes}, using default");
                config.Break.DurationMinutes = 5;
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

            return config;
        }
    }
}