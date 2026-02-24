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
    public class TimerConfigurationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<TimerConfigurationService>> _mockLogger;
        private readonly TimerConfigurationService _service;

        public TimerConfigurationServiceTests()
        {
            _mockLogger = new Mock<ILogger<TimerConfigurationService>>();
            _service = new TimerConfigurationService(_mockLogger.Object);
        }

        [Fact]
        public async Task GetDefaultConfiguration_ReturnsExpectedDefaults()
        {
            // Act
            var config = await _service.GetDefaultConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(20, config.EyeRest.DurationSeconds);
            Assert.True(config.EyeRest.StartSoundEnabled);
            Assert.True(config.EyeRest.EndSoundEnabled);
            Assert.True(config.EyeRest.WarningEnabled);
            Assert.Equal(30, config.EyeRest.WarningSeconds);

            Assert.Equal(55, config.Break.IntervalMinutes);
            Assert.Equal(5, config.Break.DurationMinutes);
            Assert.True(config.Break.WarningEnabled);
            Assert.Equal(30, config.Break.WarningSeconds);
            Assert.Equal(50, config.Break.OverlayOpacityPercent);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WhenNoFileExists_ReturnsDefaults()
        {
            // Act
            var config = await _service.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(55, config.Break.IntervalMinutes);
        }

        [Fact]
        public async Task SaveAndLoad_RoundTripsCorrectly()
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.EyeRest.IntervalMinutes = 15;
            config.EyeRest.DurationSeconds = 30;
            config.Break.IntervalMinutes = 45;
            config.Break.DurationMinutes = 10;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(15, loaded.EyeRest.IntervalMinutes);
            Assert.Equal(30, loaded.EyeRest.DurationSeconds);
            Assert.Equal(45, loaded.Break.IntervalMinutes);
            Assert.Equal(10, loaded.Break.DurationMinutes);
        }

        [Theory]
        [InlineData(0, 20)]   // Below minimum, corrected to default
        [InlineData(-5, 20)]  // Negative, corrected to default
        [InlineData(121, 20)] // Above maximum, corrected to default
        [InlineData(1, 1)]    // Minimum valid value
        [InlineData(120, 120)] // Maximum valid value
        [InlineData(20, 20)]  // Default value
        public async Task SaveConfigurationAsync_ValidatesEyeRestInterval(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.EyeRest.IntervalMinutes = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.EyeRest.IntervalMinutes);
        }

        [Theory]
        [InlineData(4, 20)]    // Below minimum (5), corrected to default
        [InlineData(301, 20)]  // Above maximum (300), corrected to default
        [InlineData(5, 5)]     // Minimum valid value
        [InlineData(300, 300)] // Maximum valid value
        [InlineData(20, 20)]   // Default value
        public async Task SaveConfigurationAsync_ValidatesEyeRestDuration(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.EyeRest.DurationSeconds = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.EyeRest.DurationSeconds);
        }

        [Theory]
        [InlineData(0, 55)]    // Below minimum, corrected to default
        [InlineData(241, 55)]  // Above maximum (240), corrected to default
        [InlineData(1, 1)]     // Minimum valid value
        [InlineData(240, 240)] // Maximum valid value
        public async Task SaveConfigurationAsync_ValidatesBreakInterval(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Break.IntervalMinutes = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.Break.IntervalMinutes);
        }

        [Theory]
        [InlineData(0, 5)]   // Below minimum (1), corrected to default
        [InlineData(31, 5)]  // Above maximum (30), corrected to default
        [InlineData(1, 1)]   // Minimum valid value
        [InlineData(30, 30)] // Maximum valid value
        public async Task SaveConfigurationAsync_ValidatesBreakDuration(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Break.DurationMinutes = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.Break.DurationMinutes);
        }

        [Theory]
        [InlineData(-1, 50)]   // Below minimum (0), corrected to default
        [InlineData(101, 50)]  // Above maximum (100), corrected to default
        [InlineData(0, 0)]     // Minimum valid value
        [InlineData(100, 100)] // Maximum valid value
        public async Task SaveConfigurationAsync_ValidatesOverlayOpacity(int inputValue, int expectedValue)
        {
            // Arrange
            var config = await _service.GetDefaultConfiguration();
            config.Break.OverlayOpacityPercent = inputValue;

            // Act
            await _service.SaveConfigurationAsync(config);
            var loaded = await _service.LoadConfigurationAsync();

            // Assert
            Assert.Equal(expectedValue, loaded.Break.OverlayOpacityPercent);
        }

        [Fact]
        public async Task ConfigurationChanged_Event_RaisedAfterSecondSave()
        {
            // Arrange
            var eventRaised = false;
            TimerConfigurationChangedEventArgs? eventArgs = null;

            _service.ConfigurationChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // First save to set current configuration
            var config = await _service.GetDefaultConfiguration();
            await _service.SaveConfigurationAsync(config);

            // Modify and save again to trigger event
            config.EyeRest.IntervalMinutes = 25;

            // Act
            await _service.SaveConfigurationAsync(config);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventArgs);
            Assert.Equal(25, eventArgs.NewConfiguration!.EyeRest.IntervalMinutes);
        }

        public void Dispose()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configFile = Path.Combine(appDataPath, "EyeRest", "timer-config.json");
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
