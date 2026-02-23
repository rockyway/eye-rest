using System;
using System.ComponentModel;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Avalonia.ViewModels
{
    public class MainWindowViewModelTests : IDisposable
    {
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly Mock<ITimerConfigurationService> _mockTimerConfigService;
        private readonly Mock<IUIConfigurationService> _mockUIConfigService;
        private readonly Mock<ITimerService> _mockTimerService;
        private readonly Mock<IStartupManager> _mockStartupManager;
        private readonly Mock<ILogger<MainWindowViewModel>> _mockLogger;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IScreenOverlayService> _mockScreenOverlayService;
        private readonly MainWindowViewModel _viewModel;
        private readonly AppConfiguration _testConfig;

        public MainWindowViewModelTests()
        {
            _mockConfigService = new Mock<IConfigurationService>();
            _mockTimerConfigService = new Mock<ITimerConfigurationService>();
            _mockUIConfigService = new Mock<IUIConfigurationService>();
            _mockTimerService = new Mock<ITimerService>();
            _mockStartupManager = new Mock<IStartupManager>();
            _mockLogger = new Mock<ILogger<MainWindowViewModel>>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockScreenOverlayService = new Mock<IScreenOverlayService>();

            _testConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    StartSoundEnabled = true,
                    EndSoundEnabled = true,
                    WarningEnabled = true,
                    WarningSeconds = 30
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningEnabled = true,
                    WarningSeconds = 30,
                    OverlayOpacityPercent = 50
                },
                Audio = new AudioSettings
                {
                    Enabled = true,
                    Volume = 50
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false,
                    IsDarkMode = false
                },
                Analytics = new AnalyticsSettings
                {
                    Enabled = true,
                    AutoOpenDashboard = false
                },
                UserPresence = new UserPresenceSettings(),
                MeetingDetection = new MeetingDetectionSettings(),
                TimerControls = new TimerControlSettings()
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_testConfig);
            _mockConfigService.Setup(x => x.GetDefaultConfiguration())
                .ReturnsAsync(_testConfig);
            _mockStartupManager.Setup(x => x.IsStartupEnabled())
                .Returns(false);

            _viewModel = new MainWindowViewModel(
                _mockConfigService.Object,
                _mockTimerConfigService.Object,
                _mockUIConfigService.Object,
                _mockTimerService.Object,
                _mockStartupManager.Object,
                _mockNotificationService.Object,
                _mockScreenOverlayService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task LoadConfigurationImmediatelyAsync_UpdatesProperties()
        {
            // Act
            await _viewModel.LoadConfigurationImmediatelyAsync();

            // Assert - Configuration should be loaded and properties updated
            Assert.Equal(20, _viewModel.EyeRestIntervalMinutes);
            Assert.Equal(20, _viewModel.EyeRestDurationSeconds);
            Assert.Equal(55, _viewModel.BreakIntervalMinutes);
            Assert.Equal(5, _viewModel.BreakDurationMinutes);
            Assert.True(_viewModel.AudioEnabled);
            Assert.Equal(50, _viewModel.AudioVolume);
            Assert.False(_viewModel.StartWithWindows);
        }

        [Fact]
        public async Task LoadConfigurationImmediatelyAsync_SyncsStartupSetting()
        {
            // Arrange
            _mockStartupManager.Setup(x => x.IsStartupEnabled()).Returns(true);

            // Act
            await _viewModel.LoadConfigurationImmediatelyAsync();

            // Assert - StartWithWindows should sync with actual startup manager status
            Assert.True(_viewModel.StartWithWindows);
        }

        [Fact]
        public async Task LoadConfigurationImmediatelyAsync_OnFailure_UsesDefaults()
        {
            // Arrange
            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ThrowsAsync(new Exception("Config file corrupt"));

            // Act
            await _viewModel.LoadConfigurationImmediatelyAsync();

            // Assert - Should fall back to defaults
            Assert.Equal(20, _viewModel.EyeRestIntervalMinutes);
            Assert.Equal(55, _viewModel.BreakIntervalMinutes);
        }

        [Fact]
        public void EyeRestIntervalMinutes_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.EyeRestIntervalMinutes = 30;

            // Assert
            Assert.Equal(30, _viewModel.EyeRestIntervalMinutes);
        }

        [Fact]
        public void EyeRestDurationSeconds_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.EyeRestDurationSeconds = 40;

            // Assert
            Assert.Equal(40, _viewModel.EyeRestDurationSeconds);
        }

        [Fact]
        public void BreakIntervalMinutes_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.BreakIntervalMinutes = 60;

            // Assert
            Assert.Equal(60, _viewModel.BreakIntervalMinutes);
        }

        [Fact]
        public void BreakDurationMinutes_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.BreakDurationMinutes = 10;

            // Assert
            Assert.Equal(10, _viewModel.BreakDurationMinutes);
        }

        [Fact]
        public void AudioEnabled_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.AudioEnabled = false;

            // Assert
            Assert.False(_viewModel.AudioEnabled);
        }

        [Fact]
        public void AudioVolume_SetValue_UpdatesProperty()
        {
            // Act
            _viewModel.AudioVolume = 75;

            // Assert
            Assert.Equal(75, _viewModel.AudioVolume);
        }

        [Fact]
        public void PropertyChanged_RaisedOnPropertySet()
        {
            // Arrange
            var propertyChangedRaised = false;
            string? changedPropertyName = null;

            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.EyeRestIntervalMinutes))
                {
                    propertyChangedRaised = true;
                    changedPropertyName = args.PropertyName;
                }
            };

            // Act
            _viewModel.EyeRestIntervalMinutes = 30;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(nameof(MainWindowViewModel.EyeRestIntervalMinutes), changedPropertyName);
        }

        [Fact]
        public void TimerStatusText_DefaultsToStopped()
        {
            // Assert
            Assert.Equal("Stopped", _viewModel.TimerStatusText);
        }

        [Fact]
        public void WindowTitle_DefaultsToStopped()
        {
            // Assert
            Assert.Equal("Eye-rest Settings - Stopped", _viewModel.WindowTitle);
        }

        [Fact]
        public void IsRunning_DefaultsToFalse()
        {
            // Assert
            Assert.False(_viewModel.IsRunning);
        }

        [Fact]
        public void Commands_AreInitialized()
        {
            // Assert - All commands should be non-null
            Assert.NotNull(_viewModel.RestoreDefaultsCommand);
            Assert.NotNull(_viewModel.StartTimersCommand);
            Assert.NotNull(_viewModel.StopTimersCommand);
            Assert.NotNull(_viewModel.PauseTimersCommand);
            Assert.NotNull(_viewModel.ResumeTimersCommand);
            Assert.NotNull(_viewModel.PauseForMeetingCommand);
            Assert.NotNull(_viewModel.PauseForMeeting1hCommand);
            Assert.NotNull(_viewModel.ExitApplicationCommand);
            Assert.NotNull(_viewModel.ShowAnalyticsCommand);
        }

        [Fact]
        public async Task StartTimersCommand_CallsTimerServiceStart()
        {
            // Act
            await Task.Run(() => _viewModel.StartTimersCommand.Execute(null));

            // Assert
            _mockTimerService.Verify(x => x.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StopTimersCommand_CallsTimerServiceStop()
        {
            // Act
            await Task.Run(() => _viewModel.StopTimersCommand.Execute(null));

            // Assert
            _mockTimerService.Verify(x => x.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task RestoreDefaultsCommand_RestoresDefaultConfiguration()
        {
            // Arrange
            _viewModel.EyeRestIntervalMinutes = 30;

            // Act
            await Task.Run(() => _viewModel.RestoreDefaultsCommand.Execute(null));

            // Assert
            _mockConfigService.Verify(x => x.GetDefaultConfiguration(), Times.AtLeastOnce);
            _mockConfigService.Verify(x => x.SaveConfigurationAsync(It.IsAny<AppConfiguration>()), Times.AtLeastOnce);
        }

        [Fact]
        public void UpdateCountdown_WhenNotRunning_ShowsNotRunningText()
        {
            // Arrange
            _mockTimerService.Setup(x => x.IsRunning).Returns(false);

            // Act
            _viewModel.UpdateCountdown();

            // Assert
            Assert.Equal("Timers not running", _viewModel.DualCountdownText);
            Assert.Equal("Timers not running", _viewModel.TimeUntilNextEyeRest);
            Assert.Equal("Timers not running", _viewModel.TimeUntilNextBreak);
            Assert.False(_viewModel.IsRunning);
        }

        [Fact]
        public void UpdateCountdown_WhenRunning_ShowsCountdownText()
        {
            // Arrange
            _mockTimerService.Setup(x => x.IsRunning).Returns(true);
            _mockTimerService.Setup(x => x.IsPaused).Returns(false);
            _mockTimerService.Setup(x => x.IsSmartPaused).Returns(false);
            _mockTimerService.Setup(x => x.IsManuallyPaused).Returns(false);
            _mockTimerService.Setup(x => x.TimeUntilNextEyeRest).Returns(TimeSpan.FromMinutes(15));
            _mockTimerService.Setup(x => x.TimeUntilNextBreak).Returns(TimeSpan.FromMinutes(40));

            // Act
            _viewModel.UpdateCountdown();

            // Assert
            Assert.True(_viewModel.IsRunning);
            Assert.Contains("15m", _viewModel.TimeUntilNextEyeRest);
            Assert.Contains("40m", _viewModel.TimeUntilNextBreak);
        }

        [Fact]
        public void UpdateCountdown_WhenPaused_ShowsPausedText()
        {
            // Arrange
            _mockTimerService.Setup(x => x.IsRunning).Returns(true);
            _mockTimerService.Setup(x => x.IsPaused).Returns(true);
            _mockTimerService.Setup(x => x.IsSmartPaused).Returns(false);
            _mockTimerService.Setup(x => x.IsManuallyPaused).Returns(false);

            // Act
            _viewModel.UpdateCountdown();

            // Assert
            Assert.Contains("paused", _viewModel.DualCountdownText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UpdateCountdown_WhenManuallyPaused_ShowsMeetingPauseText()
        {
            // Arrange
            _mockTimerService.Setup(x => x.IsRunning).Returns(true);
            _mockTimerService.Setup(x => x.IsPaused).Returns(false);
            _mockTimerService.Setup(x => x.IsSmartPaused).Returns(false);
            _mockTimerService.Setup(x => x.IsManuallyPaused).Returns(true);
            _mockTimerService.Setup(x => x.ManualPauseRemaining).Returns(TimeSpan.FromMinutes(25));

            // Act
            _viewModel.UpdateCountdown();

            // Assert
            Assert.Contains("Meeting pause", _viewModel.TimeUntilNextEyeRest, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(120)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(121)]
        public void EyeRestIntervalMinutes_AcceptsAnyValue(int value)
        {
            // Act
            _viewModel.EyeRestIntervalMinutes = value;

            // Assert - Property setter accepts any value; validation is deferred to save
            Assert.Equal(value, _viewModel.EyeRestIntervalMinutes);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(20)]
        [InlineData(300)]
        [InlineData(4)]
        [InlineData(-1)]
        [InlineData(301)]
        public void EyeRestDurationSeconds_AcceptsAnyValue(int value)
        {
            // Act
            _viewModel.EyeRestDurationSeconds = value;

            // Assert - Property setter accepts any value; validation is deferred to save
            Assert.Equal(value, _viewModel.EyeRestDurationSeconds);
        }

        [Fact]
        public void IsDarkMode_SetValue_UpdatesProperty()
        {
            // Act - Note: This may throw in test context since Avalonia.Application.Current is null,
            // but the property should still be set via backing field
            _viewModel.IsDarkMode = true;

            // Assert
            Assert.True(_viewModel.IsDarkMode);
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => _viewModel.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert - calling Dispose multiple times should not throw
            var exception = Record.Exception(() =>
            {
                _viewModel.Dispose();
                _viewModel.Dispose();
            });
            Assert.Null(exception);
        }

        public void Dispose()
        {
            _viewModel.Dispose();
        }
    }
}
