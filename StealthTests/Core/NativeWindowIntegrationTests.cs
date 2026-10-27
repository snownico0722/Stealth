
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;

namespace Stealth.Core.Tests
{
    [TestClass]
    public class NativeWindowIntegrationTests
    {
        private const uint WsVisible = 0x10000000;
        private const uint WsOverlappedWindow = 0x00CF0000;

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string title,
            uint style, int x, int y, int width, int height,
            IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        private static void WithNativeWindow(Action<WindowStateManager, IntPtr> action)
        {
            // Built-in STATIC is registered by User32. A plain native HWND
            // avoids WPF's Window.Topmost and layered-style synchronization,
            // which can legitimately undo external native changes.
            var hwnd = CreateWindowEx(0, "STATIC", "Stealth native test",
                WsVisible | WsOverlappedWindow, 40, 40, 320, 180,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Assert.AreNotEqual(IntPtr.Zero, hwnd,
                "CreateWindowEx failed: " + Marshal.GetLastWin32Error());
            try
            {
                var manager = new WindowStateManager(new NativeWindowStateApi(), 0);
                action(manager, hwnd);
                manager.RestoreAll();
            }
            finally
            {
                DestroyWindow(hwnd);
            }
        }

        [TestMethod]
        public void Win32_LayeredOpacityCanBeRevertedOnNativeTopLevelWindow()
        {
            WithNativeWindow((manager, hwnd) =>
            {
                var original = manager.ReadLayered(hwnd);
                Assert.IsTrue(manager.TrySetOpacity(hwnd, 170, out var error), error);
                Assert.IsTrue(manager.IsModified(hwnd));
                Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                Assert.AreEqual(original, manager.ReadLayered(hwnd));
            });
        }

        [TestMethod]
        public void Win32_TopmostCanBeVerifiedAndRestoredOnNativeTopLevelWindow()
        {
            WithNativeWindow((manager, hwnd) =>
            {
                var original = manager.ReadTopMost(hwnd);
                Assert.IsTrue(manager.TrySetTopMost(hwnd, !original, out var error), error);
                Assert.AreEqual(!original, manager.ReadTopMost(hwnd));
                Assert.IsTrue(manager.TryRestore(hwnd, out error), error);
                Assert.AreEqual(original, manager.ReadTopMost(hwnd));
            });
        }
    }
}
