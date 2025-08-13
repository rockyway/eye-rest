using System;

namespace EyeRest.Models
{
    public class ComplianceTrend
    {
        public DateTime Date { get; set; }
        public double ComplianceRate { get; set; }
        public TrendDirection Direction { get; set; }
        public double Change { get; set; }
    }

    public enum TrendDirection
    {
        Up,
        Down,
        Stable
    }
}