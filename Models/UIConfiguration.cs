using System.ComponentModel;

namespace EyeRest.Models
{
    /// <summary>
    /// UI configuration that auto-saves immediately on change
    /// </summary>
    public class UIConfiguration : INotifyPropertyChanged
    {
        public AudioSettings Audio { get; set; } = new();
        public ApplicationSettings Application { get; set; } = new();
        public AnalyticsSettings Analytics { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}