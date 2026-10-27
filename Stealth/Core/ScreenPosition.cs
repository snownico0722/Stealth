
using System;
using System.Runtime.InteropServices;

namespace Stealth.Core
{
    // Screen rectangles are always PHYSICAL pixels. Only the WPF visual tree
    // uses device-independent pixels. Do not add a WPF-DIP drag delta to them.
    internal static class ScreenPosition
    {
        private const uint Nearest = 2;
        private const uint NoSize = 0x0001, NoZOrder = 0x0004, NoActivate = 0x0010;
        [StructLayout(LayoutKind.Sequential)]
        private struct PixelPoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PixelRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public uint Size;
            public PixelRect Monitor;
            public PixelRect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(PixelPoint position, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y,
            int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
        private static extern uint GetWindowDpi(IntPtr hwnd);

        internal static double DpiScale(IntPtr hwnd)
        {
            try
            {
                var dpi = GetWindowDpi(hwnd);
                return dpi >= 96 && dpi <= 768 ? dpi / 96d : 1d;
            }
            catch (EntryPointNotFoundException)
            {
                return 1d;
            }
        }

        internal static void Clamp(int desiredX, int desiredY, int width, int height,
            out int x, out int y)
        {
            x = desiredX;
            y = desiredY;
            var monitor = MonitorFromPoint(new PixelPoint { X = desiredX, Y = desiredY }, Nearest);
            if (monitor == IntPtr.Zero) return;
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info)) return;
            ClampToBounds(x, y, width, height,
                info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom,
                out x, out y);
        }

        // Pure geometry logic: testable without depending on a particular monitor.
        internal static void ClampToBounds(int desiredX, int desiredY, int width, int height,
            int left, int top, int right, int bottom, out int x, out int y)
        {
            x = Math.Max(left, Math.Min(desiredX, Math.Max(left, right - width)));
            y = Math.Max(top, Math.Min(desiredY, Math.Max(top, bottom - height)));
        }

        internal static void Move(IntPtr hwnd, int x, int y)
        {
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NoSize | NoZOrder | NoActivate);
        }
    }
}
