using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EyeRest.UI;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.UseSerilog((context, config) =>
        {
            config.WriteTo.Console()
                  .WriteTo.File(
                      System.IO.Path.Combine(
                          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                          "EyeRest", "logs", "eyerest.log"),
                      rollingInterval: Serilog.RollingInterval.Day);
        });

        builder.ConfigureServices(services =>
        {
            // Platform services (conditional)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS platform services
                EyeRest.Platform.macOS.MacOSServiceCollectionExtensions.AddMacOSPlatformServices(services);
            }
            // Windows platform would be added here on Windows
            // Linux can be added later

            // Core services
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<ITimerConfigurationService, TimerConfigurationService>();
            services.AddSingleton<IUIConfigurationService, UIConfigurationService>();
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IReportingService, ReportingService>();
            services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            services.AddSingleton<IApplicationOrchestrator, ApplicationOrchestrator>();

            // Avalonia-specific services
            services.AddSingleton<IPopupWindowFactory, AvaloniaPopupWindowFactory>();
            services.AddSingleton<INotificationService, AvaloniaNotificationService>();

            // ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<AnalyticsDashboardViewModel>();
        });

        _host = builder.Build();
        Services = _host.Services;

        var logger = Services.GetRequiredService<ILogger<App>>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new Views.MainWindow();
            mainWindow.DataContext = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

            // Handle shutdown to clean up orchestrator
            desktop.ShutdownRequested += async (_, _) =>
            {
                try
                {
                    var orchestrator = Services?.GetService<IApplicationOrchestrator>();
                    if (orchestrator != null)
                        await orchestrator.ShutdownAsync();
                    _host?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during shutdown");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();

        // Initialize services after framework is ready (mirrors WPF OnStartup sequence)
        try
        {
            await _host.StartAsync();
            logger.LogInformation("Host started successfully");

            // Initialize system tray
            var systemTrayService = Services.GetRequiredService<ISystemTrayService>();
            systemTrayService.Initialize();
            systemTrayService.ShowTrayIcon();
            logger.LogInformation("System tray initialized");

            // Start the orchestrator (this starts timers, analytics, presence monitoring, etc.)
            var orchestrator = Services.GetRequiredService<IApplicationOrchestrator>();
            await orchestrator.InitializeAsync();
            logger.LogInformation("Application orchestrator initialized - timers are running");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize application services");
        }
    }
}
