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
        base.OnClosed(e);
    }
}
