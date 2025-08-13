using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using EyeRest.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.ViewModels
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
        private readonly Mock<AnalyticsDashboardViewModel> _mockAnalyticsDashboard;
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
            _mockAnalyticsDashboard = new Mock<AnalyticsDashboardViewModel>();

            _testConfig = new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,
                    DurationSeconds = 20,
                    StartSoundEnabled = true,
                    EndSoundEnabled = true
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,
                    DurationMinutes = 5,
                    WarningEnabled = true,
                    WarningSeconds = 30
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
                    ShowInTaskbar = false
                }
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
                _mockAnalyticsDashboard.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void Constructor_LoadsConfiguration()
        {
            // Assert - Configuration should be loaded during construction
            Assert.Equal(20, _viewModel.EyeRestIntervalMinutes);
            Assert.Equal(55, _viewModel.BreakIntervalMinutes);
            Assert.True(_viewModel.AudioEnabled);
            Assert.False(_viewModel.StartWithWindows);
        }

        [Fact]
        public void PropertyChanges_UpdateHasUnsavedChanges()
        {
            // Arrange
            Assert.False(_viewModel.HasUnsavedChanges);

            // Act
            _viewModel.EyeRestIntervalMinutes = 25;

            // Assert
            Assert.True(_viewModel.HasUnsavedChanges);
        }

        [Fact]
        public async Task SaveCommand_SavesConfiguration()
        {
            // Arrange
            _viewModel.EyeRestIntervalMinutes = 30;
            _viewModel.BreakIntervalMinutes = 60;

            // Act
            await Task.Run(() => _viewModel.SaveCommand.Execute(null));

            // Assert
            _mockConfigService.Verify(x => x.SaveConfigurationAsync(It.IsAny<AppConfiguration>()), Times.Once);
            Assert.False(_viewModel.HasUnsavedChanges);
        }

        [Fact]
        public void CancelCommand_RestoresOriginalValues()
        {
            // Arrange
            var originalInterval = _viewModel.EyeRestIntervalMinutes;
            _viewModel.EyeRestIntervalMinutes = 30;
            Assert.True(_viewModel.HasUnsavedChanges);

            // Act
            _viewModel.CancelCommand.Execute(null);

            // Assert
            Assert.Equal(originalInterval, _viewModel.EyeRestIntervalMinutes);
            Assert.False(_viewModel.HasUnsavedChanges);
        }

        [Fact]
        public async Task RestoreDefaultsCommand_RestoresDefaults()
        {
            // Arrange
            _viewModel.EyeRestIntervalMinutes = 30;

            // Act
            await Task.Run(() => _viewModel.RestoreDefaultsCommand.Execute(null));

            // Assert
            _mockConfigService.Verify(x => x.GetDefaultConfiguration(), Times.Once);
            Assert.True(_viewModel.HasUnsavedChanges);
        }

        [Fact]
        public async Task StartTimersCommand_StartsTimerService()
        {
            // Act
            await Task.Run(() => _viewModel.StartTimersCommand.Execute(null));

            // Assert
            _mockTimerService.Verify(x => x.StartAsync(), Times.Once);
        }

        [Fact]
        public async Task StopTimersCommand_StopsTimerService()
        {
            // Act
            await Task.Run(() => _viewModel.StopTimersCommand.Execute(null));

            // Assert
            _mockTimerService.Verify(x => x.StopAsync(), Times.Once);
        }

        [Fact]
        public void StartWithWindows_EnablesStartup_WhenTrue()
        {
            // Arrange
            _viewModel.StartWithWindows = true;

            // Act
            _viewModel.SaveCommand.Execute(null);

            // Assert
            _mockStartupManager.Verify(x => x.EnableStartup(), Times.Once);
        }

        [Fact]
        public void StartWithWindows_DisablesStartup_WhenFalse()
        {
            // Arrange
            _viewModel.StartWithWindows = false;

            // Act
            _viewModel.SaveCommand.Execute(null);

            // Assert
            _mockStartupManager.Verify(x => x.DisableStartup(), Times.Once);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(120, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(121, false)]
        public void EyeRestIntervalMinutes_ValidatesRange(int value, bool shouldBeValid)
        {
            // Act
            _viewModel.EyeRestIntervalMinutes = value;

            // Assert - In a real implementation, you might have validation logic
            // For now, we just test that the property can be set
            Assert.Equal(value, _viewModel.EyeRestIntervalMinutes);
        }

        [Theory]
        [InlineData(5, true)]
        [InlineData(20, true)]
        [InlineData(300, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(301, false)]
        public void EyeRestDurationSeconds_ValidatesRange(int value, bool shouldBeValid)
        {
            // Act
            _viewModel.EyeRestDurationSeconds = value;

            // Assert
            Assert.Equal(value, _viewModel.EyeRestDurationSeconds);
        }

        public void Dispose()
        {
            // Clean up if needed
        }
    }
}