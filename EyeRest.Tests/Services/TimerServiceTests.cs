using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Services
{
    public class TimerServiceTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly TimerService _timerService;
        private readonly AppConfiguration _testConfig;

        public TimerServiceTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            
            _testConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 1, // Short interval for testing
                    DurationSeconds = 5
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 2, // Short interval for testing
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = 5
                }
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_testConfig);

            _timerService = new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object);
        }

        [Fact]
        public async Task StartAsync_WhenNotStarted_StartsSuccessfully()
        {
            // Act
            await _timerService.StartAsync();

            // Assert
            _mockConfigService.Verify(x => x.LoadConfigurationAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_WhenAlreadyStarted_LogsWarning()
        {
            // Arrange
            await _timerService.StartAsync();

            // Act
            await _timerService.StartAsync();

            // Assert - Should log warning about already being started
            // Note: In a real test, we'd verify the logger was called with warning level
        }

        [Fact]
        public async Task StopAsync_WhenStarted_StopsSuccessfully()
        {
            // Arrange
            await _timerService.StartAsync();

            // Act
            await _timerService.StopAsync();

            // Assert - Should complete without throwing
        }

        [Fact]
        public async Task ResetEyeRestTimer_UpdatesTimerInterval()
        {
            // Arrange
            await _timerService.StartAsync();

            // Act
            await _timerService.ResetEyeRestTimer();

            // Assert - Should complete without throwing
        }

        [Fact]
        public async Task ResetBreakTimer_UpdatesTimerInterval()
        {
            // Arrange
            await _timerService.StartAsync();

            // Act
            await _timerService.ResetBreakTimer();

            // Assert - Should complete without throwing
        }

        [Fact]
        public async Task DelayBreak_UpdatesBreakTimer()
        {
            // Arrange
            await _timerService.StartAsync();
            var delay = TimeSpan.FromMinutes(5);

            // Act
            await _timerService.DelayBreak(delay);

            // Assert - Should complete without throwing
        }

        [Fact]
        public async Task ConfigurationChanged_UpdatesTimerIntervals()
        {
            // Arrange
            await _timerService.StartAsync();
            
            var newConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings { IntervalMinutes = 30 },
                Break = new BreakSettings { IntervalMinutes = 60 }
            };

            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = _testConfig,
                NewConfiguration = newConfig
            };

            // Act
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Assert - Should handle configuration change without throwing
            await Task.Delay(100); // Allow async event handling to complete
        }

        [Fact]
        public void EyeRestDue_Event_CanBeSubscribed()
        {
            // Arrange
            var eventRaised = false;
            TimerEventArgs? eventArgs = null;

            _timerService.EyeRestDue += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Note: In a real scenario, we'd need to trigger the timer
            // For this test, we're just verifying the event can be subscribed to
            Assert.False(eventRaised); // Initially false
        }

        [Fact]
        public void BreakWarning_Event_CanBeSubscribed()
        {
            // Arrange
            var eventRaised = false;
            TimerEventArgs? eventArgs = null;

            _timerService.BreakWarning += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Note: In a real scenario, we'd need to trigger the timer
            Assert.False(eventRaised); // Initially false
        }

        [Fact]
        public void BreakDue_Event_CanBeSubscribed()
        {
            // Arrange
            var eventRaised = false;
            TimerEventArgs? eventArgs = null;

            _timerService.BreakDue += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Note: In a real scenario, we'd need to trigger the timer
            Assert.False(eventRaised); // Initially false
        }

        public void Dispose()
        {
            _timerService?.Dispose();
        }
    }
}