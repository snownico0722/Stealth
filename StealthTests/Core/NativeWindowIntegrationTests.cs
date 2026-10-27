
using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;

namespace Stealth.Core.Tests
{
    [TestClass]
    public class NativeWindowIntegrationTests
    {
        private static void WithWpfWindow(Action<WindowStateManager, IntPtr> action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                Window window = null;
                try
                {
                    window = new Window {
                        Title = "Stealth integration test", Width = 320, Height = 180,
                        ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual
                    };
                    window.Show();
                    var hwnd = new WindowInteropHelper(window).Handle;
                    Assert.AreNotEqual(IntPtr.Zero, hwnd);
                    // This is a deliberately owned test window. Production manager
                    // correctly denies operations on Stealth's own PID.
                    var manager = new WindowStateManager(new NativeWindowStateApi(), 0);
                    action(manager, hwnd);
                    manager.RestoreAll();
                }
                catch (Exception e) { failure = e; }
                finally { window?.Close(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Window API integration test timed out");
            if (failure != null) throw failure;
        }

        [TestMethod]
        public void Win32_LayeredOpacityCanBeRevertedOnRealWindow()
        {
            WithWpfWindow((manager, hwnd) =>
            {
                var original = manager.ReadLayered(hwnd);
                Assert.IsTrue(manager.TrySetOpacity(hwnd, 170, out var error), error);
                Assert.IsTrue(manager.IsModified(hwnd));
                Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                Assert.AreEqual(original, manager.ReadLayered(hwnd));
            });
        }

        [TestMethod]
        public void Win32_TopmostCanBeVerifiedAndRestoredOnRealWindow()
        {
            WithWpfWindow((manager, hwnd) =>
            {
                bool wasTop = manager.ReadTopMost(hwnd);
                Assert.IsTrue(manager.TrySetTopMost(hwnd, !wasTop, out var error), error);
                Assert.AreEqual(!wasTop, manager.ReadTopMost(hwnd));
                Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                Assert.AreEqual(wasTop, manager.ReadTopMost(hwnd));
            });
        }

    }
}
