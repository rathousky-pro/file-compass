using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FileCompass
{
    public partial class Entry : System.ComponentModel.INotifyPropertyChanged
    {
        bool marked;
        public bool IsMarked
        {
            get { return marked; }
            set { if (marked == value || IsParent) return; marked = value; if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("IsMarked")); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public ImageSource Icon { get; set; }
        ImageSource thumbnail;
        public ImageSource Thumbnail { get { return thumbnail ?? Icon; } set { if (Object.ReferenceEquals(thumbnail,value)) return; thumbnail = value; if (PropertyChanged != null) PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("Thumbnail")); } }
        string mediaInfo;
        public string MediaInfo { get { return mediaInfo ?? ""; } set { mediaInfo = value; if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("MediaInfo")); } }
        public bool IsLink { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSystem { get; set; }
        string[] tagNames = new string[0];
        public string[] TagNames { get { return tagNames; } set { tagNames = value ?? new string[0]; if (PropertyChanged != null) { PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("TagsLabel")); PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("NameToolTip")); } } }
        public string TagsLabel { get { return String.Join(" · ", tagNames); } }
        public string Name { get; set; }
        public string Extension { get; set; }
        bool folderBrackets, nameTooltips = true;
        public string FullName { get { return FilePanel.EntryName(this); } }
        public string LocationName { get; set; }
        public string ListName { get { if (LocationName != null) return LocationName; return IsDirectory && !IsParent ? folderBrackets ? "[" + FullName + "]" : FullName : Name; } }
        public string NameToolTip { get { return nameTooltips && !IsParent ? (LocationName == null ? FullName : FullPath) + (TagNames.Length == 0 ? "" : "\n" + TagsLabel) : null; } }
        internal void ApplyNameSettings(bool brackets, bool tooltips) { folderBrackets = brackets; nameTooltips = tooltips; if (PropertyChanged != null) { PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("ListName")); PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("NameToolTip")); } }
        internal string NetworkDeviceTarget;
        internal NetworkProfile NetworkServiceProfile;
        internal bool NetworkServiceSaved;
        public string Attributes { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsParent { get; set; }
        public long Bytes { get; set; }
        internal bool SizeKnown, SizeLoading;
        public string Size { get { return IsParent ? "" : IsLink ? "<LNK>" : SizeLoading ? L.Text("common.ellipsis") : IsDirectory && !SizeKnown ? "<DIR>" : ByteSize.Format(Bytes); } }
        internal void FolderSize(long? bytes, bool loading)
        {
            if (loading) { SizeKnown = false; Bytes = 0; }
            SizeLoading = loading; if (bytes.HasValue) { Bytes = bytes.Value; SizeKnown = true; }
            if (PropertyChanged != null) { PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("Size")); PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("Bytes")); }
        }
        public string Modified { get; set; }
    }

    public partial class MainWindow : Window
    {
        FilePanel left, right, active;
        readonly TextBlock operation = new TextBlock { Text = L.Text("state.ready"), Margin = new Thickness(4, 2, 4, 2), Foreground = FluentTheme.Muted, Visibility = Visibility.Collapsed };
        readonly ProgressBar progress = new ProgressBar { Height = 5, Visibility = Visibility.Collapsed };
        readonly List<Button> actions = new List<Button>();
        bool busy;
        Grid panelSplit;
        GridSplitter panelSplitter;
        internal Task StartupRestoration;
        WindowState lastVisibleWindowState = WindowState.Normal;
        public MainWindow(string initialDirectory = null, ProgramSettings preferences = null)
        {
            FluentTheme.Ensure(); InitializeOperations(initialDirectory);
            StateChanged += delegate { if (WindowState != WindowState.Minimized) lastVisibleWindowState = WindowState; };
            Title = ApplicationBrand.Name + " · Fluent Preview"; Width = 1440; Height = 800; MinWidth = 850; MinHeight = 450;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Background = CompactTheme.Chrome; Foreground = FluentTheme.Text;
            SourceInitialized += delegate { AppearanceTheme.TitleBar(this); }; AppearanceTheme.RegisterShell(this);
            ApplicationBrand.Attach(this); UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            var root = new DockPanel { Background = CompactTheme.Chrome, LastChildFill = true }; Content = root;
            var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            bottom.Children.Add(progress); bottom.Children.Add(operation);
            AddChrome(root, bottom);
            operation.Cursor = Cursors.Hand; operation.MouseLeftButtonUp += delegate { ShowCopyProgress(); };
            var split = panelSplit = new Grid { Margin = new Thickness(8, 6, 8, 7) };
            split.ColumnDefinitions.Add(new ColumnDefinition()); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) }); split.ColumnDefinitions.Add(new ColumnDefinition());
            left = new FilePanel(SetActive); right = new FilePanel(SetActive);
            left.DeferAutomaticRefresh = right.DeferAutomaticRefresh = delegate { return copyProgress != null && copyProgress.IsVisible; };
            left.PathChanged += delegate { UpdatePrompt(); }; right.PathChanged += delegate { UpdatePrompt(); };
            left.LoadingChanged += delegate { UpdateNetworkActions(); }; right.LoadingChanged += delegate { UpdateNetworkActions(); };
            left.DisconnectLocalNetwork += DisconnectLocalNetworkPanel; right.DisconnectLocalNetwork += DisconnectLocalNetworkPanel;
            left.DisplayModeChanged += UpdateViewToolbarControls; right.DisplayModeChanged += UpdateViewToolbarControls;
            left.DriveFolderResolver = delegate(string disk) { return FolderOnDisk(right, disk); };
            right.DriveFolderResolver = delegate(string disk) { return FolderOnDisk(left, disk); };
            left.ArchiveFileRequested += OpenArchiveFile; right.ArchiveFileRequested += OpenArchiveFile;
            split.Children.Add(left); Grid.SetColumn(right, 2); split.Children.Add(right);
            var splitter = panelSplitter = new GridSplitter { Width = 6, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(splitter, 1); split.Children.Add(splitter);
            InstallFluentWorkspace(root,split);
            string home = initialDirectory ?? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
            preferences = preferences ?? ProgramSettings.Load();
            if (initialDirectory == null)
            {
                Width = ValidDimension(preferences.WindowWidth, MinWidth, 10000, 1440); Height = ValidDimension(preferences.WindowHeight, MinHeight, 10000, 800);
                var screen = SystemParameters.VirtualScreenWidth;
                if (!Double.IsNaN(preferences.WindowLeft) && !Double.IsNaN(preferences.WindowTop) && preferences.WindowLeft + Width > SystemParameters.VirtualScreenLeft && preferences.WindowLeft < SystemParameters.VirtualScreenLeft + screen && preferences.WindowTop + Height > SystemParameters.VirtualScreenTop && preferences.WindowTop < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
                { Left = preferences.WindowLeft; Top = preferences.WindowTop; WindowStartupLocation = WindowStartupLocation.Manual; }
                if (preferences.Maximized) WindowState = WindowState.Maximized;
                double ratio = ValidDimension(preferences.SplitRatio, .1, .9, .5); split.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star); split.ColumnDefinitions[2].Width = new GridLength(1 - ratio, GridUnitType.Star);
                left.RestorePanelView(preferences.LeftView); right.RestorePanelView(preferences.RightView);
            }
            InitializeColumnWidths(initialDirectory == null ? preferences : null);
            left.Navigate(initialDirectory != null ? home : ProgramSettings.StartupFolder(preferences.LeftFolder, home), false);
            right.Navigate(initialDirectory != null ? home : ProgramSettings.StartupFolder(preferences.RightFolder, home), false); SetActive(left);
            if (initialDirectory == null) { left.RestoreTabs(preferences.LeftTabs, preferences.LeftTabIndex, !preferences.SaveFoldersOnExit || preferences.SaveTabsOnExit); right.RestoreTabs(preferences.RightTabs, preferences.RightTabIndex, !preferences.SaveFoldersOnExit || preferences.SaveTabsOnExit); }
            if(initialDirectory==null){if(preferences.LeftPanelEmpty)left.ClearEmptyPanel(false);if(preferences.RightPanelEmpty)right.ClearEmptyPanel(false);}
            InitializeWorkflows(preferences);
            if(initialDirectory==null){left.TabsChanged+=PersistPanelTabs;right.TabsChanged+=PersistPanelTabs;}
            InitializeReleaseTools();
            InitializeQuickPreview();
            InitializePanelLayout(initialDirectory == null ? preferences : new ProgramSettings());
            if (initialDirectory == null) StartupRestoration = RestoreStartupFallback(home);
            InitializeStartupKeyboardFocus();
            if (initialDirectory == null) Loaded += delegate { if (durableQueue.Pending().Length > 0 || durableQueue.LastError != null) ShowDurableQueue(); };
            if (initialDirectory == null) Loaded += async delegate { await StartupRestoration; await RestoreSshfsConnections(preferences); };
            PreviewKeyDown += HandleKeys;
            Closed += delegate { if(updateManager!=null)updateManager.Dispose(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (fileToolsRunning > 0 || archiveOperations > 0 || deletions > 0 || busy || PanelsLoading || copyRunning || networkCopies > 0 || networkJobs.Any(x => x.Planning > 0) || copyJobs.Count > 0) { e.Cancel = true; MessageBox.Show(L.Text("app.fileOperationBusy"), ApplicationBrand.Name); }
                if (!e.Cancel && !ConfirmArchiveEditors()) e.Cancel = true;
                if (!e.Cancel && !operationJournal.PendingWrites.IsCompleted) { e.Cancel = true; FlushOperationsBeforeClose(); }
                if (!e.Cancel && initialDirectory == null) { try { SaveExitPreferences(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.saveViewFailed"), MessageBoxButton.OK, MessageBoxImage.Warning); } }
            };
            Closed += delegate { CloudRuntime.StopAll(); durableQueue.Dispose(); windowClosed = true; left.CancelNavigation(); right.CancelNavigation(); left.DisposeArchives(); right.DisposeArchives(); left.DisposeConnections(); right.DisposeConnections(); SshfsMount.CloseTemporaryMounts(); foreach (var job in networkJobs) { job.Changed = null; job.Cancellation.Dispose(); } };
        }
        void SetActive(FilePanel panel) { if (panel.Visibility == Visibility.Collapsed || (quickPreview != null && panel == previewTarget)) return; active = panel; if (left != null) left.Highlight(panel == left); if (right != null) right.Highlight(panel == right); UpdatePrompt(); UpdateViewToolbarControls(); SyncFluentSearch(); }
        void AddAction(Panel panel, string label, Action action,string icon)
        {
            var button = CompactTheme.Button(label, label, delegate { if (!busy && !PanelsLoading) action(); });
            button.ContentTemplate=FunctionKeyIcons.Caption(icon);
            button.Tag="function-key";
            button.Height = 34; button.FontSize=14;button.Padding = new Thickness(16, 8, 16, 8);
            button.Background = CompactTheme.WhiteSurface; button.BorderBrush = CompactTheme.Line;
            panel.Children.Add(button); actions.Add(button);
        }
        void HandleKeys(object sender, KeyEventArgs e)
        {
            if(active!=null&&active.List.IsKeyboardFocusWithin&&active.HandleQuickSearchKey(e))return;
            if (HandleReleaseKeys(e)) return;
            if (e.Key == Key.Q && Keyboard.Modifiers == ModifierKeys.Control) { ToggleQuickPreview(); e.Handled = true; return; }
            if(e.Key==Key.Escape&&active!=null&&!String.IsNullOrEmpty(active.QuickFilterText)&&(active.List.IsKeyboardFocusWithin||fluentSearchInput!=null&&fluentSearchInput.IsKeyboardFocusWithin)){active.SetQuickFilter("");active.FocusFiles();e.Handled=true;return;}
            if (e.Key == Key.Escape && FilePreviewOpen) { ToggleQuickPreview(); e.Handled = true; return; }
            if (e.Key == Key.Escape && PanelsLoading) { active.CancelNavigation(); e.Handled = true; return; }
            if (e.Key == Key.Q && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && !busy && !PanelsLoading) { OpenRichPreview(); e.Handled = true; return; }

            if (quickPreview != null && quickPreview.ContainsKeyboardFocus) return;
            if (HandleWorkflowKeys(e)) return;
            if(e.Key==Key.R&&Keyboard.Modifiers==ModifierKeys.Control&&!busy){RefreshPanels();e.Handled=true;return;}
            if (busy || PanelsLoading || e.OriginalSource is TextBox) return;
            if(e.Key==Key.N&&Keyboard.Modifiers==ModifierKeys.Control){NewTextFile();e.Handled=true;return;}
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Enter && Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift)) { active.ToggleDiskUsage(); e.Handled = true; return; }
            if (key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)) { OpenPowerShell(); e.Handled = true; return; }
            if (key == Key.F7 && Keyboard.Modifiers == ModifierKeys.Alt) { FindFiles(); e.Handled = true; return; }
            if (key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) { MultiRename(); e.Handled = true; return; }
            if (key == Key.F5 && Keyboard.Modifiers == ModifierKeys.Alt) { CreateArchive(); e.Handled = true; return; }
            if (key == Key.F9 && Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift)) { TestArchives(); e.Handled = true; return; }
            if (key == Key.F9 && Keyboard.Modifiers == ModifierKeys.Alt) { ExtractArchives(); e.Handled = true; return; }
            if (e.Key == Key.F5) { Transfer(false); e.Handled = true; }
            else if (e.Key == Key.F6) { if (Keyboard.Modifiers == ModifierKeys.Shift) Rename(); else Transfer(true); e.Handled = true; }
            else if (e.Key == Key.F7) { NewFolder(); e.Handled = true; }
            else if ((e.Key == Key.F8 || (e.Key == Key.Delete && active.List.IsKeyboardFocusWithin)) && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)) { DeleteSelected(); e.Handled = true; }
            else if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None) { NewTextFile(); e.Handled = true; }
            else if (e.Key == Key.F3) { active.ViewSelected(); e.Handled = true; }
            else if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Shift) { HandleExternalEditorShortcut(e.Key,Keyboard.Modifiers); e.Handled = true; }
            else if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.None) { EditSelected(); e.Handled = true; }
            else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None && panelLayout != "Single") { (active == left ? right : left).List.Focus(); e.Handled = true; }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { left.Refresh(); right.Refresh(); e.Handled = true; }
        }
        void Transfer(bool move)
        {
            if(active.EmptyPanel||(active==left?right:left).EmptyPanel){MessageBox.Show(this,L.Text("tabs.emptyTransfer"),ApplicationBrand.Name);return;}
            if(active.IsExt){ExportExtToOther(move);return;}if((active==left?right:left).IsExt){MessageBox.Show(this,L.Text("ext.readOnly"),L.Text("ext.title"));return;}
            if (active.IsShellBrowser || (active == left ? right : left).IsShellBrowser) { MessageBox.Show(this, L.Text("tree.openFolderFirst"), ApplicationBrand.Name); return; }
            if ((active == left ? right : left).IsProjection) { MessageBox.Show(this, L.Text("projection.destinationRejected"), ApplicationBrand.Name); return; }
            if (active.IsArchive || (active == left ? right : left).IsArchive) { if (move) MessageBox.Show(this, L.Text("archive.navigationShortcuts"), L.Text("archive.shortTitle")); else ArchiveTransfer(); return; }
            if (active.IsRemote || (active == left ? right : left).IsRemote) { NetworkTransfer(move); return; }
            StartCopy(move);
        }
        internal static string FolderOnDisk(FilePanel other, string disk)
        {
            return !other.IsRemote && !other.IsArchive && other.CurrentPath != null && String.Equals(Path.GetPathRoot(other.CurrentPath), disk, StringComparison.OrdinalIgnoreCase) ? other.CurrentPath : disk;
        }

        async void NewFolder()
        {
            if(RejectExtWrite())return;
            if (active.IsShellBrowser) { MessageBox.Show(this, L.Text("tree.openFolderFirst"), ApplicationBrand.Name); return; }
            if (active.IsProjection) { MessageBox.Show(this, L.Text("projection.destinationRejected"), ApplicationBrand.Name); return; }
            if (active.IsArchive) { ArchiveNewFolder(); return; }
            if (active.IsRemote) { RemoteNewFolder(); return; }
            if (active.CurrentPath == null) return;
            string name = Ask(L.Text("action.newFolder"), L.Text("folder.nameLabel"), ""); if (name == null) return;
            var targetPanel=active; string parent=targetPanel.CurrentPath;
            try { FileOperations.ValidateName(name); string path=Path.Combine(parent,name);var create=Task.Factory.StartNew(delegate{if(File.Exists(path)||Directory.Exists(path))throw new IOException(L.Text("error.itemAlreadyExists"));Directory.CreateDirectory(path);return UndoReceipt.Capture(path,null,false);},System.Threading.CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                if(await Task.WhenAny(create,Task.Delay(TimeSpan.FromSeconds(15)))!=create){var observe=create.ContinueWith(t=>{var error=t.Exception;},TaskScheduler.Default);MessageBox.Show(this,L.Text("folder.networkPending"),L.Text("folder.createFailed"));return;}
                var receipt=await create;RecordUndoTransaction(L.Text("action.newFolder"),new[]{receipt});if(targetPanel.CurrentPath==parent)targetPanel.Refresh(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, L.Text("folder.createFailed")); }
        }
        async void Rename()
        {
            if(RejectExtWrite())return;
            if (active.IsArchive) { ArchiveRename(); return; }
            if (active.IsRemote) { RemoteRename(); return; }
            var entries = active.Selected(); if (entries.Count != 1) { MessageBox.Show(L.Text("selection.exactlyOneRequired"), L.Text("action.rename")); return; }
            var sourcePanel = active;
            string name = Ask(L.Text("action.rename"), L.Text("rename.newNameLabel"), Path.GetFileName(entries[0].FullPath), !entries[0].IsDirectory); sourcePanel.FocusFiles(); if (name == null) return;
            try
            {
                string parent = Path.GetDirectoryName(entries[0].FullPath);
                var undo=UndoReceipt.Capture(entries[0].FullPath,entries[0].FullPath,true);
                FileOperations.Rename(entries[0].FullPath, name); undo=UndoReceipt.Committed(undo,Path.Combine(parent,name));RecordUndoTransaction(L.Text("action.rename"),new[]{undo}); fileToolsRunning++;try{await Task.Run(delegate { FileAssociations.Moved(entries[0].FullPath, Path.Combine(parent, name)); });}finally{fileToolsRunning--;} left.Refresh(); right.Refresh();
                await sourcePanel.NavigationTask;
                if (active == sourcePanel && String.Equals(sourcePanel.CurrentPath, parent, StringComparison.OrdinalIgnoreCase))
                { var renamed = sourcePanel.List.Items.Cast<Entry>().FirstOrDefault(x => String.Equals(x.FullPath, Path.Combine(parent, name), StringComparison.OrdinalIgnoreCase)); if (renamed != null) { sourcePanel.List.SelectedItem = renamed; sourcePanel.List.ScrollIntoView(renamed); } }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, L.Text("rename.failed")); }
        }
        string Ask(string title, string label, string initial, bool renameFile = false)
        {
            var dialog = new Window { Title = title, Width = 440, Height = 180, ResizeMode = ResizeMode.NoResize, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            FluentTheme.Dialog(dialog);
            TextOptions.SetTextFormattingMode(dialog, TextFormattingMode.Display);
            var stack = new StackPanel { Margin = new Thickness(16) }; dialog.Content = stack;
            stack.Children.Add(new TextBlock { Text = label });
            var input = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 12) }; stack.Children.Add(input);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; stack.Children.Add(buttons);
            var ok = CopyUi.Button("OK", delegate { dialog.DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok);
            var close = CopyUi.Button(L.Text("action.cancel"), delegate { dialog.DialogResult = false; }); close.IsCancel = true; buttons.Children.Add(close);
            dialog.Loaded += delegate { input.Focus(); input.Select(0, renameFile && ProgramSettings.Load().RenameWithoutExtension ? FileBehavior.NameSelectionLength(initial) : initial.Length); };
            return dialog.ShowDialog() == true ? input.Text : null;
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try{using(RuntimeIntegrity.Acquire())return RunVerified(args);}
            catch(Exception ex){if(args.Any(x=>x.StartsWith("--",StringComparison.Ordinal)))Console.Error.WriteLine(ex.Message);else MessageBox.Show(ex.Message,ApplicationBrand.Name,MessageBoxButton.OK,MessageBoxImage.Error);return 2;}
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int RunVerified(string[] args)
        {
#if APP_TESTS
            if(args.Length==3&&args[0]=="--application-package-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[2]),"profile");return ApplicationUpdatesTests.VerifyPackage(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));}
            if(args.Length==2&&args[0]=="--application-updates-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ApplicationUpdatesTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--profile-protocol-ux-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ProfileProtocolUxTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--fluent-scene-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return FluentSceneTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--rename-fluent-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return RenameFluentTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--task-dock-grouping-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return TaskDockGroupingTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--tool-dialogs-polish-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ToolDialogsPolishTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--unified-search-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return UnifiedSearchTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--workspace-polish-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return WorkspacePolishTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--cascade-navigation-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return CascadeNavigationTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--column-fill-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ColumnFillTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--program-ux-audit-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ProgramUxAuditTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--settings-revision-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return SettingsRevisionTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--compact-ux-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return CompactUxTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--fluent-preview-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return FluentPreviewTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--appearance-language-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return AppearanceLanguageTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--field-border-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return FieldBorderTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--web-gallery-video-screenshots-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"isolated-video-profile");return WebGalleryScreenshots.RunVideo(args[1]);}
            if(args.Length==2&&args[0]=="--web-gallery-screenshots-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"isolated-profile");return WebGalleryScreenshots.Run(args[1]);}
            if(args.Length==2&&args[0]=="--panel-strip-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return PanelStripTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--glass-dialog-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return GlassDialogTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--dialog-cards-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return DialogCardsTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--preview-card-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return PreviewCardTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--selection-brackets-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return SelectionBracketTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--media-keep-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return MediaKeepTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--panel-experience-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return PanelExperienceTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--toolbar-symbols-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ToolbarSymbolsTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--metadata-columns-test")AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");
            if(args.Length==2&&args[0]=="--smart-release-live-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return SmartReleaseTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--archive-sfx-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return ArchiveSfxTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--pause-fixture"){for(int i=0;i<1500;i++){File.WriteAllText(args[1],i.ToString());System.Threading.Thread.Sleep(20);}return 0;}
            if(args.Length==2&&args[0]=="--clean-refinements-test"){AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");return CleanRefinementTests.Run(args[1]);}
            if(args.Length==2&&args[0]=="--archive-worker"&&!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("FILECOMPASS_TEST_PROFILE")))AppPaths.TestProfileDirectory=Path.GetFullPath(Environment.GetEnvironmentVariable("FILECOMPASS_TEST_PROFILE"));
            if(args.Length==2&&(args[0]=="--media-progress-eta-test"||args[0]=="--openmp-media-test"||args[0]=="--media-context-test"||args[0]=="--new-text-editor-test"||args[0]=="--copy-error-flow-test"||args[0]=="--analyzer-start-smart-kind-test"||args[0]=="--archive-test"||args[0]=="--archive-upgrade-test"||args[0]=="--archive-interaction-test"||args[0]=="--archive-formats-test"||args[0]=="--compression-progress-test"||args[0]=="--archive-memory-test"||args[0]=="--panel-layout-test"||args[0]=="--preview-refresh-test"||args[0]=="--prepared-preview-test"||args[0]=="--video-filmstrip-test"||args[0]=="--vendor-updates-test"||args[0]=="--vendor-updates-live-test"||args[0]=="--task-dock-test"||args[0]=="--preview-selection-test"||args[0]=="--search-media-ux-test"||args[0]=="--disk-tools-ux-test"||args[0]=="--settings-surfaces-test"||args[0]=="--settings-layout-test"||args[0]=="--settings-connections-test"||args[0]=="--cloud-question-language-test"||args[0]=="--inline-tabs-test"||args[0]=="--pinned-tabs-test"||args[0]=="--network-progress-ux-test"||args[0]=="--conflict-ux-test"||args[0]=="--operation-dialog-test"||args[0]=="--transfer-progress-layout-test"||args[0]=="--interface-theme-test"||args[0]=="--dispatch-units-test"||args[0]=="--editor-tempo-test"||args[0]=="--editor-analyzer-test"||args[0]=="--editor-analyzer-screens-test"||args[0]=="--live-language-test"||args[0]=="--help-window-test"||args[0]=="--disk-smart-test"||args[0]=="--disk-analyzer-test"||args[0]=="--disk-analyzer-upgrade-test"||args[0]=="--local-copy-ui-test"||args[0]=="--navigation-focus-test"||args[0]=="--icloud-test"||args[0]=="--release-tools-test"||args[0]=="--media-names-test"||args[0]=="--undo-history-test"||args[0]=="--image-batch-test"||args[0]=="--media-converter-test"||args[0]=="--csv-preview-test"||args[0]=="--media-layout-test"||args[0]=="--help-screenshots-test"||args[0]=="--internal-preview-test"||args[0]=="--file-context-test"||args[0]=="--usb-drive-test"||args[0]=="--ext-reader-test"))AppPaths.TestProfileDirectory=Path.Combine(Path.GetFullPath(args[1]),"profile");
#endif
#if !APP_TESTS
            if(args.Any(x=>x.StartsWith("--",StringComparison.Ordinal)&&x.EndsWith("-test",StringComparison.Ordinal))) { Console.Error.WriteLine("Test commands are only available in development builds."); return 2; }
#endif
            if(args.Length==2&&args[0]=="--cloud-config-key")return CloudStore.PrintKey(args[1]); if (args.Length == 0 || !args[0].StartsWith("--", StringComparison.Ordinal)) StartupAcceleration.Begin();
            try { L.Initialize(); }
            catch (Exception ex) {
                string error = "Cannot load the language pack. Keep languages beside File Compass.exe.\n\n" + ex.Message;
                if (args.Any(x => x.EndsWith("-test", StringComparison.Ordinal)) || args.Contains("--archive-worker") || args.Contains("--everything-worker")) Console.Error.WriteLine(error);
                else MessageBox.Show(error, ApplicationBrand.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return 2;
            }
            if(args.Length==1&&(args[0]=="--ext-window"||args[0]=="--ext-window-left"||args[0]=="--ext-window-right")){var extApp=new Application();FluentTheme.Ensure();HelpService.Ensure();return extApp.Run(new ExtBrowserWindow(null));}
            if(args.Length==3&&args[0]=="--shell-menu-worker"){int result=ShellMenuWorker.Serve(args[1],args[2]);Environment.Exit(result);return result;}
            if(args.Length==3&&args[0]=="--ext-worker")return ExtReader.Worker(args[1],args[2]);
            if (args.Length == 1 && args[0] == "--smart-window") { var smartApp = new Application(); FluentTheme.Ensure(); return smartApp.Run(new DiskSmartWindow(null)); }
            if (args.Length > 0 && args[0] == "--sshfs-install-helper") return SshfsSetup.Helper(args);
            if (args.Length == 1 && args[0] == "--everything-worker") return EverythingSearch.Worker();
            if (args.Contains("--initialize-portable")) { Directory.CreateDirectory(AppPaths.ConfigDirectory); return 0; }
            if (args.Length == 3 && args[0] == "--content-search-worker") return ContentSearch.Worker(args[1],args[2]);
            if (args.Length == 3 && args[0] == "--media-image-worker") return MediaImages.Worker(args[1],args[2]);
            if (args.Length == 3 && args[0] == "--metadata-worker") return FileMetadataService.Worker(args[1],args[2]);
            if (args.Length == 2 && args[0] == "--archive-worker")
            { try { return ArchiveWorker.Serve(args[1]); } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
#if APP_TESTS
            if (args.Length == 2 && args[0] == "--conflict-ux-test") return ConflictUxTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--media-progress-eta-test")return MediaProgressEtaTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--openmp-media-test")return OpenMpMediaTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--media-context-test")return MediaContextTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--new-text-editor-test")return NewTextEditorTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--copy-error-flow-test")return CopyErrorFlowTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--analyzer-start-smart-kind-test")return AnalyzerStartSmartKindTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--archive-interaction-test")return ArchiveInteractionTests.Run(args[1]);
            if(args.Length==2&&args[0]=="--video-filmstrip-test")return VideoPreviewTests.Run(args[1]);
            if(args.Length==2&&(args[0]=="--vendor-updates-test"||args[0]=="--vendor-updates-live-test"))return VendorUpdatesTests.Run(args[1],args[0]=="--vendor-updates-live-test");
            if(args.Length==2&&args[0]=="--task-dock-test")return TaskDockTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--preview-selection-test") return PreviewSelectionTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--search-media-ux-test") return SearchMediaUxTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--disk-tools-ux-test") return DiskToolsUxTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--settings-surfaces-test") return SettingsSurfacesTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--inline-tabs-test") return InlineTabsTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--pinned-tabs-test") return PinnedTabsTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--network-progress-ux-test") return NetworkProgressUxTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--operation-dialog-test") return OperationDialogTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--transfer-progress-layout-test") return TransferProgressLayoutTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--interface-theme-test") return InterfaceThemeTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--media-converter-test") return MediaConverterTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--image-batch-test") return ImageBatchTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--undo-history-test") return UndoHistoryTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--media-names-test") return MediaNamesTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--release-tools-test") return ReleaseToolsTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--help-window-test") return HelpWindowTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--live-language-test") return LiveLanguageTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--low-audit-test") return LowAuditTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--security-audit-test") return SecurityAuditTests.Run(args[1]);
            if (args.Length == 2 && args[0] == "--medium-audit-test") return MediumAuditTests.Run(args[1]);
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal)))
            {
                WorkflowStore.TestRoot = Path.Combine(Path.GetFullPath(args[1]), "workflow-profile");
                FileTagStore.TestPath = Path.Combine(WorkflowStore.TestRoot, "tags.xml");
            }
            if (args.Length == 3 && args[0] == "--instance-forward-test") {
                try { using (var instance = new InstanceCoordinator(args[1])) { if (instance.Primary) return 3; instance.Forward(args[2]); return 0; } }
                catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            }
            if (args.Length > 1 && args[0] == "--sshfs-setup-test") return SshfsSetupTests.Run(args[1]);
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) FileCompass.NetworkTransport.TransferCheckpoint.TestRoot = Path.Combine(Path.GetFullPath(args[1]), "transfer-checkpoints");
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) SshfsMount.TestRoot = Path.Combine(Path.GetFullPath(args[1]), "sshfs-state");
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) SearchAdvanced.TestDirectory = Path.GetFullPath(args[1]);
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) DurableQueue.TestDirectory = Path.GetFullPath(args[1]);
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) FileIntegrity.TestDirectory = Path.GetFullPath(args[1]);
            if (args.Length > 1 && args.Any(x => x.EndsWith("-test", StringComparison.Ordinal))) ArchiveSettings.TestPath = Path.Combine(Path.GetFullPath(args[1]), "archive-limits.xml");
            if (args.Contains("--ftp-name-test")) return NetworkResumeTests.NameOnly(args[1], Int32.Parse(args[2]));
            if (args.Contains("--ftp-queue-resume-test")) return NetworkResumeTests.QueueOnly(args[1], Int32.Parse(args[2]));
            if (args.Contains("--ftp-resume-test")) return NetworkResumeTests.Run(args[1], Int32.Parse(args[2]), false);
            if (args.Contains("--sftp-resume-test")) return NetworkResumeTests.Run(args[1], Int32.Parse(args[2]), true);
            if (args.Contains("--resume-once-test")) return NetworkResumeTests.Once(args);
            if (args.Length==2&&args[0]=="--icloud-test")return CloudICloudTests.Run(args[1]);
            if (args.Length==2&&args[0]=="--navigation-focus-test")return NavigationFocusTests.Run(args[1]);
            if (args.Contains("--navigation-test")) return PanelWorkflowTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--improvements-test")) return ImprovementsTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--ui-test")) return FluentUiTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--usability-test")) return UsabilityTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--settings-layout-test")) return SettingsLayoutTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--settings-connections-test")) return SettingsConnectionsTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--ergonomics-test")) return ErgonomicsTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--cloud-question-language-test")) return CloudQuestionLanguageTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--cloud-application-oauth-test")) return CloudApplicationOAuthTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--ux-surfaces-test")) return UxSurfaceTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--zip-progress-test")) return ZipProgressTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--duplicate-compare-test")) return DuplicateCompareTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--html-preview-test")) return HtmlPreviewTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--prepared-preview-test")) return PreparedPreviewTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--associated-editing-test")) return AssociatedEditingTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--ext-reader-test")return ExtReaderTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--help-screenshots-test")return HelpScreenshotTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--media-layout-test")return MediaLayoutTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--csv-preview-test")return CsvPreviewTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--preview-library-test")return PreviewLibraryTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--document-preview-test")) return DocumentPreviewTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--connections-dialog-test")) return ConnectionsDialogTests.Run(Path.GetFullPath(args[1])); if (args.Contains("--cloud-storage-test")) return CloudStorageTests.Run(Path.GetFullPath(args[1])); if (args.Contains("--metadata-search-test")) return MetadataSearchTests.Run(Path.GetFullPath(args[1])); if (args.Contains("--clipboard-file-test")) return ClipboardFileTests.Run(Path.GetFullPath(args[1])); if (args.Contains("--metadata-columns-test")) return MetadataColumnTests.Run(Path.GetFullPath(args[1])); if (args.Contains("--file-metadata-test")) return FileMetadataTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--portable-test")) return PortableTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--dpi-test")) return DpiTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--visual-feedback-test")) return VisualFeedbackTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--advanced-tools-test")) return AdvancedToolsTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--explorer-tree-test")) return ExplorerTreeTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--dialog-enter-test")) return DialogEnterTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--file-icons-test")) return FileIconTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--toolbar-dialog-test")) return ToolbarDialogTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--panel-layout-test")) return PanelLayoutTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--language-test")) return LanguageTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--disk-smart-test")) return DiskSmartTests.Run(Path.GetFullPath(args[1]),args.Contains("--live"));
            if (args.Contains("--preview-refresh-test")) return PreviewRefreshTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--disk-analyzer-upgrade-test")) return DiskAnalyzerUpgradeTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--editor-analyzer-screens-test")return EditorAnalyzerScreenshotTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--dispatch-units-test")return DispatchUnitsTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--editor-tempo-test")return EditorTempoTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--editor-analyzer-test")return EditorAnalyzerFeatureTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--disk-analyzer-test")) return DiskAnalyzerTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--appearance-language-test")) return AppearanceLanguageTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--media-workflow-test")) return MediaWorkflowTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--priority-workflow-test")) return PriorityWorkflowTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--instance-state-test")) return InstanceStateTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--more-file-options-test")) return MoreFileOptionsTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--file-behavior-test")) return FileBehaviorTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--archive-plugin-test")) return ArchivePluginTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--legacy-archive-test")) return LegacyArchiveTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--archive-formats-test")) return ArchiveFormatsTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--archive-presentation-test")) return ArchivePresentationTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--network-browser-test")) return NetworkBrowserTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--network-browser-live-test")) return NetworkBrowserTests.Live(Path.GetFullPath(args[1]));
            if (args.Contains("--sync-preview-test")) return SyncPreviewTests.Run(args[1]);
            if (args.Contains("--relay-test")) return RelayTests.Run(args[1],Int32.Parse(args[2]),Int32.Parse(args[3]),Int32.Parse(args[4]),args.Length > 5 ? Int32.Parse(args[5]) : 0);
            if (args.Contains("--file-tools-test")) return FileToolsTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if(args.Length==2&&args[0]=="--internal-preview-test")return InternalPreviewTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--file-context-test")return FileContextTests.Run(Path.GetFullPath(args[1]));
            if(args.Length==2&&args[0]=="--usb-drive-test")return UsbDriveTests.Run(Path.GetFullPath(args[1]));
            if (args.Contains("--drive-test")) return DriveUiTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--media-preview-test")) return MediaPreviewTests.Run(args[1]);
            if (args.Contains("--compression-progress-test")) return CompressionProgressTests.Run(args[1]);
            if (args.Contains("--archive-memory-test")) return ArchiveMemoryTests.Run(args[1]);
            if (args.Contains("--delete-display-test")) return DeleteDisplayTests.Run(args[1]);
            if (args.Contains("--theme-test")) return ToolbarThemeTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--startup-test")) return StartupTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--window-state-test")) return WindowStateTests.Run(args[1]);
            if (args.Contains("--local-copy-ui-test")) return LocalCopyPresentationTests.Run(args[1]);
            if (args.Contains("--next-stage-test")) return NextStageTests.Run(args[1]);
            if (args.Contains("--next-remote-test")) return NextRemoteTests.Execute(args[1],Int32.Parse(args[2]),Int32.Parse(args[3]),Int32.Parse(args[4]));
            if (args.Contains("--durable-reopen-test")) return QueuePersistenceTests.Run(args[1]);
            if (args.Contains("--durable-seed-test")) return NextStageTests.Seed(args[1]);
            if (args.Contains("--copy-test")) return CopyTests.Execute(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--archive-test")) return ArchiveTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--integrity-test")) return IntegrityTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--archive-upgrade-test")) return ArchiveUpgradeTests.Run(args[1]);
            if (args.Contains("--self-test")) return SmokeTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--network-test")) return NetworkTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--sshfs-test")) return SshfsTests.Run(args[1], Int32.Parse(args[2]));
            if (args.Contains("--sshfs-keep-test")) return SshfsTests.KeepOnce(args[1], Int32.Parse(args[2]));
            if (args.Contains("--ftp-test")) return NetworkTests.Ftp(args[1], Int32.Parse(args[2]));
            if (args.Contains("--sftp-test")) return NetworkTests.Ftp(args[1], Int32.Parse(args[2]), true, args.Length > 3 ? Int32.Parse(args[3]) : 0);
            if (args.Contains("--ssh-test")) return SshTests.Run(args.Length > 1 ? args[1] : Path.GetTempPath());
            if (args.Contains("--scp-test")) return NativeNetworkTests.Scp(args[1], Int32.Parse(args[2]));
#endif
            try {
                var preferences = ProgramSettings.Load(); string folder = InstanceCoordinator.FolderArgument(args);
                using (var instance = preferences.SingleInstance ? new InstanceCoordinator(InstanceCoordinator.Identity) : null) {
                    if (instance != null && !instance.Primary) { instance.Forward(folder); return 0; }
                    var app = new Application(); FluentTheme.Ensure();
                    app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                    { MessageBox.Show(e.Exception.Message, L.Text("app.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
                    UpdateRegistry.Startup();
                    var window = new MainWindow(null, preferences);
                    window.Loaded += delegate { window.AcceptLaunch(""); };
                    window.Loaded += async delegate { await System.Threading.Tasks.Task.Delay(2500); if(!window.UpdateHostClosed)await window.Updates.StartAutomatic(); };
                    if (folder.Length > 0) window.AcceptLaunch(folder);
                    if (instance != null) instance.Attach(delegate(string path) { if (!window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke(new Action(delegate { if(path.Length>0&&DeleteOperations.IsNetwork(path)&&MessageBox.Show(window,L.Text("security.forwardedNetworkFolder")+"\n\n"+path,ApplicationBrand.Name,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;window.AcceptLaunch(path); })); });
                    return app.Run(window);
                }
            } catch (Exception ex) { MessageBox.Show(ex.Message, L.Text("app.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
        }
    }
}
