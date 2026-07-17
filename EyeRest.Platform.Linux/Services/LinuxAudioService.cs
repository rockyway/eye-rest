using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="IAudioService"/> using desktop audio tools
    /// (paplay/aplay for files, canberra/freedesktop theme sounds for defaults).
    ///
    /// Inherits <see cref="AudioServiceBase"/> which provides the channel-aware
    /// <c>PlayChannelAsync</c> entry point, source-resolution dispatch, and per-instance
    /// SemaphoreSlim serialization. This class implements only the platform playback
    /// primitives plus the legacy Play*Sound adapter overloads — the same structure
    /// as <c>MacOSAudioService</c>, with freedesktop sound-theme events standing in
    /// for the macOS named system sounds.
    /// </summary>
    public class LinuxAudioService : AudioServiceBase
    {
        private readonly ILogger<LinuxAudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly LinuxSoundPlayer _player;
        private bool _cachedAudioEnabled = true;

        public LinuxAudioService(
            ILogger<LinuxAudioService> logger,
            IConfigurationService configurationService,
            IUrlOpener urlOpener,
            IBundledSoundCache? bundledSoundCache = null)
            : base(urlOpener, bundledSoundCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _player = new LinuxSoundPlayer(logger);

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

        // Freedesktop sound-naming-spec event ids play the role of the macOS
        // named sounds (Glass/Tink/Blow/Submarine/Hero).
        public override Task PlayEyeRestStartSound() => PlayThemeSoundAsync("message", "eye rest start");
        public override Task PlayEyeRestEndSound()   => PlayThemeSoundAsync("complete", "eye rest end");
        public override Task PlayBreakWarningSound() => PlayThemeSoundAsync("dialog-warning", "break warning");
        public override Task PlayBreakStartSound()   => PlayThemeSoundAsync("bell", "break start");
        public override Task PlayBreakEndSound()     => PlayThemeSoundAsync("complete", "break end");
        public override Task PlayCustomSoundTestAsync() => PlayThemeSoundAsync("dialog-information", "custom sound test");
        public override Task TestEyeRestAudioAsync() => PlayThemeSoundAsync("message", "eye rest audio test");

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

        // Sound file playback (custom per-channel files and bundled WAVs from
        // BundledSoundCache). The player call is synchronous, so Task.Run honors
        // the async/cancellable contract.
        protected override Task PlayFileAsync(string filePath, CancellationToken ct)
        {
            return Task.Run(() => _player.PlayFile(filePath, ct), ct);
        }

        private Task PlayThemeSoundAsync(string eventId, string context)
        {
            if (!_cachedAudioEnabled)
            {
                _logger.LogDebug("Audio disabled, skipping {Context} sound", context);
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                try
                {
                    _player.PlayThemeSound(eventId, CancellationToken.None);
                    _logger.LogDebug("Played '{EventId}' for {Context}", eventId, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to play sound for {Context}", context);
                }
            });
        }
    }
}
