using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public sealed class ArchivePackOptions
    {
        public string Target, Password;
        public int Level = 6;
        public bool Move;
        public long VolumeBytes;
        public bool? NewestFileDate;
        public bool Paths = true, Recursive = true, Separate, ExcludeBase;
    }
    public sealed class ArchivePackDialog : Window
    {
        readonly TextBox target = new TextBox { Margin = new Thickness(2, 3, 5, 5) };
        string format = "zip";
        public ArchivePackOptions Options { get; private set; }
        public ArchivePackDialog(Window owner, int count, string initial)
        {
            CopyUi.Style(this, owner, L.Text("archive.packDialogTitle"), 730);
            var body = new DockPanel { Margin = new Thickness(8) }; Content = body;
            var buttons = FluentTheme.Actions(); DockPanel.SetDock(buttons, Dock.Bottom); body.Children.Add(buttons);
            var right = new StackPanel { Width = 216, Margin = new Thickness(12, 0, 0, 4) }; DockPanel.SetDock(right, Dock.Right); body.Children.Add(right);
            var compression = new DockPanel(); right.Children.Add(compression);
            var level = new ComboBox { Width = 60, MinWidth = 0 }; for (int i = 0; i <= 9; i++) level.Items.Add(i); level.SelectedIndex = 6;
            level.ToolTip = L.Text("archive.memoryPolicy");
            DockPanel.SetDock(level, Dock.Right); compression.Children.Add(level); compression.Children.Add(CopyUi.Label(L.Text("archive.compressionLevelLabel")));
            var group = new GroupBox { Header = L.Text("archive.archiverLabel"), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0) }; right.Children.Add(group);
            var choices = new StackPanel(); group.Content = choices;
            var radios = new UniformGrid { Columns = 2 }; choices.Children.Add(radios);
            foreach (string name in new[] { "ZIP", "RAR*", "7-ZIP", "TAR", "GZ", "ARJ*", "LHA*" })
            {
                string extension = name == "7-ZIP" ? "7z" : name == "LHA*" ? "lzh" : name.TrimEnd('*').ToLowerInvariant();
                var radio = new RadioButton { Content = name, GroupName = "ArchiveFormat", Margin = new Thickness(1, 3, 1, 3), IsEnabled = true };
                if (!radio.IsEnabled) radio.ToolTip = L.Text("archive.createFormatUnsupported");
                ToolTipService.SetShowOnDisabled(radio, true);
                radio.Checked += delegate { level.IsEnabled = extension != "tar" && extension != "arj" && extension != "lzh"; format = extension; target.Text = ChangeFormat(target.Text, extension); };
                radios.Children.Add(radio); if (extension == "zip") radio.IsChecked = true;
            }
            choices.Children.Add(new TextBlock { Text = L.Text("archive.formatCreationHelp"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) }); choices.Children.Add(CopyUi.Button(L.Text("action.settings"), delegate { new ConfigurationDialog(this, L.Text("settings.archivesSection")).ShowDialog(); }));
            var left = new StackPanel(); body.Children.Add(new GroupBox { Header = L.Text("archive.filesAndOptionsGroup"), Content = left, Margin = new Thickness(0) });
            left.Children.Add(CopyUi.Label(L.Text("archive.totalFilesPrefix") + count + L.Text("archive.targetQuestionSuffix"))); target.Text = initial; left.Children.Add(target);
            Func<string, bool, CheckBox> add = delegate(string text, bool value) { var check = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(2, 3, 0, 3) }; left.Children.Add(check); return check; };
            var paths = add(L.Text("archive.preservePaths"), true);
            var recursive = add(L.Text("archive.includeSubfolders"), true);
            var volumes = add(L.Text("archive.multiVolume"), false);
            var volumeSize = new ComboBox { IsEditable = true, ItemsSource = new[] { "100 MB", "700 MB", "4 GB" }, Text = "100 MB", IsEnabled = false, Margin = new Thickness(18,1,5,4) }; left.Children.Add(volumeSize);
            volumes.Checked += delegate { volumeSize.IsEnabled = true; }; volumes.Unchecked += delegate { volumeSize.IsEnabled = false; };
            var move = add(L.Text("archive.moveInto"), false); move.ToolTip = L.Text("archive.moveVerificationHelp");
            foreach (string label in new[] { L.Text("archive.selfExtracting") })
            { var check = add(label, false); check.IsEnabled = false; check.ToolTip = L.Text("common.optionNotSupported"); ToolTipService.SetShowOnDisabled(check, true); }
            var separate = add(L.Text("archive.separatePerSelection"), false);
            var exclude = add(L.Text("archive.omitBaseFolder"), false); exclude.Margin = new Thickness(18, 3, 0, 3); exclude.IsEnabled = false;
            separate.Checked += delegate { exclude.IsEnabled = true; }; separate.Unchecked += delegate { exclude.IsEnabled = false; exclude.IsChecked = false; };
            var password = add(L.Text("archive.setPassword"), false);
            var secret = new PasswordBox { Margin = new Thickness(18, 1, 5, 4), Visibility = Visibility.Collapsed }; left.Children.Add(secret);
            password.Checked += delegate { secret.Visibility = Visibility.Visible; secret.Focus(); }; password.Unchecked += delegate { secret.Visibility = Visibility.Collapsed; secret.Clear(); };
            var ok = CopyUi.Button("OK", delegate
            {
                try
                {
                    string destination = Environment.ExpandEnvironmentVariables(target.Text.Trim());
                    if (count > 1 && separate.IsChecked != true && format == "gz") throw new IOException(L.Text("archive.singleStreamRequired"));
                    if (!Path.IsPathRooted(destination)) throw new IOException(L.Text("archive.absoluteOutputPathRequired"));
                    destination = Path.GetFullPath(ChangeFormat(destination, format));
                    if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new IOException(L.Text("archive.outputFolderMissing"));
                    if (password.IsChecked == true && secret.Password.Length == 0) throw new IOException(L.Text("archive.passwordRequired"));
                    if (password.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.passwordRequiresZipOr7z"));
                    ArchiveSettings.Load().ValidateCreationTool(destination);
                    if (move.IsChecked == true && recursive.IsChecked != true) throw new IOException(L.Text("archive.moveNeedsRecursive"));
                    if (volumes.IsChecked == true && format != "zip" && format != "7z") throw new IOException(L.Text("archive.volumeFormats"));
                    if ((volumes.IsChecked == true || move.IsChecked == true) && File.Exists(destination)) throw new IOException(L.Text("parts.outputExists"));
                    Options = new ArchivePackOptions { Move = move.IsChecked == true, VolumeBytes = volumes.IsChecked == true ? FileParts.ParseSize(volumeSize.Text) : 0, Target = destination, Level = level.SelectedIndex, Paths = paths.IsChecked == true, Recursive = recursive.IsChecked == true,
                        Separate = separate.IsChecked == true, ExcludeBase = exclude.IsChecked == true, Password = password.IsChecked == true ? secret.Password : null }; DialogResult = true;
                }
                catch (ArchiveToolMissingException ex) { new ConfigurationDialog(this, L.Text("settings.archivesSection"), ex.Message).ShowDialog(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("action.compressArchive")); }
            }); ok.IsDefault = true; buttons.Children.Add(ok);
            buttons.Children.Add(CopyUi.Button(L.Text("panel.treeLabel"), delegate
            {
                using (var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = Path.GetDirectoryName(target.Text) })
                    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) target.Text = Path.Combine(picker.SelectedPath, Path.GetFileName(target.Text));
            }));
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            buttons.Children.Add(CopyUi.Button(L.Text("action.help"), delegate { HelpService.Open(this,"archives"); }));
            Loaded += delegate { target.Focus(); target.SelectAll(); };
        }
        internal static string ChangeFormat(string path, string format)
        {
            string suffix = ArchiveEngine.OutputSuffix(path);
            return (suffix.Length == 0 ? path : path.Substring(0, path.Length - suffix.Length)) + "." + format;
        }
    }
}

