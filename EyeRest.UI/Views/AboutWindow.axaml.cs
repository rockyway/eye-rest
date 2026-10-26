using System;
using System.IO;
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

        VersionText.Text = $"Version {ResolveDisplayVersion()}";

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
        {
            var buildTime = File.GetLastWriteTimeUtc(assemblyPath);
            BuildTimestampText.Text = $"Built {buildTime:yyyy-MM-dd HH:mm:ss} UTC";
        }

        _donationService = App.Services?.GetService<IDonationService>();
        _updateService = App.Services?.GetService<IUpdateService>();
        UpdateDonationSections();
        ReflectPendingUpdate();
    }

    /// <summary>
    /// When a background check already found an update, the button opens the changelog window
    /// (confirm first) instead of downloading straight away.
    /// </summary>
    private void ReflectPendingUpdate()
    {
        var pending = _updateService?.PendingUpdate;
        if (pending == null) return;

        UpdateButtonText.Text = $"Update to {pending.TargetVersion}";
        UpdateStatusText.Text = "New version available.";
        CheckForUpdatesButton.Click -= OnCheckForUpdatesClick;
        CheckForUpdatesButton.Click += OnOpenUpdateWindowClick;
    }

    private void OnOpenUpdateWindowClick(object? sender, RoutedEventArgs e)
    {
        (Avalonia.Application.Current as App)?.ShowUpdateWindow();
        Close();
    }

    private void OnWhatsNewClick(object? sender, PointerPressedEventArgs e)
    {
        var current = _updateService?.CurrentVersion ?? ResolveDisplayVersion();
        var releases = EyeRest.UI.Services.ChangelogProvider.GetReleasesBetween(null, current);
        // The latest release only: this link answers "what changed in the version I'm running".
        if (releases.Count > 1) releases = releases.GetRange(0, 1);
        if (releases.Count == 0)
        {
            UpdateStatusText.Text = "No highlights are bundled for this version.";
            return;
        }
        (Avalonia.Application.Current as App)?.ShowWhatsNewWindow(releases, current);
    }

    /// <summary>
    /// Prefer AssemblyInformationalVersion (the GitHub-release tag, set by publish-release.sh
    /// via -p:InformationalVersion). Fall back to AssemblyVersion only when that attribute is
    /// missing or carries an obvious build-default value.
    /// </summary>
    private static string ResolveDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();

        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Velopack/CI sometimes appends "+<git-sha>" or "+<build-meta>" — strip it for display.
            var plus = informational.IndexOf('+');
            if (plus >= 0) informational = informational[..plus];
            return informational;
        }

        var v = asm.GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "unknown";
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
            UpdateStatusText.Text = "You're on the latest version.";
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

            // Show the changelog and let the user confirm before anything downloads.
            (Avalonia.Application.Current as App)?.ShowUpdateWindow();
            Close();
        }
        catch (Exception)
        {
            UpdateButtonText.Text = "Check for Updates";
            UpdateStatusText.Text = "Update failed. Please try again later.";
            CheckForUpdatesButton.IsEnabled = true;
        }
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
