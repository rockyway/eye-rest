using System.Linq;
using EyeRest.Services;
using FluentAssertions;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    public class ChangelogParserTests
    {
        private const string Notes = @"# Title

## Highlights

- [Windows] **Wake fix** — windows draw after sleep.
- **Optional breaks** — turn breaks off.
- **No detail**

---

## Fixes & Improvements

- **Not a highlight** — ignored.
";

        [Fact]
        public void ParseHighlights_ReadsOnlyTheHighlightsSection()
        {
            var entries = ChangelogParser.ParseHighlights(Notes);

            entries.Should().HaveCount(3);
            entries[0].Platform.Should().Be("Windows");
            entries[0].Headline.Should().Be("Wake fix");
            entries[0].Detail.Should().Be("windows draw after sleep.");
            entries[1].Platform.Should().BeNull();
            entries[2].Detail.Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("## Fixes\n- **x** — y")]
        public void ParseHighlights_ReturnsEmpty_WhenNoSection(string? markdown)
        {
            ChangelogParser.ParseHighlights(markdown).Should().BeEmpty();
        }

        [Fact]
        public void ParseHighlights_HandlesCrLf()
        {
            ChangelogParser.ParseHighlights("## Highlights\r\n\r\n- **A** — b\r\n").Should().ContainSingle();
        }
    }

    public class WhatsNewPolicyTests
    {
        [Theory]
        [InlineData("1.6.0", "1.6.1", WhatsNewDecision.Show)]
        [InlineData("v1.6.0", "1.7.0+abc", WhatsNewDecision.Show)]
        [InlineData("1.6.1", "1.6.1", WhatsNewDecision.None)]
        [InlineData(null, "1.6.1", WhatsNewDecision.RecordOnly)]
        [InlineData("", "1.6.1", WhatsNewDecision.RecordOnly)]
        [InlineData("1.7.0", "1.6.1", WhatsNewDecision.RecordOnly)]
        [InlineData("1.6.0", "0.0.0", WhatsNewDecision.None)]
        [InlineData("1.6.0", "0.0.0-dev", WhatsNewDecision.None)]
        [InlineData("1.6.0", "garbage", WhatsNewDecision.None)]
        [InlineData("1.6.1-beta", "1.6.1", WhatsNewDecision.None)]
        public void Decide_FollowsTheShowOnceRule(string? lastSeen, string? current, WhatsNewDecision expected)
        {
            WhatsNewPolicy.Decide(lastSeen, current).Should().Be(expected);
        }
    }
}

namespace EyeRest.Tests.Avalonia.Services
{
    public class ChangelogProviderTests
    {
        [Fact]
        public void GetReleasesBetween_ReturnsEmbeddedReleasesNewerThanLastSeen()
        {
            var releases = EyeRest.UI.Services.ChangelogProvider.GetReleasesBetween("1.6.0", "1.6.1");

            releases.Should().ContainSingle().Which.Version.Should().Be("1.6.1");
            releases[0].Entries.Should().NotBeEmpty();
        }

        [Fact]
        public void GetReleasesBetween_ReturnsNewestFirst_WhenSeveralAreMissed()
        {
            var releases = EyeRest.UI.Services.ChangelogProvider.GetReleasesBetween("1.5.0", "1.6.1");

            releases.Select(r => r.Version).Should().Equal("1.6.1", "1.6.0");
        }
    }
}
