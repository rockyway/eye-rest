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
    public class UIConfigurationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<UIConfigurationService>> _mockLogger;
        private readonly UIConfigurationService _service;

        public UIConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<UIConfigurationService>>();
            _service = new UIConfigurationService(_mockLogger.Object);
        }

        [Fact]
        public async Task GetDefaultConfiguration_ReturnsExpectedDefaults()
        {
            // Act
            var config = await _service.GetDefaultConfiguration();

            // Assert
            Assert.NotNull(config);

            // Audio defaults
            Assert.True(config.Audio.Enabled);
            Assert.Null(config.Audio.CustomSoundPath);
            Assert.Equal(50, config.Audio.Volume);

            // Application defaults
            Assert.False(config.Application.StartWithWindows);
            Assert.True(config.Application.MinimizeToTray);
            Assert.False(config.Application.ShowInTaskbar);
            Assert.False(config.Application.IsDarkMode);

            // Analytics defaults
            Assert.True(config.Analytics.Enabled);
            Assert.False(config.Analytics.AutoOpenDashboard);
            Assert.Equal(90, config.Analytics.DataRetentionDays);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WhenNoFileExists_ReturnsDefaults()
        {
            // Act
            var config = await _service.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(config);
            Assert.True(config.Audio.Enabled);
            Assert.Equal(50, config.Audio.Volume);
            Assert.False(config.Application.IsDarkMode);
        }

        [Fact]
        public async Task SaveAndLoad_RoundTripsCorrectly()
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Audio.Volume = 75;
            config.Audio.Enabled = false;
            config.Application.IsDarkMode = true;
            config.Analytics.AutoOpenDashboard = true;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(75, loaded.Audio.Volume);
            Assert.False(loaded.Audio.Enabled);
            Assert.True(loaded.Application.IsDarkMode);
            Assert.True(loaded.Analytics.AutoOpenDashboard);
        }

        [Theory]
        [InlineData(-1, 50)]   // Below minimum (0), corrected to default
        [InlineData(101, 50)]  // Above maximum (100), corrected to default
        [InlineData(0, 0)]     // Minimum valid value
        [InlineData(100, 100)] // Maximum valid value
        [InlineData(50, 50)]   // Default value
        public async Task SaveConfigurationAsync_ValidatesVolume(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Audio.Volume = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.Audio.Volume);
        }

        [Fact]
        public async Task SaveDarkModeAsync_PersistsValue()
        {
            // Arrange - Load initial config to set current state
            await _service.LoadConfigurationAsync();

            // Act
            await _service.SaveDarkModeAsync(true);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.True(loaded.Application.IsDarkMode);
        }

        [Fact]
        public async Task SaveVolumeAsync_PersistsValue()
        {
            // Arrange - Load initial config to set current state
            await _service.LoadConfigurationAsync();

            // Act
            await _service.SaveVolumeAsync(80);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(80, loaded.Audio.Volume);
        }

        [Fact]
        public async Task SaveAutoOpenDashboardAsync_PersistsValue()
        {
            // Arrange - Load initial config to set current state
            await _service.LoadConfigurationAsync();

            // Act
            await _service.SaveAutoOpenDashboardAsync(true);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.True(loaded.Analytics.AutoOpenDashboard);
        }

        [Fact]
        public async Task ConfigurationChanged_Event_RaisedAfterSecondSave()
        {
            // Arrange
            var eventRaised = false;
            UIConfigurationChangedEventArgs? eventArgs = null;

            _service.ConfigurationChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // First save to set current configuration
            var config = await _service.GetDefaultConfiguration();
            await _service.SaveConfigurationAsync(config);

            // Modify and save again to trigger event
            config.Audio.Volume = 80;

            // Act
            await _service.SaveConfigurationAsync(config);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventArgs);
            Assert.Equal(80, eventArgs.NewConfiguration!.Audio.Volume);
        }

        [Fact]
        public async Task SaveConfigurationAsync_InvalidCustomSoundPath_ClearsPath()
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Audio.CustomSoundPath = "/nonexistent/path/to/sound.wav";

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert - Invalid path should be cleared
            Assert.Null(loaded.Audio.CustomSoundPath);
        }

        public void Dispose()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configFile = Path.Combine(appDataPath, "EyeRest", "ui-config.json");
                if (File.Exists(configFile))
                {
                    File.Delete(configFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
