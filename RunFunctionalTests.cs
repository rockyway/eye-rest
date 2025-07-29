using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.ViewModels;

namespace EyeRest
{
    /// <summary>
    /// Simple functional test runner that can verify core functionality without WPF UI dependencies
    /// </summary>
    public class SimpleFunctionalTestRunner
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== EyeRest Functional Test Runner ===");
            Console.WriteLine();

            var testResults = new List<(string TestName, bool Passed, string Details)>();
            
            try
            {
                // Test 1: Build Verification
                var buildTest = await TestApplicationBuild();
                testResults.Add(("Application Build", buildTest.Success, buildTest.Message));

                // Test 2: Service Dependencies
                var dependencyTest = await TestServiceDependencies();
                testResults.Add(("Service Dependencies", dependencyTest.Success, dependencyTest.Message));

                // Test 3: Timer Service Functionality
                var timerTest = await TestTimerServiceFunctionality();
                testResults.Add(("Timer Service", timerTest.Success, timerTest.Message));

                // Test 4: Configuration Service
                var configTest = await TestConfigurationService();
                testResults.Add(("Configuration Service", configTest.Success, configTest.Message));

                // Test 5: System Tray Service (basic)
                var trayTest = await TestSystemTrayService();
                testResults.Add(("System Tray Service", trayTest.Success, trayTest.Message));

                // Test 6: Auto-start Sequence
                var autoStartTest = await TestAutoStartSequence();
                testResults.Add(("Auto-Start Sequence", autoStartTest.Success, autoStartTest.Message));

                // Print Results
                Console.WriteLine("\n=== TEST RESULTS ===");
                int passedTests = 0;
                foreach (var testResult in testResults)
                {
                    var (testName, passed, details) = testResult;
                    var status = passed ? "PASS" : "FAIL";
                    var icon = passed ? "✅" : "❌";
                    Console.WriteLine($"{icon} {testName}: {status}");
                    if (!string.IsNullOrEmpty(details))
                    {
                        Console.WriteLine($"   Details: {details}");
                    }
                    
                    if (passed) passedTests++;
                }

                Console.WriteLine();
                Console.WriteLine($"Summary: {passedTests}/{testResults.Count} tests passed");
                
                if (passedTests == testResults.Count)
                {
                    Console.WriteLine("🎉 ALL TESTS PASSED - Application is ready for deployment!");
                    return 0;
                }
                else
                {
                    Console.WriteLine("⚠️  Some tests failed - Review implementation before deployment");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
                return 1;
            }
        }

        private static async Task<TestResult> TestApplicationBuild()
        {
            try
            {
                // Check if the executable exists
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EyeRest.exe");
                var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EyeRest.dll");
                
                if (File.Exists(exePath) || File.Exists(dllPath))
                {
                    return new TestResult(true, "Application binaries found and accessible");
                }
                else
                {
                    return new TestResult(false, "Application binaries not found");
                }
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"Build verification failed: {ex.Message}");
            }
        }

        private static async Task<TestResult> TestServiceDependencies()
        {
            try
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

                using var host = hostBuilder.Build();
                await host.StartAsync();

                // Test service resolution
                var configService = host.Services.GetRequiredService<IConfigurationService>();
                var timerService = host.Services.GetRequiredService<ITimerService>();
                var systemTrayService = host.Services.GetRequiredService<ISystemTrayService>();
                var viewModel = host.Services.GetRequiredService<MainWindowViewModel>();

                await host.StopAsync();

                return new TestResult(true, "All services resolved successfully");
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"Service dependency resolution failed: {ex.Message}");
            }
        }

        private static async Task<TestResult> TestTimerServiceFunctionality()
        {
            try
            {
                var hostBuilder = new HostBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddConsole());
                        services.AddSingleton<IConfigurationService, ConfigurationService>();
                        services.AddSingleton<ITimerService, TimerService>();
                    });

                using var host = hostBuilder.Build();
                await host.StartAsync();

                var timerService = host.Services.GetRequiredService<ITimerService>();

                // Test timer start/stop
                Assert(timerService.IsRunning == false, "Timer should not be running initially");

                await timerService.StartAsync();
                Assert(timerService.IsRunning == true, "Timer should be running after start");

                var nextEvent = timerService.NextEventDescription;
                Assert(!string.IsNullOrEmpty(nextEvent), "Next event description should not be empty");
                Assert(nextEvent != "Timers not running", "Should show actual countdown when running");

                await timerService.StopAsync();
                Assert(timerService.IsRunning == false, "Timer should be stopped after stop");

                await host.StopAsync();

                return new TestResult(true, $"Timer functionality verified - Next event: {nextEvent}");
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"Timer service test failed: {ex.Message}");
            }
        }

        private static async Task<TestResult> TestConfigurationService()
        {
            try
            {
                var hostBuilder = new HostBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddConsole());
                        services.AddSingleton<IConfigurationService, ConfigurationService>();
                    });

                using var host = hostBuilder.Build();
                await host.StartAsync();

                var configService = host.Services.GetRequiredService<IConfigurationService>();

                // Test default configuration
                var defaultConfig = await configService.GetDefaultConfiguration();
                Assert(defaultConfig != null, "Default configuration should not be null");
                Assert(defaultConfig.EyeRest.IntervalMinutes > 0, "Eye rest interval should be positive");
                Assert(defaultConfig.Break.IntervalMinutes > 0, "Break interval should be positive");

                // Test configuration loading
                var loadedConfig = await configService.LoadConfigurationAsync();
                Assert(loadedConfig != null, "Loaded configuration should not be null");

                await host.StopAsync();

                return new TestResult(true, $"Configuration loaded - Eye rest: {loadedConfig.EyeRest.IntervalMinutes}min, Break: {loadedConfig.Break.IntervalMinutes}min");
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"Configuration service test failed: {ex.Message}");
            }
        }

        private static async Task<TestResult> TestSystemTrayService()
        {
            try
            {
                var hostBuilder = new HostBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddConsole());
                        services.AddSingleton<IconService>();
                        services.AddSingleton<ISystemTrayService, SystemTrayService>();
                    });

                using var host = hostBuilder.Build();
                await host.StartAsync();

                var systemTrayService = host.Services.GetRequiredService<ISystemTrayService>();

                // Test initialization
                systemTrayService.Initialize();

                // Test icon display (basic verification)
                systemTrayService.ShowTrayIcon();

                // Test icon updates
                systemTrayService.UpdateTrayIcon(TrayIconState.Active);
                systemTrayService.UpdateTrayIcon(TrayIconState.Break);

                // Test cleanup
                systemTrayService.HideTrayIcon();

                await host.StopAsync();

                return new TestResult(true, "System tray service initialized and updated successfully");
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"System tray service test failed: {ex.Message}");
            }
        }

        private static async Task<TestResult> TestAutoStartSequence()
        {
            try
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
                    });

                using var host = hostBuilder.Build();
                var startTime = DateTime.Now;
                
                await host.StartAsync();

                // Simulate auto-start sequence
                var systemTrayService = host.Services.GetRequiredService<ISystemTrayService>();
                systemTrayService.Initialize();
                systemTrayService.ShowTrayIcon();

                var orchestrator = host.Services.GetRequiredService<IApplicationOrchestrator>();
                await orchestrator.InitializeAsync();

                var timerService = host.Services.GetRequiredService<ITimerService>();
                await timerService.StartAsync();

                var initTime = DateTime.Now - startTime;

                // Verify auto-start worked
                Assert(timerService.IsRunning == true, "Timers should be running after auto-start");
                Assert(initTime.TotalSeconds < 5, "Auto-start should complete quickly");

                await host.StopAsync();

                return new TestResult(true, $"Auto-start sequence completed in {initTime.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                return new TestResult(false, $"Auto-start sequence test failed: {ex.Message}");
            }
        }

        // Simple assertion helper
        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Assertion failed: {message}");
            }
        }

        private static void Assert<T>(T obj, string message) where T : class
        {
            if (obj == null)
            {
                throw new InvalidOperationException($"Assertion failed: {message}");
            }
        }

        private static void Assert(object obj, string message)
        {
            if (obj == null)
            {
                throw new InvalidOperationException($"Assertion failed: {message}");
            }
        }

        private record TestResult(bool Success, string Message);
    }
}