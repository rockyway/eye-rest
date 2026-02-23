using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EyeRest.UI;

public partial class App : Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
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
        });

        _host = builder.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow();
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
