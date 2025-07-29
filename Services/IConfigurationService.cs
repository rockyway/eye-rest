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
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public required AppConfiguration NewConfiguration { get; set; }
        public required AppConfiguration OldConfiguration { get; set; }
    }
}