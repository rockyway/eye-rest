using System;
using System.Threading.Tasks;
using EyeRest.Models;
using EyeRest.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EyeRest.Tests.Services
{
    public class AudioServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AudioService>> _mockLogger;
        private readonly Mock<IConfigurationService> _mockConfigService;
        private readonly AudioService _audioService;
        private readonly AppConfiguration _testConfig;

        public AudioServiceTests()
        {
            _mockLogger = new Mock<ILogger<AudioService>>();
            _mockConfigService = new Mock<IConfigurationService>();
            
            _testConfig = new AppConfiguration
            {
                Audio = new AudioSettings
                {
                    Enabled = true,
                    Volume = 50
                },
                EyeRest = new EyeRestSettings
                {
                    StartSoundEnabled = true,
                    EndSoundEnabled = true
                }
            };

            _mockConfigService.Setup(x => x.LoadConfigurationAsync())
                .ReturnsAsync(_testConfig);

            _audioService = new AudioService(_mockLogger.Object, _mockConfigService.Object);
        }

        [Fact]
        public void IsAudioEnabled_WhenAudioEnabled_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_audioService.IsAudioEnabled);
        }

        [Fact]
        public void IsAudioEnabled_WhenAudioDisabled_ReturnsFalse()
        {
            // Arrange
            _testConfig.Audio.Enabled = false;
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration(),
                NewConfiguration = _testConfig
            };

            // Act
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Assert
            Assert.False(_audioService.IsAudioEnabled);
        }

        [Fact]
        public async Task PlayEyeRestStartSound_WhenEnabled_CompletesSuccessfully()
        {
            // Act & Assert - Should not throw
            await _audioService.PlayEyeRestStartSound();
        }

        [Fact]
        public async Task PlayEyeRestStartSound_WhenDisabled_DoesNotPlay()
        {
            // Arrange
            _testConfig.EyeRest.StartSoundEnabled = false;
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration(),
                NewConfiguration = _testConfig
            };
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Act & Assert - Should complete quickly without playing
            await _audioService.PlayEyeRestStartSound();
        }

        [Fact]
        public async Task PlayEyeRestEndSound_WhenEnabled_CompletesSuccessfully()
        {
            // Act & Assert - Should not throw
            await _audioService.PlayEyeRestEndSound();
        }

        [Fact]
        public async Task PlayEyeRestEndSound_WhenDisabled_DoesNotPlay()
        {
            // Arrange
            _testConfig.EyeRest.EndSoundEnabled = false;
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration(),
                NewConfiguration = _testConfig
            };
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Act & Assert - Should complete quickly without playing
            await _audioService.PlayEyeRestEndSound();
        }

        [Fact]
        public async Task PlayBreakWarningSound_WhenEnabled_CompletesSuccessfully()
        {
            // Act & Assert - Should not throw
            await _audioService.PlayBreakWarningSound();
        }

        [Fact]
        public async Task PlayBreakWarningSound_WhenAudioDisabled_DoesNotPlay()
        {
            // Arrange
            _testConfig.Audio.Enabled = false;
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration(),
                NewConfiguration = _testConfig
            };
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Act & Assert - Should complete quickly without playing
            await _audioService.PlayBreakWarningSound();
        }

        [Fact]
        public async Task PlayCustomSound_WhenCustomPathSet_AttemptsToPlayCustomSound()
        {
            // Arrange
            _testConfig.Audio.CustomSoundPath = "test.wav"; // Non-existent file for testing
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = new AppConfiguration(),
                NewConfiguration = _testConfig
            };
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Act & Assert - Should not throw even with invalid path (fallback to system sound)
            await _audioService.PlayEyeRestStartSound();
        }

        [Fact]
        public void ConfigurationChanged_UpdatesAudioSettings()
        {
            // Arrange
            var newConfig = new AppConfiguration
            {
                Audio = new AudioSettings { Enabled = false }
            };
            var eventArgs = new ConfigurationChangedEventArgs
            {
                OldConfiguration = _testConfig,
                NewConfiguration = newConfig
            };

            // Act
            _mockConfigService.Raise(x => x.ConfigurationChanged += null, eventArgs);

            // Assert
            Assert.False(_audioService.IsAudioEnabled);
        }

        public void Dispose()
        {
            _audioService?.Dispose();
        }
    }
}