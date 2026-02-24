using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// AppKit framework bindings for macOS UI integration.
/// Provides access to NSStatusBar, NSStatusItem, NSMenu, NSMenuItem,
/// NSScreen, NSWorkspace, NSImage, NSSound functionality.
/// Ported from TextAssistant.Platform.macOS.
/// </summary>
internal static class AppKit
{
    #region Cached Classes

    private static readonly IntPtr Class_NSScreen = ObjCRuntime.objc_getClass("NSScreen");
    private static readonly IntPtr Class_NSWorkspace = ObjCRuntime.objc_getClass("NSWorkspace");
    private static readonly IntPtr Class_NSImage = ObjCRuntime.objc_getClass("NSImage");
    private static readonly IntPtr Class_NSSound = ObjCRuntime.objc_getClass("NSSound");

    #endregion

    #region Cached Selectors

    // NSScreen selectors
    private static readonly IntPtr Sel_MainScreen = ObjCRuntime.sel_registerName("mainScreen");
    private static readonly IntPtr Sel_Screens = ObjCRuntime.sel_registerName("screens");
    private static readonly IntPtr Sel_Frame = ObjCRuntime.sel_registerName("frame");
    private static readonly IntPtr Sel_VisibleFrame = ObjCRuntime.sel_registerName("visibleFrame");
    private static readonly IntPtr Sel_BackingScaleFactor = ObjCRuntime.sel_registerName("backingScaleFactor");

    // NSWorkspace selectors
    private static readonly IntPtr Sel_SharedWorkspace = ObjCRuntime.sel_registerName("sharedWorkspace");
    private static readonly IntPtr Sel_OpenURL = ObjCRuntime.sel_registerName("openURL:");

    // NSImage selectors
    private static readonly IntPtr Sel_InitWithContentsOfFile = ObjCRuntime.sel_registerName("initWithContentsOfFile:");
    private static readonly IntPtr Sel_InitWithSize = ObjCRuntime.sel_registerName("initWithSize:");
    private static readonly IntPtr Sel_SetSize = ObjCRuntime.sel_registerName("setSize:");
    private static readonly IntPtr Sel_SetTemplate = ObjCRuntime.sel_registerName("setTemplate:");

    // NSSound selectors
    private static readonly IntPtr Sel_SoundNamed = ObjCRuntime.sel_registerName("soundNamed:");
    private static readonly IntPtr Sel_Play = ObjCRuntime.sel_registerName("play");

    #endregion

    #region NSScreen

    /// <summary>
    /// Gets the main screen via [NSScreen mainScreen].
    /// </summary>
    internal static IntPtr GetMainScreen()
    {
        return ObjCRuntime.objc_msgSend_IntPtr(Class_NSScreen, Sel_MainScreen);
    }

    /// <summary>
    /// Gets all screens via [NSScreen screens].
    /// </summary>
    internal static IntPtr GetAllScreens()
    {
        return ObjCRuntime.objc_msgSend_IntPtr(Class_NSScreen, Sel_Screens);
    }

    /// <summary>
    /// Gets the frame rectangle of a screen via [screen frame].
    /// </summary>
    internal static CGRect GetScreenFrame(IntPtr screen)
    {
        if (screen == IntPtr.Zero) return CGRect.Zero;

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return ObjCRuntime.objc_msgSend_CGRect(screen, Sel_Frame);
        }

        ObjCRuntime.objc_msgSend_Stret(out var rect, screen, Sel_Frame);
        return rect;
    }

    /// <summary>
    /// Gets the visible frame of a screen via [screen visibleFrame].
    /// </summary>
    internal static CGRect GetScreenVisibleFrame(IntPtr screen)
    {
        if (screen == IntPtr.Zero) return CGRect.Zero;

        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return ObjCRuntime.objc_msgSend_CGRect(screen, Sel_VisibleFrame);
        }

        ObjCRuntime.objc_msgSend_Stret(out var rect, screen, Sel_VisibleFrame);
        return rect;
    }

    /// <summary>
    /// Gets the backing scale factor of a screen.
    /// Returns 2.0 for Retina displays, 1.0 for standard displays.
    /// </summary>
    internal static double GetBackingScaleFactor(IntPtr screen)
    {
        if (screen == IntPtr.Zero) return 1.0;
        return ObjCRuntime.objc_msgSend_Double(screen, Sel_BackingScaleFactor);
    }

    /// <summary>
    /// Gets the number of screens currently connected.
    /// </summary>
    internal static int GetScreenCount()
    {
        var screens = GetAllScreens();
        if (screens == IntPtr.Zero) return 1;
        return (int)Foundation.GetNSArrayCount(screens);
    }

    #endregion

    #region NSWorkspace

    /// <summary>
    /// Gets the shared workspace via [NSWorkspace sharedWorkspace].
    /// </summary>
    internal static IntPtr GetSharedWorkspace()
    {
        return ObjCRuntime.objc_msgSend_IntPtr(Class_NSWorkspace, Sel_SharedWorkspace);
    }

    /// <summary>
    /// Opens a URL in the default application.
    /// </summary>
    internal static bool OpenURL(IntPtr workspace, IntPtr url)
    {
        if (workspace == IntPtr.Zero || url == IntPtr.Zero) return false;
        return ObjCRuntime.objc_msgSend_Bool_IntPtr(workspace, Sel_OpenURL, url);
    }

    #endregion

    #region NSImage

    /// <summary>
    /// Creates an NSImage from a file path via [[NSImage alloc] initWithContentsOfFile:].
    /// </summary>
    internal static IntPtr CreateNSImageFromFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return IntPtr.Zero;

        var nsPath = Foundation.CreateNSString(path);
        if (nsPath == IntPtr.Zero) return IntPtr.Zero;

        var image = ObjCRuntime.objc_msgSend_IntPtr(Class_NSImage, ObjCRuntime.Sel_Alloc);
        return ObjCRuntime.objc_msgSend_IntPtr_IntPtr(image, Sel_InitWithContentsOfFile, nsPath);
    }

    /// <summary>
    /// Creates an empty NSImage with a specified size.
    /// </summary>
    internal static IntPtr CreateNSImageWithSize(double width, double height)
    {
        var image = ObjCRuntime.objc_msgSend_IntPtr(Class_NSImage, ObjCRuntime.Sel_Alloc);
        return ObjCRuntime.objc_msgSend_IntPtr_Double_Double(image, Sel_InitWithSize, width, height);
    }

    /// <summary>
    /// Sets the size of an NSImage via [image setSize:].
    /// For menu bar icons, use 18x18 so macOS renders them at the correct size.
    /// </summary>
    internal static void SetNSImageSize(IntPtr image, double width, double height)
    {
        if (image == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void_Double_Double(image, Sel_SetSize, width, height);
    }

    /// <summary>
    /// Marks an NSImage as a template image via [image setTemplate:YES].
    /// Template images automatically adapt to light/dark menu bar.
    /// </summary>
    internal static void SetNSImageTemplate(IntPtr image, bool isTemplate)
    {
        if (image == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void_Bool(image, Sel_SetTemplate, isTemplate);
    }

    #endregion

    #region NSSound

    /// <summary>
    /// Plays a named system sound via [[NSSound soundNamed:name] play].
    /// </summary>
    /// <param name="name">The system sound name (e.g., "Basso", "Blow", "Glass", "Tink").</param>
    /// <returns>True if sound was found and play was initiated.</returns>
    internal static bool PlaySystemSound(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var nsName = Foundation.CreateNSString(name);
        if (nsName == IntPtr.Zero) return false;

        var sound = ObjCRuntime.objc_msgSend_IntPtr_IntPtr(Class_NSSound, Sel_SoundNamed, nsName);
        if (sound == IntPtr.Zero) return false;

        ObjCRuntime.objc_msgSend_Bool(sound, Sel_Play);
        return true;
    }

    /// <summary>
    /// Plays the system beep via NSBeep().
    /// </summary>
    [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void NSBeep();

    #endregion
}
