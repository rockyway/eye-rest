using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EyeRest.Services;
using EyeRest.Models;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using System.Windows.Threading;

namespace EyeRest.Tests.Integration
{
    /// <summary>
    /// Integration tests for extended idle recovery scenarios
    /// Tests the fix for timers showing "Due now" after resuming from 1+ hour idle
    /// </summary>
    public class ExtendedIdleRecoveryTests
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IPauseReminderService> _mockPauseReminderService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly AppConfiguration _testConfig;
        private readonly TimerService _timerService;

        public ExtendedIdleRecoveryTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockPauseReminderService = new Mock<IPauseReminderService>();
            _fakeTimerFactory = new FakeTimerFactory();

            // Use very short intervals for testing (2 minutes eye rest, 3 minutes break)
            _testConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 2,
                    WarningSeconds = 10
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 3,
                    WarningSeconds = 15
                },
                UserPresence = new UserPresenceSettings
                {
                    IdleThresholdMinutes = 1, // 1 minute idle threshold
                    ExtendedAwayThresholdMinutes = 5, // 5 minute extended away
                    EnableSmartSessionReset = true
                }
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_testConfig);

            _timerService = new TimerService(
                _mockLogger.Object,
                _mockConfigService.Object,
                _mockAnalyticsService.Object,
                _fakeTimerFactory,
                _mockPauseReminderService.Object);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_ShortIdle_ShouldPreserveTimerState()
        {
            // Arrange: Start the timer service
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Let timers run for 30 seconds
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromSeconds(30));
            
            // Get initial remaining times
            var initialEyeRestTime = _timerService.TimeUntilNextEyeRest;
            var initialBreakTime = _timerService.TimeUntilNextBreak;
            
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Timer service started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);

            // Act: Simulate short idle (2 minutes - less than extended away threshold)
            await _timerService.SmartPauseAsync("User idle - short break");
            Assert.True(_timerService.IsSmartPaused);
            
            // Simulate 2 minutes of idle time
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromMinutes(2));
            
            // Resume from idle
            await _timerService.SmartResumeAsync("User returned from short break");

            // Assert: Timer state should be preserved (not reset)
            Assert.False(_timerService.IsSmartPaused);
            Assert.True(_timerService.IsRunning);
            
            // Times should be less than initial (time has passed) but not reset to full intervals
            var resumedEyeRestTime = _timerService.TimeUntilNextEyeRest;
            var resumedBreakTime = _timerService.TimeUntilNextBreak;
            
            Assert.True(resumedEyeRestTime < initialEyeRestTime, 
                $"Eye rest time should be less than initial. Initial: {initialEyeRestTime}, Resumed: {resumedEyeRestTime}");
            Assert.True(resumedBreakTime < initialBreakTime,
                $"Break time should be less than initial. Initial: {initialBreakTime}, Resumed: {resumedBreakTime}");
                
            // Should not show "Due now" (TimeSpan.Zero)
            Assert.True(resumedEyeRestTime > TimeSpan.Zero, "Eye rest timer should not show 'Due now'");
            Assert.True(resumedBreakTime > TimeSpan.Zero, "Break timer should not show 'Due now'");
            
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART RESUME: Restored eye rest timer interval")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_ExtendedIdle_ShouldResetToFreshSession()
        {
            // Arrange: Start the timer service
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Let timers run for 30 seconds
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromSeconds(30));
            
            _mockLogger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Timer service started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);

            // Act: Simulate extended idle (10 minutes - more than 5 minute threshold)
            await _timerService.SmartPauseAsync("User idle - extended away");
            Assert.True(_timerService.IsSmartPaused);
            
            // Simulate 10 minutes of extended idle time
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromMinutes(10));
            
            // Trigger extended away session reset
            await _timerService.SmartSessionResetAsync("Extended away (10min) - fresh session");

            // Assert: Timer should be reset to fresh session with full intervals
            Assert.False(_timerService.IsSmartPaused);
            Assert.True(_timerService.IsRunning);
            
            var resetEyeRestTime = _timerService.TimeUntilNextEyeRest;
            var resetBreakTime = _timerService.TimeUntilNextBreak;
            
            // Should be close to full intervals (allowing for warning time deduction)
            var expectedEyeRestTime = TimeSpan.FromMinutes(_testConfig.EyeRest.IntervalMinutes) - 
                                    TimeSpan.FromSeconds(_testConfig.EyeRest.WarningSeconds);
            var expectedBreakTime = TimeSpan.FromMinutes(_testConfig.Break.IntervalMinutes) - 
                                  TimeSpan.FromSeconds(_testConfig.Break.WarningSeconds);
            
            Assert.True(Math.Abs(resetEyeRestTime.TotalSeconds - expectedEyeRestTime.TotalSeconds) < 5,
                $"Eye rest should be near full interval. Expected: {expectedEyeRestTime}, Actual: {resetEyeRestTime}");
            Assert.True(Math.Abs(resetBreakTime.TotalSeconds - expectedBreakTime.TotalSeconds) < 5,
                $"Break should be near full interval. Expected: {expectedBreakTime}, Actual: {resetBreakTime}");
                
            // Should definitely not show "Due now"
            Assert.True(resetEyeRestTime > TimeSpan.Zero, "Eye rest timer should not show 'Due now' after reset");
            Assert.True(resetBreakTime > TimeSpan.Zero, "Break timer should not show 'Due now' after reset");
            
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART SESSION RESET COMPLETED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_TimersDisabled_ShouldRecreateAndRestart()
        {
            // Arrange: Start the timer service
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Simulate timers being disabled (null) - this is what happens after extended idle
            _fakeTimerFactory.DisableAllTimers();
            
            // Act: Try to resume with disabled timers (simulate the bug scenario)
            await _timerService.SmartResumeAsync("User returned but timers are disabled");
            
            // Assert: Timers should be recreated and started
            Assert.True(_timerService.IsRunning);
            Assert.False(_timerService.IsSmartPaused);
            
            // Should not show "Due now" after recreation
            var eyeRestTime = _timerService.TimeUntilNextEyeRest;
            var breakTime = _timerService.TimeUntilNextBreak;
            
            Assert.True(eyeRestTime > TimeSpan.Zero, "Eye rest timer should be recreated and not show 'Due now'");
            Assert.True(breakTime > TimeSpan.Zero, "Break timer should be recreated and not show 'Due now'");
            
            // Verify timer recreation was logged
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART RESUME FIX: Eye rest timer is null or disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
                
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART RESUME FIX: Break timer is null or disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_HealthMonitor_ShouldDetectDisabledTimers()
        {
            // Arrange: Start the timer service
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Simulate timers being disabled while service is running (the bug scenario)
            _fakeTimerFactory.DisableAllTimers();
            
            // Simulate health monitor detecting the issue (advance time to trigger health check)
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromMinutes(3)); // Trigger health check
            
            // Act: Trigger health monitor check manually
            var healthMonitorTimer = _fakeTimerFactory.GetHealthMonitorTimer();
            healthMonitorTimer?.RaiseTickEvent();
            
            // Allow for async recovery operations
            await Task.Delay(100);
            
            // Assert: Health monitor should detect and fix the issue
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("TIMER STATE FAILURE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
                
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("TIMER STATE RECOVERY")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_SessionResetWithNullTimers_ShouldCreateTimers()
        {
            // Arrange: Start the timer service
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Simulate all timers being null (extreme case after system resume)
            _fakeTimerFactory.NullifyAllTimers();
            
            // Act: Perform session reset with null timers
            await _timerService.SmartSessionResetAsync("Test session reset with null timers");
            
            // Assert: Timers should be recreated
            Assert.True(_timerService.IsRunning);
            
            var eyeRestTime = _timerService.TimeUntilNextEyeRest;
            var breakTime = _timerService.TimeUntilNextBreak;
            
            Assert.True(eyeRestTime > TimeSpan.Zero, "Eye rest timer should be recreated during session reset");
            Assert.True(breakTime > TimeSpan.Zero, "Break timer should be recreated during session reset");
            
            // Verify timer creation was logged
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART SESSION RESET FIX: Eye rest timer is null")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
                
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART SESSION RESET FIX: Break timer is null")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_CompleteScenario_SimulateRealUserWorkflow()
        {
            // This test simulates the complete user workflow that was causing the "Due now" issue
            
            // Arrange: Start fresh session
            await _timerService.StartAsync();
            Assert.True(_timerService.IsRunning);
            
            // Step 1: User works for 30 seconds
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromSeconds(30));
            var workingEyeRest = _timerService.TimeUntilNextEyeRest;
            var workingBreak = _timerService.TimeUntilNextBreak;
            
            Assert.True(workingEyeRest > TimeSpan.Zero, "Should show normal countdown during work");
            Assert.True(workingBreak > TimeSpan.Zero, "Should show normal countdown during work");
            
            // Step 2: User becomes idle (5 minutes)
            await _timerService.SmartPauseAsync("User idle - detected by UserPresenceService");
            Assert.True(_timerService.IsSmartPaused);
            
            // Step 3: User is away for 1+ hours (simulate overnight/extended break)
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromHours(1.5)); // 90 minutes
            
            // Step 4: System detects extended away and performs session reset
            await _timerService.SmartSessionResetAsync("Extended away (90min) - new working session after overnight");
            
            // Step 5: Verify recovery - should NOT show "Due now"
            Assert.False(_timerService.IsSmartPaused, "Should not be paused after session reset");
            Assert.True(_timerService.IsRunning, "Service should be running after session reset");
            
            var recoveredEyeRest = _timerService.TimeUntilNextEyeRest;
            var recoveredBreak = _timerService.TimeUntilNextBreak;
            
            // This is the key assertion - the fix prevents these from being TimeSpan.Zero ("Due now")
            Assert.True(recoveredEyeRest > TimeSpan.Zero, 
                $"CRITICAL: Eye rest should not show 'Due now' after extended idle recovery. Got: {recoveredEyeRest}");
            Assert.True(recoveredBreak > TimeSpan.Zero, 
                $"CRITICAL: Break should not show 'Due now' after extended idle recovery. Got: {recoveredBreak}");
            
            // Should be close to full intervals (fresh session)
            Assert.True(recoveredEyeRest.TotalSeconds > 90, 
                $"Eye rest should be nearly full interval. Got: {recoveredEyeRest.TotalSeconds}s");
            Assert.True(recoveredBreak.TotalSeconds > 150, 
                $"Break should be nearly full interval. Got: {recoveredBreak.TotalSeconds}s");
                
            // Verify the fix worked by checking logs
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART SESSION RESET COMPLETED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ExtendedIdleRecovery_TimerStatePreservation_ShouldMaintainCorrectIntervals()
        {
            // Test the specific fix for timer state preservation during SmartPause
            
            // Arrange: Start and let run for a bit
            await _timerService.StartAsync();
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromSeconds(45)); // 45 seconds of work
            
            var beforePauseEyeRest = _timerService.TimeUntilNextEyeRest;
            var beforePauseBreak = _timerService.TimeUntilNextBreak;
            
            // Act: Smart pause (should preserve remaining times)
            await _timerService.SmartPauseAsync("Testing state preservation");
            
            // Simulate 30 seconds of pause time
            _fakeTimerFactory.AdvanceTime(TimeSpan.FromSeconds(30));
            
            // Resume
            await _timerService.SmartResumeAsync("Testing state restoration");
            
            // Assert: Times should be restored correctly
            var afterResumeEyeRest = _timerService.TimeUntilNextEyeRest;
            var afterResumeBreak = _timerService.TimeUntilNextBreak;
            
            // Times should be preserved (the fix ensures this)
            Assert.True(Math.Abs(afterResumeEyeRest.TotalSeconds - beforePauseEyeRest.TotalSeconds) < 5,
                $"Eye rest time should be preserved. Before: {beforePauseEyeRest}, After: {afterResumeEyeRest}");
            Assert.True(Math.Abs(afterResumeBreak.TotalSeconds - beforePauseBreak.TotalSeconds) < 5,
                $"Break time should be preserved. Before: {beforePauseBreak}, After: {afterResumeBreak}");
                
            // Verify preservation was logged
            _mockLogger.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SMART PAUSE: Preserved eye rest remaining time")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
    }
}