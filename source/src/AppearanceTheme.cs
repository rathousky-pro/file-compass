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
        static readonly Dictionary<string, string> dark = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"#216EA8", "#95CBF5"}, {"#167C78", "#86D8CF"}, {"#6553B0", "#C5B8FF"}, {"#657080", "#B4BFCD"},
            {"#FFFFFF", "#202327"}, {"#20242A", "#E8ECF1"}, {"#596572", "#AEBAC8"}, {"#0067C0", "#78B9FF"},
            {"#D5D9DF", "#181B20"}, {"#E4E7EB", "#252A31"}, {"#F0F2F5", "#2C323B"},
            {"#98A5B5", "#69788C"}, {"#BCC3CD", "#495563"}, {"#F3F4F7", "#242930"}, {"#DDDFE5", "#424B57"},
            {"#C4D9EE", "#28445F"}, {"#E3EEF9", "#2C4660"}, {"#E9EDF4", "#35404D"},
            {"#E5F1FF", "#30475F"}, {"#F0F5FA", "#303B49"}, {"#DDEDFC", "#284D70"}, {"#F7F7F7", "#292D33"},
            {"#F4F7FB", "#282F38"}, {"#E6E6E6", "#454C57"}, {"#AEB8C4", "#586676"},
            {"#CDE8FF", "#284D70"}, {"#E4EAF2", "#303B49"}, {"#E4F0FF", "#264E77"}, {"#343D48", "#E8ECF1"},
            {"#939AA2", "#8A98A9"}, {"#B8C2CE", "#495563"}, {"#F0F3F8", "#303B49"},
            {"#D0E7FA", "#264E77"}, {"#DCEBFF", "#315D88"}, {"#89BFE5", "#538CB8"}, {"#B92331", "#FF8C97"},
            {"#E6F9E6", "#244A32"}, {"#EEFBF0", "#244A32"}, {"#E7ECF3", "#303B49"},
            {"#FFE8E8", "#592F36"}, {"#FFF6D6", "#51482B"},
            {"#DCF5E8", "#17452E"}, {"#12623C", "#9EF0BA"}, {"#DFEEFF", "#163B5A"}, {"#135C9E", "#9DD5FF"},
            {"#FFF0C2", "#5A4516"}, {"#704600", "#FFE08B"}, {"#FFE0E0", "#5B202B"}, {"#9E2031", "#FFB6C0"}
        };
        static string mode = "System";
        internal static string Motif { get; private set; } = "Balanced";
        internal static bool IsBalanced { get { return true; } }
        // Retained only to migrate old settings and callers to the sole supported appearance.
        internal static string NormalizeMotif(string value) { return "Balanced"; }
        internal static void SetMotif(string value) { Motif = NormalizeMotif(value); Apply(Mode); }
        static readonly Dictionary<string, string> balanced = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"#FFFFFF", "#FFFFFF"}, {"#20242A", "#292C31"}, {"#596572", "#68717C"}, {"#0067C0", "#086BC9"},
            {"#D5D9DF", "#ECEEF1"}, {"#E4E7EB", "#F8F9FA"}, {"#F0F2F5", "#F3F4F6"},
            {"#98A5B5", "#CCD3DC"}, {"#BCC3CD", "#D8DDE3"}, {"#F3F4F7", "#F5F6F8"}, {"#DDDFE5", "#D8DDE3"},
            {"#C4D9EE", "#F1F5FA"}, {"#E3EEF9", "#EEF3FA"}, {"#E9EDF4", "#F1F3F6"},
            {"#E5F1FF", "#EDF4FC"}, {"#F0F5FA", "#F5F7FA"}, {"#DDEDFC", "#EFF3F8"},
            {"#E4F0FF", "#F2F4F7"}, {"#D0E7FA", "#EFF4FA"}, {"#DCEBFF", "#DAE9FC"},
            {"#89BFE5", "#79A9DF"}, {"#79A9DF", "#79A9DF"}, {"#EEEEEE", "#E5EAF0"},
            {"#1457A2", "#086BC9"}, {"#85C5F4", "#79B5ED"}
        };
        static readonly Dictionary<string, string> balancedDark = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"#FFFFFF", "#20252B"}, {"#20242A", "#E7ECF2"}, {"#596572", "#A7AFB9"}, {"#0067C0", "#87BCF4"},
            {"#D5D9DF", "#25292F"}, {"#E4E7EB", "#272E36"}, {"#F0F2F5", "#2C333C"},
            {"#98A5B5", "#4B5868"}, {"#BCC3CD", "#414952"}, {"#F3F4F7", "#292F37"}, {"#DDDFE5", "#414952"},
            {"#C4D9EE", "#2A3543"}, {"#E3EEF9", "#303C4D"}, {"#DDEDFC", "#2A3747"}, {"#E4F0FF", "#2C3846"},
            {"#79A9DF", "#6094C9"}, {"#EEEEEE", "#3C4858"}, {"#1457A2", "#5EA5F0"}, {"#85C5F4", "#9ACAFA"}
        };
        static Color Resolve(string value) {
            string mapped;
            if (IsBalanced && (IsDark ? balancedDark : balanced).TryGetValue(value, out mapped)) value = mapped;
            else if (IsDark && dark.TryGetValue(value, out mapped)) value = mapped;
            return (Color)ColorConverter.ConvertFromString(value);
        }
        internal static string Backdrop = "None";
        static readonly List<WeakReference> shells = new List<WeakReference>();
        internal static string NormalizeBackdrop(string value) { return value == "Mica" || value == "MicaAlt" ? value : "None"; }
        internal static void SetBackdrop(string value) { Backdrop = NormalizeBackdrop(value); Apply(Mode); }
        internal static void RegisterShell(Window window) {
            shells.Add(new WeakReference(window));
            window.SourceInitialized += delegate { UpdateBackdrop(window); };
            window.Loaded += delegate { UpdateBackdrop(window); };
        }
        static void UpdateBackdrop(Window window) {
            var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return;
            bool enabled = Backdrop != "None" && !SystemParameters.HighContrast;
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (key != null && Convert.ToInt32(key.GetValue("EnableTransparency", 1)) == 0) enabled = false;
                int kind = enabled ? (Backdrop == "MicaAlt" ? 4 : 2) : 1;
                if (DwmSetWindowAttribute(handle, 38, ref kind, sizeof(int)) != 0) enabled = false;
                var margins = new FrameMargins { Left = enabled ? -1 : 0 };
                DwmExtendFrameIntoClientArea(handle, ref margins);
                var target = HwndSource.FromHwnd(handle); if (target != null && target.CompositionTarget != null) target.CompositionTarget.BackgroundColor = enabled ? Colors.Transparent : Resolve("#D5D9DF");
            } catch (DllNotFoundException) { enabled = false; } catch (EntryPointNotFoundException) { enabled = false; }
            UpdateShellLayers(window, enabled); window.Background = enabled ? Brushes.Transparent : CompactTheme.Chrome;
            var root = window.Content as System.Windows.Controls.Panel;
            if (root != null) root.Background = enabled ? Brushes.Transparent : CompactTheme.Chrome;

        }
        static readonly DependencyProperty OriginalShellBrushProperty = DependencyProperty.RegisterAttached("OriginalShellBrush", typeof(Brush), typeof(AppearanceTheme));
        static void UpdateShellLayers(DependencyObject element, bool enabled) {
            DependencyProperty property = element is System.Windows.Controls.Panel ? System.Windows.Controls.Panel.BackgroundProperty : element is System.Windows.Controls.Border ? System.Windows.Controls.Border.BackgroundProperty : null;
            if (property != null) {
                var original = element.GetValue(OriginalShellBrushProperty) as Brush;
                var current = element.GetValue(property) as Brush;
                if (original == null && current != null && (Object.ReferenceEquals(current, CompactTheme.ToolbarSurface) || Object.ReferenceEquals(current, CompactTheme.PanelSurface) || Object.ReferenceEquals(current, CompactTheme.Chrome))) {
                    original = current; element.SetValue(OriginalShellBrushProperty, original);
                }
                if (original != null) {
                    var solid = original as SolidColorBrush;
                    element.SetValue(property, enabled && solid != null ? new SolidColorBrush(Color.FromArgb(210, solid.Color.R, solid.Color.G, solid.Color.B)) : original);
                }
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) UpdateShellLayers(VisualTreeHelper.GetChild(element, i), enabled);
        }
        [StructLayout(LayoutKind.Sequential)] struct FrameMargins { public int Left, Right, Top, Bottom; }
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref FrameMargins margins);
        internal static string Mode { get { return mode; } private set { mode = value; } }
        internal static bool IsDark { get; private set; }
        static bool listening;
        internal static Func<bool> SystemDarkOverride;
        internal static string Normalize(string value) { return value == "Dark" || value == "Light" ? value : "System"; }
        internal static bool SystemIsDark() {
            if (SystemDarkOverride != null) return SystemDarkOverride();
            try { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; }
            catch { return false; }
        }
        internal static Brush Brush(string value) {
            if (!dark.ContainsKey(value) && !balanced.ContainsKey(value) && !balancedDark.ContainsKey(value)) return CompactTheme.ColorBrush(value);
            ThemeColor brush;
            if (!brushes.TryGetValue(value, out brush)) {
                brush = new ThemeColor(Resolve(value)); brushes[value] = brush;
            }
            return brush.Brush;
        }
        internal static void Initialize() {
            if (!listening) {
                listening = true; SystemEvents.UserPreferenceChanged += OnSystemChanged;
                Application.Current.Exit += delegate { SystemEvents.UserPreferenceChanged -= OnSystemChanged; listening = false; };
            }
            var settings = ProgramSettings.Load(); Motif = NormalizeMotif(settings.InterfaceMotif); Backdrop = NormalizeBackdrop(settings.WindowBackdrop); Apply(settings.ColorTheme);
        }
        static void OnSystemChanged(object sender, UserPreferenceChangedEventArgs args) {
            var app = Application.Current;
            if (app != null && !app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(RefreshSystem));
        }
        internal static void RefreshSystem() { Apply(Mode); }
        internal static void Apply(string mode) {
            Mode = Normalize(mode); IsDark = Mode == "Dark" || (Mode == "System" && SystemIsDark());
            var app = Application.Current;
            if (app == null) return;
            var theme = IsDark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.None, false);
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(IsBalanced ? Color.FromRgb(8,107,201) : IsDark ? Color.FromRgb(120,185,255) : Color.FromRgb(0,103,192), theme, false, false);
            foreach (var item in brushes) item.Value.Value = Resolve(item.Key);
            foreach (Window window in app.Windows) TitleBar(window); for (int i = shells.Count - 1; i >= 0; i--) { var shell = shells[i].Target as Window; if (shell == null) shells.RemoveAt(i); else UpdateBackdrop(shell); }
            InterfaceTheme.Refresh();
        }
        internal static void TitleBar(Window window) {
            var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return;
            try { int enabled = IsDark ? 1 : 0; DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
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
