using System;
using Avalonia;
using Avalonia.Controls;

namespace EyeRest.UI.Views
{
    public partial class PopupWindow : Window, EyeRest.Services.IPopupWindow
    {
        public PopupWindow()
        {
            InitializeComponent();
        }

        public new bool IsVisible => base.IsVisible;

        public new event EventHandler? Closed;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Closed?.Invoke(this, e);
        }

        public void PositionOnScreen()
        {
            if (Screens.Primary is { } screen)
            {
                var workArea = screen.WorkingArea;
                Position = new PixelPoint(
                    (int)(workArea.X + (workArea.Width - (int)Width) / 2),
                    (int)(workArea.Y + (workArea.Height - (int)Height) / 2)
                );
            }
        }
    }
}
