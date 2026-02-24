using System;
using System.Threading.Tasks;
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
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    _logger.LogInformation("Showing eye rest warning popup");
                    IsEyeRestWarningActive = true;
                    var popup = (PopupWindow)_popupWindowFactory.CreateEyeRestWarningPopup();
                    _currentPopup = popup;
                    popup.PositionOnScreen();
                    popup.Show();
                    await Task.Delay(timeUntilBreak);
                    CloseCurrentPopup();
                    IsEyeRestWarningActive = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing eye rest warning");
                    IsEyeRestWarningActive = false;
                }
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
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    _logger.LogInformation("Showing eye rest reminder popup for {Duration}", duration);
                    var popup = (PopupWindow)_popupWindowFactory.CreateEyeRestPopup();
                    _currentPopup = popup;
                    popup.PositionOnScreen();
                    popup.Show();
                    await Task.Delay(duration);
                    CloseCurrentPopup();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing eye rest reminder");
                }
            });
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
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    _logger.LogInformation("Showing break warning popup");
                    IsBreakWarningActive = true;
                    var popup = (PopupWindow)_popupWindowFactory.CreateBreakWarningPopup();
                    _currentPopup = popup;
                    popup.PositionOnScreen();
                    popup.Show();
                    await Task.Delay(timeUntilBreak);
                    CloseCurrentPopup();
                    IsBreakWarningActive = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing break warning");
                    IsBreakWarningActive = false;
                }
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
            return await ShowBreakReminderInternalAsync(duration, progress);
        }

        private async Task<BreakAction> ShowBreakReminderInternalAsync(TimeSpan duration, IProgress<double> progress)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    _logger.LogInformation("Showing break reminder popup for {Duration}", duration);
                    IsBreakActive = true;
                    var popup = (PopupWindow)_popupWindowFactory.CreateBreakPopup();
                    _currentPopup = popup;
                    popup.PositionOnScreen();
                    popup.Show();

                    var elapsed = TimeSpan.Zero;
                    var interval = TimeSpan.FromMilliseconds(100);
                    while (elapsed < duration)
                    {
                        await Task.Delay(interval);
                        elapsed += interval;
                        progress.Report(elapsed.TotalMilliseconds / duration.TotalMilliseconds);
                    }

                    CloseCurrentPopup();
                    IsBreakActive = false;
                    return BreakAction.Completed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error showing break reminder");
                    IsBreakActive = false;
                    return BreakAction.Completed;
                }
            });
        }

        public async Task HideAllNotifications()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CloseCurrentPopup();
                IsEyeRestWarningActive = false;
                IsBreakWarningActive = false;
                IsBreakActive = false;
            });
        }

        public void UpdateEyeRestWarningCountdown(TimeSpan remaining)
        {
            _logger.LogDebug("Eye rest warning countdown: {Remaining}", remaining);
        }

        public void UpdateBreakWarningCountdown(TimeSpan remaining)
        {
            _logger.LogDebug("Break warning countdown: {Remaining}", remaining);
        }

        public void StartEyeRestWarningCountdown(TimeSpan duration)
        {
            _logger.LogDebug("Starting eye rest warning countdown: {Duration}", duration);
        }

        public void StartBreakWarningCountdown(TimeSpan duration)
        {
            _logger.LogDebug("Starting break warning countdown: {Duration}", duration);
        }

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
    }
}
