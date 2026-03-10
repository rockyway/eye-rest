using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using EyeRest.UI.Helpers;

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
        private PopupPlacement? _pendingPlacement;
        private PopupPlacement _currentPlacement = PopupPlacement.TopRight;

        /// <summary>
        /// Tracks how many popup windows are currently open.
        /// </summary>
        private static int _activePopupCount;

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



        /// <summary>
        /// Show the popup. The popup uses NSFloatingWindowLevel to appear above all
        /// normal windows without needing to hide or manipulate the main window.
        /// </summary>
        public new void Show()
        {
            _activePopupCount++;
            base.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _activePopupCount = Math.Max(0, _activePopupCount - 1);
            Closed?.Invoke(this, e);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Reposition using actual rendered size (SizeToContent makes the window
            // smaller than the hint, so initial positioning from PositionOnScreen
            // leaves a gap on the right edge).
            if (_pendingPlacement.HasValue && FrameSize.HasValue)
            {
                RepositionWithActualSize(_pendingPlacement.Value);
                _pendingPlacement = null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Set NSFloatingWindowLevel (3) so the popup floats above ALL normal
                // windows — including other apps. This replaces the old approach of
                // hiding/restoring the main window, which blocked users from accessing
                // the main UI while a popup was showing.
                MacOSNativeWindowHelper.SetWindowLevel(this, 3); // NSFloatingWindowLevel

                // Bring popup to front of its level and give it keyboard focus.
                // orderFront: does NOT activate the app, so the main window stays
                // exactly where it was — no jumping in front of other apps.
                MacOSNativeWindowHelper.OrderFront(this);
                MacOSNativeWindowHelper.MakeKeyWindow(this);
            }
            else
            {
                Activate();
            }

            Focus();
            Focusable = true;
        }

        private void RepositionWithActualSize(PopupPlacement placement)
        {
            var screen = GetScreenWithCursor();
            if (screen == null || !FrameSize.HasValue) return;

            var workArea = screen.WorkingArea;
            var scaling = screen.Scaling;
            var actualWidth = (int)(FrameSize.Value.Width * scaling);
            var actualHeight = (int)(FrameSize.Value.Height * scaling);

            switch (placement)
            {
                case PopupPlacement.TopRight:
                    var marginPx = (int)(8 * scaling);
                    Position = new PixelPoint(
                        workArea.Right - actualWidth - marginPx,
                        workArea.Y + marginPx
                    );
                    break;

                case PopupPlacement.Center:
                    Position = new PixelPoint(
                        workArea.X + (workArea.Width - actualWidth) / 2,
                        workArea.Y + (workArea.Height - actualHeight) / 2
                    );
                    break;
            }
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
        /// <summary>
        /// Reposition the popup window using its current placement and actual rendered size.
        /// Call this after content changes (e.g., compact transition) that alter the window dimensions.
        /// </summary>
        public void Reposition()
        {
            // Post to ensure SizeToContent has finished resizing the window
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (FrameSize.HasValue)
                    RepositionWithActualSize(_currentPlacement);
            }, Avalonia.Threading.DispatcherPriority.Render);
        }

        public void PositionOnScreen(PopupPlacement placement = PopupPlacement.TopRight)
        {
            _pendingPlacement = placement;
            _currentPlacement = placement;
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
                    var marginPx = (int)(8 * scaling);
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
