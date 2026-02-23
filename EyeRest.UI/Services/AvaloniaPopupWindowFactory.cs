using EyeRest.UI.Views;

namespace EyeRest.Services
{
    public class AvaloniaPopupWindowFactory : IPopupWindowFactory
    {
        public IPopupWindow CreateEyeRestWarningPopup() => new PopupWindow();
        public IPopupWindow CreateEyeRestPopup() => new PopupWindow();
        public IPopupWindow CreateBreakWarningPopup() => new PopupWindow();
        public IPopupWindow CreateBreakPopup() => new PopupWindow();
    }
}
