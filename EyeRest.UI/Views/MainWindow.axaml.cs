using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EyeRest.UI.Helpers;
using EyeRest.UI.ViewModels;

namespace EyeRest.UI.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _countdownTimer;

    // Resize animation state
    private DispatcherTimer? _resizeTimer;
    private double _animStartWidth, _animStartHeight;
    private double _animTargetWidth, _animTargetHeight;
    private double _animTargetMinWidth, _animTargetMinHeight;
    private int _animStartX, _animStartY;
    private int _animTargetX, _animTargetY;
    private DateTime _animStartTime;
    private const double AnimDurationMs = 300;

    /// <summary>
    /// True when the window has been hidden to tray via OnClosing (native OrderOut on macOS, Hide on others).
    /// Used to prevent ShowDialog from using a hidden owner, which breaks the Avalonia renderer.
    /// </summary>
    public bool IsHiddenToTray { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        // Set window icon for taskbar/dock display
        // Windows: transparent-background eye (matches tray icon style)
        // macOS: white-background icon (standard dock icon style)
        var iconAsset = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "taskbar-icon.png"
            : "app-icon.png";
        Icon = new WindowIcon(AssetLoader.Open(
            new Uri($"avares://EyeRest/Assets/{iconAsset}")));

        // On Windows, hide system chrome since we have custom caption buttons.
        // macOS keeps PreferSystemChrome for native traffic-light buttons.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        }

        // Start countdown update timer (1-second interval, same as WPF version)
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += OnCountdownTimerTick;
        _countdownTimer.Start();

    }

    private void OnWindowPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        // Walk up from event source — if any ancestor is a Slider or ComboBox, block the event
        var source = e.Source as Avalonia.Visual;
        while (source != null && source != this)
        {
            if (source is Slider or ComboBox)
            {
                e.Handled = true;
                return;
            }
            source = source.GetVisualParent();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        DisableMaximizeButton();

        // Prevent ALL Sliders and ComboBoxes from responding to trackpad/mouse-wheel.
        // On macOS, trackpad scrolling over a Slider silently changes its value, corrupting settings.
        // Window-level tunnel handler catches wheel events before they reach any Slider/ComboBox,
        // even those created later when switching from simple to config mode.
        this.AddHandler(PointerWheelChangedEvent, OnWindowPointerWheel, RoutingStrategies.Tunnel);

        // On macOS, ensure the window comes to front on startup.
        // dotnet run launches from Terminal which keeps focus, so we
        // need to explicitly activate and bring the window forward.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSNativeWindowHelper.SetActivationPolicy(0); // Regular — show dock icon
            MacOSNativeWindowHelper.MakeKeyAndOrderFront(this);
        }

        // Subscribe to ViewModel property changes for mode switching
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;

            // Defense against Slider midpoint write-backs: after UI has fully rendered,
            // re-apply all config values to overwrite any Slider initialization artifacts.
            Dispatcher.UIThread.Post(() => vm.ReapplyConfigurationValues(),
                DispatcherPriority.Loaded);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsConfigurationMode))
        {
            AnimateWindowSize();
        }
    }

    private void AnimateWindowSize()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Stop any in-progress animation
        _resizeTimer?.Stop();
        _resizeTimer = null;

        // Capture starting values
        _animStartWidth = Width;
        _animStartHeight = Height;
        _animStartX = Position.X;
        _animStartY = Position.Y;

        if (vm.IsConfigurationMode)
        {
            _animTargetWidth = 900;
            _animTargetHeight = 700;
            _animTargetMinWidth = 900;
            _animTargetMinHeight = 600;
        }
        else
        {
            _animTargetWidth = 340;
            _animTargetHeight = 580;
            _animTargetMinWidth = 340;
            _animTargetMinHeight = 500;
        }

        // Set MinWidth/MinHeight to the smaller value so the window can
        // animate freely in both directions (fixes the expand-on-drag bug
        // where MinWidth=900 prevented Width from shrinking to 340)
        MinWidth = Math.Min(_animTargetMinWidth, _animStartWidth);
        MinHeight = Math.Min(_animTargetMinHeight, _animStartHeight);

        // Calculate target position: anchor left edge, expand/collapse to the right
        var currentScreen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (currentScreen is { } screen)
        {
            var scaling = screen.Scaling;
            var wa = screen.WorkingArea;

            var targetPhysW = (int)(_animTargetWidth * scaling);
            var targetPhysH = (int)(_animTargetHeight * scaling);

            // Keep left edge (X) fixed — only expand width to the right
            _animTargetX = _animStartX;

            // Keep vertical center fixed
            var centerY = _animStartY + (int)(_animStartHeight * scaling / 2);
            _animTargetY = centerY - targetPhysH / 2;

            // Clamp to screen working area (right edge and vertical bounds)
            if (_animTargetX + targetPhysW > wa.X + wa.Width)
                _animTargetX = wa.X + wa.Width - targetPhysW;
            _animTargetX = Math.Max(wa.X, _animTargetX);
            _animTargetY = Math.Max(wa.Y, Math.Min(_animTargetY, wa.Y + wa.Height - targetPhysH));
        }
        else
        {
            _animTargetX = _animStartX;
            _animTargetY = _animStartY;
        }

        // Start animation timer (~60fps)
        _animStartTime = DateTime.UtcNow;
        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _resizeTimer.Tick += OnResizeAnimationTick;
        _resizeTimer.Start();
    }

    private void OnResizeAnimationTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _animStartTime).TotalMilliseconds;
        var t = Math.Min(elapsed / AnimDurationMs, 1.0);

        // Ease-out cubic for smooth deceleration
        var eased = 1.0 - Math.Pow(1.0 - t, 3);

        Width = _animStartWidth + (_animTargetWidth - _animStartWidth) * eased;
        Height = _animStartHeight + (_animTargetHeight - _animStartHeight) * eased;

        var x = (int)(_animStartX + (_animTargetX - _animStartX) * eased);
        var y = (int)(_animStartY + (_animTargetY - _animStartY) * eased);
        Position = new Avalonia.PixelPoint(x, y);

        if (t >= 1.0)
        {
            _resizeTimer?.Stop();
            _resizeTimer = null;

            // Set final size and constraints
            Width = _animTargetWidth;
            Height = _animTargetHeight;
            MinWidth = _animTargetMinWidth;
            MinHeight = _animTargetMinHeight;
        }
    }

    private void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UpdateCountdown();
        }
    }

    // Nav button click handler
    private void NavButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm
            && sender is Button btn
            && btn.Tag is string tagStr
            && int.TryParse(tagStr, out var index))
        {
            vm.SelectedTabIndex = index;
            ConfigContentScrollViewer.ScrollToHome();
        }
    }

    // Title bar drag
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    // Hamburger menu handlers
    private void AboutMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        aboutWindow.ShowDialog(this);
    }

    // Windows caption button handlers
    private void MinimizeButton_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (App.IsExiting)
        {
            // Full exit requested — allow close through
            base.OnClosing(e);
            return;
        }

        // Hide to tray instead of closing
        e.Cancel = true;
        IsHiddenToTray = true;

        // Stop any in-progress resize animation to prevent stale size state
        _resizeTimer?.Stop();
        _resizeTimer = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // On macOS: use native orderOut to properly hide, then hide dock icon
            Opacity = 0;
            MacOSNativeWindowHelper.OrderOut(this);
            MacOSNativeWindowHelper.SetActivationPolicy(1); // Accessory — hide dock icon
        }
        else
        {
            Hide();
        }
    }

    private void DisableMaximizeButton()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSNativeWindowHelper.DisableZoomButton(this);
        }
    }

    private IBrush? _savedBackground;

    /// <summary>
    /// Force the correct window size and re-layout after restoring from tray.
    /// On Windows, Hide()/Show() with ExtendClientAreaToDecorationsHint can leave
    /// the non-client area calculation stale, causing a white gap on the right side.
    /// </summary>
    public void ResetWindowSizeForCurrentMode()
    {
        if (DataContext is MainWindowViewModel vm && vm.IsConfigurationMode)
        {
            Width = 900;
            Height = 700;
            MinWidth = 900;
            MinHeight = 600;
        }
        else
        {
            Width = 340;
            Height = 580;
            MinWidth = 340;
            MinHeight = 500;
        }

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    public void ShowDimOverlay()
    {
        _savedBackground = Background;
        Background = Brushes.Black; // Fill corner gaps with black to match dim
        DimOverlay.IsVisible = true;
    }

    public void HideDimOverlay()
    {
        DimOverlay.IsVisible = false;
        Background = _savedBackground ?? Brushes.Transparent;
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        _resizeTimer?.Stop();
        _resizeTimer = null;
        base.OnClosed(e);
    }
}
