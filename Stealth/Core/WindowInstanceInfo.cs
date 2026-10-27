using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using System.Diagnostics;
using System.Drawing;

namespace Stealth.Core
{
    public class WindowInstanceInfo : IDisposable
    {
        #region Basic Info

        /// <summary>
        /// Window hWnd
        /// </summary>
        internal readonly HWND HWnd;

        /// <summary>
        /// Window Title
        /// </summary>
        public string Title;
        public uint ProcessId;
        public uint ThreadId;

        /// <summary>
        /// Is this window Visible or not
        /// </summary>
        public bool IsVisible;

        /// <summary>
        /// User32.WINDOWINFO, contains window information
        /// </summary>
        // Unused WINDOWINFO storage removed along with the invalid GetWindowInfo call.
        #endregion

        #region Detailed Info
        public bool IsTopMost { get; private set; }
        public bool IsLayered { get; private set; }
        public uint CrKey { get; private set; }
        public byte BAlpha { get; private set; }
        public uint DwFlags { get; private set; }

        public Process process
        {
            get;
            set;
        }

        public Bitmap iconBitmap
        {
            get;
            private set;
        }

        #endregion

        internal WindowInstanceInfo(HWND hWnd) : base()
        {
            this.HWnd = hWnd;
            GetBasicInfo();
        }

        public unsafe static WindowInstanceInfo CreateFromHandle(IntPtr hWnd, bool getDetailedInfo = false, bool includeProcessDetails = true)
        {
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            var info = new WindowInstanceInfo(new HWND((void*)hWnd));
            if (getDetailedInfo)
            {
                info.GetDetailedInfo(includeProcessDetails);
            }

            return info;
        }

        /// <summary>
        /// Get basic info from window
        /// </summary>
        public void GetBasicInfo()
        {
            IsVisible = PInvoke.IsWindowVisible(HWnd);
            // Query the title length first instead of silently truncating at 255.
            // The limit prevents hostile or buggy windows from demanding huge buffers.
            var length = Math.Min(16383, Math.Max(0, PInvoke.GetWindowTextLength(HWnd)));
            if (length > 0)
            {
                char[] chars = new char[length + 1];
                int copied = PInvoke.GetWindowText(HWnd, chars.AsSpan());
                Title = copied > 0 ? new string(chars, 0, Math.Min(copied, length)) : string.Empty;
            }
            else Title = string.Empty;
            ThreadId = PInvoke.GetWindowThreadProcessId(HWnd, out ProcessId);
        }

        /// <summary>
        /// Get more details from a window
        /// </summary>
        public void GetDetailedInfo(bool includeProcessDetails = true)
        {
            //windowInfo.dwExStyle: you must set WS_EX_LAYERED before using window alpha
            //transparency  https://msdn.microsoft.com/en-us/library/windows/desktop/ms632599(v=vs.85).aspx#layered

            // Get opacity
            COLORREF tempCrKey = default;
            LAYERED_WINDOW_ATTRIBUTES_FLAGS tempDwFlags = 0;
            Span<byte> tempBAlpha = stackalloc byte[1];
            tempBAlpha[0] = byte.MaxValue;
            var layeredWindowAttributesLoaded = PInvoke.GetLayeredWindowAttributes(HWnd, out tempCrKey, tempBAlpha, out tempDwFlags);
            CrKey = tempCrKey;
            BAlpha = layeredWindowAttributesLoaded ? tempBAlpha[0] : byte.MaxValue;
            DwFlags = layeredWindowAttributesLoaded ? (uint)tempDwFlags : 0u;

            // Get IsLayered. Opacity works when IsLayered = true
            var extendedStyle = PInvoke.GetWindowLong(HWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            IsLayered = (extendedStyle & (int)WINDOW_EX_STYLE.WS_EX_LAYERED) != 0;
            IsTopMost = (extendedStyle & (int)WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;

            if (!includeProcessDetails)
            {
                return;
            }

            try
            {
                uint processId = 0;
                PInvoke.GetWindowThreadProcessId(HWnd, out processId);
                if (processId > 0)
                {
                    process = Process.GetProcessById((int)processId);
                }
            }
            catch (ArgumentException)
            {
                // normally caused by process not running
            }
            catch (Exception)
            {
                // other unknown exceptions
            }

            GetIcon();
        }


        /// <summary>
        /// Get the icon from process
        /// </summary>
        public void GetIcon()
        {
            if (process == null)
            {
                DisposeIconBitmap();
                return;
            }

            // https://stackoverflow.com/a/23978207/2075611
            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName))
                {
                    ReplaceIconBitmap(icon != null
                        ? icon.ToBitmap()
                        : Bitmap.FromHicon(SystemIcons.Application.Handle));
                }
            }
            catch (Exception ex)
            {
                // expected errors if there is no icon or the process is 64-bit
                if (ex is ArgumentException || ex is System.ComponentModel.Win32Exception)
                {
                    ReplaceIconBitmap(Bitmap.FromHicon(SystemIcons.Application.Handle));
                }
                else
                {
                    //iconBitmap = Bitmap.FromHicon(SystemIcons.Error.Handle);
                }
            }
        }

        private void ReplaceIconBitmap(Bitmap newBitmap)
        {
            DisposeIconBitmap();
            iconBitmap = newBitmap;
        }

        private void DisposeIconBitmap()
        {
            if (iconBitmap != null)
            {
                iconBitmap.Dispose();
                iconBitmap = null;
            }
        }

        public void Dispose()
        {
            DisposeIconBitmap();
            // Process.GetProcessById creates a disposable Process wrapper.
            // Refresh repeatedly constructs snapshots, so release the OS handle.
            process?.Dispose();
            process = null;
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return $"hWnd={HWnd}, title={Title}, isVisible={IsVisible}, crKey={CrKey}, bAlpha={BAlpha}, dwFlags={DwFlags}, isLayered={IsLayered}";
        }
    }
}
