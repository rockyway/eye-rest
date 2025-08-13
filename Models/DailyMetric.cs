using System;

namespace EyeRest.Models
{
    public class DailyMetric
    {
        public DateTime Date { get; set; }
        public int BreaksDue { get; set; }
        public int BreaksCompleted { get; set; }
        public int BreaksSkipped { get; set; }
        public int BreaksDelayed { get; set; }
        public int EyeRestsDue { get; set; }
        public int EyeRestsCompleted { get; set; }
        public int EyeRestsSkipped { get; set; }
        public double ComplianceRate { get; set; }
        public TimeSpan ActiveTime { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
    }
}