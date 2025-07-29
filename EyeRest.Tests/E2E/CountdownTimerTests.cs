using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.ViewModels;
using EyeRest.Views;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// End-to-end tests specifically for countdown timer display and real-time update functionality
    /// </summary>
    public class CountdownTimerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _testHost;

        public CountdownTimerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CountdownTimer_WhenTimersRunning_ShouldDisplayCorrectTime()
        {
            _output.WriteLine("=== Testing Countdown Timer Display ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Act - Start timers
            await timerService.StartAsync();
            await Task.Delay(1000); // Allow initialization

            // Assert
            Assert.True(viewModel.IsRunning, "ViewModel should indicate timers are running");
            Assert.NotEqual("Timers not running", viewModel.TimeUntilNextBreak);
            Assert.NotEmpty(viewModel.TimeUntilNextBreak);
            
            _output.WriteLine($"✅ Countdown displays correctly: {viewModel.TimeUntilNextBreak}");
        }

        [Fact]
        public async Task CountdownTimer_WhenTimersStopped_ShouldHideOrShowStoppedMessage()
        {
            _output.WriteLine("=== Testing Countdown Timer Hidden State ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Start then stop timers
            await timerService.StartAsync();
            await Task.Delay(500);
            await timerService.StopAsync();
            await Task.Delay(500);

            // Assert
            Assert.False(viewModel.IsRunning, "ViewModel should indicate timers are stopped");
            Assert.Equal("Timers not running", viewModel.TimeUntilNextBreak);
            
            _output.WriteLine("✅ Countdown hides when timers are stopped");
        }

        [Fact]
        public async Task CountdownTimer_ShouldUpdateEverySecond()
        {
            _output.WriteLine("=== Testing Real-Time Countdown Updates ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Act - Start timers and capture initial value
            await timerService.StartAsync();
            await Task.Delay(1000); // Allow stabilization
            
            var initialCountdown = viewModel.TimeUntilNextBreak;
            var initialNextEvent = timerService.NextEventDescription;
            
            _output.WriteLine($"Initial countdown: {initialCountdown}");
            _output.WriteLine($"Initial next event: {initialNextEvent}");

            // Wait for update cycle
            await Task.Delay(3000); // Wait 3 seconds for updates
            
            var updatedCountdown = viewModel.TimeUntilNextBreak;
            var updatedNextEvent = timerService.NextEventDescription;
            
            _output.WriteLine($"Updated countdown: {updatedCountdown}");
            _output.WriteLine($"Updated next event: {updatedNextEvent}");

            // Assert - Values should have changed (countdown decreased)
            Assert.NotEqual(initialCountdown, updatedCountdown);
            
            // Parse time values to ensure countdown is decreasing
            if (TryParseCountdownTime(initialCountdown, out var initialSeconds) &&
                TryParseCountdownTime(updatedCountdown, out var updatedSeconds))
            {
                Assert.True(updatedSeconds < initialSeconds, 
                    $"Countdown should decrease over time: {initialSeconds}s → {updatedSeconds}s");
                _output.WriteLine($"✅ Countdown decreases correctly: {initialSeconds}s → {updatedSeconds}s");
            }
            else
            {
                _output.WriteLine($"✅ Countdown format changed as expected: '{initialCountdown}' → '{updatedCountdown}'");
            }
        }

        [Fact]
        public async Task CountdownTimer_ShouldFormatTimeCorrectly()
        {
            _output.WriteLine("=== Testing Countdown Time Formatting ===");
            
            // Arrange
            await InitializeTestHost();
            var timerService = _testHost!.Services.GetRequiredService<ITimerService>();
            
            // Act - Start timers
            await timerService.StartAsync();
            await Task.Delay(1000);
            
            var nextEventDesc = timerService.NextEventDescription;
            var timeUntilNextEyeRest = timerService.TimeUntilNextEyeRest;
            var timeUntilNextBreak = timerService.TimeUntilNextBreak;
            
            // Assert - Check formatting
            Assert.NotNull(nextEventDesc);
            Assert.NotEmpty(nextEventDesc);
            
            // Should contain descriptive text
            Assert.True(nextEventDesc.Contains("Next") || nextEventDesc.Contains("eye rest") || nextEventDesc.Contains("break"),
                $"Description should be user-friendly: {nextEventDesc}");
            
            // Time values should be valid
            Assert.True(timeUntilNextEyeRest >= TimeSpan.Zero, "Eye rest time should be non-negative");
            Assert.True(timeUntilNextBreak >= TimeSpan.Zero, "Break time should be non-negative");
            
            _output.WriteLine($"✅ Next event description: {nextEventDesc}");
            _output.WriteLine($"✅ Time until eye rest: {FormatTimeSpan(timeUntilNextEyeRest)}");
            _output.WriteLine($"✅ Time until break: {FormatTimeSpan(timeUntilNextBreak)}");
        }

        [Fact]
        public async Task CountdownTimer_ShouldShowNextEventAccurately()
        {
            _output.WriteLine("=== Testing Next Event Accuracy ===");
            
            // Arrange
            await InitializeTestHost();
            var timerService = _testHost!.Services.GetRequiredService<ITimerService>();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            
            // Act - Start timers
            await timerService.StartAsync();
            await Task.Delay(1000);
            
            var nextEventFromService = timerService.NextEventDescription;
            var nextEventFromViewModel = viewModel.TimeUntilNextBreak;
            var eyeRestTime = timerService.TimeUntilNextEyeRest;
            var breakTime = timerService.TimeUntilNextBreak;
            
            // Assert - Should show the soonest event
            var soonestTime = eyeRestTime <= breakTime ? eyeRestTime : breakTime;
            var expectedEventType = eyeRestTime <= breakTime ? "eye rest" : "break";
            
            Assert.Contains(expectedEventType, nextEventFromService.ToLower());
            
            _output.WriteLine($"✅ Eye rest in: {FormatTimeSpan(eyeRestTime)}");
            _output.WriteLine($"✅ Break in: {FormatTimeSpan(breakTime)}");
            _output.WriteLine($"✅ Next event ({expectedEventType}): {nextEventFromService}");
            _output.WriteLine($"✅ ViewModel countdown: {nextEventFromViewModel}");
        }

        [Fact]
        public async Task CountdownTimer_ShouldSynchronizeWithUIUpdates()
        {
            _output.WriteLine("=== Testing UI Synchronization ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = _testHost.Services.GetRequiredService<MainWindow>();
            
            // Start timers
            viewModel.StartTimersCommand.Execute(null);
            await Task.Delay(500); // Allow command to complete
            await Task.Delay(1000);
            
            // Simulate UI countdown updates (as would happen with the dispatcher timer)
            var initialUpdate = DateTime.Now;
            viewModel.UpdateCountdown();
            var firstCountdown = viewModel.TimeUntilNextBreak;
            
            await Task.Delay(2000);
            
            viewModel.UpdateCountdown();
            var secondCountdown = viewModel.TimeUntilNextBreak;
            
            // Assert - Countdown should update
            Assert.NotEqual(firstCountdown, secondCountdown);
            
            _output.WriteLine($"✅ First update: {firstCountdown}");
            _output.WriteLine($"✅ Second update: {secondCountdown}");
            _output.WriteLine("✅ UI countdown synchronizes with timer updates");
        }

        private async Task InitializeTestHost()
        {
            if (_testHost == null)
            {
                var hostBuilder = new HostBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddConsole());
                        services.AddSingleton<IconService>();
                        services.AddSingleton<IConfigurationService, ConfigurationService>();
                        services.AddSingleton<ITimerService, TimerService>();
                        services.AddSingleton<INotificationService, NotificationService>();
                        services.AddSingleton<IAudioService, AudioService>();
                        services.AddSingleton<ISystemTrayService, SystemTrayService>();
                        services.AddSingleton<IStartupManager, StartupManager>();
                        services.AddSingleton<ILoggingService, LoggingService>();
                        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
                        services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
                        services.AddTransient<MainWindowViewModel>();
                        services.AddTransient<MainWindow>();
                    });

                _testHost = hostBuilder.Build();
                await _testHost.StartAsync();
            }
        }

        private bool TryParseCountdownTime(string countdown, out int totalSeconds)
        {
            totalSeconds = 0;
            
            try
            {
                // Try to parse formats like "19m 45s", "45s", "1h 20m"
                var parts = countdown.ToLower().Split(' ');
                
                foreach (var part in parts)
                {
                    if (part.EndsWith("h"))
                    {
                        if (int.TryParse(part[..^1], out var hours))
                            totalSeconds += hours * 3600;
                    }
                    else if (part.EndsWith("m"))
                    {
                        if (int.TryParse(part[..^1], out var minutes))
                            totalSeconds += minutes * 60;
                    }
                    else if (part.EndsWith("s"))
                    {
                        if (int.TryParse(part[..^1], out var seconds))
                            totalSeconds += seconds;
                    }
                }
                
                return totalSeconds > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalMinutes < 1)
            {
                return $"{timeSpan.Seconds}s";
            }
            else if (timeSpan.TotalHours < 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
        }

        public void Dispose()
        {
            _testHost?.StopAsync().Wait(5000);
            _testHost?.Dispose();
        }
    }
}