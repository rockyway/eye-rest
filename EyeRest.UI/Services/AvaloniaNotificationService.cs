using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using EyeRest.UI.Views;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    public class AvaloniaNotificationService : INotificationService
    {
        private readonly ILogger<AvaloniaNotificationService> _logger;
        private readonly IScreenOverlayService _screenOverlayService;
        private readonly IConfigurationService _configurationService;
        private readonly IAudioService _audioService;
        private readonly IPopupWindowFactory _popupWindowFactory;
        private ITimerService? _timerService;
        private PopupWindow? _currentPopup;
        private readonly List<Window> _overlayWindows = new();
        private readonly object _lockObject = new();
        private bool _isTestMode;

        public bool IsTestMode => _isTestMode;
        public bool IsBreakWarningActive { get; private set; }
        public bool IsEyeRestWarningActive { get; private set; }
        public bool IsAnyPopupActive => _currentPopup != null;
        public bool IsBreakActive { get; private set; }

        public AvaloniaNotificationService(
            IScreenOverlayService screenOverlayService,
            IConfigurationService configurationService,
            IAudioService audioService,
            IPopupWindowFactory popupWindowFactory,
            ILogger<AvaloniaNotificationService> logger)
        {
            _screenOverlayService = screenOverlayService;
            _configurationService = configurationService;
            _audioService = audioService;
            _popupWindowFactory = popupWindowFactory;
            _logger = logger;
        }

        public void SetTimerService(ITimerService timerService) => _timerService = timerService;

        public async Task ShowEyeRestWarningAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = false;
            await ShowEyeRestWarningInternalAsync(timeUntilBreak);
        }

        public async Task ShowEyeRestWarningTestAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = true;
            await ShowEyeRestWarningInternalAsync(timeUntilBreak);
        }

        private async Task ShowEyeRestWarningInternalAsync(TimeSpan timeUntilBreak)
        {
            var tcs = new TaskCompletionSource<bool>();
            DispatcherTimer? updateTimer = null;
            PopupWindow? myPopup = null;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Showing eye rest warning popup");
                    IsEyeRestWarningActive = true;
                    CloseCurrentPopup(); // Close any existing popup first
                    myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestWarningPopup();
                    _currentPopup = myPopup;
                    myPopup.PositionOnScreen(PopupPlacement.TopRight);
                    myPopup.Show();

                    if (myPopup.PopupContent is EyeRestWarningPopup warningPopup)
                    {
                        var totalSeconds = (int)timeUntilBreak.TotalSeconds;
                        warningPopup.StartCountdown(totalSeconds);
                        warningPopup.WarningCompleted += (s, e) => tcs.TrySetResult(true);

                        var startTime = DateTime.Now;
                        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        updateTimer.Tick += (s, e) =>
                        {
                            var remaining = timeUntilBreak - (DateTime.Now - startTime);
                            warningPopup.UpdateCountdown(remaining);
                        };
                        updateTimer.Start();
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing eye rest warning");
                    tcs.TrySetResult(false);
                }
            });

            // Wait off the UI thread — no deadlock
            await Task.WhenAny(tcs.Task, Task.Delay(timeUntilBreak));

            // Close only OUR popup (not a newer one that replaced it)
            Dispatcher.UIThread.Post(() =>
            {
                updateTimer?.Stop();
                CloseSpecificPopup(myPopup);
                IsEyeRestWarningActive = false;
            });
        }

        public async Task ShowEyeRestReminderAsync(TimeSpan duration)
        {
            _isTestMode = false;
            await ShowEyeRestReminderInternalAsync(duration);
        }

        public async Task ShowEyeRestReminderTestAsync(TimeSpan duration)
        {
            _isTestMode = true;
            await ShowEyeRestReminderInternalAsync(duration);
        }

        private async Task ShowEyeRestReminderInternalAsync(TimeSpan duration)
        {
            var tcs = new TaskCompletionSource<bool>();
            PopupWindow? myPopup = null;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Showing eye rest reminder popup for {Duration}", duration);
                    CloseCurrentPopup(); // Close any existing popup (e.g., warning) first
                    myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestPopup();
                    _currentPopup = myPopup;
                    myPopup.PositionOnScreen(PopupPlacement.TopRight);
                    myPopup.Show();

                    if (myPopup.PopupContent is EyeRestPopup eyeRestPopup)
                    {
                        eyeRestPopup.Completed += (s, e) => tcs.TrySetResult(true);
                        eyeRestPopup.StartCountdown(duration);
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing eye rest reminder");
                    tcs.TrySetResult(false);
                }
            });

            // Wait off the UI thread — no deadlock
            await Task.WhenAny(tcs.Task, Task.Delay(duration + TimeSpan.FromSeconds(2)));

            Dispatcher.UIThread.Post(() => CloseSpecificPopup(myPopup));
        }

        public async Task ShowBreakWarningAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = false;
            await ShowBreakWarningInternalAsync(timeUntilBreak);
        }

        public async Task ShowBreakWarningTestAsync(TimeSpan timeUntilBreak)
        {
            _isTestMode = true;
            await ShowBreakWarningInternalAsync(timeUntilBreak);
        }

        private async Task ShowBreakWarningInternalAsync(TimeSpan timeUntilBreak)
        {
            var tcs = new TaskCompletionSource<bool>();
            DispatcherTimer? updateTimer = null;
            PopupWindow? myPopup = null;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Showing break warning popup");
                    IsBreakWarningActive = true;
                    CloseCurrentPopup();
                    myPopup = (PopupWindow)_popupWindowFactory.CreateBreakWarningPopup();
                    _currentPopup = myPopup;
                    myPopup.PositionOnScreen(PopupPlacement.TopRight);
                    myPopup.Show();

                    if (myPopup.PopupContent is BreakWarningPopup breakWarningPopup)
                    {
                        breakWarningPopup.StartCountdown(timeUntilBreak);
                        breakWarningPopup.Completed += (s, e) => tcs.TrySetResult(true);

                        var startTime = DateTime.Now;
                        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        updateTimer.Tick += (s, e) =>
                        {
                            var remaining = timeUntilBreak - (DateTime.Now - startTime);
                            breakWarningPopup.UpdateCountdown(remaining);
                        };
                        updateTimer.Start();
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing break warning");
                    tcs.TrySetResult(false);
                }
            });

            // Wait off the UI thread — no deadlock
            await Task.WhenAny(tcs.Task, Task.Delay(timeUntilBreak));

            Dispatcher.UIThread.Post(() =>
            {
                updateTimer?.Stop();
                CloseSpecificPopup(myPopup);
                IsBreakWarningActive = false;
            });
        }

        public async Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress)
        {
            _isTestMode = false;
            return await ShowBreakReminderInternalAsync(duration, progress);
        }

        public async Task<BreakAction> ShowBreakReminderTestAsync(TimeSpan duration, IProgress<double> progress)
        {
            _isTestMode = true;
            try
            {
                // Isolated test popup — does NOT modify _currentPopup, IsBreakActive,
                // dim overlays, or any shared state. This prevents the test from
                // interfering with the real break timer flow.
                var config = await _configurationService.LoadConfigurationAsync();
                var tcs = new TaskCompletionSource<BreakAction>();
                PopupWindow? testPopup = null;

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _logger.LogInformation("TEST MODE: Showing isolated break reminder popup for {Duration}", duration);
                        testPopup = (PopupWindow)_popupWindowFactory.CreateBreakPopup();
                        testPopup.PositionOnScreen(PopupPlacement.Center);
                        testPopup.Show();

                        if (testPopup.PopupContent is BreakPopup breakPopup)
                        {
                            breakPopup.SetConfiguration(
                                config.Break.RequireConfirmationAfterBreak,
                                config.Break.ResetTimersOnBreakConfirmation);

                            breakPopup.ActionSelected += (s, action) => tcs.TrySetResult(action);
                            breakPopup.StartCountdown(duration, progress);
                        }
                        else
                        {
                            tcs.TrySetResult(BreakAction.Completed);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error showing test break reminder");
                        tcs.TrySetResult(BreakAction.Completed);
                    }
                });

                var result = await tcs.Task;

                Dispatcher.UIThread.Post(() =>
                {
                    try { testPopup?.Close(); } catch { }
                });

                return result;
            }
            finally
            {
                _isTestMode = false;
            }
        }

        private async Task<BreakAction> ShowBreakReminderInternalAsync(TimeSpan duration, IProgress<double> progress)
        {
            // Load config off the UI thread first
            var config = await _configurationService.LoadConfigurationAsync();

            var tcs = new TaskCompletionSource<BreakAction>();
            PopupWindow? myPopup = null;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Showing break reminder popup for {Duration}", duration);
                    IsBreakActive = true;

                    CloseCurrentPopup();
                    ShowDimOverlays(config.Break.OverlayOpacityPercent);

                    myPopup = (PopupWindow)_popupWindowFactory.CreateBreakPopup();
                    _currentPopup = myPopup;
                    myPopup.PositionOnScreen(PopupPlacement.Center);
                    myPopup.Show();

                    if (myPopup.PopupContent is BreakPopup breakPopup)
                    {
                        breakPopup.SetConfiguration(
                            config.Break.RequireConfirmationAfterBreak,
                            config.Break.ResetTimersOnBreakConfirmation);

                        breakPopup.ActionSelected += (s, action) => tcs.TrySetResult(action);
                        breakPopup.StartCountdown(duration, progress);
                    }
                    else
                    {
                        tcs.TrySetResult(BreakAction.Completed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing break reminder");
                    tcs.TrySetResult(BreakAction.Completed);
                }
            });

            // Wait off the UI thread — no deadlock
            var result = await tcs.Task;

            Dispatcher.UIThread.Post(() =>
            {
                HideDimOverlays();
                CloseSpecificPopup(myPopup);
                IsBreakActive = false;
            });

            return result;
        }

        public async Task HideAllNotifications()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HideDimOverlays();
                CloseCurrentPopup();
                IsEyeRestWarningActive = false;
                IsBreakWarningActive = false;
                IsBreakActive = false;
            });
        }

        public void UpdateEyeRestWarningCountdown(TimeSpan remaining)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_currentPopup?.PopupContent is EyeRestWarningPopup warningPopup)
                {
                    warningPopup.UpdateCountdown(remaining);
                }
            });
        }

        public void UpdateBreakWarningCountdown(TimeSpan remaining)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_currentPopup?.PopupContent is BreakWarningPopup warningPopup)
                {
                    warningPopup.UpdateCountdown(remaining);
                }
            });
        }

        public void StartEyeRestWarningCountdown(TimeSpan duration)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_currentPopup?.PopupContent is EyeRestWarningPopup warningPopup)
                {
                    warningPopup.StartCountdown((int)duration.TotalSeconds);
                }
            });
        }

        public void StartBreakWarningCountdown(TimeSpan duration)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_currentPopup?.PopupContent is BreakWarningPopup warningPopup)
                {
                    warningPopup.StartCountdown(duration);
                }
            });
        }

        #region Screen Dimming Overlays

        /// <summary>
        /// Creates semi-transparent dark overlay windows on all screens for break dimming.
        /// </summary>
        private void ShowDimOverlays(int opacityPercent = 50)
        {
            try
            {
                HideDimOverlays(); // Clean up any existing overlays

                var app = Application.Current;
                if (app?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    return;

                var opacity = opacityPercent / 100.0;

                var screens = desktop.MainWindow?.Screens.All;
                if (screens == null || screens.Count == 0)
                    return;

                foreach (var screen in screens)
                {
                    var overlay = new Window
                    {
                        SystemDecorations = SystemDecorations.None,
                        Background = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0)),
                        Topmost = true,
                        ShowInTaskbar = false,
                        CanResize = false,
                        ShowActivated = false,
                        Width = screen.Bounds.Width / screen.Scaling,
                        Height = screen.Bounds.Height / screen.Scaling,
                        Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y),
                        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                    };

                    // Click on overlay removes dim from that screen (don't block user)
                    overlay.PointerPressed += (s, e) =>
                    {
                        if (s is Window overlayWin)
                        {
                            _overlayWindows.Remove(overlayWin);
                            try { overlayWin.Close(); } catch { }
                            _logger.LogInformation("User clicked dim overlay - removed from screen");
                        }
                    };

                    overlay.Show();
                    _overlayWindows.Add(overlay);
                }

                _logger.LogInformation("Showed {Count} dim overlay(s) at {Opacity}% opacity",
                    _overlayWindows.Count, opacityPercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing dim overlays");
            }
        }

        /// <summary>
        /// Closes all dim overlay windows.
        /// </summary>
        private void HideDimOverlays()
        {
            foreach (var overlay in _overlayWindows)
            {
                try
                {
                    overlay.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing dim overlay");
                }
            }
            _overlayWindows.Clear();
        }

        #endregion

        /// <summary>
        /// Closes the current popup (whatever _currentPopup points to).
        /// </summary>
        private void CloseCurrentPopup()
        {
            lock (_lockObject)
            {
                if (_currentPopup != null)
                {
                    try
                    {
                        _currentPopup.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing popup");
                    }
                    _currentPopup = null;
                }
            }
        }

        /// <summary>
        /// Closes a specific popup only if it's still the current one.
        /// Prevents the race where a warning cleanup closes a newer reminder popup.
        /// </summary>
        private void CloseSpecificPopup(PopupWindow? popup)
        {
            if (popup == null) return;
            lock (_lockObject)
            {
                try
                {
                    popup.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing specific popup");
                }

                // Only clear _currentPopup if it's still pointing to this popup
                if (_currentPopup == popup)
                    _currentPopup = null;
            }
        }
    }
}
