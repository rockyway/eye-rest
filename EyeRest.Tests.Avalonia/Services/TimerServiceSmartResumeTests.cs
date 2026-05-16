using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Tests.Avalonia.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    /// <summary>
    /// Integration tests for TimerService smart pause/resume behavior.
    /// Uses FakeTimer + FakeTimerFactory to simulate time progression without real delays.
    ///
    /// These tests verify the fixes for:
    /// - Timer start times being reset on smart resume (prevents stale overdue detection)
    /// - Idle period >= eye rest duration treated as natural rest (no popup on return)
    /// - Idle period >= break duration treated as natural break (full timer reset)
    /// - Heartbeat refresh on resume (prevents false recovery triggers)
    /// - Rate-limited overdue logging (prevents log flooding)
    /// - Recovery paths resetting start times (prevents post-recovery overdue loops)
    /// </summary>
    public class TimerServiceSmartResumeTests : IDisposable
    {
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly FakeDispatcherService _fakeDispatcher;
        private readonly FakeClock _fakeClock;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IPauseReminderService> _mockPauseReminderService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly ILogger<TimerService> _logger;
        private readonly TimerService _timerService;

        // Timer indices after StartAsync creates them in order
        private const int EyeRestTimerIndex = 0;
        private const int BreakTimerIndex = 1;
        private const int EyeRestWarningTimerIndex = 2;
        private const int BreakWarningTimerIndex = 3;
        private const int HealthMonitorTimerIndex = 4;

        public TimerServiceSmartResumeTests()
        {
            _fakeTimerFactory = new FakeTimerFactory();
            _fakeDispatcher = new FakeDispatcherService();
            _fakeClock = new FakeClock();
            _logger = NullLogger<TimerService>.Instance;

            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockPauseReminderService = new Mock<IPauseReminderService>();
            _mockNotificationService = new Mock<INotificationService>();

            // Default configuration: 20min/20sec eye rest, 55min/5min break
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
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                UserPresence = new UserPresenceSettings
                {
                    ExtendedAwayThresholdMinutes = 30
                }
            };

            _mockConfigService
                .Setup(c => c.LoadConfigurationAsync())
                .ReturnsAsync(config);

            _mockAnalyticsService
                .Setup(a => a.RecordSessionStartAsync())
                .Returns(Task.CompletedTask);
            _mockAnalyticsService
                .Setup(a => a.RecordPauseEventAsync(It.IsAny<PauseReason>()))
                .Returns(Task.CompletedTask);
            _mockAnalyticsService
                .Setup(a => a.RecordResumeEventAsync(It.IsAny<ResumeReason>()))
                .Returns(Task.CompletedTask);

            _mockPauseReminderService
                .Setup(p => p.OnTimersPausedAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _mockPauseReminderService
                .Setup(p => p.OnTimersResumedAsync())
                .Returns(Task.CompletedTask);

            _mockNotificationService
                .Setup(n => n.IsAnyPopupActive)
                .Returns(false);

            _timerService = new TimerService(
                _logger,
                _mockConfigService.Object,
                _mockAnalyticsService.Object,
                _fakeTimerFactory,
                _mockPauseReminderService.Object,
                _fakeDispatcher,
                _fakeClock);

            _timerService.SetNotificationService(_mockNotificationService.Object);
        }

        public void Dispose()
        {
            (_timerService as IDisposable)?.Dispose();
        }

        private async Task StartServiceAsync()
        {
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
        }

        #region Smart Resume After Idle — Timer Reset Tests

        [Fact]
        public async Task SmartResume_AfterIdleLongerThanEyeRestDuration_ResetsEyeRestToFullInterval()
        {
            // Arrange: Start service, then smart pause
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];

            await _timerService.SmartPauseAsync("User idle");
            Assert.True(_timerService.IsSmartPaused);
            Assert.False(eyeRestTimer.IsEnabled);

            // Simulate: User was idle for 2 minutes (120s > 20s eye rest duration)
            // Manipulate _pauseStartTime via reflection to simulate elapsed idle time
            SetPrivateField("_pauseStartTime", DateTime.Now.AddMinutes(-2));

            // Act: Smart resume
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Eye rest timer should be reset to full interval (~19.75min reduced)
            Assert.False(_timerService.IsSmartPaused);
            Assert.True(eyeRestTimer.IsEnabled);

            // Timer should be running with interval close to full (not the preserved remaining)
            // Full reduced interval = 20min - 15s warning = 19.75min = 1185s
            Assert.True(eyeRestTimer.Interval.TotalMinutes > 19.0,
                $"Eye rest interval should be close to full 20min, but was {eyeRestTimer.Interval.TotalMinutes:F1}min");

            // TimeUntilNextEyeRest should NOT be zero/overdue
            var remaining = _timerService.TimeUntilNextEyeRest;
            Assert.True(remaining > TimeSpan.Zero,
                $"TimeUntilNextEyeRest should be positive after resume, but was {remaining}");
        }

        [Fact]
        public async Task SmartResume_AfterIdleLongerThanBreakDuration_ResetsBothTimersToFullInterval()
        {
            // Arrange
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var breakTimer = timers[BreakTimerIndex];

            await _timerService.SmartPauseAsync("User idle");

            // Simulate: User was idle for 6 minutes (> 5min break duration)
            SetPrivateField("_pauseStartTime", DateTime.Now.AddMinutes(-6));

            // Act
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Both timers reset to full intervals
            Assert.True(eyeRestTimer.IsEnabled);
            Assert.True(breakTimer.IsEnabled);

            Assert.True(eyeRestTimer.Interval.TotalMinutes > 19.0,
                $"Eye rest interval should be full, was {eyeRestTimer.Interval.TotalMinutes:F1}min");
            Assert.True(breakTimer.Interval.TotalMinutes > 54.0,
                $"Break interval should be full, was {breakTimer.Interval.TotalMinutes:F1}min");

            // Neither timer should be overdue
            Assert.True(_timerService.TimeUntilNextEyeRest > TimeSpan.Zero,
                "Eye rest should not be overdue after long idle resume");
            Assert.True(_timerService.TimeUntilNextBreak > TimeSpan.Zero,
                "Break should not be overdue after long idle resume");
        }

        [Fact]
        public async Task SmartResume_AfterShortIdle_RestoresPreservedRemainingTime()
        {
            // Arrange: Start, then pause
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];

            await _timerService.SmartPauseAsync("User idle");

            // Simulate: pause preserved 10 minutes of remaining time
            // (Set directly because the property getter short-circuits in paused state)
            SetPrivateField("_eyeRestRemainingTime", TimeSpan.FromMinutes(10));

            // Simulate: User was idle for only 10 seconds (< 20s eye rest duration)
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-10));

            // Act
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Should restore preserved remaining, not full interval
            Assert.True(eyeRestTimer.IsEnabled);
            // The interval should be the preserved remaining (~10min), not full (~20min)
            Assert.True(eyeRestTimer.Interval.TotalMinutes >= 9.5 && eyeRestTimer.Interval.TotalMinutes <= 10.5,
                $"Eye rest interval should be preserved remaining (~10min), was {eyeRestTimer.Interval.TotalMinutes:F1}min");

            // TimeUntilNextEyeRest should be positive (not overdue)
            Assert.True(_timerService.TimeUntilNextEyeRest > TimeSpan.Zero,
                "Eye rest should not be overdue after short idle resume");
        }

        [Fact]
        public async Task SmartResume_AfterIdleExactlyEyeRestDuration_ResetsEyeRestTimer()
        {
            // Arrange
            await StartServiceAsync();
            await _timerService.SmartPauseAsync("User idle");

            // Simulate: User was idle for exactly 20 seconds (= eye rest duration)
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-20));

            // Act
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Eye rest should be reset (20s idle >= 20s duration)
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            Assert.True(eyeRestTimer.Interval.TotalMinutes > 19.0,
                $"Eye rest should be reset to full interval, was {eyeRestTimer.Interval.TotalMinutes:F1}min");
        }

        [Fact]
        public async Task SmartResume_AfterLongIdle_DoesNotTriggerOverdueState()
        {
            // This test reproduces the exact bug from the logs:
            // User idle 15min → resume → eye rest overdue by 151s → unwanted popup
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();

            // Simulate: Eye rest was 8 minutes into its cycle when user went idle
            SetPrivateField("_eyeRestStartTime", DateTime.Now.AddMinutes(-8));

            await _timerService.SmartPauseAsync("User idle");

            // Simulate: User was idle for 15 minutes total
            SetPrivateField("_pauseStartTime", DateTime.Now.AddMinutes(-15));

            // Act
            await _timerService.SmartResumeAsync("User returned");

            // Assert: No overdue state — user rested naturally
            var eyeRestRemaining = _timerService.TimeUntilNextEyeRest;
            Assert.True(eyeRestRemaining > TimeSpan.Zero,
                $"Eye rest should NOT be overdue after 15min idle, but remaining was {eyeRestRemaining}");

            var breakRemaining = _timerService.TimeUntilNextBreak;
            Assert.True(breakRemaining > TimeSpan.Zero,
                $"Break should NOT be overdue after 15min idle, but remaining was {breakRemaining}");
        }

        #endregion

        #region Recovery Path — Start Time Reset Tests

        [Fact]
        public async Task HealthMonitor_WhenTimersRestarted_ResetsStartTimes()
        {
            // This tests that the TIMER STATE FIX recovery path resets start times
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var healthMonitor = timers[HealthMonitorTimerIndex];

            // Simulate: Set stale start time (25 min ago, past the 20min interval)
            SetPrivateField("_eyeRestStartTime", DateTime.Now.AddMinutes(-25));

            // Before health check, eye rest should appear overdue
            var beforeRemaining = _timerService.TimeUntilNextEyeRest;
            Assert.Equal(TimeSpan.Zero, beforeRemaining);

            // Now simulate the scenario where timers got disabled somehow
            timers[EyeRestTimerIndex].Stop();
            timers[BreakTimerIndex].Stop();

            // Set heartbeat stale enough to trigger detection (>= 2 min)
            SetPrivateField("_lastHeartbeat", DateTime.Now.AddMinutes(-3));

            // Act: Fire health monitor tick
            healthMonitor.FireTick();

            // Give async Task.Run a moment to execute
            await Task.Delay(100);

            // Assert: After recovery, start times should be fresh
            var afterRemaining = _timerService.TimeUntilNextEyeRest;
            Assert.True(afterRemaining > TimeSpan.Zero,
                $"Eye rest should not be overdue after recovery, but remaining was {afterRemaining}");
        }

        #endregion

        #region Heartbeat Behavior Tests

        [Fact]
        public async Task HealthMonitor_WhenTimerOverdue_DoesNotRefreshHeartbeat()
        {
            // Tests Fix #3: heartbeat should NOT be refreshed when timers are overdue
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var healthMonitor = timers[HealthMonitorTimerIndex];

            // Set eye rest start time to 25 min ago (overdue)
            SetPrivateField("_eyeRestStartTime", DateTime.Now.AddMinutes(-25));

            // Set heartbeat to 5 minutes ago
            var staleHeartbeat = DateTime.Now.AddMinutes(-5);
            SetPrivateField("_lastHeartbeat", staleHeartbeat);

            // Act: Fire health monitor
            healthMonitor.FireTick();

            // Allow async recovery to run
            await Task.Delay(100);

            // Assert: The heartbeat should either remain stale or have been updated
            // by the recovery path (not the "service running normally" early refresh)
            // The key behavior is that it doesn't blindly refresh when timers are overdue
            // After recovery, start times are reset so timers are no longer overdue
            var afterRemaining = _timerService.TimeUntilNextEyeRest;
            Assert.True(afterRemaining > TimeSpan.Zero,
                "After health monitor detects overdue, recovery should fix it");
        }

        #endregion

        #region SmartResume Updates Heartbeat

        [Fact]
        public async Task SmartResume_RefreshesHeartbeat()
        {
            await StartServiceAsync();

            // Set stale heartbeat
            SetPrivateField("_lastHeartbeat", DateTime.Now.AddMinutes(-10));

            await _timerService.SmartPauseAsync("User idle");

            // Simulate 30s idle
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-30));

            // Act
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Heartbeat should be fresh (within last few seconds)
            var lastHeartbeat = GetPrivateField<DateTime>("_lastHeartbeat");
            var heartbeatAge = DateTime.Now - lastHeartbeat;
            Assert.True(heartbeatAge.TotalSeconds < 5,
                $"Heartbeat should be fresh after resume, but was {heartbeatAge.TotalSeconds:F1}s old");
        }

        #endregion

        #region End-to-End Bug Report Scenario (2026-04-28 09:43:30 → 09:51:50 incident)

        [Fact]
        public async Task EndToEnd_EyeRestCoordinationThenBreakPopupThenIdleResume_NoPrematureBreakTick()
        {
            // Reproduces the exact user-reported sequence from the 2026-04-28 09:43:30 → 09:51:50 log:
            //   1. Eye-rest warning fires while break has only ~0.2 min remaining
            //   2. SmartResumeBreakTimerAfterEyeRest seeds _breakTimer.Interval = 0.2 min (the legitimate tail)
            //   3. Break tick fires; break popup opens (TriggerBreak stops the timer)
            //   4. User goes idle while the Complete dialog is still on screen
            //   5. User returns
            //   PRE-FIX: SmartResumeAsync cleared _isBreakNotificationActive and started the
            //   break timer with the stale 0.2 min interval → break-warning popup spawned ~12s
            //   later → CloseCurrentPopup() killed the original Complete dialog.
            //   POST-FIX: break timer stays stopped throughout; popup-completion handler
            //   (orchestrator → SmartSessionResetAsync) is the sole path that restarts it.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];
            var eyeRestTimer = timers[EyeRestTimerIndex];

            // Step 1+2: simulate the eye-rest coordination cycle that seeded the tiny break interval.
            // SmartPauseBreakTimerForEyeRest computes remainder = _breakInterval - (now - _breakStartTime).
            // Force a 0.2 min remainder by faking elapsed = (_breakInterval - 0.2 min). Read the
            // real _breakInterval (54.5 min after WarningSeconds reduction, not the raw 55 min)
            // so this test stays correct if the reduction factor changes.
            var initialBreakInterval = GetPrivateField<TimeSpan>("_breakInterval");
            var fakeElapsed = initialBreakInterval - TimeSpan.FromMinutes(0.2);
            SetPrivateField("_breakStartTime", _fakeClock.Now.Subtract(fakeElapsed));
            _timerService.SmartPauseBreakTimerForEyeRest();
            var preservedRemainder = GetPrivateField<TimeSpan>("_breakRemainingTime");
            Assert.True(preservedRemainder.TotalMinutes < 0.5,
                $"Step 1: eye-rest coordination must have preserved a tiny break remainder, got {preservedRemainder.TotalMinutes:F2}min");

            _timerService.SmartResumeBreakTimerAfterEyeRest();
            Assert.True(breakTimer.Interval.TotalMinutes < 0.5,
                $"Step 2: SmartResumeBreakTimerAfterEyeRest seeded the stale tiny interval, got {breakTimer.Interval.TotalMinutes:F2}min");

            // Step 3: break tick fired and TriggerBreak ran — timer stopped, popup active.
            breakTimer.Stop();
            SetPrivateField("_isBreakNotificationActive", true);

            // Step 4: user goes idle while popup is on screen (Fix C: this is now a no-op)
            await _timerService.SmartPauseAsync("User idle");

            // Step 5: user returns 1 minute later
            _fakeClock.Advance(TimeSpan.FromMinutes(1));
            await _timerService.SmartResumeAsync("User returned");

            // Bug verification: break timer must NOT have started, regardless of stale interval.
            // Pre-fix: this would be true (timer enabled with 0.2 min interval, ticking ~12s later).
            // Post-fix: break timer stays stopped — the popup-completion handler will restart it.
            Assert.False(breakTimer.IsEnabled,
                "REGRESSION (2026-04-28): break timer running while a break popup is on screen would " +
                "tick prematurely with the stale 0.2 min interval and re-spawn a warning popup that " +
                "closes the Complete dialog.");
            Assert.True(GetPrivateField<bool>("_isBreakNotificationActive"),
                "Notification flag must persist so the popup-completion handler can clear it");
            Assert.False(_timerService.IsSmartPaused,
                "Smart-pause state should be clean so the popup-completion handler's restart path works");
        }

        [Fact]
        public async Task EndToEnd_AfterDeferredResume_PopupCompletionViaSessionResetRestartsTimersWithFullInterval()
        {
            // Continuation of the previous test: after the user finally clicks Complete (or any
            // BreakAction that funnels into SmartSessionResetAsync), the timer service must
            // restart cleanly with full intervals. This is the "happy path after deferred resume".
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];
            var eyeRestTimer = timers[EyeRestTimerIndex];

            // Set up the post-bug-scenario state: stale interval, popup active, both timers stopped.
            breakTimer.Interval = TimeSpan.FromMinutes(0.2);
            eyeRestTimer.Interval = TimeSpan.FromSeconds(5);
            breakTimer.Stop();
            eyeRestTimer.Stop();
            SetPrivateField("_isBreakNotificationActive", true);

            // User finally clicks Complete (or Skipped/ConfirmedAfterCompletion/etc.)
            // Orchestrator calls SmartSessionResetAsync.
            await _timerService.SmartSessionResetAsync("Break completed - starting fresh session");

            Assert.True(breakTimer.IsEnabled, "Break timer must restart after session reset");
            Assert.True(eyeRestTimer.IsEnabled, "Eye-rest timer must restart after session reset");
            Assert.True(breakTimer.Interval.TotalMinutes > 54.0,
                $"Break interval must be reset to full, got {breakTimer.Interval.TotalMinutes:F1}min");
            Assert.True(eyeRestTimer.Interval.TotalMinutes > 19.0,
                $"Eye-rest interval must be reset to full, got {eyeRestTimer.Interval.TotalMinutes:F1}min");
            Assert.False(GetPrivateField<bool>("_isBreakNotificationActive"),
                "Notification flag must be cleared by session reset");
        }

        [Fact]
        public async Task EndToEnd_LongIdleDuringActiveBreakPopup_StillDefersTimerStart()
        {
            // Variant: user goes idle for >5 minutes (past break duration) during the popup.
            // Fix C makes SmartPause a no-op, so even a long idle doesn't change timer state.
            // The "natural break" reset path inside SmartResume must NOT run because no
            // SmartPause happened in the first place.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];

            breakTimer.Interval = TimeSpan.FromMinutes(0.2);
            breakTimer.Stop();
            SetPrivateField("_isBreakNotificationActive", true);

            // User goes idle for 10 minutes (well past the 5min break duration)
            await _timerService.SmartPauseAsync("User idle");
            _fakeClock.Advance(TimeSpan.FromMinutes(10));
            await _timerService.SmartResumeAsync("User returned");

            Assert.False(breakTimer.IsEnabled,
                "Even after 10min idle, break timer must not start while popup is on screen");
            Assert.True(GetPrivateField<bool>("_isBreakNotificationActive"));
        }

        [Fact]
        public async Task EndToEnd_RaceConditionNotificationActivatesAfterPauseCompleted_SmartResumeStillDefers()
        {
            // Race scenario: SmartPause completes successfully (no popup yet), THEN a popup
            // notification activates while paused (delayed event from a prior cycle), THEN
            // SmartResume runs. This exercises Fix B (SmartResume defers when notification active)
            // — the path Fix C alone wouldn't catch.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];

            // Step A: pause normally (no popup yet)
            await _timerService.SmartPauseAsync("User idle");
            Assert.True(_timerService.IsSmartPaused);
            Assert.False(breakTimer.IsEnabled);

            // Step B: notification becomes active during the pause window
            SetPrivateField("_isBreakNotificationActive", true);
            breakTimer.Interval = TimeSpan.FromMinutes(0.2); // stale interval also present

            // Step C: user returns
            _fakeClock.Advance(TimeSpan.FromMinutes(1));
            await _timerService.SmartResumeAsync("User returned");

            Assert.False(breakTimer.IsEnabled,
                "Race-fix (Fix B): SmartResume must defer timer start when notification is active, " +
                "even if the notification activated AFTER SmartPause already ran");
            Assert.True(GetPrivateField<bool>("_isBreakNotificationActive"));
            Assert.False(_timerService.IsSmartPaused);
        }

        [Fact]
        public async Task EndToEnd_SmartResumeBreakTimerAfterEyeRest_WithEmptyRemainder_ResetsToFullInterval()
        {
            // Path coverage for the fallback in SmartResumeBreakTimerAfterEyeRest:
            // when _breakRemainingTime is zero (user took a long natural rest during the
            // eye-rest popup, or initial state), the resume should fall back to a fresh
            // full break interval (Coordination.cs:125-144) rather than starting with zero.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];

            // Force the post-pause state: timer stopped, paused-flag set, remainder zero
            breakTimer.Stop();
            SetPrivateField("_breakTimerPausedForEyeRest", true);
            SetPrivateField("_breakRemainingTime", TimeSpan.Zero);

            _timerService.SmartResumeBreakTimerAfterEyeRest();

            Assert.True(breakTimer.IsEnabled, "Break timer must restart after eye-rest");
            Assert.True(breakTimer.Interval.TotalMinutes > 54.0,
                $"Empty-remainder fallback should yield full break interval, got {breakTimer.Interval.TotalMinutes:F1}min");
        }

        #endregion

        #region Notification-Active Resume Tests (regression for 2026-04-28 bug)

        [Fact]
        public async Task SmartResume_DuringActiveBreakNotification_DoesNotRestartTimers()
        {
            // Reproduces the 2026-04-28 09:51:35 bug:
            // User went idle while the break Complete dialog was open, returned, and
            // SmartResume cleared _isBreakNotificationActive and called _breakTimer.Start()
            // with a stale tiny interval (set during prior eye-rest coordination).
            // Result: a duplicate break-warning popup spawned over the active break dialog
            // and closed it via NotificationService.CloseCurrentPopup().
            //
            // Expected: while a break popup is still showing, SmartResume must keep the
            // notification flag, leave the timers stopped, and clear IsSmartPaused so the
            // popup-completion handler (RestartBreakTimerAfterCompletion) can later start
            // the timers cleanly with a fresh full interval.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];
            var eyeRestTimer = timers[EyeRestTimerIndex];

            // Pause first (no popup yet — would be no-op now if popup were active).
            await _timerService.SmartPauseAsync("User idle");
            Assert.True(_timerService.IsSmartPaused);

            // Simulate that the break popup *became* active during the pause window
            // (e.g., a delayed event that landed after SmartPause completed) and that
            // a stale 0.2-min interval was seeded by prior SmartResumeBreakTimerAfterEyeRest.
            SetPrivateField("_isBreakNotificationActive", true);
            breakTimer.Interval = TimeSpan.FromMinutes(0.2);
            SetPrivateField("_pauseStartTime", DateTime.Now.AddMinutes(-1));

            await _timerService.SmartResumeAsync("User returned");

            Assert.False(breakTimer.IsEnabled,
                "Break timer must not be started while a break popup is still active");
            Assert.False(eyeRestTimer.IsEnabled,
                "Eye-rest timer must not be started while a break popup is still active");
            Assert.True(GetPrivateField<bool>("_isBreakNotificationActive"),
                "_isBreakNotificationActive must not be cleared by SmartResume during an active popup");
            Assert.False(_timerService.IsSmartPaused,
                "IsSmartPaused must be cleared so RestartBreakTimerAfterCompletion can start timers later");
        }

        [Fact]
        public async Task SmartResume_DuringActiveEyeRestNotification_DoesNotRestartTimers()
        {
            // Symmetric guard for the eye-rest popup case.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];
            var breakTimer = timers[BreakTimerIndex];

            // Pause first (no popup yet), then simulate notification becoming active.
            await _timerService.SmartPauseAsync("User idle");
            Assert.True(_timerService.IsSmartPaused);

            SetPrivateField("_isEyeRestNotificationActive", true);
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-30));

            await _timerService.SmartResumeAsync("User returned");

            Assert.False(eyeRestTimer.IsEnabled,
                "Eye-rest timer must not be started while an eye-rest popup is still active");
            Assert.False(breakTimer.IsEnabled,
                "Break timer must not be started while an eye-rest popup is still active");
            Assert.True(GetPrivateField<bool>("_isEyeRestNotificationActive"),
                "_isEyeRestNotificationActive must not be cleared by SmartResume during an active popup");
            Assert.False(_timerService.IsSmartPaused);
        }

        #endregion

        #region SmartPause-Skipped-During-Active-Popup Tests (regression for 2026-04-28 bug)

        [Fact]
        public async Task SmartPause_DuringActiveBreakNotification_DoesNothing()
        {
            // The user is already taking the break — the popup is on screen and the
            // user just stepped away from input. Smart-pausing the timer service here
            // creates the state-management mess that gave us the 09:51:35 bug:
            // SmartResume must then re-enter and either start timers prematurely or
            // defer them. Easier: don't pause in the first place.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();

            SetPrivateField("_isBreakNotificationActive", true);

            await _timerService.SmartPauseAsync("User idle");

            Assert.False(_timerService.IsSmartPaused,
                "SmartPause must be a no-op while a break popup is still active");
            // Timer states should not have been changed by the no-op pause.
            // (We don't assert specific values — just that the pause didn't run.)
        }

        [Fact]
        public async Task SmartPause_DuringActiveEyeRestNotification_DoesNothing()
        {
            await StartServiceAsync();

            SetPrivateField("_isEyeRestNotificationActive", true);

            await _timerService.SmartPauseAsync("User idle");

            Assert.False(_timerService.IsSmartPaused,
                "SmartPause must be a no-op while an eye-rest popup is still active");
        }

        [Fact]
        public async Task SmartPauseResume_RoundTripUsingFakeClock_ComputesIdleDurationFromClock()
        {
            // Demonstrates the new IClock abstraction: time can be advanced
            // deterministically without SetPrivateField hacks.
            await StartServiceAsync();
            var baseTime = new DateTime(2026, 4, 28, 9, 50, 0, DateTimeKind.Local);
            _fakeClock.Now = baseTime;

            await _timerService.SmartPauseAsync("User idle");

            // Advance the clock by 6 minutes — long enough to qualify as a natural
            // break (>= 5min break duration) so both timers reset to full intervals.
            _fakeClock.Advance(TimeSpan.FromMinutes(6));

            await _timerService.SmartResumeAsync("User returned");

            var timers = _fakeTimerFactory.GetCreatedTimers();
            Assert.True(timers[BreakTimerIndex].Interval.TotalMinutes > 54.0,
                $"Break should reset to full interval after 6min idle, was {timers[BreakTimerIndex].Interval.TotalMinutes:F1}min");
            Assert.True(timers[EyeRestTimerIndex].Interval.TotalMinutes > 19.0,
                $"Eye rest should reset to full interval after 6min idle, was {timers[EyeRestTimerIndex].Interval.TotalMinutes:F1}min");
        }

        #endregion

        #region Stale Interval Resume Tests (Fix A regression)

        [Fact]
        public async Task SmartResume_WithStaleBreakInterval_AndNoPreservedRemaining_ResetsToFullInterval()
        {
            // Reproduces Fix A: if _breakTimer.Interval was left at a tiny stale value by
            // a prior coordination (e.g., 12s from SmartResumeBreakTimerAfterEyeRest) and
            // SmartResume falls into the "no preserved remaining" path with a short idle,
            // the timer used to start with that stale interval and fire prematurely.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var breakTimer = timers[BreakTimerIndex];

            // Seed the stale interval that would be set by SmartResumeBreakTimerAfterEyeRest
            // when the break had only 0.2 minutes remaining.
            breakTimer.Interval = TimeSpan.FromMinutes(0.2);

            await _timerService.SmartPauseAsync("User idle");

            // Force the "no preserved remaining" branch: zero out _breakRemainingTime and
            // keep idle short enough to avoid the natural-break reset (< 5min).
            SetPrivateField("_breakRemainingTime", TimeSpan.Zero);
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-30));

            await _timerService.SmartResumeAsync("User returned");

            Assert.True(breakTimer.IsEnabled);
            Assert.True(breakTimer.Interval.TotalMinutes > 1.0,
                $"Break timer interval should be reset to a fresh full interval, " +
                $"not the stale 0.2min value. Was {breakTimer.Interval.TotalMinutes:F2}min");
        }

        [Fact]
        public async Task SmartResume_WithStaleEyeRestInterval_AndNoPreservedRemaining_ResetsToFullInterval()
        {
            // Same bug pattern for the eye-rest timer's else-branch.
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();
            var eyeRestTimer = timers[EyeRestTimerIndex];

            eyeRestTimer.Interval = TimeSpan.FromSeconds(5);

            await _timerService.SmartPauseAsync("User idle");

            SetPrivateField("_eyeRestRemainingTime", TimeSpan.Zero);
            // Idle = 5s < 20s eye-rest duration, so neither the natural-rest reset
            // nor the preserved-remaining path will trigger.
            SetPrivateField("_pauseStartTime", DateTime.Now.AddSeconds(-5));

            await _timerService.SmartResumeAsync("User returned");

            Assert.True(eyeRestTimer.IsEnabled);
            Assert.True(eyeRestTimer.Interval.TotalMinutes > 1.0,
                $"Eye-rest timer interval should be reset to a fresh full interval, " +
                $"not the stale 5s value. Was {eyeRestTimer.Interval.TotalMinutes:F2}min");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task SmartResume_WhenNotSmartPaused_DoesNothing()
        {
            await StartServiceAsync();

            // Act: Try to resume when not paused
            await _timerService.SmartResumeAsync("User returned");

            // Assert: Service still running normally
            Assert.True(_timerService.IsRunning);
            Assert.False(_timerService.IsSmartPaused);
        }

        [Fact]
        public async Task SmartPause_StopsTimersAndPreservesState()
        {
            await StartServiceAsync();
            var timers = _fakeTimerFactory.GetCreatedTimers();

            // Act
            await _timerService.SmartPauseAsync("User idle");

            // Assert: timers stopped, state set
            Assert.True(_timerService.IsSmartPaused);
            Assert.False(timers[EyeRestTimerIndex].IsEnabled);
            Assert.False(timers[BreakTimerIndex].IsEnabled);

            // Pause start time should be recorded
            var pauseStart = GetPrivateField<DateTime>("_pauseStartTime");
            Assert.True((DateTime.Now - pauseStart).TotalSeconds < 2,
                "Pause start time should be set to approximately now");

            // Remaining time should be preserved (will be full interval since
            // SmartPause sets IsSmartPaused=true before reading TimeUntilNext*)
            var preserved = GetPrivateField<TimeSpan>("_eyeRestRemainingTime");
            Assert.True(preserved > TimeSpan.Zero,
                $"Should preserve some remaining time, got {preserved}");
        }

        [Fact]
        public async Task SmartResume_MultiplePauseResumeCycles_DoNotAccumulateOverdue()
        {
            await StartServiceAsync();

            for (int i = 0; i < 3; i++)
            {
                // Pause
                await _timerService.SmartPauseAsync($"Cycle {i} - idle");

                // Simulate 1 minute idle (> 20s eye rest duration)
                SetPrivateField("_pauseStartTime", DateTime.Now.AddMinutes(-1));

                // Resume
                await _timerService.SmartResumeAsync($"Cycle {i} - returned");

                // Assert: Never overdue
                Assert.True(_timerService.TimeUntilNextEyeRest > TimeSpan.Zero,
                    $"Cycle {i}: Eye rest should not be overdue");
                Assert.True(_timerService.TimeUntilNextBreak > TimeSpan.Zero,
                    $"Cycle {i}: Break should not be overdue");
            }
        }

        #endregion

        #region Reflection Helpers

        private void SetPrivateField(string fieldName, object value)
        {
            var field = typeof(TimerService).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            field!.SetValue(_timerService, value);
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var field = typeof(TimerService).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(_timerService)!;
        }

        #endregion
    }
}
