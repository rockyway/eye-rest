using System;
using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// IOKit framework bindings for display brightness control on macOS.
/// Provides access to IODisplaySetFloatParameter and IODisplayGetFloatParameter
/// for reading and setting screen brightness via the IOKit display service.
/// </summary>
internal static class IOKit
{
    private const string IOKitLib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    [DllImport(IOKitLib)]
    internal static extern uint IOServiceGetMatchingService(uint mainPort, IntPtr matching);

    [DllImport(IOKitLib)]
    internal static extern IntPtr IOServiceMatching(string name);

    [DllImport(IOKitLib)]
    internal static extern int IODisplaySetFloatParameter(uint service, uint options, string parameterName, float value);

    [DllImport(IOKitLib)]
    internal static extern int IODisplayGetFloatParameter(uint service, uint options, string parameterName, out float value);

    [DllImport(IOKitLib)]
    internal static extern int IOObjectRelease(uint obj);

    internal const uint kIOMasterPortDefault = 0;
    internal const string kIODisplayBrightnessKey = "brightness";
}
