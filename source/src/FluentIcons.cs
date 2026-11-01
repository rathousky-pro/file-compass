using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    // Local, frozen high-resolution assets: no SVG decoder or network access at runtime.
    public static class FluentIcons
    {
        internal static ImageSource TileFolder(bool parent)
        {
            var drawing = new DrawingGroup(); using (var dc = drawing.Open()) {
                if (parent) {
                    dc.DrawEllipse(CompactTheme.ColorBrush("#E7F2FD"), null, new System.Windows.Point(32, 28), 24, 24);
                    dc.DrawGeometry(null, new Pen(CompactTheme.ColorBrush("#0067C0"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Geometry.Parse("M32,42 L32,16 M21,27 L32,16 L43,27"));
                } else {
                    dc.DrawGeometry(CompactTheme.ColorBrush("#E7AB32"), null, Geometry.Parse("M4,13 Q4,8 9,8 L26,8 L32,15 L55,15 Q60,15 60,20 L60,46 L4,46 Z"));
                    dc.DrawRoundedRectangle(CompactTheme.ColorBrush("#FFF2CA"), null, new System.Windows.Rect(9, 19, 46, 24), 2, 2);
                    dc.DrawGeometry(new LinearGradientBrush(System.Windows.Media.Color.FromRgb(255, 221, 109), System.Windows.Media.Color.FromRgb(246, 181, 43), 90), new Pen(CompactTheme.ColorBrush("#E4AB31"), .6), Geometry.Parse("M4,25 L60,25 L56,49 Q56,52 52,52 L9,52 Q5,52 5,48 Z"));
                }
            } drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
        }
        internal static ImageSource Network()
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open()) {
                var blue = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#35C6F4"), (Color)ColorConverter.ConvertFromString("#0067C0"), 90);
                var line = new Pen(CompactTheme.ColorBrush("#5B8DAE"), 2) { LineJoin = PenLineJoin.Round };
                context.DrawGeometry(null, line, Geometry.Parse("M16,12 L16,17 M6,21 L6,17 L26,17 L26,21"));
                foreach (var rect in new[] { new System.Windows.Rect(10, 1, 12, 10), new System.Windows.Rect(0, 21, 12, 10), new System.Windows.Rect(20, 21, 12, 10) }) {
                    context.DrawRoundedRectangle(blue, null, rect, 2, 2);
                    context.DrawLine(new Pen(System.Windows.Media.Brushes.LightCyan, 1.4), new System.Windows.Point(rect.Left + 3, rect.Top + 7), new System.Windows.Point(rect.Right - 3, rect.Top + 7));
                }
            }
            drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
        }
        internal static ImageSource Archive(bool extract)
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open()) {
                context.DrawImage(TileFolder(false), new System.Windows.Rect(0, 0, 28, 28));
                context.DrawEllipse(System.Windows.Media.Brushes.White, null, new System.Windows.Point(24, 23), 8, 8);
                var ink = CompactTheme.ColorBrush(extract ? "#16834A" : "#0067C0");
                var pen = new Pen(ink, 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                context.DrawGeometry(null, pen, Geometry.Parse(extract ? "M24,28 L24,18 M20,22 L24,18 L28,22" : "M24,18 L24,28 M20,24 L24,28 L28,24"));
            }
            drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
        }
        static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        internal static string BuildSourceDirectory = null;
        public static ImageSource Load(string name)
        {
            if (BuildSourceDirectory == null) return IconLibrary.Action(name, ToolbarTheme.Classic);
            lock (cache)
            {
                ImageSource image;
                if (cache.TryGetValue(name, out image)) return image;
                string path = Path.Combine(BuildSourceDirectory, name + ".png");
                if (!File.Exists(path)) return null;
                var bitmap = new BitmapImage();
                using (var stream = File.OpenRead(path))
                {
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit();
                }
                bitmap.Freeze(); cache[name] = bitmap; return bitmap;
            }
        }
    }
}

