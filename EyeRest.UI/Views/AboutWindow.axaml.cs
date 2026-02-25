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

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        _donationService = App.Services?.GetService<IDonationService>();
        UpdateDonationSections();
    }

    private void UpdateDonationSections()
    {
        if (_donationService == null) return;

        var isDonor = _donationService.IsDonor;
        DonateSection.IsVisible = !isDonor;
        DonorBadgeSection.IsVisible = isDonor;
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
