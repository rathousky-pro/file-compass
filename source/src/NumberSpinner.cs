using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace FileCompass {
    internal sealed class NumberSpinner : Grid {
        internal readonly TextBox Input;
        readonly double minimum, maximum, increment;
        internal NumberSpinner(TextBox input, double minimum, double maximum, double increment = 1) {
            Input = input; this.minimum = minimum; this.maximum = maximum; this.increment = increment;
            Margin = input.Margin; input.Margin = new Thickness(0);
            if (!Double.IsNaN(input.Width)) { Width = input.Width + 18; input.Width = Double.NaN; }
            MinHeight = 28;
            ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            Children.Add(input); var arrows = new Grid(); arrows.RowDefinitions.Add(new RowDefinition()); arrows.RowDefinitions.Add(new RowDefinition()); Grid.SetColumn(arrows, 1); Children.Add(arrows);
            for (int i = 0; i < 2; i++) {
                int direction = i == 0 ? 1 : -1;
                var button = new RepeatButton { Content = i == 0 ? "▲" : "▼", FontSize = 7, Padding = new Thickness(0), Focusable = false, Delay = 400, Interval = 70, ToolTip = L.Text(i == 0 ? "number.increase" : "number.decrease") };
                System.Windows.Automation.AutomationProperties.SetName(button, Convert.ToString(button.ToolTip));
                button.Click += delegate { Change(direction); }; Grid.SetRow(button, i); arrows.Children.Add(button);
            }
            input.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Up || e.Key == Key.Down) { Change(e.Key == Key.Up ? 1 : -1); e.Handled = true; } };
        }
        internal void Change(int direction) {
            if (!IsEnabled || !Input.IsEnabled || Input.IsReadOnly) return;
            double value;
            if (!Double.TryParse(Input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || Double.IsNaN(value) || Double.IsInfinity(value)) value = minimum;
            else value += direction * increment;
            Input.Text = Math.Max(minimum, Math.Min(maximum, Math.Round(value, 6))).ToString("0.######", CultureInfo.CurrentCulture);
            Input.SelectAll();
        }
    }
}
