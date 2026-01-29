using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

namespace EyeRest.Tests.Helpers
{
    /// <summary>
    /// Helper class for timer-related tests with common utilities and configurations
    /// </summary>
    public static class TimerTestHelper
    {
        /// <summary>
        /// Predefined test configurations for various scenarios
        /// </summary>
        public static class TestConfigurations
        {
            public static AppConfiguration UltraFast => CreateConfiguration(5, 10, 1, 2);
            public static AppConfiguration Fast => CreateConfiguration(10, 20, 2, 3);
            public static AppConfiguration Short => CreateConfiguration(30, 60, 5, 10);
            public static AppConfiguration Medium => CreateConfiguration(120, 240, 10, 20);
            public static AppConfiguration Long => CreateConfiguration(300, 600, 15, 30);
            
            public static AppConfiguration AsymmetricEyeRestLonger => CreateConfiguration(300, 60, 10, 5);
            public static AppConfiguration AsymmetricBreakLonger => CreateConfiguration(30, 300, 5, 15);
            
            public static AppConfiguration MinimalWarning => CreateConfiguration(60, 120, 1, 1);
            public static AppConfiguration LongWarning => CreateConfiguration(30, 60, 10, 20);
            
            public static AppConfiguration WithUserPresence => CreateConfiguration(60, 120, 5, 10, 
                userPresenceEnabled: true, idleThresholdSeconds: 5, extendedAwayThresholdSeconds: 30);
        }

        /// <summary>
        /// Creates a test configuration with specified parameters
        /// </summary>
        public static AppConfiguration CreateConfiguration(
            int eyeRestIntervalSeconds,
            int breakIntervalSeconds,
            int eyeRestWarningSeconds = 5,
            int breakWarningSeconds = 10,
            bool userPresenceEnabled = false,
            int idleThresholdSeconds = 300,
            int extendedAwayThresholdSeconds = 1800)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = (int)(eyeRestIntervalSeconds / 60.0),
                    DurationSeconds = 5,
                    WarningSeconds = eyeRestWarningSeconds,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = (int)(breakIntervalSeconds / 60.0),
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = breakWarningSeconds,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = userPresenceEnabled,
                    IdleThresholdMinutes = (int)(idleThresholdSeconds / 60.0),
                    AwayGracePeriodSeconds = 2,
                    AutoPauseOnAway = true,
                    AutoResumeOnReturn = true,
                    ExtendedAwayThresholdMinutes = (int)(extendedAwayThresholdSeconds / 60.0),
                    EnableSmartSessionReset = true
                },
                Audio = new AudioSettings
                {
                    Enabled = false // Always disabled for tests
                }
            };
        }

        /// <summary>
        /// Creates a timer service with mocked dependencies
        /// </summary>
        public static TimerService CreateTimerService(
            AppConfiguration config, 
            out Mock<ILogger<TimerService>> mockLogger,
            out Mock<IConfigurationService> mockConfigService,
            out Mock<IAnalyticsService> mockAnalyticsService,
            out FakeTimerFactory fakeTimerFactory,
            out Mock<IPauseReminderService> mockPauseReminderService)
        {
            mockLogger = new Mock<ILogger<TimerService>>();
            mockConfigService = new Mock<IConfigurationService>();
            mockAnalyticsService = new Mock<IAnalyticsService>();
            mockPauseReminderService = new Mock<IPauseReminderService>();
            fakeTimerFactory = new FakeTimerFactory();

            mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);

            return new TimerService(mockLogger.Object, mockConfigService.Object, mockAnalyticsService.Object, fakeTimerFactory, mockPauseReminderService.Object);
        }

        /// <summary>
        /// Creates a timer service with default mocks
        /// </summary>
        public static TimerService CreateTimerService(AppConfiguration config)
        {
            return CreateTimerService(config, out _, out _, out _, out _, out _);
        }

        /// <summary>
        /// Captures log messages from a mocked logger
        /// </summary>
        public static List<string> CaptureLogMessages(Mock<ILogger<TimerService>> mockLogger)
        {
            var logMessages = new List<string>();
            
            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) =>
                {
                    var message = formatter.DynamicInvoke(state, exception)?.ToString() ?? "";
                    logMessages.Add(message);
                });
            
            return logMessages;
        }

        /// <summary>
        /// Event collector for capturing timer events
        /// </summary>
        public class TimerEventCollector : IDisposable
        {
            private readonly TimerService _timerService;
            private readonly List<TimerEventInfo> _events = new();

            public IReadOnlyList<TimerEventInfo> Events => _events.AsReadOnly();

            public TimerEventCollector(TimerService timerService)
            {
                _timerService = timerService;
                
                _timerService.EyeRestWarning += OnEyeRestWarning;
                _timerService.EyeRestDue += OnEyeRestDue;
                _timerService.BreakWarning += OnBreakWarning;
                _timerService.BreakDue += OnBreakDue;
            }

            private void OnEyeRestWarning(object? sender, TimerEventArgs e) =>
                _events.Add(new TimerEventInfo(DateTime.Now, "EyeRestWarning", e));

            private void OnEyeRestDue(object? sender, TimerEventArgs e) =>
                _events.Add(new TimerEventInfo(DateTime.Now, "EyeRestDue", e));

            private void OnBreakWarning(object? sender, TimerEventArgs e) =>
                _events.Add(new TimerEventInfo(DateTime.Now, "BreakWarning", e));

            private void OnBreakDue(object? sender, TimerEventArgs e) =>
                _events.Add(new TimerEventInfo(DateTime.Now, "BreakDue", e));

            public void Clear() => _events.Clear();

            public TimerEventInfo? FindEvent(string eventType) =>
                _events.FirstOrDefault(e => e.EventType == eventType);

            public List<TimerEventInfo> FindEvents(string eventType) =>
                _events.Where(e => e.EventType == eventType).ToList();

            public void Dispose()
            {
                _timerService.EyeRestWarning -= OnEyeRestWarning;
                _timerService.EyeRestDue -= OnEyeRestDue;
                _timerService.BreakWarning -= OnBreakWarning;
                _timerService.BreakDue -= OnBreakDue;
            }
        }

        /// <summary>
        /// Information about a timer event
        /// </summary>
        public record TimerEventInfo(DateTime Time, string EventType, TimerEventArgs EventArgs);

        /// <summary>
        /// Wait for a specific timer event with timeout
        /// </summary>
        public static async Task<TimerEventArgs> WaitForEventAsync(
            TimerService timerService,
            string eventType,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<TimerEventArgs>();

            EventHandler<TimerEventArgs>? handler = (s, e) => tcs.TrySetResult(e);

            try
            {
                switch (eventType.ToLower())
                {
                    case "eyerestwarning":
                        timerService.EyeRestWarning += handler;
                        break;
                    case "eyerestdue":
                        timerService.EyeRestDue += handler;
                        break;
                    case "breakwarning":
                        timerService.BreakWarning += handler;
                        break;
                    case "breakdue":
                        timerService.BreakDue += handler;
                        break;
                    default:
                        throw new ArgumentException($"Unknown event type: {eventType}");
                }

                return await tcs.Task.WaitAsync(timeout, cancellationToken);
            }
            finally
            {
                switch (eventType.ToLower())
                {
                    case "eyerestwarning":
                        timerService.EyeRestWarning -= handler;
                        break;
                    case "eyerestdue":
                        timerService.EyeRestDue -= handler;
                        break;
                    case "breakwarning":
                        timerService.BreakWarning -= handler;
                        break;
                    case "breakdue":
                        timerService.BreakDue -= handler;
                        break;
                }
            }
        }

        /// <summary>
        /// Assert that timer state matches expected values
        /// </summary>
        public static async Task AssertTimerStateAsync(
            TimerService timerService,
            bool shouldBeRunning,
            bool shouldBePaused,
            bool shouldBeSmartPaused,
            TimeSpan maxWait)
        {
            var deadline = DateTime.Now.Add(maxWait);
            
            while (DateTime.Now < deadline)
            {
                if (timerService.IsRunning == shouldBeRunning &&
                    timerService.IsPaused == shouldBePaused &&
                    timerService.IsSmartPaused == shouldBeSmartPaused)
                {
                    return; // Success
                }
                
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
            
            throw new Exception(
                $"Timer state mismatch after {maxWait}. " +
                $"Expected: Running={shouldBeRunning}, Paused={shouldBePaused}, SmartPaused={shouldBeSmartPaused}. " +
                $"Actual: Running={timerService.IsRunning}, Paused={timerService.IsPaused}, SmartPaused={timerService.IsSmartPaused}");
        }

        /// <summary>
        /// Measure the accuracy of timer countdown
        /// </summary>
        public static async Task<TimeSpan> MeasureTimerAccuracy(
            TimerService timerService,
            TimeSpan measurementPeriod)
        {
            var initialTime = timerService.TimeUntilNextEyeRest;
            var startTime = DateTime.Now;
            
            await Task.Delay(measurementPeriod);
            
            var finalTime = timerService.TimeUntilNextEyeRest;
            var actualElapsed = DateTime.Now - startTime;
            
            var expectedDecrease = actualElapsed;
            var actualDecrease = initialTime - finalTime;
            
            return actualDecrease - expectedDecrease; // Positive means timer is slow, negative means fast
        }

        /// <summary>
        /// Performance measurement helper
        /// </summary>
        public static async Task<TimeSpan> MeasureExecutionTime(Func<Task> operation)
        {
            var startTime = DateTime.Now;
            await operation();
            return DateTime.Now - startTime;
        }

        /// <summary>
        /// Performance measurement helper for synchronous operations
        /// </summary>
        public static TimeSpan MeasureExecutionTime(Action operation)
        {
            var startTime = DateTime.Now;
            operation();
            return DateTime.Now - startTime;
        }

        /// <summary>
        /// Stress test helper - runs operation multiple times and measures performance
        /// </summary>
        public static async Task<StressTestResults> RunStressTest(
            Func<Task> operation,
            int iterations,
            TimeSpan maxAcceptableTime)
        {
            var results = new List<TimeSpan>();
            var errors = new List<Exception>();
            
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var elapsed = await MeasureExecutionTime(operation);
                    results.Add(elapsed);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
            
            return new StressTestResults
            {
                TotalIterations = iterations,
                SuccessfulIterations = results.Count,
                FailedIterations = errors.Count,
                ExecutionTimes = results,
                Errors = errors,
                AverageTime = results.Count > 0 ? TimeSpan.FromTicks((long)results.Average(t => t.Ticks)) : TimeSpan.Zero,
                MaxTime = results.Count > 0 ? results.Max() : TimeSpan.Zero,
                MinTime = results.Count > 0 ? results.Min() : TimeSpan.Zero,
                PassedPerformanceTest = results.All(t => t <= maxAcceptableTime)
            };
        }

        /// <summary>
        /// Results from stress testing
        /// </summary>
        public class StressTestResults
        {
            public int TotalIterations { get; init; }
            public int SuccessfulIterations { get; init; }
            public int FailedIterations { get; init; }
            public List<TimeSpan> ExecutionTimes { get; init; } = new();
            public List<Exception> Errors { get; init; } = new();
            public TimeSpan AverageTime { get; init; }
            public TimeSpan MaxTime { get; init; }
            public TimeSpan MinTime { get; init; }
            public bool PassedPerformanceTest { get; init; }
        }

        /// <summary>
        /// Memory usage monitoring helper
        /// </summary>
        public static long GetCurrentMemoryUsage()
        {
            return GC.GetTotalMemory(false);
        }

        /// <summary>
        /// Force garbage collection and measure memory usage
        /// </summary>
        public static long GetMemoryUsageAfterGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(false);
        }

        /// <summary>
        /// Calculate expected heartbeat threshold for given configuration
        /// </summary>
        public static double CalculateExpectedHeartbeatThreshold(AppConfiguration config)
        {
            var eyeRestMinutes = config.EyeRest.IntervalMinutes;
            var breakMinutes = config.Break.IntervalMinutes;
            var longestMinutes = Math.Max(eyeRestMinutes, breakMinutes);
            
            // Formula: (longest * 1.25) + 5, clamped to [10, 120]
            var threshold = (longestMinutes * 1.25) + 5.0;
            return Math.Max(10.0, Math.Min(120.0, threshold));
        }

        /// <summary>
        /// Validate configuration parameters
        /// </summary>
        public static List<string> ValidateConfiguration(AppConfiguration config)
        {
            var issues = new List<string>();

            if (config.EyeRest.IntervalMinutes <= 0)
                issues.Add("Eye rest interval must be positive");
            
            if (config.Break.IntervalMinutes <= 0)
                issues.Add("Break interval must be positive");
            
            if (config.EyeRest.WarningSeconds >= config.EyeRest.IntervalMinutes * 60)
                issues.Add("Eye rest warning is longer than interval");
            
            if (config.Break.WarningSeconds >= config.Break.IntervalMinutes * 60)
                issues.Add("Break warning is longer than interval");
            
            if (config.UserPresence.Enabled && config.UserPresence.IdleThresholdMinutes <= 0)
                issues.Add("Idle threshold must be positive when user presence is enabled");

            return issues;
        }
    }
}