using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// CoreGraphics framework bindings for display information and idle time detection.
/// Provides access to CGEventSourceSecondsSinceLastEventType for user idle detection,
/// and CGDisplay for screen information.
/// Ported from TextAssistant.Platform.macOS.
/// </summary>
internal static class CoreGraphics
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    #region CGEventSource

    /// <summary>
    /// Returns the elapsed time in seconds since the last event of a given type.
    /// Used for idle time detection.
    /// </summary>
    /// <param name="stateID">The event state ID (use kCGEventSourceStateCombinedSessionState = 0).</param>
    /// <param name="eventType">The event type to query (use kCGAnyInputEventType = ~0 for any input).</param>
    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern double CGEventSourceSecondsSinceLastEventType(int stateID, int eventType);

    #endregion

    #region CGDisplay

    /// <summary>
    /// Returns the display ID of the main display.
    /// </summary>
    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint CGMainDisplayID();

    /// <summary>
    /// Returns the bounds of a display in the global display coordinate space.
    /// </summary>
    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern CGRect CGDisplayBounds(uint display);

    #endregion

    #region CoreFoundation Helpers

    /// <summary>
    /// Releases a CoreFoundation object.
    /// </summary>
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void CFRelease(IntPtr cf);

    #endregion

    #region Event Source State IDs

    /// <summary>Combined state of all event sources.</summary>
    internal const int kCGEventSourceStateCombinedSessionState = 0;

    /// <summary>State of HID system events (hardware input).</summary>
    internal const int kCGEventSourceStateHIDSystemState = 1;

    #endregion

    #region Event Types for Idle Detection

    /// <summary>Any input event type -- used with CGEventSourceSecondsSinceLastEventType for general idle detection.</summary>
    internal const int kCGAnyInputEventType = unchecked((int)0xFFFFFFFF);

    #endregion
}
