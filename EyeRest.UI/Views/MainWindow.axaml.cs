using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
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

        // Start countdown update timer (1-second interval, same as WPF version)
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += OnCountdownTimerTick;
        _countdownTimer.Start();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        DisableMaximizeButton();

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

        // Calculate target position: expand/collapse from window center, clamped to screen
        var currentScreen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (currentScreen is { } screen)
        {
            var scaling = screen.Scaling;
            var wa = screen.WorkingArea;

            // Current center in physical pixels
            var centerX = _animStartX + (int)(_animStartWidth * scaling / 2);
            var centerY = _animStartY + (int)(_animStartHeight * scaling / 2);

            // Target position keeping center fixed
            var targetPhysW = (int)(_animTargetWidth * scaling);
            var targetPhysH = (int)(_animTargetHeight * scaling);
            _animTargetX = centerX - targetPhysW / 2;
            _animTargetY = centerY - targetPhysH / 2;

            // Clamp to screen working area
            _animTargetX = Math.Max(wa.X, Math.Min(_animTargetX, wa.X + wa.Width - targetPhysW));
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
