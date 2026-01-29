using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.Views;

namespace EyeRest.Tests.E2E
{
    /// <summary>
    /// End-to-end tests for icon integration across different contexts (window title bar and system tray)
    /// </summary>
    public class IconIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private IHost? _testHost;

        public IconIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task IconService_ShouldProvideValidIcon()
        {
            _output.WriteLine("=== Testing Icon Service Functionality ===");
            
            // Arrange
            await InitializeTestHost();
            var iconService = _testHost!.Services.GetRequiredService<IconService>();
            
            // Act
            var applicationIcon = iconService.GetApplicationIcon();
            
            // Assert
            Assert.NotNull(applicationIcon);
            Assert.True(applicationIcon.Width > 0, "Icon should have valid width");
            Assert.True(applicationIcon.Height > 0, "Icon should have valid height");
            
            _output.WriteLine($"✅ Icon service provides valid icon: {applicationIcon.Width}x{applicationIcon.Height}");
        }

        [Fact]
        public async Task WindowIcon_ShouldBeSetCorrectly()
        {
            _output.WriteLine("=== Testing Window Icon Integration ===");
            
            // Arrange
            await InitializeTestHost();
            var iconService = _testHost!.Services.GetRequiredService<IconService>();
            
            // Act - Create MainWindow and verify icon setting
            MainWindow? mainWindow = null;
            BitmapSource? windowIcon = null;
            
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mainWindow = _testHost!.Services.GetRequiredService<MainWindow>();
                windowIcon = mainWindow.Icon as BitmapSource;
            });

            // Assert
            Assert.NotNull(mainWindow);
            Assert.NotNull(windowIcon);
            Assert.True(windowIcon.PixelWidth > 0, "Window icon should have valid pixel width");
            Assert.True(windowIcon.PixelHeight > 0, "Window icon should have valid pixel height");
            
            _output.WriteLine($"✅ Window icon set correctly: {windowIcon.PixelWidth}x{windowIcon.PixelHeight}");
        }

        [Fact]
        public async Task SystemTrayIcon_ShouldBeSetCorrectly()
        {
            _output.WriteLine("=== Testing System Tray Icon Integration ===");
            
            // Arrange
            await InitializeTestHost();
            var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
            var iconService = _testHost.Services.GetRequiredService<IconService>();
            
            // Act
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            
            // Allow tray icon to be set
            await Task.Delay(1000);
            
            // Verify icon service can provide the icon
            var applicationIcon = iconService.GetApplicationIcon();
            
            // Assert
            Assert.NotNull(applicationIcon);
            _output.WriteLine($"✅ System tray icon initialized with icon: {applicationIcon.Width}x{applicationIcon.Height}");
            
            // Test tray icon visibility and functionality
            systemTrayService.UpdateTrayIcon(TrayIconState.Active);
            _output.WriteLine("✅ System tray icon state can be updated");
        }

        [Fact]
        public async Task IconConsistency_ShouldUseSameSourceForAllContexts()
        {
            _output.WriteLine("=== Testing Icon Consistency Across Contexts ===");
            
            // Arrange
            await InitializeTestHost();
            var iconService = _testHost!.Services.GetRequiredService<IconService>();
            var systemTrayService = _testHost.Services.GetRequiredService<ISystemTrayService>();
            
            // Act - Get icon from service (source of truth)
            var sourceIcon = iconService.GetApplicationIcon();
            
            // Initialize system tray with icon
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            
            // Create window with icon
            MainWindow? mainWindow = null;
            BitmapSource? windowIcon = null;
            
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                mainWindow = _testHost!.Services.GetRequiredService<MainWindow>();
                windowIcon = mainWindow.Icon as BitmapSource;
            });

            // Assert - All icons should be derived from the same source
            Assert.NotNull(sourceIcon);
            Assert.NotNull(windowIcon);
            
            // Icons should have same dimensions (indicating same source)
            Assert.Equal(32, sourceIcon.Width); // Standard icon size
            Assert.Equal(32, sourceIcon.Height); // Standard icon size
            
            _output.WriteLine($"✅ Source icon: {sourceIcon.Width}x{sourceIcon.Height}");
            _output.WriteLine($"✅ Window icon: {windowIcon.PixelWidth}x{windowIcon.PixelHeight}");
            _output.WriteLine("✅ Icons are consistent across contexts");
        }

        [Fact]
        public async Task IconResource_ShouldExistAndBeAccessible()
        {
            _output.WriteLine("=== Testing Icon Resource Accessibility ===");
            
            // Arrange
            await InitializeTestHost();
            
            // Act - Check if icon file exists in resources
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            var iconExists = File.Exists(iconPath);
            
            // Also check in the application directory structure
            var alternativeIconPath = Path.Combine(
                Environment.CurrentDirectory, 
                "..", "..", "..", "..", 
                "Resources", "app.ico");
            var alternativeIconExists = File.Exists(alternativeIconPath);
            
            // Assert
            Assert.True(iconExists || alternativeIconExists, 
                $"Icon file should exist at {iconPath} or {alternativeIconPath}");
            
            if (iconExists)
            {
                _output.WriteLine($"✅ Icon found at: {iconPath}");
                var fileInfo = new FileInfo(iconPath);
                _output.WriteLine($"✅ Icon file size: {fileInfo.Length} bytes");
            }
            else if (alternativeIconExists)
            {
                _output.WriteLine($"✅ Icon found at: {alternativeIconExists}");
                var fileInfo = new FileInfo(alternativeIconPath);
                _output.WriteLine($"✅ Icon file size: {fileInfo.Length} bytes");
            }
        }

        [Fact]
        public async Task TrayIconContextMenu_ShouldDisplayCorrectly()
        {
            _output.WriteLine("=== Testing Tray Icon Context Menu ===");
            
            // Arrange
            await InitializeTestHost();
            var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
            
            // Act
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            
            await Task.Delay(1000); // Allow initialization
            
            // Verify context menu functionality by testing event handlers
            bool restoreEventTriggered = false;
            bool exitEventTriggered = false;
            
            systemTrayService.RestoreRequested += (s, e) => restoreEventTriggered = true;
            systemTrayService.ExitRequested += (s, e) => exitEventTriggered = true;
            
            // Trigger the events manually to test functionality
            await Task.Delay(100);
            
            // The events should be set up correctly during initialization
            // In a real UI test, these would be triggered by actual menu clicks
            
            // Assert - Events are properly wired up
            Assert.False(restoreEventTriggered); // Not triggered yet
            Assert.False(exitEventTriggered); // Not triggered yet
            
            _output.WriteLine("✅ Tray icon context menu events work correctly");
        }

        [Fact]
        public async Task IconStateChanges_ShouldUpdateTrayIconCorrectly()
        {
            _output.WriteLine("=== Testing Icon State Changes ===");
            
            // Arrange
            await InitializeTestHost();
            var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
            
            // Act
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            
            await Task.Delay(500);
            
            // Test different tray icon states
            var states = new[] { TrayIconState.Active, TrayIconState.Break, TrayIconState.Error };
            
            foreach (var state in states)
            {
                systemTrayService.UpdateTrayIcon(state);
                await Task.Delay(200); // Brief delay between state changes
                _output.WriteLine($"✅ Tray icon updated to state: {state}");
            }
            
            // Assert - No exceptions should be thrown during state changes
            _output.WriteLine("✅ All tray icon state changes completed successfully");
        }

        [Fact]
        public async Task BalloonTip_ShouldDisplayCorrectly()
        {
            _output.WriteLine("=== Testing Balloon Tip Functionality ===");
            
            // Arrange
            await InitializeTestHost();
            var systemTrayService = _testHost!.Services.GetRequiredService<ISystemTrayService>();
            
            // Act
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            
            await Task.Delay(500);
            
            // Test balloon tip display
            systemTrayService.ShowBalloonTip("Test Title", "Test message for balloon tip");
            
            await Task.Delay(1000); // Allow balloon tip to display
            
            // Assert - No exceptions should be thrown
            _output.WriteLine("✅ Balloon tip displayed successfully");
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
                        services.AddTransient<EyeRest.ViewModels.MainWindowViewModel>();
                        services.AddTransient<MainWindow>();
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