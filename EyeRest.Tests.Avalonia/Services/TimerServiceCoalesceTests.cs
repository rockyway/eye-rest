using System;
using System.Reflection;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Tests.Avalonia.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Tests the eye-rest → break coalescing logic. When a break is about to fire within
    /// the eye-rest occupancy window (warning + duration + buffer), the eye-rest tick
    /// should be skipped so the break fires alone instead of producing back-to-back popups.
    /// </summary>
    public class TimerServiceCoalesceTests : IDisposable
    {
        private readonly FakeTimerFactory _fakeTimerFactory = new();
        private readonly FakeDispatcherService _fakeDispatcher = new();
        private readonly FakeClock _fakeClock = new();
        private readonly Mock<IConfigurationService> _mockConfigService = new();
        private readonly Mock<IAnalyticsService> _mockAnalyticsService = new();
        private readonly Mock<IPauseReminderService> _mockPauseReminderService = new();
        private readonly Mock<INotificationService> _mockNotificationService = new();
        private readonly TimerService _timerService;

        private const int EyeRestTimerIndex = 0;
        private const int BreakTimerIndex = 1;
        private const int EyeRestWarningTimerIndex = 2;

        public TimerServiceCoalesceTests()
        {
            // Worst-case collision setup: 20-min eye rest, 60-min break.
            // After two eye-rest cycles, the third eye-rest tick collides with break.
            var config = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    WarningEnabled = true,
                    WarningSeconds = 15
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 60,
                    DurationMinutes = 5,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                UserPresence = new UserPresenceSettings { ExtendedAwayThresholdMinutes = 30 }
            };

            _mockConfigService.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(config);
            _mockAnalyticsService.Setup(a => a.RecordSessionStartAsync()).Returns(Task.CompletedTask);
            _mockAnalyticsService.Setup(a => a.RecordPauseEventAsync(It.IsAny<PauseReason>())).Returns(Task.CompletedTask);
            _mockAnalyticsService.Setup(a => a.RecordResumeEventAsync(It.IsAny<ResumeReason>())).Returns(Task.CompletedTask);
            _mockPauseReminderService.Setup(p => p.OnTimersPausedAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            _mockPauseReminderService.Setup(p => p.OnTimersResumedAsync()).Returns(Task.CompletedTask);
            _mockNotificationService.Setup(n => n.IsAnyPopupActive).Returns(false);

            _timerService = new TimerService(
                NullLogger<TimerService>.Instance,
                _mockConfigService.Object,
                _mockAnalyticsService.Object,
                _fakeTimerFactory,
                _mockPauseReminderService.Object,
                _fakeDispatcher,
                _fakeClock);
            _timerService.SetNotificationService(_mockNotificationService.Object);
        }

        public void Dispose() => (_timerService as IDisposable)?.Dispose();

        [Fact]
        public async Task EyeRestTick_WhenBreakImminent_SkipsEyeRestAndDoesNotStartWarning()
        {
            // Arrange: start the service. Then force the break timer's start time so that
            // its remaining time is within the coalesce window (warning 15 + duration 20 + buffer 5 = 40s).
            await _timerService.StartAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var eyeRestWarningTimer = timers[EyeRestWarningTimerIndex];

            // Make break "due in 30s": _breakStartTime = now - (60min - 30s)
            var breakInterval = TimeSpan.FromMinutes(60);
            SetPrivateField("_breakStartTime", DateTime.Now - (breakInterval - TimeSpan.FromSeconds(30)));

            // Eye rest must look like it's been running long enough to legitimately tick (avoid the
            // "elapsed too short" guard which rejects sub-50% intervals).
            SetPrivateField("_eyeRestStartTime", DateTime.Now - TimeSpan.FromMinutes(20));

            // Act: fire the eye-rest tick.
            eyeRestTimer.FireTick();

            // Assert: the warning timer must NOT have been started.
            Assert.False(GetPrivateField<bool>("_isEyeRestNotificationActive"),
                "Eye-rest warning should NOT have started — coalesce should have skipped it.");
        }

        [Fact]
        public async Task EyeRestTick_WhenBreakFarAway_FiresEyeRestNormally()
        {
            await _timerService.StartAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var eyeRestWarningTimer = timers[EyeRestWarningTimerIndex];

            // Break has ~40 minutes remaining → far outside the coalesce window.
            var breakInterval = TimeSpan.FromMinutes(60);
            SetPrivateField("_breakStartTime", DateTime.Now - (breakInterval - TimeSpan.FromMinutes(40)));
            SetPrivateField("_eyeRestStartTime", DateTime.Now - TimeSpan.FromMinutes(20));

            // Act
            eyeRestTimer.FireTick();

            // Assert: the warning timer should be running (normal flow).
            Assert.True(GetPrivateField<bool>("_isEyeRestNotificationActive"),
                "Eye-rest warning must run normally when break is far away.");
        }

        [Fact]
        public async Task EyeRestTick_WhenBreakDelayed_FiresEyeRestNormally()
        {
            await _timerService.StartAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var eyeRestWarningTimer = timers[EyeRestWarningTimerIndex];

            // Force "imminent break" timing AND mark IsBreakDelayed = true. Coalesce should be
            // suppressed because a delayed break is the user's explicit choice.
            var breakInterval = TimeSpan.FromMinutes(60);
            SetPrivateField("_breakStartTime", DateTime.Now - (breakInterval - TimeSpan.FromSeconds(20)));
            SetPrivateField("_eyeRestStartTime", DateTime.Now - TimeSpan.FromMinutes(20));

            // Set IsBreakDelayed via property-setter: it has a private setter through state file.
            // We set _delayDuration / _delayStartTime + IsBreakDelayed flag directly.
            SetIsBreakDelayed(true);

            // Act
            eyeRestTimer.FireTick();

            // Assert: normal eye-rest flow proceeds (warning timer running).
            Assert.True(GetPrivateField<bool>("_isEyeRestNotificationActive"),
                "Eye-rest must run normally when break is in user-delayed state.");
        }

        [Fact]
        public async Task EyeRestTick_WhenBreakTimerDisabled_FiresEyeRestNormally()
        {
            await _timerService.StartAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var breakTimer = timers[BreakTimerIndex];
            var eyeRestWarningTimer = timers[EyeRestWarningTimerIndex];

            // Break timer disabled → no collision risk regardless of remaining time.
            breakTimer.Stop();
            SetPrivateField("_eyeRestStartTime", DateTime.Now - TimeSpan.FromMinutes(20));

            // Act
            eyeRestTimer.FireTick();

            // Assert: normal eye-rest flow.
            Assert.True(GetPrivateField<bool>("_isEyeRestNotificationActive"),
                "When the break timer is disabled, coalesce must NOT trigger; eye rest runs normally.");
        }

        [Fact]
        public async Task EyeRestTick_AfterCoalesce_ReArmsEyeRestTimer()
        {
            await _timerService.StartAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];

            var breakInterval = TimeSpan.FromMinutes(60);
            SetPrivateField("_breakStartTime", DateTime.Now - (breakInterval - TimeSpan.FromSeconds(20)));
            SetPrivateField("_eyeRestStartTime", DateTime.Now - TimeSpan.FromMinutes(20));

            // Act: fire the eye-rest tick. Coalesce should fire and re-arm the eye-rest timer.
            eyeRestTimer.FireTick();

            // Assert: eye-rest timer is re-armed (running) — RestartEyeRestTimerAfterCompletion
            // sets IsEnabled = true so the user isn't left without an eye-rest schedule if the
            // break is somehow cancelled before firing.
            Assert.True(eyeRestTimer.IsEnabled,
                "After coalesce, the eye-rest timer must be re-armed for the next cycle.");
        }

        // ---- Reflection helpers ----

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(TimerService).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field!.SetValue(_timerService, value);
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var field = typeof(TimerService).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(_timerService)!;
        }

        private void SetIsBreakDelayed(bool value)
        {
            // IsBreakDelayed has a public getter and an internal setter through reflection on
            // the backing field. The State partial uses a backing field named "_isBreakDelayed"
            // or auto-property; try the property first, then the field.
            var prop = typeof(TimerService).GetProperty(nameof(ITimerService.IsBreakDelayed));
            if (prop?.CanWrite == true)
            {
                prop.SetValue(_timerService, value);
                return;
            }
            // Fall back to backing field of an auto-property.
            var field = typeof(TimerService).GetField("<IsBreakDelayed>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field!.SetValue(_timerService, value);
        }
    }
}
