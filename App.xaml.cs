using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Services.Implementation;
using EyeRest.ViewModels;
using EyeRest.Views;
using System.Windows.Threading;
using Serilog;

namespace EyeRest
{
    public partial class App : Application
    {
        private readonly IHost _host;
        private static Mutex? _instanceMutex;
        private const string MUTEX_NAME = "Global\\EyeRest_SingleInstance_Mutex_UniqueGUID_{B6F2E234-8A4B-4C5D-9E7F-3A8B1C6D4E2F}";

        private ILogger<App>? _logger;
        private DispatcherTimer? _countdownTimer;
        public static IServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
            // CRITICAL FIX: Subscribe to exception handlers to prevent silent crashes
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            // Initialize Serilog configuration
            InitializeSerilog();
            
            _host = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
                .UseSerilog()
                .Build();
            // Make DI ServiceProvider available via App
            ServiceProvider = _host.Services;
        }

        private void InitializeSerilog()
        {
            // Create early logger for startup
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDirectory = Path.Combine(appDataPath, "EyeRest", "logs");
            Directory.CreateDirectory(logDirectory);
            
            // Single log file that gets cleared on each startup
            var logFilePath = Path.Combine(logDirectory, "eyerest.log");
            
            // Clear the existing log file to start fresh
            try
            {
                if (File.Exists(logFilePath))
                {
                    File.Delete(logFilePath);
                }
            }
            catch
            {
                // Ignore errors when clearing log file (e.g., file in use)
            }
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: logFilePath,
                    rollingInterval: RollingInterval.Infinite, // No rolling - single file
                    rollOnFileSizeLimit: false, // Don't roll on size
                    fileSizeLimitBytes: null, // No size limit
                    retainedFileCountLimit: 1, // Keep only one file
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .CreateLogger();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IconService>();
            
            // Register Dispatcher for NotificationService
            services.AddSingleton<Dispatcher>(_ => Dispatcher.CurrentDispatcher);

            // Core Services
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            
            // Dual Configuration Services
            services.AddSingleton<ITimerConfigurationService, TimerConfigurationService>();
            services.AddSingleton<IUIConfigurationService, UIConfigurationService>();
            
            // Timer factory for robust timer creation - FIXED: Use HybridTimerFactory to prevent DispatcherTimer corruption
            services.AddSingleton<ITimerFactory>(provider => 
                new HybridTimerFactory(
                    provider.GetRequiredService<Dispatcher>(),
                    provider.GetService<ILoggerFactory>()
                ));
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IAudioService, AudioService>();
            services.AddSingleton<ISystemTrayService, SystemTrayService>();
            services.AddSingleton<IStartupManager, StartupManager>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            services.AddSingleton<IScreenOverlayService, ScreenOverlayService>();
            
            // Advanced Services (Phase 2+)
            services.AddSingleton<IUserPresenceService, UserPresenceService>();
            services.AddSingleton<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<IReportingService, ReportingService>();
            services.AddSingleton<IPauseReminderService, PauseReminderService>();
            
            // DISABLED: Meeting detection not working reliably - needs improvement and testing in future
            // Network monitoring services
            // services.AddSingleton<INetworkEndpointMonitor, WindowsNetworkEndpointMonitor>();
            // services.AddSingleton<IProcessMonitor, WindowsProcessMonitor>();
            
            // Meeting detection services (transient for factory pattern)
            // services.AddTransient<WindowBasedMeetingDetectionService>();
            // services.AddTransient<NetworkBasedMeetingDetectionService>();
            // services.AddTransient<HybridMeetingDetectionService>();
            
            // Meeting detection factory and manager
            // services.AddSingleton<IMeetingDetectionServiceFactory, MeetingDetectionServiceFactory>();
            // services.AddSingleton<IMeetingDetectionManager, MeetingDetectionManager>();
            
            // Orchestration
            services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
            
            // UI Components
            services.AddTransient<AnalyticsDashboardViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Parse command-line arguments
            bool startMinimizedFromArgs = e.Args.Contains("--minimized");
            if (startMinimizedFromArgs)
            {
                Console.WriteLine($"[STARTUP] Command-line argument --minimized detected");
            }

            // CRITICAL: Check for single instance before initialization
            bool isNewInstance;
            try
            {
                _instanceMutex = new Mutex(true, MUTEX_NAME, out isNewInstance);
            }
            catch (Exception ex)
            {
                // If mutex creation fails, assume we can continue
                Console.WriteLine($"[STARTUP ERROR] Failed to create mutex: {ex.Message}");
                isNewInstance = true;
            }

            if (!isNewInstance)
            {
                Console.WriteLine($"[STARTUP] Another instance of EyeRest is already running. Exiting.");
                MessageBox.Show(
                    "EyeRest is already running.\n\nCheck the system tray for the EyeRest icon.",
                    "Application Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Current.Shutdown(0);
                return;
            }

            Console.WriteLine($"[STARTUP] Starting EyeRest at {DateTime.Now:HH:mm:ss.fff} (Single instance confirmed)");

            await _host.StartAsync();

            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogCritical($"🚀 APPLICATION STARTUP INITIATED - Process ID: {Environment.ProcessId}, Time: {DateTime.Now:HH:mm:ss.fff}");
            _logger.LogCritical($"🚀 Host started successfully, initializing services...");
            // Create main window (lazy initialization)
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            // Initialize countdown timer
            InitializeCountdownTimer();

            try
            {
                _logger.LogCritical($"🚀 PHASE 1: Initializing system tray service at {DateTime.Now:HH:mm:ss.fff}");
                // Initialize system tray first (fastest startup component)
                var systemTrayService = _host.Services.GetRequiredService<ISystemTrayService>();
                systemTrayService.Initialize();
                systemTrayService.ShowTrayIcon();
                _logger.LogCritical($"✅ System tray service initialized successfully");

                _logger.LogCritical($"🚀 PHASE 2: Setting up event handlers at {DateTime.Now:HH:mm:ss.fff}");
                // Handle system tray events
                systemTrayService.RestoreRequested += OnRestoreRequested;
                systemTrayService.ExitRequested += OnExitRequested;
                _logger.LogCritical($"✅ System tray events wired up");

                _logger.LogCritical($"🚀 PHASE 3: Configuring main window at {DateTime.Now:HH:mm:ss.fff}");
                // Handle window closing to minimize to tray instead
                mainWindow.Closing += OnMainWindowClosing;

                // Check if we should start minimized
                var configService = _host.Services.GetRequiredService<IConfigurationService>();
                var config = await configService.LoadConfigurationAsync();
                bool shouldStartMinimized = startMinimizedFromArgs || config.Application.StartMinimized;

                if (shouldStartMinimized)
                {
                    _logger.LogCritical($"✅ Starting minimized to system tray (command-line: {startMinimizedFromArgs}, config: {config.Application.StartMinimized})");
                    // Don't show the window, it will remain hidden in system tray
                }
                else
                {
                    // Show the main window
                    mainWindow.Show();
                    _logger.LogCritical($"✅ Main window shown successfully");
                }

                _logger.LogCritical($"🚀 PHASE 4: Starting application orchestrator at {DateTime.Now:HH:mm:ss.fff}");
                // Initialize the application orchestrator to start all services
                var orchestrator = _host.Services.GetRequiredService<IApplicationOrchestrator>();
                await orchestrator.InitializeAsync();
                _logger.LogCritical($"✅ APPLICATION STARTUP COMPLETED SUCCESSFULLY at {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, $"💥 CRITICAL STARTUP FAILURE at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                MessageBox.Show($"Error during application startup: {ex.Message}\n\nDetails: {ex}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _logger.LogCritical($"🚀 APPLICATION STARTUP SEQUENCE COMPLETED - All services initialized and running at {DateTime.Now:HH:mm:ss.fff}");
        }

        private void InitializeCountdownTimer()
        {
            // FIXED: Use DispatcherTimer for simple UI updates (not critical like timer system)
            // This is acceptable for UI countdown since it doesn't need the reliability of the main timer system
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += OnCountdownTimerTick;
            _countdownTimer.Start();
        }

        private void OnCountdownTimerTick(object? sender, EventArgs e)
        {
            // Update countdown in the UI
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateCountdown();
            }
            
            // REMOVED: Backup trigger system that was causing race conditions
            // Now relying on proper TimerService event handling
        }
        
        // REMOVED: Backup trigger system that was causing race conditions
        // All popup triggering now handled by proper TimerService events

        private async void OnExitRequested(object? sender, EventArgs e)
        {
            // Show confirmation dialog for system tray exit
            var result = MessageBox.Show(
                "Are you sure you want to exit Eye-rest?\n\nThis will stop all break reminders.",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result == MessageBoxResult.No)
            {
                return; // User cancelled exit
            }
            
            _countdownTimer?.Stop();
            
            // Save settings or perform cleanup here
            
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.IsClosing = true;
            }

            // Stop the host
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }

            // CRITICAL: Clean up mutex on shutdown
            if (_instanceMutex != null)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                    _instanceMutex.Dispose();
                    _instanceMutex = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SHUTDOWN] Error releasing mutex: {ex.Message}");
                }
            }

            Current.Shutdown();
        }

        private void OnRestoreRequested(object? sender, EventArgs e)
        {
            if (MainWindow != null)
            {
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            }
        }

        private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Minimize to tray instead of closing
            if (sender is Window window)
            {
                e.Cancel = true;
                window.Hide();
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            _countdownTimer?.Stop();
            
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }

            // CRITICAL: Clean up mutex on application exit
            if (_instanceMutex != null)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                    _instanceMutex.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXIT] Error releasing mutex: {ex.Message}");
                }
            }

            base.OnExit(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("Unhandled domain exception", e.ExceptionObject as Exception);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("Unhandled dispatcher exception", e.Exception);
            
            // Mark as handled to prevent application crash
            e.Handled = true;
            
            // Enhanced error message with more details for debugging
            var errorDetails = $"Exception Type: {e.Exception?.GetType().Name}\n" +
                              $"Message: {e.Exception?.Message}\n" +
                              $"Source: {e.Exception?.Source}";
            
            if (e.Exception?.InnerException != null)
            {
                errorDetails += $"\nInner Exception: {e.Exception.InnerException.Message}";
            }
            
            // Show user-friendly error message
            MessageBox.Show(
                "An unexpected error occurred. The application will continue running, but some features may not work correctly.\n\n" +
                $"Technical details:\n{errorDetails}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("Unobserved task exception", e.Exception);
            
            // Mark as observed to prevent application crash
            e.SetObserved();
        }

        private void LogException(string context, Exception? exception)
        {
            try
            {
                if (_host != null)
                {
                    var logger = _host.Services.GetService<ILogger<App>>();
                    logger?.LogError(exception, context);
                }
                else
                {
                    // Fallback logging when host is not available
                    System.Diagnostics.Debug.WriteLine($"{context}: {exception}");
                }
            }
            catch
            {
                // Ignore logging errors in exception handler
            }
        }
    }
}