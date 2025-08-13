using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EyeRest.Models;

namespace EyeRest.Services
{
    public class ReportingService : IReportingService
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<ReportingService> _logger;

        public ReportingService(IAnalyticsService analyticsService, ILogger<ReportingService> logger)
        {
            _analyticsService = analyticsService;
            _logger = logger;
        }

        public async Task<string> GenerateHealthReportAsync(int days = 30)
        {
            try
            {
                var endDate = DateTime.Now.Date;
                var startDate = endDate.AddDays(-days);
                
                var healthMetrics = await _analyticsService.GetHealthMetricsAsync(startDate, endDate);
                
                var report = new StringBuilder();
                report.AppendLine("# EYE-REST HEALTH REPORT");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({days} days)");
                report.AppendLine();
                
                // Overview
                report.AppendLine("## OVERVIEW");
                report.AppendLine($"Overall Compliance Rate: {healthMetrics.ComplianceRate:P1}");
                report.AppendLine($"Total Active Time: {healthMetrics.TotalActiveTime.TotalHours:F1} hours");
                report.AppendLine($"Average Daily Active Time: {healthMetrics.TotalActiveTime.TotalHours / days:F1} hours");
                report.AppendLine();
                
                // Break Statistics
                report.AppendLine("## BREAK STATISTICS");
                report.AppendLine($"Total Breaks Due: {healthMetrics.TotalBreaksDue}");
                report.AppendLine($"Breaks Completed: {healthMetrics.BreaksCompleted} ({GetPercentage(healthMetrics.BreaksCompleted, healthMetrics.TotalBreaksDue)})");
                report.AppendLine($"Breaks Skipped: {healthMetrics.BreaksSkipped} ({GetPercentage(healthMetrics.BreaksSkipped, healthMetrics.TotalBreaksDue)})");
                report.AppendLine($"Breaks Delayed: {healthMetrics.BreaksDelayed} ({GetPercentage(healthMetrics.BreaksDelayed, healthMetrics.TotalBreaksDue)})");
                report.AppendLine($"Average Break Duration: {healthMetrics.AverageBreakDuration.TotalSeconds:F0} seconds");
                report.AppendLine();
                
                // Eye Rest Statistics
                report.AppendLine("## EYE REST STATISTICS");
                var totalEyeRests = healthMetrics.EyeRestsCompleted + healthMetrics.EyeRestsSkipped;
                report.AppendLine($"Eye Rests Completed: {healthMetrics.EyeRestsCompleted} ({GetPercentage(healthMetrics.EyeRestsCompleted, totalEyeRests)})");
                report.AppendLine($"Eye Rests Skipped: {healthMetrics.EyeRestsSkipped} ({GetPercentage(healthMetrics.EyeRestsSkipped, totalEyeRests)})");
                report.AppendLine();
                
                // Daily Breakdown
                if (healthMetrics.DailyBreakdown.Count > 0)
                {
                    report.AppendLine("## DAILY BREAKDOWN");
                    report.AppendLine("Date       | Breaks | Completed | Skipped | Compliance");
                    report.AppendLine("-----------|--------|-----------|---------|----------");
                    
                    foreach (var day in healthMetrics.DailyBreakdown)
                    {
                        report.AppendLine($"{day.Date:yyyy-MM-dd} | {day.BreaksDue,6} | {day.BreaksCompleted,9} | {day.BreaksSkipped,7} | {day.ComplianceRate,9:P1}");
                    }
                    report.AppendLine();
                }
                
                // Health Assessment
                report.AppendLine("## HEALTH ASSESSMENT");
                var assessment = GetHealthAssessment(healthMetrics);
                report.AppendLine($"Overall Health: {assessment.Level}");
                report.AppendLine($"Assessment: {assessment.Description}");
                
                if (assessment.Recommendations.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("### Recommendations:");
                    foreach (var recommendation in assessment.Recommendations)
                    {
                        report.AppendLine($"• {recommendation}");
                    }
                }
                
                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating health report");
                return $"Error generating health report: {ex.Message}";
            }
        }

        public async Task<string> GenerateComplianceReportAsync(int days = 30)
        {
            try
            {
                var complianceReport = await _analyticsService.GenerateComplianceReportAsync(days);
                
                var report = new StringBuilder();
                report.AppendLine("# COMPLIANCE REPORT");
                report.AppendLine($"Generated: {complianceReport.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Analysis Period: {days} days");
                report.AppendLine();
                
                // Compliance Summary
                report.AppendLine("## COMPLIANCE SUMMARY");
                report.AppendLine($"Overall Compliance Rate: {complianceReport.OverallComplianceRate:P1}");
                report.AppendLine($"Eye Rest Compliance: {complianceReport.EyeRestComplianceRate:P1}");
                report.AppendLine($"Break Compliance: {complianceReport.BreakComplianceRate:P1}");
                report.AppendLine();
                
                // Time Summary
                report.AppendLine("## TIME SUMMARY");
                report.AppendLine($"Total Active Time: {complianceReport.TotalActiveTime.TotalHours:F1} hours");
                report.AppendLine($"Total Break Time: {complianceReport.TotalBreakTime.TotalHours:F1} hours");
                report.AppendLine($"Break Time Percentage: {(complianceReport.TotalBreakTime.TotalMinutes / complianceReport.TotalActiveTime.TotalMinutes) * 100:F1}%");
                report.AppendLine();
                
                // Trends
                if (complianceReport.Trends.Count > 0)
                {
                    report.AppendLine("## COMPLIANCE TRENDS");
                    var improvingDays = 0;
                    var decliningDays = 0;
                    
                    foreach (var trend in complianceReport.Trends)
                    {
                        switch (trend.Direction)
                        {
                            case TrendDirection.Up: improvingDays++; break;
                            case TrendDirection.Down: decliningDays++; break;
                        }
                    }
                    
                    report.AppendLine($"Improving Days: {improvingDays}");
                    report.AppendLine($"Declining Days: {decliningDays}");
                    report.AppendLine($"Stable Days: {complianceReport.Trends.Count - improvingDays - decliningDays}");
                    report.AppendLine();
                }
                
                // Recommendations
                if (complianceReport.Recommendations.Count > 0)
                {
                    report.AppendLine("## RECOMMENDATIONS");
                    foreach (var recommendation in complianceReport.Recommendations)
                    {
                        report.AppendLine($"• {recommendation}");
                    }
                    report.AppendLine();
                }
                
                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report");
                return $"Error generating compliance report: {ex.Message}";
            }
        }

        public async Task<string> GenerateUsageReportAsync(int days = 30)
        {
            try
            {
                var endDate = DateTime.Now.Date;
                var startDate = endDate.AddDays(-days);
                
                var sessions = await _analyticsService.GetSessionSummariesAsync(startDate, endDate);
                var meetings = await _analyticsService.GetMeetingStatsAsync(startDate, endDate);
                
                var report = new StringBuilder();
                report.AppendLine("# USAGE REPORT");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({days} days)");
                report.AppendLine();
                
                // Session Statistics
                report.AppendLine("## SESSION STATISTICS");
                report.AppendLine($"Total Sessions: {sessions.Count}");
                
                if (sessions.Count > 0)
                {
                    var totalActiveTime = TimeSpan.Zero;
                    var totalIdleTime = TimeSpan.Zero;
                    var totalPresenceChanges = 0;
                    
                    foreach (var session in sessions)
                    {
                        totalActiveTime = totalActiveTime.Add(session.ActiveTime);
                        totalIdleTime = totalIdleTime.Add(session.IdleTime);
                        totalPresenceChanges += session.PresenceChanges;
                    }
                    
                    report.AppendLine($"Average Session Duration: {TimeSpan.FromTicks(sessions.Sum(s => s.Duration.Ticks) / sessions.Count):hh\\:mm\\:ss}");
                    report.AppendLine($"Total Active Time: {totalActiveTime.TotalHours:F1} hours");
                    report.AppendLine($"Total Idle Time: {totalIdleTime.TotalHours:F1} hours");
                    report.AppendLine($"Average Daily Active Time: {totalActiveTime.TotalHours / days:F1} hours");
                    report.AppendLine($"Total Presence Changes: {totalPresenceChanges}");
                    report.AppendLine($"Average Presence Changes per Session: {(double)totalPresenceChanges / sessions.Count:F1}");
                }
                report.AppendLine();
                
                // Meeting Statistics
                var totalMeetings = meetings.Sum(m => m.TotalMeetings);
                var totalMeetingTime = TimeSpan.FromTicks(meetings.Sum(m => m.TotalMeetingTime.Ticks));
                
                report.AppendLine("## MEETING STATISTICS");
                report.AppendLine($"Total Meetings: {totalMeetings}");
                report.AppendLine($"Total Meeting Time: {totalMeetingTime.TotalHours:F1} hours");
                report.AppendLine($"Average Daily Meetings: {(double)totalMeetings / days:F1}");
                report.AppendLine($"Average Meeting Duration: {(totalMeetings > 0 ? TimeSpan.FromTicks(totalMeetingTime.Ticks / totalMeetings) : TimeSpan.Zero):hh\\:mm\\:ss}");
                
                // Meeting breakdown by type
                var meetingsByType = new Dictionary<MeetingType, int>();
                foreach (var day in meetings)
                {
                    foreach (var (type, count) in day.MeetingsByType)
                    {
                        meetingsByType[type] = meetingsByType.GetValueOrDefault(type, 0) + count;
                    }
                }
                
                if (meetingsByType.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("### Meetings by Type:");
                    foreach (var (type, count) in meetingsByType.OrderByDescending(kvp => kvp.Value))
                    {
                        var percentage = totalMeetings > 0 ? (double)count / totalMeetings * 100 : 0;
                        report.AppendLine($"• {type}: {count} ({percentage:F1}%)");
                    }
                }
                
                var totalPausedCount = meetings.Sum(m => m.TimersPausedCount);
                report.AppendLine();
                report.AppendLine($"Timer Auto-Pauses: {totalPausedCount}");
                report.AppendLine($"Auto-Pause Rate: {(totalMeetings > 0 ? (double)totalPausedCount / totalMeetings * 100 : 0):F1}%");
                report.AppendLine();
                
                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating usage report");
                return $"Error generating usage report: {ex.Message}";
            }
        }

        public async Task<byte[]> GenerateChartImageAsync(ChartType chartType, DateTime startDate, DateTime endDate)
        {
            // Chart generation would require a charting library like OxyPlot or SkiaSharp
            // For now, return placeholder
            await Task.CompletedTask;
            throw new NotImplementedException("Chart generation not yet implemented");
        }

        public async Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _analyticsService.ExportDataAsync(ExportFormat.Csv, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to CSV");
                throw;
            }
        }

        public async Task<string> ExportToJsonAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _analyticsService.ExportDataAsync(ExportFormat.Json, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to JSON");
                throw;
            }
        }

        private string GetPercentage(int value, int total)
        {
            if (total == 0) return "0.0%";
            return $"{(double)value / total * 100:F1}%";
        }

        private HealthAssessment GetHealthAssessment(HealthMetrics metrics)
        {
            var assessment = new HealthAssessment();
            
            if (metrics.ComplianceRate >= 0.9)
            {
                assessment.Level = "Excellent";
                assessment.Description = "You're maintaining excellent break habits! Keep up the great work.";
            }
            else if (metrics.ComplianceRate >= 0.7)
            {
                assessment.Level = "Good";
                assessment.Description = "You're maintaining good break habits with room for improvement.";
                assessment.Recommendations.Add("Try to reduce the number of skipped breaks");
            }
            else if (metrics.ComplianceRate >= 0.5)
            {
                assessment.Level = "Fair";
                assessment.Description = "Your break compliance needs attention. Consider adjusting settings or workflow.";
                assessment.Recommendations.Add("Review your break timing settings");
                assessment.Recommendations.Add("Consider enabling auto-pause during meetings");
            }
            else
            {
                assessment.Level = "Poor";
                assessment.Description = "Your eye health may be at risk due to low break compliance.";
                assessment.Recommendations.Add("Consider shorter break intervals");
                assessment.Recommendations.Add("Enable all available break reminders");
                assessment.Recommendations.Add("Review your work schedule and break needs");
            }
            
            // Additional recommendations based on specific metrics
            if (metrics.AverageBreakDuration.TotalSeconds < 30)
            {
                assessment.Recommendations.Add("Consider taking longer breaks for better rest");
            }
            
            var eyeRestTotal = metrics.EyeRestsCompleted + metrics.EyeRestsSkipped;
            if (eyeRestTotal > 0 && (double)metrics.EyeRestsSkipped / eyeRestTotal > 0.3)
            {
                assessment.Recommendations.Add("Eye rest compliance is low - consider shorter intervals");
            }
            
            return assessment;
        }

        public async Task<string> ExportReportAsync(string reportType, ExportFormat format, int days = 30)
        {
            try
            {
                string reportContent = reportType.ToLower() switch
                {
                    "health" => await GenerateHealthReportAsync(days),
                    "compliance" => await GenerateComplianceReportAsync(days),
                    "usage" => await GenerateUsageReportAsync(days),
                    _ => throw new ArgumentException($"Unknown report type: {reportType}")
                };

                return format switch
                {
                    ExportFormat.Html => ConvertToHtml(reportContent),
                    ExportFormat.Csv => ConvertToCsv(reportContent),
                    ExportFormat.Json => ConvertToJson(reportContent),
                    ExportFormat.Pdf => throw new NotImplementedException("PDF export not yet implemented"),
                    _ => reportContent // Plain text
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report: {ReportType} as {Format}", reportType, format);
                throw;
            }
        }

        private string ConvertToHtml(string reportContent)
        {
            // Convert markdown-style report to HTML
            var html = reportContent
                .Replace("# ", "<h1>").Replace("\n## ", "</h1>\n<h2>").Replace("\n### ", "</h2>\n<h3>")
                .Replace("\n", "<br>\n");
            return $"<html><body>{html}</body></html>";
        }

        private string ConvertToCsv(string reportContent)
        {
            // Simple CSV conversion - would need more sophisticated parsing for production
            return reportContent.Replace("\n", "\r\n");
        }

        private string ConvertToJson(string reportContent)
        {
            // Simple JSON wrapping - would need proper object serialization for production
            return System.Text.Json.JsonSerializer.Serialize(new { content = reportContent, generated = DateTime.Now });
        }

        public void Dispose()
        {
            _logger.LogInformation("📊 ReportingService disposed successfully");
        }

        private class HealthAssessment
        {
            public string Level { get; set; } = "Unknown";
            public string Description { get; set; } = "";
            public List<string> Recommendations { get; set; } = new();
        }
    }
}