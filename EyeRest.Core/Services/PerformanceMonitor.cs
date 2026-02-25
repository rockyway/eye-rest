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

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger;
            _currentProcess = Process.GetCurrentProcess();
            _startTime = DateTime.Now;

            // Disable CPU counter for now to avoid permission issues
            // try
            // {
            //     _cpuCounter = new PerformanceCounter("Process", "% Processor Time", _currentProcess.ProcessName);
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogWarning(ex, "Could not initialize CPU performance counter");
            // }

            // Start periodic monitoring (every 5 minutes)
            _monitoringTimer = new Timer(MonitorPerformance, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
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
                // CPU monitoring not implemented — returns 0 to avoid
                // PerformanceCounter permission issues on macOS/Linux.
                return 0;
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
            var memoryMB = GetMemoryUsageMB();
            var cpuPercent = GetCpuUsagePercent();
            var uptime = GetUptime();

            _logger.LogInformation($"Performance Metrics - Memory: {memoryMB}MB, CPU: {cpuPercent:F1}%, Uptime: {uptime:hh\\:mm\\:ss}");

            // Log warning if memory usage exceeds 50MB requirement
            if (memoryMB > 50)
            {
                _logger.LogWarning($"Memory usage ({memoryMB}MB) exceeds 50MB requirement");
            }

            // Log warning if CPU usage is consistently high
            if (cpuPercent > 5)
            {
                _logger.LogWarning($"CPU usage ({cpuPercent:F1}%) is higher than expected for idle state");
            }
        }

        private void MonitorPerformance(object? state)
        {
            try
            {
                LogPerformanceMetrics();
                
                // Request non-blocking GC if memory usage is high
                var memoryMB = GetMemoryUsageMB();
                if (memoryMB > 40) // Trigger GC before hitting the 50MB limit
                {
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

                    var newMemoryMB = GetMemoryUsageMB();
                    _logger.LogInformation($"Garbage collection requested - Memory: {memoryMB}MB -> {newMemoryMB}MB");
                }
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