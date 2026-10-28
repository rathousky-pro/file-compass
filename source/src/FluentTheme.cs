using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace FileCompass
{
    public static class FluentTheme
    {
        static Application initialized;
        internal static string StartupTiming;
        public static readonly Brush Text = CompactTheme.ThemeBrush("#20242A");
        public static readonly Brush Muted = CompactTheme.ThemeBrush("#596572");
        public static readonly Brush Accent = CompactTheme.ThemeBrush("#0067C0");
        public static void Ensure()
        {
            WindowEscape.Install();
            HelpService.Ensure();
            var app = Application.Current;
            if (app == null) app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (initialized == app) return;
            var timing = System.Diagnostics.Stopwatch.StartNew();
            var resources = app.Resources;
            resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
            resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(System.IO.Path.Combine(AppPaths.Resource("ui"), "CompactMenu.xaml"), UriKind.Absolute) });
            long dictionariesMs = timing.ElapsedMilliseconds;
            Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(Color.FromRgb(0, 103, 192), Wpf.Ui.Appearance.ApplicationTheme.Light, false, false);
            long accentMs = timing.ElapsedMilliseconds;
            resources["ControlContentThemeFontSize"] = 12.0;
            resources["ControlCornerRadius"] = new CornerRadius(4);
            resources["TextControlThemeMinHeight"] = 28.0;
            resources["TextControlThemePadding"] = new Thickness(7, 3, 7, 3);
            resources["ComboBoxPadding"] = new Thickness(7, 3, 7, 3);
            foreach (string key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundFocused" }) resources[key] = CompactTheme.WhiteSurface;
            resources["TextControlBackgroundDisabled"] = CompactTheme.ThemeBrush("#F7F7F7");
            resources["ComboBoxBackgroundDisabled"] = CompactTheme.ThemeBrush("#F7F7F7");
            foreach (Type type in new[] { typeof(TextBox), typeof(PasswordBox), typeof(ComboBox) }) {
                var field = Add(type, "Default" + type.Name + "Style", Control.MinHeightProperty, 28.0, Control.PaddingProperty, new Thickness(7, 3, 7, 3)); field.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.WhiteSurface)); field.Setters.Add(new Setter(Control.ForegroundProperty, Text)); field.Setters.Add(new Setter(Control.BorderBrushProperty, CompactTheme.FieldLine)); resources[type] = field;
            }
            resources["TextControlBorderBrush"] = CompactTheme.FieldLine;
            resources["TextControlBorderBrushPointerOver"] = Accent;
            resources["TextControlBorderBrushFocused"] = Accent;
            resources["ComboBoxBorderBrush"] = CompactTheme.FieldLine;
            resources["ComboBoxBorderBrushPointerOver"] = Accent;
            resources["ComboBoxBorderBrushFocused"] = Accent;
            resources["CheckBoxHeight"] = 16.0;
            resources["CheckBoxWidth"] = 16.0;
            resources["CheckBoxIconSize"] = 11.0;
            resources["CompassSurface"] = CompactTheme.WhiteSurface; resources["CompassLine"] = CompactTheme.FieldLine; resources["CompassText"] = Text; resources["CompassGroup"] = CompactTheme.PanelSurface; resources["CompassAccent"] = Accent; resources["CompassMuted"] = Muted; resources["CompassHover"] = CompactTheme.ThemeBrush("#F0F5FA"); resources["CompassSelection"] = CompactTheme.ThemeBrush("#DDEDFC");
            var checks = Add(typeof(CheckBox), "DefaultCheckBoxStyle", Control.MinHeightProperty, 24.0, Control.MinWidthProperty, 0.0);
            checks.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 2, 0, 2)));
            checks.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'>
              <Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='16'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
              <Border x:Name='Box' Width='16' Height='16' Background='{StaticResource CompassSurface}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='2' VerticalAlignment='Center'>
              <Grid><Path x:Name='Tick' Data='M 3,7 L 6,10 L 12,4' Stroke='{StaticResource CompassAccent}' StrokeThickness='2' Visibility='Collapsed'/><Rectangle x:Name='Mixed' Width='8' Height='2' Fill='{StaticResource CompassMuted}' Visibility='Collapsed'/></Grid></Border>
              <ContentPresenter Grid.Column='1' ContentSource='Content' RecognizesAccessKey='True' Margin='6,0,0,0' VerticalAlignment='Center'/></Grid>
              <ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger>
              <Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='Mixed' Property='Visibility' Value='Visible'/></Trigger>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger>
              <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='{StaticResource CompassAccent}'/><Setter TargetName='Box' Property='BorderThickness' Value='2'/></Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
            resources[typeof(CheckBox)] = checks;
            resources[typeof(RadioButton)] = Add(typeof(RadioButton), "DefaultRadioButtonStyle", Control.MinHeightProperty, 24.0, Control.MinWidthProperty, 0.0);
            var buttons = Add(typeof(Button), "DefaultButtonStyle", Control.MinHeightProperty, 28.0, Control.PaddingProperty, new Thickness(12, 3, 12, 3));
            buttons.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            buttons.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.WhiteSurface)); buttons.Setters.Add(new Setter(Control.BorderBrushProperty, CompactTheme.FieldLine));
            buttons.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            ReadableDisabled(buttons);
            resources[typeof(Button)] = buttons;
            var fluent = Add(typeof(Wpf.Ui.Controls.Button), "DefaultUiButtonStyle", Control.MinHeightProperty, 0.0, Control.PaddingProperty, new Thickness(8, 2, 8, 2));
            fluent.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            fluent.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.WhiteSurface));
            fluent.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            fluent.Setters.Add(new Setter(Control.BorderBrushProperty, CompactTheme.FieldLine));
            fluent.Setters.Add(new Setter(Wpf.Ui.Controls.Button.MouseOverBackgroundProperty, CompactTheme.ThemeBrush("#E5F1FF")));
            fluent.Setters.Add(new Setter(Wpf.Ui.Controls.Button.MouseOverBorderBrushProperty, Accent));
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, Accent)); fluent.Triggers.Add(focus);
            var primary = new Trigger { Property = Button.IsDefaultProperty, Value = true };
            primary.Setters.Add(new Setter(Wpf.Ui.Controls.Button.AppearanceProperty, Wpf.Ui.Controls.ControlAppearance.Primary));
            primary.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            fluent.Triggers.Add(primary);
            var primaryAppearance = new Trigger { Property = Wpf.Ui.Controls.Button.AppearanceProperty, Value = Wpf.Ui.Controls.ControlAppearance.Primary };
            primaryAppearance.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); fluent.Triggers.Add(primaryAppearance);
            ReadableDisabled(fluent);
            resources[typeof(Wpf.Ui.Controls.Button)] = fluent;
            var text = new Style(typeof(TextBlock));
            text.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")));
            text.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            resources[typeof(TextBlock)] = text;
            var groups = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='GroupBox'>
              <Setter Property='Padding' Value='12'/><Setter Property='Margin' Value='0,0,8,0'/><Setter Property='Background' Value='{StaticResource CompassGroup}'/>
              <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='GroupBox'>
              <Border Background='{TemplateBinding Background}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}'>
              <DockPanel><ContentPresenter DockPanel.Dock='Top' ContentSource='Header' Margin='0,0,0,10' TextElement.FontWeight='SemiBold' TextElement.Foreground='{StaticResource CompassText}'/>
              <ContentPresenter ContentSource='Content'/></DockPanel></Border>
              </ControlTemplate></Setter.Value></Setter>
              </Style>");
            resources[typeof(GroupBox)] = groups;
            var headers = Add(typeof(GridViewColumnHeader), "UiGridViewColumnHeaderStyle", Control.PaddingProperty, new Thickness(8, 5, 8, 5), Control.FontWeightProperty, FontWeights.SemiBold);
            headers.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            resources[typeof(GridViewColumnHeader)] = headers;
            resources[typeof(ListViewItem)] = ListRow();
            var lists = new Style(typeof(ListView));
            lists.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.WhiteSurface));
            lists.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            lists.Setters.Add(new Setter(Control.BorderBrushProperty, CompactTheme.Line));
            lists.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            resources[typeof(ListView)] = lists;
            var listItems = new Style(typeof(ListBoxItem), resources[typeof(ListBoxItem)] as Style);
            listItems.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28.0));
            listItems.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
            listItems.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            listItems.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            listItems.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            listItems.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            listItems.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' CornerRadius='3'><ContentPresenter ContentSource='Content' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center' TextElement.Foreground='{TemplateBinding Foreground}'/></Border></ControlTemplate>")));
            var hoveredItem = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoveredItem.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#F0F5FA"))); listItems.Triggers.Add(hoveredItem);
            var selectedItem = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedItem.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#DDEDFC")));
            selectedItem.Setters.Add(new Setter(Control.ForegroundProperty, Text)); listItems.Triggers.Add(selectedItem);
            resources[typeof(ListBoxItem)] = listItems;
            StartupTiming = "dictionaries=" + dictionariesMs + "ms, accent=" + (accentMs - dictionariesMs) + "ms, styles=" + (timing.ElapsedMilliseconds - accentMs) + "ms";
            TabUx.Install(resources);
            InterfaceTheme.Install(resources);
            initialized = app; AppearanceTheme.Initialize();
        }
        static Style Add(Type type, string basis, DependencyProperty first, object value, DependencyProperty second, object other)
        {
            var resources = Application.Current.Resources;
            var style = new Style(type, resources[basis] as Style);
            style.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            style.Setters.Add(new Setter(first, value)); style.Setters.Add(new Setter(second, other));
            return style;
        }
        static Style ListRow()
        {
            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Text));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 3, 8, 3)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 26.0));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            var presenter = new FrameworkElementFactory(typeof(GridViewRowPresenter));
            presenter.SetBinding(GridViewRowPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(GridViewRowPresenter.ColumnsProperty, new System.Windows.Data.Binding("View.Columns") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListView), 1) });
            presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 3)); border.AppendChild(presenter);
            style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListViewItem)) { VisualTree = border }));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#F0F5FA"))); style.Triggers.Add(hover);
            var select = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true }; select.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#DDEDFC"))); style.Triggers.Add(select);
            return style;
        }
        public static void Dialog(Window window)
        {
            DialogOwnerActivation.Attach(window);
            Ensure(); window.FontFamily = window.Owner == null ? new FontFamily("Segoe UI") : window.Owner.FontFamily; window.FontSize = window.Owner == null ? 12 : window.Owner.FontSize;
            window.Foreground = Text; window.Background = CompactTheme.Chrome;
            window.SourceInitialized += delegate { AppearanceTheme.TitleBar(window); }; window.Loaded += delegate { Prepare(window); };
        }
        internal static void Prepare(Window window)
        {
            if (window is MainWindow) return;
            var body = window.Content as FrameworkElement;
            if (body != null && body.Margin.Left < 12 && body.Margin.Right < 12) body.Margin = new Thickness(12);
            Normalize(window);
        }
        static void Normalize(DependencyObject parent)
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                var child = item as DependencyObject; if (child == null) continue;
                var input = child as Control;
                if (input is TextBox || input is PasswordBox || input is ComboBox)
                {
                    input.MinHeight = Math.Max(28, input.MinHeight);
                    if (!Double.IsNaN(input.Height) && input.Height < 28) input.Height = 28;
                }
                var grid = child as UniformGrid;
                if (grid != null && grid.Children.Count > 0 && grid.Children.Cast<UIElement>().All(x => x is Button) && grid.Rows != 2)
                {
                    if (grid.Columns == grid.Children.Count && Double.IsNaN(grid.Width))
                    {
                        double width = grid.Children.Cast<Button>().Max(x => Math.Max(90, x.Content == null ? 90 : x.Content.ToString().Length * 6.7 + 26));
                        grid.Width = width * grid.Children.Count; grid.HorizontalAlignment = HorizontalAlignment.Right;
                        grid.Margin = new Thickness(0, 10, 0, 0);
                    }
                }
                Normalize(child);
            }
        }
        public static StackPanel Actions()
        { return new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; }
        internal static StackPanel Section(Panel parent, string title)
        { var content = new StackPanel(); parent.Children.Add(new GroupBox { Header = title, Content = content, Margin = new Thickness(0, 0, 0, 8) }); return content; }
        public static void Primary(Button button)
        {
            var fluent = button as Wpf.Ui.Controls.Button;
            if (fluent != null) fluent.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
        }
        static void ReadableDisabled(Style style) {
            var trigger=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};
            trigger.Setters.Add(new Setter(UIElement.OpacityProperty,1.0));
            trigger.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border CornerRadius='4' Background='{StaticResource CompassGroup}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter ContentSource='Content' RecognizesAccessKey='True' HorizontalAlignment='Center' VerticalAlignment='Center' TextElement.Foreground='{StaticResource CompassMuted}' TextElement.FontWeight='Normal'/></Border></ControlTemplate>")));
            style.Triggers.Add(trigger);
        }
    }
}
