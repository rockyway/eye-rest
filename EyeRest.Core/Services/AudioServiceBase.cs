using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// BL-002: shared source-resolution + serialization layer for the platform-specific
    /// audio services. Owns:
    /// - The <c>PlayChannelAsync</c> entry point which dispatches on
    ///   <see cref="AudioChannelConfig.Source"/>.
    /// - A per-instance <see cref="SemaphoreSlim"/> so back-to-back calls never overlap
    ///   (a lifecycle hazard with native audio handles).
    /// - The missing-custom-file fallback (Source=File but file doesn't exist → fall
    ///   through to Default).
    ///
    /// Platform subclasses implement only the actual playback primitives
    /// (<see cref="PlayDefaultAsync"/> and <see cref="PlayFileAsync"/>).
    /// </summary>
    public abstract class AudioServiceBase : IAudioService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly IUrlOpener _urlOpener;
        private readonly IBundledSoundCache? _bundledSoundCache;

        protected AudioServiceBase(IUrlOpener urlOpener, IBundledSoundCache? bundledSoundCache = null)
        {
            _urlOpener = urlOpener ?? throw new ArgumentNullException(nameof(urlOpener));
            _bundledSoundCache = bundledSoundCache;
        }

        public abstract bool IsAudioEnabled { get; }

        public async Task PlayChannelAsync(
            AudioChannel channel,
            AudioChannelConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config is null) return;

            switch (config.Source)
            {
                case AudioChannelSource.Off:
                    return;

                case AudioChannelSource.Url:
                    // URL opens regardless of global audio mute — it's a user-action equivalent,
                    // not a sound effect (matches §3.3 of the design spec).
                    if (!string.IsNullOrWhiteSpace(config.Url))
                        _urlOpener.Open(config.Url);
                    return;

                case AudioChannelSource.File:
                    if (!IsAudioEnabled) return;
                    if (!string.IsNullOrWhiteSpace(config.CustomFilePath)
                        && File.Exists(config.CustomFilePath))
                    {
                        await GatedAsync(
                            () => PlayFileAsync(config.CustomFilePath!, cancellationToken),
                            cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    // Missing file → fall through to Default.
                    goto case AudioChannelSource.Default;

                case AudioChannelSource.Default:
                    if (!IsAudioEnabled) return;
                    // BL-002 M3: when a bundled-sound cache is wired, play the bundled
                    // WAV via the same file-playback primitive used for Source=File. The
                    // legacy PlayDefaultAsync (platform named sounds) remains as the
                    // fallback for setups where no cache is registered (tests, embedded).
                    if (_bundledSoundCache is not null)
                    {
                        var bundledPath = _bundledSoundCache.GetPath(channel);
                        await GatedAsync(
                            () => PlayFileAsync(bundledPath, cancellationToken),
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await GatedAsync(
                            () => PlayDefaultAsync(channel, cancellationToken),
                            cancellationToken).ConfigureAwait(false);
                    }
                    return;
            }
        }

        private async Task GatedAsync(Func<Task> work, CancellationToken ct)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            finally { _gate.Release(); }
        }

        protected abstract Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct);
        protected abstract Task PlayFileAsync(string filePath, CancellationToken ct);

        // Adapter overloads — platform implementations may delegate to PlayChannelAsync
        // using their own AppConfiguration channel-config reads.
        public abstract Task PlayEyeRestStartSound();
        public abstract Task PlayEyeRestEndSound();
        public abstract Task PlayBreakStartSound();
        public abstract Task PlayBreakEndSound();
        public abstract Task PlayBreakWarningSound();
        public abstract Task PlayCustomSoundTestAsync();
        public abstract Task TestEyeRestAudioAsync();
    }
}
