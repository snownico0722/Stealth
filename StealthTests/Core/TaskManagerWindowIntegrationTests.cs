using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;
using Stealth.Model;

namespace Stealth.Core.Tests
{
    // Exercise the actual WinUI Task Manager window on a Windows runner.
    // Unlike a synthetic STATIC HWND, this reveals alternate top-level
    // surfaces, replacement HWNDs and privilege-dependent behavior.
    [TestClass]
    public sealed class TaskManagerWindowIntegrationTests
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumCallback callback, IntPtr data);
        private delegate bool EnumCallback(IntPtr hwnd, IntPtr data);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int capacity);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        public TestContext TestContext { get; set; }

        private sealed class Window
        {
            internal IntPtr Handle;
            internal uint Pid;
            internal string ClassName, Title;
        }

        private static List<Window> Enumerate(ISet<uint> pidSet)
        {
            var windows = new List<Window>();
            EnumWindows((hwnd, unused) => {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (!pidSet.Contains(pid)) return true;
                var klass = new StringBuilder(256);
                var title = new StringBuilder(512);
                GetClassName(hwnd, klass, klass.Capacity);
                GetWindowText(hwnd, title, title.Capacity);
                windows.Add(new Window {
                    Handle = hwnd, Pid = pid, ClassName = klass.ToString(), Title = title.ToString()
                });
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        private static HashSet<uint> TaskManagerProcesses()
        {
            var ids = new HashSet<uint>();
            foreach (var proc in Process.GetProcessesByName("Taskmgr"))
            {
                using (proc)
                {
                    try { ids.Add((uint)proc.Id); }
                    catch (InvalidOperationException) { }
                }
            }
            return ids;
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void WindowsTaskManager_FirstPinRequiresARealMainWindow()
        {
            var initialPids = TaskManagerProcesses();
            if (initialPids.Count == 0)
            {
                try {
                    var exe = System.IO.Path.Combine(Environment.SystemDirectory, "Taskmgr.exe");
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception ||
                                               error is InvalidOperationException)
                {
                    Assert.Inconclusive("Task Manager cannot start on this runner: " + error.Message);
                }
            }

            List<Window> windows = null;
            for (int attempt = 0; attempt < 35; attempt++)
            {
                Thread.Sleep(200);
                var pids = TaskManagerProcesses();
                windows = Enumerate(pids);
                if (windows.Any(w => w.ClassName == "TaskManagerWindow" &&
                                     IsWindowVisible(w.Handle))) break;
            }
            var diagnostics = string.Join("; ", windows.Select(w =>
                "0x" + w.Handle.ToInt64().ToString("X") +
                " pid=" + w.Pid + " class=" + w.ClassName +
                " title=" + w.Title + " visible=" + IsWindowVisible(w.Handle)));

            var main = windows.FirstOrDefault(w => w.ClassName == "TaskManagerWindow" &&
                                                   IsWindowVisible(w.Handle));
            if (main == null)
                Assert.Inconclusive("No visible WinUI TaskManagerWindow on runner. " + diagnostics);

            var api = new NativeWindowStateApi();
            var manager = new WindowStateManager(api);
            var initial = manager.ReadTopMost(main.Handle);
            var message = new StringBuilder(diagnostics);
            message.Append("; initialTopmost=").Append(initial);
            try
            {
                bool accepted = manager.TrySetTopMost(main.Handle, true, out string error);
                message.Append("; first SetWindowPos=").Append(accepted)
                    .Append(" error=").Append(error)
                    .Append(" immediate=").Append(manager.ReadTopMost(main.Handle));
                Thread.Sleep(120);
                message.Append(" at120ms=").Append(manager.ReadTopMost(main.Handle));
                Thread.Sleep(250);
                message.Append(" at370ms=").Append(manager.ReadTopMost(main.Handle));
                Thread.Sleep(450);
                bool persisted = manager.ReadTopMost(main.Handle);
                message.Append(" at820ms=").Append(persisted);
                TestContext.WriteLine(message.ToString());
                Assert.IsTrue(accepted && persisted,
                    "Task Manager first pin failed. " + message);

                // The actual user's path is WPF checkbox -> model notification
                // -> MainService -> WindowStateManager -> SetWindowPos.
                // Exercise it against TaskManagerWindow rather than only
                // testing the low-level native call.
                Assert.IsTrue(manager.TryRestore(main.Handle, out error),
                    "Cannot restore test baseline: " + error);
                if (!initial)
                {
                    using (var service = new MainService())
                    {
                        var model = service.GetWindowListData()
                            .FirstOrDefault(item => item.HWnd == main.Handle && !item.IsRemoved);
                        Assert.IsNotNull(model,
                            "Stealth did not enumerate the actual TaskManagerWindow HWND. " + message);
                        Assert.IsFalse(model.IsTopMost);
                        model.IsTopMost = true;
                        Thread.Sleep(300);
                        Assert.IsTrue(manager.ReadTopMost(main.Handle),
                            "First checkbox activation did not pin Task Manager. " +
                            "error=" + model.TopMostError + " " + message);
                        Assert.IsNull(model.TopMostError,
                            "Task Manager topmost model error: " + model.TopMostError);
                    }
                    Assert.IsFalse(manager.ReadTopMost(main.Handle),
                        "Stealth MainService did not restore the task manager on disposal.");
                }
            }
            finally
            {
                manager.RestoreAll();
                if (!initial && api.IsWindow(main.Handle))
                    api.TrySetTopMost(main.Handle, false, out _);
            }
        }
    }
}
