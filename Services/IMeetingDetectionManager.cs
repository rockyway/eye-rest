using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Interface for managing meeting detection services and method switching
    /// </summary>
    public interface IMeetingDetectionManager : IDisposable
    {
        /// <summary>
        /// Currently active detection service
        /// </summary>
        IMeetingDetectionService? CurrentDetectionService { get; }

        /// <summary>
        /// Current detection method being used
        /// </summary>
        MeetingDetectionMethod CurrentMethod { get; }

        /// <summary>
        /// Event fired when meeting state changes
        /// </summary>
        event EventHandler<MeetingStateEventArgs> MeetingStateChanged;

        /// <summary>
        /// Initialize with the specified detection method
        /// </summary>
        /// <param name="method">Detection method to use</param>
        /// <param name="settings">Detection settings</param>
        Task InitializeAsync(MeetingDetectionMethod method, MeetingDetectionSettings settings);

        /// <summary>
        /// Switch to a different detection method
        /// </summary>
        /// <param name="newMethod">New detection method</param>
        Task SwitchDetectionMethodAsync(MeetingDetectionMethod newMethod);

        /// <summary>
        /// Validate that a detection method is available on this system
        /// </summary>
        /// <param name="method">Detection method to validate</param>
        Task<bool> ValidateMethodAvailabilityAsync(MeetingDetectionMethod method);

        /// <summary>
        /// Update settings for the current detection service
        /// </summary>
        /// <param name="settings">New settings</param>
        Task UpdateSettingsAsync(MeetingDetectionSettings settings);

        /// <summary>
        /// Get status information about the current detection service
        /// </summary>
        DetectionServiceStatus GetStatus();

        /// <summary>
        /// Shutdown the detection manager
        /// </summary>
        Task ShutdownAsync();
    }

    /// <summary>
    /// Status information about the detection service
    /// </summary>
    public class DetectionServiceStatus
    {
        public MeetingDetectionMethod CurrentMethod { get; set; }
        public bool IsMonitoring { get; set; }
        public bool IsMeetingActive { get; set; }
        public int DetectedMeetingsCount { get; set; }
        public DateTime? LastStateChange { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
    }
}