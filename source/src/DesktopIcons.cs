using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace FileCompass
{
    public static partial class DesktopIcons
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ShellInfo
        {
            public IntPtr Icon; public int Index; public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShellInfo info, uint size, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        static readonly Dictionary<string, ImageSource> files = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource FileIcon(string path, bool folder)
        {
            if (folder) return FluentIcons.Load("folder");
            lock (files) {
                string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
                bool actual = !String.IsNullOrEmpty(path) && File.Exists(path);
                bool individual = extension == ".exe" || extension == ".dll" || extension == ".lnk" || extension == ".ico" || extension == ".url";
                string key = (actual ? "local:" : "type:") + (actual && individual ? Path.GetFullPath(path) : extension);
                ImageSource result; if (files.TryGetValue(key, out result)) return result;
                ShellInfo info; SHGetFileInfo(actual ? path : "file" + extension, actual ? 0u : 128u, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x100 | 0x1 | (actual ? 0u : 0x10u));
                if (info.Icon != IntPtr.Zero) {
                    try { result = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); result.Freeze(); }
                    finally { DestroyIcon(info.Icon); }
                }
                ShellInfo generic; SHGetFileInfo("file.filecompass_unknown_type",128,out generic,(uint)Marshal.SizeOf(typeof(ShellInfo)),0x100|0x1|0x10);
                if(generic.Icon!=IntPtr.Zero)DestroyIcon(generic.Icon);
                if(result==null || info.Index==generic.Index) {
                    var registered=RegisteredFileIcon(extension,actual?path:null);
                    if(registered!=null)result=registered;
                    else if(new[]{".rar",".zip",".7z",".tar",".gz",".bz2",".xz",".arj",".lzh",".cab",".iso"}.Contains(extension))result=BuildLegacyDrawing("pack");
                    else if(new[]{".md",".markdown",".txt",".log",".csv",".ini",".xml",".json",".yaml",".yml",".cs",".cpp",".h",".py",".js",".ts",".ps1",".bat",".cmd",".sql"}.Contains(extension))result=BuildLegacyDrawing("notepad");
                    else if(new[]{".jpg",".jpeg",".png",".gif",".bmp",".tif",".tiff",".webp",".svg"}.Contains(extension))result=BuildLegacyDrawing("picture");
                }
                if(result==null)result=Drawing("document");
                if(!result.IsFrozen && result.CanFreeze)result.Freeze();
                if(files.Count>=1024)files.Clear();files[key]=result;return result;
            }
        }

        public static Image Toolbar(string name, double size)
        {
            return new Image { Source = ToolbarDrawing(name, ToolbarThemeSettings.Current), Tag = name, Width = size, Height = size, SnapsToDevicePixels = true };
        }

        public static ImageSource Drawing(string name) { return name == "app" ? ApplicationBrand.Icon : IconLibrary.Action(name, ToolbarThemeSettings.Current); }
        public static ImageSource BuildLegacyDrawing(string name)
        {
            if (name == "app") return ApplicationBrand.Icon;
            var group = new DrawingGroup();
            using (var c = group.Open())
            {
                c.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
                var blue = Brush("#17A5DC"); var edge = new Pen(Brush("#6E8593"), .8);
                if (name == "back" || name == "forward")
                {
                    c.DrawEllipse(blue, null, new Point(16, 16), 14, 14);
                    c.PushTransform(new RotateTransform(name == "forward" ? 180 : 0, 16, 16));
                    Shape(c, "M 6,16 L 15,8 L 15,13 L 25,13 L 25,19 L 15,19 L 15,24 Z", Brushes.White, null); c.Pop();
                }
                else if (name == "refresh")
                {
                    Shape(c, "M 25,11 C 19,-2 3,4 4,17 M 7,21 C 13,34 29,28 28,15", null, new Pen(blue, 3));
                    Shape(c, "M 18,10 L 27,14 L 28,4 Z M 14,22 L 5,18 L 4,28 Z", blue, null);
                }
                else if (name == "search")
                {
                    c.DrawEllipse(Brush("#DDF3FC"), new Pen(Brush("#7299AE"), 2), new Point(20, 12), 9, 9);
                    c.DrawLine(new Pen(Brush("#A5825B"), 5), new Point(13, 20), new Point(3, 30));
                }
                else if (name == "picture")
                {
                    c.DrawRectangle(Brush("#8FD8F1"), edge, new Rect(2, 3, 28, 25));
                    c.DrawEllipse(Brush("#FFE260"), null, new Point(10, 10), 4, 4);
                    Shape(c, "M 3,27 L 12,16 L 19,20 L 25,12 L 30,27 Z", Brush("#4DAA42"), null);
                }
                else if (name == "pack" || name == "unpack" || name == "archive-test")
                {
                    c.DrawRectangle(Brush("#C9904F"), edge, new Rect(4, 12, 24, 17));
                    c.DrawRectangle(Brush("#ECCC98"), null, new Rect(14, 13, 5, 16));
                    if (name == "archive-test") Shape(c, "M 7,12 L 12,17 L 24,3 L 29,7 L 12,25 L 3,16 Z", Brush("#54AD35"), null);
                    else if (name == "pack") Shape(c, "M 13,1 L 20,1 L 20,9 L 26,9 L 16,19 L 7,9 L 13,9 Z", Brush("#79B049"), null);
                    else Shape(c, "M 13,20 L 20,20 L 20,10 L 26,10 L 16,0 L 7,10 L 13,10 Z", Brush("#DE7158"), null);
                }
                else if (name == "network" || name == "ftp" || name == "url")
                {
                    c.DrawLine(new Pen(blue, 2), new Point(6, 24), new Point(26, 24));
                    c.DrawLine(new Pen(blue, 2), new Point(16, 12), new Point(16, 25));
                    foreach (double x in new double[] { 1, 22 }) { c.DrawRectangle(Brush("#66BCE9"), edge, new Rect(x, 20, 9, 8)); c.DrawRectangle(Brush("#F1CC51"), null, new Rect(x, 28, 10, 3)); }
                    if (name == "network") c.DrawEllipse(blue, edge, new Point(16, 8), 7, 7);
                    else c.DrawText(new FormattedText(name == "ftp" ? "FTP" : "URL", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, name == "ftp" ? Brushes.Red : blue, 1.0), new Point(3, 0));
                }
                else if (name == "select")
                {
                    var pen = new Pen(Brush("#6C7982"), 1) { DashStyle = DashStyles.Dot };
                    c.DrawRectangle(Brushes.White, edge, new Rect(2, 2, 28, 28)); c.DrawRectangle(null, pen, new Rect(7, 7, 18, 18));
                    c.DrawRectangle(Brush("#567787"), null, new Rect(16, 10, 7, 7));
                }
                else if (name == "sync")
                {
                    Folder(c, 1, 4, 11, 10); Folder(c, 20, 19, 11, 10);
                    Shape(c, "M 13,8 L 23,8 L 23,4 L 31,11 L 23,17 L 23,13 L 13,13 Z", Brush("#79A943"), null);
                    Shape(c, "M 19,24 L 9,24 L 9,29 L 1,22 L 9,16 L 9,20 L 19,20 Z", blue, null);
                }
                else if (name == "rename")
                {
                    c.DrawRoundedRectangle(Brush("#EDF5FC"), edge, new Rect(2, 3, 28, 26), 2, 2);
                    c.DrawText(new FormattedText("A", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 14, Brush("#155A86"), 1), new Point(4, 4));
                    c.DrawText(new FormattedText("B", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 14, Brush("#31914A"), 1), new Point(19, 13));
                    Shape(c, "M5,24 L16,24 L16,20 L22,25 L16,30 L16,27 L5,27 Z", blue, null);
                }
                else if (name == "edit" || name == "notepad" || name == "document")
                {
                    c.DrawRectangle(Brush("#F3F8F4"), edge, new Rect(6, 2, 20, 28));
                    for (int y = 8; y < 28; y += 5) c.DrawLine(new Pen(Brush("#AAC7D3"), 1), new Point(9, y), new Point(24, y));
                    if (name == "edit") { c.DrawLine(new Pen(Brush("#EDB73F"), 5), new Point(7, 5), new Point(25, 23)); Shape(c, "M 23,24 L 29,29 L 26,21 Z", Brushes.DimGray, null); }
                }
                else if (name == "tree" || name == "details")
                {
                    c.DrawRectangle(Brushes.White, edge, new Rect(2, 2, 28, 28));
                    for (int y = 4; y < 30; y += 10) { Folder(c, name == "tree" && y > 4 ? 12 : 4, y, 10, 7); c.DrawLine(edge, new Point(18, y + 4), new Point(28, y + 4)); }
                    if (name == "tree") c.DrawLine(edge, new Point(7, 12), new Point(7, 27));
                }
                else if (name == "favorite")
                {
                    Folder(c, 2, 3, 28, 24);
                    Shape(c, "M16,8 L19,15 L27,15 L21,20 L23,28 L16,23 L9,28 L11,20 L5,15 L13,15 Z", Brush("#50A34E"), null);
                }
                else Folder(c, 2, 5, 28, 24);
            }
            group.Freeze(); return new DrawingImage(group);
        }
        static Brush Brush(string color) { return CompactTheme.ColorBrush(color); }
        static void Shape(DrawingContext c, string path, Brush brush, Pen pen) { c.DrawGeometry(brush, pen, Geometry.Parse(path)); }
        static void Folder(DrawingContext c, double x, double y, double w, double h)
        {
            c.DrawRectangle(Brush("#E9B957"), new Pen(Brush("#CA9B39"), .5), new Rect(x, y, w * .45, h * .3));
            c.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(255, 233, 164), Color.FromRgb(237, 186, 63), 90), new Pen(Brush("#CA9B39"), .5), new Rect(x, y + h * .2, w, h * .8), 1, 1);
        }
    }
}

