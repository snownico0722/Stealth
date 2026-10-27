
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Stealth.Core
{
    internal interface IWindowStateApi : IWindowOpacityApi
    {
        bool IsWindow(IntPtr hwnd);
        void GetIdentity(IntPtr hwnd, out uint pid, out uint tid);
        bool TrySetTopMost(IntPtr hwnd, bool enabled, out string error);
    }

    internal sealed class NativeWindowStateApi : IWindowStateApi
    {
        private readonly NativeWindowOpacityApi _opacity = new NativeWindowOpacityApi();
        private static HWND H(IntPtr ptr) { unsafe { return new HWND((void*)ptr); } }
        public bool IsWindow(IntPtr hwnd) => PInvoke.IsWindow(H(hwnd));
        public void GetIdentity(IntPtr hwnd, out uint pid, out uint tid)
        {
            tid = PInvoke.GetWindowThreadProcessId(H(hwnd), out pid);
        }
        public int GetExtendedStyle(IntPtr hwnd) => _opacity.GetExtendedStyle(hwnd);
        public bool TryGetLayeredAttributes(IntPtr hwnd, out uint key, out byte alpha, out uint flags, out int error) =>
            _opacity.TryGetLayeredAttributes(hwnd, out key, out alpha, out flags, out error);
        public bool TrySetExtendedStyle(IntPtr hwnd, int style, out int error) =>
            _opacity.TrySetExtendedStyle(hwnd, style, out error);
        public bool TrySetLayeredAttributes(IntPtr hwnd, uint key, byte alpha, uint flags, out int error) =>
            _opacity.TrySetLayeredAttributes(hwnd, key, alpha, flags, out error);
        public bool TrySetTopMost(IntPtr hwnd, bool enabled, out string error)
        {
            bool succeeded;
            unsafe {
                succeeded = PInvoke.SetWindowPos(H(hwnd), new HWND((void*)(enabled ? -1 : -2)),
                    0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                    SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
            }
            bool actual = (GetExtendedStyle(hwnd) & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;
            error = succeeded && actual == enabled ? null :
                "Always-on-top was not changed (Win32 error " + Marshal.GetLastWin32Error() + ").";
            return error == null;
        }
    }

    // Shared by the list and the floating opacity control. Original state is
    // captured only before the first successful modification.
    internal sealed class WindowStateManager
    {
        private const int Layered = (int)WINDOW_EX_STYLE.WS_EX_LAYERED;
        private const int Topmost = (int)WINDOW_EX_STYLE.WS_EX_TOPMOST;
        private const uint AlphaFlag = (uint)LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA;
        private readonly IWindowStateApi api;
        private readonly WindowOpacityWriter opacity;
        private readonly uint ownPid;
        private readonly Dictionary<IntPtr, State> entries = new Dictionary<IntPtr, State>();
        internal static WindowStateManager Shared { get; } =
            new WindowStateManager(new NativeWindowStateApi(), OwnProcessId());

        private static uint OwnProcessId()
        {
            using (var process = Process.GetCurrentProcess()) return (uint)process.Id;
        }

        private sealed class State
        {
            internal uint Pid, Tid, Key, Flags;
            internal byte OriginalAlpha, LastAlpha;
            internal int OriginalStyle;
            internal bool Readable, ChangedOpacity, ChangedTopmost, LastTopmost;
        }

        internal WindowStateManager(IWindowStateApi api, uint ownPid)
        {
            this.api = api;
            this.ownPid = ownPid;
            opacity = new WindowOpacityWriter(api);
        }

        private bool Identify(IntPtr hwnd, out uint pid, out uint tid, out string error)
        {
            pid = tid = 0;
            if (hwnd == IntPtr.Zero || !api.IsWindow(hwnd))
            {
                error = "The target window no longer exists.";
                return false;
            }
            api.GetIdentity(hwnd, out pid, out tid);
            if (pid == 0 || tid == 0 || pid == ownPid)
            {
                error = "Stealth cannot modify its own windows or an unidentified window.";
                return false;
            }
            if (entries.TryGetValue(hwnd, out var previous) &&
                (previous.Pid != pid || previous.Tid != tid))
                entries.Remove(hwnd);
            error = null;
            return true;
        }

        private State Snapshot(IntPtr hwnd, uint pid, uint tid)
        {
            var style = api.GetExtendedStyle(hwnd);
            uint key = 0, flags = 0;
            byte alpha = 255;
            bool readable = (style & Layered) == 0 ||
                api.TryGetLayeredAttributes(hwnd, out key, out alpha, out flags, out _);
            return new State {
                Pid = pid, Tid = tid, OriginalStyle = style, Readable = readable,
                Key = key, Flags = flags, OriginalAlpha = alpha, LastAlpha = alpha,
                LastTopmost = (style & Topmost) != 0
            };
        }

        internal byte ReadOpacity(IntPtr hwnd)
        {
            return api.TryGetLayeredAttributes(hwnd, out _, out byte alpha, out _, out _) ? alpha : (byte)255;
        }

        internal bool ReadLayered(IntPtr hwnd) =>
            (api.GetExtendedStyle(hwnd) & Layered) != 0;

        internal bool ReadTopMost(IntPtr hwnd) =>
            (api.GetExtendedStyle(hwnd) & Topmost) != 0;

        internal bool IsModified(IntPtr hwnd) =>
            entries.TryGetValue(hwnd, out var state) && (state.ChangedOpacity || state.ChangedTopmost);

        private bool IsOurOpacity(IntPtr hwnd, State state) =>
            (api.GetExtendedStyle(hwnd) & Layered) != 0 &&
            api.TryGetLayeredAttributes(hwnd, out _, out byte alpha, out uint flags, out _) &&
            alpha == state.LastAlpha && (flags & AlphaFlag) != 0;

        internal bool TrySetOpacity(IntPtr hwnd, byte value, out string error)
        {
            if (!Identify(hwnd, out uint pid, out uint tid, out error)) return false;
            var state = entries.TryGetValue(hwnd, out var saved) ? saved : Snapshot(hwnd, pid, tid);
            if (!state.Readable)
            {
                error = "Original layered transparency cannot be read; operation refused to protect the window.";
                return false;
            }
            if (state.ChangedOpacity && !IsOurOpacity(hwnd, state))
            {
                error = "Another application changed this window's transparency. Stealth will not overwrite it.";
                return false;
            }
            if (!opacity.TrySetOpacity(hwnd, value, out error)) return false;
            state.ChangedOpacity = true;
            state.LastAlpha = value;
            entries[hwnd] = state;
            return true;
        }

        internal bool TrySetTopMost(IntPtr hwnd, bool enabled, out string error)
        {
            if (!Identify(hwnd, out uint pid, out uint tid, out error)) return false;
            var state = entries.TryGetValue(hwnd, out var saved) ? saved : Snapshot(hwnd, pid, tid);
            if (!api.TrySetTopMost(hwnd, enabled, out error)) return false;
            state.ChangedTopmost = true;
            state.LastTopmost = enabled;
            entries[hwnd] = state;
            return true;
        }

        internal bool TryRestore(IntPtr hwnd, out string error)
        {
            error = null;
            if (!entries.TryGetValue(hwnd, out var state)) return true;
            if (!api.IsWindow(hwnd))
            {
                entries.Remove(hwnd);
                return true;
            }
            api.GetIdentity(hwnd, out uint pid, out uint tid);
            if (pid != state.Pid || tid != state.Tid)
            {
                entries.Remove(hwnd);
                error = "Window handle was reused; old state was discarded.";
                return false;
            }

            var failures = new List<string>();
            if (state.ChangedOpacity)
            {
                if (!IsOurOpacity(hwnd, state))
                    failures.Add("Transparency was modified externally; no rollback performed.");
                else if ((state.OriginalStyle & Layered) != 0)
                {
                    if (!api.TrySetLayeredAttributes(hwnd, state.Key, state.OriginalAlpha, state.Flags, out int code))
                        failures.Add("Restoring layered attributes failed (" + code + ").");
                    else state.ChangedOpacity = false;
                }
                else
                {
                    int style = api.GetExtendedStyle(hwnd);
                    if (!api.TrySetExtendedStyle(hwnd, style & ~Layered, out int code))
                        failures.Add("Restoring window style failed (" + code + ").");
                    else state.ChangedOpacity = false;
                }
            }
            if (state.ChangedTopmost)
            {
                bool actual = (api.GetExtendedStyle(hwnd) & Topmost) != 0;
                if (actual == state.LastTopmost)
                {
                    if (!api.TrySetTopMost(hwnd, (state.OriginalStyle & Topmost) != 0, out string reason))
                        failures.Add(reason);
                    else state.ChangedTopmost = false;
                }
                else failures.Add("Topmost was changed externally; no rollback performed.");
            }
            if (!state.ChangedOpacity && !state.ChangedTopmost) entries.Remove(hwnd);
            if (failures.Count > 0) error = string.Join(" ", failures);
            return failures.Count == 0;
        }

        internal void ForgetClosed()
        {
            foreach (var pair in entries.ToArray())
            {
                if (!api.IsWindow(pair.Key)) { entries.Remove(pair.Key); continue; }
                api.GetIdentity(pair.Key, out uint pid, out uint tid);
                if (pid != pair.Value.Pid || tid != pair.Value.Tid) entries.Remove(pair.Key);
            }
        }

        internal void RestoreAll()
        {
            foreach (var hwnd in entries.Keys.ToArray())
                if (!TryRestore(hwnd, out string error))
                    Debug.WriteLine("Stealth: restoration skipped: " + error);
            entries.Clear();
        }
    }
}
