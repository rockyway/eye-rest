using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Factory interface for creating meeting detection services
    /// </summary>
    public interface IMeetingDetectionServiceFactory
    {
        /// <summary>
        /// Create a meeting detection service for the specified method
        /// </summary>
        /// <param name="method">Detection method to use</param>
        /// <returns>Meeting detection service instance</returns>
        IMeetingDetectionService CreateDetectionService(MeetingDetectionMethod method);

        /// <summary>
        /// Validate that a detection method is available on this system
        /// </summary>
        /// <param name="method">Detection method to validate</param>
        /// <returns>True if method is available and functional</returns>
        Task<bool> ValidateDetectionMethodAsync(MeetingDetectionMethod method);

        /// <summary>
        /// Get a description of the detection method
        /// </summary>
        /// <param name="method">Detection method</param>
        /// <returns>Human-readable description</returns>
        string GetDetectionMethodDescription(MeetingDetectionMethod method);

        /// <summary>
        /// Check if a detection method requires elevated permissions
        /// </summary>
        /// <param name="method">Detection method</param>
        /// <returns>True if elevated permissions may be required</returns>
        bool RequiresElevatedPermissions(MeetingDetectionMethod method);
    }
}