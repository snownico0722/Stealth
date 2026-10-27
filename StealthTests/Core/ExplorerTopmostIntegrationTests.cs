using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;
using Stealth.Model;

namespace Stealth.Core.Tests
{
    // A cross-process Explorer test: the existing STATIC test window is owned
    // by the test process and doesn't exercise the shell window path.
    [TestClass]
    public sealed class ExplorerTopmostIntegrationTests
    {
        private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int capacity);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int capacity);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after,
            int x, int y, int width, int height, uint flags);

        public TestContext TestContext { get; set; }

        private static HashSet<IntPtr> ExplorerWindows()
        {
            var found = new HashSet<IntPtr>();
            EnumWindows((hwnd, data) =>
            {
                var buffer = new StringBuilder(128);
                GetClassName(hwnd, buffer, buffer.Capacity);
                if (string.Equals(buffer.ToString(), "CabinetWClass", StringComparison.Ordinal) &&
                    IsWindowVisible(hwnd))
                    found.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return found;
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ShellExplorerWindow_AcceptsTopmostAndRestoration_WhenDesktopIsAvailable()
        {
            var baseline = ExplorerWindows();
            var folder = Path.Combine(Path.GetTempPath(),
                "StealthExplorerCI_" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(folder);
            IntPtr hwnd = IntPtr.Zero;
            var api = new NativeWindowStateApi();
            var manager = new WindowStateManager(api);
            try
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception ||
                                         e is InvalidOperationException)
                {
                    Assert.Inconclusive("Interactive Explorer could not be launched on this CI runner: " + e.Message);
                }

                for (int i = 0; i < 30 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(250);
                    hwnd = ExplorerWindows().FirstOrDefault(h => !baseline.Contains(h));
                }
                if (hwnd == IntPtr.Zero)
                    Assert.Inconclusive("Explorer did not open a separate CabinetWClass HWND on this runner.");

                var caption = new StringBuilder(256);
                GetWindowText(hwnd, caption, caption.Capacity);
                TestContext.WriteLine("Explorer HWND=" + hwnd + " caption=" + caption +
                    " visible=" + IsWindowVisible(hwnd) +
                    " owner=" + GetWindow(hwnd, 4) +
                    " foreground=" + GetForegroundWindow());
                bool initial = manager.ReadTopMost(hwnd);
                bool accepted = manager.TrySetTopMost(hwnd, true, out string error);
                TestContext.WriteLine("Baseline SetWindowPos accepted=" + accepted +
                    " error=" + error + " immediate=" + manager.ReadTopMost(hwnd));
                Thread.Sleep(250);
                bool after250 = manager.ReadTopMost(hwnd);
                TestContext.WriteLine("Baseline after 250ms=" + after250);

                if (!after250)
                {
                    // Controlled CI diagnostics only. No alternate behavior is
                    // shipped to users until a variant is verified.
                    TestContext.WriteLine("Test raw flag variants on visible Explorer HWND.");
                    bool noActivationFlag = SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0003);
                    int noActivationErr = Marshal.GetLastWin32Error();
                    Thread.Sleep(250);
                    TestContext.WriteLine("No-SWP_NOACTIVATE accepted=" + noActivationFlag +
                        " error=" + noActivationErr + " topmost=" + manager.ReadTopMost(hwnd));
                    SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, 0x0013);

                    bool withShow = SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0053);
                    int showErr = Marshal.GetLastWin32Error();
                    Thread.Sleep(250);
                    TestContext.WriteLine("With-SWP_SHOWWINDOW accepted=" + withShow +
                        " error=" + showErr + " topmost=" + manager.ReadTopMost(hwnd));
                    SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, 0x0013);

                    bool foreground = SetForegroundWindow(hwnd);
                    TestContext.WriteLine("SetForegroundWindow accepted=" + foreground +
                        " nowForeground=" + GetForegroundWindow());
                    bool afterForeground = SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    int foregroundErr = Marshal.GetLastWin32Error();
                    Thread.Sleep(250);
                    TestContext.WriteLine("After foreground accepted=" + afterForeground +
                        " error=" + foregroundErr + " topmost=" + manager.ReadTopMost(hwnd));
                    SetWindowPos(hwnd, new IntPtr(-2), 0, 0, 0, 0, 0x0013);

                    bool async = SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x4013);
                    int asyncErr = Marshal.GetLastWin32Error();
                    Thread.Sleep(300);
                    TestContext.WriteLine("Async SetWindowPos accepted=" + async +
                        " error=" + asyncErr + " topmost=" + manager.ReadTopMost(hwnd));
                }

                Assert.IsTrue(accepted && after250,
                    "Explorer baseline failed: check diagnostic flag variants in test output.");

                // Exercise the actual list row -> PropertyChanged -> MainService
                // path, not just the native API. An Explorer-only UI regression
                // can otherwise slip past the native integration tests.
                if (accepted && after250)
                {
                    Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                    using (var service = new MainService())
                    {
                        var row = service.GetWindowListData()
                            .FirstOrDefault(item => item.HWnd == hwnd && !item.IsRemoved);
                        Assert.IsNotNull(row, "Explorer HWND was not enumerated by the Stealth window list.");
                        Assert.IsFalse(row.IsTopMost, "Explorer should begin unpinned for the UI test.");
                        row.IsTopMost = true; // exactly the data-binding operation on checkbox click
                        Thread.Sleep(250);
                        Assert.IsTrue(manager.ReadTopMost(hwnd),
                            "MainService property-change route failed to pin visible Explorer.");
                        Assert.IsNull(row.TopMostError, "MainService returned an error: " + row.TopMostError);
                    } // RestoreAll via MainService.Dispose()
                    Assert.IsFalse(manager.ReadTopMost(hwnd),
                        "Closing MainService did not restore Explorer's original topmost state.");
                }
            }
            finally
            {
                manager.RestoreAll();
                if (hwnd != IntPtr.Zero)
                    PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
