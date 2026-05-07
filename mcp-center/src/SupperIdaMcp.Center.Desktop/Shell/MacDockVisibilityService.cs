using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace SupperIdaMcp.Center.Desktop.Shell;

internal static class MacDockVisibilityService
{
    private const nint RegularActivationPolicy = 0;
    private const nint AccessoryActivationPolicy = 1;
    private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

    public static void ShowDockIcon(bool activate)
    {
        DispatchToUiThread(() =>
        {
            SetActivationPolicy(RegularActivationPolicy);

            if (activate)
            {
                ActivateApplicationCore();
            }
        });
    }

    public static void HideDockIcon()
    {
        DispatchToUiThread(() => SetActivationPolicy(AccessoryActivationPolicy));
    }

    public static void ActivateApplication()
    {
        DispatchToUiThread(ActivateApplicationCore);
    }

    private static void DispatchToUiThread(Action action)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private static void SetActivationPolicy(nint activationPolicy)
    {
        var application = GetSharedApplication();
        if (application == IntPtr.Zero)
        {
            return;
        }

        _ = objc_msgSend_setActivationPolicy(application, sel_registerName("setActivationPolicy:"), activationPolicy);
    }

    private static void ActivateApplicationCore()
    {
        var application = GetSharedApplication();
        if (application == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_activateIgnoringOtherApps(application, sel_registerName("activateIgnoringOtherApps:"), true);
    }

    private static IntPtr GetSharedApplication()
    {
        var applicationClass = objc_getClass("NSApplication");
        return applicationClass == IntPtr.Zero
            ? IntPtr.Zero
            : objc_msgSend_sharedApplication(applicationClass, sel_registerName("sharedApplication"));
    }

    [DllImport(ObjCLibrary)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLibrary)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_sharedApplication(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool objc_msgSend_setActivationPolicy(IntPtr receiver, IntPtr selector, nint activationPolicy);

    [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_activateIgnoringOtherApps(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.Bool)] bool flag);
}
