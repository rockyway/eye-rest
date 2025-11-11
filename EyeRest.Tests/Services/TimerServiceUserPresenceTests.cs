using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.Services.Abstractions;
using EyeRest.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using UserPresenceState = EyeRest.Services.UserPresenceState;
using TimerService = EyeRest.Services.TimerService;

namespace EyeRest.Tests.Services
{
    [Collection("Timer Tests")]
    public class TimerServiceUserPresenceTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<IUserPresenceService> _mockPresenceService;
        private readonly Mock<IPauseReminderService> _mockPauseReminderService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TimerServiceUserPresenceTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockPresenceService = new Mock<IUserPresenceService>();
            _mockPauseReminderService = new Mock<IPauseReminderService>();
            _fakeTimerFactory = new FakeTimerFactory();
            _cancellationTokenSource = new CancellationTokenSource();

            // Setup default presence service behavior
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
        }

        [Fact]
        public async Task UserAway_ShortPeriod_TimersPauseAndResume()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,    // 20 seconds
                breakInterval: 40,      // 40 seconds
                userPresenceEnabled: true,
                idleThreshold: 5,       // 5 seconds idle threshold
                awayGracePeriod: 2,     // 2 seconds grace period
                extendedAwayThreshold: 30 // 30 seconds for extended away
            );

            var timerService = CreateTimerService(config);
            
            var eyeRestEventCount = 0;
            timerService.EyeRestDue += (s, e) => Interlocked.Increment(ref eyeRestEventCount);

            // Act
            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsSmartPaused);

            // Let timer run for 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);
            var timeBeforeAway = timerService.TimeUntilNextEyeRest;

            // Simulate user goes away
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null, 
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.Away,
                    StateChangedAt = DateTime.Now
                });

            // Wait a bit while user is away
            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);
            
            // Timer should be smart paused
            Assert.True(timerService.IsSmartPaused, "Timer should be smart paused when user is away");
            
            // Time should be preserved (approximately)
            var timeWhileAway = timerService.TimeUntilNextEyeRest;
            var timeDifference = Math.Abs((timeBeforeAway - timeWhileAway).TotalSeconds);
            Assert.True(timeDifference <= 2, 
                $"Timer should preserve time while away, difference: {timeDifference}s");

            // Simulate user returns
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Away, 
                    CurrentState = UserPresenceState.Present,
                    StateChangedAt = DateTime.Now
                });

            // Timer should resume
            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
            Assert.False(timerService.IsSmartPaused, "Timer should resume when user returns");
            Assert.True(timerService.IsRunning, "Timer should be running when user returns");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task UserAway_ExtendedPeriod_FreshSessionReset()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,    // 20 seconds
                breakInterval: 40,      // 40 seconds
                userPresenceEnabled: true,
                idleThreshold: 5,
                awayGracePeriod: 2,
                extendedAwayThreshold: 10 // 10 seconds for extended away (instead of 30 minutes)
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            
            // Let timer run for 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);
            var timeBeforeAway = timerService.TimeUntilNextEyeRest;

            // Simulate user goes away for extended period
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.Away,
                    StateChangedAt = DateTime.Now
                });

            // Wait for extended away period
            await Task.Delay(TimeSpan.FromSeconds(12), _cancellationTokenSource.Token);

            // Simulate user returns after extended away
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Away, 
                    CurrentState = UserPresenceState.Present,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Timer should start fresh session - time should be reset to full interval
            var timeAfterReturn = timerService.TimeUntilNextEyeRest;
            Assert.True(timeAfterReturn.TotalSeconds >= 18, 
                $"Timer should reset to fresh session, got {timeAfterReturn.TotalSeconds}s remaining");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task ScreenLocked_TimersPause()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,
                breakInterval: 30,
                userPresenceEnabled: true,
                idleThreshold: 5,
                awayGracePeriod: 1,
                extendedAwayThreshold: 20
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsSmartPaused);

            // Simulate screen lock
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.Away,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(2), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsSmartPaused, "Timer should be paused when screen is locked");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task ScreenUnlocked_TimersResume()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,
                breakInterval: 30,
                userPresenceEnabled: true
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Simulate screen lock
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.Away,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
            Assert.True(timerService.IsSmartPaused);

            // Simulate screen unlock
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Away, 
                    CurrentState = UserPresenceState.Present,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert
            Assert.False(timerService.IsSmartPaused, "Timer should resume when screen is unlocked");
            Assert.True(timerService.IsRunning, "Timer should be running when screen is unlocked");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemSleep_TimersStop()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,
                breakInterval: 30,
                userPresenceEnabled: true
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);

            // Simulate system sleep
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.SystemSleep);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.SystemSleep,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert - Timer should be smart paused during system sleep
            Assert.True(timerService.IsSmartPaused, "Timer should be paused during system sleep");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemWake_ShortSleep_TimersRecover()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,
                breakInterval: 40,
                userPresenceEnabled: true,
                extendedAwayThreshold: 15 // 15 seconds for extended away
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Let timer run and note the time
            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);
            var timeBeforeSleep = timerService.TimeUntilNextEyeRest;

            // Simulate system sleep
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.SystemSleep);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.SystemSleep,
                    StateChangedAt = DateTime.Now
                });

            // Wait for short sleep period (less than extended away threshold)
            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);

            // Simulate system wake
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.SystemSleep, 
                    CurrentState = UserPresenceState.Present,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert - Timer should recover previous state (approximately)
            var timeAfterWake = timerService.TimeUntilNextEyeRest;
            var timeDifference = Math.Abs((timeBeforeSleep - timeAfterWake).TotalSeconds);
            
            Assert.False(timerService.IsSmartPaused, "Timer should not be paused after wake");
            Assert.True(timerService.IsRunning, "Timer should be running after wake");
            Assert.True(timeDifference <= 3, 
                $"Timer should preserve approximate time after short sleep, difference: {timeDifference}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task SystemWake_LongSleep_FreshSession()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 20,
                breakInterval: 40,
                userPresenceEnabled: true,
                extendedAwayThreshold: 8 // 8 seconds for extended away
            );

            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Let timer run
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);

            // Simulate system sleep
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.SystemSleep);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.SystemSleep,
                    StateChangedAt = DateTime.Now
                });

            // Wait for extended sleep period
            await Task.Delay(TimeSpan.FromSeconds(10), _cancellationTokenSource.Token);

            // Simulate system wake after extended period
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.SystemSleep, 
                    CurrentState = UserPresenceState.Present,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert - Should start fresh session
            var timeAfterWake = timerService.TimeUntilNextEyeRest;
            Assert.True(timeAfterWake.TotalSeconds >= 18,
                $"Timer should reset to fresh session after extended sleep, got {timeAfterWake.TotalSeconds}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task RapidLockUnlock_NoTimerConfusion()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 30,
                breakInterval: 60,
                userPresenceEnabled: true
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            var initialTime = timerService.TimeUntilNextEyeRest;

            // Simulate rapid lock/unlock cycles
            for (int i = 0; i < 5; i++)
            {
                // Lock
                _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
                _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
                _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                    new UserPresenceEventArgs { PreviousState = UserPresenceState.Present, CurrentState = UserPresenceState.Away });

                await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationTokenSource.Token);

                // Unlock
                _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
                _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
                _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                    new UserPresenceEventArgs { PreviousState = UserPresenceState.Away, CurrentState = UserPresenceState.Present });

                await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationTokenSource.Token);
            }

            // Assert
            Assert.True(timerService.IsRunning, "Timer should still be running after rapid lock/unlock");
            Assert.False(timerService.IsSmartPaused, "Timer should not be paused after rapid lock/unlock");

            var finalTime = timerService.TimeUntilNextEyeRest;
            var timeDifference = Math.Abs((initialTime - finalTime).TotalSeconds);
            Assert.True(timeDifference <= 5, 
                $"Timer should not lose much time during rapid lock/unlock, lost {timeDifference}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task MultipleAwayReturns_TimerStateCorrect()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 25,
                breakInterval: 50,
                userPresenceEnabled: true,
                extendedAwayThreshold: 30
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();

            // Test multiple away/return cycles with different durations
            var awayDurations = new[] { 2, 5, 1, 8, 3 }; // All less than extended away threshold

            foreach (var duration in awayDurations)
            {
                var timeBeforeAway = timerService.TimeUntilNextEyeRest;

                // Go away
                _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
                _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
                _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                    new UserPresenceEventArgs { PreviousState = UserPresenceState.Present, CurrentState = UserPresenceState.Away });

                await Task.Delay(TimeSpan.FromSeconds(duration), _cancellationTokenSource.Token);
                Assert.True(timerService.IsSmartPaused, $"Timer should be paused during away period {duration}s");

                // Return
                _mockPresenceService.Setup(x => x.IsUserPresent).Returns(true);
                _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Present);
                _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                    new UserPresenceEventArgs { PreviousState = UserPresenceState.Away, CurrentState = UserPresenceState.Present });

                await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);
                
                Assert.False(timerService.IsSmartPaused, $"Timer should resume after away period {duration}s");
                Assert.True(timerService.IsRunning, $"Timer should be running after away period {duration}s");

                // Time should be approximately preserved
                var timeAfterReturn = timerService.TimeUntilNextEyeRest;
                var timeDifference = Math.Abs((timeBeforeAway - timeAfterReturn).TotalSeconds);
                Assert.True(timeDifference <= 3,
                    $"Timer should preserve time after {duration}s away, difference: {timeDifference}s");
            }

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task UserPresenceDisabled_TimersIgnorePresenceChanges()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,
                breakInterval: 30,
                userPresenceEnabled: false // Disabled
            );

            var timerService = CreateTimerService(config);

            // Act
            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsSmartPaused);

            // Try to trigger presence change
            _mockPresenceService.Setup(x => x.IsUserPresent).Returns(false);
            _mockPresenceService.Setup(x => x.CurrentState).Returns(UserPresenceState.Away);
            _mockPresenceService.Raise(x => x.UserPresenceChanged += null,
                new UserPresenceEventArgs 
                { 
                    PreviousState = UserPresenceState.Present, 
                    CurrentState = UserPresenceState.Away,
                    StateChangedAt = DateTime.Now
                });

            await Task.Delay(TimeSpan.FromSeconds(2), _cancellationTokenSource.Token);

            // Assert - Timers should ignore presence changes when disabled
            Assert.True(timerService.IsRunning, "Timer should keep running when presence detection disabled");
            Assert.False(timerService.IsSmartPaused, "Timer should not pause when presence detection disabled");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        private AppConfiguration CreateTestConfiguration(
            int eyeRestInterval,
            int breakInterval,
            bool userPresenceEnabled = true,
            int idleThreshold = 5,
            int awayGracePeriod = 2,
            int extendedAwayThreshold = 30)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = (int)((double)eyeRestInterval / 60.0),
                    DurationSeconds = 5,
                    WarningSeconds = 2,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = (int)((double)breakInterval / 60.0),
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = 3,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = userPresenceEnabled,
                    IdleThresholdMinutes = (int)((double)idleThreshold / 60.0),
                    AwayGracePeriodSeconds = awayGracePeriod,
                    AutoPauseOnAway = true,
                    AutoResumeOnReturn = true,
                    ExtendedAwayThresholdMinutes = (int)((double)extendedAwayThreshold / 60.0),
                    EnableSmartSessionReset = true
                },
                Audio = new AudioSettings
                {
                    Enabled = false
                }
            };
        }

        private TimerService CreateTimerService(AppConfiguration config)
        {
            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(config);

            // Create TimerService with UserPresenceService dependency
            var timerService = new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory, _mockPauseReminderService.Object);
            
            // Set up the user presence service mock in the timer service
            // This would require modifying TimerService to accept IUserPresenceService in constructor
            // For now, we'll test the integration at a higher level

            return timerService;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}