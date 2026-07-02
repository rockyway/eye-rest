using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="IScreenOverlayService"/>.
    /// Minimal implementation mirroring macOS — the actual break dim overlays are
    /// rendered by the Avalonia layer (<c>AvaloniaNotificationService</c>), so this
    /// service only tracks visibility state and raises the contract events.
    /// </summary>
    public class LinuxScreenOverlayService : IScreenOverlayService
    {
        private readonly ILogger<LinuxScreenOverlayService> _logger;
        private bool _isOverlayVisible;

        public LinuxScreenOverlayService(ILogger<LinuxScreenOverlayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Screen enumeration lives in the Avalonia layer on Linux; a single logical
        // screen is reported here, matching how the overlay service is consumed.
        public int ScreenCount => 1;

        public bool IsOverlayVisible => _isOverlayVisible;

        public event EventHandler<int>? OverlayClickedOnScreen;
        public event EventHandler? AllOverlaysClosed;

        public Task ShowOverlayAsync(double opacity = 0.5)
        {
            _logger.LogInformation(
                "ShowOverlayAsync called with opacity {Opacity} (overlays rendered by Avalonia layer)", opacity);

            _isOverlayVisible = true;
            return Task.CompletedTask;
        }

        public Task HideOverlayAsync()
        {
            _logger.LogInformation("HideOverlayAsync called");

            _isOverlayVisible = false;
            AllOverlaysClosed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task HideOverlayOnScreenAsync(int screenIndex)
        {
            _logger.LogInformation("HideOverlayOnScreenAsync called for screen {ScreenIndex}", screenIndex);

            OverlayClickedOnScreen?.Invoke(this, screenIndex);

            // If this was the last screen, mark all overlays as closed
            _isOverlayVisible = false;
            AllOverlaysClosed?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }
    }
}
