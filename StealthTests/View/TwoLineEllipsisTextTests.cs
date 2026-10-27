
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stealth.Controls;

namespace Stealth.View.Tests
{
    [TestClass]
    public class TwoLineEllipsisTextTests
    {
        [TestMethod]
        public void LongChineseAndEnglishTitles_RenderInsideTwoLines()
        {
            Exception failure = null;
            int painted = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    var control = new TwoLineEllipsisText {
                        Text = "一个非常长的中文进程窗口标题 - C:\\\\Documents\\\\" +
                            new string('W', 140) + ".txt",
                        Width = 160, Height = 36, FontSize = 14, Foreground = Brushes.Black
                    };
                    control.Measure(new Size(160, 36));
                    control.Arrange(new Rect(0, 0, 160, 36));
                    var layout = control.CreateLayout(160);
                    Assert.AreEqual(2, layout.MaxLineCount);
                    Assert.AreEqual(TextTrimming.CharacterEllipsis, layout.Trimming);
                    var image = new RenderTargetBitmap(160, 36, 96, 96, PixelFormats.Pbgra32);
                    image.Render(control);
                    var path = Environment.GetEnvironmentVariable("STEALTH_VISUAL_TEST_DIR");
                    if (!string.IsNullOrEmpty(path))
                    {
                        Directory.CreateDirectory(path);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        using (var file = File.Create(Path.Combine(path, "two-line-ellipsis-160px.png")))
                            encoder.Save(file);
                    }
                    var pixels = new byte[160 * 36 * 4];
                    image.CopyPixels(pixels, 160 * 4, 0);
                    for (int i = 3; i < pixels.Length; i += 4)
                        if (pixels[i] > 0) painted++;
                }
                catch (Exception e) { failure = e; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "WPF rendering timed out");
            if (failure != null) throw failure;
            Assert.IsTrue(painted > 10, "No text was rendered");
        }
    }
}
