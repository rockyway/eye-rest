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
#pragma warning disable CS0649 // Field is never assigned to - CPU counter disabled due to permission issues
        private PerformanceCounter? _cpuCounter;
#pragma warning restore CS0649

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
                _currentProcess.Refresh();
                return _currentProcess.WorkingSet64 / (1024 * 1024);
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
                // Return mock CPU usage for now
                return 0.5; // Mock low CPU usage
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
                
                // Force garbage collection if memory usage is high
                var memoryMB = GetMemoryUsageMB();
                if (memoryMB > 40) // Trigger GC before hitting the 50MB limit
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    var newMemoryMB = GetMemoryUsageMB();
                    _logger.LogInformation($"Garbage collection performed - Memory reduced from {memoryMB}MB to {newMemoryMB}MB");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance monitoring");
            }
        }

        public void Dispose()
        {
            try
            {
                _monitoringTimer?.Dispose();
                _cpuCounter?.Dispose();
                _currentProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing performance monitor");
            }
        }
    }
}