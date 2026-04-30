using System;
using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop
{
    /// <summary>
    /// Interop helpers for macOS app lifecycle integration:
    /// <list type="bullet">
    ///   <item>NSProcessInfo activity tokens (App Nap opt-out)</item>
    ///   <item>NSWorkspace.notificationCenter observer registration</item>
    /// </list>
    /// Builds a runtime-registered ObjC class <c>EyeRestLifecycleObserver</c>
    /// whose did-wake / will-sleep imps are C# function pointers, so that
    /// <c>NSNotificationCenter</c> can target it like any AppKit object.
    /// </summary>
    internal static unsafe class MacOSAppLifecycleInterop
    {
        // NSActivityOptions bits (NSProcessInfo.h)
        private const ulong NSActivityIdleSystemSleepDisabled = 1UL << 20;
        private const ulong NSActivityUserInitiated = 0x00FFFFFFUL;
        private const ulong NSActivityLatencyCritical = 0xFF00000000UL;

        // We want timers to fire reliably (latency critical) but still allow
        // the laptop lid to put the system to sleep — that's the user's call.
        private const ulong ActivityOptions =
            (NSActivityUserInitiated & ~NSActivityIdleSystemSleepDisabled) | NSActivityLatencyCritical;

        // Notification name strings. These are the canonical NSString values
        // that AppKit publishes globally; constructing an NSString from the
        // literal matches what NSNotificationCenter dispatches against.
        private const string NSWorkspaceDidWakeNotification = "NSWorkspaceDidWakeNotification";
        private const string NSWorkspaceWillSleepNotification = "NSWorkspaceWillSleepNotification";

        private static readonly IntPtr Class_NSProcessInfo = ObjCRuntime.objc_getClass("NSProcessInfo");
        private static readonly IntPtr Sel_ProcessInfo = ObjCRuntime.sel_registerName("processInfo");
        private static readonly IntPtr Sel_BeginActivity = ObjCRuntime.sel_registerName("beginActivityWithOptions:reason:");
        private static readonly IntPtr Sel_EndActivity = ObjCRuntime.sel_registerName("endActivity:");

        private static readonly IntPtr Sel_NotificationCenter = ObjCRuntime.sel_registerName("notificationCenter");
        private static readonly IntPtr Sel_AddObserver = ObjCRuntime.sel_registerName("addObserver:selector:name:object:");
        private static readonly IntPtr Sel_RemoveObserver = ObjCRuntime.sel_registerName("removeObserver:");

        private static readonly IntPtr Sel_DidWake = ObjCRuntime.sel_registerName("eyeRestDidWake:");
        private static readonly IntPtr Sel_WillSleep = ObjCRuntime.sel_registerName("eyeRestWillSleep:");

        // The runtime-registered observer class. Created lazily on first use.
        private static IntPtr _observerClass = IntPtr.Zero;
        private static readonly object _classLock = new object();

        // ── App Nap opt-out ─────────────────────────────────────────────

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_BeginActivity(
            IntPtr receiver, IntPtr selector, ulong options, IntPtr reason);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_EndActivity(IntPtr receiver, IntPtr selector, IntPtr token);

        public static IntPtr BeginActivity(string reason)
        {
            var processInfo = ObjCRuntime.objc_msgSend_IntPtr(Class_NSProcessInfo, Sel_ProcessInfo);
            if (processInfo == IntPtr.Zero)
                throw new InvalidOperationException("NSProcessInfo.processInfo returned nil");

            var nsReason = Foundation.CreateNSString(reason);
            var token = objc_msgSend_BeginActivity(processInfo, Sel_BeginActivity, ActivityOptions, nsReason);
            if (token == IntPtr.Zero)
                throw new InvalidOperationException("NSProcessInfo beginActivity returned nil");

            // Retain the token so it survives autorelease pools — endActivity is
            // mandatory and we hold it for the process lifetime.
            return ObjCRuntime.objc_msgSend_IntPtr(token, ObjCRuntime.Sel_Retain);
        }

        public static void EndActivity(IntPtr token)
        {
            if (token == IntPtr.Zero) return;
            var processInfo = ObjCRuntime.objc_msgSend_IntPtr(Class_NSProcessInfo, Sel_ProcessInfo);
            if (processInfo != IntPtr.Zero)
                objc_msgSend_EndActivity(processInfo, Sel_EndActivity, token);
            ObjCRuntime.objc_msgSend_IntPtr(token, ObjCRuntime.Sel_Release);
        }

        // ── NSWorkspace observer ───────────────────────────────────────

        public static IntPtr RegisterWorkspaceObserver(
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> onDidWake,
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> onWillSleep)
        {
            var observerClass = EnsureObserverClass(onDidWake, onWillSleep);

            // Allocate an instance: [[EyeRestLifecycleObserver alloc] init]
            var alloc = ObjCRuntime.objc_msgSend_IntPtr(observerClass, ObjCRuntime.Sel_Alloc);
            var observer = ObjCRuntime.objc_msgSend_IntPtr(alloc, ObjCRuntime.Sel_Init);
            if (observer == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate EyeRestLifecycleObserver");

            // Get NSWorkspace.sharedWorkspace.notificationCenter
            var workspace = AppKit.GetSharedWorkspace();
            var center = ObjCRuntime.objc_msgSend_IntPtr(workspace, Sel_NotificationCenter);
            if (center == IntPtr.Zero)
                throw new InvalidOperationException("NSWorkspace.notificationCenter returned nil");

            var nsDidWake = Foundation.CreateNSString(NSWorkspaceDidWakeNotification);
            var nsWillSleep = Foundation.CreateNSString(NSWorkspaceWillSleepNotification);

            AddObserver(center, observer, Sel_DidWake, nsDidWake);
            AddObserver(center, observer, Sel_WillSleep, nsWillSleep);

            return observer;
        }

        public static void UnregisterWorkspaceObserver(IntPtr observer)
        {
            if (observer == IntPtr.Zero) return;
            var workspace = AppKit.GetSharedWorkspace();
            var center = ObjCRuntime.objc_msgSend_IntPtr(workspace, Sel_NotificationCenter);
            if (center != IntPtr.Zero)
                ObjCRuntime.objc_msgSend_Void_IntPtr(center, Sel_RemoveObserver, observer);
            ObjCRuntime.objc_msgSend_IntPtr(observer, ObjCRuntime.Sel_Release);
        }

        // ── Runtime ObjC class registration ────────────────────────────

        private static IntPtr EnsureObserverClass(
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> onDidWake,
            delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> onWillSleep)
        {
            lock (_classLock)
            {
                if (_observerClass != IntPtr.Zero) return _observerClass;

                // Check if class was already registered in a prior process invocation
                // — shouldn't happen in practice, but objc_allocateClassPair returns nil
                // if the name is taken.
                var existing = ObjCRuntime.objc_getClass("EyeRestLifecycleObserver");
                if (existing != IntPtr.Zero)
                {
                    _observerClass = existing;
                    return _observerClass;
                }

                var nsObject = ObjCRuntime.objc_getClass("NSObject");
                var cls = ObjCRuntime.objc_allocateClassPair(nsObject, "EyeRestLifecycleObserver", IntPtr.Zero);
                if (cls == IntPtr.Zero)
                    throw new InvalidOperationException("objc_allocateClassPair failed for EyeRestLifecycleObserver");

                // Type encoding "v@:@" means: void return, takes (id self, SEL _cmd, id arg).
                if (!ObjCRuntime.class_addMethod(cls, Sel_DidWake, (IntPtr)onDidWake, "v@:@"))
                    throw new InvalidOperationException("class_addMethod failed for eyeRestDidWake:");
                if (!ObjCRuntime.class_addMethod(cls, Sel_WillSleep, (IntPtr)onWillSleep, "v@:@"))
                    throw new InvalidOperationException("class_addMethod failed for eyeRestWillSleep:");

                ObjCRuntime.objc_registerClassPair(cls);
                _observerClass = cls;
                return _observerClass;
            }
        }

        private static void AddObserver(IntPtr center, IntPtr observer, IntPtr selector, IntPtr notificationName)
        {
            // [center addObserver:observer selector:selector name:notificationName object:nil]
            objc_msgSend_AddObserver(center, Sel_AddObserver, observer, selector, notificationName, IntPtr.Zero);
        }

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_AddObserver(
            IntPtr receiver, IntPtr selector,
            IntPtr observer, IntPtr action, IntPtr name, IntPtr obj);
    }
}
