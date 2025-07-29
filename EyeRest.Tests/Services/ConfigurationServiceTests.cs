using System;
using System.IO;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Services
{
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<ConfigurationService>> _mockLogger;
        private readonly ConfigurationService _configurationService;
        private readonly string _testConfigPath;

        public ConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<ConfigurationService>>();
            _configurationService = new ConfigurationService(_mockLogger.Object);
            
            // Use a temporary directory for testing
            var tempDir = Path.Combine(Path.GetTempPath(), "EyeRestTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            _testConfigPath = Path.Combine(tempDir, "config.json");
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
        public async Task LoadConfigurationAsync_WhenFileDoesNotExist_ReturnsDefaultConfiguration()
        {
            // Act
            var config = await _configurationService.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(55, config.Break.IntervalMinutes);
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
            config.EyeRest.IntervalMinutes = -5; // Invalid
            config.Break.DurationMinutes = 100; // Invalid
            config.Audio.Volume = 150; // Invalid

            // Act
            await _configurationService.SaveConfigurationAsync(config);

            // Assert - Load back and verify values were corrected
            var loadedConfig = await _configurationService.LoadConfigurationAsync();
            Assert.Equal(20, loadedConfig.EyeRest.IntervalMinutes); // Corrected to default
            Assert.Equal(5, loadedConfig.Break.DurationMinutes); // Corrected to default
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
                    Directory.Delete(testDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}