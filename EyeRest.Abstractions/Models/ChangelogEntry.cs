using System.Collections.Generic;

namespace EyeRest.Models
{
    /// <summary>One highlight line from a release's "Highlights" section.</summary>
    public class ChangelogEntry
    {
        public string Headline { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;

        /// <summary>Optional OS tag such as "Windows"; null when the change applies everywhere.</summary>
        public string? Platform { get; set; }
    }

    /// <summary>The highlights of a single released version.</summary>
    public class ChangelogRelease
    {
        public string Version { get; set; } = string.Empty;
        public List<ChangelogEntry> Entries { get; set; } = new();
    }
}
