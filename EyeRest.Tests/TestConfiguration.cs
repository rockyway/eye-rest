using System;
using EyeRest.Models;

namespace EyeRest.Tests
{
    /// <summary>
    /// Test Configuration Provider
    /// Provides shortened timer intervals for efficient testing
    /// while maintaining the same functionality as production
    /// </summary>
    public static class TestConfiguration
    {
        /// <summary>
        /// Creates a test configuration with shortened intervals for efficient testing
        /// Eye Rest: 2 minutes interval, 10 seconds duration, 15 seconds warning
        /// Break: 5 minutes interval, 30 seconds duration, 15 seconds warning
        /// </summary>
        public static AppConfiguration CreateFastTestConfiguration()
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 2,      // 2 minutes for testing (vs 20 production)
                    DurationSeconds = 10,     // 10 seconds for testing (vs 20 production)
                    WarningEnabled = true,
                    WarningSeconds = 15,      // 15 seconds warning (vs 30 production)
                    StartSoundEnabled = true,
                    EndSoundEnabled = true
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 5,      // 5 minutes for testing (vs 55 production)
                    DurationMinutes = 1,      // 30 seconds for testing (vs 5 minutes production)
                    WarningEnabled = true,
                    WarningSeconds = 15,      // 15 seconds warning (vs 30 production)
                },
                Audio = new AudioSettings
                {
                    Enabled = false,          // Disable audio for testing
                    Volume = 50,
                    CustomSoundPath = null
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false
                }
            };
        }

        /// <summary>
        /// Creates ultra-fast test configuration for unit testing
        /// Eye Rest: 30 seconds interval, 5 seconds duration, 5 seconds warning
        /// Break: 60 seconds interval, 10 seconds duration, 5 seconds warning
        /// </summary>
        public static AppConfiguration CreateUltraFastTestConfiguration()
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 1,      // 30 seconds for ultra-fast testing
                    DurationSeconds = 5,      // 5 seconds duration
                    WarningEnabled = true,
                    WarningSeconds = 5,       // 5 seconds warning
                    StartSoundEnabled = false,
                    EndSoundEnabled = false
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 1,      // 60 seconds for ultra-fast testing
                    DurationMinutes = 1,      // 10 seconds duration
                    WarningEnabled = true,
                    WarningSeconds = 5,       // 5 seconds warning
                },
                Audio = new AudioSettings
                {
                    Enabled = false,          // Disable audio for testing
                    Volume = 0,
                    CustomSoundPath = null
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false
                }
            };
        }

        /// <summary>
        /// Creates the default production configuration for validation testing
        /// This matches the expected default values: 20min/20sec for eye rest
        /// </summary>
        public static AppConfiguration CreateDefaultProductionConfiguration()
        {
            return new AppConfiguration
            {
                EyeRest = new EyeRestSettings
                {
                    IntervalMinutes = 20,     // Default production value
                    DurationSeconds = 20,     // Default production value
                    WarningEnabled = true,
                    WarningSeconds = 30,      // Default production value
                    StartSoundEnabled = true,
                    EndSoundEnabled = true
                },
                Break = new BreakSettings
                {
                    IntervalMinutes = 55,     // Default production value
                    DurationMinutes = 5,      // Default production value
                    WarningEnabled = true,
                    WarningSeconds = 30,      // Default production value
                },
                Audio = new AudioSettings
                {
                    Enabled = true,
                    Volume = 50,
                    CustomSoundPath = null
                },
                Application = new ApplicationSettings
                {
                    StartWithWindows = false,
                    MinimizeToTray = true,
                    ShowInTaskbar = false
                }
            };
        }

        /// <summary>
        /// Gets configuration description for logging/reporting
        /// </summary>
        public static string GetConfigurationDescription(AppConfiguration config)
        {
            return $"EyeRest: {config.EyeRest.IntervalMinutes}min interval, " +
                   $"{config.EyeRest.DurationSeconds}sec duration, " +
                   $"{config.EyeRest.WarningSeconds}sec warning | " +
                   $"Break: {config.Break.IntervalMinutes}min interval, " +
                   $"{config.Break.DurationMinutes}min duration, " +
                   $"{config.Break.WarningSeconds}sec warning";
        }

        /// <summary>
        /// Validates that configuration meets the expected default requirements
        /// </summary>
        public static bool ValidateDefaultConfiguration(AppConfiguration config)
        {
            return config.EyeRest.IntervalMinutes == 20 &&
                   config.EyeRest.DurationSeconds == 20 &&
                   config.EyeRest.WarningSeconds == 30 &&
                   config.EyeRest.WarningEnabled &&
                   config.Break.IntervalMinutes == 55 &&
                   config.Break.DurationMinutes == 5 &&
                   config.Break.WarningSeconds == 30 &&
                   config.Break.WarningEnabled;
        }

        /// <summary>
        /// Gets expected countdown format for validation
        /// </summary>
        public static string GetExpectedCountdownFormat()
        {
            return "Next eye rest: {eyeRestTime} | Next break: {breakTime}";
        }

        /// <summary>
        /// Calculates expected test completion time based on configuration
        /// </summary>
        public static TimeSpan CalculateTestDuration(AppConfiguration config)
        {
            var eyeRestCycle = TimeSpan.FromMinutes(config.EyeRest.IntervalMinutes) + 
                              TimeSpan.FromSeconds(config.EyeRest.WarningSeconds) + 
                              TimeSpan.FromSeconds(config.EyeRest.DurationSeconds);
            
            var breakCycle = TimeSpan.FromMinutes(config.Break.IntervalMinutes) + 
                            TimeSpan.FromSeconds(config.Break.WarningSeconds) + 
                            TimeSpan.FromMinutes(config.Break.DurationMinutes);

            return TimeSpan.FromTicks(Math.Min(eyeRestCycle.Ticks, breakCycle.Ticks));
        }
    }
}