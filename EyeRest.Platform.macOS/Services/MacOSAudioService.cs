using System;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IAudioService"/> using NSSound and NSBeep.
    /// Uses named system sounds for different notification events.
    /// </summary>
    public class MacOSAudioService : IAudioService
    {
        private readonly ILogger<MacOSAudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private bool _cachedAudioEnabled = true;

        public MacOSAudioService(
            ILogger<MacOSAudioService> logger,
            IConfigurationService configurationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));

            // Load config async without blocking — default to enabled until loaded
            _ = RefreshAudioConfigAsync();
        }

        public bool IsAudioEnabled => _cachedAudioEnabled;

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

        public Task PlayEyeRestStartSound()
        {
            return PlaySoundAsync("Glass", "eye rest start");
        }

        public Task PlayEyeRestEndSound()
        {
            return PlaySoundAsync("Tink", "eye rest end");
        }

        public Task PlayBreakWarningSound()
        {
            return PlaySoundAsync("Blow", "break warning");
        }

        public Task PlayBreakStartSound()
        {
            return PlaySoundAsync("Submarine", "break start");
        }

        public Task PlayBreakEndSound()
        {
            return PlaySoundAsync("Tink", "break end");
        }

        public Task PlayCustomSoundTestAsync()
        {
            return PlaySoundAsync("Hero", "custom sound test");
        }

        public Task TestEyeRestAudioAsync()
        {
            return PlaySoundAsync("Glass", "eye rest audio test");
        }

        private async Task PlaySoundAsync(string soundName, string context)
        {
            // Refresh config each time without blocking the UI thread
            await RefreshAudioConfigAsync().ConfigureAwait(false);

            if (!_cachedAudioEnabled)
            {
                _logger.LogDebug("Audio disabled, skipping {Context} sound", context);
                return;
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
        }
    }
}
