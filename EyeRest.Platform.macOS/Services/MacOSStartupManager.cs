using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IStartupManager"/> using LaunchAgents.
    /// Manages a plist file at ~/Library/LaunchAgents/ for auto-start on login.
    /// Ported from TextAssistant.Platform.macOS.
    /// </summary>
    public class MacOSStartupManager : IStartupManager
    {
        private readonly ILogger<MacOSStartupManager> _logger;
        private const string LaunchAgentLabel = "com.eyerest.app";
        private static readonly string PlistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", $"{LaunchAgentLabel}.plist");

        public MacOSStartupManager(ILogger<MacOSStartupManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsStartupEnabled()
        {
            try
            {
                var exists = File.Exists(PlistPath);
                _logger.LogDebug("Auto-start plist exists: {Exists} at {Path}", exists, PlistPath);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check auto-start status");
                return false;
            }
        }

        public void EnableStartup()
        {
            EnableStartup(startMinimized: false);
        }

        public void EnableStartup(bool startMinimized)
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for auto-start");
                    return;
                }

                // Ensure the LaunchAgents directory exists
                var launchAgentsDir = Path.GetDirectoryName(PlistPath)!;
                if (!Directory.Exists(launchAgentsDir))
                {
                    Directory.CreateDirectory(launchAgentsDir);
                }

                // Write the plist file
                var plistContent = GeneratePlist(executablePath, startMinimized);
                File.WriteAllText(PlistPath, plistContent);
                _logger.LogDebug("Wrote LaunchAgent plist to {Path}", PlistPath);

                // Load the agent via launchctl
                RunLaunchCtl("load", PlistPath);
                _logger.LogInformation("Auto-start enabled via LaunchAgent (minimized: {Minimized})", startMinimized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable auto-start");
            }
        }

        public void DisableStartup()
        {
            try
            {
                if (!File.Exists(PlistPath))
                {
                    _logger.LogDebug("Auto-start plist does not exist, nothing to disable");
                    return;
                }

                // Unload the agent via launchctl
                RunLaunchCtl("unload", PlistPath);

                // Delete the plist file
                File.Delete(PlistPath);
                _logger.LogInformation("Auto-start disabled, plist removed from {Path}", PlistPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable auto-start");
            }
        }

        #region Private Helpers

        private static string GetExecutablePath()
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        }

        private static string GeneratePlist(string executablePath, bool startMinimized)
        {
            // XML-encode the executable path to prevent plist XML injection
            var safeExecPath = System.Security.SecurityElement.Escape(executablePath);

            // Use ~/Library/Logs/EyeRest/ for log files
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "EyeRest");
            Directory.CreateDirectory(logDir);

            var stdoutLog = System.Security.SecurityElement.Escape(
                Path.Combine(logDir, $"{LaunchAgentLabel}.stdout.log"));
            var stderrLog = System.Security.SecurityElement.Escape(
                Path.Combine(logDir, $"{LaunchAgentLabel}.stderr.log"));

            var arguments = startMinimized
                ? $"""
                    <array>
                            <string>{safeExecPath}</string>
                            <string>--minimized</string>
                        </array>
                """
                : $"""
                    <array>
                            <string>{safeExecPath}</string>
                        </array>
                """;

            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{LaunchAgentLabel}</string>
                    <key>ProgramArguments</key>
                    {arguments}
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <false/>
                    <key>StandardOutPath</key>
                    <string>{stdoutLog}</string>
                    <key>StandardErrorPath</key>
                    <string>{stderrLog}</string>
                </dict>
                </plist>
                """;
        }

        private void RunLaunchCtl(string command, string plistPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/launchctl",
                    Arguments = $"{command} \"{plistPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process is null) return;

                process.WaitForExit(5000);
                var exitCode = process.ExitCode;

                if (exitCode != 0)
                {
                    var stderr = process.StandardError.ReadToEnd();
                    _logger.LogWarning("launchctl {Command} exited with code {ExitCode}: {Error}",
                        command, exitCode, stderr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run launchctl {Command}", command);
            }
        }

        #endregion
    }
}
