using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// P/Invoke bindings for macOS Security framework (Keychain Services).
/// Uses SecItem API for storing/retrieving/deleting keychain items.
/// </summary>
internal static class Security
{
    private const string SecurityLib = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(SecurityLib)]
    internal static extern int SecItemAdd(IntPtr attributes, out IntPtr result);

    [DllImport(SecurityLib)]
    internal static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport(SecurityLib)]
    internal static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [DllImport(SecurityLib)]
    internal static extern int SecItemDelete(IntPtr query);

    // CoreFoundation helpers for building CFDictionary
    [DllImport(CoreFoundationLib)]
    internal static extern IntPtr CFDictionaryCreateMutable(
        IntPtr allocator, long capacity, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [DllImport(CoreFoundationLib)]
    internal static extern void CFDictionarySetValue(IntPtr dict, IntPtr key, IntPtr value);

    [DllImport(CoreFoundationLib)]
    internal static extern void CFRelease(IntPtr obj);

    [DllImport(CoreFoundationLib)]
    internal static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, long length);

    [DllImport(CoreFoundationLib)]
    internal static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport(CoreFoundationLib)]
    internal static extern long CFDataGetLength(IntPtr data);

    // kCFTypeDictionaryKeyCallBacks and kCFTypeDictionaryValueCallBacks
    [DllImport(CoreFoundationLib)]
    internal static extern IntPtr kCFTypeDictionaryKeyCallBacks();

    [DllImport(CoreFoundationLib)]
    internal static extern IntPtr kCFTypeDictionaryValueCallBacks();

    // Use extern data symbols for CF constants
    internal static IntPtr kCFTypeDictionaryKeyCallBacksPtr =>
        GetExternPtr(CoreFoundationLib, "kCFTypeDictionaryKeyCallBacks");
    internal static IntPtr kCFTypeDictionaryValueCallBacksPtr =>
        GetExternPtr(CoreFoundationLib, "kCFTypeDictionaryValueCallBacks");

    // Security framework constants (loaded lazily as extern symbol pointers)
    internal static IntPtr kSecClass => GetSecPtr("kSecClass");
    internal static IntPtr kSecClassGenericPassword => GetSecPtr("kSecClassGenericPassword");
    internal static IntPtr kSecAttrService => GetSecPtr("kSecAttrService");
    internal static IntPtr kSecAttrAccount => GetSecPtr("kSecAttrAccount");
    internal static IntPtr kSecValueData => GetSecPtr("kSecValueData");
    internal static IntPtr kSecReturnData => GetSecPtr("kSecReturnData");
    internal static IntPtr kSecMatchLimit => GetSecPtr("kSecMatchLimit");
    internal static IntPtr kSecMatchLimitOne => GetSecPtr("kSecMatchLimitOne");

    // CoreFoundation boolean constants
    internal static IntPtr kCFBooleanTrue => GetExternPtr(CoreFoundationLib, "kCFBooleanTrue");

    // errSecItemNotFound
    internal const int errSecSuccess = 0;
    internal const int errSecItemNotFound = -25300;
    internal const int errSecDuplicateItem = -25299;

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlsym")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    private static IntPtr _securityHandle;
    private static IntPtr _cfHandle;

    private static IntPtr GetSecPtr(string symbol)
    {
        if (_securityHandle == IntPtr.Zero)
            _securityHandle = dlopen(SecurityLib, 1);
        var ptr = dlsym(_securityHandle, symbol);
        if (ptr == IntPtr.Zero) return IntPtr.Zero;
        return Marshal.ReadIntPtr(ptr);
    }

    private static IntPtr GetExternPtr(string lib, string symbol)
    {
        if (lib == CoreFoundationLib)
        {
            if (_cfHandle == IntPtr.Zero)
                _cfHandle = dlopen(CoreFoundationLib, 1);
            var ptr = dlsym(_cfHandle, symbol);
            if (ptr == IntPtr.Zero) return IntPtr.Zero;
            return Marshal.ReadIntPtr(ptr);
        }
        return GetSecPtr(symbol);
    }

    /// <summary>
    /// Creates a mutable CFDictionary with standard key/value callbacks.
    /// </summary>
    internal static IntPtr CreateMutableDictionary(int capacity = 0)
    {
        return CFDictionaryCreateMutable(
            IntPtr.Zero, capacity,
            kCFTypeDictionaryKeyCallBacksPtr,
            kCFTypeDictionaryValueCallBacksPtr);
    }

    /// <summary>
    /// Creates a CFData from a managed byte array.
    /// </summary>
    internal static IntPtr CreateCFData(byte[] bytes)
    {
        return CFDataCreate(IntPtr.Zero, bytes, bytes.Length);
    }

    /// <summary>
    /// Reads a CFData into a managed byte array.
    /// </summary>
    internal static byte[]? ReadCFData(IntPtr cfData)
    {
        if (cfData == IntPtr.Zero) return null;
        var length = CFDataGetLength(cfData);
        if (length <= 0) return null;
        var ptr = CFDataGetBytePtr(cfData);
        if (ptr == IntPtr.Zero) return null;
        var result = new byte[length];
        Marshal.Copy(ptr, result, 0, (int)length);
        return result;
    }
}
