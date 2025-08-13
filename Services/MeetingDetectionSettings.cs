using System.Collections.Generic;

namespace EyeRest.Services
{
    public class MeetingDetectionSettings
    {
        public bool EnableTeamsDetection { get; set; } = true;
        public bool EnableZoomDetection { get; set; } = true;
        public bool EnableWebexDetection { get; set; } = true;
        public bool EnableGoogleMeetDetection { get; set; } = true;
        public bool EnableSkypeDetection { get; set; } = true;
        public List<string> CustomProcessNames { get; set; } = new List<string>();
        public List<string> ExcludedWindowTitles { get; set; } = new List<string>();
        public int MonitoringIntervalSeconds { get; set; } = 30;
    }

    public class MeetingDetectionPattern
    {
        public string[] ProcessNames { get; set; } = new string[0];
        public string[] WindowTitlePatterns { get; set; } = new string[0];
        public string[] CallIndicators { get; set; } = new string[0];
    }
}