using EyeRest.UI.Views;

namespace EyeRest.Services
{
    public class AvaloniaPopupWindowFactory : IPopupWindowFactory
    {
        public IPopupWindow CreateEyeRestWarningPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new EyeRestWarningPopup();
            popup.SetPopupContent(content, 300, 400);
            content.WarningCompleted += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateEyeRestPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new EyeRestPopup();
            popup.SetPopupContent(content, 500, 500);
            content.Completed += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateBreakWarningPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new BreakWarningPopup();
            popup.SetPopupContent(content, 280, 400);
            content.Completed += (s, e) => popup.ReleaseToPool(lease);
            return popup;
        }

        public IPopupWindow CreateBreakPopup()
        {
            var popup = PopupWindow.Rent();
            var lease = popup.Lease;
            var content = new BreakPopup();
            popup.SetPopupContent(content, 700, 700);
            content.ActionSelected += (s, action) =>
            {
                if (content.CanClose())
                    popup.ReleaseToPool(lease);
            };
            return popup;
        }
    }
}
