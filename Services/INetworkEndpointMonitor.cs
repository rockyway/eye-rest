using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    /// <summary>
    /// Interface for monitoring network endpoints of processes
    /// </summary>
    public interface INetworkEndpointMonitor
    {
        /// <summary>
        /// Get all UDP endpoints for a specific process
        /// </summary>
        /// <param name="processId">Process ID to monitor</param>
        /// <returns>List of UDP endpoints for the process</returns>
        Task<List<UdpEndpoint>> GetUdpEndpointsByProcessAsync(int processId);

        /// <summary>
        /// Get all UDP endpoints from all processes
        /// </summary>
        /// <returns>List of all UDP endpoints</returns>
        Task<List<UdpEndpoint>> GetAllUdpEndpointsAsync();

        /// <summary>
        /// Check if a process has active non-loopback UDP connections
        /// </summary>
        /// <param name="processId">Process ID to check</param>
        /// <returns>True if process has meeting-like network activity</returns>
        Task<bool> HasActiveNetworkConnectionsAsync(int processId);

        /// <summary>
        /// Check if network monitoring is available on this system
        /// </summary>
        bool IsNetworkMonitoringAvailable { get; }

        /// <summary>
        /// Test if the monitor can access network information
        /// </summary>
        /// <returns>True if network access is available</returns>
        Task<bool> TestNetworkAccessAsync();
    }

    /// <summary>
    /// Interface for monitoring Teams-related processes
    /// </summary>
    public interface IProcessMonitor
    {
        /// <summary>
        /// Get all running Teams-related processes
        /// </summary>
        /// <returns>List of Teams processes</returns>
        Task<List<TeamsProcess>> GetTeamsProcessesAsync();

        /// <summary>
        /// Check if a process is Teams-related
        /// </summary>
        /// <param name="processName">Process name to check</param>
        /// <returns>True if process is Teams-related</returns>
        bool IsTeamsProcess(string processName);

        /// <summary>
        /// Get the Teams version/type for a process
        /// </summary>
        /// <param name="processName">Process name</param>
        /// <returns>Teams version enum</returns>
        TeamsVersion GetTeamsVersion(string processName);
    }
}