using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace FileCompass {
    // DWM owns the material. Only the outer window receives a tint; cards stay opaque.
    internal static class WindowGlass {
        static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(WindowGlass),new PropertyMetadata(false));
        sealed class NativeState{internal IntPtr Handle;internal bool Requested;}
        static readonly DependencyProperty NativeProperty=DependencyProperty.RegisterAttached("Native",typeof(NativeState),typeof(WindowGlass));
        static readonly Brush tint=Tint();
        static Brush Tint(){var brush=new SolidColorBrush(Color.FromArgb(190,207,222,236));brush.Freeze();return brush;}
        internal static bool Enabled(Window window){return (bool)window.GetValue(EnabledProperty);}
        internal static Brush Background(Window window){return Enabled(window)?tint:CompactTheme.Chrome;}
        internal static Brush Layer(Window window){return Enabled(window)?Brushes.Transparent:CompactTheme.Chrome;}
#if APP_TESTS
        internal static bool ForceOpaqueForTests;
#endif
        static bool Allowed(){
#if APP_TESTS
            if(ForceOpaqueForTests)return false;
#endif
            if(SystemParameters.HighContrast)return false;
            try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key==null||Convert.ToInt32(key.GetValue("EnableTransparency",1))!=0;}
            catch(System.Security.SecurityException){return false;}
        }
        internal static void Apply(Window window,bool force=false){
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            bool requested=Allowed();var previous=window.GetValue(NativeProperty) as NativeState;
            if(!force&&previous!=null&&previous.Handle==handle&&previous.Requested==requested){window.Background=Background(window);return;}
            bool enabled=false;
            try{
                int kind=requested?3:1; // DWMSBT_TRANSIENTWINDOW: Desktop Acrylic, Windows 11 22H2+.
                enabled=kind==3&&DwmSetWindowAttribute(handle,38,ref kind,sizeof(int))==0;
                var margins=new Margins{Left=enabled?-1:0,Right=enabled?-1:0,Top=enabled?-1:0,Bottom=enabled?-1:0};
                if(DwmExtendFrameIntoClientArea(handle,ref margins)!=0)enabled=false;
                if(!enabled){kind=1;DwmSetWindowAttribute(handle,38,ref kind,sizeof(int));}
                int caption=enabled?-1:0x00ECDECF;DwmSetWindowAttribute(handle,35,ref caption,sizeof(int));
            }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            window.SetValue(NativeProperty,new NativeState{Handle=handle,Requested=requested});window.SetValue(EnabledProperty,enabled);
            var source=HwndSource.FromHwnd(handle);if(source!=null&&source.CompositionTarget!=null)source.CompositionTarget.BackgroundColor=enabled?Colors.Transparent:Color.FromRgb(207,222,236);
            window.Background=Background(window);
        }
        [StructLayout(LayoutKind.Sequential)]struct Margins{public int Left,Right,Top,Bottom;}
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr handle,ref Margins margins);
    }
}
