using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.E2E
{
    [Collection("E2E UI Tests")]
    public class UIValidationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _host;

        public UIValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TC_UI_001_DefaultValues_DisplayCorrectly()
        {
            _output.WriteLine("🧪 TC_UI_001: Testing UI default values display correctly");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            // Act - Create ViewModel (simulates UI initialization)
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            
            // Wait a moment for async configuration loading
            await Task.Delay(500);
            
            // Assert - Check all default values match requirements
            _output.WriteLine($"Eye Rest Interval: {viewModel.EyeRestIntervalMinutes} (expected: 20)");
            _output.WriteLine($"Eye Rest Duration: {viewModel.EyeRestDurationSeconds} (expected: 20)");
            _output.WriteLine($"Break Interval: {viewModel.BreakIntervalMinutes} (expected: 55)");
            _output.WriteLine($"Break Duration: {viewModel.BreakDurationMinutes} (expected: 5)");
            _output.WriteLine($"Break Warning Seconds: {viewModel.BreakWarningSeconds} (expected: 30)");
            _output.WriteLine($"Audio Volume: {viewModel.AudioVolume} (expected: 50)");
            
            // Eye Rest Settings - Requirement 1
            Assert.Equal(20, viewModel.EyeRestIntervalMinutes);
            Assert.Equal(20, viewModel.EyeRestDurationSeconds);
            Assert.True(viewModel.EyeRestStartSoundEnabled);
            Assert.True(viewModel.EyeRestEndSoundEnabled);
            
            // Break Settings - Requirement 2
            Assert.Equal(55, viewModel.BreakIntervalMinutes);
            Assert.Equal(5, viewModel.BreakDurationMinutes);
            Assert.True(viewModel.BreakWarningEnabled);
            Assert.Equal(30, viewModel.BreakWarningSeconds);
            
            // Audio Settings - Requirement 1.3
            Assert.True(viewModel.AudioEnabled);
            Assert.Equal(50, viewModel.AudioVolume);
            Assert.Null(viewModel.CustomSoundPath);
            
            // Application Settings - Requirement 4
            Assert.False(viewModel.StartWithWindows);
            Assert.True(viewModel.MinimizeToTray);
            Assert.False(viewModel.ShowInTaskbar);
            
            _output.WriteLine("✅ TC_UI_001 PASSED - All default values display correctly");
        }

        [Fact]
        public async Task TC_UI_002_ConfigurationPersistence_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC_UI_002: Testing configuration persistence");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500); // Wait for initial load
            
            // Act - Modify values
            viewModel.EyeRestIntervalMinutes = 25;
            viewModel.EyeRestDurationSeconds = 15;
            viewModel.BreakIntervalMinutes = 60;
            viewModel.BreakDurationMinutes = 10;
            viewModel.AudioVolume = 75;
            viewModel.EyeRestStartSoundEnabled = false;
            
            // Save configuration
            viewModel.SaveCommand.Execute(null);
            
            // Create new ViewModel to test persistence
            var newViewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500); // Wait for configuration load
            
            // Assert - Values should be persisted
            Assert.Equal(25, newViewModel.EyeRestIntervalMinutes);
            Assert.Equal(15, newViewModel.EyeRestDurationSeconds);
            Assert.Equal(60, newViewModel.BreakIntervalMinutes);
            Assert.Equal(10, newViewModel.BreakDurationMinutes);
            Assert.Equal(75, newViewModel.AudioVolume);
            Assert.False(newViewModel.EyeRestStartSoundEnabled);
            
            _output.WriteLine("✅ TC_UI_002 PASSED - Configuration persistence works correctly");
        }

        [Fact]
        public async Task TC_UI_003_RestoreDefaults_ResetsAllValues()
        {
            _output.WriteLine("🧪 TC_UI_003: Testing restore defaults functionality");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Act - Modify values away from defaults
            viewModel.EyeRestIntervalMinutes = 99;
            viewModel.EyeRestDurationSeconds = 99;
            viewModel.BreakIntervalMinutes = 99;
            viewModel.BreakDurationMinutes = 99;
            viewModel.AudioVolume = 99;
            viewModel.EyeRestStartSoundEnabled = false;
            viewModel.EyeRestEndSoundEnabled = false;
            viewModel.BreakWarningEnabled = false;
            viewModel.AudioEnabled = false;
            
            // Restore defaults
            viewModel.RestoreDefaultsCommand.Execute(null);
            
            // Assert - All values should be back to defaults
            Assert.Equal(20, viewModel.EyeRestIntervalMinutes);
            Assert.Equal(20, viewModel.EyeRestDurationSeconds);
            Assert.Equal(55, viewModel.BreakIntervalMinutes);
            Assert.Equal(5, viewModel.BreakDurationMinutes);
            Assert.Equal(30, viewModel.BreakWarningSeconds);
            Assert.Equal(50, viewModel.AudioVolume);
            Assert.True(viewModel.EyeRestStartSoundEnabled);
            Assert.True(viewModel.EyeRestEndSoundEnabled);
            Assert.True(viewModel.BreakWarningEnabled);
            Assert.True(viewModel.AudioEnabled);
            Assert.True(viewModel.MinimizeToTray);
            Assert.False(viewModel.StartWithWindows);
            Assert.False(viewModel.ShowInTaskbar);
            
            _output.WriteLine("✅ TC_UI_003 PASSED - Restore defaults resets all values correctly");
        }

        [Fact]
        public async Task TC_UI_004_ValidationRules_EnforceCorrectRanges()
        {
            _output.WriteLine("🧪 TC_UI_004: Testing input validation rules");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Act & Assert - Test invalid values are corrected when saved
            viewModel.EyeRestIntervalMinutes = -5; // Invalid
            viewModel.EyeRestDurationSeconds = 500; // Invalid
            viewModel.BreakIntervalMinutes = 300; // Invalid
            viewModel.BreakDurationMinutes = 50; // Invalid
            viewModel.AudioVolume = 150; // Invalid
            
            viewModel.SaveCommand.Execute(null);
            
            // Create new ViewModel to check if validation was applied
            var newViewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Values should be corrected to valid defaults
            Assert.Equal(20, newViewModel.EyeRestIntervalMinutes); // Corrected from -5
            Assert.Equal(20, newViewModel.EyeRestDurationSeconds); // Corrected from 500
            Assert.Equal(55, newViewModel.BreakIntervalMinutes); // Corrected from 300
            Assert.Equal(5, newViewModel.BreakDurationMinutes); // Corrected from 50
            Assert.Equal(50, newViewModel.AudioVolume); // Corrected from 150
            
            _output.WriteLine("✅ TC_UI_004 PASSED - Validation rules enforce correct ranges");
        }

        [Fact]
        public async Task TC_UI_005_ChangeDetection_WorksCorrectly()
        {
            _output.WriteLine("🧪 TC_UI_005: Testing change detection for save/cancel functionality");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Act & Assert - Initially no unsaved changes
            Assert.False(viewModel.HasUnsavedChanges);
            
            // Make a change
            viewModel.EyeRestIntervalMinutes = 25;
            Assert.True(viewModel.HasUnsavedChanges);
            
            // Cancel changes
            viewModel.CancelCommand.Execute(null);
            Assert.False(viewModel.HasUnsavedChanges);
            Assert.Equal(20, viewModel.EyeRestIntervalMinutes); // Should be back to original
            
            // Make change and save
            viewModel.EyeRestIntervalMinutes = 25;
            Assert.True(viewModel.HasUnsavedChanges);
            
            viewModel.SaveCommand.Execute(null);
            Assert.False(viewModel.HasUnsavedChanges); // Should be false after save
            
            _output.WriteLine("✅ TC_UI_005 PASSED - Change detection works correctly");
        }

        [Fact]
        public async Task TC_UI_006_TimerCommands_ExecuteCorrectly()
        {
            _output.WriteLine("🧪 TC_UI_006: Testing timer start/stop commands");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Act & Assert - Commands should execute without throwing
            viewModel.StartTimersCommand.Execute(null);
            _output.WriteLine("Start timers command executed successfully");
            
            viewModel.StopTimersCommand.Execute(null);
            _output.WriteLine("Stop timers command executed successfully");
            
            _output.WriteLine("✅ TC_UI_006 PASSED - Timer commands execute correctly");
        }

        [Fact]
        public async Task TC_UI_007_RequirementsCompliance_AllRequirementsMet()
        {
            _output.WriteLine("🧪 TC_UI_007: Testing complete requirements compliance");
            
            // Arrange
            _host = CreateTestHost();
            await _host.StartAsync();
            
            var configService = _host.Services.GetRequiredService<IConfigurationService>();
            var timerConfigService = _host.Services.GetRequiredService<ITimerConfigurationService>();
            var uiConfigService = _host.Services.GetRequiredService<IUIConfigurationService>();
            var timerService = _host.Services.GetRequiredService<ITimerService>();
            var startupManager = _host.Services.GetRequiredService<IStartupManager>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            var screenOverlayService = _host.Services.GetRequiredService<IScreenOverlayService>();
            var analyticsViewModel = _host.Services.GetRequiredService<AnalyticsDashboardViewModel>();
            var logger = _host.Services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var viewModel = new MainWindowViewModel(configService, timerConfigService, uiConfigService, timerService, startupManager, notificationService, screenOverlayService, analyticsViewModel, logger);
            await Task.Delay(500);
            
            // Assert - Requirement 1: Eye Rest Reminder System
            _output.WriteLine("✓ Requirement 1: Eye Rest Reminder System");
            Assert.Equal(20, viewModel.EyeRestIntervalMinutes); // 20-minute intervals
            Assert.Equal(20, viewModel.EyeRestDurationSeconds); // 20-second duration
            Assert.True(viewModel.EyeRestStartSoundEnabled); // Audio notifications
            Assert.True(viewModel.EyeRestEndSoundEnabled); // Audio notifications
            
            // Assert - Requirement 2: Break Reminder System
            _output.WriteLine("✓ Requirement 2: Break Reminder System");
            Assert.Equal(55, viewModel.BreakIntervalMinutes); // 55-minute work periods
            Assert.Equal(5, viewModel.BreakDurationMinutes); // 5-minute breaks
            Assert.True(viewModel.BreakWarningEnabled); // Pre-break warnings
            Assert.Equal(30, viewModel.BreakWarningSeconds); // 30-second warning
            
            // Assert - Requirement 3: Settings Management
            _output.WriteLine("✓ Requirement 3: Settings Management");
            Assert.NotNull(viewModel.SaveCommand); // Settings can be saved
            Assert.NotNull(viewModel.CancelCommand); // Settings can be cancelled
            Assert.NotNull(viewModel.RestoreDefaultsCommand); // Defaults can be restored
            
            // Assert - Requirement 4: System Tray Integration
            _output.WriteLine("✓ Requirement 4: System Tray Integration");
            Assert.True(viewModel.MinimizeToTray); // Minimize to tray enabled
            Assert.False(viewModel.ShowInTaskbar); // Not shown in taskbar by default
            
            // Assert - Audio System
            _output.WriteLine("✓ Audio System");
            Assert.True(viewModel.AudioEnabled); // Audio enabled by default
            Assert.Equal(50, viewModel.AudioVolume); // 50% volume default
            
            // Assert - Application Settings
            _output.WriteLine("✓ Application Settings");
            Assert.False(viewModel.StartWithWindows); // Not starting with Windows by default
            
            _output.WriteLine("✅ TC_UI_007 PASSED - All requirements compliance verified");
        }

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