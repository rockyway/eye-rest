using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    /// <summary>
    /// Information about an available update.
    /// </summary>
    public class AppUpdateInfo
    {
        public string TargetVersion { get; set; } = string.Empty;
        public bool IsDownloaded { get; set; }
    }

    /// <summary>
    /// Provides application update checking, downloading, and installation.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Whether the app was installed via Velopack and can check for updates.
        /// Returns false in dev mode (dotnet run) and Store builds.
        /// </summary>
        bool IsUpdateSupported { get; }

        /// <summary>
        /// The current application version string.
        /// </summary>
        string CurrentVersion { get; }

        /// <summary>
        /// Checks GitHub Releases for a newer version.
        /// Returns null if no update is available or if not supported.
        /// </summary>
        Task<AppUpdateInfo?> CheckForUpdatesAsync();

        /// <summary>
        /// Downloads the update package. Progress reports 0-100%.
        /// </summary>
        Task DownloadUpdateAsync(IProgress<int>? progress = null);

        /// <summary>
        /// Applies the downloaded update and restarts the application.
        /// </summary>
        void ApplyUpdateAndRestart();

        /// <summary>
        /// Raised when a background check finds an available update.
        /// </summary>
        event EventHandler<AppUpdateInfo>? UpdateAvailable;
    }
}
