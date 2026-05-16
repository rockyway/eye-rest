using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly ILogger<ConfigurationService> _logger;
        private readonly string _configFilePath;
        private readonly SemaphoreSlim _configLock = new(1, 1);
        private AppConfiguration? _currentConfiguration;
        private AppConfiguration? _cachedConfiguration;
        private DateTime _cacheTimestamp;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMilliseconds(500);

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
            // Short-lived cache to avoid redundant disk reads during startup burst
            if (_cachedConfiguration != null && (DateTime.UtcNow - _cacheTimestamp) < CacheDuration)
            {
                return _cachedConfiguration;
            }

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
                    var configuration = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, s_jsonOptions);

                    if (configuration == null)
                    {
                        _logger.LogWarning("Failed to deserialize configuration, using defaults");
                        return await GetDefaultConfiguration();
                    }

                    // Detect external config overwrite (SaveCount regression)
                    if (_currentConfiguration?.Meta != null && configuration.Meta != null
                        && configuration.Meta.SaveCount < _currentConfiguration.Meta.SaveCount)
                    {
                        _logger.LogWarning(
                            "🚨 CONFIG OVERWRITE DETECTED: SaveCount dropped from {Old} to {New}. " +
                            "File was overwritten by PID={PID}, Exe={Exe}. Restoring previous values.",
                            _currentConfiguration.Meta.SaveCount,
                            configuration.Meta.SaveCount,
                            configuration.Meta.ProcessId,
                            configuration.Meta.ExecutablePath);

                        // Preserve timer settings from the known-good config
                        configuration.EyeRest = _currentConfiguration.EyeRest;
                        configuration.Break = _currentConfiguration.Break;
                        configuration.Audio = _currentConfiguration.Audio;
                        configuration.Application = _currentConfiguration.Application;
                        configuration.UserPresence = _currentConfiguration.UserPresence;
                    }

                    // Validate configuration
                    var validatedConfig = ValidateConfiguration(configuration);
                    _currentConfiguration = validatedConfig;
                    _cachedConfiguration = validatedConfig;
                    _cacheTimestamp = DateTime.UtcNow;

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

        /// <summary>
        /// Saves configuration atomically (tmp → backup → move) with forensic metadata stamping.
        /// </summary>
        public async Task SaveConfigurationAsync(AppConfiguration config, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
        {
            // Stamp metadata for tracing who/what wrote to the config
            config.Meta ??= new Models.ConfigMetadata();
            config.Meta.SaveCount++;
            config.Meta.LastSavedBy = caller;
            config.Meta.LastSavedAt = DateTime.UtcNow.ToString("O");
            config.Meta.ProcessId = Environment.ProcessId;
            config.Meta.ExecutablePath = Environment.ProcessPath;
            var asm = System.Reflection.Assembly.GetEntryAssembly();
            config.Meta.AppVersion = asm?.GetName().Version?.ToString();
            config.Meta.BuildTimestamp = asm?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                is System.Reflection.AssemblyInformationalVersionAttribute[] attrs && attrs.Length > 0
                    ? attrs[0].InformationalVersion : null;

            const int maxRetries = 3;
            var retryDelay = TimeSpan.FromMilliseconds(100);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var validatedConfig = ValidateConfiguration(config);
                    _logger.LogInformation($"💾 CONFIG SAVE [{caller}]: EyeRest={validatedConfig.EyeRest.IntervalMinutes}min, Theme={validatedConfig.Application.ThemeMode}, SaveCount={validatedConfig.Meta.SaveCount}");

                    // Ensure directory exists with proper permissions
                    var directory = Path.GetDirectoryName(_configFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        _logger.LogInformation("Creating configuration directory: {Directory}", directory);
                        Directory.CreateDirectory(directory);
                    }

                    // CRITICAL FIX: Atomic file write using temporary file + move operation
                    // This prevents corruption and file locking issues
                    var tempFilePath = _configFilePath + ".tmp";
                    var backupFilePath = _configFilePath + ".backup";

                    _logger.LogDebug("Saving configuration to temp file: {TempFile}", tempFilePath);

                    // Step 1: Write to temporary file
                    using (var stream = File.Create(tempFilePath))
                    {
                        await JsonSerializer.SerializeAsync(stream, validatedConfig, s_jsonOptions);
                        await stream.FlushAsync(); // Ensure data is written to disk
                    }

                    _logger.LogDebug("Temp file written successfully, size: {Size} bytes", new FileInfo(tempFilePath).Length);

                    // Step 2: Create backup of existing file (if it exists)
                    if (File.Exists(_configFilePath))
                    {
                        if (File.Exists(backupFilePath))
                        {
                            File.Delete(backupFilePath); // Remove old backup
                        }
                        File.Copy(_configFilePath, backupFilePath);
                        _logger.LogDebug("Created backup: {BackupFile}", backupFilePath);
                    }

                    // Step 3: Atomic replace - move temp file to final location
                    _logger.LogDebug("Moving temp file to final location: {ConfigFile}", _configFilePath);
                    File.Move(tempFilePath, _configFilePath, overwrite: true);

                    // Step 4: Clean up backup file after successful write
                    if (File.Exists(backupFilePath))
                    {
                        File.Delete(backupFilePath);
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

                    _logger.LogInformation("Configuration saved successfully on attempt {Attempt}", attempt);
                    _cachedConfiguration = null;
                    return; // Success - exit retry loop
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    _logger.LogError(uaEx, "Access denied saving configuration. Config path: {Path}, Attempt: {Attempt}/{MaxRetries}",
                        _configFilePath, attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError("Failed to save configuration after {MaxRetries} attempts due to access denied. " +
                            "Check folder permissions for: {Directory}",
                            maxRetries, Path.GetDirectoryName(_configFilePath));
                        throw;
                    }

                    // Wait before retry
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2);
                }
                catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process") && attempt < maxRetries)
                {
                    _logger.LogWarning("Configuration file locked on attempt {Attempt}/{MaxRetries}, retrying in {DelayMs}ms: {Error}",
                        attempt, maxRetries, retryDelay.TotalMilliseconds, ioEx.Message);

                    // Wait before retry with exponential backoff
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving configuration on attempt {Attempt}/{MaxRetries}", attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        // Clean up any temporary files on final failure
                        try
                        {
                            var tempFilePath = _configFilePath + ".tmp";
                            if (File.Exists(tempFilePath))
                            {
                                File.Delete(tempFilePath);
                            }
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }

                        throw; // Re-throw on final attempt
                    }

                    // Wait before retry
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2);
                }
            }
        }

        public async Task UpdateConfigurationAsync(Action<AppConfiguration> modifier, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
        {
            await _configLock.WaitAsync();
            try
            {
                var config = await LoadConfigurationAsync();
                modifier(config);
                await SaveConfigurationAsync(config, caller);
            }
            finally
            {
                _configLock.Release();
            }
        }

        public async Task ReplaceConfigurationAsync(AppConfiguration config, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
        {
            await _configLock.WaitAsync();
            try
            {
                await SaveConfigurationAsync(config, caller);
            }
            finally
            {
                _configLock.Release();
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
                    WarningEnabled = true,
                    WarningSeconds = 15  // FIXED: Use 15s warning for eye rest (short)
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,  // FIXED: Use correct PRD default (55 minutes)
                    DurationMinutes = 5,   // FIXED: Use correct PRD default (5 minutes)
                    WarningEnabled = true,
                    WarningSeconds = 30,   // Keep 30s warning for break (longer)
                    OverlayOpacityPercent = 50
                },
                Audio = new AudioSettings
                {
                    Enabled = true,
                    Volume = 50
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false,
                    ThemeMode = ThemeMode.Auto
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
                config.EyeRest.WarningSeconds = 15;
            }

            // Break validation - Allow shorter intervals for testing (1-240 minutes)
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

            if (config.Break.WarningSeconds < 10 || config.Break.WarningSeconds > 300)
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

            // BL-002: per-channel CustomFilePath validation now happens at playback time
            // (PlayChannelAsync falls back to Default on missing file). The legacy global
            // AudioSettings.CustomSoundPath was removed in schema v2.

            return config;
        }
    }
}