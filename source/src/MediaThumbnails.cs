using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    internal static class MediaThumbnails
    {
        internal static ImageSource Fallback(string path)
        {
            var drawing = new DrawingGroup(); using (var dc = drawing.Open()) {
                dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(182, 136, 247), Color.FromRgb(96, 68, 182), 90), null, new Rect(0, 0, 96, 72), 9, 9);
                if (FileViewers.IsVideo(path)) dc.DrawGeometry(Brushes.White, null, Geometry.Parse("M38,20 L66,36 L38,52 Z"));
                else { var pen = new Pen(Brushes.White, 5); dc.DrawGeometry(null, pen, Geometry.Parse("M40,48 L40,20 L65,15 L65,42 M40,26 L65,21")); dc.DrawEllipse(Brushes.White, null, new Point(33, 49), 9, 6); dc.DrawEllipse(Brushes.White, null, new Point(58, 43), 9, 6); }
            } drawing.Freeze(); var image = new DrawingImage(drawing); image.Freeze(); return image;
        }
        static object Call(object target, string method, params object[] args) { return target.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args); }
        internal static string Details(string path)
        {
            object shell = null, folder = null, item = null;
            try {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); folder = Call(shell, "NameSpace", System.IO.Path.GetDirectoryName(path)); if (folder == null) return "";
                item = Call(folder, "ParseName", System.IO.Path.GetFileName(path)); if (item == null) return "";
                object duration = Call(item, "ExtendedProperty", "System.Media.Duration"); long ticks; string detail = Int64.TryParse(Convert.ToString(duration), out ticks) && ticks > 0 ? TimeSpan.FromTicks(ticks).ToString(@"hh\:mm\:ss") : "";
                var artist = Call(item, "ExtendedProperty", "System.Music.Artist") as string[];
                if (artist != null && artist.Length > 0) detail += " · " + String.Join(", ", artist);
                object width = Call(item, "ExtendedProperty", "System.Video.FrameWidth"), height = Call(item, "ExtendedProperty", "System.Video.FrameHeight");
                if (!String.IsNullOrEmpty(Convert.ToString(width)) && Convert.ToString(width) != "0") detail += " · " + width + " × " + height;
                return detail.Trim(' ', '·');
            } catch (Exception) { return ""; }
            finally { foreach (object obj in new[] { item, folder, shell }) if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj); }
        }
        [StructLayout(LayoutKind.Sequential)] struct Size { public int Width, Height; }
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageFactory { [PreserveSig] int GetImage(Size size, uint flags, out IntPtr bitmap); }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageFactory factory);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
        internal static ImageSource Load(string path)
        {
            IImageFactory factory = null; IntPtr bitmap = IntPtr.Zero;
            try {
                var iid = typeof(IImageFactory).GUID; SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
                if (factory.GetImage(new Size { Width = 240, Height = 180 }, 8, out bitmap) < 0 || bitmap == IntPtr.Zero) return null;
                var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); return image;
            } finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); if (factory != null) Marshal.ReleaseComObject(factory); }
        }
    }
}

