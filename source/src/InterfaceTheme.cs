using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace FileCompass {
    // Shared, reversible presentation layer. File icons, toolbar commands and file operations retain their own behavior.
    internal sealed class InterfaceTheme : DependencyObject {
        internal static readonly InterfaceTheme State = new InterfaceTheme();
        public static readonly DependencyProperty BalancedProperty = DependencyProperty.Register("Balanced", typeof(bool), typeof(InterfaceTheme));
        public bool Balanced { get { return (bool)GetValue(BalancedProperty); } set { SetValue(BalancedProperty, value); } }
        internal event EventHandler Changed;
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) { base.OnPropertyChanged(e); if (e.Property == BalancedProperty && Changed != null) Changed(this, EventArgs.Empty); }
        static bool installed;
        sealed class FrameState { internal Border Frame; internal bool Owned; internal Thickness Margin; internal Brush Background, Outline; }
        static readonly DependencyProperty FrameProperty = DependencyProperty.RegisterAttached("Frame", typeof(FrameState), typeof(InterfaceTheme));
        internal static bool IsFramed(Window window) { return window.GetValue(FrameProperty) != null; }
        internal static void Install(ResourceDictionary resources) {
            if (installed) return; installed = true;
            resources["CompassMotifState"] = State;
            resources["CompassDialogOutline"] = CompactTheme.ThemeBrush("#79A9DF");
            var group = new Style(typeof(GroupBox), resources[typeof(GroupBox)] as Style);
            var trigger = BalancedTrigger();
            trigger.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.PanelSurface));
            trigger.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='GroupBox'>
              <Border Background='{TemplateBinding Background}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='6' Padding='{TemplateBinding Padding}'>
              <DockPanel><ContentPresenter DockPanel.Dock='Top' ContentSource='Header' Margin='0,0,0,10' TextElement.FontWeight='SemiBold' TextElement.Foreground='{StaticResource CompassText}'/>
              <ContentPresenter ContentSource='Content'/></DockPanel></Border></ControlTemplate>")));
            group.Triggers.Add(trigger); resources[typeof(GroupBox)] = group;
            foreach (Type type in new[] { typeof(ProgressBar), typeof(Slider) }) {
                var style = new Style(type, resources[type] as Style);
                var blue = BalancedTrigger(); blue.Setters.Add(new Setter(Control.ForegroundProperty, FluentTheme.Accent)); style.Triggers.Add(blue); resources[type] = style;
            }
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs args) {
                // Loaded bubbles from content too; process only the window itself.
                if (Object.ReferenceEquals(sender, args.OriginalSource)) Prepare((Window)sender);
            }));
        }
        static DataTrigger BalancedTrigger() { return new DataTrigger { Binding = new Binding("Balanced") { Source = State }, Value = true }; }
        internal static void Refresh() {
            State.Balanced = AppearanceTheme.IsBalanced;
            var app = Application.Current; if (app == null) return;
            foreach (Window window in app.Windows.Cast<Window>().ToArray()) if (window.IsLoaded) Prepare(window);
        }
        internal static void Prepare(Window window) {
            if (window is MainWindow) { UpdatePanels(window); return; }
            var saved = window.GetValue(FrameProperty) as FrameState;
            if (AppearanceTheme.IsBalanced) {
                if (saved != null) return;
                var content = window.Content as FrameworkElement; if (content == null) return;
                // CopyUi.Surface already owns a complete frame. Reuse it rather than nesting another outline.
                var grid = content as Grid;
                var existing = grid != null && grid.Children.Count == 1 ? grid.Children[0] as Border : null;
                if (existing != null) {
                    saved = new FrameState { Frame = existing, Background = existing.Background, Outline = existing.BorderBrush };
                    existing.Background = CompactTheme.WhiteSurface; existing.BorderBrush = CompactTheme.ThemeBrush("#79A9DF");
                    window.SetValue(FrameProperty, saved); return;
                }
                saved = new FrameState { Owned = true, Margin = content.Margin };
                window.Content = null; content.Margin = new Thickness(0);
                var frame = new Border { Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.ThemeBrush("#79A9DF"),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(10), Child = content };
                saved.Frame = frame; window.SetValue(FrameProperty, saved); window.Content = frame;
            } else if (saved != null) {
                if (!saved.Owned) { saved.Frame.Background = saved.Background; saved.Frame.BorderBrush = saved.Outline; window.ClearValue(FrameProperty); return; }
                // Restore the exact content and its spacing when switching back or cancelling preview.
                var content = saved.Frame.Child as FrameworkElement; saved.Frame.Child = null; window.Content = null;
                if (content != null) content.Margin = saved.Margin;
                window.Content = content; window.ClearValue(FrameProperty);
            }
        }
        static void UpdatePanels(DependencyObject root) {
            var panel = root as FilePanel;
            if (panel != null) { panel.BorderThickness = new Thickness(AppearanceTheme.IsBalanced ? 1 : 2); panel.Highlight(panel.IsActivePanel); return; }
            foreach (object child in LogicalTreeHelper.GetChildren(root)) { var node = child as DependencyObject; if (node != null) UpdatePanels(node); }
        }
    }
}
