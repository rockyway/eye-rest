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

namespace EyeRest.Tests.Services
{
    [Collection("Timer Tests")]
    public class TimerServiceStateTransitionTests : IDisposable
    {
        private readonly Mock<ILogger<TimerService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly FakeTimerFactory _fakeTimerFactory;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TimerServiceStateTransitionTests()
        {
            _mockLogger = new Mock<ILogger<TimerService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _fakeTimerFactory = new FakeTimerFactory();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Fact]
        public async Task StateTransition_Stopped_To_Running()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            // Initial state
            Assert.False(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            // Act
            await timerService.StartAsync();

            // Assert
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Running_To_ManuallyPaused()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsPaused);

            // Act
            await timerService.PauseAsync();

            // Assert
            Assert.True(timerService.IsRunning); // Service is running but timers are paused
            Assert.True(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused); // Manual pause, not smart pause

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_ManuallyPaused_To_Running()
        {
            // Arrange
            var config = CreateTestConfiguration(25, 50);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            await timerService.PauseAsync();
            
            Assert.True(timerService.IsPaused);

            // Act
            await timerService.ResumeAsync();

            // Assert
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Running_To_SmartPaused()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsSmartPaused);

            // Act
            await timerService.PauseAsync();

            // Assert
            Assert.True(timerService.IsRunning);
            Assert.True(timerService.IsSmartPaused); // Smart pause due to user presence
            Assert.False(timerService.IsPaused); // Not manually paused

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_SmartPaused_To_Running()
        {
            // Arrange
            var config = CreateTestConfiguration(25, 50);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            await timerService.PauseAsync();
            
            Assert.True(timerService.IsSmartPaused);

            // Act
            await timerService.ResumeAsync();

            // Assert
            Assert.True(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Warning_To_Due_To_Complete()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 15,    // 15 seconds
                breakInterval: 30,      // 30 seconds
                eyeRestWarning: 3,      // 3 seconds warning
                breakWarning: 5         // 5 seconds warning
            );

            var timerService = CreateTimerService(config);
            
            var stateSequence = new List<(DateTime Time, string State)>();
            
            timerService.EyeRestWarning += (s, e) => stateSequence.Add((DateTime.Now, "EyeRestWarning"));
            timerService.EyeRestDue += (s, e) => stateSequence.Add((DateTime.Now, "EyeRestDue"));

            // Act
            await timerService.StartAsync();
            
            // Wait for warning and due events
            await Task.Delay(TimeSpan.FromSeconds(20), _cancellationTokenSource.Token);
            
            await timerService.StopAsync();

            // Assert
            Assert.NotEmpty(stateSequence);
            
            var warning = stateSequence.FirstOrDefault(s => s.State == "EyeRestWarning");
            var due = stateSequence.FirstOrDefault(s => s.State == "EyeRestDue");
            
            if (warning != default && due != default)
            {
                Assert.True(warning.Time < due.Time, "Warning should come before due");
                
                var timeBetween = (due.Time - warning.Time).TotalSeconds;
                Assert.True(timeBetween >= 2 && timeBetween <= 5,
                    $"Time between warning and due should be ~3s, got {timeBetween}s");
            }

            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_ManualPause_During_Warning()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 12,    // 12 seconds
                breakInterval: 24,      // 24 seconds
                eyeRestWarning: 3       // 3 seconds warning
            );

            var timerService = CreateTimerService(config);
            
            var warningReceived = new TaskCompletionSource<TimerEventArgs>();
            timerService.EyeRestWarning += (s, e) => warningReceived.SetResult(e);

            // Act
            await timerService.StartAsync();
            
            // Wait for warning
            var warningEvent = await warningReceived.Task.WaitAsync(TimeSpan.FromSeconds(15), _cancellationTokenSource.Token);
            Assert.NotNull(warningEvent);

            // Pause during warning period
            await timerService.PauseAsync();

            // Assert
            Assert.True(timerService.IsPaused, "Should be paused during warning");
            Assert.True(timerService.IsRunning, "Service should still be running");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_SmartPause_During_BreakPopup()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 60,    // 1 minute
                breakInterval: 20,      // 20 seconds (break comes first)
                breakWarning: 3
            );

            var timerService = CreateTimerService(config);
            
            var breakDueReceived = new TaskCompletionSource<TimerEventArgs>();
            timerService.BreakDue += (s, e) => breakDueReceived.SetResult(e);

            // Act
            await timerService.StartAsync();
            
            // Wait for break due
            var breakEvent = await breakDueReceived.Task.WaitAsync(TimeSpan.FromSeconds(25), _cancellationTokenSource.Token);
            Assert.NotNull(breakEvent);

            // Smart pause during break (simulating user going away)
            await timerService.PauseAsync();

            // Assert
            Assert.True(timerService.IsSmartPaused, "Should be smart paused during break");
            Assert.True(timerService.IsRunning, "Service should still be running");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_ConfigChange_During_Running()
        {
            // Arrange
            var initialConfig = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(initialConfig);

            await timerService.StartAsync();
            
            var initialEyeRestTime = timerService.TimeUntilNextEyeRest;
            await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);

            // Act - Change configuration while running
            var newConfig = CreateTestConfiguration(45, 90); // Different intervals
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = initialConfig,
                NewConfiguration = newConfig
            };

            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);
            
            await Task.Delay(TimeSpan.FromSeconds(1), _cancellationTokenSource.Token);

            // Assert
            Assert.True(timerService.IsRunning, "Should remain running during config change");
            
            var newEyeRestTime = timerService.TimeUntilNextEyeRest;
            // New configuration should affect timing
            Assert.True(newEyeRestTime != initialEyeRestTime, "Timer intervals should change with new config");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Delay_During_Break()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 60,    // 1 minute (longer than break)
                breakInterval: 15,      // 15 seconds
                breakWarning: 2
            );

            var timerService = CreateTimerService(config);
            
            var breakDueReceived = new TaskCompletionSource<TimerEventArgs>();
            timerService.BreakDue += (s, e) => breakDueReceived.SetResult(e);

            // Act
            await timerService.StartAsync();
            
            // Wait for break due
            var breakEvent = await breakDueReceived.Task.WaitAsync(TimeSpan.FromSeconds(20), _cancellationTokenSource.Token);
            Assert.NotNull(breakEvent);

            var timeBeforeDelay = timerService.TimeUntilNextBreak;

            // Delay break by 1 minute
            await timerService.DelayBreak(TimeSpan.FromMinutes(1));

            // Assert
            var timeAfterDelay = timerService.TimeUntilNextBreak;
            Assert.True(timeAfterDelay > timeBeforeDelay, "Break time should increase after delay");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Skip_During_Break()
        {
            // Arrange
            var config = CreateTestConfiguration(
                eyeRestInterval: 60,
                breakInterval: 18,      // 18 seconds
                breakWarning: 3
            );

            var timerService = CreateTimerService(config);
            
            var breakDueReceived = new TaskCompletionSource<TimerEventArgs>();
            timerService.BreakDue += (s, e) => breakDueReceived.SetResult(e);

            // Act
            await timerService.StartAsync();
            
            // Wait for break due
            await breakDueReceived.Task.WaitAsync(TimeSpan.FromSeconds(25), _cancellationTokenSource.Token);

            // Skip break (reset timer)
            await timerService.ResetBreakTimer();

            // Assert
            var timeAfterSkip = timerService.TimeUntilNextBreak;
            Assert.True(timeAfterSkip.TotalSeconds >= 15, 
                $"Break should reset to near full interval after skip, got {timeAfterSkip.TotalSeconds}s");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_MultipleResumeReasons()
        {
            // Test different resume reasons and their state effects
            var config = CreateTestConfiguration(40, 80);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            var resumeReasons = new[] 
            { 
                ResumeReason.Manual, 
                ResumeReason.UserReturned, 
                ResumeReason.SmartDetection 
            };

            foreach (var reason in resumeReasons)
            {
                // Pause with corresponding reason
                var pauseReason = reason == ResumeReason.Manual ? ResumeReason.Manual : ResumeReason.UserReturned;
                await timerService.PauseAsync();
                
                Assert.True(reason == ResumeReason.Manual ? timerService.IsPaused : timerService.IsSmartPaused,
                    $"Should be in correct pause state for {reason}");

                // Resume
                await timerService.ResumeAsync();

                Assert.False(timerService.IsPaused, $"Should not be paused after resume with {reason}");
                Assert.False(timerService.IsSmartPaused, $"Should not be smart paused after resume with {reason}");
                Assert.True(timerService.IsRunning, $"Should be running after resume with {reason}");
            }

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Running_To_Stopped()
        {
            // Arrange
            var config = CreateTestConfiguration(25, 50);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            Assert.True(timerService.IsRunning);

            // Act
            await timerService.StopAsync();

            // Assert
            Assert.False(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Paused_To_Stopped()
        {
            // Arrange
            var config = CreateTestConfiguration(35, 70);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            await timerService.PauseAsync();
            
            Assert.True(timerService.IsPaused);

            // Act
            await timerService.StopAsync();

            // Assert
            Assert.False(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_SmartPaused_To_Stopped()
        {
            // Arrange
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();
            await timerService.PauseAsync();
            
            Assert.True(timerService.IsSmartPaused);

            // Act
            await timerService.StopAsync();

            // Assert
            Assert.False(timerService.IsRunning);
            Assert.False(timerService.IsPaused);
            Assert.False(timerService.IsSmartPaused);

            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_Rapid_Pause_Resume_Cycles()
        {
            // Arrange
            var config = CreateTestConfiguration(60, 120); // Longer intervals for this test
            var timerService = CreateTimerService(config);

            await timerService.StartAsync();

            // Act - Rapid pause/resume cycles
            for (int i = 0; i < 20; i++)
            {
                await timerService.PauseAsync();
                Assert.True(timerService.IsPaused, $"Should be paused in cycle {i}");
                
                await timerService.ResumeAsync();
                Assert.False(timerService.IsPaused, $"Should be resumed in cycle {i}");
                Assert.True(timerService.IsRunning, $"Should be running in cycle {i}");
            }

            // Assert
            Assert.True(timerService.IsRunning, "Should be running after rapid cycles");
            Assert.False(timerService.IsPaused, "Should not be paused after rapid cycles");

            await timerService.StopAsync();
            timerService.Dispose();
        }

        [Fact]
        public async Task StateTransition_InvalidTransitions_Handled()
        {
            // Test invalid state transitions are handled gracefully
            var config = CreateTestConfiguration(30, 60);
            var timerService = CreateTimerService(config);

            // Try to pause when not started
            var exception1 = await Record.ExceptionAsync(async () =>
            {
                await timerService.PauseAsync();
            });

            // Try to resume when not started
            var exception2 = await Record.ExceptionAsync(async () =>
            {
                await timerService.ResumeAsync();
            });

            // Start and try double resume
            await timerService.StartAsync();
            var exception3 = await Record.ExceptionAsync(async () =>
            {
                await timerService.ResumeAsync(); // Not paused
            });

            // Assert - Should handle gracefully (no exceptions or log appropriately)
            // The exact behavior depends on implementation, but should not crash
            
            await timerService.StopAsync();
            timerService.Dispose();
        }

        private AppConfiguration CreateTestConfiguration(
            int eyeRestInterval,
            int breakInterval,
            int eyeRestWarning = 3,
            int breakWarning = 5)
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = (int)((double)eyeRestInterval / 60.0),
                    DurationSeconds = 5,
                    WarningSeconds = eyeRestWarning,
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = (int)((double)breakInterval / 60.0),
                    DurationMinutes = 1,
                    WarningEnabled = true,
                    WarningSeconds = breakWarning,
                    OverlayOpacityPercent = 80,
                    RequireConfirmationAfterBreak = false,
                    ResetTimersOnBreakConfirmation = true
                },
                UserPresence = new UserPresenceSettings
                {
                    Enabled = false // Disable for state transition tests
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

            return new TimerService(_mockLogger.Object, _mockConfigService.Object, _mockAnalyticsService.Object, _fakeTimerFactory);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}