using System;
using System.IO;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<ConfigurationService>> _mockLogger;
        private readonly ConfigurationService _configurationService;

        public ConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<ConfigurationService>>();
            _configurationService = new ConfigurationService(_mockLogger.Object);
        }

        [Fact]
        public async Task GetDefaultConfiguration_ReturnsValidDefaults()
        {
            // Act
            var config = await _configurationService.GetDefaultConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(20, config.EyeRest.DurationSeconds);
            Assert.True(config.EyeRest.StartSoundEnabled);
            Assert.True(config.EyeRest.EndSoundEnabled);

            Assert.Equal(55, config.Break.IntervalMinutes);
            Assert.Equal(5, config.Break.DurationMinutes);
            Assert.True(config.Break.WarningEnabled);
            Assert.Equal(30, config.Break.WarningSeconds);

            Assert.True(config.Audio.Enabled);
            Assert.Null(config.Audio.CustomSoundPath);
            Assert.Equal(50, config.Audio.Volume);

            Assert.False(config.Application.StartWithWindows);
            Assert.True(config.Application.MinimizeToTray);
            Assert.False(config.Application.ShowInTaskbar);
        }

        [Fact]
        public async Task LoadConfigurationAsync_ReturnsNonNullConfiguration()
        {
            // Act
            var config = await _configurationService.LoadConfigurationAsync();

            // Assert - Configuration should always be returned (either from file or defaults)
            Assert.NotNull(config);
            Assert.NotNull(config.EyeRest);
            Assert.NotNull(config.Break);
            Assert.NotNull(config.Audio);
            Assert.NotNull(config.Application);
            Assert.InRange(config.EyeRest.IntervalMinutes, 1, 120);
            Assert.InRange(config.Break.IntervalMinutes, 1, 240);
        }

        [Fact]
        public async Task SaveConfigurationAsync_ValidConfiguration_SavesSuccessfully()
        {
            // Arrange
            var config = await _configurationService.GetDefaultConfiguration();
            config.EyeRest.IntervalMinutes = 25;
            config.Break.IntervalMinutes = 60;

            // Act
            await _configurationService.SaveConfigurationAsync(config);

            // Assert - Load the configuration back and verify
            var loadedConfig = await _configurationService.LoadConfigurationAsync();
            Assert.Equal(25, loadedConfig.EyeRest.IntervalMinutes);
            Assert.Equal(60, loadedConfig.Break.IntervalMinutes);
        }

        [Fact]
        public async Task SaveConfigurationAsync_InvalidValues_ValidatesAndCorrects()
        {
            // Arrange
            var config = await _configurationService.GetDefaultConfiguration();
            config.EyeRest.IntervalMinutes = -5; // Invalid: below minimum
            config.Break.DurationMinutes = 100; // Invalid: above maximum
            config.Audio.Volume = 150; // Invalid: above maximum

            // Act
            await _configurationService.SaveConfigurationAsync(config);

            // Assert - Load back and verify values were corrected
            var loadedConfig = await _configurationService.LoadConfigurationAsync();
            Assert.Equal(20, loadedConfig.EyeRest.IntervalMinutes); // Corrected to default
            Assert.InRange(loadedConfig.Break.DurationMinutes, 1, 30); // Corrected within valid range
            Assert.Equal(50, loadedConfig.Audio.Volume); // Corrected to default
        }

        [Fact]
        public async Task ConfigurationChanged_Event_RaisedOnSave()
        {
            // Arrange
            var eventRaised = false;
            ConfigurationChangedEventArgs? eventArgs = null;

            _configurationService.ConfigurationChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Load initial configuration to set current state
            await _configurationService.LoadConfigurationAsync();

            var config = await _configurationService.GetDefaultConfiguration();
            config.EyeRest.IntervalMinutes = 30;

            // Act
            await _configurationService.SaveConfigurationAsync(config);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventArgs);
            Assert.Equal(30, eventArgs.NewConfiguration.EyeRest.IntervalMinutes);
        }

        public void Dispose()
        {
            // Clean up test files
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var testDir = Path.Combine(appDataPath, "EyeRest");
                if (Directory.Exists(testDir))
                {
                    // Only delete config files we may have created, not the entire folder
                    var configFile = Path.Combine(testDir, "config.json");
                    var tmpFile = configFile + ".tmp";
                    var backupFile = configFile + ".backup";

                    if (File.Exists(tmpFile)) File.Delete(tmpFile);
                    if (File.Exists(backupFile)) File.Delete(backupFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
