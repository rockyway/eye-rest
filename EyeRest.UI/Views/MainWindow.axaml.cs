using System;
using Avalonia.Controls;
using Avalonia.Threading;
using EyeRest.UI.ViewModels;

namespace EyeRest.UI.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _countdownTimer;

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

    private void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UpdateCountdown();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
        base.OnClosed(e);
    }
}
