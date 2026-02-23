using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class UserPresenceService : IUserPresenceService
    {
        private readonly ILogger<UserPresenceService> _logger;
        private readonly IConfigurationService _configurationService;
        private ITimerService? _timerService; // NEW: For triggering timer recovery after system resume
        private readonly DispatcherTimer _monitoringTimer;
        private readonly object _stateLock = new object();
        
        private UserPresenceState _currentState;
        private DateTime _lastStateChange;
        private bool _isMonitoring;
        
        // NEW: Extended away tracking
        private DateTime _awayStartTime;
        private DateTime _idleStartTime;  // P0 FIX: Track when user went idle for extended idle detection
        private TimeSpan _totalAwayTime;
        private bool _hasBeenAwayExtended;
        private IntPtr _sessionNotificationHandle;
        private IntPtr _powerNotificationHandle;
        private IntPtr _mainWindowHandle;
        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _originalWndProc;
        
        private const int IdleThresholdMinutes = 5; // Consider user idle after 5 minutes
        private const int AwayGracePeriodSeconds = 30; // Grace period before marking user as away
        
        // Windows API constants
        private const int WM_WTSSESSION_CHANGE = 0x02B1;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int WTS_CONSOLE_CONNECT = 0x1;
        private const int WTS_CONSOLE_DISCONNECT = 0x2;
        private const int WTS_SESSION_LOCK = 0x7;
        private const int WTS_SESSION_UNLOCK = 0x8;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;
        private const int NOTIFY_FOR_THIS_SESSION = 0;
        
        // Power setting GUIDs
        private static readonly Guid GUID_MONITOR_POWER_ON = new Guid("02731015-4510-4526-99e6-e5a17ebd1aea");
        private static readonly Guid GUID_SESSION_DISPLAY_STATUS = new Guid("2b84c20e-ad23-4ddf-93db-05ffbd7efca5");
        
        // Delegates for window procedure
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public event EventHandler<UserPresenceEventArgs>? UserPresenceChanged;
        public event EventHandler<ExtendedAwayEventArgs>? ExtendedAwaySessionDetected;

        public bool IsUserPresent => _currentState == UserPresenceState.Present;
        public UserPresenceState CurrentState => _currentState;
        public TimeSpan TotalAwayTime => _totalAwayTime;

        /// <summary>
        /// Get the duration of the last away period (for extended idle detection)
        /// Returns TimeSpan.Zero if user was not away or data not available
        /// </summary>
        public TimeSpan GetLastAwayDuration()
        {
            lock (_stateLock)
            {
                return _totalAwayTime;
            }
        }

        public TimeSpan IdleTime
        {
            get
            {
                var lastInputInfo = new LASTINPUTINFO();
                lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
                
                if (GetLastInputInfo(ref lastInputInfo))
                {
                    var idleTime = (uint)Environment.TickCount - lastInputInfo.dwTime;
                    return TimeSpan.FromMilliseconds(idleTime);
                }
                
                return TimeSpan.Zero;
            }
        }

        public UserPresenceService(ILogger<UserPresenceService> logger, IConfigurationService configurationService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _currentState = UserPresenceState.Present;
            _lastStateChange = DateTime.Now;
            
            // Timer to check user presence every 15 seconds (more responsive)
            _monitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _monitoringTimer.Tick += OnMonitoringTimerTick;
            
            // Initialize window procedure delegate
            _wndProcDelegate = WindowProc;
        }

        /// <summary>
        /// Inject TimerService for triggering timer recovery after system resume
        /// Called by ApplicationOrchestrator to avoid circular dependency
        /// </summary>
        public void SetTimerService(ITimerService timerService)
        {
            _timerService = timerService;
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("User presence monitoring is already started");
                return;
            }

            try
            {
                _logger.LogInformation("Starting user presence monitoring");
                
                // Register for session change notifications
                if (!RegisterSessionNotification())
                {
                    _logger.LogWarning("Failed to register for session notifications - continuing with timer-based monitoring only");
                }

                // Start monitoring timer
                _monitoringTimer.Start();
                _isMonitoring = true;
                
                _logger.LogInformation("User presence monitoring started successfully");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start user presence monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("User presence monitoring is not started");
                return;
            }

            try
            {
                _logger.LogInformation("Stopping user presence monitoring");
                
                // Stop monitoring timer
                _monitoringTimer.Stop();
                
                // Unregister session notifications
                UnregisterSessionNotification();
                
                _isMonitoring = false;
                _logger.LogInformation("User presence monitoring stopped successfully");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping user presence monitoring");
                throw;
            }
        }

        private void OnMonitoringTimerTick(object? sender, EventArgs e)
        {
            try
            {
                CheckUserPresence();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user presence");
            }
        }

        private void CheckUserPresence()
        {
            var currentIdleTime = IdleTime;
            var newState = DeterminePresenceState(currentIdleTime);
            
            if (newState != _currentState)
            {
                // Apply grace period for Away state to avoid false triggers
                if (newState == UserPresenceState.Away && 
                    _currentState == UserPresenceState.Present && 
                    DateTime.Now - _lastStateChange < TimeSpan.FromSeconds(AwayGracePeriodSeconds))
                {
                    return; // Don't change state yet, wait for grace period
                }

                _logger.LogInformation($"👤 User presence changed: {_currentState} → {newState} (idle: {currentIdleTime.TotalMinutes:F1}min)");
                UpdatePresenceState(newState, currentIdleTime);
            }
        }

        private UserPresenceState DeterminePresenceState(TimeSpan idleTime)
        {
            // Check if session is locked first
            if (IsSessionLocked())
            {
                return UserPresenceState.Away;
            }

            // Check if user is idle based on input activity
            if (idleTime.TotalMinutes >= IdleThresholdMinutes)
            {
                return UserPresenceState.Idle;
            }

            return UserPresenceState.Present;
        }

        private bool RegisterSessionNotification()
        {
            try
            {
                // Get main window handle from current application
                var app = System.Windows.Application.Current;
                if (app?.MainWindow != null)
                {
                    var windowInteropHelper = new WindowInteropHelper(app.MainWindow);
                    _mainWindowHandle = windowInteropHelper.Handle;
                    
                    if (_mainWindowHandle != IntPtr.Zero)
                    {
                        // Register for session change notifications
                        if (WTSRegisterSessionNotification(_mainWindowHandle, NOTIFY_FOR_THIS_SESSION))
                        {
                            _sessionNotificationHandle = _mainWindowHandle;
                            
                            // Register for power notifications
                            RegisterPowerNotifications();
                            
                            // Subclass the window to receive messages
                            _originalWndProc = SetWindowLongPtr(_mainWindowHandle, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate!));
                            
                            _logger.LogInformation("✅ Session and power notifications registered successfully");
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("❌ Failed to register WTS session notifications - Win32 error: {0}", Marshal.GetLastWin32Error());
                        }
                    }
                    else
                    {
                        _logger.LogWarning("❌ Main window handle is null - cannot register for session notifications");
                    }
                }
                else
                {
                    _logger.LogWarning("❌ Application or MainWindow is null - cannot register for session notifications");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register for session notifications");
                return false;
            }
        }

        private void UnregisterSessionNotification()
        {
            try
            {
                if (_sessionNotificationHandle != IntPtr.Zero)
                {
                    WTSUnRegisterSessionNotification(_sessionNotificationHandle);
                    _sessionNotificationHandle = IntPtr.Zero;
                }
                
                if (_powerNotificationHandle != IntPtr.Zero)
                {
                    UnregisterPowerSettingNotification(_powerNotificationHandle);
                    _powerNotificationHandle = IntPtr.Zero;
                }
                
                if (_mainWindowHandle != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
                {
                    SetWindowLongPtr(_mainWindowHandle, -4, _originalWndProc);
                    _originalWndProc = IntPtr.Zero;
                }
                
                _logger.LogInformation("✅ Session and power notifications unregistered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering session notifications");
            }
        }

        private bool IsSessionLocked()
        {
            try
            {
                // Check if the current session is locked using Windows API
                var sessionInfo = IntPtr.Zero;
                var bytesReturned = 0;
                
                if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSInfoClass.WTSSessionInfo, out sessionInfo, out bytesReturned))
                {
                    try
                    {
                        var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(sessionInfo);
                        return info.State == WTS_CONNECTSTATE_CLASS.WTSDisconnected;
                    }
                    finally
                    {
                        WTSFreeMemory(sessionInfo);
                    }
                }
                
                // Fallback: Check if screen saver is active or session is locked
                return IsScreenSaverActive() || IsWorkstationLocked();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session lock status");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isMonitoring)
                {
                    StopMonitoringAsync().Wait(TimeSpan.FromSeconds(5));
                }

                _monitoringTimer?.Stop();
                UnregisterSessionNotification();
                
                _logger.LogInformation("UserPresenceService disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing UserPresenceService");
            }
        }

        private void RegisterPowerNotifications()
        {
            try
            {
                if (_mainWindowHandle != IntPtr.Zero)
                {
                    var powerGuid = GUID_MONITOR_POWER_ON; // Create local copy for ref parameter
                    _powerNotificationHandle = RegisterPowerSettingNotification(
                        _mainWindowHandle,
                        ref powerGuid,
                        0 // DEVICE_NOTIFY_WINDOW_HANDLE
                    );
                    
                    if (_powerNotificationHandle == IntPtr.Zero)
                    {
                        _logger.LogWarning("❌ Failed to register power setting notifications - Win32 error: {0}", Marshal.GetLastWin32Error());
                    }
                    else
                    {
                        _logger.LogInformation("✅ Power setting notifications registered successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering power notifications");
            }
        }
        
        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (msg)
                {
                    case WM_WTSSESSION_CHANGE:
                        HandleSessionChange((int)wParam);
                        break;
                        
                    case WM_POWERBROADCAST:
                        if ((int)wParam == PBT_POWERSETTINGCHANGE)
                        {
                            HandlePowerSettingChange(lParam);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in window procedure");
            }
            
            // Call original window procedure
            return CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
        }
        
        private void HandleSessionChange(int eventType)
        {
            try
            {
                var newState = _currentState;
                
                switch (eventType)
                {
                    case WTS_SESSION_LOCK:
                        newState = UserPresenceState.Away;
                        _logger.LogInformation("🔒 Session locked - user marked as away");
                        break;
                        
                    case WTS_SESSION_UNLOCK:
                        newState = UserPresenceState.Present;
                        _logger.LogInformation("🔓 Session unlocked - user marked as present");
                        
                        // CRITICAL FIX: Trigger timer recovery after session unlock (potential system resume)
                        if (_timerService != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Add a brief delay to ensure system is stable after unlock
                                    await Task.Delay(1000);
                                    await _timerService.RecoverFromSystemResumeAsync("Session unlocked - potential system resume");
                                    _logger.LogCritical($"🔓 Recovery completed: IsRunning={_timerService.IsRunning}, ManuallyPaused={_timerService.IsManuallyPaused}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "🔓 Error during timer recovery after session unlock");
                                }
                            });
                        }
                        break;
                        
                    case WTS_CONSOLE_DISCONNECT:
                        newState = UserPresenceState.Away;
                        _logger.LogInformation("💻 Console disconnected - user marked as away");
                        break;
                        
                    case WTS_CONSOLE_CONNECT:
                        newState = UserPresenceState.Present;
                        _logger.LogInformation("💻 Console connected - user marked as present");
                        
                        // CRITICAL FIX: Trigger timer recovery after console connect (potential system resume)
                        if (_timerService != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Add a brief delay to ensure system is stable after console connect
                                    await Task.Delay(1000);
                                    await _timerService.RecoverFromSystemResumeAsync("Console connected - potential system resume");
                                    _logger.LogCritical($"💻 Recovery completed: IsRunning={_timerService.IsRunning}, ManuallyPaused={_timerService.IsManuallyPaused}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "💻 Error during timer recovery after console connect");
                                }
                            });
                        }
                        break;
                }
                
                if (newState != _currentState)
                {
                    UpdatePresenceState(newState, TimeSpan.Zero);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling session change");
            }
        }
        
        private void HandlePowerSettingChange(IntPtr lParam)
        {
            try
            {
                var powerSetting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
                
                if (powerSetting.PowerSetting.Equals(GUID_MONITOR_POWER_ON))
                {
                    var monitorState = Marshal.ReadInt32(lParam + Marshal.SizeOf<POWERBROADCAST_SETTING>());
                    
                    if (monitorState == 0) // Monitor off
                    {
                        _logger.LogInformation("🖥️ Monitor turned off - user marked as away");
                        UpdatePresenceState(UserPresenceState.Away, TimeSpan.Zero);
                    }
                    else if (monitorState == 1) // Monitor on
                    {
                        _logger.LogInformation("🖥️ Monitor turned on - user marked as present");
                        UpdatePresenceState(UserPresenceState.Present, TimeSpan.Zero);
                        
                        // CRITICAL FIX: Trigger timer recovery after potential system resume
                        if (_timerService != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // Add a brief delay to ensure system is stable after monitor power on
                                    await Task.Delay(1000);
                                    await _timerService.RecoverFromSystemResumeAsync("Monitor power on - potential system resume");
                                    _logger.LogCritical($"🖥️ Recovery completed: IsRunning={_timerService.IsRunning}, ManuallyPaused={_timerService.IsManuallyPaused}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "🖥️ Error during timer recovery after monitor power on");
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling power setting change");
            }
        }
        
        private void UpdatePresenceState(UserPresenceState newState, TimeSpan idleDuration)
        {
            lock (_stateLock)
            {
                if (newState != _currentState)
                {
                    var previousState = _currentState;
                    var now = DateTime.Now;
                    
                    // NEW: Track extended away periods for smart session reset
                    HandleExtendedAwayTracking(previousState, newState, now);
                    
                    _currentState = newState;
                    _lastStateChange = now;
                    
                    var eventArgs = new UserPresenceEventArgs
                    {
                        PreviousState = previousState,
                        CurrentState = newState,
                        StateChangedAt = now,
                        IdleDuration = idleDuration
                    };
                    
                    UserPresenceChanged?.Invoke(this, eventArgs);
                }
            }
        }

        private async void HandleExtendedAwayTracking(UserPresenceState previousState, UserPresenceState newState, DateTime now)
        {
            try
            {
                var config = await _configurationService.LoadConfigurationAsync();
                var extendedAwayThresholdMinutes = config.UserPresence.ExtendedAwayThresholdMinutes;
            
            // User is going away (Present/Idle → Away/SystemSleep)
            if ((previousState == UserPresenceState.Present || previousState == UserPresenceState.Idle) &&
                (newState == UserPresenceState.Away || newState == UserPresenceState.SystemSleep))
            {
                _awayStartTime = now;
                _hasBeenAwayExtended = false;
                _logger.LogDebug($"🏃 User going away - tracking start time: {_awayStartTime:HH:mm:ss}");
            }

            // P0 FIX: User is going idle (Present → Idle)
            // Track idle start time to detect extended idle periods (e.g., user leaves PC without locking)
            else if (previousState == UserPresenceState.Present && newState == UserPresenceState.Idle)
            {
                _idleStartTime = now;
                _hasBeenAwayExtended = false;
                _logger.LogInformation($"⏱️ P0 FIX - IDLE START: User went idle at {now:HH:mm:ss} - tracking for extended idle detection");
            }

            // User is returning (Away/SystemSleep → Present)
            else if ((previousState == UserPresenceState.Away || previousState == UserPresenceState.SystemSleep) &&
                     newState == UserPresenceState.Present)
            {
                if (_awayStartTime != default(DateTime))
                {
                    var awayDuration = now - _awayStartTime;
                    _totalAwayTime = awayDuration;
                    
                    _logger.LogInformation($"🏠 User returned after {awayDuration.TotalMinutes:F1} minutes away");
                    
                    // Check if this was an extended away period requiring smart session reset
                    if (awayDuration.TotalMinutes >= extendedAwayThresholdMinutes && !_hasBeenAwayExtended)
                    {
                        _hasBeenAwayExtended = true;
                        _logger.LogInformation($"⚡ Extended away period detected: {awayDuration.TotalMinutes:F1} minutes - triggering smart session reset");
                        
                        var extendedAwayArgs = new ExtendedAwayEventArgs
                        {
                            TotalAwayTime = awayDuration,
                            AwayStartTime = _awayStartTime,
                            ReturnTime = now,
                            AwayState = previousState
                        };
                        
                        ExtendedAwaySessionDetected?.Invoke(this, extendedAwayArgs);
                    }
                    
                    // Reset tracking for next away period
                    _awayStartTime = default(DateTime);
                }
            }

            // P0 FIX: User is returning from idle (Idle → Present)
            // Check if idle duration exceeds extended away threshold to trigger session reset
            // This handles the common case where user leaves PC idle without locking session
            else if (previousState == UserPresenceState.Idle && newState == UserPresenceState.Present)
            {
                if (_idleStartTime != default(DateTime))
                {
                    var idleDuration = now - _idleStartTime;
                    _totalAwayTime = idleDuration;

                    _logger.LogInformation($"⏱️ P0 FIX - IDLE END: User was idle for {idleDuration.TotalMinutes:F1} minutes (threshold: {extendedAwayThresholdMinutes}min)");

                    // Check if this was an extended idle period requiring smart session reset
                    if (idleDuration.TotalMinutes >= extendedAwayThresholdMinutes && !_hasBeenAwayExtended)
                    {
                        _hasBeenAwayExtended = true;
                        _logger.LogCritical($"⚡ P0 FIX - EXTENDED IDLE DETECTED: {idleDuration.TotalMinutes:F1} minutes idle (threshold: {extendedAwayThresholdMinutes}min) - triggering smart session reset");

                        var extendedAwayArgs = new ExtendedAwayEventArgs
                        {
                            TotalAwayTime = idleDuration,
                            AwayStartTime = _idleStartTime,
                            ReturnTime = now,
                            AwayState = previousState  // Idle
                        };

                        ExtendedAwaySessionDetected?.Invoke(this, extendedAwayArgs);
                    }
                    else
                    {
                        _logger.LogInformation($"⏱️ P0 FIX - IDLE DURATION OK: {idleDuration.TotalMinutes:F1} minutes is below threshold {extendedAwayThresholdMinutes}min - no session reset needed");
                    }

                    // Reset idle tracking for next idle period
                    _idleStartTime = default(DateTime);
                }
                else
                {
                    _logger.LogWarning($"⏱️ P0 FIX - IDLE END: User returned from idle but _idleStartTime was not set (unexpected state)");
                }
            }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling extended away tracking");
            }
        }
        
        private bool IsScreenSaverActive()
        {
            try
            {
                if (SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, out bool isActive, 0))
                {
                    return isActive;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        private bool IsWorkstationLocked()
        {
            try
            {
                var hDesktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
                if (hDesktop == IntPtr.Zero)
                {
                    return true; // Likely locked
                }
                
                CloseDesktop(hDesktop);
                return false;
            }
            catch
            {
                return false;
            }
        }

        #region Windows API Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionId;
            public IntPtr pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public int DataLength;
        }
        
        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }
        
        private enum WTSInfoClass
        {
            WTSSessionInfo = 24
        }
        
        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
        private const int WTS_CURRENT_SESSION = -1;
        private const int SPI_GETSCREENSAVERRUNNING = 0x0072;
        private const int DESKTOP_SWITCHDESKTOP = 0x0100;

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        
        [DllImport("wtsapi32.dll")]
        private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);
        
        [DllImport("wtsapi32.dll")]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
        
        [DllImport("wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTSInfoClass wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);
        
        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);
        
        [DllImport("user32.dll")]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, int Flags);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(int uAction, int uParam, out bool lpvParam, int fuWinIni);
        
        [DllImport("user32.dll")]
        private static extern IntPtr OpenInputDesktop(int dwFlags, bool fInherit, int dwDesiredAccess);
        
        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        #endregion
    }
}