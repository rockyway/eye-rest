using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace EyeRest.Platform.macOS.Services
{
    /// <summary>
    /// External-watchdog support for the memory-pressure freeze (see docs/plan/008).
    ///
    /// <para>The app cannot recover itself from a full OS-level suspension — when macOS freezes
    /// the process, even its threadpool timers stop. The only reliable recovery is an
    /// out-of-process watchdog. This helper (1) lets the app beat a heartbeat file while it is
    /// alive and scheduled, and (2) self-installs a tiny launchd agent that, every 60s, restarts
    /// the app ONLY if it is running but its heartbeat has gone stale (i.e. frozen). A cleanly
    /// quit app (no process / no heartbeat) is never relaunched.</para>
    /// </summary>
    internal static class MacOSWatchdog
    {
        private const string WatchdogLabel = "com.pmtlabs.eyerest.watchdog";
        private const string AppDisplayName = "Blink Twice EyeRest";
        private const string ProcessMatch = "BlinkTwiceEyeRest";
        private const int HeartbeatStaleSeconds = 180; // 6 missed 30s beats → frozen

        private static readonly string SupportDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EyeRest");

        public static readonly string HeartbeatPath = Path.Combine(SupportDir, "heartbeat");
        private static readonly string ScriptPath = Path.Combine(SupportDir, "watchdog.sh");

        private static readonly string PlistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents", $"{WatchdogLabel}.plist");

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Logs", "EyeRest", $"{WatchdogLabel}.log");

        /// <summary>Writes the current UTC unix time to the heartbeat file (updates mtime).</summary>
        public static void WriteHeartbeat()
        {
            try
            {
                Directory.CreateDirectory(SupportDir);
                File.WriteAllText(HeartbeatPath, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            }
            catch { /* best effort — a missed beat just delays watchdog detection */ }
        }

        /// <summary>Deletes the heartbeat file. Called on clean shutdown so the watchdog
        /// treats a subsequent absence-of-process as a deliberate quit, not a freeze.</summary>
        public static void DeleteHeartbeat()
        {
            try { if (File.Exists(HeartbeatPath)) File.Delete(HeartbeatPath); }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Writes/refreshes the watchdog script + launchd plist and (re)loads the agent.
        /// Idempotent. Skips dev builds so `dotnet run` never installs a watchdog that would
        /// fight the developer.
        /// </summary>
        public static void Install(ILogger logger)
        {
            try
            {
                var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (exe.Contains("/bin/Debug/") || exe.Contains("/bin/Release/"))
                {
                    logger.LogWarning("🐕 Watchdog install skipped — running from dev build path: {Path}", exe);
                    return;
                }

                Directory.CreateDirectory(SupportDir);
                Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                File.WriteAllText(ScriptPath, GenerateScript());
                if (OperatingSystem.IsMacOS()) // satisfies the platform analyzer; this service is macOS-only
                {
                    File.SetUnixFileMode(ScriptPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                File.WriteAllText(PlistPath, GeneratePlist());

                // Refresh the agent so plist/script edits take effect. unload-when-not-loaded
                // is a harmless no-op; tolerate its non-zero exit.
                RunLaunchCtl("unload", logger);
                RunLaunchCtl("load", logger);

                logger.LogInformation("🐕 Watchdog agent installed/refreshed at {Path}", PlistPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "🐕 Failed to install watchdog agent");
            }
        }

        private static string GenerateScript() =>
            $$"""
            #!/bin/bash
            # Blink Twice EyeRest watchdog — restarts the app only if it is RUNNING but FROZEN
            # (heartbeat stale). Does nothing if the app was quit cleanly. Generated and kept up
            # to date by the app itself; see docs/plan/008. Do not edit by hand.
            set -u
            HEARTBEAT="{{HeartbeatPath}}"
            STALE_SECONDS={{HeartbeatStaleSeconds}}

            [ -f "$HEARTBEAT" ] || exit 0
            NOW=$(date +%s)
            MTIME=$(stat -f %m "$HEARTBEAT" 2>/dev/null || echo "$NOW")
            AGE=$((NOW - MTIME))
            [ "$AGE" -lt "$STALE_SECONDS" ] && exit 0

            # Stale heartbeat. Only intervene if the process actually exists (frozen/suspended) —
            # never relaunch an app the user deliberately quit.
            PID=$(pgrep -f "{{ProcessMatch}}" | head -1)
            [ -n "$PID" ] || exit 0

            echo "$(date '+%Y-%m-%d %H:%M:%S') watchdog: heartbeat stale ${AGE}s, pid $PID frozen — restarting"
            /bin/kill -9 "$PID" 2>/dev/null
            sleep 2
            /usr/bin/open -a "{{AppDisplayName}}"

            """;

        private static string GeneratePlist()
        {
            var script = System.Security.SecurityElement.Escape(ScriptPath);
            var log = System.Security.SecurityElement.Escape(LogPath);
            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{WatchdogLabel}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>/bin/bash</string>
                        <string>{script}</string>
                    </array>
                    <key>StartInterval</key>
                    <integer>60</integer>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>StandardOutPath</key>
                    <string>{log}</string>
                    <key>StandardErrorPath</key>
                    <string>{log}</string>
                </dict>
                </plist>
                """;
        }

        private static void RunLaunchCtl(string command, ILogger logger)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/launchctl",
                    Arguments = $"{command} \"{PlistPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                if (process is null) return;
                process.WaitForExit(5000);
                // unload of a not-loaded agent returns non-zero — expected, don't log as error.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "🐕 launchctl {Command} failed", command);
            }
        }
    }
}
