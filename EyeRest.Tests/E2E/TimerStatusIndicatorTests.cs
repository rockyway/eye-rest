using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.ViewModels;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// End-to-end tests for timer status indicator updates (green Running / red Stopped status)
    /// </summary>
    public class TimerStatusIndicatorTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _testHost;

        public TimerStatusIndicatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TimerStatusIndicator_WhenTimersRunning_ShouldShowGreenRunningStatus()
        {
            _output.WriteLine("=== Testing Green Running Status ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Act - Start timers
            await timerService.StartAsync();
            await Task.Delay(1000); // Allow status to update
            
            // Assert
            Assert.True(timerService.IsRunning, "Timer service should be running");
            Assert.Equal("Running", viewModel.TimerStatusText);
            Assert.Equal("#4CAF50", viewModel.TimerStatusColor); // Green color
            Assert.Equal("Eye-rest Settings - Running", viewModel.WindowTitle);
            
            _output.WriteLine($"✅ Status Text: {viewModel.TimerStatusText}");
            _output.WriteLine($"✅ Status Color: {viewModel.TimerStatusColor}");
            _output.WriteLine($"✅ Window Title: {viewModel.WindowTitle}");
        }

        [Fact]
        public async Task TimerStatusIndicator_WhenTimersStopped_ShouldShowRedStoppedStatus()
        {
            _output.WriteLine("=== Testing Red Stopped Status ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Start then stop timers
            await timerService.StartAsync();
            await Task.Delay(500);
            await timerService.StopAsync();
            await Task.Delay(500); // Allow status to update
            
            // Assert
            Assert.False(timerService.IsRunning, "Timer service should be stopped");
            Assert.Equal("Stopped", viewModel.TimerStatusText);
            Assert.Equal("#F44336", viewModel.TimerStatusColor); // Red color
            Assert.Equal("Eye-rest Settings - Stopped", viewModel.WindowTitle);
            
            _output.WriteLine($"✅ Status Text: {viewModel.TimerStatusText}");
            _output.WriteLine($"✅ Status Color: {viewModel.TimerStatusColor}");
            _output.WriteLine($"✅ Window Title: {viewModel.WindowTitle}");
        }

        [Fact]
        public async Task TimerStatusIndicator_ShouldUpdateInRealTime()
        {
            _output.WriteLine("=== Testing Real-Time Status Updates ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Initial state (should be stopped)
            Assert.Equal("Stopped", viewModel.TimerStatusText);
            Assert.Equal("#F44336", viewModel.TimerStatusColor);
            _output.WriteLine($"Initial state: {viewModel.TimerStatusText} ({viewModel.TimerStatusColor})");
            
            // Start timers
            await timerService.StartAsync();
            await Task.Delay(500);
            
            Assert.Equal("Running", viewModel.TimerStatusText);
            Assert.Equal("#4CAF50", viewModel.TimerStatusColor);
            _output.WriteLine($"After start: {viewModel.TimerStatusText} ({viewModel.TimerStatusColor})");
            
            // Stop timers
            await timerService.StopAsync();
            await Task.Delay(500);
            
            Assert.Equal("Stopped", viewModel.TimerStatusText);
            Assert.Equal("#F44336", viewModel.TimerStatusColor);
            _output.WriteLine($"After stop: {viewModel.TimerStatusText} ({viewModel.TimerStatusColor})");
            
            // Start again
            await timerService.StartAsync();
            await Task.Delay(500);
            
            Assert.Equal("Running", viewModel.TimerStatusText);
            Assert.Equal("#4CAF50", viewModel.TimerStatusColor);
            _output.WriteLine($"After restart: {viewModel.TimerStatusText} ({viewModel.TimerStatusColor})");
            
            _output.WriteLine("✅ Status indicator updates in real-time");
        }

        [Fact]
        public async Task TimerStatusIndicator_ShouldSynchronizeWithTimerService()
        {
            _output.WriteLine("=== Testing Synchronization with Timer Service ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Test multiple rapid state changes
            var states = new[]
            {
                (false, "Stopped", "#F44336"),
                (true, "Running", "#4CAF50"),
                (false, "Stopped", "#F44336"),
                (true, "Running", "#4CAF50")
            };
            
            foreach (var (shouldBeRunning, expectedText, expectedColor) in states)
            {
                if (shouldBeRunning)
                {
                    await timerService.StartAsync();
                }
                else
                {
                    await timerService.StopAsync();
                }
                
                await Task.Delay(300); // Allow synchronization
                
                Assert.Equal(shouldBeRunning, timerService.IsRunning);
                Assert.Equal(expectedText, viewModel.TimerStatusText);
                Assert.Equal(expectedColor, viewModel.TimerStatusColor);
                
                _output.WriteLine($"✅ State {(shouldBeRunning ? "Running" : "Stopped")}: Service={timerService.IsRunning}, VM={viewModel.TimerStatusText}");
            }
            
            _output.WriteLine("✅ View model stays synchronized with timer service");
        }

        [Fact]
        public async Task TimerStatusIndicator_ShouldUpdateWindowTitle()
        {
            _output.WriteLine("=== Testing Window Title Updates ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Test window title changes with status
            Assert.Equal("Eye-rest Settings - Stopped", viewModel.WindowTitle);
            _output.WriteLine($"Initial title: {viewModel.WindowTitle}");
            
            // Start timers
            await timerService.StartAsync();
            await Task.Delay(500);
            
            Assert.Equal("Eye-rest Settings - Running", viewModel.WindowTitle);
            _output.WriteLine($"Running title: {viewModel.WindowTitle}");
            
            // Stop timers
            await timerService.StopAsync();
            await Task.Delay(500);
            
            Assert.Equal("Eye-rest Settings - Stopped", viewModel.WindowTitle);
            _output.WriteLine($"Stopped title: {viewModel.WindowTitle}");
            
            _output.WriteLine("✅ Window title updates correctly with timer status");
        }

        [Fact]
        public async Task TimerStatusIndicator_CommandStates_ShouldUpdateCorrectly()
        {
            _output.WriteLine("=== Testing Command States with Status ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            
            // Test start command
            Assert.True(viewModel.StartTimersCommand.CanExecute(null), "Start command should be available initially");
            
            // Execute start command
            viewModel.StartTimersCommand.Execute(null);
            await Task.Delay(500);
            
            Assert.Equal("Running", viewModel.TimerStatusText);
            _output.WriteLine($"✅ After start command: {viewModel.TimerStatusText}");
            
            // Execute stop command
            viewModel.StopTimersCommand.Execute(null);
            await Task.Delay(500);
            
            Assert.Equal("Stopped", viewModel.TimerStatusText);
            _output.WriteLine($"✅ After stop command: {viewModel.TimerStatusText}");
            
            _output.WriteLine("✅ Commands update status indicator correctly");
        }

        [Fact]
        public async Task TimerStatusIndicator_ShouldHandleCountdownVisibility()
        {
            _output.WriteLine("=== Testing Countdown Visibility with Status ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Initial state - stopped
            Assert.False(viewModel.IsRunning, "Should not be running initially");
            _output.WriteLine($"Initial running state: {viewModel.IsRunning}");
            
            // Start timers
            await timerService.StartAsync();
            await Task.Delay(500);
            viewModel.UpdateCountdown(); // Simulate UI update
            
            Assert.True(viewModel.IsRunning, "Should be running after start");
            Assert.NotEqual("Timers not running", viewModel.TimeUntilNextBreak);
            _output.WriteLine($"Running state: {viewModel.IsRunning}, Countdown: {viewModel.TimeUntilNextBreak}");
            
            // Stop timers
            await timerService.StopAsync();
            await Task.Delay(500);
            viewModel.UpdateCountdown(); // Simulate UI update
            
            Assert.False(viewModel.IsRunning, "Should not be running after stop");
            Assert.Equal("Timers not running", viewModel.TimeUntilNextBreak);
            _output.WriteLine($"Stopped state: {viewModel.IsRunning}, Countdown: {viewModel.TimeUntilNextBreak}");
            
            _output.WriteLine("✅ Countdown visibility corresponds to status indicator");
        }

        [Fact]
        public async Task TimerStatusIndicator_ColorValues_ShouldBeValidHexColors()
        {
            _output.WriteLine("=== Testing Status Color Values ===");
            
            // Arrange
            await InitializeTestHost();
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Test stopped color
            await timerService.StopAsync();
            await Task.Delay(200);
            
            var stoppedColor = viewModel.TimerStatusColor;
            Assert.True(IsValidHexColor(stoppedColor), $"Stopped color should be valid hex: {stoppedColor}");
            Assert.Equal("#F44336", stoppedColor); // Red
            _output.WriteLine($"✅ Stopped color: {stoppedColor}");
            
            // Test running color
            await timerService.StartAsync();
            await Task.Delay(200);
            
            var runningColor = viewModel.TimerStatusColor;
            Assert.True(IsValidHexColor(runningColor), $"Running color should be valid hex: {runningColor}");
            Assert.Equal("#4CAF50", runningColor); // Green
            _output.WriteLine($"✅ Running color: {runningColor}");
            
            _output.WriteLine("✅ All status colors are valid hex values");
        }

        private bool IsValidHexColor(string color)
        {
            if (string.IsNullOrEmpty(color) || !color.StartsWith("#"))
                return false;
            
            if (color.Length != 7) // #RRGGBB format
                return false;
            
            for (int i = 1; i < color.Length; i++)
            {
                if (!Uri.IsHexDigit(color[i]))
                    return false;
            }
            
            return true;
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
                    });

                _testHost = hostBuilder.Build();
                await _testHost.StartAsync();
            }
        }

        public void Dispose()
        {
            _testHost?.StopAsync().Wait(5000);
            _testHost?.Dispose();
        }
    }
}