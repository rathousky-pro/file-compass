using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

[assembly: AssemblyTitle("File Compass")]
[assembly: AssemblyProduct("File Compass")]
[assembly: AssemblyDescription("Dual-pane file manager for Windows")]

namespace CommanderCZ
{
    public enum ApplicationIcon { Compass, Panels, Route, Night, FloppyRoute, FloppyOrbit, FloppyClassic }
    internal static class ApplicationBrand
    {
        internal const string Name = "File Compass";
        internal static ApplicationIcon Current = Valid(ToolbarThemeSettings.Load().ApplicationIcon);
        static readonly Dictionary<ApplicationIcon, ImageSource> images = new Dictionary<ApplicationIcon, ImageSource>();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string name, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadImage(IntPtr module, IntPtr name, uint type, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        internal static ApplicationIcon Valid(ApplicationIcon value) { return Enum.IsDefined(typeof(ApplicationIcon), value) ? value : ApplicationIcon.FloppyOrbit; }
        internal static ImageSource Icon { get { return For(Current); } }
        internal static ImageSource For(ApplicationIcon value)
        {
            value = Valid(value);
            ImageSource image; if (images.TryGetValue(value, out image)) return image;
            string path = Path.Combine(AppPaths.Resource("icons"), "FileCompass.Icons.dll");
            // Map resources only. DllMain and any executable content are never run.
            IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x02 | 0x20);
            if (module == IntPtr.Zero) throw new IOException(L.Text("icons.invalidLibrary") + "\n" + path);
            try
            {
                IntPtr handle = LoadImage(module, new IntPtr((int)value + 1), 1, 256, 256, 0);
                if (handle == IntPtr.Zero) throw new IOException(L.Text("icons.invalidLibrary"));
                try { var bitmap = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); bitmap.Freeze(); image = bitmap; }
                finally { DestroyIcon(handle); }
            }
            finally { FreeLibrary(module); }
            images[value] = image; return image;
        }
        internal static void Apply(ApplicationIcon value)
        {
            ImageSource image = For(value); Current = Valid(value);
            if (Application.Current != null) foreach (Window window in Application.Current.Windows) window.Icon = image;
        }
    }
}


