namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// User Notifications framework bindings for showing native macOS notifications.
/// Uses the UNUserNotificationCenter API through Objective-C runtime calls.
/// Ported from TextAssistant.Platform.macOS.
/// </summary>
internal static class UserNotifications
{
    #region Cached Classes

    private static readonly IntPtr Class_UNUserNotificationCenter =
        ObjCRuntime.objc_getClass("UNUserNotificationCenter");

    private static readonly IntPtr Class_UNMutableNotificationContent =
        ObjCRuntime.objc_getClass("UNMutableNotificationContent");

    private static readonly IntPtr Class_UNNotificationRequest =
        ObjCRuntime.objc_getClass("UNNotificationRequest");

    private static readonly IntPtr Class_UNTimeIntervalNotificationTrigger =
        ObjCRuntime.objc_getClass("UNTimeIntervalNotificationTrigger");

    #endregion

    #region Cached Selectors

    private static readonly IntPtr Sel_CurrentNotificationCenter =
        ObjCRuntime.sel_registerName("currentNotificationCenter");

    private static readonly IntPtr Sel_RequestAuthorizationWithOptions =
        ObjCRuntime.sel_registerName("requestAuthorizationWithOptions:completionHandler:");

    private static readonly IntPtr Sel_AddNotificationRequest =
        ObjCRuntime.sel_registerName("addNotificationRequest:withCompletionHandler:");

    private static readonly IntPtr Sel_RemoveAllDeliveredNotifications =
        ObjCRuntime.sel_registerName("removeAllDeliveredNotifications");

    private static readonly IntPtr Sel_SetTitle =
        ObjCRuntime.sel_registerName("setTitle:");

    private static readonly IntPtr Sel_SetBody =
        ObjCRuntime.sel_registerName("setBody:");

    private static readonly IntPtr Sel_SetSubtitle =
        ObjCRuntime.sel_registerName("setSubtitle:");

    private static readonly IntPtr Sel_SetSound =
        ObjCRuntime.sel_registerName("setSound:");

    private static readonly IntPtr Sel_DefaultSound =
        ObjCRuntime.sel_registerName("defaultSound");

    private static readonly IntPtr Sel_RequestWithIdentifierContentTrigger =
        ObjCRuntime.sel_registerName("requestWithIdentifier:content:trigger:");

    private static readonly IntPtr Sel_TriggerWithTimeIntervalRepeats =
        ObjCRuntime.sel_registerName("triggerWithTimeInterval:repeats:");

    #endregion

    #region Authorization Options (UNAuthorizationOptions)

    internal const ulong UNAuthorizationOptionAlert = 1 << 0;
    internal const ulong UNAuthorizationOptionSound = 1 << 1;
    internal const ulong UNAuthorizationOptionBadge = 1 << 2;
    internal const ulong UNAuthorizationOptionAlertSoundBadge =
        UNAuthorizationOptionAlert | UNAuthorizationOptionSound | UNAuthorizationOptionBadge;

    #endregion

    #region UNUserNotificationCenter

    /// <summary>
    /// Gets the current notification center.
    /// </summary>
    internal static IntPtr GetCurrentNotificationCenter()
    {
        return ObjCRuntime.objc_msgSend_IntPtr(Class_UNUserNotificationCenter, Sel_CurrentNotificationCenter);
    }

    /// <summary>
    /// Requests authorization for notifications (fire-and-forget, nil completion handler).
    /// </summary>
    internal static void RequestAuthorization(IntPtr center, ulong options)
    {
        if (center == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void_ULong_IntPtr(
            center,
            Sel_RequestAuthorizationWithOptions,
            options,
            IntPtr.Zero);
    }

    /// <summary>
    /// Adds a notification request to the notification center.
    /// </summary>
    internal static void AddNotification(IntPtr center, IntPtr request)
    {
        if (center == IntPtr.Zero || request == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void_IntPtr_IntPtr(
            center,
            Sel_AddNotificationRequest,
            request,
            IntPtr.Zero);
    }

    /// <summary>
    /// Removes all delivered notifications.
    /// </summary>
    internal static void RemoveAllDeliveredNotifications(IntPtr center)
    {
        if (center == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void(center, Sel_RemoveAllDeliveredNotifications);
    }

    #endregion

    #region Notification Content & Request Helpers

    /// <summary>
    /// Creates a UNMutableNotificationContent with the given title, body, and optional subtitle.
    /// Automatically sets the default notification sound.
    /// </summary>
    internal static IntPtr CreateNotificationContent(string title, string body, string? subtitle = null)
    {
        if (Class_UNMutableNotificationContent == IntPtr.Zero) return IntPtr.Zero;

        var content = ObjCRuntime.objc_msgSend_IntPtr(Class_UNMutableNotificationContent, ObjCRuntime.Sel_New);
        if (content == IntPtr.Zero) return IntPtr.Zero;

        // Set title
        var nsTitle = Foundation.CreateNSString(title);
        if (nsTitle != IntPtr.Zero)
            ObjCRuntime.objc_msgSend_Void_IntPtr(content, Sel_SetTitle, nsTitle);

        // Set body
        var nsBody = Foundation.CreateNSString(body);
        if (nsBody != IntPtr.Zero)
            ObjCRuntime.objc_msgSend_Void_IntPtr(content, Sel_SetBody, nsBody);

        // Set subtitle (optional)
        if (!string.IsNullOrEmpty(subtitle))
        {
            var nsSubtitle = Foundation.CreateNSString(subtitle);
            if (nsSubtitle != IntPtr.Zero)
                ObjCRuntime.objc_msgSend_Void_IntPtr(content, Sel_SetSubtitle, nsSubtitle);
        }

        // Set default sound
        var soundClass = ObjCRuntime.objc_getClass("UNNotificationSound");
        if (soundClass != IntPtr.Zero)
        {
            var defaultSound = ObjCRuntime.objc_msgSend_IntPtr(soundClass, Sel_DefaultSound);
            if (defaultSound != IntPtr.Zero)
                ObjCRuntime.objc_msgSend_Void_IntPtr(content, Sel_SetSound, defaultSound);
        }

        return content;
    }

    /// <summary>
    /// Creates a UNNotificationRequest with an identifier, content, and optional trigger.
    /// </summary>
    internal static IntPtr CreateNotificationRequest(string identifier, IntPtr content, IntPtr trigger)
    {
        if (Class_UNNotificationRequest == IntPtr.Zero || content == IntPtr.Zero) return IntPtr.Zero;

        var nsIdentifier = Foundation.CreateNSString(identifier);
        if (nsIdentifier == IntPtr.Zero) return IntPtr.Zero;

        return ObjCRuntime.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(
            Class_UNNotificationRequest,
            Sel_RequestWithIdentifierContentTrigger,
            nsIdentifier,
            content,
            trigger);
    }

    /// <summary>
    /// Creates a time-interval notification trigger.
    /// </summary>
    internal static IntPtr CreateTimeIntervalTrigger(double timeInterval, bool repeats)
    {
        if (Class_UNTimeIntervalNotificationTrigger == IntPtr.Zero) return IntPtr.Zero;

        return ObjCRuntime.objc_msgSend_IntPtr_Double_Bool(
            Class_UNTimeIntervalNotificationTrigger,
            Sel_TriggerWithTimeIntervalRepeats,
            timeInterval,
            repeats);
    }

    #endregion
}
