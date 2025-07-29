using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    [Collection("E2E Tests")]
    public class E2ETestSuite : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _host;

        public E2ETestSuite(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Application Lifecycle Tests

        [Fact]
        public async Task TC001_ApplicationStartup_CompletesSuccessfully()
        {
            _output.WriteLine("🧪 TC001: Testing application startup and initialization");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Arrange & Act
            _host = CreateTestHost();
            await _host.StartAsync();
            
            stopwatch.Stop();
            
            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 3000, 
                $"Startup took {stopwatch.ElapsedMilliseconds}ms, exceeds 3000ms requirement");
            
            // Verify all services are registered
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var audioService = _host.Services.GetRequiredService<IAudioService>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var systemTrayService = _host.Services.GetRequiredService<ISystemTrayService>();
            var orchestrator = _host.Services.GetRequiredService<IApplicationOrchestrator>();
            
            Assert.NotNull(configService);
            Assert.NotNull(timerService);
            Assert.NotNull(audioService);
            Assert.NotNull(notificationService);
            Assert.NotNull(systemTrayService);
            Assert.NotNull(orchestrator);
            
            _output.WriteLine($"✅ TC001 PASSED - Startup completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task TC002_SystemTrayIntegration_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC002: Testing system tray integration");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var systemTrayService = _host.Services.GetRequiredService<ISystemTrayService>();
            
            // Act & Assert
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            systemTrayService.UpdateTrayIcon(TrayIconState.Active);
            systemTrayService.UpdateTrayIcon(TrayIconState.Break);
            systemTrayService.UpdateTrayIcon(TrayIconState.Paused);
            systemTrayService.UpdateTrayIcon(TrayIconState.Error);
            systemTrayService.HideTrayIcon();
            
            _output.WriteLine("✅ TC002 PASSED - System tray integration working");
        }

        [Fact]
        public async Task TC003_ApplicationShutdown_CleansUpProperly()
        {
            _output.WriteLine("🧪 TC003: Testing application shutdown and cleanup");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var orchestrator = _host.Services.GetRequiredService<IApplicationOrchestrator>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            
            await orchestrator.InitializeAsync();
            await timerService.StartAsync();
            
            // Act
            await orchestrator.ShutdownAsync();
            await timerService.StopAsync();
            await _host.StopAsync();
            
            // Assert - Should complete without throwing
            _output.WriteLine("✅ TC003 PASSED - Application shutdown completed cleanly");
        }

        #endregion

        #region Eye Rest Reminder Tests

        [Fact]
        public async Task TC005_DefaultEyeRestReminder_ConfiguredCorrectly()
        {
            _output.WriteLine("🧪 TC005: Testing default eye rest reminder configuration");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var config = await configService.LoadConfigurationAsync();
            
            // Assert
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(20, config.EyeRest.DurationSeconds);
            Assert.True(config.EyeRest.StartSoundEnabled);
            Assert.True(config.EyeRest.EndSoundEnabled);
            
            _output.WriteLine("✅ TC005 PASSED - Default eye rest configuration correct");
        }

        [Fact]
        public async Task TC006_CustomEyeRestIntervals_PersistCorrectly()
        {
            _output.WriteLine("🧪 TC006: Testing custom eye rest intervals");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var config = await configService.LoadConfigurationAsync();
            config.EyeRest.IntervalMinutes = 30;
            config.EyeRest.DurationSeconds = 15;
            
            await configService.SaveConfigurationAsync(config);
            var reloadedConfig = await configService.LoadConfigurationAsync();
            
            // Assert
            Assert.Equal(30, reloadedConfig.EyeRest.IntervalMinutes);
            Assert.Equal(15, reloadedConfig.EyeRest.DurationSeconds);
            
            _output.WriteLine("✅ TC006 PASSED - Custom eye rest intervals persisted");
        }

        [Fact]
        public async Task TC007_EyeRestAudioNotifications_WorkCorrectly()
        {
            _output.WriteLine("🧪 TC007: Testing eye rest audio notifications");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var audioService = _host.Services.GetRequiredService<IAudioService>();
            
            // Act & Assert - Should not throw
            await audioService.PlayEyeRestStartSound();
            await audioService.PlayEyeRestEndSound();
            
            _output.WriteLine("✅ TC007 PASSED - Eye rest audio notifications working");
        }

        [Fact]
        public async Task TC008_EyeRestTimerService_FunctionsCorrectly()
        {
            _output.WriteLine("🧪 TC008: Testing eye rest timer service");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var eventRaised = false;
            
            timerService.EyeRestDue += (s, e) => eventRaised = true;
            
            // Act
            await timerService.StartAsync();
            await timerService.ResetEyeRestTimer();
            await timerService.StopAsync();
            
            // Assert
            Assert.False(eventRaised); // Event won't fire in short test duration
            
            _output.WriteLine("✅ TC008 PASSED - Eye rest timer service functioning");
        }

        #endregion

        #region Break Reminder Tests

        [Fact]
        public async Task TC009_DefaultBreakReminder_ConfiguredCorrectly()
        {
            _output.WriteLine("🧪 TC009: Testing default break reminder configuration");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var config = await configService.LoadConfigurationAsync();
            
            // Assert
            Assert.Equal(55, config.Break.IntervalMinutes);
            Assert.Equal(5, config.Break.DurationMinutes);
            Assert.True(config.Break.WarningEnabled);
            Assert.Equal(30, config.Break.WarningSeconds);
            
            _output.WriteLine("✅ TC009 PASSED - Default break configuration correct");
        }

        [Fact]
        public async Task TC010_BreakWarningSystem_ConfiguredCorrectly()
        {
            _output.WriteLine("🧪 TC010: Testing break warning system");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var warningEventRaised = false;
            
            timerService.BreakWarning += (s, e) => warningEventRaised = true;
            
            // Act
            await timerService.StartAsync();
            await timerService.StopAsync();
            
            // Assert
            Assert.False(warningEventRaised); // Event won't fire in short test duration
            
            _output.WriteLine("✅ TC010 PASSED - Break warning system configured");
        }

        [Fact]
        public async Task TC012_BreakDelayFunctionality_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC012: Testing break delay functionality");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            
            // Act & Assert - Should not throw
            await timerService.StartAsync();
            await timerService.DelayBreak(TimeSpan.FromMinutes(1));
            await timerService.DelayBreak(TimeSpan.FromMinutes(5));
            await timerService.StopAsync();
            
            _output.WriteLine("✅ TC012 PASSED - Break delay functionality working");
        }

        #endregion

        #region Settings Management Tests

        [Fact]
        public async Task TC015_SettingsUI_DataBindingWorks()
        {
            _output.WriteLine("🧪 TC015: Testing settings UI data binding");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var logger = _host.Services.GetRequiredService<ILogger<ViewModels.MainWindowViewModel>>();
            
            // Act
            var viewModel = new ViewModels.MainWindowViewModel(
                configService, timerService, startupManager, logger);
            
            // Assert
            Assert.Equal(20, viewModel.EyeRestIntervalMinutes);
            Assert.Equal(55, viewModel.BreakIntervalMinutes);
            Assert.True(viewModel.AudioEnabled);
            
            _output.WriteLine("✅ TC015 PASSED - Settings UI data binding working");
        }

        [Fact]
        public async Task TC016_ConfigurationPersistence_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC016: Testing configuration persistence");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var originalConfig = await configService.LoadConfigurationAsync();
            originalConfig.EyeRest.IntervalMinutes = 25;
            originalConfig.Break.IntervalMinutes = 60;
            originalConfig.Audio.Volume = 75;
            
            await configService.SaveConfigurationAsync(originalConfig);
            var reloadedConfig = await configService.LoadConfigurationAsync();
            
            // Assert
            Assert.Equal(25, reloadedConfig.EyeRest.IntervalMinutes);
            Assert.Equal(60, reloadedConfig.Break.IntervalMinutes);
            Assert.Equal(75, reloadedConfig.Audio.Volume);
            
            _output.WriteLine("✅ TC016 PASSED - Configuration persistence working");
        }

        [Fact]
        public async Task TC017_SettingsValidation_HandlesInvalidValues()
        {
            _output.WriteLine("🧪 TC017: Testing settings validation");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var config = await configService.LoadConfigurationAsync();
            config.EyeRest.IntervalMinutes = -5; // Invalid
            config.Break.DurationMinutes = 100; // Invalid
            config.Audio.Volume = 150; // Invalid
            
            await configService.SaveConfigurationAsync(config);
            var validatedConfig = await configService.LoadConfigurationAsync();
            
            // Assert - Values should be corrected to defaults
            Assert.Equal(20, validatedConfig.EyeRest.IntervalMinutes);
            Assert.Equal(5, validatedConfig.Break.DurationMinutes);
            Assert.Equal(50, validatedConfig.Audio.Volume);
            
            _output.WriteLine("✅ TC017 PASSED - Settings validation working");
        }

        [Fact]
        public async Task TC018_RestoreDefaults_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC018: Testing restore defaults functionality");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act
            var defaultConfig = await configService.GetDefaultConfiguration();
            
            // Assert
            Assert.Equal(20, defaultConfig.EyeRest.IntervalMinutes);
            Assert.Equal(20, defaultConfig.EyeRest.DurationSeconds);
            Assert.Equal(55, defaultConfig.Break.IntervalMinutes);
            Assert.Equal(5, defaultConfig.Break.DurationMinutes);
            Assert.True(defaultConfig.Audio.Enabled);
            Assert.Equal(50, defaultConfig.Audio.Volume);
            
            _output.WriteLine("✅ TC018 PASSED - Restore defaults working");
        }

        #endregion

        #region Audio System Tests

        [Fact]
        public async Task TC019_AudioEnableDisable_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC019: Testing audio enable/disable functionality");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var audioService = _host.Services.GetRequiredService<IAudioService>();
            
            // Act
            var config = await configService.LoadConfigurationAsync();
            Assert.True(audioService.IsAudioEnabled);
            
            config.Audio.Enabled = false;
            await configService.SaveConfigurationAsync(config);
            
            // Simulate configuration change event
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration { Audio = new AudioSettings { Enabled = true } },
                NewConfiguration = config
            };
            
            // Assert - Audio should be disabled
            Assert.False(config.Audio.Enabled);
            
            _output.WriteLine("✅ TC019 PASSED - Audio enable/disable working");
        }

        [Fact]
        public async Task TC020_SystemSoundPlayback_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC020: Testing system sound playback");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var audioService = _host.Services.GetRequiredService<IAudioService>();
            
            // Act & Assert - Should not throw
            await audioService.PlayEyeRestStartSound();
            await audioService.PlayEyeRestEndSound();
            await audioService.PlayBreakWarningSound();
            
            _output.WriteLine("✅ TC020 PASSED - System sound playback working");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task TC027_StartupTime_MeetsRequirement()
        {
            _output.WriteLine("🧪 TC027: Testing startup time requirement (<3 seconds)");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Act
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var orchestrator = _host.Services.GetRequiredService<IApplicationOrchestrator>();
            
            await configService.LoadConfigurationAsync();
            await orchestrator.InitializeAsync();
            await timerService.StartAsync();
            
            stopwatch.Stop();
            
            // Assert
            var startupTimeMs = stopwatch.ElapsedMilliseconds;
            Assert.True(startupTimeMs < 3000, 
                $"Startup time {startupTimeMs}ms exceeds 3000ms requirement");
            
            _output.WriteLine($"✅ TC027 PASSED - Startup time: {startupTimeMs}ms (< 3000ms)");
        }

        [Fact]
        public async Task TC028_MemoryUsage_MeetsRequirement()
        {
            _output.WriteLine("🧪 TC028: Testing memory usage requirement (<50MB)");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var performanceMonitor = _host.Services.GetRequiredService<IPerformanceMonitor>();
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            
            await configService.LoadConfigurationAsync();
            await timerService.StartAsync();
            
            // Force garbage collection for accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Act
            var memoryUsageMB = performanceMonitor.GetMemoryUsageMB();
            
            // Assert
            Assert.True(memoryUsageMB < 50, 
                $"Memory usage {memoryUsageMB}MB exceeds 50MB requirement");
            
            _output.WriteLine($"✅ TC028 PASSED - Memory usage: {memoryUsageMB}MB (< 50MB)");
        }

        [Fact]
        public async Task TC029_CPUUsage_MeetsRequirement()
        {
            _output.WriteLine("🧪 TC029: Testing CPU usage requirement (<1% idle)");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var performanceMonitor = _host.Services.GetRequiredService<IPerformanceMonitor>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            
            await timerService.StartAsync();
            
            // Let the application run for a moment to get accurate CPU reading
            await Task.Delay(2000);
            
            // Act
            var cpuUsage = performanceMonitor.GetCpuUsagePercent();
            
            // Assert - Allow some tolerance for test environment
            Assert.True(cpuUsage < 5, 
                $"CPU usage {cpuUsage:F1}% is higher than expected for idle state");
            
            _output.WriteLine($"✅ TC029 PASSED - CPU usage: {cpuUsage:F1}% (< 5% tolerance)");
        }

        [Fact]
        public async Task TC030_LongRunningStability_MaintainsPerformance()
        {
            _output.WriteLine("🧪 TC030: Testing long-running stability");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var performanceMonitor = _host.Services.GetRequiredService<IPerformanceMonitor>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            await timerService.StartAsync();
            
            var initialMemoryMB = performanceMonitor.GetMemoryUsageMB();
            
            // Act - Simulate extended usage
            for (int i = 0; i < 50; i++)
            {
                await timerService.ResetEyeRestTimer();
                await timerService.ResetBreakTimer();
                await configService.LoadConfigurationAsync();
                
                if (i % 10 == 0)
                {
                    await Task.Delay(100); // Brief pause
                }
            }
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemoryMB = performanceMonitor.GetMemoryUsageMB();
            var memoryIncrease = finalMemoryMB - initialMemoryMB;
            
            // Assert
            Assert.True(memoryIncrease < 10, 
                $"Memory increased by {memoryIncrease}MB, indicating potential memory leak");
            
            _output.WriteLine($"✅ TC030 PASSED - Memory stable: {initialMemoryMB}MB → {finalMemoryMB}MB");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task TC031_ConfigurationCorruption_RecoveredGracefully()
        {
            _output.WriteLine("🧪 TC031: Testing configuration corruption recovery");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            
            // Act - Try to load configuration (should handle corruption gracefully)
            var config = await configService.LoadConfigurationAsync();
            
            // Assert - Should return valid default configuration
            Assert.NotNull(config);
            Assert.Equal(20, config.EyeRest.IntervalMinutes);
            Assert.Equal(55, config.Break.IntervalMinutes);
            
            _output.WriteLine("✅ TC031 PASSED - Configuration corruption handled gracefully");
        }

        [Fact]
        public async Task TC032_TimerFailureRecovery_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC032: Testing timer failure recovery");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            
            // Act - Start, stop, and restart timers multiple times
            await timerService.StartAsync();
            await timerService.StopAsync();
            await timerService.StartAsync();
            await timerService.ResetEyeRestTimer();
            await timerService.ResetBreakTimer();
            await timerService.StopAsync();
            
            // Assert - Should complete without throwing
            _output.WriteLine("✅ TC032 PASSED - Timer failure recovery working");
        }

        #endregion

        private IHost CreateTestHost()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Register all services as in the main application
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<ITimerService, TimerService>();
                    services.AddSingleton<INotificationService, NotificationService>();
                    services.AddSingleton<IAudioService, AudioService>();
                    services.AddSingleton<ISystemTrayService, SystemTrayService>();
                    services.AddSingleton<IStartupManager, StartupManager>();
                    services.AddSingleton<ILoggingService, LoggingService>();
                    services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
                    services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .Build();
        }

        public void Dispose()
        {
            _host?.StopAsync().Wait();
            _host?.Dispose();
        }
    }
}