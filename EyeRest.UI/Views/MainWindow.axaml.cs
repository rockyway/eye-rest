using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Controls;
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
        // On Windows, Avalonia respects MaxHeight/MinHeight constraints which already
        // limit resize. The maximize button is visually present but won't go full-screen
        // because MaxHeight is set in XAML.
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        base.OnClosed(e);
    }
}
