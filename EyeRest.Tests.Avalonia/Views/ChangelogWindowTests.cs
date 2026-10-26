using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.UI.Views;
using FluentAssertions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Views
{
    public class ChangelogWindowTests
    {
        private static T Find<T>(Window w, string name) where T : Control => w.FindControl<T>(name)!;

        [AvaloniaFact]
        public void ShowUpdate_ListsHighlightsAndOffersDownload()
        {
            var service = new Mock<IUpdateService>();
            service.SetupGet(s => s.CurrentVersion).Returns("1.6.0");
            var update = new AppUpdateInfo
            {
                TargetVersion = "1.6.1",
                ReleaseNotesMarkdown = "## Highlights\n\n- **One** — a.\n- [Windows] **Two** — b.\n"
            };

            var window = new ChangelogWindow();
            window.ShowUpdate(service.Object, update);
            window.Show();

            Find<StackPanel>(window, "EntriesPanel").Children.Should().HaveCount(2);
            Find<TextBlock>(window, "TitleText").Text.Should().Be("Update available");
            Find<TextBlock>(window, "PrimaryButtonText").Text.Should().Be("Download & install");
            Find<TextBlock>(window, "FromVersionText").Text.Should().Be("1.6.0");
            Find<TextBlock>(window, "ToVersionText").Text.Should().Be("1.6.1");
            window.Close();
        }

        [AvaloniaFact]
        public void ShowUpdate_OpensOnReady_WhenAlreadyDownloaded()
        {
            var service = new Mock<IUpdateService>();
            service.SetupGet(s => s.CurrentVersion).Returns("1.6.0");
            var update = new AppUpdateInfo { TargetVersion = "1.6.1", IsDownloaded = true };

            var window = new ChangelogWindow();
            window.ShowUpdate(service.Object, update);
            window.Show();

            Find<TextBlock>(window, "PrimaryButtonText").Text.Should().Be("Restart now");
            // No highlights in the feed: a note points to the full release notes instead.
            Find<StackPanel>(window, "EntriesPanel").Children.Should().HaveCount(1);
            window.Close();
        }

        [AvaloniaFact]
        public void ShowWhatsNew_GroupsReleasesAndHidesSecondaryButton()
        {
            var releases = new List<ChangelogRelease>
            {
                new() { Version = "1.6.1", Entries = new() { new ChangelogEntry { Headline = "A" } } },
                new() { Version = "1.6.0", Entries = new() { new ChangelogEntry { Headline = "B" } } }
            };

            var window = new ChangelogWindow();
            window.ShowWhatsNew(releases, "1.6.1");
            window.Show();

            // two version headers + two entries
            Find<StackPanel>(window, "EntriesPanel").Children.Should().HaveCount(4);
            Find<TextBlock>(window, "TitleText").Text.Should().Be("Updated to 1.6.1");
            Find<Button>(window, "SecondaryButton").IsVisible.Should().BeFalse();
            Find<TextBlock>(window, "PrimaryButtonText").Text.Should().Be("Got it");
            window.Close();
        }
    }
}
