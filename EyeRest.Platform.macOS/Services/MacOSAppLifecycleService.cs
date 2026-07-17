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
        private IntPtr _memoryPressureSource = IntPtr.Zero;
        private System.Threading.Timer? _heartbeatTimer;
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
                    "Blink Twice EyeRest must run scheduled break reminders on time.");
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

            try
            {
                unsafe
                {
                    _memoryPressureSource = MacOSMemoryPressureInterop.Start(&OnMemoryPressureStatic);
                }
                if (_memoryPressureSource != IntPtr.Zero)
                    _logger.LogInformation("🧠 MEMORY PRESSURE: dispatch source registered (warn/critical)");
                else
                    _logger.LogWarning("🧠 MEMORY PRESSURE: failed to register dispatch source — proactive trim disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧠 MEMORY PRESSURE: failed to register dispatch source");
            }

            try
            {
                MacOSWatchdog.Install(_logger);
                MacOSWatchdog.WriteHeartbeat(); // initial beat before the first timer tick
                // Heartbeat runs on a threadpool timer — independent of the UI run loop and the
                // managed timer service, so it keeps beating right up until the OS suspends the
                // whole process, which is exactly when the external watchdog must take over.
                _heartbeatTimer = new System.Threading.Timer(
                    _ => MacOSWatchdog.WriteHeartbeat(), null,
                    TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                _logger.LogInformation("🐕 Heartbeat started (30s interval) + watchdog agent ensured");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🐕 Failed to start heartbeat / install watchdog");
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

                if (_memoryPressureSource != IntPtr.Zero)
                {
                    MacOSMemoryPressureInterop.Stop(_memoryPressureSource);
                    _memoryPressureSource = IntPtr.Zero;
                }

                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
                // Clean shutdown: remove the heartbeat so the watchdog treats a future
                // absence-of-process as a deliberate quit (it gates restart on the process
                // still existing), not a freeze.
                MacOSWatchdog.DeleteHeartbeat();
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
            // Refresh the heartbeat IMMEDIATELY on wake. The heartbeat timer is frozen during
            // sleep, so its mtime is stale until the next 30s tick; without this, the external
            // watchdog can mistake a just-resumed (healthy) app for a frozen one and kill+relaunch
            // it (docs/plan/009 review B2).
            MacOSWatchdog.WriteHeartbeat();
            _logger.LogWarning("🌅 SYSTEM WAKE: NSWorkspaceDidWakeNotification received — notifying subscribers");
            SystemAwoke?.Invoke();
        }

        private void OnWillSleep()
        {
            // Drop the heartbeat before sleeping so a stale file can't look like a freeze while
            // asleep — the watchdog skips when no heartbeat exists, and OnDidWake re-creates it on
            // resume (docs/plan/009 review B2).
            MacOSWatchdog.DeleteHeartbeat();
            _logger.LogInformation("🌙 SYSTEM SLEEP: NSWorkspaceWillSleepNotification received — notifying subscribers");
            SystemWillSleep?.Invoke();
        }

        // ----- Memory-pressure bridge (called from a background dispatch queue) -----

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static void OnMemoryPressureStatic(IntPtr context)
        {
            try { _current?.OnMemoryPressure(); }
            catch { /* swallow — must never throw across the dispatch boundary */ }
        }

        private void OnMemoryPressure()
        {
            // Runs on a background dispatch queue, not the UI thread (Serilog is thread-safe).
            // Snapshot the handle: StopAsync may zero it concurrently and we must never act on a
            // released source.
            var source = _memoryPressureSource;
            if (source == IntPtr.Zero) return;

            uint level = MacOSMemoryPressureInterop.GetLevel(source);
            bool critical = (level & MacOSMemoryPressureInterop.DISPATCH_MEMORYPRESSURE_CRITICAL) != 0;

            _logger.LogWarning("🧠 MEMORY PRESSURE: level={Level}{Critical} — proactively trimming (non-blocking GC)",
                level, critical ? " [CRITICAL]" : " [WARN]");

            // Be a good memory citizen so macOS is less likely to suspend us. Non-blocking and
            // non-compacting: this runs on a background dispatch queue and must never stop-the-world
            // the UI thread (that was the failure mode the original blocking GC.Collect risked).
            GC.Collect(critical ? 2 : 1, GCCollectionMode.Optimized, blocking: false, compacting: false);
        }
    }
}
