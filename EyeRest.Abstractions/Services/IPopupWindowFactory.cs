namespace EyeRest.Services
{
    public interface IPopupWindowFactory
    {
        IPopupWindow CreateEyeRestWarningPopup();
        IPopupWindow CreateEyeRestPopup();
        IPopupWindow CreateBreakWarningPopup();
        IPopupWindow CreateBreakPopup();
    }
}
