using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace FileCompass {
    internal static class AppearanceTheme {
        static readonly Dictionary<string, ThemeColor> brushes = new Dictionary<string, ThemeColor>(StringComparer.OrdinalIgnoreCase);
        // One permanent appearance. These names remain only to migrate older settings and callers.
        internal static string Motif { get { return "Clean"; } }
        internal static bool IsBalanced { get { return true; } }
        internal static bool IsClean { get { return true; } }
        internal static string NormalizeMotif(string value) { return "Clean"; }
        internal static void SetMotif(string value) { Apply(Mode); }
        static readonly Dictionary<string,string> palette = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            {"#D3E2EF","#E4EFFD"},{"#1457A2","#075DCE"},{"#85C5F4","#CFE3FF"},{"#EEEEEE","#E4EFFD"},{"#FFFFFF","#FFFFFF"},{"#20242A","#203E5C"},{"#596572","#5C7791"},{"#0067C0","#087BBB"},
            {"#D5D9DF","#CFDEEC"},{"#E4E7EB","#DBE7F2"},{"#F0F2F5","#EDF3F9"},
            {"#98A5B5","#A9C3DC"},{"#BCC3CD","#A8BFD5"},{"#F3F4F7","#EDF3F9"},{"#DDDFE5","#B7CCE0"},
            {"#C4D9EE","#E2EDF7"},{"#E3EEF9","#EDF3F9"},{"#E9EDF4","#E5EFF9"},
            {"#E5F1FF","#E5EFF9"},{"#F0F5FA","#EDF3F9"},{"#DDEDFC","#DCECFB"},
            {"#E4F0FF","#DCECFB"},{"#D0E7FA","#DCECFB"},{"#DCEBFF","#DCECFB"},
            {"#79A9DF","#B7CCE0"},{"#89BFE5","#7FA9CD"},{"#DCE8F3","#CFDEEC"},{"#ECF3FA","#EDF3F9"},
            {"#EAF2FF","#CFDEEC"},{"#F6F9FE","#EDF3F9"},{"#EFF5FF","#CFDEEC"},{"#C6D8F5","#B7CCE0"}
        };
        static Color Blend(Color a,Color b,double amount) { return Color.FromRgb((byte)(a.R*(1-amount)+b.R*amount),(byte)(a.G*(1-amount)+b.G*amount),(byte)(a.B*(1-amount)+b.B*amount)); }
        static Color Resolve(string value) {
            string mapped;
            if (!IsDark) return (Color)ColorConverter.ConvertFromString(palette.TryGetValue(value,out mapped)?FluentColors.Map(value,mapped):value);
            var tint=(Color)ColorConverter.ConvertFromString(FluentColors.Current.Ink);
            switch(value.ToUpperInvariant()) {
                case "#20242A": return Color.FromRgb(235,240,246);
                case "#596572": return Color.FromRgb(174,188,202);
                case "#0067C0": case "#1457A2": return Blend((Color)ColorConverter.ConvertFromString(FluentColors.Current.Accent),Colors.White,.24);
                case "#FFFFFF": return Blend(Color.FromRgb(24,28,34),tint,.22);
                case "#D5D9DF": case "#DCE8F3": case "#EAF2FF": case "#EFF5FF": return Blend(Color.FromRgb(17,21,27),tint,.32);
                case "#85C5F4": case "#DDEDFC": case "#E4F0FF": case "#D0E7FA": case "#DCEBFF": return Blend(Color.FromRgb(30,37,47),tint,.55);
                case "#98A5B5": case "#BCC3CD": case "#DDDFE5": case "#79A9DF": case "#89BFE5": case "#C6D8F5": return Blend(Color.FromRgb(85,99,115),tint,.32);
                default: return Blend(Color.FromRgb(29,34,41),tint,.34);
            }
        }
        internal static string Backdrop = "None";
        internal static string Mode { get; private set; } = "Light";
        internal static bool IsDark { get { return Mode == "Dark"; } }
        internal static Func<bool> SystemDarkOverride;
        internal static bool SystemIsDark() { return false; }
        internal static string Normalize(string value) { return String.Equals(value,"Dark",StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light"; }
        internal static string NormalizeBackdrop(string value) { return "None"; }
        internal static void SetBackdrop(string value) { Backdrop="None";Apply(Mode); }
        internal static void RegisterShell(Window window) {
            window.Background=CompactTheme.Chrome;
            window.SourceInitialized+=delegate{TitleBar(window);};
        }
        internal static Brush Brush(string value) {
            if(!palette.ContainsKey(value))return CompactTheme.ColorBrush(value);
            ThemeColor brush;if(!brushes.TryGetValue(value,out brush)){brush=new ThemeColor(Resolve(value));brushes[value]=brush;}return brush.Brush;
        }
        static bool listening;
        internal static void Initialize() {
            if(!listening){listening=true;SystemEvents.UserPreferenceChanged+=OnPreferenceChanged;Application.Current.Exit+=delegate{SystemEvents.UserPreferenceChanged-=OnPreferenceChanged;listening=false;};}
            Apply(Mode);
        }
        static void OnPreferenceChanged(object sender,UserPreferenceChangedEventArgs args){var app=Application.Current;if(app!=null&&!app.Dispatcher.HasShutdownStarted)app.Dispatcher.BeginInvoke(new Action(RefreshSystem));}

        internal static void RefreshSystem() { Apply(Mode); }
        internal static void Apply(string mode) {
            Mode=Normalize(mode); var app=Application.Current;if(app==null)return;
            var theme=IsDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme,Wpf.Ui.Controls.WindowBackdropType.None,false);
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply((Color)ColorConverter.ConvertFromString(FluentColors.Current.Accent),theme,false,false);
            foreach(var item in brushes)item.Value.Value=Resolve(item.Key);
            foreach(Window window in app.Windows) { TitleBar(window); foreach(var editor in FluentUiTestsSafe.Children<ICSharpCode.AvalonEdit.TextEditor>(window)) editor.TextArea.TextView.Redraw(); }
            InterfaceTheme.Refresh();
        }
        internal static void TitleBar(Window window) {
            var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
            try{int enabled=IsDark ? 1 : 0;DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int));}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            WindowGlass.Apply(window,true);
        }
        sealed class ThemeColor : DependencyObject {
            internal static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(Color), typeof(ThemeColor));
            public Color Value { get { return (Color)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
            internal readonly SolidColorBrush Brush = new SolidColorBrush();
            internal ThemeColor(Color color) { Value = color; System.Windows.Data.BindingOperations.SetBinding(Brush, SolidColorBrush.ColorProperty, new System.Windows.Data.Binding("Value") { Source = this }); }
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
