using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
{
    public sealed class AppearanceDialog : Window
    {
        public AppearanceDialog(Window owner)
        {
            CopyUi.Style(this, owner, L.Text("settings.appearanceAndFont"), 530);
            var body = new StackPanel { Margin = new Thickness(12) }; Content = body;
            body.Children.Add(new TextBlock { Text = L.Text("settings.appearanceWindowTitle"), FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            body.Children.Add(new TextBlock { Text = L.Text("settings.compactStyleDescription"), Foreground = FluentTheme.Muted, Margin = new Thickness(0, 0, 0, 14) });
            var typography = new StackPanel(); body.Children.Add(new GroupBox { Header = L.Text("settings.fontAndSize"), Content = typography, Margin = new Thickness(0, 0, 0, 12) });
            typography.Children.Add(CopyUi.Label(L.Text("settings.defaultFontDescription")));
            typography.Children.Add(CopyUi.Label(L.Text("settings.fontSample")));
            typography.Children.Add(new TextBlock { Text = L.Text("settings.dpiDescription"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted });
            var icons = new StackPanel(); body.Children.Add(new GroupBox { Header = L.Text("settings.toolbarIcons"), Content = icons, Margin = new Thickness(0) });
            icons.Children.Add(CopyUi.Label(L.Text("settings.iconStylesDescription")));
            icons.Children.Add(CopyUi.Button(L.Text("settings.chooseIconStyle"), delegate { if (new ToolbarThemeDialog(this).ShowDialog() == true) { var main = Owner as MainWindow; if (main != null) main.ApplyToolbarTheme(ToolbarThemeSettings.Current); } }));
            var actions = FluentTheme.Actions(); body.Children.Add(actions); var close = CopyUi.Button(L.Text("action.close"), Close); close.IsCancel = true; actions.Children.Add(close);
        }
    }
}

