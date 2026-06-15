using System;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Models;
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
        private readonly IConfigurationService _configurationService;

        // Source of "seconds since last input". Production uses the native HID-system reading;
        // tests inject a deterministic provider so the polling state machine can be exercised
        // without the (macOS-only) CGEventSource P/Invoke.
        private readonly Func<TimeSpan> _idleTimeProvider;

        private Timer? _pollingTimer;
        private ITimerService? _timerService;
        private bool _disposed;

        private UserPresenceState _currentState = UserPresenceState.Present;
        private DateTime _lastStateChangeTime = DateTime.UtcNow;
        private DateTime _awayStartTime = DateTime.MinValue;
        private TimeSpan _lastAwayDuration = TimeSpan.Zero;
        private TimeSpan _totalAwayTime = TimeSpan.Zero;

        // IdleThreshold is now driven by UserPresence.IdleTimeoutMinutes (the slider in the
        // Advanced settings tab). AwayThreshold is held at idle + 10 min so the existing
        // Idle → Away progression is preserved regardless of user-chosen idle timeout.
        private TimeSpan _idleThreshold = TimeSpan.FromMinutes(15);
        private TimeSpan _awayThreshold = TimeSpan.FromMinutes(25);
        private static readonly TimeSpan ExtendedAwayThreshold = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

        public MacOSUserPresenceService(
            ILogger<MacOSUserPresenceService> logger,
            IConfigurationService configurationService,
            Func<TimeSpan>? idleTimeProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _idleTimeProvider = idleTimeProvider ?? GetSystemIdleTime;
            _configurationService.ConfigurationChanged += OnConfigurationChanged;
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            ApplyPresenceSettings(e.NewConfiguration?.UserPresence);
        }

        private void ApplyPresenceSettings(UserPresenceSettings? settings)
        {
            if (settings == null) return;
            var idleMinutes = Math.Max(1, settings.IdleTimeoutMinutes);
            _idleThreshold = TimeSpan.FromMinutes(idleMinutes);
            _awayThreshold = TimeSpan.FromMinutes(idleMinutes + 10);
            _logger.LogInformation(
                "User presence thresholds updated: idle={Idle}min, away={Away}min (from config)",
                _idleThreshold.TotalMinutes, _awayThreshold.TotalMinutes);
        }

        public event EventHandler<UserPresenceEventArgs>? UserPresenceChanged;
        public event EventHandler<ExtendedAwayEventArgs>? ExtendedAwaySessionDetected;

        public bool IsUserPresent => _currentState == UserPresenceState.Present;
        public TimeSpan IdleTime => _idleTimeProvider();
        public UserPresenceState CurrentState => _currentState;
        public TimeSpan TotalAwayTime => _totalAwayTime;

        public async Task StartMonitoringAsync()
        {
            if (_pollingTimer != null)
            {
                _logger.LogDebug("User presence monitoring already started");
                return;
            }

            // Seed thresholds from the current persisted configuration before polling.
            try
            {
                var config = await _configurationService.LoadConfigurationAsync();
                ApplyPresenceSettings(config?.UserPresence);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load presence settings; falling back to defaults (idle={Idle}min)", _idleThreshold.TotalMinutes);
            }

            _logger.LogInformation("Starting macOS user presence monitoring (idle threshold: {Idle}min)", _idleThreshold.TotalMinutes);
            _pollingTimer = new Timer(PollUserPresence, null, TimeSpan.Zero, PollingInterval);
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

        private void PollUserPresence(object? state) => EvaluatePresence();

        /// <summary>
        /// Reads the current idle time and raises presence-change events when the user crosses
        /// the idle / away thresholds. Invoked by the polling timer; <c>internal</c> so tests can
        /// drive it deterministically with an injected idle provider.
        /// </summary>
        internal void EvaluatePresence()
        {
            try
            {
                var idleTime = _idleTimeProvider();
                var previousState = _currentState;
                UserPresenceState newState;

                if (idleTime >= _awayThreshold)
                {
                    newState = UserPresenceState.Away;
                }
                else if (idleTime >= _idleThreshold)
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

        // Event-source state queried for idle time. MUST be the HID-system source (hardware
        // input only). The combined-session source is reset by non-HID session events — notably
        // NotificationCenter display-wakes — which produced phantom "user returned" events while
        // the user was physically away (root-caused 2026-06-02 via pmset DisplayWake correlation).
        internal const int IdleEventSourceStateId = CoreGraphics.kCGEventSourceStateHIDSystemState;

        /// <summary>
        /// Gets the system idle time using CGEventSourceSecondsSinceLastEventType against the
        /// HID-system event source. Returns how long since the last physical keyboard/mouse/trackpad event.
        /// </summary>
        private static TimeSpan GetSystemIdleTime()
        {
            try
            {
                var seconds = CoreGraphics.CGEventSourceSecondsSinceLastEventType(
                    IdleEventSourceStateId,
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

            _configurationService.ConfigurationChanged -= OnConfigurationChanged;

            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _logger.LogInformation("MacOSUserPresenceService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
