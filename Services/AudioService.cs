using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;
using System.Windows.Media; // For MediaPlayer

namespace EyeRest.Services
{
    public class AudioService : IAudioService
    {
        private readonly ILogger<AudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private AppConfiguration _configuration;
        private readonly object _soundLock = new object(); // Prevent concurrent sound playback
        private bool _isPlayingSound = false; // Track if sound is currently playing

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
                
                // ENHANCED: Log current audio configuration for debugging
                _logger.LogInformation($"🔊 Audio configuration loaded - Enabled: {_configuration.Audio.Enabled}, Volume: {_configuration.Audio.Volume}%, Custom Path: {(_configuration.Audio.CustomSoundPath ?? "None")}");
                
                // Validate custom sound file if configured
                if (!string.IsNullOrEmpty(_configuration.Audio.CustomSoundPath))
                {
                    if (File.Exists(_configuration.Audio.CustomSoundPath))
                    {
                        _logger.LogInformation($"🔊 ✅ Custom sound file validated: {_configuration.Audio.CustomSoundPath}");
                    }
                    else
                    {
                        _logger.LogWarning($"🔊 ⚠️ Custom sound file not found: {_configuration.Audio.CustomSoundPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 Failed to load configuration for audio service");
            }
        }

        public async Task PlayEyeRestStartSound()
        {
            if (!IsAudioEnabled || !_configuration.EyeRest.StartSoundEnabled)
            {
                _logger.LogDebug("🔊 Eye rest start sound skipped - audio disabled or start sound disabled");
                return;
            }

            try
            {
                _logger.LogInformation("🔊 Playing eye rest start sound...");
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Asterisk);
                });
                
                _logger.LogInformation("🔊 ✅ Eye rest start sound completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error playing eye rest start sound");
            }
        }

        public async Task PlayEyeRestEndSound()
        {
            if (!IsAudioEnabled || !_configuration.EyeRest.EndSoundEnabled)
            {
                _logger.LogDebug("🔊 Eye rest end sound skipped - audio disabled or end sound disabled");
                return;
            }

            lock (_soundLock)
            {
                if (_isPlayingSound)
                {
                    _logger.LogDebug("🔊 Eye rest end sound already playing - skipping duplicate playback");
                    return;
                }
                _isPlayingSound = true;
            }

            try
            {
                _logger.LogInformation("🔊 Playing eye rest end sound...");
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Asterisk); // Lighter, softer sound or custom sound
                });
                
                _logger.LogInformation("🔊 ✅ Eye rest end sound completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error playing eye rest end sound");
            }
            finally
            {
                lock (_soundLock)
                {
                    _isPlayingSound = false;
                }
            }
        }

        public async Task PlayBreakWarningSound()
        {
            if (!IsAudioEnabled)
            {
                _logger.LogDebug("🔊 Break warning sound skipped - audio disabled");
                return;
            }

            try
            {
                _logger.LogInformation("🔊 Playing break warning sound...");
                await Task.Run(() =>
                {
                    PlaySystemSound(SystemSounds.Question);
                });
                
                _logger.LogInformation("🔊 ✅ Break warning sound completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error playing break warning sound");
            }
        }

        private void PlaySystemSound(SystemSound sound)
        {
            try
            {
                // ENHANCED: Always prefer custom sound when configured
                if (!string.IsNullOrEmpty(_configuration.Audio.CustomSoundPath))
                {
                    if (File.Exists(_configuration.Audio.CustomSoundPath))
                    {
                        _logger.LogInformation($"🔊 Playing custom sound: {_configuration.Audio.CustomSoundPath}");
                        PlayCustomSound(_configuration.Audio.CustomSoundPath);
                        return; // Successfully played custom sound
                    }
                    else
                    {
                        _logger.LogWarning($"🔊 Custom sound file not found: {_configuration.Audio.CustomSoundPath}, falling back to system sound");
                    }
                }
                
                // Play system sound as fallback
                _logger.LogDebug($"🔊 Playing system sound: {sound.GetType().Name}");
                sound.Play();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🔊 Failed to play sound, falling back to system beep");
                
                // Fallback to system beep
                try
                {
                    Console.Beep();
                }
                catch
                {
                    // If even system beep fails, just log and continue
                    _logger.LogWarning("🔊 All audio playback methods failed");
                }
            }
        }

        private void PlayCustomSound(string soundPath)
        {
            try
            {
                // ENHANCED: Better custom sound playback with MediaPlayer for volume control
                if (Path.GetExtension(soundPath).ToLowerInvariant() == ".wav")
                {
                    // Use SoundPlayer for WAV files (more reliable)
                    using var player = new SoundPlayer(soundPath);
                    player.LoadAsync(); // Load the sound first
                    player.PlaySync(); // Play synchronously
                    
                    _logger.LogInformation($"🔊 ✅ Custom WAV sound played successfully: {Path.GetFileName(soundPath)}");
                }
                else
                {
                    // Use MediaPlayer for other formats (MP3, WMA, etc.) with volume control
                    var mediaPlayer = new System.Windows.Media.MediaPlayer();
                    mediaPlayer.Open(new Uri(soundPath));
                    
                    // Apply volume setting from configuration (0-100 to 0.0-1.0)
                    mediaPlayer.Volume = _configuration.Audio.Volume / 100.0;
                    
                    mediaPlayer.Play();
                    
                    // Wait for playback to complete (approximate)
                    System.Threading.Thread.Sleep(1000); // Basic wait - could be improved with MediaEnded event
                    
                    mediaPlayer.Close();
                    
                    _logger.LogInformation($"🔊 ✅ Custom media sound played successfully: {Path.GetFileName(soundPath)} at {_configuration.Audio.Volume}% volume");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"🔊 ❌ Failed to play custom sound: {soundPath}");
                throw; // Re-throw to trigger fallback in PlaySystemSound
            }
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            _configuration = e.NewConfiguration;
            
            // ENHANCED: Log detailed configuration changes for debugging
            _logger.LogInformation($"🔊 Audio configuration updated - Enabled: {_configuration.Audio.Enabled}, Volume: {_configuration.Audio.Volume}%, Custom Path: {(_configuration.Audio.CustomSoundPath ?? "None")}");
            
            // Validate new custom sound file if configured
            if (!string.IsNullOrEmpty(_configuration.Audio.CustomSoundPath))
            {
                if (File.Exists(_configuration.Audio.CustomSoundPath))
                {
                    _logger.LogInformation($"🔊 ✅ New custom sound file validated: {_configuration.Audio.CustomSoundPath}");
                }
                else
                {
                    _logger.LogWarning($"🔊 ⚠️ New custom sound file not found: {_configuration.Audio.CustomSoundPath}");
                }
            }
        }

        /// <summary>
        /// Test play the currently configured custom sound (for UI testing)
        /// </summary>
        public async Task PlayCustomSoundTestAsync()
        {
            if (!IsAudioEnabled)
            {
                _logger.LogWarning("🔊 Cannot test custom sound - audio is disabled");
                throw new InvalidOperationException("Audio is disabled. Please enable audio in settings first.");
            }

            try
            {
                _logger.LogInformation("🔊 Testing custom sound file...");
                
                if (string.IsNullOrEmpty(_configuration.Audio.CustomSoundPath))
                {
                    _logger.LogWarning("🔊 No custom sound path configured");
                    throw new InvalidOperationException("No custom sound file selected. Please select a custom sound file first.");
                }
                
                if (!File.Exists(_configuration.Audio.CustomSoundPath))
                {
                    _logger.LogError($"🔊 Custom sound file not found: {_configuration.Audio.CustomSoundPath}");
                    throw new FileNotFoundException($"The selected custom sound file was not found: {_configuration.Audio.CustomSoundPath}");
                }
                
                await Task.Run(() =>
                {
                    PlayCustomSound(_configuration.Audio.CustomSoundPath);
                });
                
                _logger.LogInformation($"🔊 ✅ Custom sound test completed successfully: {Path.GetFileName(_configuration.Audio.CustomSoundPath)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Custom sound test failed");
                throw;
            }
        }

        public void Dispose()
        {
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;
        }
    }
}