using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileCompass {
    internal sealed class DrivePropertiesWindow : Window {
        readonly string root;
        readonly FilePanel panel;
        readonly StackPanel details = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };
        internal DrivePropertiesWindow(MainWindow owner, string driveRoot, FilePanel source) {
            root = driveRoot; panel = source;
            CopyUi.Style(this, owner, L.Text("driveProperties.title"), 540);
            StackPanel actions; var body = SettingsUx.Frame(this, L.Text("driveProperties.title"), out actions);
            body.Children.Add(details);
            details.Children.Add(new TextBlock { Text = L.Text("driveProperties.loading"), Foreground = FluentTheme.Muted });
            actions.Children.Add(CopyUi.Button(L.Text("action.close"), Close));
            Loaded += async delegate {
                try {
                    var info = await Task.Run(() => ReadDrive(root));
                    if (!IsLoaded) return;
                    DialogGeometry.PreserveCenterAfterLayout(this);
                    details.Children.Clear();
                    var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
                    heading.Children.Add(new Image { Source = DesktopIcons.WindowsDriveIcon(root, info.Item2 == DriveType.Network, true), Width = 36, Height = 36, Margin = new Thickness(0, 0, 14, 0) });
                    heading.Children.Add(new TextBlock { Text = String.IsNullOrWhiteSpace(info.Item1) ? root : info.Item1 + " (" + root.TrimEnd('\\') + ")", FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                    details.Children.Add(heading);
                    AddRow(L.Text("driveProperties.type"), DriveTypeName(info.Item2));
                    AddRow(L.Text("driveProperties.fileSystem"), info.Item3);
                    long total = info.Item4, free = info.Item5, used = Math.Max(0, total - free);
                    details.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = total > 0 ? Math.Min(100, used * 100.0 / total) : 0, Height = 14, Margin = new Thickness(0, 16, 0, 12), Foreground = FluentTheme.Accent });
                    AddAnalysisRow(L.Text("driveProperties.used"), used);
                    AddAnalysisRow(L.Text("driveProperties.free"), free);
                    AddRow(L.Text("driveProperties.capacity"), ByteSize.Format(total));
                    details.Children.Add(new TextBlock { Text = L.Text("driveProperties.analysisHint"), Foreground = FluentTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
                } catch (Exception ex) {
                    if (!IsLoaded) return;
                    DialogGeometry.PreserveCenterAfterLayout(this);
                    details.Children.Clear(); details.Children.Add(new TextBlock { Text = L.Text("drive.capacityUnavailable") + "\n" + ex.Message, TextWrapping = TextWrapping.Wrap });
                }
            };
        }
        internal static Tuple<string, DriveType, string, long, long> ReadDrive(string path) {
            var drive = new DriveInfo(path);
            return Tuple.Create(drive.VolumeLabel, drive.DriveType, drive.DriveFormat, drive.TotalSize, drive.TotalFreeSpace);
        }
        static string DriveTypeName(DriveType type) {
            switch(type) {
                case DriveType.Fixed: return L.Text("driveProperties.fixed");
                case DriveType.Removable: return L.Text("driveProperties.removable");
                case DriveType.Network: return L.Text("driveProperties.network");
                case DriveType.CDRom: return L.Text("driveProperties.optical");
                case DriveType.Ram: return L.Text("driveProperties.ram");
                default: return L.Text("driveProperties.unknown");
            }
        }
        void AddRow(string label, string value) {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = label, Foreground = FluentTheme.Muted, VerticalAlignment = VerticalAlignment.Center });
            var text = new TextBlock { Text = value, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(text, 1); row.Children.Add(text); details.Children.Add(row);
        }
        void AddAnalysisRow(string label, long bytes) {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = label, Foreground = FluentTheme.Muted, VerticalAlignment = VerticalAlignment.Center });
            var link = new TextBlock { Cursor = System.Windows.Input.Cursors.Hand, Foreground = FluentTheme.Accent, VerticalAlignment = VerticalAlignment.Center, ToolTip = L.Text("analyzer.title") };
            var hyperlink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(ByteSize.Format(bytes)));
            hyperlink.Click += delegate { var main = Owner as MainWindow; if (main == null) return; Close(); new DiskAnalyzerWindow(main, root, target => panel.Navigate(target, true)).Show(); };
            link.Inlines.Add(hyperlink); Grid.SetColumn(link, 1); row.Children.Add(link); details.Children.Add(row);
        }
    }
}
