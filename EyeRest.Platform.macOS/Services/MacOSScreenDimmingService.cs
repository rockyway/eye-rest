using System;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IScreenDimmingService"/> using IOKit display brightness control.
    /// Uses IODisplayGetFloatParameter/IODisplaySetFloatParameter for reading and setting brightness.
    /// Note: On newer macOS versions, IOKit brightness control may be restricted.
    /// </summary>
    public class MacOSScreenDimmingService : IScreenDimmingService
    {
        private readonly ILogger<MacOSScreenDimmingService> _logger;
        private float _originalBrightness = -1f;
        private bool _isDimmed;
        private bool _isSupported;
        private bool _supportChecked;

        public MacOSScreenDimmingService(ILogger<MacOSScreenDimmingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsSupported
        {
            get
            {
                if (!_supportChecked)
                {
                    _isSupported = CheckBrightnessSupport();
                    _supportChecked = true;
                }
                return _isSupported;
            }
        }

        public Task DimScreensAsync(int brightnessPercent)
        {
            if (!IsSupported)
            {
                _logger.LogWarning("Screen dimming is not supported on this system");
                return Task.CompletedTask;
            }

            try
            {
                // Save original brightness if not already saved
                if (_originalBrightness < 0)
                {
                    _originalBrightness = GetBrightnessFloat();
                    _logger.LogDebug("Saved original brightness: {Brightness}", _originalBrightness);
                }

                var targetBrightness = Math.Clamp(brightnessPercent / 100f, 0f, 1f);
                SetBrightnessFloat(targetBrightness);
                _isDimmed = true;

                _logger.LogDebug("Screen dimmed to {Percent}%", brightnessPercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dim screen to {Percent}%", brightnessPercent);
            }

            return Task.CompletedTask;
        }

        public Task RestoreScreenBrightnessAsync()
        {
            if (!_isDimmed)
            {
                return Task.CompletedTask;
            }

            try
            {
                if (_originalBrightness >= 0)
                {
                    SetBrightnessFloat(_originalBrightness);
                    _logger.LogDebug("Screen brightness restored to {Brightness}", _originalBrightness);
                    _originalBrightness = -1f;
                }
                else
                {
                    // Restore to full brightness as fallback
                    SetBrightnessFloat(1.0f);
                    _logger.LogDebug("Screen brightness restored to full (no original saved)");
                }

                _isDimmed = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore screen brightness");
            }

            return Task.CompletedTask;
        }

        public Task<int> GetCurrentBrightnessAsync()
        {
            if (!IsSupported)
            {
                return Task.FromResult(-1);
            }

            try
            {
                var brightness = GetBrightnessFloat();
                return Task.FromResult((int)(brightness * 100));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current brightness");
                return Task.FromResult(-1);
            }
        }

        private bool CheckBrightnessSupport()
        {
            try
            {
                var service = IOKit.IOServiceGetMatchingService(
                    IOKit.kIOMasterPortDefault,
                    IOKit.IOServiceMatching("IODisplayConnect"));

                if (service == 0)
                {
                    _logger.LogDebug("IOKit display service not found - brightness control not supported");
                    return false;
                }

                try
                {
                    var result = IOKit.IODisplayGetFloatParameter(
                        service, 0, IOKit.kIODisplayBrightnessKey, out _);

                    var supported = result == 0; // kIOReturnSuccess
                    _logger.LogDebug("IOKit brightness support check: {Supported}", supported);
                    return supported;
                }
                finally
                {
                    IOKit.IOObjectRelease(service);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IOKit brightness support check failed");
                return false;
            }
        }

        private float GetBrightnessFloat()
        {
            var service = IOKit.IOServiceGetMatchingService(
                IOKit.kIOMasterPortDefault,
                IOKit.IOServiceMatching("IODisplayConnect"));

            if (service == 0)
                return 1.0f;

            try
            {
                var result = IOKit.IODisplayGetFloatParameter(
                    service, 0, IOKit.kIODisplayBrightnessKey, out var brightness);

                return result == 0 ? brightness : 1.0f;
            }
            finally
            {
                IOKit.IOObjectRelease(service);
            }
        }

        private void SetBrightnessFloat(float brightness)
        {
            var service = IOKit.IOServiceGetMatchingService(
                IOKit.kIOMasterPortDefault,
                IOKit.IOServiceMatching("IODisplayConnect"));

            if (service == 0)
            {
                _logger.LogWarning("Failed to get IOKit display service for brightness control");
                return;
            }

            try
            {
                var result = IOKit.IODisplaySetFloatParameter(
                    service, 0, IOKit.kIODisplayBrightnessKey, brightness);

                if (result != 0)
                {
                    _logger.LogWarning("IODisplaySetFloatParameter returned {Result}", result);
                }
            }
            finally
            {
                IOKit.IOObjectRelease(service);
            }
        }
    }
}
