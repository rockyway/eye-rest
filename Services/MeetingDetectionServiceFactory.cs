using System;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Factory for creating meeting detection services based on the selected method
    /// </summary>
    public class MeetingDetectionServiceFactory : IMeetingDetectionServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MeetingDetectionServiceFactory> _logger;

        public MeetingDetectionServiceFactory(
            IServiceProvider serviceProvider,
            ILogger<MeetingDetectionServiceFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IMeetingDetectionService CreateDetectionService(MeetingDetectionMethod method)
        {
            try
            {
                _logger.LogInformation($"Creating meeting detection service for method: {method}");

                return method switch
                {
                    MeetingDetectionMethod.WindowBased => _serviceProvider.GetRequiredService<WindowBasedMeetingDetectionService>(),
                    MeetingDetectionMethod.NetworkBased => _serviceProvider.GetRequiredService<NetworkBasedMeetingDetectionService>(),
                    MeetingDetectionMethod.Hybrid => _serviceProvider.GetRequiredService<HybridMeetingDetectionService>(),
                    _ => throw new NotSupportedException($"Detection method {method} is not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create detection service for method {method}");
                throw;
            }
        }

        public async Task<bool> ValidateDetectionMethodAsync(MeetingDetectionMethod method)
        {
            try
            {
                _logger.LogDebug($"Validating detection method: {method}");

                switch (method)
                {
                    case MeetingDetectionMethod.WindowBased:
                        // Window-based detection should always work
                        return true;

                    case MeetingDetectionMethod.NetworkBased:
                        // Check if network monitoring is available
                        var networkMonitor = _serviceProvider.GetService<INetworkEndpointMonitor>();
                        if (networkMonitor == null)
                        {
                            _logger.LogWarning("Network endpoint monitor service not available");
                            return false;
                        }

                        if (!networkMonitor.IsNetworkMonitoringAvailable)
                        {
                            _logger.LogWarning("Network monitoring not available on this system");
                            return false;
                        }

                        return await networkMonitor.TestNetworkAccessAsync();

                    case MeetingDetectionMethod.Hybrid:
                        // Hybrid requires at least one method to work
                        var windowWorks = await ValidateDetectionMethodAsync(MeetingDetectionMethod.WindowBased);
                        var networkWorks = await ValidateDetectionMethodAsync(MeetingDetectionMethod.NetworkBased);
                        
                        bool hybridWorks = windowWorks || networkWorks;
                        _logger.LogInformation($"Hybrid validation: Window={windowWorks}, Network={networkWorks}, Result={hybridWorks}");
                        return hybridWorks;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating detection method {method}");
                return false;
            }
        }

        public string GetDetectionMethodDescription(MeetingDetectionMethod method)
        {
            return method switch
            {
                MeetingDetectionMethod.WindowBased => 
                    "Monitors Teams window titles and process names. Works with most Teams versions and requires no special permissions.",
                
                MeetingDetectionMethod.NetworkBased => 
                    "Monitors UDP network connections for meeting activity. More reliable but may require elevated permissions.",
                
                MeetingDetectionMethod.Hybrid => 
                    "Uses both window and network detection methods. Maximum reliability with automatic fallback.",
                
                _ => "Unknown detection method"
            };
        }

        public bool RequiresElevatedPermissions(MeetingDetectionMethod method)
        {
            return method switch
            {
                MeetingDetectionMethod.WindowBased => false,
                MeetingDetectionMethod.NetworkBased => true,
                MeetingDetectionMethod.Hybrid => true, // Because it includes network monitoring
                _ => false
            };
        }
    }
}