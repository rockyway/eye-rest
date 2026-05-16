using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EyeRest.Platform.macOS.Interop;
using EyeRest.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace EyeRest.Platform.macOS.Services
{
    /// <summary>
    /// macOS lifecycle service. Two responsibilities:
    /// <list type="number">
    ///   <item>Opt the process out of <b>App Nap</b> so DispatcherTimers
    ///         keep firing on schedule under memory pressure or while the
    ///         app has no visible window. Without this, macOS will throttle
    ///         the process aggressively (timers stop, app reports
    ///         "Not Responding") whenever it decides we're "background work."</item>
    ///   <item>Observe <c>NSWorkspaceDidWakeNotification</c> and
    ///         <c>NSWorkspaceWillSleepNotification</c> so the timer service
    ///         can reset to a clean cycle on resume rather than waiting for
    ///         a stale tick to arrive and trip the in-tick clock-jump
    ///         detection (which only fires when the timer eventually does).</item>
    /// </list>
    ///
    /// <para>
    /// The observer is implemented by registering a runtime ObjC class
    /// <c>EyeRestLifecycleObserver</c> whose two action selectors call back
    /// into <see cref="OnDidWake"/> / <see cref="OnWillSleep"/> via
    /// <see cref="UnmanagedCallersOnlyAttribute"/>. We forward the
    /// notification through a static instance pointer because ObjC method
    /// imps don't carry managed context.
    /// </para>
    /// </summary>
    public sealed class MacOSAppLifecycleService : IAppLifecycleService, IDisposable
    {
        private readonly ILogger<MacOSAppLifecycleService> _logger;

        private IntPtr _activityToken = IntPtr.Zero;
        private IntPtr _observerInstance = IntPtr.Zero;
        private bool _started;
        private bool _disposed;

        // Static instance pointer so the unmanaged ObjC method imps can
        // route notifications back to the live service.
        private static MacOSAppLifecycleService? _current;

        public event Action? SystemAwoke;
        public event Action? SystemWillSleep;

        public MacOSAppLifecycleService(ILogger<MacOSAppLifecycleService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync()
        {
            if (_started) return Task.CompletedTask;
            _started = true;
            _current = this;

            try
            {
                _activityToken = MacOSAppLifecycleInterop.BeginActivity(
                    "Eye-Rest must run scheduled break reminders on time.");
                _logger.LogInformation("🛡️ APP NAP OPT-OUT: NSProcessInfo activity token acquired (token=0x{Token:X})",
                    _activityToken.ToInt64());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🛡️ APP NAP OPT-OUT: Failed to acquire activity token — timers may be throttled under memory pressure");
            }

            try
            {
                unsafe
                {
                    _observerInstance = MacOSAppLifecycleInterop.RegisterWorkspaceObserver(
                        onDidWake: &OnDidWakeStatic,
                        onWillSleep: &OnWillSleepStatic);
                }
                _logger.LogInformation("🛡️ NSWorkspace observers registered for did-wake / will-sleep");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🛡️ Failed to register NSWorkspace observers — wake-from-sleep won't trigger session reset");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_started) return Task.CompletedTask;
            _started = false;

            try
            {
                if (_observerInstance != IntPtr.Zero)
                {
                    MacOSAppLifecycleInterop.UnregisterWorkspaceObserver(_observerInstance);
                    _observerInstance = IntPtr.Zero;
                }

                if (_activityToken != IntPtr.Zero)
                {
                    MacOSAppLifecycleInterop.EndActivity(_activityToken);
                    _activityToken = IntPtr.Zero;
                    _logger.LogInformation("🛡️ APP NAP OPT-OUT: Activity token released");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🛡️ Failed to release lifecycle resources cleanly");
            }

            if (_current == this) _current = null;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
        }

        // ----- Unmanaged callback bridges (called from ObjC runtime) -----
        // ObjC instance method signature: void method(id self, SEL _cmd, id arg)

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void OnDidWakeStatic(IntPtr self, IntPtr selector, IntPtr notification)
        {
            try { _current?.OnDidWake(); }
            catch { /* swallow — must never throw across the ObjC boundary */ }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void OnWillSleepStatic(IntPtr self, IntPtr selector, IntPtr notification)
        {
            try { _current?.OnWillSleep(); }
            catch { /* swallow — must never throw across the ObjC boundary */ }
        }

        private void OnDidWake()
        {
            _logger.LogWarning("🌅 SYSTEM WAKE: NSWorkspaceDidWakeNotification received — notifying subscribers");
            SystemAwoke?.Invoke();
        }

        private void OnWillSleep()
        {
            _logger.LogInformation("🌙 SYSTEM SLEEP: NSWorkspaceWillSleepNotification received — notifying subscribers");
            SystemWillSleep?.Invoke();
        }
    }
}
