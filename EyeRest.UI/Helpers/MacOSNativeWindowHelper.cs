using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace EyeRest.UI.Helpers;

/// <summary>
/// Native macOS NSWindow interop for operations that Avalonia doesn't safely support.
/// Specifically: orderOut:/makeKeyAndOrderFront: for truly hiding/showing a window,
/// and setActivationPolicy: for toggling dock icon visibility.
///
/// Why this exists:
///   - Avalonia's Hide()/Show() cycle has known renderer restart bugs on macOS (#18148, #8281)
///   - Native orderOut: removes the window from the window server entirely
///   - Native makeKeyAndOrderFront: brings it back without going through Avalonia's broken state machine
///   - SetActivationPolicy toggles dock icon: Regular (0) = visible, Accessory (1) = hidden
/// </summary>
internal static class MacOSNativeWindowHelper
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr objc_getClass(string className);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Long(IntPtr receiver, IntPtr selector, long arg1);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_Long(IntPtr receiver, IntPtr selector, long arg1);

    private static readonly IntPtr Sel_OrderOut = sel_registerName("orderOut:");
    private static readonly IntPtr Sel_MakeKeyAndOrderFront = sel_registerName("makeKeyAndOrderFront:");
    private static readonly IntPtr Sel_SetActivationPolicy = sel_registerName("setActivationPolicy:");
    private static readonly IntPtr Sel_SharedApplication = sel_registerName("sharedApplication");

    /// <summary>
    /// Calls [NSWindow orderOut:nil] to remove the window from the window server's screen list.
    /// After this call, macOS does not track or relocate the window during display changes.
    /// </summary>
    internal static bool OrderOut(Window window, ILogger? logger = null)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                logger?.LogDebug("OrderOut: No platform handle available");
                return false;
            }

            objc_msgSend_Void_IntPtr(handle, Sel_OrderOut, IntPtr.Zero);
            logger?.LogDebug("OrderOut: NSWindow removed from screen list");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "OrderOut: Failed to call [NSWindow orderOut:]");
            return false;
        }
    }

    /// <summary>
    /// Calls [NSWindow makeKeyAndOrderFront:nil] to bring the window to front AND
    /// make it the key (focused) window.
    /// </summary>
    internal static bool MakeKeyAndOrderFront(Window window, ILogger? logger = null)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                logger?.LogDebug("MakeKeyAndOrderFront: No platform handle available");
                return false;
            }

            objc_msgSend_Void_IntPtr(handle, Sel_MakeKeyAndOrderFront, IntPtr.Zero);
            logger?.LogDebug("MakeKeyAndOrderFront: NSWindow brought to front with focus");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "MakeKeyAndOrderFront: Failed");
            return false;
        }
    }

    /// <summary>
    /// Sets the NSApplication activation policy.
    /// 0 = NSApplicationActivationPolicyRegular   — shows dock icon + app menu
    /// 1 = NSApplicationActivationPolicyAccessory — hides dock icon, menu bar only
    /// 2 = NSApplicationActivationPolicyProhibited — no UI activation at all
    /// </summary>
    internal static bool SetActivationPolicy(int policy, ILogger? logger = null)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            var nsApp = objc_msgSend(nsAppClass, Sel_SharedApplication);
            if (nsApp == IntPtr.Zero)
            {
                logger?.LogDebug("SetActivationPolicy: Could not get NSApplication");
                return false;
            }

            objc_msgSend_Void_Long(nsApp, Sel_SetActivationPolicy, policy);
            logger?.LogDebug("SetActivationPolicy: Set to {Policy}", policy);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "SetActivationPolicy: Failed to set policy {Policy}", policy);
            return false;
        }
    }

    /// <summary>
    /// Disables the macOS green zoom (maximize) button on an NSWindow.
    /// Calls [[window standardWindowButton:NSWindowZoomButton] setEnabled:NO].
    /// </summary>
    internal static bool DisableZoomButton(Window window, ILogger? logger = null)
    {
        if (!OperatingSystem.IsMacOS()) return false;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                logger?.LogDebug("DisableZoomButton: No platform handle available");
                return false;
            }

            // NSWindowZoomButton = 2
            var selStandardWindowButton = sel_registerName("standardWindowButton:");
            var zoomButton = objc_msgSend_IntPtr_Long(handle, selStandardWindowButton, 2);
            if (zoomButton == IntPtr.Zero)
            {
                logger?.LogDebug("DisableZoomButton: Could not get zoom button");
                return false;
            }

            var selSetEnabled = sel_registerName("setEnabled:");
            objc_msgSend_Void_Bool(zoomButton, selSetEnabled, false);
            logger?.LogDebug("DisableZoomButton: Zoom button disabled");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "DisableZoomButton: Failed");
            return false;
        }
    }
}
