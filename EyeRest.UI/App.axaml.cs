using System;
using System.Collections.Generic;
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
using EyeRest.UI.Helpers;
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
    private Dictionary<TrayIconState, WindowIcon>? _trayIconCache;

    public static IServiceProvider? Services { get; private set; }

    /// <summary>
    /// When true, the app is in the process of fully exiting.
    /// MainWindow.OnClosing checks this to allow close-through vs hide-to-tray.
    /// </summary>
    public static bool IsExiting { get; set; }

    /// <summary>
    /// Set when a background update check finds a new version.
    /// Used to open the About window when the user clicks the balloon notification.
    /// </summary>
    private volatile bool _updateNotificationPending;

    /// <summary>
    /// Debug flag: force the donation banner to show regardless of eligibility.
    /// Set via --show-donation startup argument.
    /// </summary>
    public static bool ForceShowDonationBanner { get; set; }

    /// <summary>
    /// Called from the named-pipe single-instance listener (Program.cs) when a second
    /// instance tries to launch. Marshals to the UI thread and restores the main window.
    /// </summary>
    public static void RestoreMainWindow()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Current is App app)
                app.ShowMainWindow();
        });
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.UseSerilog((context, config) =>
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EyeRest", "logs", "eyerest-.log");
            config.WriteTo.Console()
                  .WriteTo.File(
                      logPath,
                      rollingInterval: Serilog.RollingInterval.Hour,
                      retainedFileCountLimit: 24,
                      rollOnFileSizeLimit: true,
                      fileSizeLimitBytes: 5 * 1024 * 1024);
        });

        builder.ConfigureServices(services =>
        {
            // Cross-platform dispatcher service (must be registered before platform services)
            services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();

            // Platform services — guarded by compile constants so the
            // unused platform assembly is never referenced at compile time,
            // enabling cross-compilation from any host OS.
#if PLATFORM_WINDOWS
            EyeRest.Platform.Windows.WindowsServiceCollectionExtensions.AddWindowsPlatformServices(services);
#elif PLATFORM_MACOS
            EyeRest.Platform.macOS.MacOSServiceCollectionExtensions.AddMacOSPlatformServices(services);
#endif

            // Core services
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<ITimerConfigurationService, TimerConfigurationService>();
            services.AddSingleton<IUIConfigurationService, UIConfigurationService>();
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IReportingService, ReportingService>();
            services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
            services.AddSingleton<IDonationService, DonationService>();
#if !STORE_BUILD
            services.AddSingleton<IUpdateService, EyeRest.UI.Services.UpdateService>();
#endif
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
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            // Handle shutdown to clean up orchestrator and ViewModel
            desktop.ShutdownRequested += async (_, _) =>
            {
                IsExiting = true;
                try
                {
                    // Dispose the ViewModel first to stop debounce timer
                    // and prevent stale config saves during shutdown
                    if (desktop.MainWindow?.DataContext is IDisposable disposableVm)
                        disposableVm.Dispose();

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

            // Bridge host shutdown (Ctrl+C / SIGTERM) to Avalonia's event loop.
            // Without this, Ctrl+C stops the host but the Avalonia loop keeps running
            // because ShutdownMode is OnExplicitShutdown.
            var hostLifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
            hostLifetime.ApplicationStopping.Register(() =>
            {
                // Set immediately (thread-safe) to block Slider write-backs before the Post is processed
                IsExiting = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dt)
                    {
                        dt.Shutdown();
                    }
                });
            });

            // Initialize system tray service (for events, tooltips, notifications)
            var systemTrayService = Services.GetRequiredService<ISystemTrayService>();
            systemTrayService.Initialize();

            // Set up Avalonia TrayIcon (the ObjC NSStatusItem approach doesn't work with Avalonia)
            SetupTrayIcon(systemTrayService);
            PreloadTrayIcons();
            SetMacOSDockIcon();
            SubscribeDockIconClick();

            // Subscribe to tray icon state changes to swap the menu bar icon color
            systemTrayService.TrayIconStateChanged += state =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateTrayIconForState(state));

            // Subscribe to timer updates to refresh the tray menu countdown text
            systemTrayService.TimerDetailsUpdated += (eyeRest, breakTime, status) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateTrayTimerMenuItem(eyeRest, breakTime, status));

            // Open About window when user clicks the update balloon notification
            systemTrayService.BalloonTipClicked += (_, _) =>
            {
                if (_updateNotificationPending)
                {
                    _updateNotificationPending = false;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowAboutWindow());
                }
            };

            logger.LogInformation("System tray initialized");

            // Start the orchestrator (this starts timers, analytics, presence monitoring, etc.)
            var orchestrator = Services.GetRequiredService<IApplicationOrchestrator>();
            await orchestrator.InitializeAsync();
            logger.LogInformation("Application orchestrator initialized - timers are running");

            CheckForUpdatesInBackground();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize application services");
        }
    }

    /// <summary>
    /// Checks for updates 30 seconds after startup, then every 4 hours.
    /// When an update is found, shows a tray balloon notification.
    /// Clicking the notification opens the About window (download/restart flow).
    /// </summary>
    private void CheckForUpdatesInBackground()
    {
#if !STORE_BUILD
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            while (Services != null && !IsExiting)
            {
                try
                {
                    var updateService = Services?.GetService<IUpdateService>();
                    if (updateService == null || !updateService.IsUpdateSupported)
                        return;

                    var updateInfo = await updateService.CheckForUpdatesAsync();
                    if (updateInfo != null)
                    {
                        var version = updateInfo.TargetVersion;
                        Log.Information("Update available: v{Version}", version);

                        _updateNotificationPending = true;
                        var trayService = Services?.GetService<ISystemTrayService>();
                        trayService?.ShowBalloonTip(
                            "Update Available",
                            $"Eye Rest v{version} is ready to download. Click here to update.");

                        return; // Stop checking — user has been notified
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Background update check failed (non-critical)");
                }

                await Task.Delay(TimeSpan.FromHours(4));
            }
        });
#endif
    }

    /// <summary>
    /// Handles the "About Eye-Rest" click from the macOS application menu.
    /// Declared in App.axaml via NativeMenu.Menu.
    /// </summary>
    private void AboutEyeRest_OnClick(object? sender, EventArgs args)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowAboutWindow());
    }

    /// <summary>
    /// Creates an Avalonia TrayIcon with menu, wired to the ISystemTrayService events.
    /// This replaces the broken ObjC NSStatusItem approach — Avalonia manages its own
    /// NSStatusItem internally and the two conflict.
    /// </summary>
    // Timer info menu item — updated every 5s by UpdateTrayTimerMenuItem()
    private NativeMenuItem? _trayTimerInfoItem;

    private void SetupTrayIcon(ISystemTrayService systemTrayService)
    {
        try
        {
            var trayMenu = new NativeMenu();

            // Timer countdown — first row for quick glance
            _trayTimerInfoItem = new NativeMenuItem("Eye Rest: --m | Break: --m") { IsEnabled = false };
            trayMenu.Add(_trayTimerInfoItem);

            trayMenu.Add(new NativeMenuItemSeparator());

            trayMenu.Add(new NativeMenuItem("Show Eye Rest")
            {
                Command = new RelayCommand(() =>
                {
                    ShowMainWindow();
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
            trayMenu.Add(new NativeMenuItem("Meeting 30m")
            {
                Command = new RelayCommand(() => systemTrayService.OnPauseForMeetingRequested())
            });
            trayMenu.Add(new NativeMenuItem("Meeting 1h")
            {
                Command = new RelayCommand(() => systemTrayService.OnPauseForMeeting1hRequested())
            });

            trayMenu.Add(new NativeMenuItemSeparator());

            trayMenu.Add(new NativeMenuItem("About Eye-Rest")
            {
                Command = new RelayCommand(() =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowAboutWindow()))
            });

            trayMenu.Add(new NativeMenuItem("Quit Eye Rest")
            {
                Command = new RelayCommand(() =>
                {
                    IsExiting = true;
                    systemTrayService.OnExitRequested();
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        desktop.Shutdown();
                })
            });

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(
                    GetTrayIconUri(TrayIconState.Active))),
                ToolTipText = "Eye Rest",
                Menu = trayMenu,
                IsVisible = true
            };

            _trayIcon.Clicked += (_, _) => ShowMainWindow();

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
    /// Pre-loads all tray icon images into a dictionary at startup so that
    /// UpdateTrayIconForState can use a fast dictionary lookup instead of
    /// opening PNG streams from the asset loader on every state change.
    /// </summary>
    private void PreloadTrayIcons()
    {
        _trayIconCache = new Dictionary<TrayIconState, WindowIcon>();
        foreach (TrayIconState state in Enum.GetValues(typeof(TrayIconState)))
        {
            try
            {
                var uri = GetTrayIconUri(state);
                var icon = new WindowIcon(AssetLoader.Open(uri));
                _trayIconCache[state] = icon;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to preload tray icon for state {State}; will fall back at runtime", state);
            }
        }
        Log.Information("Preloaded {Count} tray icons into cache", _trayIconCache.Count);
    }

    private void UpdateTrayIconForState(TrayIconState state)
    {
        if (_trayIcon == null) return;
        try
        {
            if (_trayIconCache != null && _trayIconCache.TryGetValue(state, out var cachedIcon))
            {
                _trayIcon.Icon = cachedIcon;
            }
            else
            {
                // Fallback: load from assets if cache miss or cache not yet initialized
                _trayIcon.Icon = new WindowIcon(AssetLoader.Open(GetTrayIconUri(state)));
            }
            Log.Debug("Tray icon updated for state: {State}", state);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update tray icon for state {State}", state);
        }
    }

    private void UpdateTrayTimerMenuItem(TimeSpan eyeRest, TimeSpan breakTime, string status)
    {
        if (_trayTimerInfoItem == null) return;
        try
        {
            string FormatTs(TimeSpan ts) => ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
                : $"{ts.Minutes}m {ts.Seconds:D2}s";

            var text = status switch
            {
                "Stopped" => "Timers stopped",
                var s when s.StartsWith("Meeting Pause") => $"Meeting pause  {s.Replace("Meeting Pause ", "")}",
                var s when s.StartsWith("Paused") => "Paused",
                var s when s.StartsWith("Smart Paused") => $"Smart paused",
                _ => $"Eye Rest {FormatTs(eyeRest)}  |  Break {FormatTs(breakTime)}"
            };

            _trayTimerInfoItem.Header = text;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update tray timer menu item");
        }
    }

    private static Uri GetTrayIconUri(TrayIconState state)
    {
        var name = state switch
        {
            TrayIconState.Active => "tray_active",
            TrayIconState.Paused => "tray_paused",
            TrayIconState.SmartPaused => "tray_smart_paused",
            TrayIconState.ManuallyPaused => "tray_manually_paused",
            TrayIconState.Break => "tray_break",
            TrayIconState.EyeRest => "tray_eye_rest",
            TrayIconState.MeetingMode => "tray_meeting_mode",
            TrayIconState.UserAway => "tray_user_away",
            TrayIconState.Error => "tray_error",
            _ => "tray_active"
        };
        // Use 1x images (22px at 72 DPI = 22pt) so macOS renders at full menu bar size.
        // @2x images (44px) caused Avalonia to downscale, making the icon appear small.
        return new Uri($"avares://EyeRest/Assets/TrayIcons/{name}.png");
    }

    /// <summary>
    /// Restores and shows the main window with platform-specific handling.
    /// On macOS: uses native MakeKeyAndOrderFront + resets activation policy to show dock icon.
    /// On others: uses standard Show() + Activate().
    /// </summary>
    private void ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not Window mainWindow)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSNativeWindowHelper.SetActivationPolicy(0); // Regular — show dock icon
            MacOSNativeWindowHelper.MakeKeyAndOrderFront(mainWindow);
            mainWindow.Opacity = 1;
            mainWindow.InvalidateVisual();
        }
        else
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
            mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();

        if (mainWindow is Views.MainWindow mw)
        {
            mw.IsHiddenToTray = false;

            // On Windows, force re-layout after Show() to fix white gap caused by
            // stale non-client area calculation with ExtendClientAreaToDecorationsHint
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                mw.ResetWindowSizeForCurrentMode();
        }
    }

    /// <summary>
    /// Shows the About window. When the main window is hidden to tray, opens About
    /// as a standalone window to avoid ShowDialog pulling the hidden owner back into
    /// the window server and corrupting the Avalonia renderer.
    /// </summary>
    private void ShowAboutWindow()
    {
        var aboutWindow = new Views.AboutWindow();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Views.MainWindow mainWindow
            && !mainWindow.IsHiddenToTray)
        {
            aboutWindow.ShowDialog(mainWindow);
        }
        else
        {
            aboutWindow.Show();
        }
    }

    /// <summary>
    /// On macOS, subscribes to the dock icon click (IActivatableLifetime.Activated with Reopen)
    /// to restore the main window when the user clicks the dock icon.
    /// </summary>
    private void SubscribeDockIconClick()
    {
        if (ApplicationLifetime is IActivatableLifetime activatable)
        {
            activatable.Activated += (_, args) =>
            {
                if (args.Kind == ActivationKind.Reopen)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ShowMainWindow();
                        // On macOS, balloon click events don't fire — open About on dock click instead
                        if (_updateNotificationPending)
                        {
                            _updateNotificationPending = false;
                            ShowAboutWindow();
                        }
                    });
                }
            };
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
                new Uri("avares://EyeRest/Assets/app-icon.png"));
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
