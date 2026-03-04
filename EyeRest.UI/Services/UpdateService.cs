using System;
using System.Reflection;
using System.Threading.Tasks;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
#if !STORE_BUILD
using Velopack;
using Velopack.Sources;
#endif

namespace EyeRest.UI.Services;

public class UpdateService : IUpdateService
{
    private const string GitHubRepoUrl = "https://github.com/rockyway/eye-rest";

    private readonly ILogger<UpdateService> _logger;

#if !STORE_BUILD
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _latestUpdateInfo;
#endif

#pragma warning disable CS0067 // Event is unused in STORE_BUILD configuration
    public event EventHandler<AppUpdateInfo>? UpdateAvailable;
#pragma warning restore CS0067

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;

#if !STORE_BUILD
        _updateManager = new UpdateManager(
            new GithubSource(GitHubRepoUrl, null, false));
#endif

        _logger.LogInformation(
            "UpdateService initialized. IsUpdateSupported={IsSupported}, CurrentVersion={Version}",
            IsUpdateSupported, CurrentVersion);
    }

    public bool IsUpdateSupported
    {
        get
        {
#if STORE_BUILD
            return false;
#else
            return _updateManager.IsInstalled;
#endif
        }
    }

    public string CurrentVersion
    {
        get
        {
#if !STORE_BUILD
            if (_updateManager.IsInstalled && _updateManager.CurrentVersion != null)
                return _updateManager.CurrentVersion.ToString();
#endif
            var asm = Assembly.GetExecutingAssembly().GetName().Version;
            return asm != null
                ? $"{asm.Major}.{asm.Minor}.{asm.Build}"
                : "0.0.0";
        }
    }

    public Task<AppUpdateInfo?> CheckForUpdatesAsync()
    {
#if STORE_BUILD
        return Task.FromResult<AppUpdateInfo?>(null);
#else
        return CheckForUpdatesInternalAsync();
    }

    private async Task<AppUpdateInfo?> CheckForUpdatesInternalAsync()
    {
        if (!IsUpdateSupported)
        {
            _logger.LogDebug("Update check skipped: not installed via Velopack");
            return null;
        }

        try
        {
            _logger.LogInformation("Checking for updates...");
            var updateInfo = await _updateManager.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                _logger.LogInformation("No updates available");
                return null;
            }

            _latestUpdateInfo = updateInfo;
            var result = new AppUpdateInfo
            {
                TargetVersion = updateInfo.TargetFullRelease.Version.ToString(),
                IsDownloaded = false
            };

            _logger.LogInformation("Update available: {Version}", result.TargetVersion);
            UpdateAvailable?.Invoke(this, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
            return null;
        }
#endif
    }

    public Task DownloadUpdateAsync(IProgress<int>? progress = null)
    {
#if STORE_BUILD
        return Task.CompletedTask;
#else
        return DownloadUpdateInternalAsync(progress);
    }

    private async Task DownloadUpdateInternalAsync(IProgress<int>? progress)
    {
        if (!IsUpdateSupported || _latestUpdateInfo == null)
        {
            _logger.LogWarning("Download skipped: no update info available");
            return;
        }

        try
        {
            _logger.LogInformation("Downloading update {Version}...",
                _latestUpdateInfo.TargetFullRelease.Version);

            await _updateManager.DownloadUpdatesAsync(
                _latestUpdateInfo,
                p => progress?.Report(p));

            _logger.LogInformation("Update downloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download update");
            throw;
        }
#endif
    }

    public void ApplyUpdateAndRestart()
    {
#if STORE_BUILD
        return;
#else
        if (!IsUpdateSupported || _latestUpdateInfo == null)
        {
            _logger.LogWarning("Apply skipped: no update info available");
            return;
        }

        try
        {
            _logger.LogInformation("Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_latestUpdateInfo.TargetFullRelease);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");
        }
#endif
    }
}
