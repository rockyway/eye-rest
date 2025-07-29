using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class AudioService : IAudioService
    {
        private readonly ILogger<AudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private AppConfiguration _configuration;

        public bool IsAudioEnabled => _configuration.Audio.Enabled;

        public AudioService(ILogger<AudioService> logger, IConfigurationService configurationService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _configuration = new AppConfiguration(); // Will be loaded
            
            // Subscribe to configuration changes
            _configurationService.ConfigurationChanged += OnConfigurationChanged;
            
            // Load initial configuration
            LoadConfigurationAsync();
        }

        private async void LoadConfigurationAsync()
        {
            try
            {
                _configuration = await _configurationService.LoadConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration for audio service");
            }
        }

        public async Task PlayEyeRestStartSound()
        {
            if (!IsAudioEnabled || !_configuration.EyeRest.StartSoundEnabled)
            {
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Asterisk);
                });
                
                _logger.LogDebug("Eye rest start sound played");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing eye rest start sound");
            }
        }

        public async Task PlayEyeRestEndSound()
        {
            if (!IsAudioEnabled || !_configuration.EyeRest.EndSoundEnabled)
            {
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Exclamation);
                });
                
                _logger.LogDebug("Eye rest end sound played");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing eye rest end sound");
            }
        }

        public async Task PlayBreakWarningSound()
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Question);
                });
                
                _logger.LogDebug("Break warning sound played");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing break warning sound");
            }
        }

        private void PlaySystemSound(SystemSound sound)
        {
            try
            {
                // Check if custom sound path is configured and exists
                if (!string.IsNullOrEmpty(_configuration.Audio.CustomSoundPath) && 
                    File.Exists(_configuration.Audio.CustomSoundPath))
                {
                    PlayCustomSound(_configuration.Audio.CustomSoundPath);
                }
                else
                {
                    // Play system sound
                    sound.Play();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to play sound, falling back to system beep");
                
                // Fallback to system beep
                try
                {
                    Console.Beep();
                }
                catch
                {
                    // If even system beep fails, just log and continue
                    _logger.LogWarning("All audio playback methods failed");
                }
            }
        }

        private void PlayCustomSound(string soundPath)
        {
            try
            {
                using var player = new SoundPlayer(soundPath);
                
                // Set volume based on configuration (Windows system volume will be respected)
                // Note: SoundPlayer doesn't support volume control directly,
                // but we can implement this with more advanced audio libraries if needed
                
                player.PlaySync();
                
                _logger.LogDebug($"Custom sound played: {soundPath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to play custom sound: {soundPath}");
                throw; // Re-throw to trigger fallback in PlaySystemSound
            }
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            _configuration = e.NewConfiguration;
            _logger.LogDebug("Audio service configuration updated");
        }

        public void Dispose()
        {
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;
        }
    }
}