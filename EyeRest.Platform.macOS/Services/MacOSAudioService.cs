using System;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IAudioService"/> using NSSound and NSBeep.
    /// Uses named system sounds for different notification events.
    ///
    /// BL-002 M2: inherits <see cref="AudioServiceBase"/> which provides the channel-aware
    /// <c>PlayChannelAsync</c> entry point, source-resolution dispatch, and per-instance
    /// SemaphoreSlim serialization. This class implements only the platform playback
    /// primitives plus the legacy Play*Sound adapter overloads.
    /// </summary>
    public class MacOSAudioService : AudioServiceBase
    {
        private readonly ILogger<MacOSAudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private bool _cachedAudioEnabled = true;

        public MacOSAudioService(
            ILogger<MacOSAudioService> logger,
            IConfigurationService configurationService,
            IUrlOpener urlOpener,
            IBundledSoundCache? bundledSoundCache = null)
            : base(urlOpener, bundledSoundCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            // Load config once at startup — default to enabled until loaded
            _ = RefreshAudioConfigAsync();

            // Subscribe to configuration changes to update cached audio settings
            _configurationService.ConfigurationChanged += OnConfigurationChanged;
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            _cachedAudioEnabled = e.NewConfiguration.Audio.Enabled;
        }

        public override bool IsAudioEnabled => _cachedAudioEnabled;

        private async Task RefreshAudioConfigAsync()
        {
            try
            {
                var config = await _configurationService.LoadConfigurationAsync().ConfigureAwait(false);
                _cachedAudioEnabled = config.Audio.Enabled;
            }
            catch
            {
                _cachedAudioEnabled = true;
            }
        }

        public override Task PlayEyeRestStartSound() => PlaySoundAsync("Glass", "eye rest start");
        public override Task PlayEyeRestEndSound()   => PlaySoundAsync("Tink", "eye rest end");
        public override Task PlayBreakWarningSound() => PlaySoundAsync("Blow", "break warning");
        public override Task PlayBreakStartSound()   => PlaySoundAsync("Submarine", "break start");
        public override Task PlayBreakEndSound()     => PlaySoundAsync("Tink", "break end");
        public override Task PlayCustomSoundTestAsync() => PlaySoundAsync("Hero", "custom sound test");
        public override Task TestEyeRestAudioAsync() => PlaySoundAsync("Glass", "eye rest audio test");

        // BL-002 M2: PlayDefaultAsync routes channel → existing NSSound-named-sound helpers.
        // M3 will introduce bundled WAVs and route Default through PlayFileAsync via BundledSoundCache.
        protected override Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return channel switch
            {
                AudioChannel.EyeRestStart => PlayEyeRestStartSound(),
                AudioChannel.EyeRestEnd   => PlayEyeRestEndSound(),
                AudioChannel.BreakStart   => PlayBreakStartSound(),
                AudioChannel.BreakEnd     => PlayBreakEndSound(),
                AudioChannel.BreakWarning => PlayBreakWarningSound(),
                _ => Task.CompletedTask,
            };
        }

        // BL-002 M2: WAV file playback on macOS via NSSound file-URL. NSSound.Play is
        // asynchronous on the native side; for M2 we kick it off and return — M3 will
        // tighten this with a completion callback / TaskCompletionSource. Disposal is
        // handled by the autorelease pool (NSSound is reference-counted by the runtime).
        protected override Task PlayFileAsync(string filePath, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    var played = AppKit.PlaySoundFromFile(filePath);
                    if (!played)
                    {
                        _logger.LogDebug("NSSound failed to play file {File}, NSBeep fallback", filePath);
                        AppKit.NSBeep();
                    }
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }, ct);
        }

        private Task PlaySoundAsync(string soundName, string context)
        {
            if (!_cachedAudioEnabled)
            {
                _logger.LogDebug("Audio disabled, skipping {Context} sound", context);
                return Task.CompletedTask;
            }

            try
            {
                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    var played = AppKit.PlaySystemSound(soundName);
                    if (!played)
                    {
                        _logger.LogDebug(
                            "Named sound '{SoundName}' not found, falling back to NSBeep for {Context}",
                            soundName, context);
                        AppKit.NSBeep();
                    }
                    else
                    {
                        _logger.LogDebug("Played '{SoundName}' for {Context}", soundName, context);
                    }
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play sound for {Context}", context);

                // Last resort: try NSBeep
                try
                {
                    AppKit.NSBeep();
                }
                catch (Exception beepEx)
                {
                    _logger.LogError(beepEx, "NSBeep also failed");
                }
            }

            return Task.CompletedTask;
        }
    }
}
