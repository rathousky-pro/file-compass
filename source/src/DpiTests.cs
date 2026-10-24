using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace FileCompass {
    internal static class DpiTests {
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
        internal static int Run(string root) {
            Directory.CreateDirectory(root); Window window=null;
            try {
                window=new MainWindow(root,new ProgramSettings{AutoRefresh=false}) {ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                window.Show();window.UpdateLayout();
                var handle=new WindowInteropHelper(window).Handle;
                if(!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(handle),new IntPtr(-4)))throw new Exception("Window must use Per-Monitor V2 awareness.");
                var dpi=GetDpiForWindow(handle);var scale=VisualTreeHelper.GetDpi(window);
                if(Math.Abs(scale.DpiScaleX-dpi/96.0)>.01)throw new Exception("WPF scale differs from native window DPI.");
                if(AppContextSwitch())throw new Exception("Legacy WPF DPI scaling is enabled.");
                File.WriteAllText(Path.Combine(root,"dpi-result.txt"),"PASS: Native window is Per-Monitor V2; WPF and native DPI match ("+dpi+"); legacy DPI scaling disabled.");
                return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"dpi-result.txt"),"FAIL: "+ex);return 1;}
            finally{if(window!=null)window.Close();}
        }
        static bool AppContextSwitch() { bool legacy; return AppContext.TryGetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges",out legacy)&&legacy; }
    }
}

