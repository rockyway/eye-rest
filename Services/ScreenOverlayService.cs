using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class ScreenOverlayService : IScreenOverlayService
    {
        private readonly ILogger<ScreenOverlayService> _logger;
        private readonly Dispatcher _dispatcher;
        private readonly List<OverlayWindow> _overlayWindows = new();
        private bool _isOverlayVisible = false;

        public event EventHandler<int>? OverlayClickedOnScreen;
        public event EventHandler? AllOverlaysClosed;

        public ScreenOverlayService(ILogger<ScreenOverlayService> logger, Dispatcher dispatcher)
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
            
            // Window properties for full-screen overlay
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            
            // Position and size to cover the specific screen
            Left = screen.Bounds.X;
            Top = screen.Bounds.Y;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
            
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
            Loaded += (s, e) => Focus();
        }
    }
}