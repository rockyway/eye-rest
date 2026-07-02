using EyeRest.Models;
using EyeRest.Platform.Linux.Interop;
using Microsoft.Extensions.Logging;
using Timer = System.Threading.Timer;

namespace EyeRest.Services
{
    /// <summary>
    /// Linux implementation of <see cref="IUserPresenceService"/>.
    /// Uses the X11 MIT-SCREEN-SAVER extension (libXss) for idle time detection
    /// and polling via System.Threading.Timer. The polling state machine is the
    /// same as the macOS implementation; only the idle probe differs.
    ///
    /// On sessions without X11 (pure Wayland, headless) the probe degrades to
    /// "always present" — timers keep running, smart pause is simply inactive.
    /// </summary>
    public class LinuxUserPresenceService : IUserPresenceService
    {
        private readonly ILogger<LinuxUserPresenceService> _logger;
        private readonly IConfigurationService _configurationService;

        // Source of "time since last input". Production uses the X11 screensaver-extension
        // reading; tests inject a deterministic provider so the polling state machine can
        // be exercised without X11.
        private readonly Func<TimeSpan> _idleTimeProvider;

        private Timer? _pollingTimer;
        private ITimerService? _timerService;
        private bool _disposed;
        private bool _idleProbeWarned;

        private UserPresenceState _currentState = UserPresenceState.Present;
        private DateTime _lastStateChangeTime = DateTime.UtcNow;
        private DateTime _awayStartTime = DateTime.MinValue;
        private TimeSpan _lastAwayDuration = TimeSpan.Zero;
        private TimeSpan _totalAwayTime = TimeSpan.Zero;

        // IdleThreshold is driven by UserPresence.IdleTimeoutMinutes (the slider in the
        // Advanced settings tab). AwayThreshold is held at idle + 10 min so the existing
        // Idle → Away progression is preserved regardless of user-chosen idle timeout.
        private TimeSpan _idleThreshold = TimeSpan.FromMinutes(15);
        private TimeSpan _awayThreshold = TimeSpan.FromMinutes(25);
        private static readonly TimeSpan ExtendedAwayThreshold = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

        public LinuxUserPresenceService(
            ILogger<LinuxUserPresenceService> logger,
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

            _logger.LogInformation("Starting Linux user presence monitoring (idle threshold: {Idle}min)", _idleThreshold.TotalMinutes);
            _pollingTimer = new Timer(PollUserPresence, null, TimeSpan.Zero, PollingInterval);
        }

        public Task StopMonitoringAsync()
        {
            _logger.LogInformation("Stopping Linux user presence monitoring");
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

        /// <summary>
        /// Gets the system idle time from the X11 MIT-SCREEN-SAVER extension —
        /// how long since the last physical keyboard/mouse event in this session.
        /// Returns zero (user present) when X11 is unavailable.
        /// </summary>
        private TimeSpan GetSystemIdleTime()
        {
            var idle = X11IdleProbe.GetIdleTime();
            if (X11IdleProbe.IsUnavailable && !_idleProbeWarned)
            {
                _idleProbeWarned = true;
                _logger.LogWarning(
                    "X11 idle probe unavailable (no X11 display or MIT-SCREEN-SAVER extension) — " +
                    "smart pause / presence detection is disabled for this session");
            }
            return idle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _configurationService.ConfigurationChanged -= OnConfigurationChanged;

            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _logger.LogInformation("LinuxUserPresenceService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
