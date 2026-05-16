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
    /// Tests for the manual "Break Now" trigger path:
    /// <see cref="ITimerService.TriggerImmediateBreakAsync"/>.
    /// </summary>
    public class TimerServiceImmediateBreakTests : IDisposable
    {
        private readonly FakeTimerFactory _fakeTimerFactory = new();
        private readonly FakeDispatcherService _fakeDispatcher = new();
        private readonly FakeClock _fakeClock = new();
        private readonly Mock<IConfigurationService> _mockConfigService = new();
        private readonly Mock<IAnalyticsService> _mockAnalyticsService = new();
        private readonly Mock<IPauseReminderService> _mockPauseReminderService = new();
        private readonly Mock<INotificationService> _mockNotificationService = new();
        private readonly TimerService _timerService;

        public TimerServiceImmediateBreakTests()
        {
            var config = new AppConfiguration
            {
                EyeRest = new EyeRestSettings { IntervalMinutes = 20, DurationSeconds = 20, WarningEnabled = true, WarningSeconds = 15 },
                Break = new BreakSettings { IntervalMinutes = 55, DurationMinutes = 5, WarningEnabled = true, WarningSeconds = 30 },
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
        public async Task TriggerImmediateBreakAsync_WhenNotRunning_DoesNotFireBreakDue()
        {
            // Arrange: service not started
            TimerEventArgs? captured = null;
            _timerService.BreakDue += (_, e) => captured = e;

            // Act
            await _timerService.TriggerImmediateBreakAsync();

            // Assert
            Assert.Null(captured);
        }

        [Fact]
        public async Task TriggerImmediateBreakAsync_WhenRunning_FiresBreakDueWithManualSource()
        {
            // Arrange
            await _timerService.StartAsync();
            TimerEventArgs? captured = null;
            _timerService.BreakDue += (_, e) => captured = e;

            // Act
            await _timerService.TriggerImmediateBreakAsync();

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(TimerType.Break, captured!.Type);
            Assert.Equal(BreakTriggerSource.Manual, captured.Source);
            Assert.Equal(TimeSpan.FromMinutes(5), captured.NextInterval);
        }

        [Fact]
        public void TimerEventArgs_DefaultSource_IsAutomatic()
        {
            // The default for the auto-trigger path: every existing call site that
            // constructs TimerEventArgs without setting Source must inherit Automatic.
            var args = new TimerEventArgs { Type = TimerType.Break };
            Assert.Equal(BreakTriggerSource.Automatic, args.Source);
        }

        [Fact]
        public async Task TriggerImmediateBreakAsync_WhenManuallyPaused_StillFiresBreakDue()
        {
            // Arrange: start, then manual pause
            await _timerService.StartAsync();
            await _timerService.PauseForDurationAsync(TimeSpan.FromMinutes(30), "Meeting");
            Assert.True(_timerService.IsManuallyPaused);

            TimerEventArgs? captured = null;
            _timerService.BreakDue += (_, e) => captured = e;

            // Act
            await _timerService.TriggerImmediateBreakAsync();

            // Assert: pause guard bypassed
            Assert.NotNull(captured);
            Assert.Equal(BreakTriggerSource.Manual, captured!.Source);
        }
    }
}
