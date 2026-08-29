using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DWMB_AIO.DWMB.Notifications
{
    /// <summary>
    /// Flashes a WPF window's taskbar button via the Win32 <c>FlashWindowEx</c> API — WPF
    /// has no managed equivalent. Used as a visual "someone is trying to reach you" cue
    /// when the main window isn't in the foreground, alongside (and independent of) the
    /// optional alarm sound in <see cref="DWMB_AIO.DWMB.Audio"/> — this one is always on,
    /// since a taskbar flash isn't disruptive the way audio can be.
    /// </summary>
    static class TaskbarFlasher
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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_TRAY = 0x00000002;
        // Keeps flashing (ignoring uCount) until the window comes to the foreground, which
        // is exactly the "stop nagging once acknowledged" behavior we want, without needing
        // to separately track/cancel a flash count or timer ourselves.
        private const uint FLASHW_TIMERNOFG = 0x0000000C;

        /// <summary>
        /// Starts flashing the taskbar button until the window comes to the foreground (or
        /// <see cref="Stop"/> is called explicitly). Safe to call repeatedly/redundantly.
        /// </summary>
        public static void Start(Window window)
        {
            Flash(window, FLASHW_TRAY | FLASHW_TIMERNOFG);
        }

        /// <summary>Stops an in-progress taskbar flash. Safe to call when none is in progress.</summary>
        public static void Stop(Window window)
        {
            Flash(window, FLASHW_STOP);
        }

        private static void Flash(Window window, uint flags)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                // Window hasn't been shown yet (no HWND assigned) — nothing to flash.
                return;
            }

            var info = new FLASHWINFO
            {
                hwnd = hwnd,
                dwFlags = flags,
                uCount = uint.MaxValue,
                dwTimeout = 0, // 0 = use the default cursor blink rate
            };
            info.cbSize = (uint)Marshal.SizeOf(info);

            FlashWindowEx(ref info);
        }
    }
}
