using System;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IScreenOverlayService"/>.
    /// Minimal implementation using screen count detection via AppKit.
    /// Full overlay rendering will be completed in Phase 6 when Avalonia windowing is integrated.
    /// </summary>
    public class MacOSScreenOverlayService : IScreenOverlayService
    {
        private readonly ILogger<MacOSScreenOverlayService> _logger;
        private bool _isOverlayVisible;

        public MacOSScreenOverlayService(ILogger<MacOSScreenOverlayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int ScreenCount
        {
            get
            {
                try
                {
                    return AppKit.GetScreenCount();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get screen count");
                    return 1;
                }
            }
        }

        public bool IsOverlayVisible => _isOverlayVisible;

        public event EventHandler<int>? OverlayClickedOnScreen;
        public event EventHandler? AllOverlaysClosed;

        public Task ShowOverlayAsync(double opacity = 0.5)
        {
            _logger.LogInformation(
                "ShowOverlayAsync called with opacity {Opacity} on {ScreenCount} screen(s). " +
                "Full implementation pending Phase 6 (Avalonia windowing).",
                opacity, ScreenCount);

            _isOverlayVisible = true;
            return Task.CompletedTask;
        }

        public Task HideOverlayAsync()
        {
            _logger.LogInformation("HideOverlayAsync called. Full implementation pending Phase 6.");

            _isOverlayVisible = false;
            AllOverlaysClosed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task HideOverlayOnScreenAsync(int screenIndex)
        {
            _logger.LogInformation(
                "HideOverlayOnScreenAsync called for screen {ScreenIndex}. Full implementation pending Phase 6.",
                screenIndex);

            OverlayClickedOnScreen?.Invoke(this, screenIndex);

            // If this was the last screen, mark all overlays as closed
            _isOverlayVisible = false;
            AllOverlaysClosed?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }
    }
}
