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

            if (File.Exists(destPath) && new FileInfo(destPath).Length > 0)
                return destPath;

            // The assembly name is "EyeRest" (set in EyeRest.UI.csproj <AssemblyName>),
            // NOT "EyeRest.UI". The avares:// URI must use the assembly name.
            var uri = new Uri($"avares://EyeRest/Assets/Sounds/{fileName}");
            using var src = AssetLoader.Open(uri);
            using var dst = File.Create(destPath);
            src.CopyTo(dst);
            return destPath;
        }
    }
}
