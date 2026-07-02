using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="IStartupManager"/> using the XDG autostart
    /// specification. Manages a .desktop file at ~/.config/autostart/ which
    /// GNOME/Cinnamon/KDE/XFCE all honor at session login.
    /// </summary>
    public class LinuxStartupManager : IStartupManager
    {
        private readonly ILogger<LinuxStartupManager> _logger;
        private const string DesktopFileName = "com.pmtlabs.eyerest.desktop";

        // SpecialFolder.ApplicationData maps to $XDG_CONFIG_HOME (default ~/.config) on Linux.
        private static readonly string DesktopFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "autostart", DesktopFileName);

        public LinuxStartupManager(ILogger<LinuxStartupManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsStartupEnabled()
        {
            try
            {
                var exists = File.Exists(DesktopFilePath);
                _logger.LogDebug("Auto-start .desktop exists: {Exists} at {Path}", exists, DesktopFilePath);
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

                // Prevent dev builds from registering for autostart — same guard as macOS.
                // Dev builds (dotnet run) use bin/Debug or bin/Release paths and create
                // ghost processes that corrupt the shared config file.
                if (executablePath.Contains("/bin/Debug/") || executablePath.Contains("/bin/Release/"))
                {
                    _logger.LogWarning("Auto-start skipped — running from dev build path: {Path}", executablePath);
                    return;
                }

                var autostartDir = Path.GetDirectoryName(DesktopFilePath)!;
                if (!Directory.Exists(autostartDir))
                {
                    Directory.CreateDirectory(autostartDir);
                }

                File.WriteAllText(DesktopFilePath, GenerateDesktopEntry(executablePath, startMinimized));
                _logger.LogInformation("Auto-start enabled via XDG autostart at {Path} (minimized: {Minimized})",
                    DesktopFilePath, startMinimized);
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
                if (!File.Exists(DesktopFilePath))
                {
                    _logger.LogDebug("Auto-start .desktop does not exist, nothing to disable");
                    return;
                }

                File.Delete(DesktopFilePath);
                _logger.LogInformation("Auto-start disabled, .desktop removed from {Path}", DesktopFilePath);
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

        private static string GenerateDesktopEntry(string executablePath, bool startMinimized)
        {
            // Desktop Entry Exec quoting: wrap in double quotes and backslash-escape
            // the reserved characters (", `, $, \) per the freedesktop spec.
            var escaped = executablePath
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("`", "\\`")
                .Replace("$", "\\$");
            var exec = startMinimized ? $"\"{escaped}\" --minimized" : $"\"{escaped}\"";

            return $"""
                [Desktop Entry]
                Type=Application
                Name=Blink Twice EyeRest
                Comment=Automated eye rest and break reminders
                Exec={exec}
                Terminal=false
                X-GNOME-Autostart-enabled=true
                """;
        }

        #endregion
    }
}
