using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public static class CopyUi
    {
        public static void Style(Window window, Window owner, string title, double width)
        {
            window.Owner = owner; window.Title = title; window.Width = width; window.SizeToContent = SizeToContent.Height;
            window.ResizeMode = ResizeMode.NoResize; window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FluentTheme.Dialog(window);
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            window.Icon = DesktopIcons.Drawing("app"); window.UseLayoutRounding = true; window.ShowInTaskbar = false;
        }
        public static Button Button(string text, Action action)
        {
            var button = CompactTheme.Button(text, text, delegate { action(); });
            button.ClearValue(Wpf.Ui.Controls.Button.MouseOverBackgroundProperty); button.ClearValue(Wpf.Ui.Controls.Button.MouseOverBorderBrushProperty);
            button.Height = 30; button.MinWidth = text.Length <= 3 && text != "OK" ? 28 : 86; button.Margin = new Thickness(3); button.Padding = new Thickness(12, 3, 12, 3);
            return button;
        }
        public static TextBlock Label(string text) { return new TextBlock { Text = text, Margin = new Thickness(2, 4, 2, 7), TextWrapping = TextWrapping.Wrap }; }
        public static string Bytes(long bytes)
        {
            double n = bytes; string[] units = { "B", "kB", "MB", "GB", "TB" }; int i = 0;
            while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
            return n.ToString(i == 0 ? "N0" : "N2") + " " + units[i];
        }
        public static string Prompt(Window owner, string title, string value)
        {
            var window = new Window(); Style(window, owner, title, 470);
            var panel = new StackPanel { Margin = new Thickness(12) }; window.Content = panel;
            panel.Children.Add(Label(title)); var input = new TextBox { Text = value, Margin = new Thickness(2, 3, 2, 9) }; panel.Children.Add(input);
            var buttons = new UniformGrid { Columns = 2 };
            var ok = Button("OK", delegate { window.DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = Button(L.Text("action.cancel"), delegate { window.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel); panel.Children.Add(buttons);
            window.Loaded += delegate { input.Focus(); input.SelectAll(); };
            return window.ShowDialog() == true ? input.Text : null;
        }
    }

    public class CopySetupDialog : Window
    {
        public CopyOptions Options;
        readonly ComboBox destination = new ComboBox { IsEditable = true, Height = 26 };
        readonly ComboBox filter = new ComboBox { IsEditable = true, Text = "*.*", Height = 26 };
        readonly ComboBox rule = new ComboBox { SelectedIndex = 0, Margin = new Thickness(2, 3, 2, 8) };
        readonly CheckBox permissions = new CheckBox { Content = L.Text("copy.copyNtfsPermissions"), Margin = new Thickness(2, 6, 2, 7) };
        readonly CheckBox verify = new CheckBox { Content = L.Text("copy.verifySha256Option"), Margin = new Thickness(8, 6, 2, 7) };
        readonly CheckBox readOnly = new CheckBox { Content = L.Text("copy.allowReadOnlyOverwrite"), Margin = new Thickness(2, 4, 2, 4) };
        readonly CheckBox times = new CheckBox { Content = L.Text("copy.preserveModifiedTime"), IsChecked = true, Margin = new Thickness(2, 4, 2, 4) };
        readonly CheckBox attributes = new CheckBox { Content = L.Text("copy.preserveAttributes"), IsChecked = true, Margin = new Thickness(2, 4, 2, 4) };
        readonly CheckBox hidden = new CheckBox { Content = L.Text("copy.skipHiddenAndSystem"), Margin = new Thickness(2, 4, 2, 4) };
        readonly CheckBox empty = new CheckBox { Content = L.Text("copy.copyEmptyFolders"), IsChecked = true, Margin = new Thickness(2, 4, 2, 4) };
        readonly CheckBox fileTarget = new CheckBox { Content = L.Text("copy.destinationIncludesNewName"), Margin = new Thickness(2, 4, 2, 4) };
        readonly string sourceDirectory;
        readonly bool moving, single;
        public CopySetupDialog(Window owner, IList<Entry> entries, string target, IEnumerable<string> recent, bool move = false, ProgramSettings defaults = null)
        {
            CopyUi.Style(this, owner, move ? L.Text("copy.moveDialogTitle") : L.Text("copy.copyDialogTitle"), 690);
            defaults = defaults ?? ProgramSettings.Load();
            verify.IsChecked = defaults.DefaultCopyVerify; permissions.IsChecked = defaults.DefaultCopyPermissions; times.IsChecked = defaults.DefaultCopyTimes;
            attributes.IsChecked = defaults.DefaultCopyAttributes; empty.IsChecked = defaults.DefaultCopyEmptyFolders; hidden.IsChecked = defaults.DefaultCopySkipHidden;
            moving = move; single = entries.Count == 1; sourceDirectory = move && single ? Path.GetDirectoryName(entries[0].FullPath.TrimEnd('\\')) : target;
            verify.ToolTip = L.Text("copy.verificationCostHelp");
            var panel = new StackPanel { Margin = new Thickness(9) }; Content = panel;
            panel.Children.Add(CopyUi.Label((move ? L.Text("copy.moveTotalItemsPrefix") : L.Text("copy.copyTotalItemsPrefix")) + entries.Count + ") do:"));
            destination.Text = target.TrimEnd('\\') + "\\*.*"; foreach (string path in recent) destination.Items.Add(path);
            var pathRow = new DockPanel { Margin = new Thickness(0, 7, 0, 8) };
            var makeFolder = CopyUi.Button("+ F7", NewFolder); DockPanel.SetDock(makeFolder, Dock.Right); pathRow.Children.Add(makeFolder); pathRow.Children.Add(destination); panel.Children.Add(pathRow);
            if (moving && single) panel.Children.Add(new TextBlock { Text = L.Text("copy.destinationRenameHelp"), Foreground = FluentTheme.Muted, Margin = new Thickness(2, 0, 2, 6), TextWrapping = TextWrapping.Wrap });
            var filterLabel = new DockPanel();
            var stats = CopyUi.Label(CopyUi.Bytes(entries.Sum(x => x.Bytes)) + "   📄 " + entries.Count(x => !x.IsDirectory) + "   📁 " + entries.Count(x => x.IsDirectory));
            stats.ToolTip = L.Text("copy.selectionCountHelp"); DockPanel.SetDock(stats, Dock.Right); filterLabel.Children.Add(stats);
            filterLabel.Children.Add(CopyUi.Label(L.Text("copy.fileMaskLabel"))); panel.Children.Add(filterLabel);
            foreach (string mask in new[] { "*.*", "*.txt;*.md", "*.jpg;*.jpeg;*.png;*.webp", "*.pdf;*.docx;*.xlsx", "*.mp4;*.mkv;*.avi", "*.* | *.tmp;*.bak" }) filter.Items.Add(mask);
            filter.ToolTip = L.Text("copy.maskSyntaxHelp");
            var maskRow = new DockPanel(); var masks = CopyUi.Button("+ F8", delegate { filter.IsDropDownOpen = true; }); DockPanel.SetDock(masks, Dock.Right); maskRow.Children.Add(masks); maskRow.Children.Add(filter); panel.Children.Add(maskRow);
            var checks = new DockPanel(); DockPanel.SetDock(verify, Dock.Right); checks.Children.Add(verify); checks.Children.Add(permissions); panel.Children.Add(checks);
            var options = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(2, 3, 2, 7) };
            options.Children.Add(CopyUi.Label(L.Text("copy.existingDestinationLabel")));
            foreach (string text in new[] { L.Text("conflict.ask"), L.Text("conflict.overwriteAll"), L.Text("conflict.overwriteOlderTargets"), L.Text("conflict.skipAll"), L.Text("conflict.overwriteDifferent"), L.Text("conflict.overwriteIfSourceLarger"), L.Text("conflict.overwriteIfSourceSmaller"), L.Text("conflict.autoRenameCopies") }) rule.Items.Add(text);
            rule.SelectedIndex = 0; options.Children.Add(rule);
            foreach (CheckBox check in new[] { readOnly, times, attributes, hidden, empty, fileTarget }) options.Children.Add(check);
            fileTarget.IsEnabled = single;
            panel.Children.Add(options);
            var buttons = new UniformGrid { Columns = 5 };
            var ok = CopyUi.Button("OK", delegate { Accept(false); }); ok.IsDefault = true; buttons.Children.Add(ok);
            buttons.Children.Add(CopyUi.Button(L.Text("functionKey.enqueue"), delegate { Accept(true); }));
            buttons.Children.Add(CopyUi.Button(L.Text("panel.treeLabel"), Browse));
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            Button more = null; more = CopyUi.Button(L.Text("copy.showOptions"), delegate { options.Visibility = options.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; more.Content = options.Visibility == Visibility.Visible ? L.Text("copy.hideOptions") : L.Text("copy.showOptions"); }); buttons.Children.Add(more);
            panel.Children.Add(buttons);
            Loaded += delegate { destination.Focus(); var textBox = destination.Template.FindName("PART_EditableTextBox", destination) as TextBox; if (textBox != null) textBox.SelectAll(); };
            PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.F2) { Accept(true); e.Handled = true; }
                if (e.Key == System.Windows.Input.Key.F7) { NewFolder(); e.Handled = true; }
                if (e.Key == System.Windows.Input.Key.F8) { filter.IsDropDownOpen = true; e.Handled = true; }
            };
        }
        string Target()
        {
            string path = Environment.ExpandEnvironmentVariables(destination.Text.Trim());
            if (path.EndsWith("\\*.*")) path = path.Substring(0, path.Length - 3);
            if (String.IsNullOrWhiteSpace(path)) throw new IOException(L.Text("copy.destinationRequired"));
            if (!Path.IsPathRooted(path)) path = Path.Combine(sourceDirectory, path);
            if (path.IndexOfAny(new[] { '*', '?' }) >= 0) throw new IOException(L.Text("copy.supportedDestinationSyntax"));
            return Path.GetFullPath(path);
        }
        void Accept(bool queue)
        {
            try
            {
                Options = CreateOptions(queue);
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("copy.destinationTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        internal CopyOptions CreateOptions(bool queue)
        {
            string target = Target(); if (filter.Text.Split('|').Length > 2) throw new IOException(L.Text("copy.multipleExclusionSeparators"));
            string raw = destination.Text.Trim();
            bool folder = raw.EndsWith("\\") || raw.EndsWith("/") || raw.EndsWith("\\*.*") || Directory.Exists(target);
            bool exact = fileTarget.IsChecked == true || (moving && single && !folder);
            return new CopyOptions { Target = target, Filter = filter.Text, Queue = queue, Move = moving, Verify = verify.IsChecked == true, Permissions = permissions.IsChecked == true,
                ReadOnly = readOnly.IsChecked == true, Times = times.IsChecked == true, Attributes = attributes.IsChecked == true, SkipHidden = hidden.IsChecked == true,
                EmptyFolders = empty.IsChecked == true, TargetIsFile = exact, Rule = (CopyRule)rule.SelectedIndex };
        }
        void Browse()
        {
            using (var picker = new System.Windows.Forms.FolderBrowserDialog())
            { picker.Description = L.Text("copy.destinationFolderTitle"); try { picker.SelectedPath = Target(); } catch { } if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) destination.Text = picker.SelectedPath.TrimEnd('\\') + "\\*.*"; }
        }
        void NewFolder()
        {
            string name = CopyUi.Prompt(this, L.Text("copy.newDestinationFolderTitle"), ""); if (name == null) return;
            try { FileOperations.ValidateName(name); string path = Path.Combine(Target(), name); Directory.CreateDirectory(path); destination.Text = path + "\\*.*"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("folder.createFailed")); }
        }
    }

    public class CopyConflictDialog : Window
    {
        public CopyDecision Decision = new CopyDecision(CopyChoice.Cancel);
        readonly CheckBox readOnly = new CheckBox { Content = L.Text("conflict.allowReadOnlyOverwriteForJob"), Margin = new Thickness(3, 5, 3, 6) };
        readonly CopyConflict conflict;
        public CopyConflictDialog(Window owner, CopyConflict conflict)
        {
            this.conflict = conflict; CopyUi.Style(this, owner, L.Text("conflict.fileExistsTitle"), 590);
            var panel = new StackPanel { Margin = new Thickness(9) }; Content = panel;
            var thumbs = new List<Image>();
            panel.Children.Add(Info(L.Text("conflict.overwriteLabel"), conflict.Target, conflict.TargetDirectory, conflict.TargetSize, conflict.TargetTime, thumbs));
            panel.Children.Add(new TextBlock { Text = "↑", Foreground = Brushes.OrangeRed, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(Info("Souborem:", conflict.Source, conflict.SourceDirectory, conflict.SourceSize, conflict.SourceTime, thumbs));
            var previews = new CheckBox { Content = L.Text("action.preview"), Margin = new Thickness(3, 5, 3, 5) };
            previews.Checked += delegate { foreach (Image image in thumbs) { image.Source = Thumbnail((string)image.Tag); image.Visibility = image.Source == null ? Visibility.Collapsed : Visibility.Visible; } };
            previews.Unchecked += delegate { foreach (Image image in thumbs) image.Visibility = Visibility.Collapsed; }; panel.Children.Add(previews);
            if (conflict.IsReadOnly) panel.Children.Add(readOnly);
            bool compatible = !conflict.SourceDirectory && !conflict.TargetDirectory;
            if (!compatible) panel.Children.Add(CopyUi.Label(L.Text("conflict.fileFolderHelp")));
            var buttons = new UniformGrid { Columns = 3 };
            Add(buttons, L.Code("action.overwrite"), CopyChoice.Overwrite, compatible); Add(buttons, L.Text("conflict.overwriteAll"), CopyChoice.OverwriteAll, compatible); Add(buttons, L.Code("action.skip"), CopyChoice.Skip, true);
            Add(buttons, L.Text("action.cancel"), CopyChoice.Cancel, true); Add(buttons, L.Text("conflict.overwriteOlder"), CopyChoice.Older, compatible); Add(buttons, L.Text("conflict.skipAll"), CopyChoice.SkipAll, true);
            buttons.Children.Add(CopyUi.Button(L.Text("action.rename"), delegate
            {
                string name = CopyUi.Prompt(this, L.Text("conflict.newCopyNameTitle"), Path.GetFileName(conflict.Target)); if (name == null) return;
                try
                {
                    FileOperations.ValidateName(name); if (String.Equals(name, Path.GetFileName(conflict.Target), StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("conflict.differentNameRequired"));
                    Decision = new CopyDecision(CopyChoice.Rename, name); DialogResult = true;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            }));
            var append = CopyUi.Button(L.Text("action.append"), delegate
            {
                if (MessageBox.Show(this, L.Text("conflict.confirmAppendBytes"), L.Text("conflict.appendTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) Choose(CopyChoice.Append);
            }); append.IsEnabled = compatible; buttons.Children.Add(append);
            var extras = new StackPanel { Visibility = Visibility.Collapsed };
            Button more = null; more = CopyUi.Button(L.Text("conflict.showMoreOptions"), delegate { extras.Visibility = extras.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; more.Content = extras.Visibility == Visibility.Visible ? L.Text("conflict.showFewerOptions") : L.Text("conflict.showMoreOptions"); }); buttons.Children.Add(more);
            panel.Children.Add(buttons);
            foreach (var pair in new[] { Tuple.Create(L.Text("conflict.overwriteDifferentSizeOrTime"), CopyChoice.Different), Tuple.Create(L.Text("conflict.overwriteIfSourceLarger"), CopyChoice.Larger), Tuple.Create(L.Text("conflict.overwriteIfSourceSmaller"), CopyChoice.Smaller), Tuple.Create(L.Text("conflict.autoRenameAll"), CopyChoice.RenameAll) })
                Add(extras, pair.Item1, pair.Item2, compatible || pair.Item2 == CopyChoice.RenameAll);
            extras.Children.Add(CopyUi.Label(L.Text("conflict.applyRulesHelp"))); panel.Children.Add(extras);
        }
        void Choose(CopyChoice choice) { Decision = new CopyDecision(choice) { AllowReadOnly = readOnly.IsChecked == true }; DialogResult = true; }
        void Add(Panel panel, string text, CopyChoice choice, bool enabled)
        {
            var b = CopyUi.Button(text, delegate { Choose(choice); }); b.IsEnabled = enabled;
            if (choice == CopyChoice.Overwrite) FluentTheme.Primary(b);
            panel.Children.Add(b);
        }
        UIElement Info(string title, string path, bool directory, long bytes, DateTime time, List<Image> thumbs)
        {
            var group = new GroupBox { Header = title, Margin = new Thickness(1, 0, 1, 0), Padding = new Thickness(8, 2, 8, 6) };
            var inner = new DockPanel(); var image = new Image { Tag = path, Width = 64, Height = 56, Margin = new Thickness(6), Visibility = Visibility.Collapsed }; thumbs.Add(image); DockPanel.SetDock(image, Dock.Right); inner.Children.Add(image);
            var text = new StackPanel(); text.Children.Add(CopyUi.Label(path)); text.Children.Add(new TextBlock { Text = (directory ? L.Text("common.folder") : bytes.ToString("N0") + L.Text("common.bytesSuffix")) + ", " + time.ToString("dd.MM.yyyy HH:mm:ss"), Margin = new Thickness(12, 0, 0, 0) });
            inner.Children.Add(text); group.Content = inner; return group;
        }
        static ImageSource Thumbnail(string path)
        {
            if (!new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(Path.GetExtension(path).ToLowerInvariant())) return null;
            try { var image = new BitmapImage(); using (var stream = File.OpenRead(path)) { image.BeginInit(); image.StreamSource = stream; image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 96; image.EndInit(); image.Freeze(); } return image; } catch { return null; }
        }
    }

    public class CopyProgressDialog : Window
    {
        internal Action BecameBackground, BecameForeground;
        readonly TransferWindowState placement;
        internal void RestoreProgress() { placement.Restore(); }
        readonly CopyControl control;
        Button pauseButton;
        internal void TogglePause() { control.Pause(!control.Paused); pauseButton.Content = control.Paused ? L.Text("action.resume") : L.Text("action.pause"); UpdateMetrics(); }
        readonly TextBlock phase = Line(L.Text("copy.loadingFiles")), source = Line(""), target = Line(""), count = Line(""), totals = Line("");
        readonly TextBlock filePercent = Line("0 %"), totalPercent = Line("0 %"), speed = Line(L.Text("transfer.unknownRate"));
        readonly LocalCopyProgressBar fileProgress = new LocalCopyProgressBar(false) { Height = 3, ToolTip = L.Text("progress.currentFileLabel") };
        readonly LocalCopyProgressBar totalProgress = new LocalCopyProgressBar(true) { Height = 3, ToolTip = L.Text("progress.wholeJobLabel") };
        readonly System.Windows.Threading.DispatcherTimer metricsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        CopyUpdate latest;
        DateTime lastUpdate;
        internal Action MetricsChanged;
        bool complete;
        static TextBlock Line(string text) { return new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }; }
        internal NetworkTransferMetrics Metrics()
        {
            bool active = !complete && !control.Paused && !control.Cancelled && latest != null && (DateTime.UtcNow - lastUpdate).TotalSeconds < 3;
            return new NetworkTransferMetrics { BytesPerSecond = active ? latest.BytesPerSecond : 0, RemainingSeconds = active ? latest.RemainingSeconds : null };
        }
        void UpdateMetrics()
        {
            var metrics = Metrics(); string state = control.Cancelled ? L.Text("copy.cancellingStatus") : control.Paused ? L.Code("state.paused") : latest == null ? L.Code("copy.loadingFiles") : latest.Phase;
            phase.Text = latest != null && latest.Phase == L.Code("copy.loadingFiles") && !control.Paused && !control.Cancelled
                ? state + L.Text("search.foundCountSeparator") + latest.TotalFiles.ToString("N0") + L.Text("copy.filesAndBytesSeparator") + CopyUi.Bytes(latest.TotalBytes)
                : state + L.Text("transfer.remainingPrefix") + MainWindow.TransferTime(metrics.RemainingSeconds);
            fileProgress.Preparing = totalProgress.Preparing = latest == null || latest.Phase == L.Code("copy.loadingFiles") || latest.Phase == L.Code("copy.savingPlan");
            speed.Text = (metrics.BytesPerSecond / 1048576.0).ToString("F1") + " MB/s";
            if (MetricsChanged != null) MetricsChanged();
        }
        public CopyProgressDialog(Window owner, CopyControl control)
        {
            this.control = control; CopyUi.Style(this, owner, L.Text("copy.progressWindowTitle"), 580);
            ResizeMode = ResizeMode.CanMinimize; SizeToContent = SizeToContent.Manual; Height = 286;
            placement = new TransferWindowState(this,delegate { if (BecameBackground != null) BecameBackground(); },delegate { if (BecameForeground != null) BecameForeground(); });
            var panel = new Grid { Margin = new Thickness(12) }; Content = panel;
            foreach (double height in new[] { 22.0,20,20,12,30,5,46,24,40 }) panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
            panel.ColumnDefinitions.Add(new ColumnDefinition()); panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            phase.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumnSpan(phase,2); panel.Children.Add(phase);
            Grid.SetRow(source,1); Grid.SetColumnSpan(source,2); panel.Children.Add(source);
            Grid.SetRow(target,2); Grid.SetColumnSpan(target,2); panel.Children.Add(target);
            Grid.SetRow(fileProgress,4); panel.Children.Add(fileProgress); Grid.SetRow(filePercent,4); Grid.SetColumn(filePercent,1); filePercent.HorizontalAlignment = HorizontalAlignment.Right; panel.Children.Add(filePercent);
            Grid.SetRow(totalProgress,6); panel.Children.Add(totalProgress);
            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }; right.Children.Add(totalPercent); right.Children.Add(speed); totalPercent.HorizontalAlignment = speed.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetRow(right,6); Grid.SetColumn(right,1); panel.Children.Add(right);
            Grid.SetRow(count,7); panel.Children.Add(count); Grid.SetRow(totals,7); Grid.SetColumnSpan(totals,2); totals.HorizontalAlignment = HorizontalAlignment.Right; panel.Children.Add(totals);
            var buttons = new Grid(); for (int i = 0; i < 3; i++) buttons.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(buttons,8); Grid.SetColumnSpan(buttons,2); panel.Children.Add(buttons);
            pauseButton = CopyUi.Button(L.Text("action.pause"), delegate { TogglePause(); }); pauseButton.HorizontalAlignment = HorizontalAlignment.Left; buttons.Children.Add(pauseButton);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { control.Cancel(); UpdateMetrics(); }); cancel.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(cancel,1); buttons.Children.Add(cancel);
            var minimize = CopyUi.Button(L.Text("action.minimize"), placement.Background); minimize.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(minimize,2); buttons.Children.Add(minimize);
            metricsTimer.Tick += delegate { UpdateMetrics(); }; Loaded += delegate { if (!complete) metricsTimer.Start(); }; Closed += delegate { metricsTimer.Stop(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!complete) { e.Cancel = true; control.Cancel(); UpdateMetrics(); } };
        }
        public void Update(CopyUpdate update)
        {
            latest = update; lastUpdate = DateTime.UtcNow; UpdateMetrics();
            source.Text = L.Text("transfer.sourceLabel") + update.Source; target.Text = L.Text("transfer.targetLabel") + update.Target;
            source.ToolTip = source.Text; target.ToolTip = target.Text;
            double file = update.FileTotal == 0 ? 0 : (double)update.FileDone / update.FileTotal;
            double total = update.TotalBytes == 0 ? (update.TotalFiles == 0 ? 0 : (double)update.Completed / update.TotalFiles) : (double)update.TotalDone / update.TotalBytes;
            fileProgress.Update(file); totalProgress.Update(total); filePercent.Text = (Math.Max(0,Math.Min(1,file)) * 100).ToString("F0") + " %"; totalPercent.Text = (Math.Max(0,Math.Min(1,total)) * 100).ToString("F0") + " %";
            count.Text = update.Completed + " / " + update.TotalFiles;
            totals.Text = CopyUi.Bytes(update.TotalDone) + " / " + CopyUi.Bytes(update.TotalBytes);
        }
        public void Finish() { complete = true; metricsTimer.Stop(); placement.Finish(); }
    }
}

