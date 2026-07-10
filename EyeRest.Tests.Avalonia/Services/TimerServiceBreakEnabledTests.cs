using System;
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
    /// Tests for eye-rest-only mode: when <see cref="BreakSettings.Enabled"/> is false the
    /// automatic break flow is suppressed while manual "Break Now" keeps working.
    /// </summary>
    [Collection(TimerServiceStaticStateCollection.Name)]
    public class TimerServiceBreakEnabledTests : IDisposable
    {
        private readonly FakeTimerFactory _fakeTimerFactory = new();
        private readonly FakeDispatcherService _fakeDispatcher = new();
        private readonly FakeClock _fakeClock = new();
        private readonly Mock<IConfigurationService> _mockConfigService = new();
        private readonly Mock<IAnalyticsService> _mockAnalyticsService = new();
        private readonly Mock<IPauseReminderService> _mockPauseReminderService = new();
        private readonly Mock<INotificationService> _mockNotificationService = new();
        private readonly AppConfiguration _config;
        private readonly TimerService _timerService;

        public TimerServiceBreakEnabledTests()
        {
            _config = new AppConfiguration
            {
                EyeRest = new EyeRestSettings { IntervalMinutes = 20, DurationSeconds = 20, WarningEnabled = true, WarningSeconds = 15 },
                Break = new BreakSettings { Enabled = true, IntervalMinutes = 55, DurationMinutes = 5, WarningEnabled = true, WarningSeconds = 30 },
                UserPresence = new UserPresenceSettings { ExtendedAwayThresholdMinutes = 30 }
            };
            _mockConfigService.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(_config);
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

            // TimerService's break/eye-rest processing guards are process-wide statics, so a
            // sibling test that fired a break can leave them set. Reset before we start (and on
            // tear-down) so the manual-break assertion isn't blocked by an inherited lock.
            ResetGlobalProcessingFlags();
        }

        public void Dispose()
        {
            (_timerService as IDisposable)?.Dispose();
            ResetGlobalProcessingFlags();
        }

        private static void ResetGlobalProcessingFlags()
        {
            var t = typeof(TimerService);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            foreach (var name in new[]
            {
                "_isAnyEyeRestEventProcessing", "_isAnyBreakEventProcessing",
                "_isAnyEyeRestWarningProcessing", "_isAnyBreakWarningProcessing",
            })
            {
                t.GetField(name, flags)?.SetValue(null, false);
            }
            foreach (var name in new[]
            {
                "_atomicEyeRestProcessing", "_atomicBreakProcessing",
                "_atomicEyeRestWarningProcessing", "_atomicBreakWarningProcessing",
            })
            {
                t.GetField(name, flags)?.SetValue(null, 0);
            }
        }

        // FakeTimerFactory creation order after StartAsync: [0] eyeRest, [1] break,
        // [2] eyeRestWarning, [3] breakWarning, [4] healthMonitor.
        private FakeTimer BreakTimer => _fakeTimerFactory.GetCreatedTimers()[1];

        [Fact]
        public void BreakSettings_Enabled_DefaultsTrue()
        {
            Assert.True(new BreakSettings().Enabled);
        }

        [Fact]
        public async Task StartAsync_WhenBreakDisabled_DoesNotStartBreakTimer()
        {
            _config.Break.Enabled = false;

            await _timerService.StartAsync();

            Assert.True(_timerService.IsRunning);
            Assert.False(BreakTimer.IsEnabled);
        }

        [Fact]
        public async Task StartAsync_WhenBreakEnabled_StartsBreakTimer()
        {
            await _timerService.StartAsync();

            Assert.True(BreakTimer.IsEnabled);
        }

        [Fact]
        public async Task StartBreakWarningTimer_WhenBreakDisabled_DoesNotFireBreakWarning()
        {
            _config.Break.Enabled = false;
            await _timerService.StartAsync();
            TimerEventArgs? captured = null;
            _timerService.BreakWarning += (_, e) => captured = e;

            _timerService.StartBreakWarningTimer();

            Assert.Null(captured);
        }

        [Fact]
        public async Task StartBreakWarningTimer_WhenBreakEnabled_FiresBreakWarning()
        {
            await _timerService.StartAsync();
            TimerEventArgs? captured = null;
            _timerService.BreakWarning += (_, e) => captured = e;

            _timerService.StartBreakWarningTimer();

            Assert.NotNull(captured);
            Assert.Equal(TimerType.BreakWarning, captured!.Type);
        }

        [Fact]
        public async Task TriggerImmediateBreakAsync_WhenBreakDisabled_StillFiresBreakDue()
        {
            // Manual "Break Now" must bypass the eye-rest-only gate.
            _config.Break.Enabled = false;
            await _timerService.StartAsync();
            TimerEventArgs? captured = null;
            _timerService.BreakDue += (_, e) => captured = e;

            // Clear the process-wide break guard right before the act so a parallel sibling's
            // leaked lock can't spuriously short-circuit TriggerBreak.
            ResetGlobalProcessingFlags();
            await _timerService.TriggerImmediateBreakAsync();

            Assert.NotNull(captured);
            Assert.Equal(BreakTriggerSource.Manual, captured!.Source);
        }

        [Fact]
        public async Task UpdateConfiguration_DisablingBreakWhileRunning_StopsBreakTimer()
        {
            await _timerService.StartAsync();
            Assert.True(BreakTimer.IsEnabled);

            var updated = CloneWithBreakEnabled(false);
            _timerService.UpdateConfiguration(updated);

            Assert.False(BreakTimer.IsEnabled);
        }

        [Fact]
        public async Task UpdateConfiguration_DisablingBreak_ThenWarning_IsSuppressed()
        {
            await _timerService.StartAsync();
            _timerService.UpdateConfiguration(CloneWithBreakEnabled(false));

            TimerEventArgs? captured = null;
            _timerService.BreakWarning += (_, e) => captured = e;
            _timerService.StartBreakWarningTimer();

            Assert.Null(captured);
        }

        [Fact]
        public async Task UpdateConfiguration_ReEnablingBreakWhileRunning_RestartsBreakTimer()
        {
            _config.Break.Enabled = false;
            await _timerService.StartAsync();
            Assert.False(BreakTimer.IsEnabled);

            _timerService.UpdateConfiguration(CloneWithBreakEnabled(true));

            Assert.True(BreakTimer.IsEnabled);
        }

        [Fact]
        public async Task DisableWhileBreakWarningActive_ResumesEyeRestTimer()
        {
            // H2 regression: a break warning pauses the eye-rest timer; disabling breaks
            // mid-warning must re-arm eye-rest rather than leave it paused forever.
            await _timerService.StartAsync();
            var eyeRestTimer = _fakeTimerFactory.GetCreatedTimers()[0];
            Assert.True(eyeRestTimer.IsEnabled);

            // Start a break warning — SmartPauseEyeRestTimerForBreak stops the eye-rest timer.
            _timerService.StartBreakWarningTimer();
            Assert.False(eyeRestTimer.IsEnabled);

            // Disable breaks during the warning countdown.
            _timerService.UpdateConfiguration(CloneWithBreakEnabled(false));

            Assert.True(eyeRestTimer.IsEnabled);
            Assert.False(GetPrivateField<bool>("_eyeRestTimerPausedForBreak"));
        }

        [Fact]
        public async Task OnBreakTimerTick_WhenDisabled_SelfStopsAndDoesNotWarn()
        {
            // A stray restart of the break timer in eye-rest-only mode must be neutralized:
            // the gated tick fires nothing and stops the timer.
            _config.Break.Enabled = false;
            await _timerService.StartAsync();
            BreakTimer.Start(); // simulate a stray restart from some resume/recovery path
            Assert.True(BreakTimer.IsEnabled);

            TimerEventArgs? warned = null;
            _timerService.BreakWarning += (_, e) => warned = e;

            BreakTimer.FireTick();

            Assert.Null(warned);
            Assert.False(BreakTimer.IsEnabled);
        }

        [Fact]
        public async Task DisableLive_ThenManualBreak_StillFiresBreakDue()
        {
            await _timerService.StartAsync();
            _timerService.UpdateConfiguration(CloneWithBreakEnabled(false));

            TimerEventArgs? captured = null;
            _timerService.BreakDue += (_, e) => captured = e;

            ResetGlobalProcessingFlags();
            await _timerService.TriggerImmediateBreakAsync();

            Assert.NotNull(captured);
            Assert.Equal(BreakTriggerSource.Manual, captured!.Source);
        }

        [Fact]
        public async Task ReEnableWhilePaused_DoesNotStartBreakTimerButSeedsRemaining()
        {
            // agy H#3 regression: re-enabling while paused must NOT start the break timer, and
            // must seed a full remaining interval so resume doesn't fire an instant break
            // (disable had left _breakRemainingTime == 0).
            _config.Break.Enabled = false;
            await _timerService.StartAsync();
            await _timerService.PauseForDurationAsync(TimeSpan.FromMinutes(30), "Meeting");
            Assert.True(_timerService.IsManuallyPaused);

            _timerService.UpdateConfiguration(CloneWithBreakEnabled(true));

            Assert.False(BreakTimer.IsEnabled);
            Assert.True(GetPrivateField<TimeSpan>("_breakRemainingTime") > TimeSpan.Zero);
        }

        private T GetPrivateField<T>(string name)
        {
            var f = typeof(TimerService).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(f);
            return (T)f!.GetValue(_timerService)!;
        }

        private AppConfiguration CloneWithBreakEnabled(bool enabled)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = _config.EyeRest.IntervalMinutes,
                    DurationSeconds = _config.EyeRest.DurationSeconds,
                    WarningEnabled = _config.EyeRest.WarningEnabled,
                    WarningSeconds = _config.EyeRest.WarningSeconds
                },
                Break = new BreakSettings
                {
                    Enabled = enabled,
                    IntervalMinutes = _config.Break.IntervalMinutes,
                    DurationMinutes = _config.Break.DurationMinutes,
                    WarningEnabled = _config.Break.WarningEnabled,
                    WarningSeconds = _config.Break.WarningSeconds
                },
                UserPresence = new UserPresenceSettings { ExtendedAwayThresholdMinutes = 30 }
            };
        }
    }
}
