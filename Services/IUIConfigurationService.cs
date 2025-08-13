using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public class UIConfigurationChangedEventArgs : EventArgs
    {
        public UIConfiguration? NewConfiguration { get; set; }
        public UIConfiguration? OldConfiguration { get; set; }
    }

    public interface IUIConfigurationService
    {
        event EventHandler<UIConfigurationChangedEventArgs>? ConfigurationChanged;
        
        Task<UIConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(UIConfiguration config);
        Task<UIConfiguration> GetDefaultConfiguration();
        
        // Auto-save specific settings immediately
        Task SaveDarkModeAsync(bool isDarkMode);
        Task SaveVolumeAsync(int volume);
        Task SaveAutoOpenDashboardAsync(bool autoOpen);
    }
}