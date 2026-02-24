using System;
using System.Collections.Generic;

namespace EyeRest.Services
{
    /// <summary>
    /// Session state enumeration for enhanced activity tracking
    /// </summary>
    public enum SessionState
    {
        Active,      // User is actively using the computer
        Paused,      // Manually paused
        Idle,        // User idle but session unlocked (>5min)
        Away,        // Session locked or monitor off
        Sleep,       // System in sleep/hibernate mode
        Ended,       // Session completed
        Error        // Error state
    }

    /// <summary>
    /// Real-time session activity metrics
    /// </summary>
    public class SessionActivityMetrics
    {
        public int SessionId { get; set; }
        public DateTime SessionStartTime { get; set; }
        public SessionState CurrentState { get; set; }
        public TimeSpan TotalSessionTime { get; set; }
        public TimeSpan ActiveTime { get; set; }
        public TimeSpan InactiveTime { get; set; }
        public double ActivityRate { get; set; } // ActiveTime / TotalSessionTime

        public string FormattedActivitySummary =>
            $"Session {SessionId}: {CurrentState} | " +
            $"Total: {TotalSessionTime.TotalMinutes:F1}min | " +
            $"Active: {ActiveTime.TotalMinutes:F1}min ({ActivityRate:P0}) | " +
            $"Inactive: {InactiveTime.TotalMinutes:F1}min";
    }

    /// <summary>
    /// Session activity tracking validation result
    /// </summary>
    public class SessionActivityValidationResult
    {
        public DateTime ValidationTime { get; set; }
        public int SessionId { get; set; }
        public bool IsValid { get; set; }
        public List<string> ValidationMessages { get; set; } = new();

        public string FormattedReport =>
            $"Validation at {ValidationTime:HH:mm:ss} for Session {SessionId}: {(IsValid ? "✅ VALID" : "❌ INVALID")}\n" +
            string.Join("\n", ValidationMessages);
    }
}
