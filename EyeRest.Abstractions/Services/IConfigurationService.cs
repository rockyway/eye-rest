using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public interface IConfigurationService
    {
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null);
        Task<AppConfiguration> GetDefaultConfiguration();
        /// <summary>
        /// Atomically loads the current configuration, applies a modifier, and saves.
        /// Prevents race conditions when multiple callers do read-modify-write cycles.
        /// </summary>
        Task UpdateConfigurationAsync(Action<AppConfiguration> modifier, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null);
        /// <summary>
        /// Saves a complete configuration object while holding the config lock.
        /// Use this instead of SaveConfigurationAsync when replacing the entire config
        /// (e.g., RestoreDefaults) to prevent race conditions with concurrent UpdateConfigurationAsync calls.
        /// </summary>
        Task ReplaceConfigurationAsync(AppConfiguration config, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null);
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public required AppConfiguration NewConfiguration { get; set; }
        public required AppConfiguration OldConfiguration { get; set; }
    }
}