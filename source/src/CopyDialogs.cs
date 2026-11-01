using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
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
            var root = new Grid { Margin = new Thickness(0), Background=CompactTheme.Chrome };
            root.Children.Add(new Border { Tag="window-shell", Background = CompactTheme.Chrome, BorderBrush = CompactTheme.Line,
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
            return ByteSize.Format(bytes);
        }
        public static string Prompt(Window owner, string title, string value)
        {
            return SettingsUx.Prompt(owner,title,value,null,"action.save");
        }
    }

    public partial class CopySetupDialog : Window
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
            CopyUi.Style(this, owner, move ? L.Text("copy.moveDialogTitle") : L.Text("copy.copyDialogTitle"), 780);
            defaults = defaults ?? ProgramSettings.Load();
            verify.IsChecked = defaults.DefaultCopyVerify; permissions.IsChecked = defaults.DefaultCopyPermissions; times.IsChecked = defaults.DefaultCopyTimes;
            attributes.IsChecked = defaults.DefaultCopyAttributes; empty.IsChecked = defaults.DefaultCopyEmptyFolders; hidden.IsChecked = defaults.DefaultCopySkipHidden;
            moving = move; single = entries.Count == 1; sourceDirectory = move && single ? Path.GetDirectoryName(entries[0].FullPath.TrimEnd('\\')) : target;
            verify.ToolTip = L.Text("copy.verificationCostHelp");
            var summary=L.Format("copy.designSummary",entries.Count(x=>!x.IsDirectory),entries.Count(x=>x.IsDirectory))+(entries.Any(x=>!x.IsDirectory)?" · "+CopyUi.Bytes(entries.Where(x=>!x.IsDirectory).Sum(x=>x.Bytes)):"");
            var layout=new OperationDialogLayout(this,L.Text(move?"copy.designMove":"copy.designCopy"),summary);
            var buttons=layout.Actions;
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
            buttons.Children.Add(CopyUi.Button(L.Text("functionKey.enqueue"), delegate { Accept(true); }));
            var ok = CopyUi.Button(L.Text(move ? "copy.designMove" : "copy.designCopy"), delegate { Accept(false); }); ok.IsDefault = true; FluentTheme.Primary(ok); ok.Background = FluentTheme.Accent; ok.Foreground = Brushes.White; ok.BorderBrush = FluentTheme.Accent; buttons.Children.Add(ok);
            CopyUi.EnterDefault(this, ok);
            var body=layout.AddPage(L.Text("operation.basic"));
            var selection=layout.AddPage(L.Text("operation.selection"));
            var properties=layout.AddPage(L.Text("operation.properties"));
            var security=layout.AddPage(L.Text("operation.verification"));
            var presets=layout.AddPage(L.Text("tools1.presets"));
            var destinationSection=OperationDialogLayout.Group(body,L.Text("copy.designDestination"));

            destination.Text = target.TrimEnd('\\') + "\\*.*"; foreach (string path in recent) destination.Items.Add(path);
            destination.Height = filter.Height = 32;
            destinationSection.Children.Add(destination);
            var pathActions = new WrapPanel { Margin = new Thickness(0,5,0,12) };
            pathActions.Children.Add(CopyUi.Button(L.Text("copy.designBrowse"), Browse));
            pathActions.Children.Add(CopyUi.Button(L.Text("copy.designNewFolder"), NewFolder)); destinationSection.Children.Add(pathActions);
            if (moving && single) destinationSection.Children.Add(new TextBlock { Text = L.Text("copy.destinationRenameHelp"), Foreground = FluentTheme.Muted, Margin = new Thickness(2,0,2,10), TextWrapping = TextWrapping.Wrap });
            var filterSection = OperationDialogLayout.Group(selection,L.Text("copy.designFilterOptions"));
            filterSection.Children.Add(new TextBlock { Text = L.Text("copy.fileMaskLabel"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(2,0,2,5) });
            foreach (string mask in new[] { "*.*", "*.txt;*.md", "*.jpg;*.jpeg;*.png;*.webp", "*.pdf;*.docx;*.xlsx", "*.mp4;*.mkv;*.avi", "*.* | *.tmp;*.bak" }) filter.Items.Add(mask);
            filter.ToolTip = L.Text("copy.maskSyntaxHelp"); filterSection.Children.Add(filter);
            var verification=OperationDialogLayout.Group(security,L.Text("operation.verification"));verification.Children.Add(verify);verification.Children.Add(permissions);verification.Children.Add(readOnly);
            var collisions=OperationDialogLayout.Group(body,L.Text("copy.existingDestinationLabel"));
            foreach (string text in new[] { L.Text("conflict.ask"), L.Text("conflict.overwriteAll"), L.Text("conflict.overwriteOlderTargets"), L.Text("conflict.skipAll"), L.Text("conflict.overwriteDifferent"), L.Text("conflict.overwriteIfSourceLarger"), L.Text("conflict.overwriteIfSourceSmaller"), L.Text("conflict.autoRenameCopies") }) rule.Items.Add(text);
            rule.SelectedIndex = 0; collisions.Children.Add(rule);
            var fileSelection=OperationDialogLayout.Group(selection,L.Text("operation.selection"));fileSelection.Children.Add(hidden);fileSelection.Children.Add(empty);
            var metadata=OperationDialogLayout.Group(properties,L.Text("operation.properties"));metadata.Children.Add(times);metadata.Children.Add(attributes);
            destinationSection.Children.Add(fileTarget);fileTarget.IsEnabled=single;
            AddPresets(presets);
            Loaded += delegate { destination.Focus(); var textBox = destination.Template.FindName("PART_EditableTextBox", destination) as TextBox; if (textBox != null) textBox.SelectAll(); };
            PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.F2) { Accept(true); e.Handled = true; }
                if (e.Key == System.Windows.Input.Key.F7) { layout.Navigation.SelectedIndex=0; NewFolder(); e.Handled = true; }
                if (e.Key == System.Windows.Input.Key.F8) { layout.Navigation.SelectedIndex=1; UpdateLayout(); filter.IsDropDownOpen = true; e.Handled = true; }
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
        internal readonly CheckBox ApplyToAll = new CheckBox { Content=L.Text("conflict.applyToAllInTask"),Margin=new Thickness(0,12,0,4) };
        public CopyConflictDialog(Window owner, CopyConflict conflict)
        {
            this.conflict = conflict; CopyUi.Style(this, owner, L.Text("conflict.fileExistsTitle"), 900);
            var layout=new OperationDialogLayout(this,L.Text("conflict.compareTitle"),Path.GetFileName(conflict.Target));var panel=layout.AddPage(L.Text("operation.basic"));var extras=layout.AddPage(L.Text("conflict.rulesTab"));
            if(AppearanceTheme.IsClean){Width=760;layout.Compact(230);}
            var thumbs=new List<Image>();var comparison=new Grid();comparison.ColumnDefinitions.Add(new ColumnDefinition());comparison.ColumnDefinitions.Add(new ColumnDefinition());
            var existing=Info(L.Text("conflict.existingVersion"),conflict.Target,conflict.TargetDirectory,conflict.TargetSize,conflict.TargetTime,thumbs);existing.Margin=new Thickness(0,0,6,0);comparison.Children.Add(existing);
            var incoming=Info(L.Text("conflict.incomingVersion"),conflict.Source,conflict.SourceDirectory,conflict.SourceSize,conflict.SourceTime,thumbs);incoming.Margin=new Thickness(6,0,0,0);Grid.SetColumn(incoming,1);comparison.Children.Add(incoming);panel.Children.Add(comparison);
            bool compatible=!conflict.SourceDirectory&&!conflict.TargetDirectory;
            string note=!compatible?L.Text("conflict.fileFolderHelp"):conflict.SourceTime>conflict.TargetTime?L.Text("conflict.incomingNewer"):conflict.SourceTime<conflict.TargetTime?L.Text("conflict.incomingOlder"):conflict.SourceSize==conflict.TargetSize?L.Text("conflict.sameMetadata"):L.Text("conflict.sameTimeDifferentSize");
            panel.Children.Add(new TextBlock{Text=note,TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,12,0,0)});
            panel.Children.Add(ApplyToAll);if(conflict.IsReadOnly)panel.Children.Add(readOnly);
            var extraActions=new WrapPanel{Margin=new Thickness(0,8,0,0)};(AppearanceTheme.IsClean?extras:panel).Children.Add(extraActions);
            extraActions.Children.Add(CopyUi.Button(L.Text("action.rename"),delegate{
                string name=CopyUi.Prompt(this,L.Text("conflict.newCopyNameTitle"),Path.GetFileName(conflict.Target));if(name==null)return;
                try{FileOperations.ValidateName(name);if(String.Equals(name,Path.GetFileName(conflict.Target),StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("conflict.differentNameRequired"));Decision=new CopyDecision(CopyChoice.Rename,name){AllowReadOnly=readOnly.IsChecked==true};DialogResult=true;}catch(Exception ex){MessageBox.Show(this,ex.Message);}
            }));
            var append=CopyUi.Button(L.Text("action.append"),delegate{if(MessageBox.Show(this,L.Text("conflict.confirmAppendBytes"),L.Text("conflict.appendTitle"),MessageBoxButton.OKCancel,MessageBoxImage.Question)==MessageBoxResult.OK)Choose(CopyChoice.Append);});append.IsEnabled=compatible;extraActions.Children.Add(append);
            var previews=new CheckBox{Content=L.Text("action.preview"),Margin=new Thickness(0,8,0,8)};previews.Checked+=delegate{foreach(Image image in thumbs){image.Source=Thumbnail((string)image.Tag);image.Visibility=image.Source==null?Visibility.Collapsed:Visibility.Visible;}};previews.Unchecked+=delegate{foreach(Image image in thumbs)image.Visibility=Visibility.Collapsed;};(AppearanceTheme.IsClean?extras:panel).Children.Add(previews);
            extras.Children.Add(new TextBlock{Text=L.Text("conflict.applyRulesHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,8,0,6)});
            var rules=new WrapPanel();extras.Children.Add(rules);
            foreach(var pair in new[]{Tuple.Create(L.Text("conflict.overwriteAll"),CopyChoice.OverwriteAll),Tuple.Create(L.Text("conflict.skipAll"),CopyChoice.SkipAll),Tuple.Create(L.Text("conflict.overwriteOlder"),CopyChoice.Older),Tuple.Create(L.Text("conflict.overwriteDifferentSizeOrTime"),CopyChoice.Different),Tuple.Create(L.Text("conflict.overwriteIfSourceLarger"),CopyChoice.Larger),Tuple.Create(L.Text("conflict.overwriteIfSourceSmaller"),CopyChoice.Smaller),Tuple.Create(L.Text("conflict.autoRenameAll"),CopyChoice.RenameAll)})Add(rules,pair.Item1,pair.Item2,compatible||pair.Item2==CopyChoice.SkipAll||pair.Item2==CopyChoice.RenameAll);
            var cancel=CopyUi.Button(L.Text("conflict.stopTask"),delegate{Choose(CopyChoice.Cancel);});cancel.IsCancel=true;layout.Actions.Children.Add(cancel);
            var skip=CopyUi.Button(L.Text("action.skip"),delegate{Choose(ApplyToAll.IsChecked==true?CopyChoice.SkipAll:CopyChoice.Skip);});layout.Actions.Children.Add(skip);
            var overwrite=CopyUi.Button(L.Text("action.overwrite"),delegate{Choose(ApplyToAll.IsChecked==true?CopyChoice.OverwriteAll:CopyChoice.Overwrite);});overwrite.IsEnabled=compatible;FluentTheme.Primary(overwrite);layout.Actions.Children.Add(overwrite);
            ApplyToAll.Checked+=delegate{skip.Content=L.Text("conflict.skipAll");overwrite.Content=L.Text("conflict.overwriteAll");};ApplyToAll.Unchecked+=delegate{skip.Content=L.Text("action.skip");overwrite.Content=L.Text("action.overwrite");};
        }
        void Choose(CopyChoice choice){Decision=new CopyDecision(choice){AllowReadOnly=readOnly.IsChecked==true};DialogResult=true;}
        void Add(Panel panel,string text,CopyChoice choice,bool enabled){var button=CopyUi.Button(text,delegate{Choose(choice);});button.IsEnabled=enabled;panel.Children.Add(button);}
        GroupBox Info(string title,string path,bool directory,long bytes,DateTime time,List<Image> thumbs){
            var group=new GroupBox{Header=title,Padding=new Thickness(12)};
            var inner=new DockPanel();var image=new Image{Tag=path,Width=64,Height=56,Margin=new Thickness(6),Visibility=Visibility.Collapsed};thumbs.Add(image);DockPanel.SetDock(image,Dock.Right);inner.Children.Add(image);
            var text=new StackPanel();text.Children.Add(new TextBlock{Text=Path.GetFileName(path),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)});
            text.Children.Add(new TextBlock{Text=directory?L.Text("common.folder"):CopyUi.Bytes(bytes),ToolTip=bytes.ToString("N0")+L.Text("common.bytesSuffix"),Margin=new Thickness(0,0,0,3)});
            text.Children.Add(new TextBlock{Text=time.ToString("dd.MM.yyyy HH:mm:ss"),Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,9)});
            text.Children.Add(new PathTextBlock{FullPath=path,Foreground=FluentTheme.Muted});inner.Children.Add(text);group.Content=inner;return group;
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
        readonly TextBlock phase, count, totals;
        readonly PathTextBlock source = new PathTextBlock(), target = new PathTextBlock();
        readonly TextBlock filePercent, totalPercent, speed = Line(L.Text("transfer.unknownRate"));
        readonly LocalCopyProgressBar fileProgress, totalProgress;
        readonly TransferProgressLayout presentation;
        readonly System.Windows.Threading.DispatcherTimer metricsTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        CopyUpdate latest;
        internal string FileCountSummary { get { return count.Text+L.Text("copy.filesAndBytesSeparator"); } }
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
            speed.Text = (metrics.BytesPerSecond / 1048576.0).ToString("F1") + " MiB/s";
            if (MetricsChanged != null) MetricsChanged();
        }
        public CopyProgressDialog(Window owner, CopyControl control)
        {
            this.control = control; CopyUi.Style(this, owner, L.Text("copy.progressWindowTitle"), 580);
            ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height;
            placement = new TransferWindowState(this,delegate { if (BecameBackground != null) BecameBackground(); },delegate { if (BecameForeground != null) BecameForeground(); });
            presentation = new TransferProgressLayout(this,L.Text("copy.progressWindowTitle"));
            phase=presentation.Remaining;count=presentation.Count;totals=presentation.Totals;
            filePercent=presentation.FilePercent;totalPercent=presentation.Percent;fileProgress=presentation.File;totalProgress=presentation.Overall;
            source.Margin = target.Margin = new Thickness(0,3,0,3);presentation.DetailBody.Children.Add(source);presentation.DetailBody.Children.Add(target);
            speed.Margin=new Thickness(0,10,0,6);presentation.DetailBody.Children.Add(speed);
            presentation.DetailBody.Children.Add(new TextBlock{Text=L.Text("transfer.speedHistory"),Foreground=FluentTheme.Muted,Margin=new Thickness(0,4,0,4)});presentation.DetailBody.Children.Add(speedTrend);
            var buttons=presentation.Actions;
            pauseButton=CopyUi.Button(L.Text("action.pause"),delegate{TogglePause();});buttons.Children.Add(pauseButton);
            buttons.Children.Add(CopyUi.Button(L.Text("action.cancel"),delegate{control.Cancel();UpdateMetrics();}));
            var background=CopyUi.Button(L.Text("copy.backgroundAction"),placement.Background);FluentTheme.Primary(background);buttons.Children.Add(background);
            metricsTimer.Tick += delegate { UpdateMetrics(); }; Loaded += delegate { if (!complete) metricsTimer.Start(); }; Closed += delegate { metricsTimer.Stop(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!complete) { e.Cancel = true; control.Cancel(); UpdateMetrics(); } };
        }
        public void Update(CopyUpdate update)
        {
            latest = update; lastUpdate = DateTime.UtcNow; UpdateMetrics();
            if(!String.IsNullOrWhiteSpace(update.Phase))presentation.Heading.Text=update.Phase;
            source.SetPath(L.Text("transfer.sourceLabel"),update.Source);target.SetPath(L.Text("transfer.targetLabel"),update.Target);
            if(!String.IsNullOrEmpty(update.Source)&&!String.IsNullOrEmpty(update.Target)){
                presentation.Route.SetPath("",TransferProgressLayout.ParentPath(update.Source)+" → "+TransferProgressLayout.ParentPath(update.Target));presentation.Route.Visibility=Visibility.Visible;
            }
            double file = update.FileTotal == 0 ? 0 : (double)update.FileDone / update.FileTotal;
            double total = update.TotalBytes == 0 ? (update.TotalFiles == 0 ? 0 : (double)update.Completed / update.TotalFiles) : (double)update.TotalDone / update.TotalBytes;
            fileProgress.Update(file); totalProgress.Update(total); filePercent.Text = (Math.Max(0,Math.Min(1,file)) * 100).ToString("F0") + " %"; totalPercent.Text = (Math.Max(0,Math.Min(1,total)) * 100).ToString("F0") + " %";
            count.Text = update.Completed + " / " + update.TotalFiles;
            totals.Text = CopyUi.Bytes(update.TotalDone) + " / " + CopyUi.Bytes(update.TotalBytes);
            presentation.Current(update.Source,update.FileTotal>0?(double?)file:null);
        }
        public void Finish() { complete = true; metricsTimer.Stop(); placement.Finish(); }
    }
}
