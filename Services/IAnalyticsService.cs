using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services; // For SessionActivityMetrics and SessionState

namespace EyeRest.Services
{
    public interface IAnalyticsService : IDisposable
    {
        Task<bool> IsDatabaseInitializedAsync();
        Task InitializeDatabaseAsync();
        
        Task RecordSessionStartAsync();
        Task RecordSessionEndAsync();
        
        // NEW: Enhanced session activity tracking methods
        Task PauseSessionAsync(UserPresenceState awayState, string reason = "");
        Task ResumeSessionAsync(string reason = "");
        SessionActivityMetrics GetCurrentSessionMetrics();
        SessionActivityValidationResult ValidateSessionTracking();
        
        Task RecordEyeRestEventAsync(RestEventType type, UserAction action, TimeSpan duration);
        Task RecordBreakEventAsync(RestEventType type, UserAction action, TimeSpan duration);
        Task RecordPresenceChangeAsync(UserPresenceState oldState, UserPresenceState newState, TimeSpan idleDuration);
        Task RecordMeetingEventAsync(MeetingApplication meeting, bool timersPaused);
        Task RecordPauseEventAsync(PauseReason reason);
        Task RecordResumeEventAsync(ResumeReason reason);
        
        Task<HealthMetrics> GetHealthMetricsAsync(DateTime startDate, DateTime endDate);
        Task<ComplianceReport> GenerateComplianceReportAsync(int days = 30);
        Task<string> ExportDataAsync(ExportFormat format, DateTime? startDate = null, DateTime? endDate = null);
        Task<List<SessionSummary>> GetSessionSummariesAsync(DateTime startDate, DateTime endDate);
        Task<List<MeetingStats>> GetMeetingStatsAsync(DateTime startDate, DateTime endDate);
        
        // Weekly and Monthly Analytics
        Task<List<WeeklyMetrics>> GetWeeklyMetricsAsync(DateTime startDate, DateTime endDate);
        Task<List<MonthlyMetrics>> GetMonthlyMetricsAsync(DateTime startDate, DateTime endDate);
        Task<long> GetDatabaseSizeAsync();
        string GetDatabasePath();
        
        Task CleanupOldDataAsync(int retentionDays);
        Task DeleteAllDataAsync();
    }
}