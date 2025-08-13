using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// Windows-specific implementation of network endpoint monitoring using P/Invoke
    /// </summary>
    public class WindowsNetworkEndpointMonitor : INetworkEndpointMonitor
    {
        private readonly ILogger<WindowsNetworkEndpointMonitor> _logger;
        private readonly Dictionary<int, List<UdpEndpoint>> _endpointCache = new();
        private readonly Dictionary<int, DateTime> _lastCacheUpdate = new();
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(5);

        public WindowsNetworkEndpointMonitor(ILogger<WindowsNetworkEndpointMonitor> logger)
        {
            _logger = logger;
        }

        #region Windows API Constants and Structures

        private const int AF_INET = 2; // IPv4
        private const int AF_INET6 = 23; // IPv6
        private const int NO_ERROR = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            // Note: The actual table follows this structure
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr;
            public uint localPort;
            public uint owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] localAddr;
            public uint localScopeId;
            public uint localPort;
            public uint owningPid;
        }

        public enum UDP_TABLE_CLASS
        {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable,
            ref int dwSize,
            bool bOrder,
            int ulAf,
            UDP_TABLE_CLASS TableClass,
            uint Reserved);

        #endregion

        public bool IsNetworkMonitoringAvailable 
        { 
            get
            {
                try
                {
                    // Test if we can call the API
                    int size = 0;
                    uint result = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                    return result == 0 || result == 122; // ERROR_INSUFFICIENT_BUFFER is expected
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<bool> TestNetworkAccessAsync()
        {
            try
            {
                var endpoints = await GetAllUdpEndpointsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Network access test failed");
                return false;
            }
        }

        public async Task<List<UdpEndpoint>> GetUdpEndpointsByProcessAsync(int processId)
        {
            try
            {
                // Check cache first
                if (_endpointCache.ContainsKey(processId) && 
                    _lastCacheUpdate.ContainsKey(processId) &&
                    DateTime.Now - _lastCacheUpdate[processId] < _cacheTimeout)
                {
                    return _endpointCache[processId];
                }

                var allEndpoints = await GetAllUdpEndpointsAsync();
                var processEndpoints = allEndpoints.Where(e => e.ProcessId == processId).ToList();

                // Update cache
                _endpointCache[processId] = processEndpoints;
                _lastCacheUpdate[processId] = DateTime.Now;

                return processEndpoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get UDP endpoints for process {processId}");
                return new List<UdpEndpoint>();
            }
        }

        public async Task<List<UdpEndpoint>> GetAllUdpEndpointsAsync()
        {
            var endpoints = new List<UdpEndpoint>();

            try
            {
                // Get IPv4 endpoints
                var ipv4Endpoints = await GetUdpEndpointsAsync(AF_INET);
                endpoints.AddRange(ipv4Endpoints);

                // Get IPv6 endpoints
                var ipv6Endpoints = await GetUdpEndpointsAsync(AF_INET6);
                endpoints.AddRange(ipv6Endpoints);

                _logger.LogDebug($"Retrieved {endpoints.Count} UDP endpoints ({ipv4Endpoints.Count} IPv4, {ipv6Endpoints.Count} IPv6)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve UDP endpoints");
            }

            return endpoints;
        }

        public async Task<bool> HasActiveNetworkConnectionsAsync(int processId)
        {
            try
            {
                var endpoints = await GetUdpEndpointsByProcessAsync(processId);
                
                // Filter for meeting-like endpoints
                var meetingEndpoints = endpoints.Where(e => 
                    e.IsLikelyMeetingEndpoint && 
                    e.ActiveDuration > TimeSpan.FromSeconds(5) // Must be sustained
                ).ToList();

                bool hasConnections = meetingEndpoints.Count >= 2; // Audio + Video streams typically
                
                if (hasConnections)
                {
                    _logger.LogDebug($"Process {processId} has {meetingEndpoints.Count} meeting-like endpoints");
                }

                return hasConnections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check network connections for process {processId}");
                return false;
            }
        }

        private async Task<List<UdpEndpoint>> GetUdpEndpointsAsync(int addressFamily)
        {
            var endpoints = new List<UdpEndpoint>();

            try
            {
                await Task.Run(() =>
                {
                    int bufferSize = 0;
                    uint result = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, addressFamily, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                    
                    if (bufferSize == 0)
                    {
                        _logger.LogDebug($"No UDP table data available for address family {addressFamily}");
                        return;
                    }

                    IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                    try
                    {
                        result = GetExtendedUdpTable(buffer, ref bufferSize, true, addressFamily, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                        
                        if (result == NO_ERROR)
                        {
                            if (addressFamily == AF_INET)
                            {
                                endpoints.AddRange(ParseIPv4UdpTable(buffer));
                            }
                            else if (addressFamily == AF_INET6)
                            {
                                endpoints.AddRange(ParseIPv6UdpTable(buffer));
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"GetExtendedUdpTable failed with error code: {result}");
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving UDP endpoints for address family {addressFamily}");
            }

            return endpoints;
        }

        private List<UdpEndpoint> ParseIPv4UdpTable(IntPtr buffer)
        {
            var endpoints = new List<UdpEndpoint>();

            try
            {
                var table = Marshal.PtrToStructure<MIB_UDPTABLE_OWNER_PID>(buffer);
                IntPtr rowPtr = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(uint))); // Skip dwNumEntries

                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                    
                    var endpoint = new UdpEndpoint
                    {
                        ProcessId = (int)row.owningPid,
                        LocalAddress = new IPAddress(row.localAddr),
                        LocalPort = (int)((row.localPort & 0xFF) << 8 | (row.localPort >> 8) & 0xFF), // Convert from network byte order
                        AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork
                    };

                    endpoints.Add(endpoint);
                    rowPtr = new IntPtr(rowPtr.ToInt64() + Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing IPv4 UDP table");
            }

            return endpoints;
        }

        private List<UdpEndpoint> ParseIPv6UdpTable(IntPtr buffer)
        {
            var endpoints = new List<UdpEndpoint>();

            try
            {
                var table = Marshal.PtrToStructure<MIB_UDPTABLE_OWNER_PID>(buffer);
                IntPtr rowPtr = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(uint))); // Skip dwNumEntries

                for (int i = 0; i < table.dwNumEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDP6ROW_OWNER_PID>(rowPtr);
                    
                    var endpoint = new UdpEndpoint
                    {
                        ProcessId = (int)row.owningPid,
                        LocalAddress = new IPAddress(row.localAddr),
                        LocalPort = (int)((row.localPort & 0xFF) << 8 | (row.localPort >> 8) & 0xFF), // Convert from network byte order
                        AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6
                    };

                    endpoints.Add(endpoint);
                    rowPtr = new IntPtr(rowPtr.ToInt64() + Marshal.SizeOf(typeof(MIB_UDP6ROW_OWNER_PID)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing IPv6 UDP table");
            }

            return endpoints;
        }

        public void Dispose()
        {
            _endpointCache.Clear();
            _lastCacheUpdate.Clear();
        }
    }
}