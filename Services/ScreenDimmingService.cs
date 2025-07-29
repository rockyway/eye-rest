using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class ScreenDimmingService : IScreenDimmingService
    {
        private readonly ILogger<ScreenDimmingService> _logger;
        private readonly Dictionary<string, int> _originalBrightnessLevels = new();
        private bool _isDimmed = false;

        // Windows API declarations for monitor brightness control
        [DllImport("dxva2.dll")]
        private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwMinimumBrightness, uint dwCurrentBrightness, uint dwMaximumBrightness);

        [DllImport("dxva2.dll")]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll")]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll")]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll")]
        private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, ref PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public ScreenDimmingService(ILogger<ScreenDimmingService> logger)
        {
            _logger = logger;
        }

        public bool IsSupported => true; // We support both WMI and API approaches

        public async Task DimScreensAsync(int brightnessPercent)
        {
            try
            {
                _logger.LogInformation($"Dimming screens to {brightnessPercent}% brightness");

                if (brightnessPercent < 0 || brightnessPercent > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(brightnessPercent), "Brightness must be between 0 and 100");
                }

                // Store original brightness levels before dimming
                if (!_isDimmed)
                {
                    await StoreOriginalBrightnessLevels();
                }

                // Try WMI approach first (works for laptop screens and some external monitors)
                var wmiSuccess = await SetBrightnessViaWMI(brightnessPercent);
                
                // Also try Windows API approach (works for external monitors)
                var apiSuccess = await SetBrightnessViaAPI(brightnessPercent);

                if (wmiSuccess || apiSuccess)
                {
                    _isDimmed = true;
                    _logger.LogInformation($"Successfully dimmed screens to {brightnessPercent}% (WMI: {wmiSuccess}, API: {apiSuccess})");
                }
                else
                {
                    _logger.LogWarning("Failed to dim screens using both WMI and API approaches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error dimming screens to {brightnessPercent}%");
                throw;
            }
        }

        public async Task RestoreScreenBrightnessAsync()
        {
            try
            {
                if (!_isDimmed)
                {
                    _logger.LogInformation("Screens are not currently dimmed, no restoration needed");
                    return;
                }

                _logger.LogInformation("Restoring original screen brightness levels");

                // Try WMI approach first
                var wmiSuccess = await RestoreBrightnessViaWMI();
                
                // Also try Windows API approach
                var apiSuccess = await RestoreBrightnessViaAPI();

                if (wmiSuccess || apiSuccess)
                {
                    _isDimmed = false;
                    _originalBrightnessLevels.Clear();
                    _logger.LogInformation($"Successfully restored screen brightness (WMI: {wmiSuccess}, API: {apiSuccess})");
                }
                else
                {
                    _logger.LogWarning("Failed to restore screen brightness using both WMI and API approaches");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring screen brightness");
                throw;
            }
        }

        public async Task<int> GetCurrentBrightnessAsync()
        {
            try
            {
                // Try WMI approach first
                var wmiBrightness = await GetBrightnessViaWMI();
                if (wmiBrightness >= 0)
                {
                    return wmiBrightness;
                }

                // Try API approach
                var apiBrightness = await GetBrightnessViaAPI();
                return apiBrightness;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current brightness");
                return -1;
            }
        }

        private async Task StoreOriginalBrightnessLevels()
        {
            try
            {
                _originalBrightnessLevels.Clear();

                // Store WMI brightness levels
                await Task.Run(() =>
                {
                    try
                    {
                        using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var instanceName = obj["InstanceName"]?.ToString() ?? "Unknown";
                            var currentBrightness = Convert.ToInt32(obj["CurrentBrightness"]);
                            _originalBrightnessLevels[instanceName] = currentBrightness;
                            _logger.LogDebug($"Stored original brightness for {instanceName}: {currentBrightness}%");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to store WMI brightness levels");
                    }
                });

                _logger.LogInformation($"Stored original brightness levels for {_originalBrightnessLevels.Count} display(s)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing original brightness levels");
            }
        }

        private async Task<bool> SetBrightnessViaWMI(int brightnessPercent)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        obj.InvokeMethod("WmiSetBrightness", new object[] { 1, brightnessPercent });
                        _logger.LogDebug($"Set brightness to {brightnessPercent}% via WMI for monitor");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "WMI brightness control failed");
                    return false;
                }
            });
        }

        private async Task<bool> SetBrightnessViaAPI(int brightnessPercent)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var monitors = new List<IntPtr>();
                    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                        (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                        {
                            monitors.Add(hMonitor);
                            return true;
                        }, IntPtr.Zero);

                    var success = false;
                    foreach (var monitor in monitors)
                    {
                        try
                        {
                            if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint numMonitors) && numMonitors > 0)
                            {
                                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                                if (GetPhysicalMonitorsFromHMONITOR(monitor, numMonitors, physicalMonitors))
                                {
                                    for (int i = 0; i < numMonitors; i++)
                                    {
                                        var brightness = (uint)brightnessPercent;
                                        if (SetMonitorBrightness(physicalMonitors[i].hPhysicalMonitor, 0, brightness, 100))
                                        {
                                            success = true;
                                            _logger.LogDebug($"Set brightness to {brightnessPercent}% via API for {physicalMonitors[i].szPhysicalMonitorDescription}");
                                        }
                                    }
                                    DestroyPhysicalMonitors(numMonitors, ref physicalMonitors);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, $"Failed to set brightness for monitor via API");
                        }
                    }
                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "API brightness control failed");
                    return false;
                }
            });
        }

        private async Task<bool> RestoreBrightnessViaWMI()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var instanceName = obj["InstanceName"]?.ToString() ?? "Unknown";
                        if (_originalBrightnessLevels.TryGetValue(instanceName, out var originalBrightness))
                        {
                            obj.InvokeMethod("WmiSetBrightness", new object[] { 1, originalBrightness });
                            _logger.LogDebug($"Restored brightness to {originalBrightness}% via WMI for {instanceName}");
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "WMI brightness restoration failed");
                    return false;
                }
            });
        }

        private async Task<bool> RestoreBrightnessViaAPI()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // For API approach, we'll restore to 100% since we don't have monitor-specific original values
                    var monitors = new List<IntPtr>();
                    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                        (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                        {
                            monitors.Add(hMonitor);
                            return true;
                        }, IntPtr.Zero);

                    var success = false;
                    foreach (var monitor in monitors)
                    {
                        try
                        {
                            if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint numMonitors) && numMonitors > 0)
                            {
                                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                                if (GetPhysicalMonitorsFromHMONITOR(monitor, numMonitors, physicalMonitors))
                                {
                                    for (int i = 0; i < numMonitors; i++)
                                    {
                                        // Restore to 100% brightness (could be improved to store/restore actual original values)
                                        if (SetMonitorBrightness(physicalMonitors[i].hPhysicalMonitor, 0, 100, 100))
                                        {
                                            success = true;
                                            _logger.LogDebug($"Restored brightness to 100% via API for {physicalMonitors[i].szPhysicalMonitorDescription}");
                                        }
                                    }
                                    DestroyPhysicalMonitors(numMonitors, ref physicalMonitors);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, $"Failed to restore brightness for monitor via API");
                        }
                    }
                    return success;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "API brightness restoration failed");
                    return false;
                }
            });
        }

        private async Task<int> GetBrightnessViaWMI()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightness");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt32(obj["CurrentBrightness"]);
                    }
                    return -1;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get brightness via WMI");
                    return -1;
                }
            });
        }

        private async Task<int> GetBrightnessViaAPI()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var monitors = new List<IntPtr>();
                    EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                        (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                        {
                            monitors.Add(hMonitor);
                            return true;
                        }, IntPtr.Zero);

                    foreach (var monitor in monitors)
                    {
                        try
                        {
                            if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint numMonitors) && numMonitors > 0)
                            {
                                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                                if (GetPhysicalMonitorsFromHMONITOR(monitor, numMonitors, physicalMonitors))
                                {
                                    if (GetMonitorBrightness(physicalMonitors[0].hPhysicalMonitor, 
                                        out uint min, out uint current, out uint max))
                                    {
                                        DestroyPhysicalMonitors(numMonitors, ref physicalMonitors);
                                        return (int)current;
                                    }
                                    DestroyPhysicalMonitors(numMonitors, ref physicalMonitors);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, $"Failed to get brightness for monitor via API");
                        }
                    }
                    return -1;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "API brightness retrieval failed");
                    return -1;
                }
            });
        }
    }
}