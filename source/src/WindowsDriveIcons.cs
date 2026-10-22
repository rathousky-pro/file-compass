using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public static partial class DesktopIcons
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct StockIconInfo
        {
            public uint Size;
            public IntPtr Icon;
            public int SystemIndex, IconIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
        }
        [DllImport("shell32.dll")]
        static extern int SHGetStockIconInfo(int id, uint flags, ref StockIconInfo info);
        static readonly ConcurrentDictionary<int, ImageSource> stockIcons = new ConcurrentDictionary<int, ImageSource>();
        static readonly ConcurrentDictionary<string, ImageSource> driveIcons = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        public static ImageSource StockIcon(int id)
        {
            return stockIcons.GetOrAdd(id, delegate(int key)
            {
                var info = new StockIconInfo { Size = (uint)Marshal.SizeOf(typeof(StockIconInfo)) };
                if (SHGetStockIconInfo(key, 0x100 | 0x1, ref info) != 0 || info.Icon == IntPtr.Zero) { var fallback = Drawing("document"); fallback.Freeze(); return fallback; }
                try { var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(16, 16)); image.Freeze(); return image; }
                finally { DestroyIcon(info.Icon); }
            });
        }
        public static ImageSource WindowsDriveIcon(string root, bool network, bool connected, bool actual = false)
        {
            if (network) return StockIcon(connected ? 9 : 10);
            ImageSource cached; if (driveIcons.TryGetValue(root, out cached)) return cached;
            int type = 8;
            try { var drive = new DriveInfo(root); if (drive.DriveType == DriveType.CDRom) type = 11; else if (drive.DriveType == DriveType.Removable) type = 7; else if (drive.DriveType == DriveType.Ram) type = 12; } catch (IOException) { }
            if (!actual) return StockIcon(type);
            ShellInfo info; SHGetFileInfo(root, 0, out info, (uint)Marshal.SizeOf(typeof(ShellInfo)), 0x100 | 0x1 | 0x20);
            if (info.Icon == IntPtr.Zero) return StockIcon(type);
            try
            {
                var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(16, 16)); image.Freeze(); driveIcons[root] = image; return image;
            }
            finally { DestroyIcon(info.Icon); }
        }
    }
}

