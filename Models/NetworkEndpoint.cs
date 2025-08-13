using System;
using System.Net;

namespace EyeRest.Models
{
    /// <summary>
    /// Represents a UDP network endpoint associated with a process
    /// </summary>
    public class UdpEndpoint
    {
        /// <summary>
        /// Process ID that owns this endpoint
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Local IP address
        /// </summary>
        public IPAddress LocalAddress { get; set; } = IPAddress.None;

        /// <summary>
        /// Local port number
        /// </summary>
        public int LocalPort { get; set; }

        /// <summary>
        /// When this endpoint was first detected
        /// </summary>
        public DateTime FirstSeen { get; set; } = DateTime.Now;

        /// <summary>
        /// Last time this endpoint was seen active
        /// </summary>
        public DateTime LastSeen { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether this endpoint is considered active for meeting detection
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Address family (IPv4 or IPv6)
        /// </summary>
        public System.Net.Sockets.AddressFamily AddressFamily { get; set; }

        /// <summary>
        /// Check if this endpoint represents a non-loopback address
        /// </summary>
        public bool IsNonLoopback => 
            !IPAddress.IsLoopback(LocalAddress) && 
            !LocalAddress.Equals(IPAddress.Any) && 
            !LocalAddress.Equals(IPAddress.IPv6Any);

        /// <summary>
        /// Check if this endpoint is likely used for meeting traffic
        /// </summary>
        public bool IsLikelyMeetingEndpoint =>
            IsNonLoopback && 
            LocalPort > 1024 && 
            LocalPort < 65535;

        /// <summary>
        /// Duration this endpoint has been active
        /// </summary>
        public TimeSpan ActiveDuration => DateTime.Now - FirstSeen;

        public override string ToString()
        {
            return $"{LocalAddress}:{LocalPort} (PID: {ProcessId})";
        }
    }

    /// <summary>
    /// Process information for Teams-related processes
    /// </summary>
    public class TeamsProcess
    {
        /// <summary>
        /// Process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Process name
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Window title if available
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Detected Teams version/type
        /// </summary>
        public TeamsVersion Version { get; set; }

        /// <summary>
        /// When this process was first detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether this process has a visible window
        /// </summary>
        public bool HasVisibleWindow { get; set; }

        public override string ToString()
        {
            return $"{ProcessName} (PID: {ProcessId}, Version: {Version})";
        }
    }

    /// <summary>
    /// Teams client version/type enumeration
    /// </summary>
    public enum TeamsVersion
    {
        Classic,        // teams.exe
        NewClient,      // ms-teams.exe
        WebView2,       // msedgewebview2.exe
        Unknown
    }
}