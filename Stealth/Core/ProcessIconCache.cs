
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Stealth.Core
{
    // Cache icons at process/executable level rather than extracting them
    // for every visible window on every refresh.
    internal sealed class ProcessIconCache
    {
        internal static ProcessIconCache Shared { get; } = new ProcessIconCache();
        private readonly Dictionary<uint, ImageSource> perProcess = new Dictionary<uint, ImageSource>();
        private readonly Dictionary<string, ImageSource> perExecutable =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private ImageSource fallback;

        internal ImageSource ForProcess(uint processId)
        {
            if (processId == 0) return Fallback();
            if (perProcess.TryGetValue(processId, out var cached)) return cached;
            ImageSource icon = null;
            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    var path = process.MainModule.FileName;
                    // Include file modification time to invalidate replaced executables.
                    var key = path + "|" + System.IO.File.GetLastWriteTimeUtc(path).Ticks;
                    if (!perExecutable.TryGetValue(key, out icon))
                    {
                        using (var appIcon = Icon.ExtractAssociatedIcon(path))
                        using (var bitmap = (appIcon ?? SystemIcons.Application).ToBitmap())
                            icon = ToFrozenImage(bitmap);
                        if (icon != null)
                        {
                            if (perExecutable.Count >= 128) perExecutable.Clear();
                            perExecutable[key] = icon;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.ComponentModel.Win32Exception ||
                                       ex is System.InvalidOperationException || ex is System.IO.IOException ||
                                       ex is UnauthorizedAccessException)
            {
                // Protected processes and rapidly exiting windows are normal.
            }
            icon = icon ?? Fallback();
            perProcess[processId] = icon;
            return icon;
        }

        internal void Prune(ISet<uint> liveProcesses)
        {
            foreach (var pid in perProcess.Keys.Where(pid => !liveProcesses.Contains(pid)).ToArray())
                perProcess.Remove(pid);
        }

        private ImageSource Fallback()
        {
            if (fallback == null)
            {
                using (var bitmap = SystemIcons.Application.ToBitmap())
                    fallback = ToFrozenImage(bitmap);
            }
            return fallback;
        }

        private static BitmapSource ToFrozenImage(Bitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            try
            {
                var image = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            finally
            {
                PInvoke.DeleteObject((HGDIOBJ)handle);
            }
        }
    }
}
