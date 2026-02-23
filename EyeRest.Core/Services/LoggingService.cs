using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class LoggingService : ILoggingService, IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public LoggingService()
        {
            // Create logs directory in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logDirectory = Path.Combine(appDataPath, "EyeRest", "logs");
            Directory.CreateDirectory(_logDirectory);

            // Create daily log file
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(_logDirectory, $"eyerest-{today}.log");

            // Clean up old log files (keep last 30 days)
            CleanupOldLogs();
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var fullMessage = exception != null 
                ? $"{message} - Exception: {exception}" 
                : message;
            WriteLog("ERROR", fullMessage);
        }

        private void WriteLog(string level, string message)
        {
            if (_disposed) return;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}";

                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-30);
                var logFiles = Directory.GetFiles(_logDirectory, "eyerest-*.log");

                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(logFile);
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}