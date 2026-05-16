using System;
using System.Threading.Tasks;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace EyeRest.Platform.Windows.Services
{
    /// <summary>
    /// Windows lifecycle service. Translates <c>SystemEvents.PowerModeChanged</c>
    /// (Suspend/Resume) into <see cref="IAppLifecycleService"/> events so the
    /// timer service can reset its cycle on resume from sleep, mirroring the
    /// macOS NSWorkspace did-wake / will-sleep behavior.
    ///
    /// <para>
    /// There is no Windows equivalent of macOS App Nap to opt out of, so the
    /// activity-token concept is a no-op here — Windows DispatcherTimers fire
    /// reliably as long as the process is awake.
    /// </para>
    /// </summary>
    public sealed class WindowsAppLifecycleService : IAppLifecycleService, IDisposable
    {
        private readonly ILogger<WindowsAppLifecycleService> _logger;
        private bool _started;
        private bool _disposed;

        public event Action? SystemAwoke;
        public event Action? SystemWillSleep;

        public WindowsAppLifecycleService(ILogger<WindowsAppLifecycleService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync()
        {
            if (_started) return Task.CompletedTask;
            _started = true;

            try
            {
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                _logger.LogInformation("🛡️ Windows lifecycle: subscribed to SystemEvents.PowerModeChanged");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🛡️ Windows lifecycle: failed to subscribe to PowerModeChanged — wake-from-sleep won't trigger session reset");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_started) return Task.CompletedTask;
            _started = false;

            try
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🛡️ Windows lifecycle: failed to unsubscribe from PowerModeChanged");
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    _logger.LogWarning("🌅 SYSTEM WAKE: PowerModes.Resume received — notifying subscribers");
                    SystemAwoke?.Invoke();
                    break;
                case PowerModes.Suspend:
                    _logger.LogInformation("🌙 SYSTEM SLEEP: PowerModes.Suspend received — notifying subscribers");
                    SystemWillSleep?.Invoke();
                    break;
            }
        }
    }
}
