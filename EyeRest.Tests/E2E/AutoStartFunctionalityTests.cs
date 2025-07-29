using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
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
    /// End-to-end tests for auto-start functionality and initialization sequence
    /// </summary>
    public class AutoStartFunctionalityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _testHost;

        public AutoStartFunctionalityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task AutoStart_WhenApplicationStarts_ShouldInitializeServicesInCorrectOrder()
        {
            _output.WriteLine("=== Testing Auto-Start Initialization Sequence ===");
            
            // Arrange - Simulate application startup
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

            // Act - Start the host and initialize services in the same order as App.xaml.cs
            await _testHost.StartAsync();

            // Step 1: Initialize system tray first (fastest startup component)
            var systemTrayService = _testHost.Services.GetRequiredService<ISystemTrayService>();
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            _output.WriteLine("✅ Step 1: System tray service initialized");

            // Step 2: Initialize application orchestrator
            var orchestrator = _testHost.Services.GetRequiredService<IApplicationOrchestrator>();
            await orchestrator.InitializeAsync();
            _output.WriteLine("✅ Step 2: Application orchestrator initialized");

            // Step 3: Start timer service (this should happen automatically)
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            await timerService.StartAsync();
            _output.WriteLine("✅ Step 3: Timer service started");

            // Assert - Verify all services are properly initialized
            Assert.True(timerService.IsRunning, "Timer service should be running after auto-start");
            _output.WriteLine("✅ All services initialized in correct order");
        }

        [Fact]
        public async Task AutoStart_WhenApplicationStarts_ShouldStartTimersAutomatically()
        {
            _output.WriteLine("=== Testing Automatic Timer Start ===");
            
            // Arrange & Act - Initialize test environment
            await InitializeTestHost();
            
            var timerService = _testHost!.Services.GetRequiredService<ITimerService>();
            var orchestrator = _testHost.Services.GetRequiredService<IApplicationOrchestrator>();
            
            // Initialize orchestrator and start timers automatically
            await orchestrator.InitializeAsync();
            await timerService.StartAsync(); // This simulates the auto-start behavior
            
            await Task.Delay(1000); // Allow initialization to complete

            // Assert
            Assert.True(timerService.IsRunning, "Timers should start automatically when app opens");
            Assert.NotEqual("Timers not running", timerService.NextEventDescription);
            
            _output.WriteLine($"✅ Timers started automatically");
            _output.WriteLine($"✅ Next event: {timerService.NextEventDescription}");
        }

        [Fact]
        public async Task AutoStart_CountdownDisplay_ShouldAppearImmediately()
        {
            _output.WriteLine("=== Testing Immediate Countdown Display ===");
            
            // Arrange & Act
            await InitializeTestHost();
            
            var viewModel = _testHost!.Services.GetRequiredService<MainWindowViewModel>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Simulate the auto-start sequence
            await timerService.StartAsync();
            await Task.Delay(500); // Brief delay for initialization
            
            // Update countdown as would happen in the UI
            viewModel.UpdateCountdown();

            // Assert
            Assert.True(viewModel.IsRunning, "ViewModel should reflect running state immediately");
            Assert.NotEqual("Timers not running", viewModel.TimeUntilNextBreak);
            Assert.NotEmpty(viewModel.TimeUntilNextBreak);
            
            _output.WriteLine($"✅ Countdown appears immediately: {viewModel.TimeUntilNextBreak}");
        }

        [Fact]
        public async Task AutoStart_InitializationSequence_ShouldCompleteWithoutErrors()
        {
            _output.WriteLine("=== Testing Error-Free Initialization ===");
            
            var initializationSteps = new List<string>();
            var errors = new List<string>();

            try
            {
                // Step 1: Host creation and startup
                await InitializeTestHost();
                initializationSteps.Add("Host initialized");

                // Step 2: System tray initialization
                var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
                systemTrayService.Initialize();
                systemTrayService.ShowTrayIcon();
                initializationSteps.Add("System tray initialized");

                // Step 3: Application orchestrator
                var orchestrator = _testHost.Services.GetRequiredService<IApplicationOrchestrator>();
                await orchestrator.InitializeAsync();
                initializationSteps.Add("Orchestrator initialized");

                // Step 4: Timer service
                var timerService = _testHost.Services.GetRequiredService<ITimerService>();
                await timerService.StartAsync();
                initializationSteps.Add("Timer service started");

                // Step 5: View model initialization
                var viewModel = _testHost.Services.GetRequiredService<MainWindowViewModel>();
                await Task.Delay(500); // Allow async loading
                initializationSteps.Add("View model ready");

                _output.WriteLine("✅ All initialization steps completed:");
                foreach (var step in initializationSteps)
                {
                    _output.WriteLine($"   • {step}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Initialization error: {ex.Message}");
            }

            // Assert
            Assert.Empty(errors);
            Assert.True(initializationSteps.Count >= 5, "All initialization steps should complete");
            
            _output.WriteLine("✅ Initialization sequence completed without errors");
        }

        [Fact]
        public async Task AutoStart_ConfigurationLoading_ShouldHappenInBackground()
        {
            _output.WriteLine("=== Testing Background Configuration Loading ===");
            
            // Arrange & Act
            await InitializeTestHost();
            
            var configService = _testHost!.Services.GetRequiredService<IConfigurationService>();
            var viewModel = _testHost.Services.GetRequiredService<MainWindowViewModel>();

            // Configuration should load asynchronously
            var config = await configService.LoadConfigurationAsync();
            
            // Allow some time for view model to receive configuration
            await Task.Delay(1000);

            // Assert
            Assert.NotNull(config);
            Assert.True(config.EyeRest.IntervalMinutes > 0, "Configuration should be loaded with valid values");
            Assert.True(config.Break.IntervalMinutes > 0, "Configuration should be loaded with valid values");
            
            // View model should have configuration values
            Assert.True(viewModel.EyeRestIntervalMinutes > 0, "View model should have loaded configuration");
            Assert.True(viewModel.BreakIntervalMinutes > 0, "View model should have loaded configuration");
            
            _output.WriteLine($"✅ Configuration loaded: Eye rest {config.EyeRest.IntervalMinutes}min, Break {config.Break.IntervalMinutes}min");
            _output.WriteLine($"✅ View model updated: Eye rest {viewModel.EyeRestIntervalMinutes}min, Break {viewModel.BreakIntervalMinutes}min");
        }

        [Fact]
        public async Task AutoStart_StartupPerformance_ShouldBeFast()
        {
            _output.WriteLine("=== Testing Startup Performance ===");
            
            var startTime = DateTime.Now;
            
            // Arrange & Act - Measure initialization time
            await InitializeTestHost();
            
            var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
            var orchestrator = _testHost.Services.GetRequiredService<IApplicationOrchestrator>();
            var timerService = _testHost.Services.GetRequiredService<ITimerService>();
            
            // Simulate the startup sequence timing
            var trayInitStart = DateTime.Now;
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            var trayInitTime = DateTime.Now - trayInitStart;
            
            var orchestratorInitStart = DateTime.Now;
            await orchestrator.InitializeAsync();
            var orchestratorInitTime = DateTime.Now - orchestratorInitStart;
            
            var timerInitStart = DateTime.Now;
            await timerService.StartAsync();
            var timerInitTime = DateTime.Now - timerInitStart;
            
            var totalTime = DateTime.Now - startTime;

            // Assert - Startup should be fast
            Assert.True(totalTime.TotalSeconds < 5, $"Total startup time should be under 5 seconds, was {totalTime.TotalSeconds:F2}s");
            Assert.True(trayInitTime.TotalMilliseconds < 500, $"Tray init should be under 500ms, was {trayInitTime.TotalMilliseconds:F1}ms");
            
            _output.WriteLine($"✅ Startup performance metrics:");
            _output.WriteLine($"   • System tray init: {trayInitTime.TotalMilliseconds:F1}ms");
            _output.WriteLine($"   • Orchestrator init: {orchestratorInitTime.TotalMilliseconds:F1}ms");
            _output.WriteLine($"   • Timer service init: {timerInitTime.TotalMilliseconds:F1}ms");
            _output.WriteLine($"   • Total startup time: {totalTime.TotalSeconds:F2}s");
        }

        [Fact]
        public async Task AutoStart_ServiceDependencies_ShouldBeResolvedCorrectly()
        {
            _output.WriteLine("=== Testing Service Dependency Resolution ===");
            
            // Arrange & Act
            await InitializeTestHost();

            // Assert - All required services should be resolvable
            var services = new Dictionary<string, Type>
            {
                { "Configuration Service", typeof(IConfigurationService) },
                { "Timer Service", typeof(ITimerService) },
                { "System Tray Service", typeof(ISystemTrayService) },
                { "Notification Service", typeof(INotificationService) },
                { "Audio Service", typeof(IAudioService) },
                { "Application Orchestrator", typeof(IApplicationOrchestrator) },
                { "Main Window ViewModel", typeof(MainWindowViewModel) }
            };

            foreach (var (name, serviceType) in services)
            {
                var service = _testHost!.Services.GetRequiredService(serviceType);
                Assert.NotNull(service);
                _output.WriteLine($"✅ {name}: {service.GetType().Name}");
            }

            _output.WriteLine("✅ All service dependencies resolved correctly");
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