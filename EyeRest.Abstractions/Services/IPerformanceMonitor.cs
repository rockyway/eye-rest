using System;

namespace EyeRest.Services
{
    public interface IPerformanceMonitor
    {
        long GetMemoryUsageMB();
        double GetCpuUsagePercent();
        TimeSpan GetUptime();
        void LogPerformanceMetrics();
    }
}