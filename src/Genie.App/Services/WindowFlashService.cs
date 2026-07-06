using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Genie.App.Services;

/// <summary>
/// Cross-platform "get the player's attention" backend for the <c>#flash</c>
/// command (Genie 4 parity — its FormMain called Win32 <c>FlashWindow</c>).
/// Best-effort and fire-and-forget: an unsupported platform or a failed native
/// call is swallowed so a trigger firing <c>#flash</c> never disrupts the
/// session.
/// <list type="bullet">
///   <item>Windows — <c>user32 FlashWindowEx</c> with <c>FLASHW_ALL |
///         FLASHW_TIMERNOFG</c>: caption + taskbar button flash until the
///         window comes to the foreground (an upgrade over Genie 4's single
///         invert, which was easy to miss).</item>
///   <item>macOS — <c>[NSApp requestUserAttention: NSCriticalRequest]</c>:
///         dock icon bounces until the app is activated.</item>
///   <item>Linux — no-op. Avalonia exposes no urgency-hint API and the X11 /
///         Wayland handles it surfaces aren't enough to set one natively.</item>
/// </list>
/// Skips entirely when the window is already active — flashing for attention
/// the user is already paying is just noise.
/// </summary>
public static class WindowFlashService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL       = 0x3; // caption + taskbar button
    private const uint FLASHW_TIMERNOFG = 0xC; // flash until foregrounded

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    // macOS: [[NSApplication sharedApplication] requestUserAttention:0]
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, long arg);

    /// <summary>Flash <paramref name="window"/>'s taskbar / dock entry. No-op
    /// when the window is already active or the platform has no attention API.
    /// Call on the UI thread (reads <see cref="WindowBase.IsActive"/>). Never
    /// throws.</summary>
    public static void Flash(Window window)
    {
        try
        {
            if (window.IsActive) return;

            if (OperatingSystem.IsWindows())
            {
                var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero) return;
                var info = new FLASHWINFO
                {
                    cbSize    = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd      = hwnd,
                    dwFlags   = FLASHW_ALL | FLASHW_TIMERNOFG,
                    uCount    = uint.MaxValue,
                    dwTimeout = 0, // default cursor-blink cadence
                };
                FlashWindowEx(ref info);
            }
            else if (OperatingSystem.IsMacOS())
            {
                const long NSCriticalRequest = 0;
                var nsApp = objc_msgSend(objc_getClass("NSApplication"),
                                         sel_registerName("sharedApplication"));
                if (nsApp != IntPtr.Zero)
                    objc_msgSend(nsApp, sel_registerName("requestUserAttention:"),
                                 NSCriticalRequest);
            }
            // Linux: no urgency-hint path available through Avalonia — no-op.
        }
        catch (Exception ex)
        {
            Diagnostics.ErrorLog.Log("WindowFlashService.Flash", ex);
        }
    }
}
