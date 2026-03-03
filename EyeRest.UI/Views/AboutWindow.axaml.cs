using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EyeRest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EyeRest.UI.Views;

public partial class AboutWindow : Window
{
    private readonly IDonationService? _donationService;
    private readonly IUpdateService? _updateService;

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        _donationService = App.Services?.GetService<IDonationService>();
        _updateService = App.Services?.GetService<IUpdateService>();
        UpdateDonationSections();
    }

    private void UpdateDonationSections()
    {
        if (_donationService == null) return;

        var isDonor = _donationService.IsDonor;
        DonateSection.IsVisible = !isDonor;
        DonorBadgeSection.IsVisible = isDonor;
    }

    private async void OnCheckForUpdatesClick(object? sender, RoutedEventArgs e)
    {
        if (_updateService == null || !_updateService.IsUpdateSupported)
        {
            UpdateStatusText.Text = "Updates are not available in this build.";
            return;
        }

        try
        {
            // Phase 1: Check
            CheckForUpdatesButton.IsEnabled = false;
            UpdateButtonText.Text = "Checking...";
            UpdateStatusText.Text = "";

            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                UpdateButtonText.Text = "Check for Updates";
                UpdateStatusText.Text = "You're on the latest version.";
                CheckForUpdatesButton.IsEnabled = true;
                return;
            }

            // Phase 2: Download
            UpdateButtonText.Text = "Downloading...";
            UpdateStatusText.Text = $"Downloading v{updateInfo.TargetVersion}...";

            var progress = new Progress<int>(percent =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateStatusText.Text = $"Downloading v{updateInfo.TargetVersion}... {percent}%";
                });
            });

            await _updateService.DownloadUpdateAsync(progress);

            // Phase 3: Offer restart
            UpdateButtonText.Text = "Restart to Update";
            UpdateStatusText.Text = $"v{updateInfo.TargetVersion} is ready. Click to restart.";
            CheckForUpdatesButton.IsEnabled = true;

            // Rewire button to apply update
            CheckForUpdatesButton.Click -= OnCheckForUpdatesClick;
            CheckForUpdatesButton.Click += OnApplyUpdateClick;
        }
        catch (Exception)
        {
            UpdateButtonText.Text = "Check for Updates";
            UpdateStatusText.Text = "Update failed. Please try again later.";
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void OnApplyUpdateClick(object? sender, RoutedEventArgs e)
    {
        _updateService?.ApplyUpdateAndRestart();
    }

    private void OnDonateClick(object? sender, RoutedEventArgs e)
    {
        if (_donationService == null) return;
        try
        {
            var url = _donationService.DonationUrl;
            var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Best-effort URL open
        }
    }

    private void OnEnterCodeClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new DonationCodeDialog();
        dialog.ShowDialog(this);
    }

    private void OnDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnWebsiteLinkClick(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("https://eyerest.net/") { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Best-effort URL open
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
