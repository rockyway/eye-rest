using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="IScreenDimmingService"/>.
    /// Hardware brightness control has no universal API across Linux display
    /// stacks (X11/Wayland, laptop backlight vs external DDC monitors), so this
    /// reports unsupported and no-ops — the same graceful degradation the macOS
    /// implementation exhibits on displays without IOKit brightness controls.
    /// Break-time dimming is provided by the Avalonia window overlays instead.
    /// </summary>
    public class LinuxScreenDimmingService : IScreenDimmingService
    {
        private readonly ILogger<LinuxScreenDimmingService> _logger;

        public LinuxScreenDimmingService(ILogger<LinuxScreenDimmingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsSupported => false;

        public Task DimScreensAsync(int brightnessPercent)
        {
            _logger.LogDebug("DimScreensAsync({Brightness}%) skipped — hardware dimming not supported on Linux", brightnessPercent);
            return Task.CompletedTask;
        }

        public Task RestoreScreenBrightnessAsync()
        {
            _logger.LogDebug("RestoreScreenBrightnessAsync skipped — hardware dimming not supported on Linux");
            return Task.CompletedTask;
        }

        public Task<int> GetCurrentBrightnessAsync()
        {
            return Task.FromResult(100);
        }
    }
}
