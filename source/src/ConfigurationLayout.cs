using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FileCompass {
    public sealed partial class ConfigurationDialog {
        bool fitQueued, fitting;
        Rect lastWorkArea;
        UniformGrid applicationIconGrid;
        internal Rect? LayoutWorkArea;
        Rect SettingsWorkArea() {
            if (LayoutWorkArea.HasValue) return LayoutWorkArea.Value;
            var handle = new WindowInteropHelper(IsLoaded ? this : Owner ?? this).Handle;
            if (handle == IntPtr.Zero) return SystemParameters.WorkArea;
            var bounds = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var source = HwndSource.FromHwnd(handle);
            var transform = source == null || source.CompositionTarget == null ? System.Windows.Media.Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            return new Rect(transform.Transform(new Point(bounds.Left, bounds.Top)), transform.Transform(new Point(bounds.Right, bounds.Bottom)));
        }
        void InitializeSettingsSize() {
            var area = SettingsWorkArea();
            Width = Math.Min(980, area.Width - 32); Height = Math.Min(760, area.Height - 32);
            MinWidth = Math.Min(820, Width); MinHeight = Math.Min(350, Height);
            SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            Loaded += delegate { QueueSettingsFit(); };
            SizeChanged += delegate(object sender, SizeChangedEventArgs e) { if (e.WidthChanged && !fitting) QueueSettingsFit(); };
            LocationChanged += delegate { if (IsLoaded && !fitting && SettingsWorkArea() != lastWorkArea) QueueSettingsFit(); };
        }
        void QueueSettingsFit() {
            if (fitQueued) return; fitQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate { fitQueued = false; FitSettingsPage(); }));
        }
        internal void FitSettingsPage() {
            if (!IsLoaded || fitting) return;
            var panel = page.Content as FrameworkElement;
            if (panel == null) return;
            fitting = true;
            try {
                var area = lastWorkArea = SettingsWorkArea();
                MinWidth = Math.Min(820, area.Width - 32); MinHeight = Math.Min(350, area.Height - 32);
                MaxWidth = area.Width - 32; MaxHeight = area.Height - 32;
                UpdateLayout();
                if (applicationIconGrid != null) applicationIconGrid.Columns = panel.ActualWidth < 720 ? 3 : 4;
                // Measure the attached, styled page at its available width. Reserve scrollbar
                // space even when hidden so a bounded fit cannot oscillate between widths.
                panel.Measure(new Size(Math.Max(100, panel.ActualWidth - SystemParameters.VerticalScrollBarWidth), Double.PositiveInfinity));
                double chrome = ActualHeight - panel.ActualHeight;
                Height = Math.Min(MaxHeight, 700);
                // Keep deliberately offscreen test windows offscreen.
                if (new Rect(Left, Top, ActualWidth, ActualHeight).IntersectsWith(area)) {
                    Left = Math.Max(area.Left + 16, Math.Min(Left, area.Right - Width - 16));
                    Top = Math.Max(area.Top + 16, Math.Min(Top, area.Bottom - Height - 16));
                }
                UpdateLayout();
            } finally { fitting = false; }
        }
        static UIElement[] Take(Panel panel) {
            var children = panel.Children.Cast<UIElement>().ToArray(); panel.Children.Clear(); return children;
        }
        static Grid Row(params UIElement[] items) {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < items.Length; i++) {
                grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
                var item = (FrameworkElement)items[i];
                item.Margin = new Thickness(0,0,0,18);
                item.VerticalAlignment = VerticalAlignment.Stretch;
                Grid.SetColumn(item, 0);Grid.SetRow(item,i); grid.Children.Add(item);
            }
            return grid;
        }
        static GroupBox Card(string title, params UIElement[] children) {
            var stack = new StackPanel(); foreach (var child in children) stack.Children.Add(child);
            return new GroupBox { Header = title, Content = stack };
        }
        void ArrangeSettingsPages(StackPanel[] pages) {
            foreach (var panel in pages) panel.Margin = new Thickness(0,0,14,0);
            var appearance = pages[0]; var a = Take(appearance); appearance.Children.Add(a[0]); appearance.Children.Add(a[1]);
            var fonts = (StackPanel)((GroupBox)a[2]).Content; var choices = Take(fonts); fonts.Children.Add(Row(choices));
            automaticRefresh.Margin = new Thickness(0, 5, 0, 5);
            appearance.Children.Add(Row(a[2])); appearance.Children.Add(Row(a[3], a[6])); appearance.Children.Add(Row(a[4]));
            ((TextBlock)a[5]).TextWrapping = TextWrapping.Wrap; ((FrameworkElement)a[5]).Margin = new Thickness(0); appearance.Children.Add(a[5]);

            var icons = pages[1]; var b = Take(icons); icons.Children.Add(b[0]); icons.Children.Add(b[1]);
            var app = (StackPanel)((GroupBox)b[2]).Content; var options = (WrapPanel)app.Children[1];
            var radios = Take(options); app.Children.Remove(options);
            var appGrid = applicationIconGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 6, 0, 6) };
            foreach (RadioButton radio in radios) {
                radio.Margin = new Thickness(0, 4, 8, 4);
                var preview = (StackPanel)radio.Content; var image = (Image)preview.Children[0]; image.Width = image.Height = 32;
                ((TextBlock)preview.Children[1]).TextWrapping = TextWrapping.Wrap;
                appGrid.Children.Add(radio);
            }
            app.Children.Insert(1, appGrid); icons.Children.Add(Row(b[2]));
            foreach (GroupBox scheme in b.Skip(3)) {
                var stack = (StackPanel)scheme.Content; var radio = (RadioButton)stack.Children[0]; stack.Children.Remove(radio);
                radio.Margin = new Thickness(0); scheme.Header = radio;
                radio.Content = new TextBlock { Text = Convert.ToString(radio.Content), TextWrapping = TextWrapping.Wrap };
            }
            icons.Children.Add(Row(b.Skip(3).ToArray()));

            var archivesPage = pages[2]; var c = Take(archivesPage); archivesPage.Children.Add(c[0]); archivesPage.Children.Add(c[1]);
            foreach (var item in c.Skip(2).Where(x => !(x is GroupBox) && x != c.Last())) archivesPage.Children.Add(item);
            var library = c.Skip(2).OfType<TextBlock>().First(); library.TextWrapping = TextWrapping.NoWrap; library.TextTrimming = TextTrimming.CharacterEllipsis; library.ToolTip = library.Text; library.Margin = new Thickness(0, 0, 0, 10);
            archivesPage.Children.Add(Row(c.OfType<GroupBox>().Where(x => x != archiveLimits).Cast<UIElement>().ToArray()));
            ArrangeArchiveLimits(); archivesPage.Children.Add(Row(archiveLimits));
            ((FrameworkElement)c.Last()).Margin = new Thickness(0); archivesPage.Children.Add(c.Last());

            foreach (GroupBox group in pages[3].Children.OfType<GroupBox>()) group.Margin = new Thickness(0, 0, 0, 8);
            var startup = pages[4]; var d = Take(startup); startup.Children.Add(d[0]); startup.Children.Add(d[1]);
            startup.Children.Add(Row(Card(L.Text("settings.startupOptions"), d[2]), Card(L.Text("settings.saveView"), d[3], d[4], d[5])));
            ArrangeBehavior(fileBehavior);
        }
        void ArrangeArchiveLimits() {
            archiveLimits.SetResourceReference(StyleProperty, typeof(GroupBox));
            var grid = (Grid)archiveLimits.Content; var fields = Take(grid);
            grid.RowDefinitions.Clear();grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(120)});
            for(int i=0;i<fields.Length;i++){int row=i/2;if(i%2==0)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetRow(fields[i],row);Grid.SetColumn(fields[i],i%2);var label=fields[i] as TextBlock;if(label!=null)label.TextWrapping=TextWrapping.Wrap;grid.Children.Add(fields[i]);}
        }
        static void ArrangeBehavior(FileBehaviorEditor editor) {
            var children = Take(editor);
            var general = Card(L.Text("settings.listControls"), children.Take(9).ToArray());
            var copy = Card(((TextBlock)children[9]).Text, children.Skip(10).Take(6).ToArray());
            var options = Row(general, copy); Grid.SetIsSharedSizeScope(options, true);
            foreach (var card in new[] { general, copy }) {
                var content = (StackPanel)card.Content; var checks = Take(content); var rows = new Grid();
                for (int i = 0; i < checks.Length; i++) {
                    ((FrameworkElement)checks[i]).Margin = new Thickness(0, 3, 0, 3);
                    rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, SharedSizeGroup = "Option" + i });
                    Grid.SetRow(checks[i], i); rows.Children.Add(checks[i]);
                }
                content.Children.Add(rows);
            }
            editor.Children.Add(options);
            editor.Children.Add(Row(Card(((TextBlock)children[16]).Text, children[17])));
            editor.Children.Add(children[18]);
        }
    }
}
