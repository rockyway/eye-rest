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

        // USABILITY FIX: Allow users to drag the popup window to move it out of the way
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Debug.WriteLine($"🖱️ DRAG: Mouse left button down detected - starting window drag");
                
                // Check if this is a break popup that should be draggable
                var isBreakReminder = ContentArea?.Content?.GetType().Name == "BreakPopup";
                Debug.WriteLine($"🖱️ DRAG: Content type: {ContentArea?.Content?.GetType().Name}, IsBreakReminder: {isBreakReminder}");
                
                if (isBreakReminder)
                {
                    // Enable dragging for break reminders so users can move them when working on critical tasks
                    Debug.WriteLine("🖱️ DRAG: Break reminder detected - enabling window drag");
                    this.DragMove();
                    Debug.WriteLine("🖱️ DRAG: Window drag completed");
                }
                else
                {
                    // Warning popups should not be draggable to maintain their intended positioning
                    Debug.WriteLine("🖱️ DRAG: Warning popup detected - drag disabled for positioning consistency");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🖱️ DRAG ERROR: Failed to handle mouse drag - {ex.Message}");
                // Don't rethrow - dragging is a usability feature, not critical functionality
            }
        }

        // USABILITY FIX: Change cursor to indicate draggable break popups
        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            var isBreakReminder = ContentArea?.Content?.GetType().Name == "BreakPopup";
            if (isBreakReminder)
            {
                this.Cursor = Cursors.SizeAll; // Four-arrow cursor indicates draggable
                Debug.WriteLine("🖱️ DRAG: Mouse entered break popup - cursor changed to SizeAll");
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            var isBreakReminder = ContentArea?.Content?.GetType().Name == "BreakPopup";
            if (isBreakReminder)
            {
                this.Cursor = Cursors.Arrow; // Reset to default cursor
                Debug.WriteLine("🖱️ DRAG: Mouse left break popup - cursor reset to Arrow");
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow Loaded event fired - HashCode: {GetHashCode()}");
            // Position on primary monitor or all monitors based on popup type
            PositionWindow();
            Debug.WriteLine($"🟡 BasePopupWindow positioned. Width: {Width}, Height: {Height}, Left: {Left}, Top: {Top}");
            Debug.WriteLine($"🟡 BasePopupWindow visibility: IsVisible={IsVisible}, Visibility={Visibility}, WindowState={WindowState}");
        }
        
        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow CLOSING event - HashCode: {GetHashCode()}, Cancel: {e.Cancel}");
            
            // CRITICAL FIX: Check if content is BreakPopup waiting for confirmation
            if (ContentArea?.Content is BreakPopup breakPopup)
            {
                if (!breakPopup.CanClose())
                {
                    Debug.WriteLine("🔒 PREVENTING CLOSE: Break popup is waiting for user confirmation");
                    e.Cancel = true;  // Prevent window from closing
                    return;
                }
            }
            
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 Closing called from:\n{stackTrace}");
        }
        
        private void OnClosed(object? sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow CLOSED event - HashCode: {GetHashCode()}");
        }
        
        private void OnActivated(object? sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow ACTIVATED - HashCode: {GetHashCode()}");
        }
        
        private void OnDeactivated(object? sender, EventArgs e)
        {
            Debug.WriteLine($"🟡 BasePopupWindow DEACTIVATED - HashCode: {GetHashCode()}");
        }
        
        private void OnStateChanged(object? sender, EventArgs e)
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
            
            // CRITICAL FIX: Check if this is a BreakPopup and handle differently
            var isBreakReminder = ContentArea?.Content?.GetType().Name == "BreakPopup";
            Debug.WriteLine($"🎯 POSITIONING FIX: Content type: {ContentArea?.Content?.GetType().Name}, IsBreakReminder: {isBreakReminder}");
            
            if (isBreakReminder)
            {
                // CRITICAL FIX: BreakPopup should be prominently displayed in center of screen
                Debug.WriteLine("🎯 POSITIONING FIX: Centering BreakPopup window for maximum visibility");
                
                // Set reasonable size for break popup
                SizeToContent = SizeToContent.Manual;
                Width = 1000;
                Height = 800;
                MinWidth = 800;
                MinHeight = 600;
                MaxWidth = 1200;
                MaxHeight = 1000;
                
                // Center on primary screen
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                Left = (screenWidth - Width) / 2;
                Top = (screenHeight - Height) / 2;
                
                // Make sure it's very visible
                Topmost = true;
                WindowState = WindowState.Normal;
                ShowInTaskbar = true; // Allow user to see it in taskbar
                
                Debug.WriteLine($"🎯 POSITIONING FIX: BreakPopup positioned at center: {Width}x{Height} at ({Left},{Top}) on screen {screenWidth}x{screenHeight}");
            }
            else
            {
                // Original positioning for warning popups (corner)
                Debug.WriteLine("🎯 POSITIONING FIX: Using corner positioning for warning popup");
                
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
                
                Debug.WriteLine($"🎯 POSITIONING FIX: Warning popup positioned at corner: {actualWidth}x{actualHeight} at ({Left},{Top}) on screen {screenWidth}x{screenHeight}");
            }
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
            
            try
            {
                // CRITICAL FIX: Enhanced popup visibility and Z-order management
                // Set Owner to null to make popup independent of main window state
                Owner = null;
                
                // CRITICAL: Use HWND_TOP instead of HWND_TOPMOST to avoid conflicts with other topmost windows
                Topmost = false; // Start with false to avoid Z-order conflicts
                ShowInTaskbar = false;
                WindowState = WindowState.Normal;
                
                // CRITICAL FIX: Set proper Z-order priority for popup stacking
                var currentTime = DateTime.Now.Ticks;
                this.Tag = currentTime; // Use timestamp for popup ordering
                Debug.WriteLine($"🟡 Popup Z-order timestamp: {currentTime}");
                
                base.Show();
                
                // CRITICAL FIX: Enhanced activation with retry mechanism
                var activationAttempts = 0;
                var maxAttempts = 3;
                
                while (activationAttempts < maxAttempts && !IsActive)
                {
                    activationAttempts++;
                    Debug.WriteLine($"🟡 Popup activation attempt #{activationAttempts}");
                    
                    try
                    {
                        // Progressive activation strategy
                        Activate();
                        Focus();
                        
                        // Use Win32 API with enhanced error handling
                        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                        if (hwnd != IntPtr.Zero)
                        {
                            // CRITICAL FIX: Use HWND_TOP instead of HWND_TOPMOST for better Z-order control
                            SetForegroundWindow(hwnd);
                            ShowWindow(hwnd, SW_SHOW);
                            SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                            
                            // FINAL: Set topmost only after positioning
                            if (activationAttempts == maxAttempts)
                            {
                                Topmost = true; // Final attempt: use topmost
                                Debug.WriteLine($"🟡 Final attempt: Set Topmost=true");
                            }
                        }
                        
                        if (IsActive)
                        {
                            Debug.WriteLine($"🟡 Popup successfully activated on attempt #{activationAttempts}");
                            break;
                        }
                        
                        if (activationAttempts < maxAttempts)
                        {
                            System.Threading.Thread.Sleep(50); // Brief delay between attempts
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"🟡 Activation attempt #{activationAttempts} failed: {ex.Message}");
                        if (activationAttempts == maxAttempts)
                        {
                            Debug.WriteLine($"🟡 All activation attempts failed, popup may not be properly focused");
                        }
                    }
                }
                
                // CRITICAL FIX: Post-resume visibility recovery
                Debug.WriteLine("🟡 POST-RESUME RECOVERY: Verifying window visibility after activation attempts");
                bool actuallyVisible = VerifyWindowIsActuallyVisible();
                
                if (!actuallyVisible)
                {
                    Debug.WriteLine("🟡 POST-RESUME RECOVERY: Window not actually visible - initiating recovery");
                    ForceToForeground();
                    
                    // Final verification
                    actuallyVisible = VerifyWindowIsActuallyVisible();
                    if (!actuallyVisible)
                    {
                        Debug.WriteLine("🟡 CRITICAL: Popup still not visible after recovery - may be invisible to user");
                    }
                }
                else
                {
                    Debug.WriteLine("🟡 POST-RESUME RECOVERY: Window properly visible - no recovery needed");
                }
                
                Debug.WriteLine($"🟡 Show() completed - IsVisible: {IsVisible}, IsActive: {IsActive}, WindowState: {WindowState}, ActuallyVisible: {actuallyVisible}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🟡 CRITICAL ERROR in Show(): {ex.Message}\n{ex.StackTrace}");
                throw; // Re-throw to ensure caller knows about the failure
            }
        }
        
        #region Win32 API for forcing popup to foreground
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        #endregion
        
        /// <summary>
        /// CRITICAL FIX: Verify window is actually visible to the user (not just IsActive)
        /// Addresses post-resume window visibility issues
        /// </summary>
        private bool VerifyWindowIsActuallyVisible()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return false;
                
                // Check if window is visible and not minimized
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return false;
                
                // Check if window is positioned on screen (not moved off-screen)
                if (GetWindowRect(hwnd, out RECT rect))
                {
                    var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                    var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
                    
                    // Window must have some visible area on screen
                    bool onScreen = rect.Right > 0 && rect.Bottom > 0 && 
                                   rect.Left < screenWidth && rect.Top < screenHeight;
                    
                    Debug.WriteLine($"🟡 VISIBILITY CHECK: OnScreen={onScreen}, Rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}), Screen=({screenWidth}x{screenHeight})");
                    return onScreen;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🟡 ERROR in VerifyWindowIsActuallyVisible: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// CRITICAL FIX: Force window to foreground using multiple recovery strategies
        /// Essential for post-resume popup visibility
        /// </summary>
        public void ForceToForeground()
        {
            try
            {
                Debug.WriteLine($"🟡 FORCE FOREGROUND: Starting recovery for HashCode: {GetHashCode()}");
                
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    Debug.WriteLine("🟡 FORCE FOREGROUND: Invalid window handle");
                    return;
                }
                
                // Multi-stage recovery process
                Debug.WriteLine("🟡 FORCE FOREGROUND: Stage 1 - Window state recovery");
                
                // Stage 1: Reset window state (fixes post-resume state corruption)
                this.WindowState = WindowState.Minimized;
                System.Threading.Thread.Sleep(50); // Brief delay for state change
                this.WindowState = WindowState.Normal;
                
                // Stage 2: Force Z-order and visibility
                Debug.WriteLine("🟡 FORCE FOREGROUND: Stage 2 - Z-order recovery");
                this.Topmost = false;
                this.Topmost = true;
                
                // Stage 3: Win32 API recovery
                Debug.WriteLine("🟡 FORCE FOREGROUND: Stage 3 - Win32 API recovery");
                ShowWindow(hwnd, SW_RESTORE);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                
                // Stage 4: WPF activation
                Debug.WriteLine("🟡 FORCE FOREGROUND: Stage 4 - WPF activation");
                this.Activate();
                this.Focus();
                
                // Verify recovery success
                bool visible = VerifyWindowIsActuallyVisible();
                Debug.WriteLine($"🟡 FORCE FOREGROUND: Recovery completed - Visible: {visible}, Active: {IsActive}");
                
                if (!visible)
                {
                    Debug.WriteLine("🟡 FORCE FOREGROUND: WARNING - Window still not visible after recovery");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🟡 ERROR in ForceToForeground: {ex.Message}");
            }
        }
        
        public new void Close()
        {
            Debug.WriteLine($"🟡 Close() called - HashCode: {GetHashCode()}");
            var stackTrace = new StackTrace(true);
            Debug.WriteLine($"🟡 Close() called from:\n{stackTrace}");
            
            base.Close();
        }
    }
}