using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using EyeRest.Models;
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

        /// <summary>
        /// BL-002 M5: fire-and-forget play of a per-channel audio config. Runs on the
        /// thread-pool so the popup show/close path doesn't block on audio I/O. The
        /// inner exception is swallowed and logged — audio is non-critical for the
        /// popup pipeline; a missing custom file or unreachable URL must NOT prevent
        /// the timer from working.
        /// </summary>
        private void FireChannelAudio(AudioChannel channel, Func<AppConfiguration, AudioChannelConfig> selector)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var config = await _configurationService.LoadConfigurationAsync().ConfigureAwait(false);
                    var channelConfig = selector(config);
                    await _audioService.PlayChannelAsync(channel, channelConfig).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Channel audio fire-and-forget failed for {Channel}", channel);
                }
            });
        }

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

                    // Deferred via Dispatcher.Post — see ShowBreakReminderInternalAsync
                    // for the full race-condition explanation.
                    myPopup.Closed += (_, _) =>
                        Dispatcher.UIThread.Post(
                            () => tcs.TrySetResult(false),
                            DispatcherPriority.Background);

                    myPopup.Show();

                    if (myPopup.PopupContent is EyeRestWarningPopup warningPopup)
                    {
                        var totalSeconds = (int)timeUntilBreak.TotalSeconds;
                        warningPopup.StartCountdown(totalSeconds);
                        warningPopup.WarningCompleted += (s, e) => tcs.TrySetResult(true);
                        // Countdown updates are driven by TimerService via
                        // UpdateEyeRestWarningCountdown() — no local timer needed.
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
            // Load config OFF the UI thread first (mirrors BreakInternal).
            var config = await _configurationService.LoadConfigurationAsync();

            var tcs = new TaskCompletionSource<bool>();
            PopupWindow? myPopup = null;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    _logger.LogInformation("Showing eye rest reminder popup for {Duration}", duration);
                    CloseCurrentPopup(); // Close any existing popup (e.g., warning) first
                    // Defensive: a prior popup's overlays could outlive its popup
                    // if cleanup raced with this show path. Tear them down before we
                    // potentially show our own — HideDimOverlays is idempotent (no-op on empty).
                    HideDimOverlays();

                    if (config.EyeRest.OverlayEnabled)
                        ShowDimOverlays(config.EyeRest.OverlayOpacityPercent);

                    myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestPopup();
                    _currentPopup = myPopup;
                    myPopup.PositionOnScreen(MapPlacement(config.EyeRest.PopupPosition));

                    // Deferred via Dispatcher.Post — see ShowBreakReminderInternalAsync
                    // for the full race-condition explanation.
                    // BL-002 M5: also fire the EyeRest END channel audio on any close path.
                    myPopup.Closed += (_, _) =>
                    {
                        FireChannelAudio(AudioChannel.EyeRestEnd, c => c.EyeRest.EndAudio);
                        Dispatcher.UIThread.Post(
                            () => tcs.TrySetResult(false),
                            DispatcherPriority.Background);
                    };

                    myPopup.Show();

                    // BL-002 M5: fire the EyeRest START channel audio after the popup is shown.
                    FireChannelAudio(AudioChannel.EyeRestStart, c => c.EyeRest.StartAudio);

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

            Dispatcher.UIThread.Post(() =>
            {
                // Unconditional cleanup: HideDimOverlays is idempotent, and gating
                // on the captured config value would leak overlay windows if the user
                // toggled OverlayEnabled off during the popup's lifetime.
                HideDimOverlays();
                CloseSpecificPopup(myPopup);
            });
        }

        private PopupPlacement MapPlacement(PopupPosition position) => position switch
        {
            PopupPosition.Center        => PopupPlacement.Center,
            PopupPosition.TopLeft       => PopupPlacement.TopLeft,
            PopupPosition.TopCenter     => PopupPlacement.TopCenter,
            PopupPosition.TopRight      => PopupPlacement.TopRight,
            PopupPosition.LeftCenter    => PopupPlacement.LeftCenter,
            PopupPosition.RightCenter   => PopupPlacement.RightCenter,
            PopupPosition.BottomLeft    => PopupPlacement.BottomLeft,
            PopupPosition.BottomCenter  => PopupPlacement.BottomCenter,
            PopupPosition.BottomRight   => PopupPlacement.BottomRight,
            _ => LogUnknownPositionAndFallback(position),
        };

        private PopupPlacement LogUnknownPositionAndFallback(PopupPosition position)
        {
            _logger.LogWarning("Unknown PopupPosition value {Value} — defaulting to TopRight. Possible config corruption or forward-compat from newer build.", position);
            return PopupPlacement.TopRight;
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

                    // Symmetric defence; deferred via Dispatcher.Post for the same reason
                    // documented in ShowBreakReminderInternalAsync — the factory's
                    // Completed → popup.Close() handler fires myPopup.Closed synchronously
                    // before the inner Completed handler can resolve with `true`.
                    myPopup.Closed += (_, _) =>
                        Dispatcher.UIThread.Post(
                            () => tcs.TrySetResult(false),
                            DispatcherPriority.Background);

                    myPopup.Show();

                    // Fire the BreakWarning audio after the popup is shown.
                    // Shares Break.StartAudio config (same sound registered in BundledSoundCache).
                    FireChannelAudio(AudioChannel.BreakWarning, c => c.Break.StartAudio);

                    if (myPopup.PopupContent is BreakWarningPopup breakWarningPopup)
                    {
                        breakWarningPopup.StartCountdown(timeUntilBreak);
                        breakWarningPopup.Completed += (s, e) => tcs.TrySetResult(true);
                        // Countdown updates are driven by TimerService via
                        // UpdateBreakWarningCountdown() — no local timer needed.
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
                CloseSpecificPopup(myPopup);
                IsBreakWarningActive = false;
            });
        }

        public async Task<BreakAction> ShowBreakReminderAsync(TimeSpan duration, IProgress<double> progress, int consecutiveDelayCount = 0, int maxDelays = 0)
        {
            _isTestMode = false;
            return await ShowBreakReminderInternalAsync(duration, progress, consecutiveDelayCount, maxDelays);
        }

        public async Task<BreakAction> ShowBreakReminderTestAsync(TimeSpan duration, IProgress<double> progress)
        {
            _isTestMode = true;
            try
            {
                // Isolated test popup — does NOT modify _currentPopup or IsBreakActive.
                // Does show dim overlays to match real break behavior.
                var config = await _configurationService.LoadConfigurationAsync();
                var tcs = new TaskCompletionSource<BreakAction>();
                PopupWindow? testPopup = null;

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _logger.LogInformation("TEST MODE: Showing isolated break reminder popup for {Duration}", duration);

                        ShowDimOverlays(config.Break.OverlayOpacityPercent);

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
                    HideDimOverlays();
                    try { testPopup?.Close(); } catch { }
                });

                return result;
            }
            finally
            {
                _isTestMode = false;
            }
        }

        private async Task<BreakAction> ShowBreakReminderInternalAsync(TimeSpan duration, IProgress<double> progress, int consecutiveDelayCount = 0, int maxDelays = 0)
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

                    // Safety net: any close path that does NOT go through ActionSelected
                    // (X-button, app shutdown, etc.) must resolve the awaiting task as Skipped
                    // so the orchestrator can run SmartSessionResetAsync and clear
                    // _isBreakNotificationActive.
                    //
                    // CRITICAL (2026-04-28 regression): the resolution must be DEFERRED via
                    // Dispatcher.Post. The factory's ActionSelected handler (registered first)
                    // calls popup.Close() synchronously, which fires myPopup.Closed inside the
                    // multicast-delegate chain BEFORE the inner ActionSelected handler that
                    // resolves with the user's actual action runs. Direct resolution here
                    // races and wins, converting "Delay 5 Minutes" into "Skipped".
                    // Background priority defers until the synchronous ActionSelected chain
                    // completes — by then tcs is already resolved with the user's action and
                    // this Skipped TrySetResult is a no-op. For pure X-close paths (no
                    // ActionSelected fires), the deferred Skipped wins as intended.
                    myPopup.Closed += (_, _) =>
                    {
                        Dispatcher.UIThread.Post(
                            () => tcs.TrySetResult(BreakAction.Skipped),
                            DispatcherPriority.Background);
                    };

                    myPopup.Show();

                    // BL-002 M5: fire the Break START channel audio after the popup is shown.
                    FireChannelAudio(AudioChannel.BreakStart, c => c.Break.StartAudio);

                    if (myPopup.PopupContent is BreakPopup breakPopup)
                    {
                        breakPopup.SetConfiguration(
                            config.Break.RequireConfirmationAfterBreak,
                            config.Break.ResetTimersOnBreakConfirmation);

                        breakPopup.SetDelayChipState(consecutiveDelayCount, maxDelays);

                        breakPopup.ActionSelected += (s, action) => tcs.TrySetResult(action);

                        // Fire BreakEnd the moment the countdown elapses, NOT when
                        // the user dismisses the Complete dialog — the audio should
                        // accompany the natural end of the rest period.
                        breakPopup.CountdownCompleted += (_, _) =>
                            FireChannelAudio(AudioChannel.BreakEnd, c => c.Break.EndAudio);

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

        // Pool of reusable dim-overlay windows (one per screen). Windows are created once and
        // reused across cycles — Show()/Hide() rather than new/Close() — to avoid leaking
        // Avalonia.Controls.Window + their visual trees (and the backing NSWindow Mach ports)
        // on every break/eye-rest cycle. Confirmed leak: see docs/plan/008. The pool size
        // tracks the current screen count and is therefore bounded.

        /// <summary>
        /// Shows semi-transparent dark overlay windows on all screens for break dimming,
        /// reusing pooled windows to avoid per-cycle allocation.
        /// </summary>
        private void ShowDimOverlays(int opacityPercent = 50)
        {
            try
            {
                var app = Application.Current;
                if (app?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    return;

                var screens = desktop.MainWindow?.Screens.All;
                if (screens == null || screens.Count == 0)
                {
                    HideDimOverlays();
                    return;
                }

                var opacity = opacityPercent / 100.0;
                var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
                int screenCount = screens.Count; // stable snapshot for trim + show

                // Trim the pool if the screen count decreased since last time.
                while (_overlayWindows.Count > screenCount)
                {
                    var extra = _overlayWindows[_overlayWindows.Count - 1];
                    _overlayWindows.RemoveAt(_overlayWindows.Count - 1);
                    try { extra.Close(); } catch { /* best effort */ }
                }

                for (int i = 0; i < screenCount; i++)
                {
                    var screen = screens[i];
                    Window overlay;
                    if (i < _overlayWindows.Count)
                    {
                        overlay = _overlayWindows[i]; // reuse pooled window
                    }
                    else
                    {
                        overlay = CreateOverlayWindow();
                        _overlayWindows.Add(overlay);
                    }

                    overlay.Background = brush;
                    overlay.Width = screen.Bounds.Width / screen.Scaling;
                    overlay.Height = screen.Bounds.Height / screen.Scaling;
                    overlay.Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
                    overlay.Show();
                }

                _logger.LogInformation("Showed {Count} dim overlay(s) at {Opacity}% opacity",
                    screenCount, opacityPercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing dim overlays");
            }
        }

        /// <summary>
        /// Creates a single reusable overlay window. Called once per pool slot; the window is
        /// then reused (Show/Hide) for the lifetime of the service.
        /// </summary>
        private Window CreateOverlayWindow()
        {
            var overlay = new Window
            {
                SystemDecorations = SystemDecorations.None,
                Topmost = true,
                ShowInTaskbar = false,
                CanResize = false,
                ShowActivated = false,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            };

            // Click on overlay hides the dim on that screen (don't block the user). The window
            // stays in the pool (Hide, not Close) so it can be reused on the next cycle.
            overlay.PointerPressed += (s, e) =>
            {
                if (s is Window overlayWin)
                {
                    try { overlayWin.Hide(); } catch { /* best effort */ }
                    _logger.LogInformation("User clicked dim overlay - hidden for this screen");
                }
            };

            return overlay;
        }

        /// <summary>
        /// Hides all dim overlay windows, keeping them pooled for reuse.
        /// </summary>
        private void HideDimOverlays()
        {
            foreach (var overlay in _overlayWindows)
            {
                try
                {
                    overlay.Hide();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error hiding dim overlay");
                }
            }
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
