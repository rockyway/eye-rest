using System;
using System.Threading.Tasks;
using EyeRest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.Integration
{
    /// <summary>
    /// Integration tests for timer warning popup functionality
    /// Tests the complete flow from timer initialization to warning event triggering
    /// </summary>
    public class WarningPopupIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IConfigurationService> _mockConfigurationService;
        private IHost? _host;

        public WarningPopupIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockNotificationService = new Mock<INotificationService>();
            _mockConfigurationService = new Mock<IConfigurationService>();
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "WarningPopups")]
        public async Task EyeRestTimer_WithWarningsEnabled_ShouldUseReducedInterval()
        {
            // Arrange
            _output.WriteLine("=== Testing Eye Rest Timer Reduced Interval Logic ===");
            
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,      // 20 minutes
                eyeRestWarning: 15,       // 15 seconds warning
                eyeRestWarningEnabled: true
            );
            
            _mockConfigurationService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);
            
            var timerService = await CreateTimerServiceAsync();
            
            try
            {
                // Act
                await timerService.StartAsync();
                
                // Assert - Verify timer uses reduced interval
                // Eye rest should trigger at 19m 45s (20m - 15s), not 20m
                var expectedInterval = TimeSpan.FromMinutes(20) - TimeSpan.FromSeconds(15);
                
                // We can't directly access private fields, but we can verify behavior
                // The timer should show approximately 19.75 minutes initially
                var timeUntilRest = timerService.TimeUntilNextEyeRest;
                
                _output.WriteLine($"Eye rest time until next: {timeUntilRest.TotalMinutes:F2} minutes");
                _output.WriteLine($"Expected (reduced): {expectedInterval.TotalMinutes:F2} minutes");
                
                // Allow for small timing variations (within 1 second)
                var timeDifference = Math.Abs((timeUntilRest - expectedInterval).TotalSeconds);
                Assert.True(timeDifference < 1.0, 
                    $"Timer interval should be reduced for warnings. Expected: {expectedInterval.TotalMinutes:F2}m, Actual: {timeUntilRest.TotalMinutes:F2}m, Difference: {timeDifference:F1}s");
                
                _output.WriteLine("✅ SUCCESS: Eye rest timer uses reduced interval for warnings");
            }
            finally
            {
                if (timerService.IsRunning)
                {
                    await timerService.StopAsync();
                }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "WarningPopups")]
        public async Task BreakTimer_WithWarningsEnabled_ShouldUseReducedInterval()
        {
            // Arrange
            _output.WriteLine("=== Testing Break Timer Reduced Interval Logic ===");
            
            var config = CreateTestConfiguration(
                breakInterval: 55,        // 55 minutes
                breakWarning: 30,         // 30 seconds warning
                breakWarningEnabled: true
            );
            
            _mockConfigurationService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);
            
            var timerService = await CreateTimerServiceAsync();
            
            try
            {
                // Act
                await timerService.StartAsync();
                
                // Assert - Verify timer uses reduced interval
                // Break should trigger at 54m 30s (55m - 30s), not 55m
                var expectedInterval = TimeSpan.FromMinutes(55) - TimeSpan.FromSeconds(30);
                
                var timeUntilBreak = timerService.TimeUntilNextBreak;
                
                _output.WriteLine($"Break time until next: {timeUntilBreak.TotalMinutes:F2} minutes");
                _output.WriteLine($"Expected (reduced): {expectedInterval.TotalMinutes:F2} minutes");
                
                // Allow for small timing variations (within 1 second)
                var timeDifference = Math.Abs((timeUntilBreak - expectedInterval).TotalSeconds);
                Assert.True(timeDifference < 1.0, 
                    $"Timer interval should be reduced for warnings. Expected: {expectedInterval.TotalMinutes:F2}m, Actual: {timeUntilBreak.TotalMinutes:F2}m, Difference: {timeDifference:F1}s");
                
                _output.WriteLine("✅ SUCCESS: Break timer uses reduced interval for warnings");
            }
            finally
            {
                if (timerService.IsRunning)
                {
                    await timerService.StopAsync();
                }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "WarningPopups")]
        public async Task Timer_WithWarningsDisabled_ShouldUseFullInterval()
        {
            // Arrange
            _output.WriteLine("=== Testing Timer Full Interval When Warnings Disabled ===");
            
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,
                eyeRestWarning: 15,
                eyeRestWarningEnabled: false,  // Warnings disabled
                breakInterval: 55,
                breakWarning: 30,
                breakWarningEnabled: false     // Warnings disabled
            );
            
            _mockConfigurationService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);
            
            var timerService = await CreateTimerServiceAsync();
            
            try
            {
                // Act
                await timerService.StartAsync();
                
                // Assert - Should use full intervals when warnings disabled
                var eyeRestTime = timerService.TimeUntilNextEyeRest;
                var breakTime = timerService.TimeUntilNextBreak;
                
                _output.WriteLine($"Eye rest time (warnings disabled): {eyeRestTime.TotalMinutes:F2} minutes");
                _output.WriteLine($"Break time (warnings disabled): {breakTime.TotalMinutes:F2} minutes");
                
                // Should be approximately 20 and 55 minutes respectively (full intervals)
                Assert.True(Math.Abs(eyeRestTime.TotalMinutes - 20.0) < 0.1, 
                    $"Eye rest should use full 20m interval when warnings disabled. Actual: {eyeRestTime.TotalMinutes:F2}m");
                Assert.True(Math.Abs(breakTime.TotalMinutes - 55.0) < 0.1, 
                    $"Break should use full 55m interval when warnings disabled. Actual: {breakTime.TotalMinutes:F2}m");
                
                _output.WriteLine("✅ SUCCESS: Timers use full intervals when warnings disabled");
            }
            finally
            {
                if (timerService.IsRunning)
                {
                    await timerService.StopAsync();
                }
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "WarningPopups")]
        public async Task Timer_WithInvalidWarningTime_ShouldUseFullInterval()
        {
            // Arrange
            _output.WriteLine("=== Testing Timer Behavior With Invalid Warning Times ===");
            
            var config = CreateTestConfiguration(
                eyeRestInterval: 1,       // 1 minute total
                eyeRestWarning: 90,       // 90 seconds warning (longer than interval!)
                eyeRestWarningEnabled: true
            );
            
            _mockConfigurationService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);
            
            var timerService = await CreateTimerServiceAsync();
            
            try
            {
                // Act
                await timerService.StartAsync();
                
                // Assert - Should use full interval when warning > total interval
                var eyeRestTime = timerService.TimeUntilNextEyeRest;
                
                _output.WriteLine($"Eye rest time (invalid warning): {eyeRestTime.TotalMinutes:F2} minutes");
                
                // Should fall back to full interval (1 minute) when warning time is invalid
                Assert.True(Math.Abs(eyeRestTime.TotalMinutes - 1.0) < 0.1, 
                    $"Should use full interval when warning time exceeds total interval. Actual: {eyeRestTime.TotalMinutes:F2}m");
                
                _output.WriteLine("✅ SUCCESS: Timer handles invalid warning times gracefully");
            }
            finally
            {
                if (timerService.IsRunning)
                {
                    await timerService.StopAsync();
                }
            }
        }

        private async Task<TimerService> CreateTimerServiceAsync()
        {
            // Set up minimal DI container for integration testing
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(_mockNotificationService.Object);
                    services.AddSingleton(_mockConfigurationService.Object);
                    services.AddSingleton<ITimerService, TimerService>();
                    services.AddLogging();
                })
                .Build();

            await _host.StartAsync();
            return (TimerService)_host.Services.GetRequiredService<ITimerService>();
        }

        private static Models.AppConfiguration CreateTestConfiguration(
            int eyeRestInterval = 20,
            int eyeRestWarning = 15,
            bool eyeRestWarningEnabled = true,
            int breakInterval = 55,
            int breakWarning = 30,
            bool breakWarningEnabled = true)
        {
            return new Models.AppConfiguration
            {
                EyeRest = new Models.EyeRestSettings
                {
                    IntervalMinutes = eyeRestInterval,
                    DurationSeconds = 20,
                    WarningEnabled = eyeRestWarningEnabled,
                    WarningSeconds = eyeRestWarning,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new Models.BreakSettings
                {
                    IntervalMinutes = breakInterval,
                    DurationMinutes = 5,
                    WarningEnabled = breakWarningEnabled,
                    WarningSeconds = breakWarning,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new Models.UserPresenceSettings
                {
                    Enabled = false // Disable for timer warning tests
                },
                Audio = new Models.AudioSettings
                {
                    Enabled = false
                }
            };
        }

        public void Dispose()
        {
            _host?.Dispose();
        }
    }
}