using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Platform;
using EyeRest.Services;

namespace EyeRest.UI.Services
{
    /// <summary>
    /// BL-002 M3: extracts bundled WAV resources (avares://) to a deterministic
    /// per-channel temp path so the platform audio engines (SoundPlayer on Windows,
    /// NSSound on macOS) can play them — neither accepts an avares:// URI nor a
    /// non-seekable resource stream. Extraction happens lazily on first GetPath
    /// per channel; subsequent calls return the cached path. The temp directory
    /// persists across app restarts (cache reuse) and is bounded to at most 5
    /// small WAV files (~250 KB total).
    /// </summary>
    public sealed class BundledSoundCache : IBundledSoundCache
    {
        private static readonly string CacheDir =
            Path.Combine(Path.GetTempPath(), "EyeRest", "sounds");

        private readonly ConcurrentDictionary<AudioChannel, string> _extracted = new();

        public BundledSoundCache()
        {
            Directory.CreateDirectory(CacheDir);
        }

        public string GetPath(AudioChannel channel)
            => _extracted.GetOrAdd(channel, Extract);

        private static string Extract(AudioChannel channel)
        {
            var fileName = channel switch
            {
                AudioChannel.EyeRestStart => "eye-rest-start.wav",
                AudioChannel.EyeRestEnd   => "eye-rest-end.wav",
                AudioChannel.BreakStart   => "break-start.wav",
                AudioChannel.BreakEnd     => "break-end.wav",
                // BreakWarning shares the break-start cue; no separate asset yet.
                AudioChannel.BreakWarning => "break-start.wav",
                _ => throw new ArgumentOutOfRangeException(nameof(channel)),
            };
            var destPath = Path.Combine(CacheDir, fileName);

            // Atomic rename makes destPath only-ever-fully-written. Existence is a
            // sufficient cache-hit check — no need for the weak Length > 0 gate.
            if (File.Exists(destPath))
                return destPath;

            // M3 Architect review found two concurrent-race hazards on the previous
            // direct File.Create(destPath) write:
            //   1. Intra-process: ConcurrentDictionary.GetOrAdd can invoke this factory
            //      concurrently for the same channel; loser threw IOException out to
            //      the popup-show path.
            //   2. Cross-process: documented dual-binary scenario (CLAUDE.md Mar 2026
            //      LaunchAgent stale-binary + dev binary) — two processes truncate +
            //      write the same path, racing readers see partial data.
            // Both fixes are the same atomic-rename pattern below: each writer uses a
            // unique tmp path so File.Create never collides; File.Move(overwrite:true)
            // is atomic on the same volume; deterministic WAV content makes concurrent
            // renames idempotent (whichever wins, the bytes are identical).
            //
            // Assembly name is "EyeRest" (EyeRest.UI.csproj <AssemblyName>), not "EyeRest.UI".
            var uri = new Uri($"avares://EyeRest/Assets/Sounds/{fileName}");
            var tmpPath = $"{destPath}.tmp.{Guid.NewGuid():N}";
            try
            {
                using (var src = AssetLoader.Open(uri))
                using (var dst = File.Create(tmpPath))
                {
                    src.CopyTo(dst);
                }
                File.Move(tmpPath, destPath, overwrite: true);
            }
            catch
            {
                try { File.Delete(tmpPath); } catch { /* best-effort tmp cleanup */ }
                throw;
            }
            return destPath;
        }
    }
}
