using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EyeRest.Models;
using Microsoft.Extensions.Logging;
using System.Windows.Media; // For MediaPlayer
using Microsoft.Win32; // For Registry access

namespace EyeRest.Services
{
    public class AudioService : AudioServiceBase
    {
        // Windows API for playing system sounds
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MessageBeep(uint type);

        // Windows API for checking audio devices
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutGetNumDevs();

        private readonly ILogger<AudioService> _logger;
        private readonly IConfigurationService _configurationService;
        private AppConfiguration _configuration;
        private readonly object _soundLock = new object(); // Prevent concurrent sound playback
        private bool _isPlayingSound = false; // Track if sound is currently playing
        private readonly Queue<string> _soundQueue = new Queue<string>(); // Queue for multiple sound requests

        // 🔍 ULTRATHINK: Cycle tracking for diagnostics
        private static int _startSoundCycleCount = 0;
        private static int _endSoundCycleCount = 0;

        // Audio diagnostics and alternative sound paths
        private readonly Dictionary<string, string> _fallbackSounds = new Dictionary<string, string>
        {
            { "Question", @"C:\WINDOWS\media\Windows Default.wav" },
            { "Hand", @"C:\WINDOWS\media\Windows Ding.wav" },
            { "Asterisk", @"C:\WINDOWS\media\Windows Exclamation.wav" },
            { "Exclamation", @"C:\WINDOWS\media\Windows Error.wav" },
            { "Beep", @"C:\WINDOWS\media\chord.wav" }
        };

        public override bool IsAudioEnabled => _configuration.Audio.Enabled;

        public AudioService(ILogger<AudioService> logger, IConfigurationService configurationService, IUrlOpener urlOpener)
            : base(urlOpener)
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
                _logger.LogInformation($"🔊 Audio configuration loaded - Enabled: {_configuration.Audio.Enabled}, Volume: {_configuration.Audio.Volume}%");

                // Audio diagnostics disabled — was playing audible test sounds on every startup

                // BL-002 schema v2: legacy global Audio.CustomSoundPath removed; per-channel
                // CustomFilePath fields live on EyeRest/Break StartAudio/EndAudio. Validation
                // happens at playback time in M2's PlayChannelAsync (fallback to Default on miss).
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 Failed to load configuration for audio service");
            }
        }

        private async Task RunAudioDiagnosticsAsync()
        {
            try
            {
                _logger.LogInformation("🔊 🔍 Running comprehensive audio diagnostics...");

                // Check audio devices
                uint deviceCount = waveOutGetNumDevs();
                _logger.LogInformation($"🔊 📱 Audio output devices available: {deviceCount}");

                // Check registry for corrupted sound settings
                CheckSoundRegistrySettings();

                // Test fallback sound files existence
                foreach (var sound in _fallbackSounds)
                {
                    bool exists = File.Exists(sound.Value);
                    _logger.LogInformation($"🔊 📁 Fallback sound '{sound.Key}' file exists: {exists} ({sound.Value})");
                }

                // Test different audio methods
                await TestAudioMethodsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error during audio diagnostics");
            }
        }

        private void CheckSoundRegistrySettings()
        {
            try
            {
                // Check for the common registry corruption issue
                using (var key = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\.Default\.Current"))
                {
                    if (key != null)
                    {
                        var defaultValue = key.GetValue("") as string;
                        _logger.LogInformation($"🔊 🔑 Registry default sound: {defaultValue ?? "(empty)"}");

                        if (!string.IsNullOrEmpty(defaultValue) && !File.Exists(defaultValue))
                        {
                            _logger.LogWarning($"🔊 ⚠️ Registry points to non-existent sound file: {defaultValue}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("🔊 ⚠️ Could not access sound registry settings");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error checking sound registry settings");
            }
        }

        private async Task TestAudioMethodsAsync()
        {
            try
            {
                _logger.LogInformation("🔊 🧪 Testing audio playback methods...");

                // Test 1: SystemSound.Play()
                try
                {
                    SystemSounds.Beep.Play();
                    _logger.LogInformation("🔊 ✅ SystemSounds.Beep.Play() - No exceptions thrown");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔊 ❌ SystemSounds.Beep.Play() failed");
                }

                await Task.Delay(100);

                // Test 2: MessageBeep API
                try
                {
                    bool result = MessageBeep(0x00000000);
                    _logger.LogInformation($"🔊 MessageBeep API result: {result}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔊 ❌ MessageBeep API failed");
                }

                await Task.Delay(100);

                // Test 3: SoundPlayer with WAV file
                try
                {
                    if (File.Exists(_fallbackSounds["Beep"]))
                    {
                        using (var player = new SoundPlayer(_fallbackSounds["Beep"]))
                        {
                            player.Play();
                            _logger.LogInformation("🔊 ✅ SoundPlayer WAV playback - No exceptions thrown");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "🔊 ❌ SoundPlayer WAV playback failed");
                }

                _logger.LogInformation("🔊 🔍 Audio diagnostics completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error during audio method testing");
            }
        }

        public override async Task PlayEyeRestStartSound()
        {
            // 🔍 ULTRATHINK DIAGNOSTICS: Increment and log cycle
            _startSoundCycleCount++;
            _logger.LogInformation($"🔊 🔍 PlayEyeRestStartSound called - CYCLE #{_startSoundCycleCount}");
            _logger.LogInformation($"🔊 🔍 IsAudioEnabled: {IsAudioEnabled}");
            _logger.LogInformation($"🔊 🔍 _configuration.Audio.Enabled: {_configuration.Audio.Enabled}");
            var startEnabled = _configuration.EyeRest.StartAudio.Source != AudioChannelSource.Off;
            _logger.LogInformation($"🔊 🔍 EyeRest.StartAudio.Source: {_configuration.EyeRest.StartAudio.Source}");
            _logger.LogInformation($"🔊 🔍 Audio Volume: {_configuration.Audio.Volume}");

            if (!IsAudioEnabled || !startEnabled)
            {
                _logger.LogWarning($"🔊 ❌ CYCLE #{_startSoundCycleCount}: Eye rest start sound SKIPPED - IsAudioEnabled: {IsAudioEnabled}, StartAudio: {_configuration.EyeRest.StartAudio.Source}");
                return;
            }

            // 🔧 ULTRATHINK FIX: Reload configuration to ensure fresh state
            var currentConfig = await _configurationService.LoadConfigurationAsync();
            var currentStartEnabled = currentConfig.EyeRest.StartAudio.Source != AudioChannelSource.Off;
            _logger.LogInformation($"🔊 🔍 CYCLE #{_startSoundCycleCount}: Config reloaded - StartAudio: {currentConfig.EyeRest.StartAudio.Source}");

            if (!currentConfig.Audio.Enabled || !currentStartEnabled)
            {
                _logger.LogWarning($"🔊 ❌ CYCLE #{_startSoundCycleCount}: Config check failed after reload - Audio: {currentConfig.Audio.Enabled}, StartAudio: {currentConfig.EyeRest.StartAudio.Source}");
                return;
            }

            try
            {
                _logger.LogInformation($"🔊 ▶️ CYCLE #{_startSoundCycleCount}: Playing eye rest start sound with comprehensive fallback...");

                // 🔧 ULTRATHINK FIX: Try multiple sound methods for reliability
                bool soundPlayed = false;

                // Method 1: Try SystemSounds.Asterisk (more reliable than Question)
                if (!soundPlayed)
                {
                    try
                    {
                        _logger.LogInformation($"🔊 🔍 CYCLE #{_startSoundCycleCount}: Attempting SystemSounds.Asterisk...");
                        await Task.Run(() => SystemSounds.Asterisk.Play());
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ CYCLE #{_startSoundCycleCount}: SystemSounds.Asterisk succeeded");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"🔊 ⚠️ CYCLE #{_startSoundCycleCount}: SystemSounds.Asterisk failed");
                    }
                }

                // Method 2: Try our comprehensive PlaySystemSound method
                if (!soundPlayed)
                {
                    try
                    {
                        _logger.LogInformation($"🔊 🔍 CYCLE #{_startSoundCycleCount}: Attempting PlaySystemSound(SystemSounds.Question)...");
                        await Task.Run(() => PlaySystemSound(SystemSounds.Question));
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ CYCLE #{_startSoundCycleCount}: PlaySystemSound(SystemSounds.Question) succeeded");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"🔊 ⚠️ CYCLE #{_startSoundCycleCount}: PlaySystemSound(SystemSounds.Question) failed");
                    }
                }

                // Method 3: Try MessageBeep API directly
                if (!soundPlayed)
                {
                    try
                    {
                        _logger.LogInformation($"🔊 🔍 CYCLE #{_startSoundCycleCount}: Attempting MessageBeep API...");
                        await Task.Run(() => MessageBeep(0x00000040)); // MB_ICONQUESTION
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ CYCLE #{_startSoundCycleCount}: MessageBeep API succeeded");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"🔊 ⚠️ CYCLE #{_startSoundCycleCount}: MessageBeep API failed");
                    }
                }

                // Method 4: Last resort - Console.Beep
                if (!soundPlayed)
                {
                    try
                    {
                        _logger.LogInformation($"🔊 🔍 CYCLE #{_startSoundCycleCount}: Attempting Console.Beep fallback...");
                        await Task.Run(() => Console.Beep(800, 200)); // Higher pitched beep
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ CYCLE #{_startSoundCycleCount}: Console.Beep fallback succeeded");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"🔊 ❌ CYCLE #{_startSoundCycleCount}: All audio methods failed including Console.Beep");
                    }
                }

                if (soundPlayed)
                {
                    _logger.LogInformation($"🔊 ✅ CYCLE #{_startSoundCycleCount}: Eye rest start sound completed successfully");
                }
                else
                {
                    _logger.LogError($"🔊 ❌ CYCLE #{_startSoundCycleCount}: ALL AUDIO METHODS FAILED - No sound played");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"🔊 ❌ CYCLE #{_startSoundCycleCount}: Unexpected error in comprehensive audio playback");
            }
        }

        public override async Task PlayEyeRestEndSound()
        {
            // 🔍 ULTRATHINK DIAGNOSTICS: Track end sound cycles for comparison
            _endSoundCycleCount++;
            _logger.LogInformation($"🔊 🔍 PlayEyeRestEndSound called - CYCLE #{_endSoundCycleCount}");
            var endEnabled = _configuration.EyeRest.EndAudio.Source != AudioChannelSource.Off;
            _logger.LogInformation($"🔊 🔍 IsAudioEnabled: {IsAudioEnabled}, EndAudio: {_configuration.EyeRest.EndAudio.Source}");

            if (!IsAudioEnabled || !endEnabled)
            {
                _logger.LogWarning($"🔊 ❌ CYCLE #{_endSoundCycleCount}: Eye rest end sound SKIPPED - IsAudioEnabled: {IsAudioEnabled}, EndAudio: {_configuration.EyeRest.EndAudio.Source}");
                return;
            }

            try
            {
                _logger.LogInformation($"🔊 ▶️ CYCLE #{_endSoundCycleCount}: Playing eye rest end sound (SystemSounds.Hand)...");
                await Task.Run(() =>
                {
                    // IMPROVEMENT: Use very light, gentle completion sound - Hand is softer than Asterisk/Question
                    PlaySystemSound(SystemSounds.Hand);
                });

                _logger.LogInformation($"🔊 ✅ CYCLE #{_endSoundCycleCount}: Eye rest end sound completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"🔊 ❌ CYCLE #{_endSoundCycleCount}: Error playing eye rest end sound");
            }
        }

        public override async Task PlayBreakWarningSound()
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

        public override async Task PlayBreakStartSound()
        {
            if (!IsAudioEnabled || _configuration.Break.StartAudio.Source == AudioChannelSource.Off)
            {
                _logger.LogDebug("🔊 Break start sound skipped - audio disabled or StartAudio.Source == Off");
                return;
            }

            try
            {
                _logger.LogInformation("🔊 Playing break start sound...");
                await Task.Run(() =>
                {
                    // Use attention-getting sound for break start - Asterisk is more noticeable
                    PlaySystemSound(SystemSounds.Asterisk);
                });
                
                _logger.LogInformation("🔊 ✅ Break start sound completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error playing break start sound");
            }
        }

        public override async Task PlayBreakEndSound()
        {
            if (!IsAudioEnabled || _configuration.Break.EndAudio.Source == AudioChannelSource.Off)
            {
                _logger.LogDebug("🔊 Break end sound skipped - audio disabled or EndAudio.Source == Off");
                return;
            }

            lock (_soundLock)
            {
                if (_isPlayingSound)
                {
                    _logger.LogDebug("🔊 Break end sound already playing - skipping duplicate playback");
                    return;
                }
                _isPlayingSound = true;
            }

            try
            {
                _logger.LogInformation("🔊 Playing break end sound...");
                await Task.Run(() =>
                {
                    // Use positive completion sound for break end - Hand is gentle and positive
                    PlaySystemSound(SystemSounds.Hand);
                });
                
                _logger.LogInformation("🔊 ✅ Break end sound completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error playing break end sound");
            }
            finally
            {
                lock (_soundLock)
                {
                    _isPlayingSound = false;
                }
            }
        }

        private void PlaySystemSound(SystemSound sound)
        {
            lock (_soundLock)
            {
                if (_isPlayingSound)
                {
                    _logger.LogDebug("🔊 Sound already playing - skipping duplicate playback to prevent ghost sounds");
                    return;
                }
                _isPlayingSound = true;
            }

            try
            {
                // BL-002 schema v2: legacy global Audio.CustomSoundPath was removed.
                // Per-channel custom sound playback is implemented via PlayChannelAsync
                // (M2). This legacy code path was the only place that played a custom
                // sound; it now always falls through to the system-sound fallback below.

                // Determine sound type for fallback selection
                string soundType = GetSoundTypeName(sound);
                _logger.LogInformation($"🔊 Attempting to play {soundType} sound using multiple methods...");

                bool soundPlayed = false;

                // METHOD 1: Try SystemSound.Play() on UI thread
                if (!soundPlayed)
                {
                    try
                    {
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => sound.Play());
                        }
                        else
                        {
                            sound.Play();
                        }
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ {soundType} sound played via SystemSound.Play()");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"🔊 ⚠️ SystemSound.Play() failed for {soundType}, trying next method");
                    }
                }

                // METHOD 2: Try Windows API MessageBeep
                if (!soundPlayed)
                {
                    try
                    {
                        uint beepType = GetMessageBeepType(sound);
                        bool result = MessageBeep(beepType);
                        soundPlayed = result;
                        _logger.LogInformation($"🔊 ✅ {soundType} sound played via MessageBeep API (result: {result})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"🔊 ⚠️ MessageBeep API failed for {soundType}, trying next method");
                    }
                }

                // METHOD 3: Try SoundPlayer with fallback WAV file
                if (!soundPlayed && _fallbackSounds.ContainsKey(soundType))
                {
                    try
                    {
                        string wavFile = _fallbackSounds[soundType];
                        if (File.Exists(wavFile))
                        {
                            using (var player = new SoundPlayer(wavFile))
                            {
                                player.Play();
                                soundPlayed = true;
                                _logger.LogInformation($"🔊 ✅ {soundType} sound played via SoundPlayer WAV: {Path.GetFileName(wavFile)}");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"🔊 ⚠️ Fallback WAV file not found: {wavFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"🔊 ⚠️ SoundPlayer WAV playback failed for {soundType}, trying next method");
                    }
                }

                // METHOD 4: Try any available Windows sound file
                if (!soundPlayed)
                {
                    try
                    {
                        string[] alternativeFiles = {
                            @"C:\WINDOWS\media\Windows Default.wav",
                            @"C:\WINDOWS\media\chord.wav",
                            @"C:\WINDOWS\media\ding.wav",
                            @"C:\WINDOWS\media\chimes.wav"
                        };

                        foreach (string file in alternativeFiles)
                        {
                            if (File.Exists(file))
                            {
                                using (var player = new SoundPlayer(file))
                                {
                                    player.Play();
                                    soundPlayed = true;
                                    _logger.LogInformation($"🔊 ✅ {soundType} sound played via alternative WAV: {Path.GetFileName(file)}");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"🔊 ⚠️ Alternative WAV playback failed for {soundType}, trying final method");
                    }
                }

                // METHOD 5: Final fallback to Console.Beep
                if (!soundPlayed)
                {
                    try
                    {
                        // Use different tones for different sound types
                        (int frequency, int duration) = GetConsoleBeepParams(soundType);
                        Console.Beep(frequency, duration);
                        soundPlayed = true;
                        _logger.LogInformation($"🔊 ✅ {soundType} sound played via Console.Beep ({frequency}Hz, {duration}ms)");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"🔊 ❌ All audio methods failed for {soundType} sound");
                    }
                }

                if (!soundPlayed)
                {
                    _logger.LogError($"🔊 ❌ CRITICAL: Unable to play {soundType} sound using any method!");
                }
            }
            finally
            {
                lock (_soundLock)
                {
                    _isPlayingSound = false;
                }
            }
        }

        private string GetSoundTypeName(SystemSound sound)
        {
            if (sound == SystemSounds.Question) return "Question";
            if (sound == SystemSounds.Hand) return "Hand";
            if (sound == SystemSounds.Asterisk) return "Asterisk";
            if (sound == SystemSounds.Exclamation) return "Exclamation";
            if (sound == SystemSounds.Beep) return "Beep";
            return "Unknown";
        }

        private uint GetMessageBeepType(SystemSound sound)
        {
            if (sound == SystemSounds.Asterisk) return 0x00000040;
            if (sound == SystemSounds.Exclamation) return 0x00000030;
            if (sound == SystemSounds.Hand) return 0x00000010;
            if (sound == SystemSounds.Question) return 0x00000020;
            if (sound == SystemSounds.Beep) return 0x00000000;
            return 0xFFFFFFFF; // Default beep
        }

        private (int frequency, int duration) GetConsoleBeepParams(string soundType)
        {
            return soundType switch
            {
                "Question" => (800, 150),    // Gentle question tone
                "Hand" => (400, 100),        // Soft completion tone
                "Asterisk" => (1000, 200),   // Attention tone
                "Exclamation" => (600, 250), // Warning tone
                "Beep" => (500, 100),        // Simple beep
                _ => (800, 200)              // Default pleasant tone
            };
        }

        // BL-002 M2: PlayDefaultAsync routes channel → existing platform sound helpers.
        // Each channel's "default" remains the platform-specific SystemSounds choice for
        // now; M3 will bundle WAVs and route them through PlayFileAsync via BundledSoundCache.
        protected override async Task PlayDefaultAsync(AudioChannel channel, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            switch (channel)
            {
                case AudioChannel.EyeRestStart:  await PlayEyeRestStartSound().ConfigureAwait(false); break;
                case AudioChannel.EyeRestEnd:    await PlayEyeRestEndSound().ConfigureAwait(false);   break;
                case AudioChannel.BreakStart:    await PlayBreakStartSound().ConfigureAwait(false);   break;
                case AudioChannel.BreakEnd:      await PlayBreakEndSound().ConfigureAwait(false);     break;
                case AudioChannel.BreakWarning:  await PlayBreakWarningSound().ConfigureAwait(false); break;
            }
        }

        // BL-002 M2: WAV file playback via System.Media.SoundPlayer. SoundPlayer is
        // synchronous, so Task.Run honors the async/cancellable contract. Disposal in
        // finally guards every code path — success, exception, and OperationCanceledException.
        protected override Task PlayFileAsync(string filePath, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                System.Media.SoundPlayer? player = null;
                try
                {
                    player = new System.Media.SoundPlayer(filePath);
                    player.Load();
                    ct.ThrowIfCancellationRequested();
                    player.PlaySync();
                }
                finally
                {
                    player?.Dispose();
                }
            }, ct);
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
                    // CRITICAL FIX: Properly manage MediaPlayer lifecycle to prevent ghost sounds
                    // Note: MediaPlayer doesn't implement IDisposable, so manual cleanup is required
                    var mediaPlayer = new System.Windows.Media.MediaPlayer();
                    try
                    {
                        mediaPlayer.Open(new Uri(soundPath));
                        
                        // Apply volume setting from configuration (0-100 to 0.0-1.0)
                        mediaPlayer.Volume = _configuration.Audio.Volume / 100.0;
                        
                        // Use proper async playback with completion tracking
                        var playbackCompleted = new TaskCompletionSource<bool>();
                        
                        EventHandler? mediaEndedHandler = null;
                        EventHandler<System.Windows.Media.ExceptionEventArgs>? mediaFailedHandler = null;
                        
                        mediaEndedHandler = (s, e) =>
                        {
                            _logger.LogInformation($"🔊 MediaPlayer playback ended for {Path.GetFileName(soundPath)}");
                            playbackCompleted.TrySetResult(true);
                        };
                        
                        mediaFailedHandler = (s, e) =>
                        {
                            _logger.LogWarning($"🔊 MediaPlayer playback failed for {Path.GetFileName(soundPath)}: {e.ErrorException?.Message}");
                            playbackCompleted.TrySetException(e.ErrorException ?? new Exception("Media playback failed"));
                        };
                        
                        mediaPlayer.MediaEnded += mediaEndedHandler;
                        mediaPlayer.MediaFailed += mediaFailedHandler;
                        
                        mediaPlayer.Play();
                        
                        // Wait for playback completion or timeout
                        var completionTask = playbackCompleted.Task;
                        if (completionTask.Wait(TimeSpan.FromSeconds(10))) // 10 second timeout
                        {
                            _logger.LogInformation($"🔊 ✅ Custom media sound played successfully: {Path.GetFileName(soundPath)} at {_configuration.Audio.Volume}% volume");
                        }
                        else
                        {
                            _logger.LogWarning($"🔊 ⚠️ MediaPlayer playback timeout for {Path.GetFileName(soundPath)}, but disposing properly");
                        }
                        
                        // CRITICAL: Always clean up event handlers to prevent memory leaks
                        if (mediaEndedHandler != null)
                            mediaPlayer.MediaEnded -= mediaEndedHandler;
                        if (mediaFailedHandler != null)
                            mediaPlayer.MediaFailed -= mediaFailedHandler;
                    }
                    finally
                    {
                        // Stop and close to ensure proper cleanup - prevents ghost sounds
                        mediaPlayer.Stop();
                        mediaPlayer.Close();
                        _logger.LogDebug($"🔊 🧹 MediaPlayer properly cleaned up for {Path.GetFileName(soundPath)}");
                    }
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
            _logger.LogInformation($"🔊 Audio configuration updated - Enabled: {_configuration.Audio.Enabled}, Volume: {_configuration.Audio.Volume}%");
            // BL-002 schema v2: global CustomSoundPath removed; custom paths now per-channel
            // on EyeRest/Break StartAudio/EndAudio. Per-channel validation happens in M4.
        }

        /// <summary>
        /// Test play the currently configured custom sound (for UI testing)
        /// </summary>
        public override async Task PlayCustomSoundTestAsync()
        {
            if (!IsAudioEnabled)
            {
                _logger.LogWarning("🔊 Cannot test custom sound - audio is disabled");
                throw new InvalidOperationException("Audio is disabled. Please enable audio in settings first.");
            }

            // BL-002 schema v2: global Audio.CustomSoundPath was removed; custom paths
            // are now per-channel. Pick the first channel with a CustomFilePath set
            // as a best-effort "test sound" for the legacy Test button. M4 replaces
            // this UI entirely with per-channel Test buttons.
            var candidate =
                _configuration.EyeRest.StartAudio.CustomFilePath
                ?? _configuration.EyeRest.EndAudio.CustomFilePath
                ?? _configuration.Break.StartAudio.CustomFilePath
                ?? _configuration.Break.EndAudio.CustomFilePath;

            if (string.IsNullOrEmpty(candidate))
            {
                _logger.LogWarning("🔊 No per-channel custom sound path configured");
                throw new InvalidOperationException(
                    "No custom sound file selected on any channel. Pick one in the Popup Audio settings first.");
            }

            if (!File.Exists(candidate))
            {
                _logger.LogError($"🔊 Custom sound file not found: {candidate}");
                throw new FileNotFoundException($"The selected custom sound file was not found: {candidate}");
            }

            try
            {
                _logger.LogInformation($"🔊 Testing custom sound file: {candidate}");
                await Task.Run(() => PlayCustomSound(candidate));
                _logger.LogInformation($"🔊 ✅ Custom sound test completed: {Path.GetFileName(candidate)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Custom sound test failed");
                throw;
            }
        }

        public override async Task TestEyeRestAudioAsync()
        {
            if (!IsAudioEnabled)
            {
                _logger.LogWarning("🔊 Cannot test eye rest audio - audio is disabled");
                throw new InvalidOperationException("Audio is disabled. Please enable audio in settings first.");
            }

            try
            {
                _logger.LogInformation("🔊 🧪 Testing eye rest audio sequence (same sounds used during actual eye rest)...");

                // Test the same sequence as actual eye rest popup
                _logger.LogInformation("🔊 Playing eye rest START sound...");
                await PlayEyeRestStartSound();

                // Wait a moment between sounds
                await Task.Delay(1000);

                _logger.LogInformation("🔊 Playing eye rest END sound...");
                await PlayEyeRestEndSound();

                _logger.LogInformation("🔊 ✅ Eye rest audio test sequence completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔊 ❌ Error during eye rest audio test");
                throw;
            }
        }

        public void Dispose()
        {
            _configurationService.ConfigurationChanged -= OnConfigurationChanged;
        }
    }
}