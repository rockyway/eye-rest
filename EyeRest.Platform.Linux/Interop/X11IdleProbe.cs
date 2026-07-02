using System.Runtime.InteropServices;

namespace EyeRest.Platform.Linux.Interop
{
    /// <summary>
    /// Reads "time since last user input" from the X11 MIT-SCREEN-SAVER extension.
    /// Opens a fresh display connection per query — the presence service polls every
    /// 30 seconds, so connection cost is negligible and we avoid sharing an Xlib
    /// Display handle across threads (Xlib is not thread-safe without XInitThreads).
    ///
    /// If X11 or the extension is unavailable (e.g. Wayland-only session, headless),
    /// the probe permanently disables itself and reports zero idle ("user present")
    /// so the app keeps running with presence detection degraded rather than crashing.
    /// </summary>
    internal static class X11IdleProbe
    {
        private static volatile bool _unavailable;

        /// <summary>True once a probe attempt has failed permanently (no X11 / no extension).</summary>
        internal static bool IsUnavailable => _unavailable;

        internal static TimeSpan GetIdleTime()
        {
            if (_unavailable) return TimeSpan.Zero;

            var display = IntPtr.Zero;
            var info = IntPtr.Zero;
            try
            {
                display = X11Interop.XOpenDisplay(null);
                if (display == IntPtr.Zero)
                {
                    _unavailable = true;
                    return TimeSpan.Zero;
                }

                if (X11Interop.XScreenSaverQueryExtension(display, out _, out _) == 0)
                {
                    _unavailable = true;
                    return TimeSpan.Zero;
                }

                info = X11Interop.XScreenSaverAllocInfo();
                if (info == IntPtr.Zero) return TimeSpan.Zero;

                var root = X11Interop.XDefaultRootWindow(display);
                if (X11Interop.XScreenSaverQueryInfo(display, root, info) == 0)
                    return TimeSpan.Zero;

                var saverInfo = Marshal.PtrToStructure<X11Interop.XScreenSaverInfo>(info);
                return TimeSpan.FromMilliseconds(saverInfo.idle);
            }
            catch (DllNotFoundException)
            {
                // libX11 / libXss not installed — disable further attempts.
                _unavailable = true;
                return TimeSpan.Zero;
            }
            catch (EntryPointNotFoundException)
            {
                _unavailable = true;
                return TimeSpan.Zero;
            }
            finally
            {
                if (info != IntPtr.Zero) X11Interop.XFree(info);
                if (display != IntPtr.Zero) X11Interop.XCloseDisplay(display);
            }
        }
    }
}
