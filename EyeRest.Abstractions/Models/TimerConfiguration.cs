using System.ComponentModel;

namespace EyeRest.Models
{
    /// <summary>
    /// Timer configuration that requires manual save to prevent accidental changes
    /// </summary>
    public class TimerConfiguration : INotifyPropertyChanged
    {
        public EyeRestSettings EyeRest { get; set; } = new();
        public BreakSettings Break { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}