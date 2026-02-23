using System;
using System.Runtime.InteropServices;
using EyeRest.Platform.macOS.Interop;
using Microsoft.Extensions.Logging;

namespace EyeRest.Services
{
    /// <summary>
    /// macOS implementation of <see cref="ISystemTrayService"/> using NSStatusBar/NSStatusItem.
    /// On macOS, the "system tray" equivalent is a status bar item in the menu bar.
    /// Ported from TextAssistant.Platform.macOS.
    /// </summary>
    public class MacOSSystemTrayService : ISystemTrayService
    {
        private readonly ILogger<MacOSSystemTrayService> _logger;
        private readonly object _lock = new();

        private IntPtr _statusItem;
        private IntPtr _statusItemButton;
        private bool _isInitialized;
        private TrayIconState _currentState = TrayIconState.Active;

        // ObjC callback bridge fields
        private static IntPtr _menuCallbackClass;
        private static MacOSSystemTrayService? _instance;
        private IntPtr _menuTarget;

        // Must be static to prevent GC collection of the delegate used as a native callback
        private delegate void MenuItemActionDelegate(IntPtr self, IntPtr sel, IntPtr sender);
        private static MenuItemActionDelegate? _menuActionDelegate;

        // Menu item tag constants for identification
        private const int TagRestore = 1;
        private const int TagPause = 2;
        private const int TagResume = 3;
        private const int TagPauseForMeeting = 4;
        private const int TagTimerStatus = 5;
        private const int TagAnalytics = 6;
        private const int TagExit = 99;

        public MacOSSystemTrayService(ILogger<MacOSSystemTrayService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _instance = this;
        }

        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? PauseTimersRequested;
        public event EventHandler? ResumeTimersRequested;
        public event EventHandler? PauseForMeetingRequested;
        public event EventHandler? ShowTimerStatusRequested;
        public event EventHandler? ShowAnalyticsRequested;

        public void Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing macOS system tray");

                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    EnsureCallbackClass();

                    // Get system status bar: [NSStatusBar systemStatusBar]
                    var statusBarClass = ObjCRuntime.GetClass("NSStatusBar");
                    var selSystemStatusBar = ObjCRuntime.GetSelector("systemStatusBar");
                    var statusBar = ObjCRuntime.objc_msgSend_IntPtr(statusBarClass, selSystemStatusBar);

                    if (statusBar == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to get NSStatusBar.systemStatusBar");
                        return;
                    }

                    // Create status item with variable length: -1 = NSVariableStatusItemLength
                    var selStatusItemWithLength = ObjCRuntime.GetSelector("statusItemWithLength:");
                    _statusItem = ObjCRuntime.objc_msgSend_IntPtr_Long(statusBar, selStatusItemWithLength, -1);

                    if (_statusItem == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to create NSStatusItem");
                        return;
                    }

                    // Retain the status item to prevent deallocation
                    Foundation.Retain(_statusItem);

                    // Get button and set initial title
                    var selButton = ObjCRuntime.GetSelector("button");
                    _statusItemButton = ObjCRuntime.objc_msgSend_IntPtr(_statusItem, selButton);

                    if (_statusItemButton != IntPtr.Zero)
                    {
                        // Set a text title as placeholder (will be replaced with icon in Phase 6)
                        var nsTitle = Foundation.CreateNSString("Eye Rest");
                        var selSetTitle = ObjCRuntime.GetSelector("setTitle:");
                        ObjCRuntime.objc_msgSend_Void_IntPtr(_statusItemButton, selSetTitle, nsTitle);
                    }

                    // Build initial menu
                    RebuildMenu();

                    _isInitialized = true;
                    _logger.LogInformation("macOS system tray initialized successfully");
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize macOS system tray");
            }
        }

        public void ShowTrayIcon()
        {
            if (!_isInitialized || _statusItem == IntPtr.Zero) return;

            try
            {
                var selSetVisible = ObjCRuntime.GetSelector("setVisible:");
                ObjCRuntime.objc_msgSend_Void_Bool(_statusItem, selSetVisible, true);
                _logger.LogDebug("Tray icon shown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show tray icon");
            }
        }

        public void HideTrayIcon()
        {
            if (!_isInitialized || _statusItem == IntPtr.Zero) return;

            try
            {
                var selSetVisible = ObjCRuntime.GetSelector("setVisible:");
                ObjCRuntime.objc_msgSend_Void_Bool(_statusItem, selSetVisible, false);
                _logger.LogDebug("Tray icon hidden");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide tray icon");
            }
        }

        public void UpdateTrayIcon(TrayIconState state)
        {
            if (!_isInitialized) return;

            _currentState = state;
            _logger.LogDebug("Tray icon state updated to {State}", state);
        }

        public void ShowBalloonTip(string title, string text)
        {
            try
            {
                PostMacOSNotification(title, text, Guid.NewGuid().ToString());
                _logger.LogDebug("Showed balloon tip: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show balloon tip");
            }
        }

        public void UpdateTimerStatus(string status)
        {
            if (!_isInitialized || _statusItemButton == IntPtr.Zero) return;

            try
            {
                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    var nsTooltip = Foundation.CreateNSString(status);
                    var selSetToolTip = ObjCRuntime.GetSelector("setToolTip:");
                    ObjCRuntime.objc_msgSend_Void_IntPtr(_statusItemButton, selSetToolTip, nsTooltip);
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update timer status tooltip");
            }
        }

        public void UpdateTimerDetails(TimeSpan eyeRestRemaining, TimeSpan breakRemaining)
        {
            var status = $"Eye Rest: {FormatTimeSpan(eyeRestRemaining)} | Break: {FormatTimeSpan(breakRemaining)}";
            UpdateTimerStatus(status);
        }

        public void SetMeetingMode(bool isInMeeting, string meetingType = "")
        {
            if (isInMeeting)
            {
                _logger.LogDebug("Meeting mode enabled: {MeetingType}", meetingType);
                UpdateTrayIcon(TrayIconState.MeetingMode);
            }
            else
            {
                _logger.LogDebug("Meeting mode disabled");
                UpdateTrayIcon(TrayIconState.Active);
            }
        }

        #region Private Helpers

        private void EnsureCallbackClass()
        {
            if (_menuCallbackClass != IntPtr.Zero) return;

            var nsObjectClass = ObjCRuntime.GetClass("NSObject");
            _menuCallbackClass = ObjCRuntime.objc_allocateClassPair(nsObjectClass, "EyeRestMenuTarget", IntPtr.Zero);

            _menuActionDelegate = HandleMenuItemClick;
            var funcPtr = Marshal.GetFunctionPointerForDelegate(_menuActionDelegate);
            var actionSel = ObjCRuntime.GetSelector("menuItemClicked:");
            ObjCRuntime.class_addMethod(_menuCallbackClass, actionSel, funcPtr, "v@:@");

            ObjCRuntime.objc_registerClassPair(_menuCallbackClass);

            // Create singleton target instance
            _menuTarget = ObjCRuntime.objc_msgSend_IntPtr(
                ObjCRuntime.objc_msgSend_IntPtr(_menuCallbackClass, ObjCRuntime.Sel_Alloc),
                ObjCRuntime.Sel_Init);
        }

        private static void HandleMenuItemClick(IntPtr self, IntPtr sel, IntPtr sender)
        {
            try
            {
                var selTag = ObjCRuntime.GetSelector("tag");
                var tag = ObjCRuntime.objc_msgSend_IntPtr(sender, selTag);
                var tagValue = (int)(long)tag;

                _instance?.ProcessMenuItemClick(tagValue);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Menu callback error: {ex}");
            }
        }

        private void ProcessMenuItemClick(int tag)
        {
            switch (tag)
            {
                case TagRestore:
                    RestoreRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagPause:
                    PauseTimersRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagResume:
                    ResumeTimersRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagPauseForMeeting:
                    PauseForMeetingRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagTimerStatus:
                    ShowTimerStatusRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagAnalytics:
                    ShowAnalyticsRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case TagExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
                default:
                    _logger.LogWarning("Unknown menu item tag: {Tag}", tag);
                    break;
            }
        }

        private void RebuildMenu()
        {
            if (_statusItem == IntPtr.Zero) return;

            var pool = Foundation.CreateAutoreleasePool();
            try
            {
                var menuClass = ObjCRuntime.GetClass("NSMenu");
                var menu = ObjCRuntime.objc_msgSend_IntPtr(menuClass, ObjCRuntime.Sel_New);
                if (menu == IntPtr.Zero) return;

                var selAddItem = ObjCRuntime.GetSelector("addItem:");

                // Add menu items
                AddMenuItem(menu, selAddItem, "Show Eye Rest", TagRestore);
                AddSeparator(menu, selAddItem);
                AddMenuItem(menu, selAddItem, "Timer Status", TagTimerStatus);
                AddMenuItem(menu, selAddItem, "Analytics", TagAnalytics);
                AddSeparator(menu, selAddItem);
                AddMenuItem(menu, selAddItem, "Pause Timers", TagPause);
                AddMenuItem(menu, selAddItem, "Resume Timers", TagResume);
                AddMenuItem(menu, selAddItem, "Pause for Meeting", TagPauseForMeeting);
                AddSeparator(menu, selAddItem);
                AddMenuItem(menu, selAddItem, "Quit Eye Rest", TagExit);

                // Set menu on status item
                var selSetMenu = ObjCRuntime.GetSelector("setMenu:");
                ObjCRuntime.objc_msgSend_Void_IntPtr(_statusItem, selSetMenu, menu);
            }
            finally
            {
                Foundation.DrainAutoreleasePool(pool);
            }
        }

        private void AddMenuItem(IntPtr menu, IntPtr selAddItem, string title, int tag)
        {
            var menuItemClass = ObjCRuntime.GetClass("NSMenuItem");
            var nsTitle = Foundation.CreateNSString(title);
            var nsKeyEquiv = Foundation.CreateNSString("");
            var selInitWithTitle = ObjCRuntime.GetSelector("initWithTitle:action:keyEquivalent:");
            var actionSel = ObjCRuntime.GetSelector("menuItemClicked:");

            var nsMenuItem = ObjCRuntime.objc_msgSend_IntPtr(menuItemClass, ObjCRuntime.Sel_Alloc);
            nsMenuItem = ObjCRuntime.objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(
                nsMenuItem, selInitWithTitle, nsTitle, actionSel, nsKeyEquiv);

            if (nsMenuItem == IntPtr.Zero) return;

            // Set target
            var selSetTarget = ObjCRuntime.GetSelector("setTarget:");
            ObjCRuntime.objc_msgSend_Void_IntPtr(nsMenuItem, selSetTarget, _menuTarget);

            // Set tag for identification
            var selSetTag = ObjCRuntime.GetSelector("setTag:");
            ObjCRuntime.objc_msgSend_Void_IntPtr(nsMenuItem, selSetTag, (IntPtr)tag);

            ObjCRuntime.objc_msgSend_Void_IntPtr(menu, selAddItem, nsMenuItem);
        }

        private static void AddSeparator(IntPtr menu, IntPtr selAddItem)
        {
            var menuItemClass = ObjCRuntime.GetClass("NSMenuItem");
            var selSeparatorItem = ObjCRuntime.GetSelector("separatorItem");
            var separator = ObjCRuntime.objc_msgSend_IntPtr(menuItemClass, selSeparatorItem);
            if (separator != IntPtr.Zero)
            {
                ObjCRuntime.objc_msgSend_Void_IntPtr(menu, selAddItem, separator);
            }
        }

        private void PostMacOSNotification(string title, string body, string identifier)
        {
            try
            {
                var pool = Foundation.CreateAutoreleasePool();
                try
                {
                    var center = UserNotifications.GetCurrentNotificationCenter();
                    if (center == IntPtr.Zero)
                    {
                        _logger.LogWarning("UNUserNotificationCenter not available");
                        return;
                    }

                    UserNotifications.RequestAuthorization(center, UserNotifications.UNAuthorizationOptionAlertSoundBadge);

                    var content = UserNotifications.CreateNotificationContent(title, body);
                    if (content == IntPtr.Zero) return;

                    var trigger = UserNotifications.CreateTimeIntervalTrigger(1.0, false);
                    var request = UserNotifications.CreateNotificationRequest(identifier, content, trigger);
                    if (request == IntPtr.Zero) return;

                    UserNotifications.AddNotification(center, request);
                    _logger.LogDebug("Posted macOS notification: {Title}", title);
                }
                finally
                {
                    Foundation.DrainAutoreleasePool(pool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post macOS notification");
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
                : $"{ts.Minutes}m {ts.Seconds:D2}s";
        }

        #endregion
    }
}
