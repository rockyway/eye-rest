using System;
using System.Collections.Generic;
using EyeRest.Services;

namespace EyeRest.Models
{
    public class PresenceAnalytic
    {
        public DateTime Date { get; set; }
        public int PresenceChanges { get; set; }
        public TimeSpan TotalAwayTime { get; set; }
        public TimeSpan TotalIdleTime { get; set; }
        public TimeSpan TotalPresentTime { get; set; }
    }

    public class MeetingAnalytic
    {
        public DateTime Date { get; set; }
        public int TotalMeetings { get; set; }
        public TimeSpan TotalMeetingTime { get; set; }
        public Dictionary<MeetingType, int> MeetingsByType { get; set; } = new Dictionary<MeetingType, int>();
        public int TimersPausedCount { get; set; }
    }

    /// <summary>
    /// Weekly aggregated analytics metrics
    /// </summary>
    public class WeeklyMetrics
    {
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public int WeekNumber { get; set; }
        public int Year { get; set; }
        public int DaysActive { get; set; }
        public int TotalBreaks { get; set; }
        public int CompletedBreaks { get; set; }
        public int SkippedBreaks { get; set; }
        public int DelayedBreaks { get; set; }
        public int TotalEyeRests { get; set; }
        public int CompletedEyeRests { get; set; }
        public int SkippedEyeRests { get; set; }
        public double ComplianceRate { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public TimeSpan AverageBreakTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
        
        // Formatted display properties
        public string WeekText => $"Week {WeekNumber}, {Year} ({WeekStartDate:MMM dd} - {WeekEndDate:MMM dd})";
        public string ComplianceRateText => $"{ComplianceRate:P0}";
        public string AverageBreakTimeText => $"{AverageBreakTime.TotalMinutes:F1}min";
        public string TotalActiveTimeText => $"{TotalActiveTime.TotalHours:F1}h";
        public string ComplianceStatusColor => ComplianceRate >= 0.8 ? "#4CAF50" : ComplianceRate >= 0.6 ? "#FFC107" : "#F44336";
    }

    /// <summary>
    /// Monthly aggregated analytics metrics
    /// </summary>
    public class MonthlyMetrics
    {
        public DateTime MonthStartDate { get; set; }
        public DateTime MonthEndDate { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public int DaysActive { get; set; }
        public int TotalBreaks { get; set; }
        public int CompletedBreaks { get; set; }
        public int SkippedBreaks { get; set; }
        public int DelayedBreaks { get; set; }
        public int TotalEyeRests { get; set; }
        public int CompletedEyeRests { get; set; }
        public int SkippedEyeRests { get; set; }
        public double ComplianceRate { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public TimeSpan AverageBreakTime { get; set; }
        public TimeSpan TotalBreakTime { get; set; }
        public int WeeksActive { get; set; }
        
        // Formatted display properties
        public string MonthText => $"{MonthStartDate:MMMM yyyy}";
        public string ComplianceRateText => $"{ComplianceRate:P0}";
        public string AverageBreakTimeText => $"{AverageBreakTime.TotalMinutes:F1}min";
        public string TotalActiveTimeText => $"{TotalActiveTime.TotalHours:F1}h";
        public string ComplianceStatusColor => ComplianceRate >= 0.8 ? "#4CAF50" : ComplianceRate >= 0.6 ? "#FFC107" : "#F44336";
    }
}