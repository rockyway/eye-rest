using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// Foundation framework helpers for common Objective-C types.
/// Provides managed wrappers for NSString, NSArray, NSDictionary,
/// NSAutoreleasePool, NSURL, NSData, and NSObject memory management.
/// Ported from TextAssistant.Platform.macOS.
/// </summary>
internal static class Foundation
{
    #region NSString

    /// <summary>
    /// Creates an NSString from a C# string using [NSString stringWithUTF8String:].
    /// WARNING: Returns an AUTORELEASED object. Do NOT store in static fields.
    /// Use <see cref="CreateRetainedNSString"/> for long-lived string constants.
    /// </summary>
    internal static IntPtr CreateNSString(string? str)
    {
        if (str is null) return IntPtr.Zero;

        return ObjCRuntime.objc_msgSend_IntPtr_String(
            ObjCRuntime.Class_NSString,
            ObjCRuntime.Sel_StringWithUTF8String,
            str);
    }

    /// <summary>
    /// Creates a RETAINED NSString from a C# string. Safe for storage in static fields.
    /// Uses [[NSString alloc] initWithUTF8String:] which returns a +1 retained object.
    /// </summary>
    internal static IntPtr CreateRetainedNSString(string? str)
    {
        if (str is null) return IntPtr.Zero;

        var allocated = ObjCRuntime.objc_msgSend_IntPtr(
            ObjCRuntime.Class_NSString,
            ObjCRuntime.Sel_Alloc);

        return ObjCRuntime.objc_msgSend_IntPtr_String(
            allocated,
            Sel_InitWithUTF8String,
            str);
    }

    private static readonly IntPtr Sel_InitWithUTF8String = ObjCRuntime.sel_registerName("initWithUTF8String:");

    /// <summary>
    /// Extracts a C# string from an NSString using [nsString UTF8String].
    /// </summary>
    internal static string? GetStringFromNSString(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero) return null;

        var utf8Ptr = ObjCRuntime.objc_msgSend_IntPtr(nsString, ObjCRuntime.Sel_UTF8String);
        if (utf8Ptr == IntPtr.Zero) return null;

        return Marshal.PtrToStringUTF8(utf8Ptr);
    }

    #endregion

    #region NSArray

    /// <summary>
    /// Gets the count of objects in an NSArray.
    /// </summary>
    internal static long GetNSArrayCount(IntPtr nsArray)
    {
        if (nsArray == IntPtr.Zero) return 0;
        return ObjCRuntime.objc_msgSend_Long(nsArray, ObjCRuntime.Sel_Count);
    }

    /// <summary>
    /// Gets the object at a specific index in an NSArray.
    /// </summary>
    internal static IntPtr GetNSArrayObjectAtIndex(IntPtr nsArray, long index)
    {
        if (nsArray == IntPtr.Zero) return IntPtr.Zero;
        return ObjCRuntime.objc_msgSend_IntPtr_Long(nsArray, ObjCRuntime.Sel_ObjectAtIndex, index);
    }

    #endregion

    #region NSDictionary

    /// <summary>
    /// Gets the value for a key in an NSDictionary.
    /// </summary>
    internal static IntPtr GetNSDictionaryObjectForKey(IntPtr dict, IntPtr key)
    {
        if (dict == IntPtr.Zero || key == IntPtr.Zero) return IntPtr.Zero;
        return ObjCRuntime.objc_msgSend_IntPtr_IntPtr(dict, ObjCRuntime.Sel_ObjectForKey, key);
    }

    #endregion

    #region NSAutoreleasePool

    /// <summary>
    /// Creates a new NSAutoreleasePool via [[NSAutoreleasePool alloc] init].
    /// Must be balanced with <see cref="DrainAutoreleasePool"/>.
    /// </summary>
    internal static IntPtr CreateAutoreleasePool()
    {
        var pool = ObjCRuntime.objc_msgSend_IntPtr(ObjCRuntime.Class_NSAutoreleasePool, ObjCRuntime.Sel_Alloc);
        return ObjCRuntime.objc_msgSend_IntPtr(pool, ObjCRuntime.Sel_Init);
    }

    /// <summary>
    /// Drains an NSAutoreleasePool, releasing all autoreleased objects within it.
    /// </summary>
    internal static void DrainAutoreleasePool(IntPtr pool)
    {
        if (pool == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void(pool, ObjCRuntime.Sel_Drain);
    }

    #endregion

    #region NSObject Memory Management

    /// <summary>
    /// Sends [obj retain] to increment the reference count.
    /// </summary>
    internal static IntPtr Retain(IntPtr obj)
    {
        if (obj == IntPtr.Zero) return IntPtr.Zero;
        return ObjCRuntime.objc_msgSend_IntPtr(obj, ObjCRuntime.Sel_Retain);
    }

    /// <summary>
    /// Sends [obj release] to decrement the reference count.
    /// </summary>
    internal static void Release(IntPtr obj)
    {
        if (obj == IntPtr.Zero) return;
        ObjCRuntime.objc_msgSend_Void(obj, ObjCRuntime.Sel_Release);
    }

    #endregion

    #region NSURL

    private static readonly IntPtr Sel_URLWithString = ObjCRuntime.sel_registerName("URLWithString:");

    /// <summary>
    /// Creates an NSURL from a string.
    /// </summary>
    internal static IntPtr CreateNSURL(string? urlString)
    {
        if (urlString is null) return IntPtr.Zero;

        var nsUrlString = CreateNSString(urlString);
        if (nsUrlString == IntPtr.Zero) return IntPtr.Zero;

        return ObjCRuntime.objc_msgSend_IntPtr_IntPtr(
            ObjCRuntime.Class_NSURL,
            Sel_URLWithString,
            nsUrlString);
    }

    #endregion

    #region NSData

    private static readonly IntPtr Sel_Bytes = ObjCRuntime.sel_registerName("bytes");
    private static readonly IntPtr Sel_Length = ObjCRuntime.sel_registerName("length");
    private static readonly IntPtr Sel_DataWithBytesLength = ObjCRuntime.sel_registerName("dataWithBytes:length:");

    /// <summary>
    /// Gets the raw bytes pointer from an NSData.
    /// </summary>
    internal static IntPtr GetNSDataBytes(IntPtr nsData)
    {
        if (nsData == IntPtr.Zero) return IntPtr.Zero;
        return ObjCRuntime.objc_msgSend_IntPtr(nsData, Sel_Bytes);
    }

    /// <summary>
    /// Gets the length of an NSData.
    /// </summary>
    internal static long GetNSDataLength(IntPtr nsData)
    {
        if (nsData == IntPtr.Zero) return 0;
        return ObjCRuntime.objc_msgSend_Long(nsData, Sel_Length);
    }

    /// <summary>
    /// Creates an NSData from a managed byte array.
    /// </summary>
    internal static IntPtr CreateNSDataFromBytes(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return IntPtr.Zero;

        var pinnedHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return ObjCRuntime.objc_msgSend_IntPtr_IntPtr_Long(
                ObjCRuntime.Class_NSData,
                Sel_DataWithBytesLength,
                pinnedHandle.AddrOfPinnedObject(),
                bytes.Length);
        }
        finally
        {
            pinnedHandle.Free();
        }
    }

    /// <summary>
    /// Reads the contents of an NSData into a managed byte array.
    /// </summary>
    internal static byte[]? GetBytesFromNSData(IntPtr nsData)
    {
        if (nsData == IntPtr.Zero) return null;

        var length = GetNSDataLength(nsData);
        if (length <= 0) return null;

        var bytesPtr = GetNSDataBytes(nsData);
        if (bytesPtr == IntPtr.Zero) return null;

        var result = new byte[length];
        Marshal.Copy(bytesPtr, result, 0, (int)length);
        return result;
    }

    #endregion
}
