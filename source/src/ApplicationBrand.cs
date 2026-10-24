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

namespace FileCompass
{
    public enum ApplicationIcon { Compass, Panels, Route, Night, FloppyRoute, FloppyOrbit, FloppyClassic }
    internal static class ApplicationBrand
    {
        internal const string Name = "File Compass";
        internal const string Developer = "Rathousky.pro";
        internal const string GitHubUrl = "https://github.com/rathousky-pro/file-compass";
        static ImageSource developerLogo;
        internal static ImageSource DeveloperLogo { get {
            if(developerLogo!=null)return developerLogo;
            using(var stream=typeof(ApplicationBrand).Assembly.GetManifestResourceStream("FileCompass.Branding.RathouskyPro.png")) {
                if(stream==null)throw new IOException("Missing developer logo resource.");
                var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();developerLogo=image;return image;
            }
        } }
        internal static ApplicationIcon Current = Valid(ToolbarThemeSettings.Load().ApplicationIcon);
        static readonly Dictionary<ApplicationIcon, ImageSource> images = new Dictionary<ApplicationIcon, ImageSource>();
        static readonly Dictionary<Window,IntPtr[]> nativeIcons = new Dictionary<Window,IntPtr[]>();
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr parameter,IntPtr value);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string name, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadImage(IntPtr module, IntPtr name, uint type, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        internal static ApplicationIcon Valid(ApplicationIcon value) { return Enum.IsDefined(typeof(ApplicationIcon), value) ? value : ApplicationIcon.FloppyOrbit; }
        internal static ImageSource Icon { get { return For(Current); } }
        internal static void Attach(Window window)
        {
            SetCurrentProcessExplicitAppUserModelID("RathouskyPro.FileCompass");
            window.Icon=Icon;
            window.SourceInitialized+=delegate{SetNativeIcons(window);};
        }
        static void SetNativeIcons(Window window)
        {
            IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;
            string path=Path.Combine(AppPaths.Resource("icons"),"FileCompass.Icons.dll");
            IntPtr module=LoadLibraryEx(path,IntPtr.Zero,0x02|0x20);if(module==IntPtr.Zero)throw new IOException(L.Text("icons.invalidLibrary"));
            IntPtr small=IntPtr.Zero,large=IntPtr.Zero;
            try{
                small=LoadImage(module,new IntPtr((int)Current+1),1,16,16,0);
                large=LoadImage(module,new IntPtr((int)Current+1),1,32,32,0);
                if(small==IntPtr.Zero||large==IntPtr.Zero)throw new IOException(L.Text("icons.invalidLibrary"));
                SendMessage(hwnd,0x80,IntPtr.Zero,small);SendMessage(hwnd,0x80,new IntPtr(1),large);
                IntPtr[] previous;
                if(nativeIcons.TryGetValue(window,out previous)){foreach(var icon in previous)DestroyIcon(icon);}
                else window.Closed+=delegate{IntPtr[] owned;if(nativeIcons.TryGetValue(window,out owned)){nativeIcons.Remove(window);foreach(var icon in owned)DestroyIcon(icon);}};
                nativeIcons[window]=new[]{small,large};small=large=IntPtr.Zero;
            }finally{if(small!=IntPtr.Zero)DestroyIcon(small);if(large!=IntPtr.Zero)DestroyIcon(large);FreeLibrary(module);}
        }
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
            if (Application.Current != null) foreach (Window window in Application.Current.Windows) { window.Icon = image; SetNativeIcons(window); }
        }
    }
}


