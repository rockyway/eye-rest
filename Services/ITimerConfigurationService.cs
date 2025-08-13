using System;
using System.Threading.Tasks;
using EyeRest.Models;

namespace EyeRest.Services
{
    public class TimerConfigurationChangedEventArgs : EventArgs
    {
        public TimerConfiguration? NewConfiguration { get; set; }
        public TimerConfiguration? OldConfiguration { get; set; }
    }

    public interface ITimerConfigurationService
    {
        event EventHandler<TimerConfigurationChangedEventArgs>? ConfigurationChanged;
        
        Task<TimerConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(TimerConfiguration config);
        Task<TimerConfiguration> GetDefaultConfiguration();
    }
}