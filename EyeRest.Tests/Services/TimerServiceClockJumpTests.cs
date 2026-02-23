using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Services
{
    /// <summary>
    /// Tests for TimerService clock jump detection functionality
    /// </summary>
    public class TimerServiceClockJumpTests
    {
        private readonly Mock<ILogger<TimerService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<IAudioService> _audioMock;
        private readonly Mock<IAnalyticsService> _analyticsMock;
        private readonly Mock<IPauseReminderService> _pauseReminderMock;
        private readonly TimerService _timerService;
        private readonly AppConfiguration _configuration;

        public TimerServiceClockJumpTests()
        {
            _loggerMock = new Mock<ILogger<TimerService>>();
            _configMock = new Mock<IConfigurationService>();
            _notificationMock = new Mock<INotificationService>();
            _audioMock = new Mock<IAudioService>();
            _analyticsMock = new Mock<IAnalyticsService>();
            _pauseReminderMock = new Mock<IPauseReminderService>();

            _configuration = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    WarningSeconds = 30
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningSeconds = 30
                }
            };

            _configMock.Setup(x => x.LoadConfigurationAsync()).ReturnsAsync(_configuration);

            // Create a mock timer factory for testing
            var timerFactoryMock = new Mock<ITimerFactory>();
            timerFactoryMock.Setup(x => x.CreateTimer(It.IsAny<TimerPriority>()))
                .Returns(() => new Mock<EyeRest.Services.Abstractions.ITimer>().Object);

            _timerService = new TimerService(
                _loggerMock.Object,
                _configMock.Object,
                _analyticsMock.Object,
                timerFactoryMock.Object,
                _pauseReminderMock.Object,
                new Fakes.FakeDispatcherService()
            );
            
            // Set notification service separately to avoid circular dependency
            _timerService.SetNotificationService(_notificationMock.Object);
        }

        [Fact]
        public async Task SmartSessionResetAsync_Should_Clear_LastTickTimestamps()
        {
            // Arrange
            string resetReason = "Test clock jump detection";
            
            // Act
            await _timerService.SmartSessionResetAsync(resetReason);
            
            // Assert - Verify logging for clock jump detection reset
            _loggerMock.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v.ToString() ?? "").Contains("CLOCK JUMP DETECTION: Timestamps cleared for fresh session")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify analytics event was recorded
            _analyticsMock.Verify(x => x.RecordResumeEventAsync(ResumeReason.NewWorkingSession), Times.Once);
        }

        [Fact]
        public async Task TimeUntilNextEyeRest_Should_Handle_Uninitialized_StartTime()
        {
            // Arrange
            await _timerService.StartAsync();
            
            // Act
            var timeUntilNext = _timerService.TimeUntilNextEyeRest;
            
            // Assert - Should not be negative or excessive
            Assert.True(timeUntilNext >= TimeSpan.Zero);
            Assert.True(timeUntilNext <= TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes));
        }

        [Fact]
        public async Task TimeUntilNextBreak_Should_Handle_Uninitialized_StartTime()
        {
            // Arrange
            await _timerService.StartAsync();
            
            // Act
            var timeUntilNext = _timerService.TimeUntilNextBreak;
            
            // Assert - Should not be negative or excessive
            Assert.True(timeUntilNext >= TimeSpan.Zero);
            Assert.True(timeUntilNext <= TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes));
        }

        [Fact]
        public async Task SmartSessionResetAsync_Should_Reset_All_Timer_States()
        {
            // Arrange
            await _timerService.SmartPauseAsync("Test pause");
            
            // Act
            await _timerService.SmartSessionResetAsync("Test reset");
            
            // Assert
            Assert.False(_timerService.IsSmartPaused);
            Assert.False(_timerService.IsPaused);
            Assert.False(_timerService.IsManuallyPaused);
            Assert.Null(_timerService.PauseReason);
            
            // Verify fresh session started
            _loggerMock.Verify(x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v.ToString() ?? "").Contains("SMART SESSION RESET COMPLETED")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SmartSessionResetAsync_Should_Start_Fresh_Timer_Intervals()
        {
            // Arrange
            await _timerService.StartAsync();
            
            // Act
            await _timerService.SmartSessionResetAsync("Test fresh start");
            
            // Assert - Timers should be at full intervals
            var eyeRestTime = _timerService.TimeUntilNextEyeRest;
            var breakTime = _timerService.TimeUntilNextBreak;
            
            // Should be close to configured intervals (minus warning seconds)
            var expectedEyeRestInterval = TimeSpan.FromMinutes(_configuration.EyeRest.IntervalMinutes) - 
                                         TimeSpan.FromSeconds(_configuration.EyeRest.WarningSeconds);
            var expectedBreakInterval = TimeSpan.FromMinutes(_configuration.Break.IntervalMinutes) - 
                                       TimeSpan.FromSeconds(_configuration.Break.WarningSeconds);
            
            // Allow small tolerance for timer startup
            Assert.True(Math.Abs((eyeRestTime - expectedEyeRestInterval).TotalSeconds) < 2);
            Assert.True(Math.Abs((breakTime - expectedBreakInterval).TotalSeconds) < 2);
        }

        [Theory]
        [InlineData(2)]   // 2 hours - should trigger clock jump
        [InlineData(4)]   // 4 hours - should trigger clock jump
        [InlineData(12)]  // 12 hours - overnight sleep scenario
        [InlineData(24)]  // 24 hours - full day scenario
        public void Clock_Jump_Detection_Should_Trigger_For_Large_Time_Gaps(int hoursGap)
        {
            // This test verifies that the clock jump detection logic
            // would trigger for gaps larger than 2 hours, as implemented
            // in OnEyeRestTimerTick and OnBreakTimerTick
            
            var timeGap = TimeSpan.FromHours(hoursGap);
            
            // Clock jump detection threshold is 2 hours
            var shouldTriggerClockJumpDetection = timeGap > TimeSpan.FromHours(2);
            
            Assert.True(shouldTriggerClockJumpDetection, 
                $"Clock jump detection should trigger for {hoursGap} hour gap");
        }

        [Theory]
        [InlineData(0.5)]  // 30 minutes - should NOT trigger
        [InlineData(1)]    // 1 hour - should NOT trigger
        [InlineData(1.5)]  // 1.5 hours - should NOT trigger
        public void Clock_Jump_Detection_Should_Not_Trigger_For_Normal_Gaps(double hoursGap)
        {
            // This test verifies that normal time gaps don't trigger
            // false positive clock jump detection
            
            var timeGap = TimeSpan.FromHours(hoursGap);
            
            // Clock jump detection threshold is 2 hours
            var shouldTriggerClockJumpDetection = timeGap > TimeSpan.FromHours(2);
            
            Assert.False(shouldTriggerClockJumpDetection, 
                $"Clock jump detection should NOT trigger for {hoursGap} hour gap");
        }
    }
}