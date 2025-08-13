namespace EyeRest.Models
{
    /// <summary>
    /// Enumeration of available meeting detection methods
    /// </summary>
    public enum MeetingDetectionMethod
    {
        /// <summary>
        /// Window title and process name based detection (default)
        /// - Monitors Teams window titles and process names
        /// - Works with most Teams versions
        /// - No special permissions required
        /// </summary>
        WindowBased,

        /// <summary>
        /// Network activity based detection (advanced)
        /// - Monitors UDP network connections for meeting activity
        /// - More reliable detection of active meetings
        /// - May require elevated permissions
        /// </summary>
        NetworkBased,

        /// <summary>
        /// Hybrid detection using both methods
        /// - Uses both window and network detection
        /// - Maximum reliability with automatic fallback
        /// - Combines benefits of both approaches
        /// </summary>
        Hybrid
    }
}