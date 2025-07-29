using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IScreenDimmingService
    {
        /// <summary>
        /// Dims all screens to the specified brightness level (0-100)
        /// </summary>
        /// <param name="brightnessPercent">Brightness level from 0 (completely dark) to 100 (full brightness)</param>
        Task DimScreensAsync(int brightnessPercent);
        
        /// <summary>
        /// Restores all screens to their original brightness levels
        /// </summary>
        Task RestoreScreenBrightnessAsync();
        
        /// <summary>
        /// Gets the current brightness level of the primary screen
        /// </summary>
        /// <returns>Brightness level from 0-100, or -1 if unable to determine</returns>
        Task<int> GetCurrentBrightnessAsync();
        
        /// <summary>
        /// Checks if screen dimming is supported on this system
        /// </summary>
        bool IsSupported { get; }
    }
}