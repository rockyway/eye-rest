using System;
using System.Collections.Generic;
using System.Net;

namespace EyeRest.Models
{
    /// <summary>
    /// Configuration settings for meeting detection functionality
    /// </summary>
    public class MeetingDetectionConfiguration
    {
        /// <summary>
        /// Selected meeting detection method
        /// </summary>
        public MeetingDetectionMethod DetectionMethod { get; set; } = MeetingDetectionMethod.WindowBased;

        /// <summary>
        /// Enable fallback to alternative detection method if primary fails
        /// </summary>
        public bool EnableFallbackDetection { get; set; } = true;

        /// <summary>
        /// Polling interval for network-based detection (seconds)
        /// </summary>
        public int NetworkPollingIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Polling interval for window-based detection (seconds)
        /// </summary>
        public int WindowPollingIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Enable detailed logging of detection activity for debugging
        /// </summary>
        public bool LogDetectionActivity { get; set; } = false;

        /// <summary>
        /// Include IPv6 network monitoring in addition to IPv4
        /// </summary>
        public bool IncludeIPv6Monitoring { get; set; } = true;

        /// <summary>
        /// Minimum number of UDP endpoints required to consider a meeting active
        /// </summary>
        public int MinimumUdpEndpointsForMeeting { get; set; } = 2;

        /// <summary>
        /// Maximum time to wait for meeting detection confirmation (seconds)
        /// </summary>
        public int MeetingDetectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Include private network addresses in monitoring (10.x.x.x, 192.168.x.x, etc.)
        /// </summary>
        public bool IncludePrivateNetworkAddresses { get; set; } = false;

        /// <summary>
        /// Network addresses to exclude from meeting detection
        /// </summary>
        public List<string> ExcludedNetworkAddresses { get; set; } = new List<string>
        {
            "127.0.0.1",    // IPv4 loopback
            "::1",          // IPv6 loopback
            "0.0.0.0",      // IPv4 any
            "::"            // IPv6 any
        };

        /// <summary>
        /// Port ranges to exclude from meeting detection (format: "start-end" or "port")
        /// </summary>
        public List<string> ExcludedPortRanges { get; set; } = new List<string>
        {
            "1-1023",       // System/privileged ports
            "5353",         // mDNS
            "53"            // DNS
        };

        /// <summary>
        /// Enable Teams detection
        /// </summary>
        public bool EnableTeamsDetection { get; set; } = true;

        /// <summary>
        /// Enable Zoom detection
        /// </summary>
        public bool EnableZoomDetection { get; set; } = true;

        /// <summary>
        /// Enable Webex detection
        /// </summary>
        public bool EnableWebexDetection { get; set; } = true;

        /// <summary>
        /// Enable Google Meet detection
        /// </summary>
        public bool EnableGoogleMeetDetection { get; set; } = true;

        /// <summary>
        /// Enable Skype detection
        /// </summary>
        public bool EnableSkypeDetection { get; set; } = true;

        /// <summary>
        /// Custom process names to monitor for meetings
        /// </summary>
        public List<string> CustomProcessNames { get; set; } = new List<string>();

        /// <summary>
        /// Window titles to exclude from meeting detection
        /// </summary>
        public List<string> ExcludedWindowTitles { get; set; } = new List<string>();
    }
}