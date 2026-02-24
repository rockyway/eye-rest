using System;
using System.Threading.Tasks;

namespace EyeRest.Services
{
    public interface IPopupWindow
    {
        void Show();
        void Close();
        bool IsVisible { get; }
        event EventHandler? Closed;
        object? DataContext { get; set; }
    }
}
