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

            Console.WriteLine($"[STARTUP] Starting EyeRest at {DateTime.Now:HH:mm:ss.fff}");

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

                // Show the main window initially
                mainWindow.Show();
                _logger.LogCritical($"✅ Main window shown successfully");
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
            
            // CRITICAL FIX: Alternative popup trigger system to bypass broken DispatcherTimer.Tick events
            // This piggybacks on the working UI countdown timer since TimerService DispatcherTimer events never fire
            CheckAndTriggerPopups();
        }
        
        // Track last popup trigger times to prevent duplicate popups
        private DateTime _lastEyeRestWarningCheck = DateTime.MinValue;
        private DateTime _lastEyeRestDueCheck = DateTime.MinValue;
        private DateTime _lastBreakWarningCheck = DateTime.MinValue;
        private DateTime _lastBreakDueCheck = DateTime.MinValue;
        
        private void CheckAndTriggerPopups()
        {
            try
            {
                // Get services from DI container
                var timerService = ServiceProvider?.GetService<ITimerService>();
                var applicationOrchestrator = ServiceProvider?.GetService<IApplicationOrchestrator>();
                var notificationService = ServiceProvider?.GetService<INotificationService>();
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                
                // DEBUG: Log that we're being called
                logger?.LogCritical($"🔍 BACKUP TRIGGER: CheckAndTriggerPopups() called at {DateTime.Now:HH:mm:ss.fff}");
                
                if (timerService == null || applicationOrchestrator == null || notificationService == null)
                {
                    logger?.LogCritical($"❌ BACKUP TRIGGER: Services not available - TimerService={timerService != null}, NotificationService={notificationService != null}");
                    return; // Services not available
                }
                
                var now = DateTime.Now;
                
                // DEBUG: Log timer states
                logger?.LogCritical($"🔍 BACKUP TRIGGER: Timer states - Running={timerService.IsRunning}, Paused={timerService.IsPaused}, SmartPaused={timerService.IsSmartPaused}, ManuallyPaused={timerService.IsManuallyPaused}");
                
                // Only check when timers are running and not manually paused
                // NOTE: We ignore IsSmartPaused because backup trigger is designed to work when user presence detection fails
                if (!timerService.IsRunning || timerService.IsPaused || timerService.IsManuallyPaused)
                {
                    logger?.LogCritical($"🔍 BACKUP TRIGGER: Skipping - timer not active");
                    return;
                }
                
                // Log if smart paused but continuing anyway
                if (timerService.IsSmartPaused)
                {
                    logger?.LogCritical($"⚠️ BACKUP TRIGGER: Timer is smart paused (user presence detection may be wrong) but continuing with backup triggers");
                }
                
                // DEBUG: Log current timer values
                var timeUntilEyeRest = timerService.TimeUntilNextEyeRest;
                var timeUntilBreak = timerService.TimeUntilNextBreak;
                logger?.LogCritical($"🔍 BACKUP TRIGGER: Current times - EyeRest: {timeUntilEyeRest.TotalSeconds:F1}s, Break: {timeUntilBreak.TotalSeconds:F1}s");
                
                // CRITICAL: Zombie popup detection and cleanup
                // Detect when popup claims to be active but timer shows it shouldn't be (mismatch = zombie)
                if (notificationService.IsBreakWarningActive && timeUntilBreak.TotalSeconds > 60)
                {
                    logger?.LogCritical($"🧟 ZOMBIE DETECTION: Break warning popup active but timer shows {timeUntilBreak.TotalSeconds:F0}s remaining (>60s) - ZOMBIE POPUP DETECTED!");
                    logger?.LogCritical($"🧟 ZOMBIE CLEANUP: Force clearing zombie break warning popup");
                    try
                    {
                        notificationService.HideAllNotifications();
                        logger?.LogCritical($"✅ ZOMBIE CLEANUP: Zombie popup cleared successfully");
                    }
                    catch (Exception zombieEx)
                    {
                        logger?.LogError(zombieEx, "❌ ZOMBIE CLEANUP: Error clearing zombie popup");
                    }
                }
                
                if (notificationService.IsEyeRestWarningActive && timeUntilEyeRest.TotalSeconds > 60)
                {
                    logger?.LogCritical($"🧟 ZOMBIE DETECTION: Eye rest warning popup active but timer shows {timeUntilEyeRest.TotalSeconds:F0}s remaining (>60s) - ZOMBIE POPUP DETECTED!");
                    logger?.LogCritical($"🧟 ZOMBIE CLEANUP: Force clearing zombie eye rest warning popup");
                    try
                    {
                        notificationService.HideAllNotifications();
                        logger?.LogCritical($"✅ ZOMBIE CLEANUP: Zombie popup cleared successfully");
                    }
                    catch (Exception zombieEx)
                    {
                        logger?.LogError(zombieEx, "❌ ZOMBIE CLEANUP: Error clearing zombie popup");
                    }
                }
                
                // Check for eye rest warning (30 seconds before due)
                if (timeUntilEyeRest.TotalSeconds <= 30 && timeUntilEyeRest.TotalSeconds > 29 && 
                    (now - _lastEyeRestWarningCheck).TotalSeconds > 5 &&
                    !notificationService.IsEyeRestWarningActive)  // NEW: Don't trigger if warning already active
                {
                    logger?.LogCritical($"🚨 BACKUP TRIGGER: Eye rest warning - {timeUntilEyeRest.TotalSeconds:F0} seconds remaining");
                    _lastEyeRestWarningCheck = now;
                    
                    // Trigger eye rest warning directly via reflection to access private method
                    TriggerTimerEvent(timerService, "EyeRestWarning", timeUntilEyeRest, TimerType.EyeRestWarning);
                }
                else if (timeUntilEyeRest.TotalSeconds <= 30 && timeUntilEyeRest.TotalSeconds > 29 && 
                         notificationService.IsEyeRestWarningActive)
                {
                    // Warning already active - don't trigger again
                    logger?.LogDebug($"🛡️ BACKUP TRIGGER: Eye rest warning already active - skipping duplicate trigger");
                }
                
                // Check for eye rest due (0 seconds or negative)
                else if (timeUntilEyeRest.TotalSeconds <= 0 && 
                    (now - _lastEyeRestDueCheck).TotalSeconds > 5)
                {
                    logger?.LogCritical($"🚨 BACKUP TRIGGER: Eye rest due - {timeUntilEyeRest.TotalSeconds:F0} seconds past due");
                    _lastEyeRestDueCheck = now;
                    
                    // Trigger eye rest due directly
                    TriggerTimerEvent(timerService, "EyeRestDue", TimeSpan.FromSeconds(20), TimerType.EyeRest);
                }
                
                // Check for break warning (30 seconds before due)
                if (timeUntilBreak.TotalSeconds <= 30 && timeUntilBreak.TotalSeconds > 29 && 
                    (now - _lastBreakWarningCheck).TotalSeconds > 5 && 
                    !notificationService.IsBreakWarningActive)  // NEW: Don't trigger if warning already active
                {
                    logger?.LogCritical($"🚨 BACKUP TRIGGER: Break warning - {timeUntilBreak.TotalSeconds:F0} seconds remaining");
                    _lastBreakWarningCheck = now;
                    
                    // Trigger break warning directly
                    TriggerTimerEvent(timerService, "BreakWarning", timeUntilBreak, TimerType.BreakWarning);
                }
                else if (timeUntilBreak.TotalSeconds <= 30 && timeUntilBreak.TotalSeconds > 29 && 
                         notificationService.IsBreakWarningActive)
                {
                    // Warning already active - don't trigger again
                    logger?.LogDebug($"🛡️ BACKUP TRIGGER: Break warning already active - skipping duplicate trigger");
                }
                
                // Check for break due (0 seconds or negative)
                else if (timeUntilBreak.TotalSeconds <= 0 && 
                    (now - _lastBreakDueCheck).TotalSeconds > 5)
                {
                    logger?.LogCritical($"🚨 BACKUP TRIGGER: Break due - {timeUntilBreak.TotalSeconds:F0} seconds past due");
                    _lastBreakDueCheck = now;
                    
                    // Trigger break due directly  
                    TriggerTimerEvent(timerService, "BreakDue", TimeSpan.FromMinutes(5), TimerType.Break);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the UI countdown timer
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, "Error in backup popup trigger system");
            }
        }
        
        private void TriggerTimerEvent(ITimerService timerService, string eventName, TimeSpan duration, TimerType timerType)
        {
            try
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogCritical($"🔥 BACKUP TRIGGER: Manually firing {eventName} event");
                
                // Create timer event args
                var eventArgs = new TimerEventArgs
                {
                    TriggeredAt = DateTime.Now,
                    NextInterval = duration,
                    Type = timerType
                };
                
                // Use reflection to access the private event field and fire it manually
                var eventField = timerService.GetType().GetField(eventName, 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (eventField?.GetValue(timerService) is EventHandler<TimerEventArgs> eventHandler)
                {
                    logger?.LogCritical($"🔥 BACKUP TRIGGER: Found {eventName} event with {eventHandler.GetInvocationList().Length} subscribers");
                    eventHandler?.Invoke(timerService, eventArgs);
                    logger?.LogCritical($"🔥 BACKUP TRIGGER: Successfully fired {eventName} event - popup should appear!");
                }
                else
                {
                    logger?.LogError($"🔥 BACKUP TRIGGER: Could not find {eventName} event field via reflection");
                }
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetService<ILogger<App>>();
                logger?.LogError(ex, $"Error manually triggering {eventName} event");
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