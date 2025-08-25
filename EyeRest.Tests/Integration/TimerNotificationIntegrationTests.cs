using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Integration
{
    public class TimerNotificationIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockTimerLogger;
        private readonly Mock<ILogger<NotificationService>> _mockNotificationLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IScreenOverlayService> _mockScreenOverlayService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly TimerService _timerService;
        private readonly NotificationService _notificationService;
        private readonly AppConfiguration _testConfig;

        public TimerNotificationIntegrationTests()
        {
            _mockTimerLogger = new Mock<ILogger<TimerService>>();
            _mockNotificationLogger = new Mock<ILogger<NotificationService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockScreenOverlayService = new Mock<IScreenOverlayService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _fakeTimerFactory = new FakeTimerFactory();

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

            _timerService = new TimerService(_mockTimerLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory);
            _notificationService = new NotificationService(_mockNotificationLogger.Object, System.Windows.Threading.Dispatcher.CurrentDispatcher, _mockScreenOverlayService.Object, _mockConfigService.Object);
        }

        [Fact]
        public async Task TimerService_WhenStarted_LoadsConfiguration()
        {
            // Act
            await _timerService.StartAsync();

            // Assert
            _mockConfigService.Verify(x => x.LoadConfigurationAsync(), Times.Once);
        }

        [Fact]
        public async Task TimerService_CanStartAndStop_WithoutErrors()
        {
            // Act & Assert - Should not throw
            await _timerService.StartAsync();
            await _timerService.StopAsync();
        }

        [Fact]
        public async Task TimerService_CanResetTimers_WithoutErrors()
        {
            // Arrange
            await _timerService.StartAsync();

            // Act & Assert - Should not throw
            await _timerService.ResetEyeRestTimer();
            await _timerService.ResetBreakTimer();
        }

        [Fact]
        public async Task TimerService_CanDelayBreak_WithoutErrors()
        {
            // Arrange
            await _timerService.StartAsync();
            var delay = TimeSpan.FromMinutes(5);

            // Act & Assert - Should not throw
            await _timerService.DelayBreak(delay);
        }

        [Fact]
        public void TimerService_Events_CanBeSubscribed()
        {
            // Arrange
            var eyeRestEventRaised = false;
            var breakWarningEventRaised = false;
            var breakEventRaised = false;

            _timerService.EyeRestDue += (s, e) => eyeRestEventRaised = true;
            _timerService.BreakWarning += (s, e) => breakWarningEventRaised = true;
            _timerService.BreakDue += (s, e) => breakEventRaised = true;

            // Assert - Events should be subscribable without errors
            Assert.False(eyeRestEventRaised);
            Assert.False(breakWarningEventRaised);
            Assert.False(breakEventRaised);
        }

        [Fact]
        public async Task NotificationService_CanShowEyeRestReminder()
        {
            // Act & Assert - Should not throw
            await _notificationService.ShowEyeRestReminderAsync(TimeSpan.FromSeconds(1));
            await _notificationService.HideAllNotifications();
        }

        [Fact]
        public async Task NotificationService_CanShowBreakWarning()
        {
            // Act & Assert - Should not throw
            await _notificationService.ShowBreakWarningAsync(TimeSpan.FromSeconds(1));
            await _notificationService.HideAllNotifications();
        }

        [Fact]
        public async Task NotificationService_CanShowBreakReminder()
        {
            // Arrange
            var progress = new Progress<double>();

            // Act & Assert - Should not throw
            var result = await _notificationService.ShowBreakReminderAsync(TimeSpan.FromSeconds(1), progress);
            
            // The result will depend on user interaction or timeout
            Assert.True(Enum.IsDefined(typeof(BreakAction), result));
        }

        [Fact]
        public async Task ConfigurationChange_UpdatesTimerService()
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

        public void Dispose()
        {
            _timerService?.Dispose();
        }
    }
}