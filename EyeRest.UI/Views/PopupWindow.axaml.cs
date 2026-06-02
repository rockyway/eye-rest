using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Input;
using Avalonia.Platform;
using EyeRest.UI.Helpers;

namespace EyeRest.UI.Views
{
    public enum PopupPlacement
    {
        Center,
        TopLeft,
        TopCenter,
        TopRight,
        LeftCenter,
        RightCenter,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    public partial class PopupWindow : Window, EyeRest.Services.IPopupWindow
    {
        public Control? PopupContent { get; private set; }
        private double _positionHintWidth;
        private double _positionHintHeight;
        private PopupPlacement? _pendingPlacement;
        private PopupPlacement _currentPlacement = PopupPlacement.TopRight;

        // True between PositionOnScreen() and ReleaseToPool(): gates the SizeChanged-driven
        // reposition so a pooled, hidden shell doesn't reposition on stray size events.
        private bool _positioned;

        /// <summary>
        /// Tracks how many popup windows are currently open.
        /// </summary>
        private static int _activePopupCount;

        /// <summary>
        /// Pool of reusable shells. Avalonia.Native pins each shown Window forever via its
        /// accessibility peer (see docs/plan/009); reusing shells makes that peer created once
        /// per shell instead of leaked per cycle. Bounded — at most a couple of shells are ever
        /// live. All access is on the UI thread.
        /// </summary>
        private static readonly Stack<PopupWindow> s_pool = new();
        private const int MaxPoolSize = 4;
        private static long s_leaseCounter;

        // Generation token: a deferred close from a prior cycle must not release a shell that
        // has since been re-rented for a new popup. Each Rent() assigns a fresh lease; release
        // only acts when the caller's captured lease still matches.
        private long _lease;
        private bool _released = true; // a shell not currently rented is "released"

        /// <summary>The lease of the current rental. Capture this when scheduling a deferred close.</summary>
        public long Lease => _lease;

        public PopupWindow()
        {
            InitializeComponent();

            // SizeToContent resizes the window AFTER OnOpened, and on a pooled shell reused for
            // new content FrameSize is stale until that resize lands. Reposition whenever the
            // rendered size settles so the popup is placed for its ACTUAL size — fixes the popup
            // clipping off-screen / leaving a gap when shown across differently-sized monitors.
            SizeChanged += OnContentSizeSettled;
        }

        private void OnContentSizeSettled(object? sender, SizeChangedEventArgs e)
        {
            if (_positioned)
                RepositionWithActualSize(_currentPlacement);
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

        // Suppress the macOS accessibility automation peers for this transient popup's CONTENT.
        // Avalonia.Native pins every control's automation peer with a strong native (MicroComShadow)
        // GC handle that is never released on close — a per-cycle leak confirmed by gcroot (the
        // content UserControl's UserControlAutomationPeer; see docs/plan/009). Returning null is
        // UNSAFE (AvnWindow dereferences the root peer). Instead we hand the window a childless
        // peer, so the native side never enumerates/materializes the content controls' peers.
        // Tradeoff: these transient reminder popups are not exposed to screen readers (the main
        // settings window keeps its accessibility).
        protected override AutomationPeer OnCreateAutomationPeer()
            // Only suppress on macOS, where the AvnAutomationPeer/MicroComShadow strong-handle
            // leak lives. On Windows the Win32 backend has no such leak, so keep full popup
            // accessibility there.
            => OperatingSystem.IsMacOS()
                ? new ChildlessAutomationPeer(this)
                : base.OnCreateAutomationPeer();

        private sealed class ChildlessAutomationPeer : NoneAutomationPeer
        {
            public ChildlessAutomationPeer(Control owner) : base(owner) { }
            protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => Array.Empty<AutomationPeer>();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _activePopupCount = Math.Max(0, _activePopupCount - 1);
            Closed?.Invoke(this, e);
        }

        /// <summary>
        /// Rents a shell from the pool (or creates one) with a fresh lease. The returned shell
        /// has no content and no Closed subscribers. Caller must SetPopupContent + Show.
        /// UI-thread only.
        /// </summary>
        public static PopupWindow Rent()
        {
            var w = s_pool.Count > 0 ? s_pool.Pop() : new PopupWindow();
            w._lease = ++s_leaseCounter;
            w._released = false;
            return w;
        }

        /// <summary>
        /// "Soft close": hide the shell and return it to the pool for reuse instead of destroying
        /// it (real Close() leaks the native accessibility peer on macOS — docs/plan/009). No-op
        /// if already released this cycle, or if <paramref name="expectedLease"/> is stale (the
        /// shell was re-rented). Raises <see cref="Closed"/> exactly once per lease, then clears subscribers
        /// + content so the pooled shell retains nothing. UI-thread only.
        /// </summary>
        public void ReleaseToPool(long expectedLease)
        {
            if (_released || _lease != expectedLease) return;
            _released = true;
            _positioned = false; // stop SizeChanged-driven repositioning while pooled/hidden

            try { base.Hide(); } catch { /* best effort */ }
            _activePopupCount = Math.Max(0, _activePopupCount - 1);

            // Snapshot + clear subscribers BEFORE invoking so a re-entrant release sees none,
            // and clear per-cycle references so the pooled shell retains nothing.
            var handlers = Closed;
            Closed = null;
            ContentArea.Content = null; // release the heavy content visual tree
            PopupContent = null;
            _pendingPlacement = null;
            DataContext = null;

            try { handlers?.Invoke(this, EventArgs.Empty); }
            finally
            {
                if (s_pool.Count < MaxPoolSize)
                    s_pool.Push(this);
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // OnOpened re-fires on every Show() (including pooled reuse) in Avalonia 11.3 — so
            // this is the single per-show setup path.
            ApplyShowState();
        }

        /// <summary>
        /// Applies per-show window state: reposition using the actual rendered size, raise to the
        /// floating window level (macOS) and take focus. Idempotent; safe to call on every show
        /// (first open and pooled reuse).
        /// </summary>
        private void ApplyShowState()
        {
            // Reposition using actual rendered size (SizeToContent makes the window
            // smaller than the hint, so initial positioning from PositionOnScreen
            // leaves a gap on the right edge).
            if (_pendingPlacement.HasValue)
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

                // Surface the popup above other apps WITHOUT stealing focus. We deliberately
                // do NOT call makeKeyWindow/Focus(): this app runs as an Accessory app, so
                // making an inactive app's window key forces AppKit to activate us — yanking
                // the user's keyboard focus out of whatever they were typing in (the reported
                // bug). orderFrontRegardless raises the floating-level window above other apps
                // while leaving focus exactly where it was.
                MacOSNativeWindowHelper.OrderFrontRegardless(this);
            }
            // Windows/Linux: Topmost="True" + ShowActivated="False" (PopupWindow.axaml) already
            // raise the popup above other windows without taking focus. We deliberately do NOT
            // call Activate()/Focus() here — either would steal the foreground app's focus.

            // Stay focusable so the user can click the popup to engage keyboard handlers
            // (e.g. Esc-to-dismiss), but never grab focus programmatically on show.
            Focusable = true;
        }

        private void RepositionWithActualSize(PopupPlacement placement)
        {
            var screen = GetScreenWithCursor();
            if (screen == null) return;

            // Use DesiredSize (the measured content size), NOT FrameSize/ClientSize. On a pooled
            // shell reused for new content, FrameSize/ClientSize intermittently report the PREVIOUS
            // popup's size (stale) — confirmed via diagnostics — which mis-positions the popup so it
            // clips off-screen or leaves a gap. DesiredSize stays correct across pooled reuse and the
            // full→compact transition.
            var size = DesiredSize;
            if (size.Width <= 0 || size.Height <= 0) return;

            var scaling = screen.Scaling;
            var actualWidth = (int)(size.Width * scaling);
            var actualHeight = (int)(size.Height * scaling);

            Position = ComputePosition(placement, screen.WorkingArea, scaling, actualWidth, actualHeight);
        }

        public static PixelPoint ComputePosition(
            PopupPlacement placement,
            PixelRect workArea,
            double scaling,
            int widthPx,
            int heightPx)
        {
            var marginPx = (int)(8 * scaling);
            return placement switch
            {
                PopupPlacement.Center =>
                    new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                                   workArea.Y + (workArea.Height - heightPx) / 2),
                PopupPlacement.TopLeft =>
                    new PixelPoint(workArea.X + marginPx, workArea.Y + marginPx),
                PopupPlacement.TopCenter =>
                    new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                                   workArea.Y + marginPx),
                PopupPlacement.TopRight =>
                    new PixelPoint(workArea.Right - widthPx - marginPx,
                                   workArea.Y + marginPx),
                PopupPlacement.LeftCenter =>
                    new PixelPoint(workArea.X + marginPx,
                                   workArea.Y + (workArea.Height - heightPx) / 2),
                PopupPlacement.RightCenter =>
                    new PixelPoint(workArea.Right - widthPx - marginPx,
                                   workArea.Y + (workArea.Height - heightPx) / 2),
                PopupPlacement.BottomLeft =>
                    new PixelPoint(workArea.X + marginPx,
                                   workArea.Bottom - heightPx - marginPx),
                PopupPlacement.BottomCenter =>
                    new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                                   workArea.Bottom - heightPx - marginPx),
                PopupPlacement.BottomRight =>
                    new PixelPoint(workArea.Right - widthPx - marginPx,
                                   workArea.Bottom - heightPx - marginPx),
                _ => new PixelPoint(workArea.Right - widthPx - marginPx,
                                    workArea.Y + marginPx),
            };
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
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => RepositionWithActualSize(_currentPlacement),
                Avalonia.Threading.DispatcherPriority.Render);
        }

        public void PositionOnScreen(PopupPlacement placement = PopupPlacement.TopRight)
        {
            _pendingPlacement = placement;
            _currentPlacement = placement;
            _positioned = true;
            var screen = GetScreenWithCursor();
            if (screen == null)
                return;

            var scaling = screen.Scaling;
            var widthPx = (int)(_positionHintWidth * scaling);
            var heightPx = (int)(_positionHintHeight * scaling);

            Position = ComputePosition(placement, screen.WorkingArea, scaling, widthPx, heightPx);
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
