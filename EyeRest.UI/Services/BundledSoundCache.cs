using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
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

        public string? GetPath(AudioChannel channel)
        {
            // Eye-rest channels opt out of bundled WAVs — user feedback was that the
            // synthesized chimes sound less polished than the platform-native named
            // sounds (NSSound "Glass" / "Tink" on macOS). Returning null routes
            // AudioServiceBase to the legacy PlayDefaultAsync path for these channels.
            if (channel == AudioChannel.EyeRestStart || channel == AudioChannel.EyeRestEnd)
                return null;

            // Re-validate cached path on every call: macOS periodically purges /tmp
            // (system idle maintenance, even mid-session), which would leave us
            // handing a missing path to NSSound and producing silent failures.
            if (_extracted.TryGetValue(channel, out var cached) && File.Exists(cached))
                return cached;
            _extracted.TryRemove(channel, out _);
            return _extracted.GetOrAdd(channel, Extract);
        }

        private static string Extract(AudioChannel channel)
        {
            var fileName = channel switch
            {
                AudioChannel.BreakStart   => "break-start.wav",
                AudioChannel.BreakEnd     => "break-end.wav",
                // BreakWarning shares the break-start cue; no separate asset yet.
                AudioChannel.BreakWarning => "break-start.wav",
                _ => throw new ArgumentOutOfRangeException(nameof(channel),
                    $"No bundled asset registered for channel {channel}. "
                    + "GetPath should have returned null upstream."),
            };

            // Open the bundled stream up-front so we can version the destination
            // filename by a hash of its content. When a new build ships updated
            // WAV bytes the hash changes → the destPath changes → the stale
            // cached copy on disk is bypassed without needing a manual /tmp
            // wipe. A length-based version isn't enough: PCM amplitude edits
            // (e.g. -3 dB attenuation) keep the file length identical because
            // sample count and bit depth are unchanged, only sample values shift.
            var uri = new Uri($"avares://BlinkTwiceEyeRest/Assets/Sounds/{fileName}");
            using var src = AssetLoader.Open(uri);
            var bundledHash = ComputeShortHash(src);

            var stem = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var destPath = Path.Combine(CacheDir, $"{stem}-{bundledHash}{ext}");

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
            var tmpPath = $"{destPath}.tmp.{Guid.NewGuid():N}";
            try
            {
                if (src.CanSeek) src.Position = 0;
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

        /// <summary>
        /// 8-hex-char SHA256 prefix of the stream's content. 32 bits of entropy
        /// is overkill for distinguishing ~5 channels' worth of bundled assets
        /// but keeps the filename short. Caller is responsible for re-seeking
        /// the stream if it needs to be read again afterwards.
        /// </summary>
        private static string ComputeShortHash(Stream src)
        {
            if (src.CanSeek) src.Position = 0;
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(src);
            return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }
    }
}
