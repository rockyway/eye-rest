using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class ScreenOverlayService : IScreenOverlayService
    {
        private readonly ILogger<ScreenOverlayService> _logger;
        private readonly IDispatcherService _dispatcher;
        private readonly List<OverlayWindow> _overlayWindows = new();
        private bool _isOverlayVisible = false;

        public event EventHandler<int>? OverlayClickedOnScreen;
        public event EventHandler? AllOverlaysClosed;

        public ScreenOverlayService(ILogger<ScreenOverlayService> logger, IDispatcherService dispatcher)
        {
            _logger = logger;
            _dispatcher = dispatcher;
        }

        public int ScreenCount => Screen.AllScreens.Length;

        public bool IsOverlayVisible => _isOverlayVisible;

        public async Task ShowOverlayAsync(double opacity = 0.5)
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _logger.LogInformation($"Showing overlay on all screens with opacity {opacity:P0}");

                    // Hide any existing overlays first
                    HideOverlayInternal();

                    var screens = Screen.AllScreens;
                    _logger.LogInformation($"Detected {screens.Length} screen(s)");

                    for (int i = 0; i < screens.Length; i++)
                    {
                        var screen = screens[i];
                        var overlay = new OverlayWindow(i, screen, opacity);
                        
                        // Subscribe to click event
                        overlay.OverlayClicked += OnOverlayClicked;
                        
                        _overlayWindows.Add(overlay);
                        overlay.Show();
                        
                        _logger.LogInformation($"Overlay {i + 1} shown on screen {i + 1} ({screen.Bounds.Width}x{screen.Bounds.Height} at {screen.Bounds.X},{screen.Bounds.Y})");
                    }

                    _isOverlayVisible = true;
                    _logger.LogInformation($"All {screens.Length} overlay(s) are now visible");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing screen overlays");
                throw;
            }
        }

        public async Task HideOverlayAsync()
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    HideOverlayInternal();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding screen overlays");
                throw;
            }
        }

        public async Task HideOverlayOnScreenAsync(int screenIndex)
        {
            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    if (screenIndex >= 0 && screenIndex < _overlayWindows.Count)
                    {
                        var overlay = _overlayWindows[screenIndex];
                        if (overlay != null && overlay.IsVisible)
                        {
                            overlay.OverlayClicked -= OnOverlayClicked;
                            overlay.Close();
                            _overlayWindows[screenIndex] = null!; // Mark as closed
                            
                            _logger.LogInformation($"Overlay hidden on screen {screenIndex + 1}");
                            
                            // Check if all overlays are now closed
                            CheckIfAllOverlaysClosed();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error hiding overlay on screen {screenIndex}");
                throw;
            }
        }

        private void HideOverlayInternal()
        {
            _logger.LogInformation($"Hiding {_overlayWindows.Count} overlay window(s)");

            foreach (var overlay in _overlayWindows.Where(o => o != null))
            {
                try
                {
                    overlay.OverlayClicked -= OnOverlayClicked;
                    if (overlay.IsLoaded)
                    {
                        overlay.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing individual overlay window");
                }
            }

            _overlayWindows.Clear();
            _isOverlayVisible = false;
            _logger.LogInformation("All overlay windows hidden and cleared");
        }

        private void OnOverlayClicked(object? sender, int screenIndex)
        {
            _logger.LogInformation($"Overlay clicked on screen {screenIndex + 1}");
            
            // Fire event for specific screen
            OverlayClickedOnScreen?.Invoke(this, screenIndex);
            
            // Hide the clicked overlay
            _ = HideOverlayOnScreenAsync(screenIndex);
        }

        private void CheckIfAllOverlaysClosed()
        {
            var visibleOverlays = _overlayWindows.Where(o => o != null && o.IsVisible).ToList();
            
            _logger.LogInformation($"Checking overlay status: {visibleOverlays.Count} still visible out of {_overlayWindows.Count} total");
            
            if (visibleOverlays.Count == 0)
            {
                _isOverlayVisible = false;
                _overlayWindows.Clear();
                
                _logger.LogInformation("All overlays have been closed by user interaction");
                AllOverlaysClosed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Full-screen overlay window for a specific screen
    /// </summary>
    public class OverlayWindow : Window
    {
        private readonly int _screenIndex;
        
        public event EventHandler<int>? OverlayClicked;

        public OverlayWindow(int screenIndex, Screen screen, double opacity)
        {
            _screenIndex = screenIndex;
            
            // Window properties for full-screen overlay with popup cutout
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            AllowsTransparency = true;
            Background = Brushes.Transparent; // Transparent to allow custom content
            Topmost = false; // Set to false so popup can appear on top
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            
            // Position and size to cover the specific screen
            Left = screen.Bounds.X;
            Top = screen.Bounds.Y;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            
            // Create custom content with cutout for break popup (only on primary screen)
            CreateOverlayWithCutout(screen, opacity, screenIndex);
            
            // Handle click to close
            MouseDown += (s, e) =>
            {
                OverlayClicked?.Invoke(this, _screenIndex);
            };
            
            // Handle key press to close (Escape key)
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    OverlayClicked?.Invoke(this, _screenIndex);
                }
            };
            
            // Ensure window can receive focus for keyboard input
            Focusable = true;
            Loaded += (s, e) => 
            {
                Focus();
                
                // Force overlay to show even when main app is minimized
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        SetForegroundWindow(hwnd);
                        ShowWindow(hwnd, SW_SHOW);
                        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to force overlay activation: {ex.Message}");
                }
            };
        }

        private void CreateOverlayWithCutout(Screen screen, double opacity, int screenIndex)
        {
            var canvas = new Canvas();
            
            // Determine if this screen should have a cutout for the break popup
            // Break popup appears on primary screen only, positioned in top-right corner
            var primaryScreen = Screen.PrimaryScreen;
            var shouldHaveCutout = screen.Equals(primaryScreen);
            
            if (shouldHaveCutout)
            {
                // Calculate cutout based on actual BasePopupWindow.PositionWindow() logic
                // Popup dimensions (from BreakPopup.xaml): MaxWidth="900" MaxHeight="750"
                var popupMaxWidth = 900.0;
                var popupMaxHeight = 750.0;
                
                // Replicate BasePopupWindow positioning logic
                var actualWidth = Math.Max(350, Math.Min(popupMaxWidth, popupMaxWidth)); // Assume max size for cutout
                var actualHeight = Math.Max(200, Math.Min(750, popupMaxHeight));
                
                // Position calculation from BasePopupWindow.PositionWindow()
                var cutoutLeft = Math.Max(0, screen.Bounds.Width - actualWidth - 50);
                var cutoutTop = 50;
                
                // Add some padding around the cutout area
                var cutoutPadding = 20;
                cutoutLeft = Math.Max(0, cutoutLeft - cutoutPadding);
                cutoutTop = Math.Max(0, cutoutTop - cutoutPadding);
                var cutoutWidth = actualWidth + (cutoutPadding * 2);
                var cutoutHeight = actualHeight + (cutoutPadding * 2);
                
                // Ensure cutout stays within screen bounds
                if (cutoutLeft + cutoutWidth > screen.Bounds.Width)
                    cutoutWidth = screen.Bounds.Width - cutoutLeft;
                if (cutoutTop + cutoutHeight > screen.Bounds.Height)
                    cutoutHeight = screen.Bounds.Height - cutoutTop;
                
                // Create 4 rectangles around the cutout area
                // Top rectangle (above cutout)
                if (cutoutTop > 0)
                    canvas.Children.Add(CreateOverlayRectangle(opacity, 0, 0, screen.Bounds.Width, cutoutTop));
                
                // Left rectangle (left of cutout)
                if (cutoutLeft > 0)
                    canvas.Children.Add(CreateOverlayRectangle(opacity, 0, cutoutTop, cutoutLeft, cutoutHeight));
                
                // Right rectangle (right of cutout)
                var rightStart = cutoutLeft + cutoutWidth;
                if (rightStart < screen.Bounds.Width)
                    canvas.Children.Add(CreateOverlayRectangle(opacity, rightStart, cutoutTop, screen.Bounds.Width - rightStart, cutoutHeight));
                
                // Bottom rectangle (below cutout)
                var bottomStart = cutoutTop + cutoutHeight;
                if (bottomStart < screen.Bounds.Height)
                    canvas.Children.Add(CreateOverlayRectangle(opacity, 0, bottomStart, screen.Bounds.Width, screen.Bounds.Height - bottomStart));
            }
            else
            {
                // Non-primary screens get full overlay (no cutout)
                canvas.Children.Add(CreateOverlayRectangle(opacity, 0, 0, screen.Bounds.Width, screen.Bounds.Height));
            }
            
            Content = canvas;
        }

        private Rectangle CreateOverlayRectangle(double opacity, double left, double top, double width, double height)
        {
            var rectangle = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0)),
                Width = width,
                Height = height
            };
            
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            
            // Handle click on this rectangle to close overlay
            rectangle.MouseDown += (s, e) =>
            {
                OverlayClicked?.Invoke(this, _screenIndex);
            };
            
            return rectangle;
        }
        
        #region Win32 API for forcing overlay to foreground
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
    }
}