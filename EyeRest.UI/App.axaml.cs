using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
    private TrayIcon? _trayIcon;

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

            // Initialize system tray service (for events, tooltips, notifications)
            var systemTrayService = Services.GetRequiredService<ISystemTrayService>();
            systemTrayService.Initialize();

            // Set up Avalonia TrayIcon (the ObjC NSStatusItem approach doesn't work with Avalonia)
            SetupTrayIcon(systemTrayService);
            SetMacOSDockIcon();
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

    /// <summary>
    /// Creates an Avalonia TrayIcon with menu, wired to the ISystemTrayService events.
    /// This replaces the broken ObjC NSStatusItem approach — Avalonia manages its own
    /// NSStatusItem internally and the two conflict.
    /// </summary>
    private void SetupTrayIcon(ISystemTrayService systemTrayService)
    {
        try
        {
            var trayMenu = new NativeMenu();

            trayMenu.Add(new NativeMenuItem("Show Eye Rest")
            {
                Command = new RelayCommand(() =>
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                        && desktop.MainWindow is Window mainWindow)
                    {
                        mainWindow.Show();
                        if (mainWindow.WindowState == WindowState.Minimized)
                            mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                    systemTrayService.OnRestoreRequested();
                })
            });

            trayMenu.Add(new NativeMenuItemSeparator());

            trayMenu.Add(new NativeMenuItem("Pause Timers")
            {
                Command = new RelayCommand(() => systemTrayService.OnPauseTimersRequested())
            });
            trayMenu.Add(new NativeMenuItem("Resume Timers")
            {
                Command = new RelayCommand(() => systemTrayService.OnResumeTimersRequested())
            });
            trayMenu.Add(new NativeMenuItem("Pause for Meeting")
            {
                Command = new RelayCommand(() => systemTrayService.OnPauseForMeetingRequested())
            });

            trayMenu.Add(new NativeMenuItemSeparator());

            trayMenu.Add(new NativeMenuItem("Quit Eye Rest")
            {
                Command = new RelayCommand(() =>
                {
                    systemTrayService.OnExitRequested();
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        desktop.Shutdown();
                })
            });

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(
                    new Uri("avares://EyeRest.UI/Assets/app-icon.png"))),
                ToolTipText = "Eye Rest",
                Menu = trayMenu,
                IsVisible = true
            };

            var trayIcons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(this, trayIcons);

            Log.Information("Avalonia TrayIcon configured with context menu");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set up tray icon");
        }
    }

    /// <summary>
    /// Sets the macOS dock icon via native NSApplication API.
    /// When running unbundled (dotnet run), there's no .app bundle Info.plist to read from,
    /// so we must set it programmatically via ObjC runtime interop.
    /// </summary>
    private void SetMacOSDockIcon()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyDockIcon());
        });
    }

    private void ApplyDockIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(
                new Uri("avares://EyeRest.UI/Assets/app-icon.png"));
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var pngBytes = ms.ToArray();

            var nsDataClass = objc_getClass("NSData");
            var dataWithBytesLengthSel = sel_registerName("dataWithBytes:length:");
            var nsData = objc_msgSend_IntPtr(nsDataClass, dataWithBytesLengthSel, pngBytes, (nint)pngBytes.Length);
            if (nsData == IntPtr.Zero) return;

            var nsImageClass = objc_getClass("NSImage");
            var allocSel = sel_registerName("alloc");
            var initWithDataSel = sel_registerName("initWithData:");
            var nsImageAlloc = objc_msgSend(nsImageClass, allocSel);
            var nsImage = objc_msgSend(nsImageAlloc, initWithDataSel, nsData);
            if (nsImage == IntPtr.Zero) return;

            var nsAppClass = objc_getClass("NSApplication");
            var sharedAppSel = sel_registerName("sharedApplication");
            var setIconSel = sel_registerName("setApplicationIconImage:");
            var nsApp = objc_msgSend(nsAppClass, sharedAppSel);
            if (nsApp == IntPtr.Zero) return;

            objc_msgSend(nsApp, setIconSel, nsImage);
            Log.Information("macOS dock icon set from app-icon.png");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to set macOS dock icon (non-critical)");
        }
    }

    // ObjC runtime P/Invoke for macOS dock icon

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selectorName);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, byte[] arg1, nint arg2);
}

/// <summary>
/// Simple ICommand implementation for NativeMenuItem commands.
/// </summary>
internal class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
