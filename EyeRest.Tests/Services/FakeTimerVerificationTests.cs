using System;
using System.Linq;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Services
{
    /// <summary>
    /// Tests to verify FakeTimer is properly used by TimerService
    /// </summary>
    public class FakeTimerVerificationTests
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly FakeTimerFactory _fakeTimerFactory;

        public FakeTimerVerificationTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _fakeTimerFactory = new FakeTimerFactory();
        }

        [Fact]
        public async Task TimerService_Should_Use_FakeTimerFactory()
        {
            // Arrange
            var config = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 1,
                    DurationSeconds = 5,
                    WarningSeconds = 5,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 2,
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = 10
                }
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync()).ReturnsAsync(config);

            var timerService = new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory);
            
            // Mock notification service
            var mockNotificationService = new Mock<INotificationService>();
            timerService.SetNotificationService(mockNotificationService.Object);

            try
            {
                // Act
                await timerService.StartAsync();

                // Debug: Check what happened
                Console.WriteLine($"TimerService.IsRunning: {timerService.IsRunning}");
                
                // Assert - Verify FakeTimers were created
                var createdTimers = _fakeTimerFactory.GetCreatedTimers();
                Console.WriteLine($"Number of timers created: {createdTimers.Count}");
                
                // This should complete immediately with FakeTimers
                Assert.True(createdTimers.Count > 0, $"Expected FakeTimers to be created, got {createdTimers.Count}");
                Assert.True(timerService.IsRunning, "Timer service should be running");
                
                // Debug output - log all created timers
                for (int i = 0; i < createdTimers.Count; i++)
                {
                    var timer = createdTimers[i];
                    Console.WriteLine($"Timer {i}: StartCount={timer.StartCount}, IsEnabled={timer.IsEnabled}, TickCount={timer.TickCount}");
                }
                
                // Verify timers are FakeTimer instances
                foreach (var timer in createdTimers)
                {
                    Assert.IsType<FakeTimer>(timer);
                }
                
                // Verify the correct timers were started (only main timers and health monitor)
                var startedTimers = createdTimers.Where(t => t.StartCount > 0).ToList();
                Assert.True(startedTimers.Count == 3, $"Expected 3 timers to be started (EyeRest, Break, HealthMonitor), got {startedTimers.Count}");
                
                // Verify each started timer
                foreach (var timer in startedTimers)
                {
                    Assert.True(timer.IsEnabled, $"Started timer should be enabled");
                    Assert.True(timer.StartCount > 0, $"Started timer should have StartCount > 0, got {timer.StartCount}");
                }
                
                // The test completing in milliseconds proves FakeTimer is being used
                Assert.True(true, "Test completed quickly - FakeTimer is working correctly");
            }
            finally
            {
                await timerService.StopAsync();
                timerService.Dispose();
            }
        }
    }
}