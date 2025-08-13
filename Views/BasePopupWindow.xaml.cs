using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace EyeRest.Views
{
    public partial class BasePopupWindow : Window
    {
        public event EventHandler? PopupClosed;
        public BasePopupWindow()
        {
            Debug.WriteLine($"🟡 BasePopupWindow constructor called - HashCode: {GetHashCode()}");
            InitializeComponent();
            
            // Subscribe to all window events for debugging
            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosed;
            Activated += OnActivated;
            Deactivated += OnDeactivated;
            StateChanged += OnStateChanged;
            IsVisibleChanged += OnIsVisibleChanged;
            
            Debug.WriteLine($"🟡 BasePopupWindow constructor completed - HashCode: {GetHashCode()}");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow Loaded event fired - HashCode: {GetHashCode()}");
            // Position on primary monitor or all monitors based on popup type
            PositionWindow();
            Debug.WriteLine($"🟡 BasePopupWindow positioned. Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}");
            Debug.WriteLine($"🟡 BasePopupWindow visibility: IsVisible={IsVisible}, Visibility={Visibility}, WindowState={WindowState}");
        }
        
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow CLOSING event - HashCode: {GetHashCode()}, Cancel: {e.Cancel}");
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 Closing called from:\n{stackTrace}");
        }
        
        private void OnClosed(object sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow CLOSED event - HashCode: {GetHashCode()}");
        }
        
        private void OnActivated(object sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow ACTIVATED - HashCode: {GetHashCode()}");
        }
        
        private void OnDeactivated(object sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow DEACTIVATED - HashCode: {GetHashCode()}");
        }
        
        private void OnStateChanged(object sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow STATE CHANGED - HashCode: {GetHashCode()}, WindowState: {WindowState}");
        }
        
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow VISIBILITY CHANGED - HashCode: {GetHashCode()}, IsVisible: {IsVisible}");
        }

        protected virtual void PositionWindow()
        {
            Debug.WriteLine("PositionWindow called");
            // Position as a stable non-intrusive overlay in top-right corner
            
            // Auto-size based on content, with reasonable limits
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 350;
            MinHeight = 200;
            MaxWidth = 900;
            MaxHeight = 1200;
            
            // Force measure to calculate actual size needed
            Measure(new Size(MaxWidth, MaxHeight));
            
            // Get actual size after measuring
            var actualWidth = Math.Max(MinWidth, Math.Min(MaxWidth, DesiredSize.Width));
            var actualHeight = Math.Max(MinHeight, Math.Min(MaxHeight, DesiredSize.Height));
            
            // Calculate position to avoid screen edge issues
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Position with safe margins
            Left = Math.Max(0, screenWidth - actualWidth - 50);
            Top = 50;
            
            // Ensure window stays within screen bounds
            if (Left + actualWidth > screenWidth) Left = screenWidth - actualWidth - 10;
            if (Top + actualHeight > screenHeight) Top = screenHeight - actualHeight - 10;
            
            Debug.WriteLine($"Window positioned: {actualWidth}x{actualHeight} at ({Left},{Top}) on screen {screenWidth}x{screenHeight}");
        }



        protected virtual void ClosePopup()
        {
            Debug.WriteLine($"🟡 ClosePopup called - HashCode: {GetHashCode()}");
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 ClosePopup called from:\n{stackTrace}");
            
            PopupClosed?.Invoke(this, EventArgs.Empty);
            Close();
            Debug.WriteLine($"🟡 Popup closed - HashCode: {GetHashCode()}");
        }

        public void SetContent(FrameworkElement content)
        {
            Debug.WriteLine($"🟡 SetContent called with content type: {content?.GetType().Name} - HashCode: {GetHashCode()}");
            ContentArea.Content = content;
            Debug.WriteLine($"🟡 Content set successfully - HashCode: {GetHashCode()}");
        }
        
        public new void Show()
        {
            Debug.WriteLine($"🟡 Show() called - HashCode: {GetHashCode()}");
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 Show() called from:\n{stackTrace}");
            
            // Critical fix: Ensure popup shows even when main window is minimized/hidden
            // Set Owner to null to make popup independent of main window state
            Owner = null;
            
            // Ensure window is topmost and can be activated
            Topmost = true;
            ShowInTaskbar = false;
            WindowState = WindowState.Normal;
            
            base.Show();
            
            // Force activation and bring to foreground
            try
            {
                Activate();
                Focus();
                
                // Use Win32 API to force foreground window (for cases where main app is minimized)
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hwnd);
                    ShowWindow(hwnd, SW_SHOW);
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                }
                
                Debug.WriteLine($"🟡 Popup forced to foreground and activated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🟡 Warning: Failed to force popup activation: {ex.Message}");
            }
            
            Debug.WriteLine($"🟡 Show() completed - IsVisible: {IsVisible}, WindowState: {WindowState}");
        }
        
        #region Win32 API for forcing popup to foreground
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int SW_SHOW = 5;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        #endregion
        
        public new void Close()
        {
            Debug.WriteLine($"🟡 Close() called - HashCode: {GetHashCode()}");
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 Close() called from:\n{stackTrace}");
            
            base.Close();
        }
    }
}