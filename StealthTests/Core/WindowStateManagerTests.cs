
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Core;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Stealth.Core.Tests
{
    [TestClass]
    public class WindowStateManagerTests
    {
        private const int Layered = (int)WINDOW_EX_STYLE.WS_EX_LAYERED;
        private const int Topmost = (int)WINDOW_EX_STYLE.WS_EX_TOPMOST;
        private const uint Alpha = (uint)LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA;
        private const uint Key = (uint)LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY;
        private static readonly IntPtr Hwnd = new IntPtr(42);

        [TestMethod]
        public void OrdinaryWindow_RemovesTemporaryLayeredStyle()
        {
            var api = new FakeApi();
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 100, out _));
            Assert.IsTrue(state.IsModified(Hwnd));
            Assert.IsTrue(state.TryRestore(Hwnd, out _));
            Assert.AreEqual(0, api.Style & Layered);
            Assert.IsFalse(state.IsModified(Hwnd));
        }

        [TestMethod]
        public void OriginalLayeredState_RestoresAlphaAndColorKey()
        {
            var api = new FakeApi { Style = Layered, Color = 0x1234, Transparency = 170, Flags = Alpha | Key };
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 100, out _));
            Assert.IsTrue(state.TryRestore(Hwnd, out _));
            Assert.AreEqual((byte)170, api.Transparency);
            Assert.AreEqual((uint)0x1234, api.Color);
            Assert.AreEqual(Alpha | Key, api.Flags);
        }

        [TestMethod]
        public void UnreadableLayeredState_AllowsBestEffortModification()
        {
            var api = new FakeApi { Style = Layered, CanRead = false };
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 120, out var warning), warning);
            Assert.IsTrue(api.DidWriteTransparency);
            StringAssert.Contains(warning, "Best-effort opacity update");
            Assert.IsFalse(state.TryRestore(Hwnd, out var limitation));
            StringAssert.Contains(limitation, "exact restoration is unavailable");
        }

        [TestMethod]
        public void DoesNotBlockWindowsWithMatchingProcessId()
        {
            var api = new FakeApi { Pid = 44 };
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetTopMost(Hwnd, true, out _));
        }

        [TestMethod]
        public void AllowsOverwritingAnExternalOpacityChange()
        {
            var api = new FakeApi();
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 100, out _));
            api.Transparency = 145; // Simulate another program updating its alpha.
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 80, out _));
            Assert.AreEqual((byte)80, api.Transparency);
        }

        [TestMethod]
        public void FailedTopmost_IsNotMarkedModified()
        {
            var api = new FakeApi { CanPin = false };
            var state = new WindowStateManager(api);
            Assert.IsFalse(state.TrySetTopMost(Hwnd, true, out _));
            Assert.IsFalse(state.IsModified(Hwnd));
        }

        [TestMethod]
        public void TopmostChange_IsReverted()
        {
            var api = new FakeApi();
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetTopMost(Hwnd, true, out _));
            state.RestoreAll();
            Assert.AreEqual(0, api.Style & Topmost);
        }

        [TestMethod]
        public void DelayedTopmostApplication_CanBeConfirmedAfterTheApiReturns()
        {
            var api = new FakeApi { DelayTopmostStyle = true };
            var state = new WindowStateManager(api);
            // Windows accepted the request, but has not updated the style yet.
            Assert.IsTrue(state.TrySetTopMost(Hwnd, true, out _));
            Assert.IsFalse(state.ReadTopMost(Hwnd));
            api.ApplyPendingTopmost();
            Assert.IsTrue(state.ReadTopMost(Hwnd));
            Assert.IsTrue(state.IsModified(Hwnd));
        }

        [TestMethod]
        public void UnconfirmedTopmost_IsNotRetainedAsModified()
        {
            var api = new FakeApi { DelayTopmostStyle = true };
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetTopMost(Hwnd, true, out _));
            state.DiscardUnconfirmedTopMost(Hwnd, true);
            Assert.IsFalse(state.IsModified(Hwnd));
        }

        [TestMethod]
        public void RecycledHandle_IsNotRestored()
        {
            var api = new FakeApi();
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 120, out _));
            api.Pid = 55;
            Assert.IsFalse(state.TryRestore(Hwnd, out _));
            Assert.IsFalse(state.IsModified(Hwnd));
        }

        [TestMethod]
        public void PartialRestore_CanBeRetriedWithoutRestoringAlreadyRevertedTopmost()
        {
            var api = new FakeApi { Style = Layered, Transparency = 180, Flags = Alpha };
            var state = new WindowStateManager(api);
            Assert.IsTrue(state.TrySetTopMost(Hwnd, true, out _));
            Assert.IsTrue(state.TrySetOpacity(Hwnd, 100, out _));
            api.FailOriginalTransparencyRestore = true;
            Assert.IsFalse(state.TryRestore(Hwnd, out _));
            Assert.AreEqual(0, api.Style & Topmost);
            Assert.IsTrue(state.IsModified(Hwnd));
            api.FailOriginalTransparencyRestore = false;
            Assert.IsTrue(state.TryRestore(Hwnd, out _));
            Assert.IsFalse(state.IsModified(Hwnd));
        }

        private sealed class FakeApi : IWindowStateApi
        {
            internal int Style;
            internal uint Pid = 44, Tid = 17, Color, Flags;
            internal byte Transparency = 255;
            internal bool CanRead = true, CanPin = true, DidWriteTransparency, FailOriginalTransparencyRestore;
            internal bool DelayTopmostStyle, PendingTopmostValue;
            internal void ApplyPendingTopmost()
            {
                Style = PendingTopmostValue ? Style | Topmost : Style & ~Topmost;
                DelayTopmostStyle = false;
            }
            public bool IsWindow(IntPtr hwnd) => true;
            public void GetIdentity(IntPtr hwnd, out uint pid, out uint tid) { pid = Pid; tid = Tid; }
            public int GetExtendedStyle(IntPtr hwnd) => Style;
            public bool TryGetLayeredAttributes(IntPtr hwnd, out uint key, out byte alpha, out uint flags, out int error)
            {
                key = Color; alpha = Transparency; flags = Flags; error = CanRead ? 0 : 5; return CanRead;
            }
            public bool TrySetExtendedStyle(IntPtr hwnd, int style, out int error)
            {
                Style = style; error = 0; return true;
            }
            public bool TrySetLayeredAttributes(IntPtr hwnd, uint key, byte alpha, uint flags, out int error)
            {
                if (FailOriginalTransparencyRestore && alpha == 180) {
                    error = 5;
                    return false;
                }
                Color = key; Transparency = alpha; Flags = flags; DidWriteTransparency = true;
                error = 0; return true;
            }
            public bool TrySetTopMost(IntPtr hwnd, bool enabled, out string error)
            {
                error = CanPin ? null : "Access denied";
                if (CanPin)
                {
                    PendingTopmostValue = enabled;
                    if (!DelayTopmostStyle)
                        Style = enabled ? Style | Topmost : Style & ~Topmost;
                }
                return CanPin;
            }
        }
    }
}
