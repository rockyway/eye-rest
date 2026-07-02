using System.Runtime.InteropServices;

namespace EyeRest.Platform.Linux.Interop
{
    /// <summary>
    /// Minimal X11 / XScreenSaver-extension interop for idle-time detection.
    /// Binds against the runtime sonames (libX11.so.6 / libXss.so.1) so no -dev
    /// packages are required on the target machine.
    /// </summary>
    internal static class X11Interop
    {
        private const string LibX11 = "libX11.so.6";
        private const string LibXss = "libXss.so.1";

        [StructLayout(LayoutKind.Sequential)]
        internal struct XScreenSaverInfo
        {
            public IntPtr window;      // screen saver window
            public int state;          // ScreenSaver Off/On/Disabled
            public int kind;           // ScreenSaver Blanked/Internal/External
            public ulong til_or_since; // ms until saver activates / ms since it activated
            public ulong idle;         // milliseconds since the last user input
            public ulong eventMask;
        }

        [DllImport(LibX11)]
        internal static extern IntPtr XOpenDisplay(string? display);

        [DllImport(LibX11)]
        internal static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        internal static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibXss)]
        internal static extern int XScreenSaverQueryExtension(IntPtr display, out int eventBase, out int errorBase);

        [DllImport(LibXss)]
        internal static extern IntPtr XScreenSaverAllocInfo();

        [DllImport(LibXss)]
        internal static extern int XScreenSaverQueryInfo(IntPtr display, IntPtr drawable, IntPtr info);

        [DllImport(LibX11)]
        internal static extern int XFree(IntPtr data);
    }
}
