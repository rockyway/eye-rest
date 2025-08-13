using System;

namespace EyeRest.Models
{
    public class SessionSummary
    {
        public DateTime Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan ActiveTime { get; set; }
        public TimeSpan IdleTime { get; set; }
        public int PresenceChanges { get; set; }
        public int BreaksCompleted { get; set; }
        public int BreaksSkipped { get; set; }
        public int EyeRestsCompleted { get; set; }
        public int EyeRestsSkipped { get; set; }
    }
}