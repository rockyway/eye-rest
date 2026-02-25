using System;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="IUserPresenceService"/>.
    /// Uses CGEventSourceSecondsSinceLastEventType for idle time detection
    /// and polling via System.Threading.Timer.
    /// </summary>
    public class MacOSUserPresenceService : IUserPresenceService
    {
        private readonly ILogger<MacOSUserPresenceService> _logger;
        private Timer? _pollingTimer;
        private ITimerService? _timerService;
        private bool _disposed;

        private UserPresenceState _currentState = UserPresenceState.Present;
        private DateTime _lastStateChangeTime = DateTime.UtcNow;
        private DateTime _awayStartTime = DateTime.MinValue;
        private TimeSpan _lastAwayDuration = TimeSpan.Zero;
        private TimeSpan _totalAwayTime = TimeSpan.Zero;

        // Thresholds for presence detection
        private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AwayThreshold = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ExtendedAwayThreshold = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

        public MacOSUserPresenceService(ILogger<MacOSUserPresenceService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<UserPresenceEventArgs>? UserPresenceChanged;
        public event EventHandler<ExtendedAwayEventArgs>? ExtendedAwaySessionDetected;

        public bool IsUserPresent => _currentState == UserPresenceState.Present;
        public TimeSpan IdleTime => GetSystemIdleTime();
        public UserPresenceState CurrentState => _currentState;
        public TimeSpan TotalAwayTime => _totalAwayTime;

        public Task StartMonitoringAsync()
        {
            if (_pollingTimer != null)
            {
                _logger.LogDebug("User presence monitoring already started");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Starting macOS user presence monitoring");
            _pollingTimer = new Timer(PollUserPresence, null, TimeSpan.Zero, PollingInterval);
            return Task.CompletedTask;
        }

        public Task StopMonitoringAsync()
        {
            _logger.LogInformation("Stopping macOS user presence monitoring");
            _pollingTimer?.Dispose();
            _pollingTimer = null;
            return Task.CompletedTask;
        }

        public void SetTimerService(ITimerService timerService)
        {
            _timerService = timerService;
        }

        public TimeSpan GetLastAwayDuration()
        {
            return _lastAwayDuration;
        }

        private void PollUserPresence(object? state)
        {
            try
            {
                var idleTime = GetSystemIdleTime();
                var previousState = _currentState;
                UserPresenceState newState;

                if (idleTime >= AwayThreshold)
                {
                    newState = UserPresenceState.Away;
                }
                else if (idleTime >= IdleThreshold)
                {
                    newState = UserPresenceState.Idle;
                }
                else
                {
                    newState = UserPresenceState.Present;
                }

                if (newState != previousState)
                {
                    _currentState = newState;
                    var now = DateTime.UtcNow;

                    // Track away time
                    if (previousState == UserPresenceState.Away || previousState == UserPresenceState.SystemSleep)
                    {
                        if (_awayStartTime != DateTime.MinValue)
                        {
                            _lastAwayDuration = now - _awayStartTime;
                            _totalAwayTime += _lastAwayDuration;

                            // Check for extended away session
                            if (_lastAwayDuration >= ExtendedAwayThreshold)
                            {
                                _logger.LogInformation(
                                    "Extended away session detected: {Duration:F1} minutes",
                                    _lastAwayDuration.TotalMinutes);

                                ExtendedAwaySessionDetected?.Invoke(this, new ExtendedAwayEventArgs
                                {
                                    TotalAwayTime = _lastAwayDuration,
                                    AwayStartTime = _awayStartTime,
                                    ReturnTime = now,
                                    AwayState = previousState
                                });
                            }
                        }

                        _awayStartTime = DateTime.MinValue;
                    }

                    if (newState == UserPresenceState.Away || newState == UserPresenceState.SystemSleep)
                    {
                        _awayStartTime = now;
                    }

                    _lastStateChangeTime = now;

                    _logger.LogDebug(
                        "User presence changed: {Previous} -> {Current} (idle: {Idle:F1}s)",
                        previousState, newState, idleTime.TotalSeconds);

                    UserPresenceChanged?.Invoke(this, new UserPresenceEventArgs
                    {
                        PreviousState = previousState,
                        CurrentState = newState,
                        StateChangedAt = now,
                        IdleDuration = idleTime
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling user presence");
            }
        }

        /// <summary>
        /// Gets the system idle time using CGEventSourceSecondsSinceLastEventType.
        /// Returns how long since the last keyboard or mouse event.
        /// </summary>
        private static TimeSpan GetSystemIdleTime()
        {
            try
            {
                var seconds = CoreGraphics.CGEventSourceSecondsSinceLastEventType(
                    CoreGraphics.kCGEventSourceStateCombinedSessionState,
                    CoreGraphics.kCGAnyInputEventType);

                return TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                // If CoreGraphics call fails, assume user is present
                return TimeSpan.Zero;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _logger.LogInformation("MacOSUserPresenceService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
