using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Plays sound files on Linux by shelling out to the desktop audio tools that
    /// ship with every mainstream distribution:
    /// <list type="bullet">
    /// <item><c>paplay</c> — PulseAudio/PipeWire client, plays WAV/OGG/FLAC (libsndfile)</item>
    /// <item><c>aplay</c> — ALSA fallback, plays WAV</item>
    /// </list>
    /// Process-based playback is deliberate: it needs no native library binding
    /// (PortAudio's runtime soname is not P/Invoke-resolvable without the -dev
    /// symlink), works on both PulseAudio and PipeWire, and a crashed player can
    /// never take the app down. Playback is serialized upstream by
    /// <see cref="AudioServiceBase"/>'s semaphore gate.
    /// </summary>
    internal sealed class LinuxSoundPlayer
    {
        private readonly ILogger _logger;

        // Resolved lazily on first use; null entry means "not available on this system".
        private static readonly string[] FilePlayers = { "paplay", "aplay" };

        public LinuxSoundPlayer(ILogger logger) => _logger = logger;

        /// <summary>
        /// Plays <paramref name="filePath"/> synchronously. Honours <paramref name="ct"/>
        /// by killing the player process on cancellation. Tries paplay, then aplay.
        /// </summary>
        public void PlayFile(string filePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var player in FilePlayers)
            {
                if (RunPlayer(player, new[] { filePath }, ct)) return;
                ct.ThrowIfCancellationRequested();
            }

            _logger.LogWarning("No Linux audio player could play {File} (tried paplay, aplay)", filePath);
        }

        /// <summary>
        /// Plays a freedesktop sound-theme event synchronously: canberra-gtk-play
        /// (theme-aware), then the raw freedesktop .oga file via paplay.
        /// </summary>
        public void PlayThemeSound(string eventId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (RunPlayer("canberra-gtk-play", new[] { "-i", eventId }, ct)) return;
            ct.ThrowIfCancellationRequested();

            var ogaPath = $"/usr/share/sounds/freedesktop/stereo/{eventId}.oga";
            if (File.Exists(ogaPath) && RunPlayer("paplay", new[] { ogaPath }, ct)) return;

            _logger.LogWarning("Could not play theme sound '{EventId}' (canberra-gtk-play/paplay unavailable)", eventId);
        }

        /// <summary>Runs a player process to completion. Returns false if it could not start or exited non-zero.</summary>
        private bool RunPlayer(string fileName, string[] args, CancellationToken ct)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);

                using var process = Process.Start(psi);
                if (process is null) return false;

                using var registration = ct.Register(() =>
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
                });

                // Sound cues are short; 30s is a generous safety net against a hung player.
                if (!process.WaitForExit(30_000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
                    _logger.LogWarning("{Player} did not finish within 30s — killed", fileName);
                    return false;
                }

                if (process.ExitCode != 0 && !ct.IsCancellationRequested)
                {
                    var stderr = process.StandardError.ReadToEnd();
                    _logger.LogDebug("{Player} exited with {Code}: {Error}", fileName, process.ExitCode, stderr.Trim());
                    return false;
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Player binary not installed — try the next one.
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "{Player} playback failed", fileName);
                return false;
            }
        }
    }
}
