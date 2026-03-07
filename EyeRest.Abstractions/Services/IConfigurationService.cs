using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public interface IConfigurationService
    {
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        Task<AppConfiguration> GetDefaultConfiguration();
        /// <summary>
        /// Atomically loads the current configuration, applies a modifier, and saves.
        /// Prevents race conditions when multiple callers do read-modify-write cycles.
        /// </summary>
        Task UpdateConfigurationAsync(Action<AppConfiguration> modifier);
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public required AppConfiguration NewConfiguration { get; set; }
        public required AppConfiguration OldConfiguration { get; set; }
    }
}