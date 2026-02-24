using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;

namespace EyeRest.UI.Views
{
    public enum PopupPlacement
    {
        TopRight,
        Center
    }

    public partial class PopupWindow : Window, EyeRest.Services.IPopupWindow
    {
        public Control? PopupContent { get; private set; }
        private double _positionHintWidth;
        private double _positionHintHeight;

        public PopupWindow()
        {
            InitializeComponent();
        }

        public void SetPopupContent(Control content, double width, double height)
        {
            PopupContent = content;
            ContentArea.Content = content;
            // Store hint sizes for positioning; actual window size is determined by SizeToContent
            _positionHintWidth = width;
            _positionHintHeight = height;
        }

        public new bool IsVisible => base.IsVisible;

        public new event EventHandler? Closed;

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Closed?.Invoke(this, e);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // Ensure window gets keyboard focus for ESC handling
            Activate();
            Focus();
            Focusable = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            // Allow dragging the popup window from background areas only.
            // Don't intercept clicks on interactive controls (buttons, etc.)
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && !e.Handled)
            {
                var source = e.Source as Avalonia.Controls.Control;
                while (source != null && source != this)
                {
                    if (source is Avalonia.Controls.Button ||
                        source is Avalonia.Controls.Primitives.ToggleButton ||
                        source is Avalonia.Controls.Slider ||
                        source is Avalonia.Controls.TextBox)
                        return; // Don't drag — let the control handle the click
                    source = source.Parent as Avalonia.Controls.Control;
                }
                BeginMoveDrag(e);
            }
        }

        /// <summary>
        /// Position popup on screen using the specified placement.
        /// Detects which screen the mouse cursor is on and positions accordingly.
        /// </summary>
        public void PositionOnScreen(PopupPlacement placement = PopupPlacement.TopRight)
        {
            var screen = GetScreenWithCursor();
            if (screen == null)
                return;

            var workArea = screen.WorkingArea;
            var scaling = screen.Scaling;

            // Use hint sizes for positioning (actual size determined by SizeToContent)
            var windowWidthPx = (int)(_positionHintWidth * scaling);
            var windowHeightPx = (int)(_positionHintHeight * scaling);

            switch (placement)
            {
                case PopupPlacement.TopRight:
                    // Position at top-right of the working area with some margin
                    var marginPx = (int)(20 * scaling);
                    Position = new PixelPoint(
                        workArea.Right - windowWidthPx - marginPx,
                        workArea.Y + marginPx
                    );
                    break;

                case PopupPlacement.Center:
                    Position = new PixelPoint(
                        workArea.X + (workArea.Width - windowWidthPx) / 2,
                        workArea.Y + (workArea.Height - windowHeightPx) / 2
                    );
                    break;
            }
        }

        /// <summary>
        /// Get the screen that currently contains the mouse cursor.
        /// Falls back to primary screen if detection fails.
        /// </summary>
        private Screen? GetScreenWithCursor()
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    return GetScreenWithCursorMacOS();
                }

                if (OperatingSystem.IsWindows())
                {
                    return GetScreenWithCursorWindows();
                }
            }
            catch
            {
                // Fall through to primary screen
            }

            return Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        }

        #region macOS cursor detection

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint
        {
            public double X;
            public double Y;
        }

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreate(IntPtr source);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern CGPoint CGEventGetLocation(IntPtr eventRef);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        private Screen? GetScreenWithCursorMacOS()
        {
            // CGEventGetLocation returns in macOS global display coordinates (points, top-left origin)
            var eventRef = CGEventCreate(IntPtr.Zero);
            if (eventRef == IntPtr.Zero)
                return Screens.Primary;

            try
            {
                var cursorPt = CGEventGetLocation(eventRef);

                // Find which Avalonia screen contains this cursor position
                // Avalonia Screen.Bounds on macOS are in pixel coords; CGEvent is in points
                foreach (var screen in Screens.All)
                {
                    var scaling = screen.Scaling;
                    // Convert screen pixel bounds to point bounds for comparison
                    var left = screen.Bounds.X / scaling;
                    var top = screen.Bounds.Y / scaling;
                    var right = left + screen.Bounds.Width / scaling;
                    var bottom = top + screen.Bounds.Height / scaling;

                    if (cursorPt.X >= left && cursorPt.X < right &&
                        cursorPt.Y >= top && cursorPt.Y < bottom)
                    {
                        return screen;
                    }
                }
            }
            finally
            {
                CFRelease(eventRef);
            }

            return Screens.Primary;
        }

        #endregion

        #region Windows cursor detection

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private Screen? GetScreenWithCursorWindows()
        {
            if (!GetCursorPos(out var cursorPos))
                return Screens.Primary;

            // On Windows, both cursor and screen bounds are in physical pixels
            foreach (var screen in Screens.All)
            {
                if (cursorPos.X >= screen.Bounds.X && cursorPos.X < screen.Bounds.Right &&
                    cursorPos.Y >= screen.Bounds.Y && cursorPos.Y < screen.Bounds.Bottom)
                {
                    return screen;
                }
            }

            return Screens.Primary;
        }

        #endregion
    }
}
