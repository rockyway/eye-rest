using System.Text.Json;
using EyeRest.Models;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Avalonia.Models
{
    public class PopupDisplayModeConfigTests
    {
        [Fact]
        public void Defaults_PreserveExistingBehavior()
        {
            var c = new AppConfiguration();
            c.EyeRest.DisplayMode.Should().Be(PopupDisplayMode.Standard);
            c.Break.DisplayMode.Should().Be(PopupDisplayMode.Standard);
            c.Break.OverlayEnabled.Should().BeTrue();
        }

        [Fact]
        public void RoundTrips_ThroughJson()
        {
            var c = new AppConfiguration();
            c.EyeRest.DisplayMode = PopupDisplayMode.Minimal;
            c.Break.DisplayMode = PopupDisplayMode.Compact;
            c.Break.OverlayEnabled = false;

            var back = JsonSerializer.Deserialize<AppConfiguration>(JsonSerializer.Serialize(c))!;
            back.EyeRest.DisplayMode.Should().Be(PopupDisplayMode.Minimal);
            back.Break.DisplayMode.Should().Be(PopupDisplayMode.Compact);
            back.Break.OverlayEnabled.Should().BeFalse();
        }

        [Fact]
        public void OldConfigWithoutNewKeys_GetsDefaults()
        {
            var c = JsonSerializer.Deserialize<AppConfiguration>("{\"Break\":{\"IntervalMinutes\":40}}")!;
            c.Break.IntervalMinutes.Should().Be(40);
            c.Break.DisplayMode.Should().Be(PopupDisplayMode.Standard);
            c.Break.OverlayEnabled.Should().BeTrue();
        }
    }
}
