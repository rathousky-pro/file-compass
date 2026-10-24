using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FileCompass
{
    public static class CompactTheme
    {
        public static readonly Brush WhiteSurface = ThemeBrush("#FFFFFF");
        public static readonly Brush Chrome = ThemeBrush("#D5D9DF");
        public static readonly Brush ToolbarSurface = ThemeBrush("#E4E7EB");
        public static readonly Brush PanelSurface = ThemeBrush("#F0F2F5");
        public static readonly Brush FieldLine = ThemeBrush("#98A5B5");
        public static readonly Brush ChromeLine = ThemeBrush("#BCC3CD");
        public static readonly Brush Surface = ThemeBrush("#F3F4F7");
        public static readonly Brush Line = ThemeBrush("#DDDFE5");
        public static readonly Brush ActivePath = ThemeBrush("#C4D9EE");
        public static readonly Brush SortedHeader = ThemeBrush("#E3EEF9");
        public static readonly Brush InactivePath = PanelSurface;
        public static Brush ColorBrush(string color) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        public static Brush ThemeBrush(string color) { return AppearanceTheme.Brush(color); }

        public static Button Button(object content, string tooltip, RoutedEventHandler click)
        {
            FluentTheme.Ensure();
            var button = new Wpf.Ui.Controls.Button { Content = content, ToolTip = tooltip, Height = 23, MinHeight = 0, MinWidth = 22,
                Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(2, 0, 2, 0), CornerRadius = new CornerRadius(4),
                MouseOverBackground = ColorBrush("#E9EDF4"), MouseOverBorderBrush = Brushes.Transparent,
                VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (click != null) button.Click += click;
            return button;
        }
        public static void ToolbarTile(Button button)
        {
            button.Height = 34; button.MinWidth = 30; button.MinHeight = 0;
            button.Margin = new Thickness(1, 1, 1, 1); button.Padding = new Thickness(8, 4, 8, 4);
            button.Background = CompactTheme.WhiteSurface; button.BorderBrush = Line;
            button.UseLayoutRounding = true; button.SnapsToDevicePixels = true;
        }
        public static Image DriveIcon(bool network, bool connected = true)
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(ColorBrush("#D9DDE0"), new Pen(ColorBrush("#72797F"), 0.7), new RectangleGeometry(new Rect(0, 3, 15, 8), 1, 1)));
            drawing.Children.Add(new GeometryDrawing(ColorBrush("#7A8185"), null, new RectangleGeometry(new Rect(1, 8, 13, 2))));
            drawing.Children.Add(new GeometryDrawing(network ? Brushes.LimeGreen : Brushes.LightSkyBlue, null, new RectangleGeometry(new Rect(2, 8, network ? 9 : 4, 2))));
            if (network && !connected)
            {
                var cross = new GeometryGroup();
                cross.Children.Add(new LineGeometry(new Point(8, 5), new Point(14, 11)));
                cross.Children.Add(new LineGeometry(new Point(14, 5), new Point(8, 11)));
                drawing.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 3.5), cross));
                drawing.Children.Add(new GeometryDrawing(null, new Pen(Brushes.Firebrick, 2), cross));
            }
            return new Image { Source = new DrawingImage(drawing), Width = 15, Height = 13, Margin = new Thickness(0, 0, 3, 0) };
        }
    }
}




