using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass
{
    internal sealed class FontChoiceControl : StackPanel
    {
        internal readonly ComboBox Family = new ComboBox { MinWidth = 160, Margin = new Thickness(0, 3, 8, 3) };
        internal readonly TextBox Points = new TextBox { Width = 56, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 4, 3) };
        readonly TextBlock preview = new TextBlock { Text = L.Text("settings.fontSample"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10) };
        readonly double maximumPoints;
        internal FontChoiceControl(string label, string family, double size, double maximum)
        {
            maximumPoints = maximum; Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
            var row = new DockPanel(); Children.Add(row); var unit = new TextBlock { Text = L.Text("settings.fontPoints"), VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(unit, Dock.Right); row.Children.Add(unit); var spinner = new NumberSpinner(Points, 8, maximum, .5); DockPanel.SetDock(spinner, Dock.Right); row.Children.Add(spinner); row.Children.Add(Family);
            var families = Fonts.SystemFontFamilies.Select(x => x.Source).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray(); Family.ItemsSource = families;
            Family.SelectedItem = families.FirstOrDefault(x => String.Equals(x, family, StringComparison.OrdinalIgnoreCase)) ?? families.FirstOrDefault(x => x == "Segoe UI") ?? families.FirstOrDefault();
            Points.Text = (DisplayPreferences.Size(size) * .75).ToString("0.#", CultureInfo.CurrentCulture); Children.Add(preview);
            Family.SelectionChanged += delegate { Preview(); }; Points.TextChanged += delegate { Preview(); }; Preview();
        }
        void Preview() { double points; preview.FontFamily = DisplayPreferences.Font(Family.SelectedItem as string); if (Double.TryParse(Points.Text, out points) && points >= 8 && points <= maximumPoints) preview.FontSize = points * 4 / 3; }
        internal void Read(out string family, out double size)
        {
            double points; if (!Double.TryParse(Points.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out points) || Double.IsNaN(points) || Double.IsInfinity(points) || points < 8 || points > maximumPoints) throw new ArgumentException(L.Format("settings.fontSizeRange", 8, maximumPoints));
            family = Family.SelectedItem as string; if (String.IsNullOrEmpty(family)) throw new ArgumentException(L.Text("settings.fontRequired")); size = points * 4 / 3;
        }
    }
}

