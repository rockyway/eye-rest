using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Reads the "## Highlights" section of a release-notes markdown file.
    /// Entry format: <c>- [Windows] **Headline** — one-line detail</c> (the [Platform] tag is optional).
    /// The same section feeds both the pre-update and the post-update changelog popups.
    /// </summary>
    public static class ChangelogParser
    {
        private static readonly Regex EntryRegex = new(
            @"^\s*[-*]\s+(?:\[(?<plat>[^\]]+)\]\s+)?\*\*(?<head>.+?)\*\*\s*(?:[—–:-]\s*)?(?<detail>.*)$",
            RegexOptions.Compiled);

        public static List<ChangelogEntry> ParseHighlights(string? markdown)
        {
            var entries = new List<ChangelogEntry>();
            if (string.IsNullOrWhiteSpace(markdown)) return entries;

            var inSection = false;
            foreach (var raw in markdown.Split('\n'))
            {
                var line = raw.TrimEnd('\r');

                if (Regex.IsMatch(line, @"^##\s+Highlights\s*$", RegexOptions.IgnoreCase))
                {
                    inSection = true;
                    continue;
                }

                if (!inSection) continue;
                if (line.StartsWith("## ") || line.Trim() == "---") break;

                var m = EntryRegex.Match(line);
                if (!m.Success) continue;

                entries.Add(new ChangelogEntry
                {
                    Headline = m.Groups["head"].Value.Trim(),
                    Detail = m.Groups["detail"].Value.Trim(),
                    Platform = m.Groups["plat"].Success ? m.Groups["plat"].Value.Trim() : null
                });
            }

            return entries;
        }
    }

    public enum WhatsNewDecision
    {
        /// <summary>Nothing to show and nothing to save.</summary>
        None,
        /// <summary>Save the running version silently (fresh install, downgrade, dev build).</summary>
        RecordOnly,
        /// <summary>Show the popup and save the running version.</summary>
        Show
    }

    /// <summary>The show-once rule for the post-update "What's new" popup.</summary>
    public static class WhatsNewPolicy
    {
        public static WhatsNewDecision Decide(string? lastSeenVersion, string? currentVersion)
        {
            if (!TryParseVersion(currentVersion, out var current) || current == new Version(0, 0, 0))
                return WhatsNewDecision.None; // dev build or unknown version

            if (string.IsNullOrWhiteSpace(lastSeenVersion) || !TryParseVersion(lastSeenVersion, out var seen))
                return WhatsNewDecision.RecordOnly; // fresh install

            if (current > seen) return WhatsNewDecision.Show;
            if (current < seen) return WhatsNewDecision.RecordOnly; // downgrade
            return WhatsNewDecision.None;
        }

        /// <summary>Parses "1.6.1", "v1.6.1", "1.6.1-beta" and "1.6.1+sha" by their numeric core.</summary>
        public static bool TryParseVersion(string? text, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;

            var core = text.Trim().TrimStart('v', 'V');
            var cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) core = core[..cut];

            if (!Version.TryParse(core, out var parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            return true;
        }
    }
}
