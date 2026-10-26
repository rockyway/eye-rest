using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.UI.Services;

namespace EyeRest.UI.Views;

/// <summary>
/// One window, two jobs. Update mode shows the changelog before an update and walks through
/// confirm, download and restart. What's-new mode shows the changelog once after an update.
/// </summary>
public partial class ChangelogWindow : Window
{
    private enum UpdateStep { Available, Downloading, Ready, Failed }

    private IUpdateService? _updateService;
    private AppUpdateInfo? _update;
    private UpdateStep _step;
    private bool _isWhatsNew;
    private bool _closed;
    private string _fullNotesUrl = string.Empty;
    private Action<AppUpdateInfo>? _onDownloadedWhileClosed;

    public ChangelogWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Closed += (_, _) => _closed = true;
    }

    /// <summary>
    /// Pre-update mode. Opens on the confirm step, or on "ready" when the update is already downloaded.
    /// <paramref name="onDownloadedWhileClosed"/> is called if the user hides the window and the
    /// download finishes afterwards.
    /// </summary>
    public void ShowUpdate(IUpdateService updateService, AppUpdateInfo update,
        Action<AppUpdateInfo>? onDownloadedWhileClosed = null)
    {
        _isWhatsNew = false;
        _updateService = updateService;
        _update = update;
        _onDownloadedWhileClosed = onDownloadedWhileClosed;
        _fullNotesUrl = ChangelogProvider.ReleasesUrlPrefix + update.TargetVersion;

        FromVersionText.Text = updateService.CurrentVersion;
        ToVersionText.Text = update.TargetVersion;
        FullNotesLink.Text = "Read the full release notes on GitHub ↗";

        var entries = ChangelogParser.ParseHighlights(update.ReleaseNotesMarkdown);
        EntriesPanel.Children.Clear();
        if (entries.Count == 0)
        {
            AddNote("Highlights for this version are in the full release notes.");
        }
        else
        {
            foreach (var entry in entries) AddEntry(entry);
        }

        SetStep(update.IsDownloaded ? UpdateStep.Ready : UpdateStep.Available);
    }

    /// <summary>Post-update mode: highlights of every release the user has not seen yet.</summary>
    public void ShowWhatsNew(IReadOnlyList<ChangelogRelease> releases, string currentVersion)
    {
        _isWhatsNew = true;
        _fullNotesUrl = ChangelogProvider.ReleasesUrlPrefix + currentVersion;

        TitleText.Text = $"Updated to {currentVersion}";
        SubtitleText.Text = "Here is what changed.";
        VersionRow.IsVisible = false;
        FullNotesLink.Text = "Read the full release notes ↗";
        FullNotesLink.IsVisible = true;
        ProgressPanel.IsVisible = false;
        FootnoteText.IsVisible = false;

        EntriesPanel.Children.Clear();
        foreach (var release in releases)
        {
            if (releases.Count > 1)
            {
                EntriesPanel.Children.Add(new TextBlock
                {
                    Text = $"Version {release.Version}",
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush("AccentBrush")
                });
            }
            foreach (var entry in release.Entries) AddEntry(entry);
        }

        SecondaryButton.IsVisible = false;
        Grid.SetColumn(PrimaryButton, 0);
        Grid.SetColumnSpan(PrimaryButton, 3);
        PrimaryButtonText.Text = "Got it";
    }

    private void SetStep(UpdateStep step)
    {
        _step = step;
        var version = _update?.TargetVersion ?? string.Empty;

        // Defaults for the confirm step; each branch below overrides what differs.
        NotesBorder.IsVisible = true;
        VersionRow.IsVisible = true;
        FullNotesLink.IsVisible = true;
        ProgressPanel.IsVisible = false;
        FootnoteText.IsVisible = false;
        SecondaryButton.IsVisible = true;
        PrimaryButton.IsVisible = true;
        Grid.SetColumn(PrimaryButton, 2);
        Grid.SetColumnSpan(PrimaryButton, 1);
        Grid.SetColumnSpan(SecondaryButton, 1);

        switch (step)
        {
            case UpdateStep.Available:
                TitleText.Text = "Update available";
                SubtitleText.Text = "EyeRest Blink Twice";
                SecondaryButtonText.Text = "Remind me later";
                PrimaryButtonText.Text = "Download & install";
                FootnoteText.Text = "Your timers pause only while the app restarts.";
                FootnoteText.IsVisible = true;
                break;

            case UpdateStep.Downloading:
                TitleText.Text = $"Downloading {version}…";
                SubtitleText.Text = "You can keep working.";
                NotesBorder.IsVisible = false;
                VersionRow.IsVisible = false;
                FullNotesLink.IsVisible = false;
                ProgressPanel.IsVisible = true;
                DownloadBar.Value = 0;
                ProgressText.Text = "0%";
                PrimaryButton.IsVisible = false;
                SecondaryButtonText.Text = "Hide";
                Grid.SetColumnSpan(SecondaryButton, 3);
                break;

            case UpdateStep.Ready:
                TitleText.Text = $"{version} is ready";
                SubtitleText.Text = "Restart to finish updating.";
                SecondaryButtonText.Text = "Later";
                PrimaryButtonText.Text = "Restart now";
                FootnoteText.Text = "If you choose Later, restart any time from the About window.";
                FootnoteText.IsVisible = true;
                break;

            case UpdateStep.Failed:
                TitleText.Text = "Download failed";
                SubtitleText.Text = "Check your connection and try again.";
                SecondaryButtonText.Text = "Close";
                PrimaryButtonText.Text = "Try again";
                break;
        }
    }

    private async void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (_isWhatsNew) { Close(); return; }
        if (_updateService == null || _update == null) return;

        switch (_step)
        {
            case UpdateStep.Available:
            case UpdateStep.Failed:
                await DownloadAsync();
                break;
            case UpdateStep.Ready:
                _updateService.ApplyUpdateAndRestart();
                break;
        }
    }

    private async System.Threading.Tasks.Task DownloadAsync()
    {
        if (_updateService == null || _update == null) return;

        SetStep(UpdateStep.Downloading);
        var progress = new Progress<int>(percent =>
        {
            if (_closed) return;
            DownloadBar.Value = percent;
            ProgressText.Text = $"{percent}%";
        });

        try
        {
            await _updateService.DownloadUpdateAsync(progress);
            if (_closed)
                _onDownloadedWhileClosed?.Invoke(_update);
            else
                SetStep(UpdateStep.Ready);
        }
        catch (Exception)
        {
            if (!_closed) SetStep(UpdateStep.Failed);
        }
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e) => Close();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnFullNotesClick(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_fullNotesUrl) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort URL open
        }
    }

    private void AddEntry(ChangelogEntry entry)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = entry.Headline,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("NavButtonActiveText")
        });

        if (!string.IsNullOrEmpty(entry.Platform) || !string.IsNullOrEmpty(entry.Detail))
        {
            var detail = new TextBlock
            {
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("ContentSubtitleText")
            };
            if (!string.IsNullOrEmpty(entry.Platform))
            {
                detail.Inlines!.Add(new Run(entry.Platform.ToUpperInvariant() + "  ")
                {
                    FontSize = 10.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush("AccentBrush")
                });
            }
            detail.Inlines!.Add(new Run(entry.Detail));
            panel.Children.Add(detail);
        }

        EntriesPanel.Children.Add(panel);
    }

    private void AddNote(string text) => EntriesPanel.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 12.5,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush("ContentSubtitleText")
    });

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
}
