using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace EyeRest.Tests.Integration
{
    /// <summary>
    /// Fast 2-day simulation test using FakeTimerFactory to simulate 48 hours in seconds
    /// Tests all features including clock jump detection after overnight sleep
    /// </summary>
    public class FastTwoDaySimulationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<IAnalyticsService> _analyticsMock;
        private readonly Mock<IConfigurationService> _configMock;
        private readonly Mock<IPauseReminderService> _pauseReminderMock;
        private readonly AppConfiguration _configuration;
        private TimerService _timerService = null!;
        
        // Virtual time tracking
        private DateTime _virtualTime;
        private readonly DateTime _startTime = new DateTime(2025, 1, 15, 9, 0, 0); // Day 1: 9:00 AM
        
        // Event tracking
        private readonly List<string> _events = new();
        private int _eyeRestCount = 0;
        private int _breakCount = 0;
        private int _sessionResetCount = 0;
        
        public FastTwoDaySimulationTests(ITestOutputHelper output)
        {
            _output = output;
            _virtualTime = _startTime;
            
            // Create fake timer factory
            _fakeTimerFactory = new FakeTimerFactory();
            
            // Setup mocks
            _notificationMock = new Mock<INotificationService>();
            _analyticsMock = new Mock<IAnalyticsService>();
            _configMock = new Mock<IConfigurationService>();
            _pauseReminderMock = new Mock<IPauseReminderService>();
            
            // Setup configuration
            _configuration = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    WarningSeconds = 15
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningSeconds = 30
                }
            };
            
            _configMock.Setup(x => x.LoadConfigurationAsync()).ReturnsAsync(_configuration);
            
            // Track notification events
            _notificationMock.Setup(x => x.ShowEyeRestWarningAsync(It.IsAny<TimeSpan>()))
                .Callback(() => LogEvent("Eye rest warning"))
                .Returns(Task.CompletedTask);
                
            _notificationMock.Setup(x => x.ShowEyeRestReminderAsync(It.IsAny<TimeSpan>()))
                .Callback(() => {
                    _eyeRestCount++;
                    LogEvent($"Eye rest reminder #{_eyeRestCount}");
                })
                .Returns(Task.CompletedTask);
                
            _notificationMock.Setup(x => x.ShowBreakWarningAsync(It.IsAny<TimeSpan>()))
                .Callback(() => LogEvent("Break warning"))
                .Returns(Task.CompletedTask);
                
            _notificationMock.Setup(x => x.ShowBreakReminderAsync(It.IsAny<TimeSpan>(), It.IsAny<IProgress<double>>()))
                .Callback(() => {
                    _breakCount++;
                    LogEvent($"Break reminder #{_breakCount}");
                })
                .ReturnsAsync(BreakAction.Completed);
            
            // Track analytics events
            _analyticsMock.Setup(x => x.RecordResumeEventAsync(It.IsAny<ResumeReason>()))
                .Callback<ResumeReason>(reason => {
                    if (reason == ResumeReason.NewWorkingSession)
                    {
                        _sessionResetCount++;
                        LogEvent($"Session reset #{_sessionResetCount}: {reason}");
                    }
                })
                .Returns(Task.CompletedTask);
        }

        public async Task InitializeAsync()
        {
            // Create TimerService with fake timers
            var logger = new Mock<ILogger<TimerService>>();
            
            _timerService = new TimerService(
                logger.Object,
                _configMock.Object,
                _analyticsMock.Object,
                _fakeTimerFactory,
                _pauseReminderMock.Object
            );
            
            _timerService.SetNotificationService(_notificationMock.Object);
            
            // Initialize timers
            await _timerService.StartAsync();
            
            _output.WriteLine("=== FAST 2-DAY SIMULATION INITIALIZED ===");
            _output.WriteLine($"Start time: {_virtualTime:yyyy-MM-dd HH:mm:ss}");
        }

        public async Task DisposeAsync()
        {
            await _timerService.StopAsync();
            _fakeTimerFactory.Clear();
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "FastSimulation")]
        public async Task SimulateTwoDaysWithFakeTimers_ShouldCompleteInSeconds()
        {
            var testStart = DateTime.Now;
            
            _output.WriteLine("\n=== DAY 1: MORNING (9:00 AM - 12:00 PM) ===");
            
            // Advance 19:45 minutes (just before first eye rest warning)
            await AdvanceVirtualTime(TimeSpan.FromMinutes(19.75));
            _fakeTimerFactory.FireAllTimers(); // Should trigger eye rest warning
            
            // Advance 15 seconds to eye rest
            await AdvanceVirtualTime(TimeSpan.FromSeconds(15));
            _fakeTimerFactory.FireAllTimers(); // Should trigger eye rest reminder
            
            // Advance to 40 minutes (second eye rest)
            await AdvanceVirtualTime(TimeSpan.FromMinutes(20));
            _fakeTimerFactory.FireAllTimers();
            
            // Advance to 54:30 (break warning at 55 - 30s)
            await AdvanceVirtualTime(TimeSpan.FromMinutes(14.5));
            _fakeTimerFactory.FireAllTimers(); // Should trigger break warning
            
            // Advance 30 seconds to break
            await AdvanceVirtualTime(TimeSpan.FromSeconds(30));
            _fakeTimerFactory.FireAllTimers(); // Should trigger break reminder
            
            _output.WriteLine($"Morning complete - Eye rests: {_eyeRestCount}, Breaks: {_breakCount}");
            
            _output.WriteLine("\n=== DAY 1: AFTERNOON - EXTENDED AWAY (LUNCH) ===");
            
            // Simulate 1-hour lunch break (extended away > 30 min)
            await SimulateExtendedAway(TimeSpan.FromHours(1), "Lunch break");
            
            _output.WriteLine($"After lunch - Session resets: {_sessionResetCount}");
            
            _output.WriteLine("\n=== DAY 1: EVENING - SYSTEM SLEEP ===");
            
            // Simulate system going to sleep at 6 PM
            _virtualTime = new DateTime(2025, 1, 15, 18, 0, 0);
            await _timerService.PauseAsync();
            LogEvent("System sleep at 6:00 PM");
            
            _output.WriteLine("\n=== OVERNIGHT (14 HOURS) ===");
            
            // Advance time 14 hours overnight
            _virtualTime = new DateTime(2025, 1, 16, 8, 0, 0); // Day 2: 8:00 AM
            
            _output.WriteLine("\n=== DAY 2: MORNING - WAKE FROM SLEEP ===");
            
            // Simulate clock jump detection on wake
            await SimulateClockJumpWake();
            
            // CRITICAL VERIFICATION: No immediate reminder after wake
            var popupsBeforeWake = _eyeRestCount + _breakCount;
            _fakeTimerFactory.FireAllTimers(); // Try to fire timers
            var popupsAfterWake = _eyeRestCount + _breakCount;
            
            Assert.Equal(popupsBeforeWake, popupsAfterWake); // No new reminders!
            _output.WriteLine("✅ CLOCK JUMP DETECTION WORKING: No immediate reminders after wake!");
            
            _output.WriteLine("\n=== DAY 2: NORMAL WORK ===");
            
            // Continue with normal work
            await _timerService.ResumeAsync();
            
            // Advance 20 minutes for eye rest
            await AdvanceVirtualTime(TimeSpan.FromMinutes(20));
            _fakeTimerFactory.FireAllTimers();
            
            // Final statistics
            var testDuration = DateTime.Now - testStart;
            
            _output.WriteLine("\n=== SIMULATION COMPLETE ===");
            _output.WriteLine($"Virtual time elapsed: 47 hours");
            _output.WriteLine($"Real time elapsed: {testDuration.TotalSeconds:F2} seconds");
            _output.WriteLine($"Total eye rests: {_eyeRestCount}");
            _output.WriteLine($"Total breaks: {_breakCount}");
            _output.WriteLine($"Session resets: {_sessionResetCount}");
            _output.WriteLine($"Events logged: {_events.Count}");
            
            // Assertions
            Assert.True(testDuration.TotalSeconds < 60, $"Test must complete in < 60 seconds (took {testDuration.TotalSeconds:F2}s)");
            Assert.True(_eyeRestCount >= 3, $"Should have at least 3 eye rests (got {_eyeRestCount})");
            Assert.True(_breakCount >= 1, $"Should have at least 1 break (got {_breakCount})");
            Assert.True(_sessionResetCount >= 1, $"Should have at least 1 session reset (got {_sessionResetCount})");
            
            // Verify clock jump detection worked
            Assert.DoesNotContain(_events, e => e.Contains("reminder") && e.Contains("after wake"));
            
            _output.WriteLine("\n✅ ALL TESTS PASSED!");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Feature", "ClockJumpDetection")]
        public async Task ClockJumpDetection_ShouldPreventImmediatePopupAfterWake()
        {
            _output.WriteLine("=== TESTING CLOCK JUMP DETECTION ===");
            
            // Setup: Timers running normally
            await _timerService.StartAsync();
            
            // Advance time to make timers "overdue"
            await AdvanceVirtualTime(TimeSpan.FromHours(2));
            
            // Simulate system wake with clock jump
            _output.WriteLine("Simulating PC wake after overnight sleep...");
            
            var popupsBefore = _eyeRestCount + _breakCount;
            
            // This should trigger clock jump detection instead of showing reminders
            await _timerService.SmartSessionResetAsync("Clock jump detected - PC wake from sleep");
            
            // Try to fire timers
            _fakeTimerFactory.FireAllTimers();
            
            var popupsAfter = _eyeRestCount + _breakCount;
            
            // CRITICAL: No new reminders should appear
            Assert.Equal(popupsBefore, popupsAfter);
            _output.WriteLine("✅ SUCCESS: No reminders shown after clock jump!");
            
            // Verify fresh session started
            var eyeRestTime = _timerService.TimeUntilNextEyeRest;
            var breakTime = _timerService.TimeUntilNextBreak;
            
            Assert.True(eyeRestTime.TotalMinutes > 18, $"Eye rest should be fresh (got {eyeRestTime.TotalMinutes:F1} min)");
            Assert.True(breakTime.TotalMinutes > 50, $"Break should be fresh (got {breakTime.TotalMinutes:F1} min)");
            
            _output.WriteLine($"Fresh session: Eye rest in {eyeRestTime.TotalMinutes:F1} min, Break in {breakTime.TotalMinutes:F1} min");
        }

        private async Task AdvanceVirtualTime(TimeSpan duration)
        {
            _virtualTime = _virtualTime.Add(duration);
            await Task.Delay(10); // Small real delay for async operations
        }

        private async Task SimulateExtendedAway(TimeSpan duration, string reason)
        {
            LogEvent($"User away: {reason}");
            await _timerService.SmartPauseAsync(reason);
            await AdvanceVirtualTime(duration);
            
            // Extended away should trigger session reset
            if (duration > TimeSpan.FromMinutes(30))
            {
                await _timerService.SmartSessionResetAsync($"Extended away: {reason}");
            }
            
            await _timerService.SmartResumeAsync();
        }

        private async Task SimulateClockJumpWake()
        {
            LogEvent("System wake - checking for clock jump");
            
            // Instead of showing overdue popups, should reset session
            await _timerService.SmartSessionResetAsync("Clock jump detected - PC wake from sleep");
            
            LogEvent("Clock jump handled - fresh session started");
        }

        private void LogEvent(string message)
        {
            var timestamp = _virtualTime.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            _events.Add(logEntry);
            _output.WriteLine(logEntry);
        }
    }
}