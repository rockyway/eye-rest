using System;

namespace EyeRest.Services
{
    public class MeetingApplication
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public MeetingType Type { get; set; }
        public int ProcessId { get; set; }
        public bool IsInCall { get; set; }
        public string MeetingId { get; set; } = string.Empty;
        public string ApplicationPath { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public enum MeetingType
    {
        Teams,
        Zoom,
        Webex,
        GoogleMeet,
        Skype,
        Unknown
    }
}