using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EyeRest.Services;
using EyeRest.ViewModels;
using EyeRest.Views;
using System.Windows.Threading;
using Serilog;

namespace EyeRest
{
    public partial class App : Application
    {
        private readonly IHost _host;

        private ILogger<App>? _logger;
        private DispatcherTimer? _countdownTimer;

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

            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IAudioService, AudioService>();
            services.AddSingleton<ISystemTrayService, SystemTrayService>();
            services.AddSingleton<IStartupManager, StartupManager>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            services.AddSingleton<IScreenOverlayService, ScreenOverlayService>();
            services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            await _host.StartAsync();

            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            // Create main window (lazy initialization)
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            // Initialize countdown timer
            InitializeCountdownTimer();

            try
            {
                // Initialize system tray first (fastest startup component)
                var systemTrayService = _host.Services.GetRequiredService<ISystemTrayService>();
                systemTrayService.Initialize();
                systemTrayService.ShowTrayIcon();

                // Handle system tray events
                systemTrayService.RestoreRequested += OnRestoreRequested;
                systemTrayService.ExitRequested += OnExitRequested;

                // MainWindow is already created above

                // Handle window closing to minimize to tray instead
                mainWindow.Closing += OnMainWindowClosing;

                // Show the main window initially
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during application startup: {ex.Message}\n\nDetails: {ex}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Initialize application orchestrator on UI thread (DispatcherTimer requires UI thread)
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    // Step 1: Initialize orchestrator (subscribes to timer events)
                    var orchestrator = _host.Services.GetRequiredService<IApplicationOrchestrator>();
                    await orchestrator.InitializeAsync();
                    _logger?.LogInformation("ApplicationOrchestrator initialized - events subscribed");
                    
                    // Step 2: Small delay to ensure event subscription completes
                    await Task.Delay(200);
                    
                    // Step 3: Start timer service ON UI THREAD (DispatcherTimer requirement)
                    var timerService = _host.Services.GetRequiredService<ITimerService>();
                    await timerService.StartAsync();
                    
                    _logger?.LogInformation("🎯 Application services started successfully - timers running ON UI THREAD");
                    
                    // Update UI
                    if (MainWindow is MainWindow window)
                    {
                        window.UpdateCountdown();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to start application services");
                    
                    MessageBox.Show(
                        $"Warning: Timer service failed to start automatically.\n\n" +
                        $"Error: {ex.Message}\n\n" +
                        $"You can manually start the timers using the 'Start Timers' button in the application.",
                        "Timer Startup Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            });
        }

        private void InitializeCountdownTimer()
        {
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
        }



        private async void OnExitRequested(object? sender, EventArgs e)
        {
            // Show confirmation dialog for system tray exit
            var result = MessageBox.Show(
                "Are you sure you want to exit Eye-rest?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
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
            
            // Show user-friendly error message
            MessageBox.Show(
                "An unexpected error occurred. The application will continue running, but some features may not work correctly.",
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