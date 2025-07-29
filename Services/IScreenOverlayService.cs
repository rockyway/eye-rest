using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IScreenOverlayService
    {
        /// <summary>
        /// Shows overlay on all screens with specified opacity (0.0 to 1.0)
        /// </summary>
        /// <param name="opacity">Overlay opacity from 0.0 (transparent) to 1.0 (opaque)</param>
        Task ShowOverlayAsync(double opacity = 0.5);
        
        /// <summary>
        /// Hides overlay on all screens
        /// </summary>
        Task HideOverlayAsync();
        
        /// <summary>
        /// Hides overlay on a specific screen
        /// </summary>
        /// <param name="screenIndex">Index of the screen to hide overlay on</param>
        Task HideOverlayOnScreenAsync(int screenIndex);
        
        /// <summary>
        /// Gets the number of screens currently detected
        /// </summary>
        int ScreenCount { get; }
        
        /// <summary>
        /// Checks if overlay is currently visible on any screen
        /// </summary>
        bool IsOverlayVisible { get; }
        
        /// <summary>
        /// Event fired when user clicks on overlay to close it on a specific screen
        /// </summary>
        event EventHandler<int> OverlayClickedOnScreen;
        
        /// <summary>
        /// Event fired when all overlays have been closed by user interaction
        /// </summary>
        event EventHandler AllOverlaysClosed;
    }
}