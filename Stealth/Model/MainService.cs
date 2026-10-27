using Stealth.Core;
using Stealth.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Stealth.Model
{
    public class MainService : IMainService, IDisposable
    {
        // window info from native user32
        private WindowInstanceUtil util;
        private List<WindowInstanceInfo> windowsInstanceList;
        private readonly WindowStateManager _states = WindowStateManager.Shared;

        // window info for view
        public ObservableCollection<WindowInfoItemModel> windowInfoViewList { get; private set; }

        public MainService()
        {
            util = new WindowInstanceUtil();
            windowInfoViewList = new ObservableCollection<WindowInfoItemModel>();
            RefreshWindowData();
        }


        public ObservableCollection<WindowInfoItemModel> GetWindowListData()
        {
            return windowInfoViewList;
        }


        /// <summary>
        /// Update the WindowInfoItemModel list from known WindowInstanceInfo(native) list.
        /// If a window is no longer exist in WindowInstanceInfo(native) list, 
        /// it will be marked as 'isRemoved' in WindowInfoItemModel list
        /// </summary>
        /// <param name="targetModelList"></param>
        /// <param name="sourceNativeList"></param>
        private void UpdateWindowInfoItemModelList(ObservableCollection<WindowInfoItemModel> targetModelList,
                                                List<WindowInstanceInfo> sourceNativeList)
        {
            // Only live entries participate in matching. A closed historical row
            // may have the same recycled HWND as a new window and must not be reused.
            var active = targetModelList.Where(item => !item.IsRemoved)
                .GroupBy(item => item.HWnd)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var item in active.Values) item.IsRemoved = true;

            var liveProcessIds = new HashSet<uint>();
            foreach (var native in sourceNativeList)
            {
                var hwnd = (IntPtr)native.HWnd;
                liveProcessIds.Add(native.ProcessId);
                WindowInfoItemModel item;
                if (!active.TryGetValue(hwnd, out item) ||
                    item.ProcessId != native.ProcessId || item.ThreadId != native.ThreadId)
                {
                    item = new WindowInfoItemModel();
                    item.PropertyChanged += WindowInfoItemModel_PropertyChanged;
                    targetModelList.Add(item);
                }
                item.CopyFrom(native);
                item.UpdateModificationFromNative(_states.IsModified(hwnd));
                item.IsRemoved = false;
            }

            // A closed, never-modified window must not consume list memory forever.
            // Modified windows are kept as read-only history until restart.
            foreach (var obsolete in targetModelList.Where(item => item.IsRemoved && !item.IsModified).ToArray())
            {
                obsolete.PropertyChanged -= WindowInfoItemModel_PropertyChanged;
                targetModelList.Remove(obsolete);
            }
            ProcessIconCache.Shared.Prune(liveProcessIds);
        }


        public void RefreshWindowData()
        {
            //please note that the UI (ListBoxItem) content is not refershed if updating nested elements
            DisposeWindowSnapshots();

            _states.ForgetClosed();
            windowsInstanceList = util.RetrieveAllWindows(true);
            UpdateWindowInfoItemModelList(windowInfoViewList, windowsInstanceList);
        }

        private void DisposeWindowSnapshots()
        {
            if (windowsInstanceList == null)
            {
                return;
            }

            foreach (var window in windowsInstanceList)
            {
                window.Dispose();
            }

            windowsInstanceList = null;
        }

        public void Dispose()
        {
            foreach (var item in windowInfoViewList)
            {
                item.PropertyChanged -= WindowInfoItemModel_PropertyChanged;
            }

            _states.RestoreAll();
            DisposeWindowSnapshots();
        }


        public void Detail(WindowInfoItemModel item)
        {
            if (item == null)
            {
                return;
            }

            MessageBox.Show(
                Stealth.App.Localize("Detail.Handle") + ": 0x" + item.HWnd.ToInt64().ToString("X") + "\n" +
                Stealth.App.Localize("Detail.Title") + ": " +
                    (string.IsNullOrWhiteSpace(item.Title) ? Stealth.App.Localize("Detail.Empty") : item.Title) + "\n" +
                Stealth.App.Localize("Detail.Opacity") + ": " + item.Opacity + "\n" +
                Stealth.App.Localize("Detail.Topmost") + ": " + item.IsTopMost,
                Stealth.App.Localize("Detail.Caption"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        public void ChangeOpacity(WindowInfoItemModel item)
        {
            Console.WriteLine(string.Format($"Change opacity: {item.Title}, {item.Opacity}"));
            var nativeWindow = windowsInstanceList.SingleOrDefault(window => (IntPtr)window.HWnd == item.HWnd);
            if (nativeWindow != null)
            {
                var opacity = (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, item.Opacity));
                var applied = _states.TrySetOpacity(item.HWnd, opacity, out var diagnostic);
                if (!applied)
                {
                    // The slider changed optimistically; restore the value from
                    // the last native snapshot if Windows denied the change.
                    item.UpdateOpacityFromNative(_states.ReadOpacity(item.HWnd));
                }
                if (applied) item.UpdateModificationFromNative(_states.IsModified(item.HWnd));
                item.OpacityError = diagnostic;
            }
        }

        public void UpdateOpacityFromOverlay(IntPtr handle, byte opacity)
        {
            var item = windowInfoViewList.SingleOrDefault(window => window.HWnd == handle && !window.IsRemoved);
            if (item != null)
            {
                item.UpdateOpacityFromNative(opacity);
                item.UpdateModificationFromNative(_states.IsModified(handle));
            }
        }


        public void RestoreOriginal(WindowInfoItemModel item)
        {
            if (item == null || item.IsRemoved || !item.IsModified) return;
            if (!_states.TryRestore(item.HWnd, out string error))
            {
                item.OpacityError = error;
                return;
            }
            item.UpdateOpacityFromNative(_states.ReadOpacity(item.HWnd));
            item.UpdateTopMostFromNative(_states.ReadTopMost(item.HWnd));
            item.UpdateModificationFromNative(false);
            item.TopMostError = null;
            item.OpacityError = null;
        }

        public void SetTopMost(WindowInfoItemModel item)
        {
            Console.WriteLine(string.Format($"Set TopMost: {item.Title}, {item.IsTopMost}"));
            var nativeWindow = windowsInstanceList.SingleOrDefault(window => (IntPtr)window.HWnd == item.HWnd);
            if (nativeWindow == null)
                return;

            // One direct SetWindowPos request for each click. No deferred
            // verification, timers or automatic retries.
            bool requested = item.IsTopMost;
            if (!_states.TrySetTopMost(item.HWnd, requested, out var error))
            {
                item.UpdateTopMostFromNative(_states.ReadTopMost(item.HWnd));
                item.TopMostError = error;
                return;
            }

            item.TopMostError = null;
            item.UpdateModificationFromNative(_states.IsModified(item.HWnd));
        }

        private void WindowInfoItemModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = sender as WindowInfoItemModel;
            if (item == null || item.IsUpdatingFromNative || item.IsRemoved)
            {
                return;
            }

            if (e.PropertyName == nameof(WindowInfoItemModel.Opacity))
            {
                ChangeOpacity(item);
            }
            else if (e.PropertyName == nameof(WindowInfoItemModel.IsTopMost))
            {
                SetTopMost(item);
            }
        }
    }
}
