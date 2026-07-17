using System;
using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop
{
    /// <summary>
    /// Interop for a libdispatch memory-pressure source
    /// (<c>DISPATCH_SOURCE_TYPE_MEMORYPRESSURE</c>). Lets the app react to the OS telling it
    /// memory is tight (warn / critical) by proactively trimming — so it stays a good memory
    /// citizen and is a less attractive target for the memory-pressure suspension that caused
    /// the "Not Responding" freeze (see docs/plan/008).
    ///
    /// <para>The handler runs on a background global dispatch queue, never the UI thread.</para>
    /// </summary>
    internal static unsafe class MacOSMemoryPressureInterop
    {
        private const string Lib = "/usr/lib/libSystem.dylib";

        // dlsym(RTLD_DEFAULT, ...) — search every loaded image. RTLD_DEFAULT is (void*)-2 on macOS.
        private static readonly IntPtr RTLD_DEFAULT = new(-2);

        // Pressure level bits (sys/event.h / dispatch/source.h).
        public const uint DISPATCH_MEMORYPRESSURE_WARN = 0x02;
        public const uint DISPATCH_MEMORYPRESSURE_CRITICAL = 0x04;
        private const nuint Mask = DISPATCH_MEMORYPRESSURE_WARN | DISPATCH_MEMORYPRESSURE_CRITICAL;

        [DllImport(Lib)]
        private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string symbol);

        [DllImport(Lib)]
        private static extern IntPtr dispatch_get_global_queue(nint identifier, nuint flags);

        [DllImport(Lib)]
        private static extern IntPtr dispatch_source_create(IntPtr type, nuint handle, nuint mask, IntPtr queue);

        [DllImport(Lib)]
        private static extern void dispatch_source_set_event_handler_f(IntPtr source, IntPtr handler);

        [DllImport(Lib)]
        private static extern nuint dispatch_source_get_data(IntPtr source);

        [DllImport(Lib)]
        private static extern void dispatch_resume(IntPtr obj);

        [DllImport(Lib)]
        private static extern void dispatch_source_cancel(IntPtr source);

        [DllImport(Lib)]
        private static extern void dispatch_release(IntPtr obj);

        /// <summary>
        /// Creates and starts a memory-pressure dispatch source for warn+critical levels.
        /// Returns the source handle, or <see cref="IntPtr.Zero"/> on failure.
        /// </summary>
        public static IntPtr Start(delegate* unmanaged[Cdecl]<IntPtr, void> handler)
        {
            var type = dlsym(RTLD_DEFAULT, "_dispatch_source_type_memorypressure");
            if (type == IntPtr.Zero)
                return IntPtr.Zero;

            // DISPATCH_QUEUE_PRIORITY_DEFAULT == 0.
            var queue = dispatch_get_global_queue(0, 0);
            var source = dispatch_source_create(type, 0, Mask, queue);
            if (source == IntPtr.Zero)
                return IntPtr.Zero;

            dispatch_source_set_event_handler_f(source, (IntPtr)handler);
            dispatch_resume(source); // sources are created suspended — must resume to fire.
            return source;
        }

        /// <summary>Returns the pressure-level bitmask that triggered the latest event.</summary>
        public static uint GetLevel(IntPtr source) =>
            source == IntPtr.Zero ? 0 : (uint)dispatch_source_get_data(source);

        /// <summary>Cancels and releases the source (stops further events). Safe on <see cref="IntPtr.Zero"/>.</summary>
        public static void Stop(IntPtr source)
        {
            if (source != IntPtr.Zero)
            {
                dispatch_source_cancel(source);
                // Balance the +1 reference returned by dispatch_source_create. The source is
                // resumed (never suspended) at this point, so releasing it is safe; libdispatch
                // keeps it alive internally until any in-flight handler drains.
                dispatch_release(source);
            }
        }
    }
}
