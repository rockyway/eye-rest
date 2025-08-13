using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Windows-specific implementation of process monitoring for Teams-related processes
    /// </summary>
    public class WindowsProcessMonitor : IProcessMonitor
    {
        private readonly ILogger<WindowsProcessMonitor> _logger;
        private readonly Dictionary<string, TeamsVersion> _processVersionMap;

        public WindowsProcessMonitor(ILogger<WindowsProcessMonitor> logger)
        {
            _logger = logger;
            _processVersionMap = new Dictionary<string, TeamsVersion>(StringComparer.OrdinalIgnoreCase)
            {
                { "teams", TeamsVersion.Classic },
                { "ms-teams", TeamsVersion.NewClient },
                { "msteams", TeamsVersion.NewClient },
                { "teams2", TeamsVersion.NewClient },
                { "msteamsupdate", TeamsVersion.NewClient },
                { "msedgewebview2", TeamsVersion.WebView2 }
            };
        }

        #region Windows API Declarations

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        #endregion

        public async Task<List<TeamsProcess>> GetTeamsProcessesAsync()
        {
            var teamsProcesses = new List<TeamsProcess>();

            try
            {
                await Task.Run(() =>
                {
                    var allProcesses = Process.GetProcesses();
                    
                    foreach (var process in allProcesses)
                    {
                        try
                        {
                            if (process.HasExited) continue;

                            var processName = process.ProcessName.ToLowerInvariant();
                            
                            if (IsTeamsProcess(processName))
                            {
                                var windowTitle = GetWindowTitle(process.MainWindowHandle);
                                var version = GetTeamsVersion(processName);
                                var hasVisibleWindow = process.MainWindowHandle != IntPtr.Zero && 
                                                     IsWindowVisible(process.MainWindowHandle);

                                var teamsProcess = new TeamsProcess
                                {
                                    ProcessId = process.Id,
                                    ProcessName = process.ProcessName,
                                    WindowTitle = windowTitle,
                                    Version = version,
                                    HasVisibleWindow = hasVisibleWindow,
                                    DetectedAt = DateTime.Now
                                };

                                teamsProcesses.Add(teamsProcess);
                                
                                _logger.LogDebug($"Found Teams process: {teamsProcess}");
                                
                                // Special logging for WebView2 processes
                                if (version == TeamsVersion.WebView2 && !string.IsNullOrEmpty(windowTitle))
                                {
                                    if (windowTitle.Contains("Teams", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Meeting with", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogInformation($"🎯 WebView2 Teams process detected: {windowTitle}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip processes we can't access (permission issues, etc.)
                            _logger.LogTrace($"Could not analyze process {process.ProcessName}: {ex.Message}");
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                });

                _logger.LogDebug($"Found {teamsProcesses.Count} Teams-related processes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for Teams processes");
            }

            return teamsProcesses;
        }

        public bool IsTeamsProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            
            var lowerProcessName = processName.ToLowerInvariant();
            
            // Direct matches
            if (_processVersionMap.ContainsKey(lowerProcessName))
                return true;
            
            // Partial matches for Teams-related processes
            return lowerProcessName.Contains("teams") || 
                   lowerProcessName.Contains("msteams");
        }

        public TeamsVersion GetTeamsVersion(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return TeamsVersion.Unknown;
            
            var lowerProcessName = processName.ToLowerInvariant();
            
            if (_processVersionMap.TryGetValue(lowerProcessName, out var version))
                return version;
            
            // Fallback logic for unknown process names
            if (lowerProcessName.Contains("teams"))
            {
                if (lowerProcessName.Contains("ms-teams") || lowerProcessName.Contains("msteams"))
                    return TeamsVersion.NewClient;
                else
                    return TeamsVersion.Classic;
            }
            
            return TeamsVersion.Unknown;
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero) return string.Empty;
                
                var titleLength = GetWindowTextLength(windowHandle);
                if (titleLength == 0) return string.Empty;
                
                var title = new StringBuilder(titleLength + 1);
                GetWindowText(windowHandle, title, title.Capacity);
                
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}