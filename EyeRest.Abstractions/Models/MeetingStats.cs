using System;
using System.Collections.Generic;
using EyeRest.Services;

namespace EyeRest.Models
{
    public class MeetingStats
    {
        public DateTime Date { get; set; }
        public int TotalMeetings { get; set; }
        public TimeSpan TotalMeetingTime { get; set; }
        public int TimersPausedCount { get; set; }
        public Dictionary<MeetingType, int> MeetingsByType { get; set; } = new();
    }
}