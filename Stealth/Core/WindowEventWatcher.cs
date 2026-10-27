
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Stealth.Core
{
    // Native out-of-context WinEvent hooks run only while the overlay is enabled.
    // Debounce move events so a window being dragged doesn't flood WPF layout.
    internal sealed class WindowEventWatcher : IDisposable
    {
        private const uint Foreground = 0x0003;
        private const uint Destroy = 0x8001, Show = 0x8002, Hide = 0x8003, Move = 0x800B;
        private const uint OutOfContextAndSkipOwn = 0x0002;
        private readonly Dispatcher dispatcher;
        private readonly DispatcherTimer debounce;
        private readonly Action refresh;
        private readonly WinEventProc callback;
        private IntPtr foregroundHook, lifetimeHook, positionHook;
        private bool disposed;

        private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd,
            int objectId, int childId, uint eventThread, uint eventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module,
            WinEventProc proc, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hook);

        internal WindowEventWatcher(Dispatcher dispatcher, Action refresh)
        {
            this.dispatcher = dispatcher;
            this.refresh = refresh;
            debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher) {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            debounce.Tick += OnDebounceTick;
            callback = OnNativeEvent;
            foregroundHook = SetWinEventHook(Foreground, Foreground, IntPtr.Zero, callback, 0, 0, OutOfContextAndSkipOwn);
            lifetimeHook = SetWinEventHook(Destroy, Hide, IntPtr.Zero, callback, 0, 0, OutOfContextAndSkipOwn);
            positionHook = SetWinEventHook(Move, Move, IntPtr.Zero, callback, 0, 0, OutOfContextAndSkipOwn);
            if (foregroundHook == IntPtr.Zero || lifetimeHook == IntPtr.Zero || positionHook == IntPtr.Zero)
            {
                Dispose();
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                    "Unable to subscribe to Windows foreground/window events.");
            }
        }

        private void OnNativeEvent(IntPtr hook, uint kind, IntPtr hwnd,
            int objectId, int childId, uint thread, uint time)
        {
            if (disposed || hwnd == IntPtr.Zero || objectId != 0 || childId != 0) return;
            // Events are delivered to the installing thread. Marshal defensively
            // for unusual accessibility providers.
            dispatcher.BeginInvoke((Action)(() => {
                if (!disposed) { debounce.Stop(); debounce.Start(); }
            }));
        }

        private void OnDebounceTick(object sender, EventArgs e)
        {
            debounce.Stop();
            if (!disposed) refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            debounce.Stop();
            debounce.Tick -= OnDebounceTick;
            if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
            if (lifetimeHook != IntPtr.Zero) UnhookWinEvent(lifetimeHook);
            if (positionHook != IntPtr.Zero) UnhookWinEvent(positionHook);
            foregroundHook = lifetimeHook = positionHook = IntPtr.Zero;
            GC.KeepAlive(callback);
        }
    }
}
