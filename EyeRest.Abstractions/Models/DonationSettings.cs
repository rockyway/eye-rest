using System;

namespace EyeRest.Models
{
    public class DonationSettings
    {
        public DateTime? LastPromptShownAt { get; set; }
        public DateTime? LastPromptDismissedAt { get; set; }
        public int PromptDismissCount { get; set; }
        public int SessionCount { get; set; }
        public long TotalUsageMinutes { get; set; }
        public DateTime? FirstInstallDate { get; set; }
        public string DonationUrl { get; set; } = "https://eyerest.net/#support";
    }
}
