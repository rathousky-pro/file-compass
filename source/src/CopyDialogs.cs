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
        internal static void EnterDefault(Window window, Button primary)
        {
            window.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) {
                if (e.Key != System.Windows.Input.Key.Enter || System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.None) return;
                var input = System.Windows.Input.Keyboard.FocusedElement as TextBox;
                if (input != null && input.AcceptsReturn) return;
                if (FindOpenCombo(window)) return;
                if (!primary.IsEnabled || !primary.IsVisible) return;
                e.Handled = true;
                if (!e.IsRepeat) primary.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            };
        }
        static bool FindOpenCombo(DependencyObject root) {
            var combo = root as ComboBox; if (combo != null && combo.IsDropDownOpen) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (FindOpenCombo(VisualTreeHelper.GetChild(root, i))) return true;
            return false;
        }
        internal static void SectionedSurface(Window window, FrameworkElement content) {
            Surface(window, content);
            var outer = (Border)((Grid)window.Content).Children[0];
            outer.Background = CompactTheme.ToolbarSurface; outer.BorderBrush = CompactTheme.ChromeLine;
        }
        internal static void ConfigurationSurface(Window window, FrameworkElement content)
        {
            var inner = new Border { Tag = "configuration-dialog-inner", Background = CompactTheme.PanelSurface,
                BorderBrush = CompactTheme.FieldLine, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = content };
            Surface(window, inner);
            var outer = (Border)((Grid)window.Content).Children[0];
            outer.Background = CompactTheme.ToolbarSurface; outer.BorderBrush = CompactTheme.ChromeLine;
            outer.Padding = new Thickness(12);
        }
        internal static void Surface(Window window, FrameworkElement content)
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.Children.Add(new Border { Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = content });
            content.Margin = new Thickness(0); window.Content = root;
            AppearanceTheme.RegisterShell(window);
            var owner = window.Owner ?? window;
            var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(owner).Handle).WorkingArea;
            var dpi = System.Windows.PresentationSource.FromVisual(owner);
            double scale = dpi == null || dpi.CompositionTarget == null ? 1 : dpi.CompositionTarget.TransformToDevice.M22;
            window.MaxHeight = Math.Max(200, area.Height / scale - 32);
            window.MaxWidth = Math.Max(320, area.Width / scale - 32);
            window.Width = Math.Min(window.Width, window.MaxWidth);
            window.MinHeight = 0;
        }
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
            var panel = new DockPanel(); CopyUi.SectionedSurface(this, panel);
            var buttons = QueuePresentation.Actions(); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            buttons.Children.Add(CopyUi.Button(L.Text("functionKey.enqueue"), delegate { Accept(true); }));
            var ok = CopyUi.Button(L.Text(move ? "copy.designMove" : "copy.designCopy"), delegate { Accept(false); }); ok.IsDefault = true; FluentTheme.Primary(ok); ok.Background = FluentTheme.Accent; ok.Foreground = Brushes.White; ok.BorderBrush = FluentTheme.Accent; buttons.Children.Add(ok);
            CopyUi.EnterDefault(this, ok);
            var body = new StackPanel();
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(160, MaxHeight - 130) }; panel.Children.Add(scroll);
            var destinationSection = FluentTheme.Section(body, L.Text(move ? "copy.designMove" : "copy.designCopy"));
            destinationSection.Children.Add(new TextBlock { Text = L.Format("copy.designSummary", entries.Count(x => !x.IsDirectory), entries.Count(x => x.IsDirectory)) + (entries.Any(x => !x.IsDirectory) ? " · " + CopyUi.Bytes(entries.Where(x => !x.IsDirectory).Sum(x => x.Bytes)) : ""), Foreground = FluentTheme.Muted, Margin = new Thickness(2,0,2,18) });
            destinationSection.Children.Add(new TextBlock { Text = L.Text("copy.designDestination"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(2,0,2,5) });
            destination.Text = target.TrimEnd('\\') + "\\*.*"; foreach (string path in recent) destination.Items.Add(path);
            destination.Height = filter.Height = 32;
            destinationSection.Children.Add(destination);
            var pathActions = new WrapPanel { Margin = new Thickness(0,5,0,12) };
            pathActions.Children.Add(CopyUi.Button(L.Text("copy.designBrowse"), Browse));
            pathActions.Children.Add(CopyUi.Button(L.Text("copy.designNewFolder"), NewFolder)); destinationSection.Children.Add(pathActions);
            if (moving && single) destinationSection.Children.Add(new TextBlock { Text = L.Text("copy.destinationRenameHelp"), Foreground = FluentTheme.Muted, Margin = new Thickness(2,0,2,10), TextWrapping = TextWrapping.Wrap });
            var filterSection = FluentTheme.Section(body, L.Text("copy.designFilterOptions"));
            filterSection.Children.Add(new TextBlock { Text = L.Text("copy.fileMaskLabel"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(2,0,2,5) });
            foreach (string mask in new[] { "*.*", "*.txt;*.md", "*.jpg;*.jpeg;*.png;*.webp", "*.pdf;*.docx;*.xlsx", "*.mp4;*.mkv;*.avi", "*.* | *.tmp;*.bak" }) filter.Items.Add(mask);
            filter.ToolTip = L.Text("copy.maskSyntaxHelp"); filterSection.Children.Add(filter);
            var basic = new StackPanel { Margin = new Thickness(0,10,0,4) }; verify.Margin = new Thickness(2,4,2,4); basic.Children.Add(verify); filterSection.Children.Add(basic);
            var options = new StackPanel { Margin = new Thickness(2,10,2,4) };
            options.Children.Add(CopyUi.Label(L.Text("copy.existingDestinationLabel")));
            foreach (string text in new[] { L.Text("conflict.ask"), L.Text("conflict.overwriteAll"), L.Text("conflict.overwriteOlderTargets"), L.Text("conflict.skipAll"), L.Text("conflict.overwriteDifferent"), L.Text("conflict.overwriteIfSourceLarger"), L.Text("conflict.overwriteIfSourceSmaller"), L.Text("conflict.autoRenameCopies") }) rule.Items.Add(text);
            rule.SelectedIndex = 0; options.Children.Add(rule);
            foreach (CheckBox check in new[] { times, attributes, empty, hidden, readOnly, permissions, fileTarget }) options.Children.Add(check);
            fileTarget.IsEnabled = single;
            filterSection.Children.Add(new Expander { Header = L.Text("copy.designAdvanced"), Content = options, Margin = new Thickness(2,8,2,4) });
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
        readonly CompressionRatioTrend speedTrend = new CompressionRatioTrend { SpeedMode=true,Height=60 };
        Button pauseButton;
        internal void TogglePause() { control.Pause(!control.Paused); pauseButton.Content = control.Paused ? L.Text("action.resume") : L.Text("action.pause"); UpdateMetrics(); }
        readonly TextBlock phase = Line(L.Text("copy.loadingFiles")), source = Line(""), target = Line(""), count = Line(""), totals = Line("");
        readonly TextBlock filePercent = Line("0 %"), totalPercent = Line("0 %"), speed = Line(L.Text("transfer.unknownRate"));
        readonly LocalCopyProgressBar fileProgress = new LocalCopyProgressBar(false) { Height = 3, ToolTip = L.Text("progress.currentFileLabel") };
        readonly LocalCopyProgressBar totalProgress = new LocalCopyProgressBar(true) { Height = 3, ToolTip = L.Text("progress.wholeJobLabel") };
        readonly System.Windows.Threading.DispatcherTimer metricsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        CopyUpdate latest;
        internal string QueueStatus { get { return phase.Text + " · " + count.Text + " · " + totals.Text; } }
        internal double QueueFraction { get { return latest == null ? 0 : latest.TotalBytes > 0 ? (double)latest.TotalDone / latest.TotalBytes : latest.TotalFiles > 0 ? (double)latest.Completed / latest.TotalFiles : 0; } }
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
            if (!complete) speedTrend.Add(metrics.BytesPerSecond / 1048576.0, QueueFraction);
            speed.Text = (metrics.BytesPerSecond / 1048576.0).ToString("F1") + " MB/s";
            if (MetricsChanged != null) MetricsChanged();
        }
        public CopyProgressDialog(Window owner, CopyControl control)
        {
            this.control = control; CopyUi.Style(this, owner, L.Text("copy.progressWindowTitle"), 580);
            ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height;
            placement = new TransferWindowState(this,delegate { if (BecameBackground != null) BecameBackground(); },delegate { if (BecameForeground != null) BecameForeground(); });
            var body = new StackPanel(); CopyUi.SectionedSurface(this, body);
            var route = FluentTheme.Section(body, L.Text("copy.designTransfer"));
            phase.FontWeight = FontWeights.SemiBold; phase.Margin = new Thickness(0,0,0,10); route.Children.Add(phase);
            source.Margin = target.Margin = new Thickness(0,3,0,3); route.Children.Add(source); route.Children.Add(target);
            var progressSection = FluentTheme.Section(body, L.Text("copy.designProgress"));
            Action<TextBlock,LocalCopyProgressBar,TextBlock> track = delegate(TextBlock label, LocalCopyProgressBar bar, TextBlock percent) {
                progressSection.Children.Add(label);
                var row = new DockPanel { Margin = new Thickness(0,6,0,12) };
                percent.Width = 64; percent.TextAlignment = TextAlignment.Right; DockPanel.SetDock(percent,Dock.Right); row.Children.Add(percent);
                bar.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(bar); progressSection.Children.Add(row);
            };
            track(Line(L.Text("progress.currentFileLabel")),fileProgress,filePercent);
            track(Line(L.Text("progress.wholeJobLabel")),totalProgress,totalPercent);
            var stats = new DockPanel(); speed.HorizontalAlignment=HorizontalAlignment.Right; DockPanel.SetDock(speed,Dock.Right);stats.Children.Add(speed);stats.Children.Add(count);progressSection.Children.Add(stats);
            totals.Margin=new Thickness(0,5,0,0);progressSection.Children.Add(totals);
            var speedSection=FluentTheme.Section(body,L.Text("transfer.speedHistory"));speedSection.Children.Add(speedTrend);
            var buttons=QueuePresentation.Actions();body.Children.Add(buttons);
            pauseButton=CopyUi.Button(L.Text("action.pause"),delegate{TogglePause();});buttons.Children.Add(pauseButton);
            buttons.Children.Add(CopyUi.Button(L.Text("action.cancel"),delegate{control.Cancel();UpdateMetrics();}));
            buttons.Children.Add(CopyUi.Button(L.Text("copy.backgroundAction"),placement.Background));
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

