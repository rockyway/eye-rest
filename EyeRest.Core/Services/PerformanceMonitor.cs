using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class PerformanceMonitor : IPerformanceMonitor, IDisposable
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly Process _currentProcess;
        private readonly DateTime _startTime;
        private Timer? _monitoringTimer;
        private bool _disposed;

        // CPU% computation needs deltas of TotalProcessorTime against wall-clock,
        // normalized by ProcessorCount. We sample once at construction and again
        // on each tick — the difference is the CPU time consumed in the interval.
        private TimeSpan _lastCpuTime;
        private DateTime _lastCpuSampleAt;

        // Periodic process-stats log cadence. Short enough to surface drift quickly
        // during dev / triage, long enough to keep the log file size reasonable.
        private static readonly TimeSpan StatsInterval = TimeSpan.FromSeconds(15);

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
            _startTime = DateTime.Now;
            _lastCpuTime = _currentProcess.TotalProcessorTime;
            _lastCpuSampleAt = DateTime.UtcNow;

            // First sample at +15s so the CPU delta has a real interval to measure.
            _monitoringTimer = new Timer(MonitorPerformance, null, StatsInterval, StatsInterval);
        }

        public long GetMemoryUsageMB()
        {
            try
            {
                return (long)(GC.GetTotalMemory(false) / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory usage");
                return 0;
            }
        }

        public double GetCpuUsagePercent()
        {
            try
            {
                _currentProcess.Refresh();
                var nowCpu = _currentProcess.TotalProcessorTime;
                var nowAt = DateTime.UtcNow;

                var cpuDelta = nowCpu - _lastCpuTime;
                var wallDelta = nowAt - _lastCpuSampleAt;

                _lastCpuTime = nowCpu;
                _lastCpuSampleAt = nowAt;

                if (wallDelta.TotalMilliseconds <= 0) return 0;

                // Normalize by core count so a single fully-used core on an 8-core
                // box reads ~12.5%, not 100%.
                var cores = Math.Max(1, Environment.ProcessorCount);
                var pct = (cpuDelta.TotalMilliseconds / wallDelta.TotalMilliseconds) / cores * 100.0;
                return Math.Max(0, pct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU usage");
                return 0;
            }
        }

        public TimeSpan GetUptime()
        {
            return DateTime.Now - _startTime;
        }

        public void LogPerformanceMetrics()
        {
            try
            {
                _currentProcess.Refresh();
                var workingSetMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
                var gcHeapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024.0 * 1024.0);
                var cpuPercent = GetCpuUsagePercent();
                var g0 = GC.CollectionCount(0);
                var g1 = GC.CollectionCount(1);
                var g2 = GC.CollectionCount(2);

                _logger.LogDebug(
                    "Process stats: CPU={Cpu:F1}% | Mem={Mem:F1}MB | GCHeap={GcHeap:F1}MB | GC: g0={G0} g1={G1} g2={G2}",
                    cpuPercent, workingSetMb, gcHeapMb, g0, g1, g2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging performance metrics");
            }
        }

        private void MonitorPerformance(object? state)
        {
            try
            {
                LogPerformanceMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance monitoring");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _monitoringTimer?.Dispose();
                _currentProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing performance monitor");
            }
        }
    }
}
