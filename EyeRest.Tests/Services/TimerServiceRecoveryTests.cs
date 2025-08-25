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
    public class TimerServiceRecoveryTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TimerServiceRecoveryTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _fakeTimerFactory = new FakeTimerFactory();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Fact]
        public async Task TimerHang_Detection_WithShortInterval()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 30,    // 30 seconds
                breakInterval: 60       // 1 minute
            );

            var timerService = CreateTimerService(config);
            var logMessages = CaptureLogMessages();

            // Act
            await timerService.StartAsync();
            
            // Simulate timer hang by setting very old heartbeat
            SetLastHeartbeat(timerService, DateTime.Now.AddMinutes(-15)); // 15 minutes ago
            
            // Trigger health monitor check
            CallHealthMonitorTick(timerService);

            // Assert
            var hangDetected = logMessages.Any(msg => 
                msg.Contains("TIMER HANG DETECTED") && msg.Contains("No heartbeat"));
            
            Assert.True(hangDetected, "Should detect timer hang when heartbeat is too old");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task TimerHang_Recovery_Success()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,    // 20 seconds
                breakInterval: 40       // 40 seconds
            );

            var timerService = CreateTimerService(config);
            var logMessages = CaptureLogMessages();

            await timerService.StartAsync();
            Assert.True(timerService.IsRunning, "Timer should be running initially");

            // Simulate hang
            SetLastHeartbeat(timerService, DateTime.Now.AddMinutes(-20));

            // Act - Trigger recovery
            CallRecoverTimersFromHang(timerService);

            // Small delay to let recovery complete
            await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Timer should still be running after recovery");
            
            var recoveryInitiated = logMessages.Any(msg => 
                msg.Contains("INITIATING TIMER RECOVERY"));
            Assert.True(recoveryInitiated, "Recovery should be initiated");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemResume_ShortSleep_RestoresState()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 30,
                breakInterval: 60,
                extendedAwayThreshold: 300 // 5 minutes for extended away
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            
            // Let timer run and capture state
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);
            var timeBeforeSleep = timerService.TimeUntilNextEyeRest;

            // Act - Simulate system resume after short sleep (2 minutes)
            await CallRecoverFromSystemResumeAsync(timerService, minutesAway: 2);

            // Allow recovery to complete
            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Timer should be running after short sleep recovery");
            
            var timeAfterResume = timerService.TimeUntilNextEyeRest;
            var timeDifference = Math.Abs((timeBeforeSleep - timeAfterResume).TotalSeconds);
            
            // Should preserve approximate time (within tolerance for processing delay)
            Assert.True(timeDifference <= 5, 
                $"Timer should preserve time after short sleep, difference: {timeDifference}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemResume_ExtendedSleep_FreshSession()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 30,
                breakInterval: 60,
                extendedAwayThreshold: 120 // 2 minutes for extended away
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            
            // Let timer run partway
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);

            // Act - Simulate system resume after extended sleep (5 minutes)
            await CallRecoverFromSystemResumeAsync(timerService, minutesAway: 5);

            // Allow recovery to complete
            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Timer should be running after extended sleep recovery");
            
            var timeAfterResume = timerService.TimeUntilNextEyeRest;
            
            // Should start fresh session - time should be close to full interval
            Assert.True(timeAfterResume.TotalSeconds >= 25, 
                $"Timer should reset to fresh session after extended sleep, got {timeAfterResume.TotalSeconds}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task CrashRecovery_TimersRestart()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 25,
                breakInterval: 50
            );

            var timerService = CreateTimerService(config);
            var logMessages = CaptureLogMessages();

            // Act - Start, then simulate crash recovery
            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);

            // Force a recovery scenario
            CallRecoverTimersFromHang(timerService);
            await Task.Delay(TimeSpan.FromMilliseconds(200), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Timers should restart after crash recovery");
            
            var recoveryLogged = logMessages.Any(msg => 
                msg.Contains("RECOVERY") || msg.Contains("recovery"));
            Assert.True(recoveryLogged, "Recovery should be logged");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task MultipleRecoveryAttempts_Succeed()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,
                breakInterval: 30
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Act - Perform multiple recovery attempts
            for (int i = 0; i < 3; i++)
            {
                // Simulate hang
                SetLastHeartbeat(timerService, DateTime.Now.AddMinutes(-10));
                
                // Recover
                CallRecoverTimersFromHang(timerService);
                
                await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationTokenSource.Token);
                
                // Assert each recovery
                Assert.True(timerService.IsRunning, $"Timer should be running after recovery attempt {i + 1}");
            }

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task Recovery_PreservesConfiguration()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 45,    // Custom intervals
                breakInterval: 90,
                eyeRestWarning: 8,
                breakWarning: 15
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            
            // Capture initial timer intervals
            var initialEyeRestInterval = GetTimerInterval(timerService, "EyeRest");
            var initialBreakInterval = GetTimerInterval(timerService, "Break");

            // Act - Force recovery
            CallRecoverTimersFromHang(timerService);
            await Task.Delay(TimeSpan.FromMilliseconds(200), _cancellationTokenSource.Token);

            // Assert
            var recoveredEyeRestInterval = GetTimerInterval(timerService, "EyeRest");
            var recoveredBreakInterval = GetTimerInterval(timerService, "Break");
            
            Assert.True(Math.Abs((initialEyeRestInterval - recoveredEyeRestInterval).TotalSeconds) < 1, 
                $"Eye rest interval should be preserved within 1s. Expected: {initialEyeRestInterval}, Got: {recoveredEyeRestInterval}");
            Assert.True(Math.Abs((initialBreakInterval - recoveredBreakInterval).TotalSeconds) < 1,
                $"Break interval should be preserved within 1s. Expected: {initialBreakInterval}, Got: {recoveredBreakInterval}");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task Recovery_UpdatesHeartbeat()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,
                breakInterval: 40
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            
            // Set old heartbeat
            var oldHeartbeat = DateTime.Now.AddMinutes(-10);
            SetLastHeartbeat(timerService, oldHeartbeat);

            // Act
            CallRecoverTimersFromHang(timerService);
            await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationTokenSource.Token);

            // Assert
            var newHeartbeat = GetLastHeartbeat(timerService);
            Assert.True(newHeartbeat > oldHeartbeat.AddMinutes(9), 
                "Heartbeat should be updated during recovery");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task Recovery_HandlesNullTimers()
        {
            // Arrange
            var config = CreateTestConfiguration(20, 40);
            var timerService = CreateTimerService(config);

            // Don't start the service to ensure timers are null/uninitialized
            
            // Act - Try to recover when timers are null
            var exception = Record.Exception(() => CallRecoverTimersFromHang(timerService));

            // Assert - Should not throw exception
            Assert.Null(exception);

            timerService.Dispose();
        }

        [Fact]
        public async Task HealthMonitor_PerformanceUnderLoad()
        {
            // Arrange
            var config = CreateTestConfiguration(10, 20); // Very short intervals
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Act - Trigger health monitor many times rapidly
            var startTime = DateTime.Now;
            
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    CallHealthMonitorTick(timerService);
                }
                catch
                {
                    // Ignore exceptions for performance test
                }
                
                if (i % 20 == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1), _cancellationTokenSource.Token);
                }
            }
            
            var elapsed = DateTime.Now - startTime;

            // Assert - Should complete quickly
            Assert.True(elapsed.TotalMilliseconds < 1000, 
                $"100 health checks should complete in < 1000ms, took {elapsed.TotalMilliseconds}ms");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemResume_ErrorHandling()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Act - Try to recover with invalid parameters
            var exceptions = new List<Exception>();
            
            try
            {
                await CallRecoverFromSystemResumeAsync(timerService, minutesAway: -1);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            try
            {
                await CallRecoverFromSystemResumeAsync(timerService, minutesAway: int.MaxValue);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            // Assert - Service should remain functional despite errors
            Assert.True(timerService.IsRunning, "Timer should remain running despite recovery errors");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task RecoveryIntegration_RealWorldScenario()
        {
            // Arrange - Simulate real world scenario with events
            var config = CreateTestConfiguration(
                eyeRestInterval: 25,
                breakInterval: 50
            );

            var timerService = CreateTimerService(config);
            
            var eventsFired = new List<(DateTime Time, string Event)>();
            timerService.EyeRestDue += (s, e) => eventsFired.Add((DateTime.Now, "EyeRestDue"));
            timerService.BreakDue += (s, e) => eventsFired.Add((DateTime.Now, "BreakDue"));

            // Act - Start, let run, crash, recover, continue
            await timerService.StartAsync();
            
            // Let it run for a bit
            await Task.Delay(TimeSpan.FromSeconds(15), _cancellationTokenSource.Token);
            
            // Simulate crash and recovery
            SetLastHeartbeat(timerService, DateTime.Now.AddMinutes(-20));
            CallRecoverTimersFromHang(timerService);
            
            await Task.Delay(TimeSpan.FromMilliseconds(200), _cancellationTokenSource.Token);
            
            // Continue running to see if events still fire
            await Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Timer should be running after recovery");
            var recentEvents = eventsFired.Where(e => e.Time > DateTime.Now.AddSeconds(-25)).ToList();
            Assert.NotEmpty(recentEvents);

            await timerService.StopAsync();
            timerService.Dispose();
        }

        // Helper methods using reflection to access private members
        private void SetLastHeartbeat(TimerService timerService, DateTime heartbeat)
        {
            var field = typeof(TimerService).GetField("_lastHeartbeat", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(timerService, heartbeat);
        }

        private DateTime GetLastHeartbeat(TimerService timerService)
        {
            var field = typeof(TimerService).GetField("_lastHeartbeat", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (DateTime)field.GetValue(timerService)!;
        }

        private void CallHealthMonitorTick(TimerService timerService)
        {
            var method = typeof(TimerService).GetMethod("OnHealthMonitorTick", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(timerService, new object[] { null, EventArgs.Empty });
        }

        private void CallRecoverTimersFromHang(TimerService timerService)
        {
            var method = typeof(TimerService).GetMethod("RecoverTimersFromHang", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(timerService, null);
        }

        private async Task CallRecoverFromSystemResumeAsync(TimerService timerService, int minutesAway)
        {
            var method = typeof(TimerService).GetMethod("RecoverFromSystemResumeAsync", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            
            var task = (Task)method.Invoke(timerService, new object[] { minutesAway })!;
            await task;
        }

        private TimeSpan GetTimerInterval(TimerService timerService, string timerName)
        {
            var fieldName = timerName == "EyeRest" ? "_eyeRestTimer" : "_breakTimer";
            var field = typeof(TimerService).GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            
            var timer = field.GetValue(timerService) as System.Windows.Threading.DispatcherTimer;
            return timer?.Interval ?? TimeSpan.Zero;
        }

        private List<string> CaptureLogMessages()
        {
            var logMessages = new List<string>();
            _mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) =>
                {
                    logMessages.Add(formatter.DynamicInvoke(state, exception)?.ToString() ?? "");
                });
            return logMessages;
        }

        private AppConfiguration CreateTestConfiguration(
            int eyeRestInterval,
            int breakInterval,
            int eyeRestWarning = 3,
            int breakWarning = 5,
            int extendedAwayThreshold = 300)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = (int)((double)eyeRestInterval / 60.0),
                    DurationSeconds = 5,
                    WarningSeconds = eyeRestWarning,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = (int)((double)breakInterval / 60.0),
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = breakWarning,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = false, // Disable for recovery tests
                    ExtendedAwayThresholdMinutes = (int)(extendedAwayThreshold / 60.0)
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

            return new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}