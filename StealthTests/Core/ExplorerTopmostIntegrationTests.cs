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

        private static HashSet<IntPtr> ExplorerWindows()
        {
            var found = new HashSet<IntPtr>();
            EnumWindows((hwnd, data) =>
            {
                var buffer = new StringBuilder(128);
                GetClassName(hwnd, buffer, buffer.Capacity);
                if (string.Equals(buffer.ToString(), "CabinetWClass", StringComparison.Ordinal))
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

                bool initial = manager.ReadTopMost(hwnd);
                Assert.IsTrue(manager.TrySetTopMost(hwnd, true, out string error),
                    "Explorer SetWindowPos failed: " + error + " HWND=" + hwnd);
                Thread.Sleep(250);
                Assert.IsTrue(manager.ReadTopMost(hwnd),
                    "Explorer accepted SetWindowPos but did not retain WS_EX_TOPMOST after 250ms.");
                Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                Assert.AreEqual(initial, manager.ReadTopMost(hwnd));
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
