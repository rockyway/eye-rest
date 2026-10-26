using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EyeRest.Models;
using EyeRest.Services;

namespace EyeRest.UI.Services;

/// <summary>
/// Reads the release-notes files embedded in the app (docs/releases/release-notes-vX.Y.Z.md,
/// see EyeRest.UI.csproj) so the post-update popup works offline and in Store builds.
/// </summary>
public static class ChangelogProvider
{
    public const string ReleasesUrlPrefix = "https://github.com/rockyway/eye-rest/releases/tag/v";

    private const string ResourcePrefix = "ReleaseNotes.release-notes-v";

    /// <summary>
    /// Highlights for every embedded release newer than <paramref name="lastSeen"/> and up to
    /// <paramref name="current"/>, newest first. Releases without a Highlights section are skipped.
    /// </summary>
    public static List<ChangelogRelease> GetReleasesBetween(string? lastSeen, string current)
    {
        WhatsNewPolicy.TryParseVersion(current, out var currentVersion);
        var hasSeen = WhatsNewPolicy.TryParseVersion(lastSeen, out var seenVersion);

        var asm = Assembly.GetExecutingAssembly();
        var releases = new List<(Version Version, ChangelogRelease Release)>();

        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            var versionText = name[ResourcePrefix.Length..].Replace(".md", "", StringComparison.OrdinalIgnoreCase);
            if (!WhatsNewPolicy.TryParseVersion(versionText, out var version)) continue;
            if (version > currentVersion || (hasSeen && version <= seenVersion)) continue;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            var entries = ChangelogParser.ParseHighlights(reader.ReadToEnd());
            if (entries.Count == 0) continue;

            releases.Add((version, new ChangelogRelease { Version = versionText, Entries = entries }));
        }

        return releases.OrderByDescending(r => r.Version).Select(r => r.Release).ToList();
    }
}
