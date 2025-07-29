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

        public StartupManager(ILogger<StartupManager> logger)
        {
            _logger = logger;
        }

        public bool IsStartupEnabled()
        {
            try
            {
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
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Could not determine executable path for startup registration");
                    return;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                key?.SetValue(ApplicationName, $"\"{executablePath}\"");
                
                _logger.LogInformation("Startup enabled successfully");
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