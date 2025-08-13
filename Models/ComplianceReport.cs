using System;
using System.Collections.Generic;
using EyeRest.Services;

namespace EyeRest.Models
{
    public class ComplianceReport
    {
        public HealthMetrics OverallMetrics { get; set; } = new HealthMetrics();
        public List<DailyMetric> DailyMetrics { get; set; } = new List<DailyMetric>();
        public List<PresenceAnalytic> PresencePatterns { get; set; } = new List<PresenceAnalytic>();
        public List<MeetingAnalytic> MeetingPatterns { get; set; } = new List<MeetingAnalytic>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<ComplianceTrend> Trends { get; set; } = new List<ComplianceTrend>();
        public DateTime GeneratedAt { get; set; }
        public string ReportPeriod { get; set; } = string.Empty;
        public int DaysAnalyzed { get; set; }
        public double OverallComplianceRate { get; set; }
        public double EyeRestComplianceRate { get; set; }
        public double BreakComplianceRate { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
    }


}