using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace EyeRest.Services
{
    public class StartupManager : IStartupManager
    {
        private readonly ILogger<StartupManager> _logger;
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ApplicationName = "EyeRest";
        private const string MsixStartupTaskId = "EyeRestStartup";

        public StartupManager(ILogger<StartupManager> logger)
        {
            _logger = logger;
        }

        public bool IsStartupEnabled()
        {
            try
            {
                if (IsPackagedApp())
                    return IsStartupEnabledMsix();

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                var value = key?.GetValue(ApplicationName);
                return value != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking startup status");
                return false;
            }
        }

        public void EnableStartup()
        {
            EnableStartup(startMinimized: false);
        }

        public void EnableStartup(bool startMinimized)
        {
            try
            {
                if (IsPackagedApp())
                {
                    EnableStartupMsix();
                    return;
                }

                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for startup registration");
                    return;
                }

                // Build command with optional --minimized argument
                var command = startMinimized
                    ? $"\"{executablePath}\" --minimized"
                    : $"\"{executablePath}\"";

                _logger.LogInformation($"Registering startup with command: {command}");

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    _logger.LogError("Failed to open registry key for writing");
                    throw new InvalidOperationException("Cannot access Windows startup registry key");
                }

                key.SetValue(ApplicationName, command);

                // Verify the registry write was successful
                var verifyValue = key.GetValue(ApplicationName) as string;
                if (verifyValue != command)
                {
                    _logger.LogError($"Registry verification failed. Expected: {command}, Got: {verifyValue}");
                    throw new InvalidOperationException("Failed to verify registry write");
                }

                _logger.LogInformation($"Startup enabled successfully (minimized: {startMinimized})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling startup");
                throw;
            }
        }

        public void DisableStartup()
        {
            try
            {
                if (IsPackagedApp())
                {
                    DisableStartupMsix();
                    return;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key?.GetValue(ApplicationName) != null)
                {
                    key.DeleteValue(ApplicationName);
                    _logger.LogInformation("Startup disabled successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling startup");
                throw;
            }
        }

        #region MSIX Packaged App Support

        /// <summary>
        /// Detects whether the app is running as an MSIX-packaged Desktop Bridge app.
        /// </summary>
        private static bool IsPackagedApp()
        {
            try
            {
                // Windows.ApplicationModel.Package.Current throws if not packaged
                _ = Windows.ApplicationModel.Package.Current.Id.Name;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStartupEnabledMsix()
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(MsixStartupTaskId)
                    .AsTask().GetAwaiter().GetResult();
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking MSIX startup task state");
                return false;
            }
        }

        private void EnableStartupMsix()
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(MsixStartupTaskId)
                    .AsTask().GetAwaiter().GetResult();

                if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
                {
                    var result = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                    _logger.LogInformation($"MSIX startup task request result: {result}");
                }
                else if (task.State == Windows.ApplicationModel.StartupTaskState.DisabledByUser)
                {
                    _logger.LogWarning("Startup was disabled by the user in Windows Settings. Cannot re-enable programmatically.");
                }
                else
                {
                    _logger.LogInformation($"MSIX startup task already in state: {task.State}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling MSIX startup task");
                throw;
            }
        }

        private void DisableStartupMsix()
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(MsixStartupTaskId)
                    .AsTask().GetAwaiter().GetResult();
                task.Disable();
                _logger.LogInformation("MSIX startup task disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling MSIX startup task");
                throw;
            }
        }

        #endregion

        private string? GetExecutablePath()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var location = assembly.Location;

                // For .NET 6+ single-file deployments, use the process path
                if (string.IsNullOrEmpty(location) || location.EndsWith(".dll"))
                {
                    location = Environment.ProcessPath;
                }

                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting executable path");
                return null;
            }
        }
    }
}
