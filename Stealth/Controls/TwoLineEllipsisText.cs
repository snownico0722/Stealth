
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Stealth.Controls
{
    // WPF TextBlock.TextTrimming + MaxHeight merely clips overflowing lines.
    // FormattedText provides an actual two-line truncating renderer.
    public sealed class TwoLineEllipsisText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(TwoLineEllipsisText),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(TwoLineEllipsisText),
                new FrameworkPropertyMetadata(Brushes.Black,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(TwoLineEllipsisText),
                new FrameworkPropertyMetadata(14.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        internal FormattedText CreateLayout(double availableWidth)
        {
            // WPF renders in DIPs, and this constructor accounts for the actual DPI.
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(SystemFonts.MessageFontFamily,
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var formatted = new FormattedText(Text ?? string.Empty, CultureInfo.CurrentUICulture,
                FlowDirection, typeface, FontSize, Foreground ?? Brushes.Black, dpi)
            {
                MaxLineCount = 2,
                Trimming = TextTrimming.CharacterEllipsis,
                MaxTextWidth = Math.Max(1, availableWidth),
                LineHeight = FontSize * 1.35,
            };
            return formatted;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 400 : Math.Max(1, availableSize.Width);
            var layout = CreateLayout(width);
            return new Size(Math.Min(width, Math.Ceiling(layout.WidthIncludingTrailingWhitespace)),
                Math.Min(36, Math.Ceiling(layout.Height)));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (RenderSize.Width < 1 || RenderSize.Height < 1) return;
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            dc.DrawText(CreateLayout(RenderSize.Width), new Point(0, 0));
            dc.Pop();
        }
    }
}
