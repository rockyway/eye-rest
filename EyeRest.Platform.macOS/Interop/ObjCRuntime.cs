using System.Runtime.InteropServices;

namespace EyeRest.Platform.macOS.Interop;

/// <summary>
/// Core Objective-C runtime bindings for interacting with macOS frameworks.
/// Provides P/Invoke declarations for the ObjC runtime (libobjc) including
/// message sending, class/selector lookup, and cached accessors.
/// Ported from TextAssistant.Platform.macOS.
/// </summary>
internal static class ObjCRuntime
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    #region P/Invoke Declarations

    /// <summary>
    /// Returns the class definition of a specified class by name.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr objc_getClass(string name);

    /// <summary>
    /// Registers a method selector with the Objective-C runtime.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr sel_registerName(string name);

    /// <summary>
    /// Sends a message to an object returning an IntPtr result.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with one IntPtr argument returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    /// <summary>
    /// Sends a message with two IntPtr arguments returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    /// <summary>
    /// Sends a message with three IntPtr arguments returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    /// <summary>
    /// Sends a message returning void.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with one IntPtr argument returning void.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    /// <summary>
    /// Sends a message with two IntPtr arguments returning void.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    /// <summary>
    /// Sends a message returning bool.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_Bool(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with one IntPtr argument returning bool.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_Bool_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    /// <summary>
    /// Sends a message with two IntPtr arguments returning bool.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_Bool_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    /// <summary>
    /// Sends a message with a long argument returning bool.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_Bool_Long(IntPtr receiver, IntPtr selector, long arg1);

    /// <summary>
    /// Sends a message returning double.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern double objc_msgSend_Double(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message returning long (NSInteger on 64-bit).
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern long objc_msgSend_Long(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with a long argument returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_Long(IntPtr receiver, IntPtr selector, long arg1);

    /// <summary>
    /// Sends a message returning ulong (NSUInteger on 64-bit).
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern ulong objc_msgSend_ULong(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with a ulong argument returning void.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void_ULong(IntPtr receiver, IntPtr selector, ulong arg1);

    /// <summary>
    /// Sends a message with a ulong argument and an IntPtr argument returning void.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void_ULong_IntPtr(IntPtr receiver, IntPtr selector, ulong arg1, IntPtr arg2);

    /// <summary>
    /// Sends a message with IntPtr and long arguments returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_IntPtr_Long(IntPtr receiver, IntPtr selector, IntPtr arg1, long arg2);

    /// <summary>
    /// Sends a message with double width and double height arguments returning IntPtr.
    /// Used for methods like initWithSize:.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_Double_Double(IntPtr receiver, IntPtr selector, double arg1, double arg2);

    /// <summary>
    /// Sends a message that returns a struct via stret convention (used for CGRect on x86_64).
    /// On ARM64 macOS, struct returns up to 4 registers go through normal return; larger ones use stret.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend_stret")]
    internal static extern void objc_msgSend_Stret(out CGRect result, IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message that returns a CGRect (used on ARM64 where stret is not needed).
    /// On Apple Silicon, CGRect is returned via registers directly.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern CGRect objc_msgSend_CGRect(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message that returns an NSPoint (CGPoint) -- two doubles.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern NSPoint objc_msgSend_NSPoint(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Returns the name of a class as a UTF-8 string pointer.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr class_getName(IntPtr cls);

    /// <summary>
    /// Sends a message with a UTF8 string argument returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_String(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg1);

    /// <summary>
    /// Sends a message with an int argument returning IntPtr.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_Int(IntPtr receiver, IntPtr selector, int arg1);

    /// <summary>
    /// Sends a message returning int.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern int objc_msgSend_Int(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// Sends a message with one bool argument returning void.
    /// Uses proper BOOL marshaling (1-byte) for ARM64 ABI compatibility.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern void objc_msgSend_Void_Bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

    /// <summary>
    /// Sends a message with one IntPtr argument and one bool argument returning bool.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool objc_msgSend_Bool_IntPtr_Bool(
        IntPtr receiver, IntPtr selector, IntPtr arg1, [MarshalAs(UnmanagedType.I1)] bool arg2);

    /// <summary>
    /// Sends a message with a double and a bool argument returning IntPtr.
    /// Uses proper BOOL marshaling (1-byte) for ARM64 ABI compatibility.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_IntPtr_Double_Bool(IntPtr receiver, IntPtr selector, double arg1, [MarshalAs(UnmanagedType.I1)] bool arg2);

    /// <summary>
    /// Allocates a new Objective-C class pair for dynamic class registration.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr objc_allocateClassPair(IntPtr superclass, string name, IntPtr extraBytes);

    /// <summary>
    /// Registers a previously allocated Objective-C class pair with the runtime.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void objc_registerClassPair(IntPtr cls);

    /// <summary>
    /// Adds a method implementation to an Objective-C class.
    /// </summary>
    [DllImport(ObjCLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool class_addMethod(IntPtr cls, IntPtr sel, IntPtr imp, string types);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets an Objective-C class by name. Returns IntPtr.Zero if not found.
    /// </summary>
    internal static IntPtr GetClass(string name)
    {
        return objc_getClass(name);
    }

    /// <summary>
    /// Registers and returns a selector for the given name.
    /// </summary>
    internal static IntPtr GetSelector(string name)
    {
        return sel_registerName(name);
    }

    #endregion

    #region Cached Selectors

    internal static readonly IntPtr Sel_Alloc = sel_registerName("alloc");
    internal static readonly IntPtr Sel_Init = sel_registerName("init");
    internal static readonly IntPtr Sel_New = sel_registerName("new");
    internal static readonly IntPtr Sel_Release = sel_registerName("release");
    internal static readonly IntPtr Sel_Retain = sel_registerName("retain");
    internal static readonly IntPtr Sel_Autorelease = sel_registerName("autorelease");
    internal static readonly IntPtr Sel_Drain = sel_registerName("drain");
    internal static readonly IntPtr Sel_Count = sel_registerName("count");
    internal static readonly IntPtr Sel_ObjectAtIndex = sel_registerName("objectAtIndex:");
    internal static readonly IntPtr Sel_ObjectForKey = sel_registerName("objectForKey:");
    internal static readonly IntPtr Sel_StringWithUTF8String = sel_registerName("stringWithUTF8String:");
    internal static readonly IntPtr Sel_UTF8String = sel_registerName("UTF8String");
    internal static readonly IntPtr Sel_Description = sel_registerName("description");

    #endregion

    #region Cached Classes

    internal static readonly IntPtr Class_NSString = objc_getClass("NSString");
    internal static readonly IntPtr Class_NSArray = objc_getClass("NSArray");
    internal static readonly IntPtr Class_NSDictionary = objc_getClass("NSDictionary");
    internal static readonly IntPtr Class_NSAutoreleasePool = objc_getClass("NSAutoreleasePool");
    internal static readonly IntPtr Class_NSURL = objc_getClass("NSURL");
    internal static readonly IntPtr Class_NSData = objc_getClass("NSData");
    internal static readonly IntPtr Class_NSNumber = objc_getClass("NSNumber");

    #endregion
}

#region Interop Structs

/// <summary>
/// Represents a point in a Cartesian coordinate system (CGPoint / NSPoint).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NSPoint
{
    public double X;
    public double Y;

    public NSPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Represents the dimensions of a rectangle (CGSize / NSSize).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NSSize
{
    public double Width;
    public double Height;

    public NSSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public override string ToString() => $"({Width} x {Height})";
}

/// <summary>
/// Represents a rectangle (CGRect / NSRect) with origin and size.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CGRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;

    public CGRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public NSPoint Origin => new(X, Y);
    public NSSize Size => new(Width, Height);
    public static CGRect Zero => new(0, 0, 0, 0);

    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}

#endregion
