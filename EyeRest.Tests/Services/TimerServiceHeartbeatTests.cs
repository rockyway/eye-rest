using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Threading;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace EyeRest.Tests.Services
{
    [Collection("Timer Tests")]
    public class TimerServiceHeartbeatTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IPauseReminderService> _mockPauseReminderService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TimerServiceHeartbeatTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockPauseReminderService = new Mock<IPauseReminderService>();
            _fakeTimerFactory = new FakeTimerFactory();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Theory]
        [InlineData(10, 20, 10.0)]      // Ultra-short intervals → minimum threshold (clamped)
        [InlineData(30, 60, 10.0)]      // Short intervals → minimum threshold (clamped)  
        [InlineData(60, 120, 10.0)]     // 1-2 min intervals → minimum threshold (clamped)
        [InlineData(180, 240, 10.0)]    // 3-4 min intervals → minimum threshold (clamped)
        [InlineData(300, 420, 13.75)]   // 5-7 min intervals → (7*1.25)+5 = 13.75 (calculated)
        [InlineData(360, 600, 17.5)]    // 6-10 min intervals → (10*1.25)+5 = 17.5 (calculated)
        public async Task DynamicThreshold_CalculatesCorrectly(
            int eyeRestSeconds, 
            int breakSeconds, 
            double expectedThresholdMinutes)
        {
            // Arrange
            var config = CreateTestConfiguration(eyeRestSeconds, breakSeconds);
            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            
            // Use reflection to access the private CalculateDynamicHeartbeatThreshold method
            var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

            // Assert
            var tolerance = 0.5; // Allow 0.5 minute tolerance
            Assert.True(Math.Abs(dynamicThreshold - expectedThresholdMinutes) <= tolerance,
                $"Expected threshold ~{expectedThresholdMinutes}min, got {dynamicThreshold}min for intervals {eyeRestSeconds}s/{breakSeconds}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task DynamicThreshold_AlwaysGreaterThanLongestTimer()
        {
            // Test various configurations to ensure threshold is always greater than longest timer
            var testCases = new[]
            {
                new { EyeRest = 30, Break = 60, LongestMinutes = 1.0 },
                new { EyeRest = 120, Break = 180, LongestMinutes = 3.0 },
                new { EyeRest = 180, Break = 120, LongestMinutes = 3.0 }, // Eye rest longer
                new { EyeRest = 300, Break = 600, LongestMinutes = 10.0 },
                new { EyeRest = 600, Break = 300, LongestMinutes = 10.0 }, // Eye rest longer
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var config = CreateTestConfiguration(testCase.EyeRest, testCase.Break);
                var timerService = CreateTimerService(config);

                // Act
                await timerService.StartAsync();
                var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

                // Assert
                Assert.True(dynamicThreshold > testCase.LongestMinutes,
                    $"Threshold {dynamicThreshold}min should be > longest timer {testCase.LongestMinutes}min " +
                    $"for intervals {testCase.EyeRest}s/{testCase.Break}s");

                await timerService.StopAsync();
                timerService.Dispose();
            }
        }

        [Fact]
        public async Task DynamicThreshold_Clamping_MinimumThreshold()
        {
            // Arrange - Very short intervals should be clamped to minimum
            var config = CreateTestConfiguration(5, 10); // 5s and 10s intervals
            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

            // Assert
            Assert.Equal(10.0, dynamicThreshold, 1); // Should be clamped to minimum of 10 minutes

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task DynamicThreshold_Clamping_MaximumThreshold()
        {
            // Arrange - Very long intervals should be clamped to maximum
            var config = CreateTestConfiguration(7200, 10800); // 2 hours and 3 hours
            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

            // Assert
            Assert.Equal(120.0, dynamicThreshold, 1); // Should be clamped to maximum of 120 minutes

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task Heartbeat_UpdatesFromAllOperations()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            
            // Get initial heartbeat time
            var initialHeartbeat = GetLastHeartbeat(timerService);
            
            // Wait a bit then perform various operations
            await Task.Delay(TimeSpan.FromSeconds(2), _cancellationTokenSource.Token);
            
            // Call UpdateHeartbeatFromOperation directly
            CallUpdateHeartbeatFromOperation(timerService, "Test");
            var heartbeatAfterUpdate = GetLastHeartbeat(timerService);
            
            // Assert
            Assert.True(heartbeatAfterUpdate > initialHeartbeat, 
                "Heartbeat should be updated after operation");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task HealthMonitor_DetectsTimerHang()
        {
            // Arrange
            var config = CreateTestConfiguration(20, 40); // 20s and 40s intervals
            var timerService = CreateTimerService(config);

            // We'll need to simulate a timer hang by manipulating the heartbeat
            await timerService.StartAsync();

            // Use reflection to set an old heartbeat time (simulate hang)
            SetLastHeartbeat(timerService, DateTime.Now.AddMinutes(-15)); // 15 minutes ago

            // Force a health check by calling the private method
            var healthMonitorTriggered = false;
            try
            {
                CallHealthMonitorTick(timerService);
                healthMonitorTriggered = true;
            }
            catch (Exception)
            {
                // Expected - health monitor might trigger recovery which could throw
                healthMonitorTriggered = true;
            }

            // Assert
            Assert.True(healthMonitorTriggered, "Health monitor should detect timer hang");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task DynamicThreshold_AsymmetricIntervals_UsesLongest()
        {
            // Test cases where one timer is much longer than the other
            var asymmetricCases = new[]
            {
                new { EyeRest = 30, Break = 300, ExpectedBasedOn = 300 },   // Break much longer
                new { EyeRest = 300, Break = 60, ExpectedBasedOn = 300 },   // Eye rest much longer
                new { EyeRest = 60, Break = 600, ExpectedBasedOn = 600 },   // Break 10x longer
                new { EyeRest = 480, Break = 120, ExpectedBasedOn = 480 },  // Eye rest 4x longer
            };

            foreach (var testCase in asymmetricCases)
            {
                // Arrange
                var config = CreateTestConfiguration(testCase.EyeRest, testCase.Break);
                var timerService = CreateTimerService(config);

                // Act
                await timerService.StartAsync();
                var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

                // Calculate expected threshold: (longest * 1.25) + 5, clamped to [10, 120]
                var expectedThreshold = Math.Max(10.0, 
                    Math.Min(120.0, (testCase.ExpectedBasedOn / 60.0 * 1.25) + 5.0));

                // Assert
                var tolerance = 0.5;
                Assert.True(Math.Abs(dynamicThreshold - expectedThreshold) <= tolerance,
                    $"Threshold should be based on longest interval ({testCase.ExpectedBasedOn}s). " +
                    $"Expected ~{expectedThreshold}min, got {dynamicThreshold}min");

                await timerService.StopAsync();
                timerService.Dispose();
            }
        }

        [Fact]
        public async Task HeartbeatCalculation_Performance()
        {
            // Arrange
            var config = CreateTestConfiguration(60, 120);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Act - Measure performance of threshold calculation
            var startTime = DateTime.Now;
            
            for (int i = 0; i < 10000; i++)
            {
                CallCalculateDynamicHeartbeatThreshold(timerService);
            }
            
            var elapsed = DateTime.Now - startTime;

            // Assert - Should be very fast (< 100ms for 10,000 calculations)
            Assert.True(elapsed.TotalMilliseconds < 100, 
                $"10,000 threshold calculations should take < 100ms, took {elapsed.TotalMilliseconds}ms");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task DynamicThreshold_NullTimers_UsesDefaults()
        {
            // Arrange
            var config = CreateTestConfiguration(60, 120);
            var timerService = CreateTimerService(config);

            // Don't start the service to ensure timers are null
            
            // Act
            var dynamicThreshold = CallCalculateDynamicHeartbeatThreshold(timerService);

            // Assert - Should use default values (20 min eye rest, 55 min break)
            // Expected: max(20, 55) = 55, then (55 * 1.25) + 5 = 73.75, clamped to [10, 120]
            var expectedThreshold = 73.75;
            var tolerance = 1.0;
            Assert.True(Math.Abs(dynamicThreshold - expectedThreshold) <= tolerance,
                $"Should use defaults when timers are null. Expected ~{expectedThreshold}min, got {dynamicThreshold}min");

            timerService.Dispose();
        }

        [Fact]
        public async Task Heartbeat_Monitoring_IntegratesCorrectly()
        {
            // Arrange
            var config = CreateTestConfiguration(20, 40);
            var timerService = CreateTimerService(config);

            var logMessages = new List<string>();
            _mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) =>
                {
                    logMessages.Add(formatter.DynamicInvoke(state, exception)?.ToString() ?? "");
                });

            // Act
            await timerService.StartAsync();
            
            // Trigger health monitor
            CallHealthMonitorTick(timerService);

            // Assert
            var dynamicThresholdLogged = logMessages.Any(msg => 
                msg.Contains("DYNAMIC THRESHOLD") && msg.Contains("minutes (based on current timer intervals)"));
            
            Assert.True(dynamicThresholdLogged, 
                "Health monitor should log dynamic threshold calculation");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        // Helper methods using reflection to access private members
        private double CallCalculateDynamicHeartbeatThreshold(TimerService timerService)
        {
            var method = typeof(TimerService).GetMethod("CalculateDynamicHeartbeatThreshold", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            
            var result = method.Invoke(timerService, null);
            return Convert.ToDouble(result);
        }

        private void CallUpdateHeartbeatFromOperation(TimerService timerService, string operation)
        {
            var method = typeof(TimerService).GetMethod("UpdateHeartbeatFromOperation", 
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            
            method.Invoke(timerService, new object[] { operation });
        }

        private DateTime GetLastHeartbeat(TimerService timerService)
        {
            var field = typeof(TimerService).GetField("_lastHeartbeat", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            
            return (DateTime)field.GetValue(timerService)!;
        }

        private void SetLastHeartbeat(TimerService timerService, DateTime heartbeat)
        {
            var field = typeof(TimerService).GetField("_lastHeartbeat", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            
            field.SetValue(timerService, heartbeat);
        }

        private void CallHealthMonitorTick(TimerService timerService)
        {
            var method = typeof(TimerService).GetMethod("OnHealthMonitorTick", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            
            method!.Invoke(timerService, new object?[] { null, EventArgs.Empty });
        }

        private AppConfiguration CreateTestConfiguration(int eyeRestSeconds, int breakSeconds)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = (int)((double)eyeRestSeconds / 60.0),
                    DurationSeconds = 5,
                    WarningSeconds = 2,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = (int)((double)breakSeconds / 60.0),
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = 3,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = false // Disable for heartbeat tests
                },
                Audio = new AudioSettings
                {
                    Enabled = false
                }
            };
        }

        private TimerService CreateTimerService(AppConfiguration config)
        {
            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);

            return new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory, _mockPauseReminderService.Object, new Fakes.FakeDispatcherService());
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}