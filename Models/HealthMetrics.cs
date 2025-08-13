using System;
using System.Collections.Generic;

namespace EyeRest.Models
{
    public class HealthMetrics
    {
        public double ComplianceRate { get; set; }
        public int TotalBreaksDue { get; set; }
        public int BreaksCompleted { get; set; }
        public int BreaksSkipped { get; set; }
        public int BreaksDelayed { get; set; }
        public int EyeRestsSkipped { get; set; }
        public int EyeRestsCompleted { get; set; }
        public TimeSpan AverageBreakDuration { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public List<DailyMetric> DailyBreakdown { get; set; } = new List<DailyMetric>();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }
}