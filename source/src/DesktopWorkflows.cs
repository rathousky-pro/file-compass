using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace FileCompass
{
    public partial class MainWindow
    {
        List<FavoriteLocation> favorites;
        const string CutFormat = "Preferred DropEffect";
        const string VirtualClipboardFormat = "FileCompass.VirtualSelection";
        string virtualClipboardId, virtualClipboardDisplay;
        Entry[] virtualClipboardEntries;
        RemoteConnection virtualClipboardRemote;
        ArchiveDocument virtualClipboardArchive;
        internal void InitializeWorkflows(ProgramSettings preferences)
        {
            favorites = preferences.Favorites ?? new List<FavoriteLocation>();
            left.BookmarksRequested=right.BookmarksRequested=delegate(FilePanel panel){SetActive(panel);ShowFavorites(true);};
            ApplyRefreshSettings(preferences.AutoRefresh, preferences.WatchNetworkFolders);
            ApplyDisplaySettings(preferences);
            left.FilesDropped = right.FilesDropped = QueueDroppedFiles;
            left.PanelFilesDropped = right.PanelFilesDropped = QueuePanelDrop;
            Closed += delegate { left.DisposePanelTools(); right.DisposePanelTools(); };
        }
        internal void ApplyRefreshSettings(bool enabled, bool network) { left.SetRefreshPolicy(enabled, network); right.SetRefreshPolicy(enabled, network); }
        bool HandleWorkflowKeys(KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key; ModifierKeys modifiers = Keyboard.Modifiers;
            bool editing = e.OriginalSource is TextBox || e.OriginalSource is PasswordBox;
            // Embedded browser controls own their clipboard shortcuts, including native PDF selections.
            if ((key==Key.C||key==Key.X||key==Key.V)&&!active.List.IsKeyboardFocusWithin)return false;
            if (modifiers == ModifierKeys.Control && key == Key.L) { active.FocusAddress(); e.Handled = true; return true; }
            if (editing || busy || PanelsLoading) return false;
            Action action = null;
            if (modifiers == ModifierKeys.Control) {
                if (key == Key.H) action = delegate { SetPanelLayout(panelLayout == "Stacked" ? "SideBySide" : "Stacked"); };
                if (key == Key.T) action = active.NewTab;
                if (key == Key.W) action = delegate { active.CloseTab(active.CurrentTabIndex); };
                if (key == Key.Tab) action = delegate { active.SwitchTab((active.CurrentTabIndex + 1) % active.TabCount); };
                if (key == Key.D) action = ShowFavorites;
                if (key == Key.F) action = active.FocusFilter;
                if (key == Key.C) action = delegate { CopyClipboard(false); };
                if (key == Key.X) action = delegate { CopyClipboard(true); };
                if (key == Key.V) action = PasteClipboard;
            }
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) {
                if (key == Key.H) action = delegate { SetPanelLayout(panelLayout == "Single" ? lastDualLayout : "Single"); };
                if (key == Key.Tab) action = delegate { active.SwitchTab((active.CurrentTabIndex + active.TabCount - 1) % active.TabCount); };
                if (key == Key.C) action = delegate { CopyNames(true); };
                if (key == Key.N) action = delegate { CopyNames(false); };
                if (key == Key.D) action = CompareFilesByContent; if (key == Key.V) action = PasteClipboardAsFile;
            }
            if (modifiers == ModifierKeys.Alt && key == Key.Enter) action = ShowProperties;
            if (action == null) return false;
            action(); e.Handled = true; return true;
        }
        internal async void ReopenTab(FilePanel panel, SavedTab saved) { await ReopenTabTask(panel, saved); }
        internal async Task ReopenTabTask(FilePanel panel, SavedTab saved)
        {
            if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath);
            var profile = saved.ProfileId!=null&&saved.ProfileId.StartsWith("cloud-")?CloudRuntime.SavedProfile(saved.ProfileId.Substring(6)):networkStore.Settings.Connections.FirstOrDefault(x => x.Id == saved.ProfileId);
            if (profile == null) { MessageBox.Show(this, L.Text("tabs.savedConnectionMissing"), L.Text("tabs.title")); return; }
            var copy = profile.Clone(); copy.Path = saved.Locked ? saved.Home : saved.Path; SetActive(panel); await ConnectNetworkTask(copy);
        }
        internal void OpenSameFolder() { OpenFolderInOtherPanel(active); }
        internal async void OpenFolderInOtherPanel(FilePanel source)
        {
            var other = source == left ? right : left;
            if (other.IsLoading || other.LocalLoading || source.CurrentPath == null) return;
            if (source.IsRemote) { var saved = source.SaveTabs()[source.CurrentTabIndex]; ReopenTab(other, saved); }
            else if (source.IsArchive) { var document = source.Archive; string prefix = source.ArchivePrefix; await other.OpenArchive(document.FilePath); if (other.IsArchive) other.Navigate("/" + prefix, false); }
            else other.Navigate(source.CurrentPath, true);
        }
        SavedTab CurrentLocation(FilePanel panel) { var saved = panel.SaveTabs()[panel.CurrentTabIndex]; return new SavedTab { Path = saved.Path, Home = saved.Home, Label = saved.Label, Caption = saved.Caption, ProfileId = saved.ProfileId, ArchiveFile = saved.ArchiveFile, ArchivePrefix = saved.ArchivePrefix, Locked = saved.Locked }; }
        async Task OpenFavoriteLocation(FilePanel panel, SavedTab location, string fallback)
        {
            if (location == null) { if (!String.IsNullOrEmpty(fallback)) panel.Navigate(fallback, true); return; }
            if (!String.IsNullOrEmpty(location.ProfileId)) await ReopenTabTask(panel, location);
            else if (!String.IsNullOrEmpty(location.ArchiveFile)) { await panel.OpenArchive(location.ArchiveFile); if (panel.IsArchive) panel.Navigate("/" + location.ArchivePrefix, false); }
            else panel.Navigate(location.Path, true);
        }
        async void OpenFavorite(FavoriteLocation favorite)
        {
            if (favorite.RightLocation != null || !String.IsNullOrEmpty(favorite.Right)) { await OpenFavoriteLocation(left, favorite.LeftLocation, favorite.Left); await OpenFavoriteLocation(right, favorite.RightLocation, favorite.Right); }
            else await OpenFavoriteLocation(active, favorite.LeftLocation, favorite.Left);
        }
        internal int BookmarkCount {get{return favorites.Count;}}
        internal void StoreFolderBookmark(string name,string group,FilePanel panel){favorites.Add(new FavoriteLocation{Name=name,Group=group,Left=panel.DisplayPath,LeftLocation=CurrentLocation(panel)});SaveFavorites();}
        internal async Task OpenBookmarkTab(FavoriteLocation favorite){
            if(favorite.RightLocation!=null||!String.IsNullOrEmpty(favorite.Right)){await left.OpenLocationTab(favorite.LeftLocation??new SavedTab{Path=favorite.Left});await right.OpenLocationTab(favorite.RightLocation??new SavedTab{Path=favorite.Right});}
            else await active.OpenLocationTab(favorite.LeftLocation??new SavedTab{Path=favorite.Left});
        }
        void AddFavorite(bool pair)
        {
            string name = Ask(L.Text("favorites.title"), L.Text("favorites.itemNameLabel"), pair ? L.Text("favorites.panelPairTitle") : active.CurrentPath); if (String.IsNullOrWhiteSpace(name)) return;
            string group = Ask(L.Text("favorites.title"), L.Text("favorites.groupLabel"), ""); if (group == null) return;
            if(!pair){StoreFolderBookmark(name.Trim(),group.Trim(),active);return;}
            favorites.Add(new FavoriteLocation { Name = name.Trim(), Group = group.Trim(), Left = (pair ? left : active).DisplayPath, Right = pair ? right.DisplayPath : "", LeftLocation = CurrentLocation(pair ? left : active), RightLocation = pair ? CurrentLocation(right) : null }); SaveFavorites();
        }
        void SaveFavorites() { var settings = ProgramSettings.Load(); settings.Favorites = favorites; settings.Save(); }
        void ShowFavorites(){ShowFavorites(false);}
        void ShowFavorites(bool newTab)
        {
            var menu = new ContextMenu();
            if(newTab)menu.Items.Add(new MenuItem{Header=L.Text("favorites.openInNewTab"),IsEnabled=false});
            foreach (var group in favorites.GroupBy(x => x.Group).OrderBy(x => x.Key)) {
                ItemsControl parent = menu;
                if (!String.IsNullOrEmpty(group.Key)) { var folder = new MenuItem { Header = group.Key }; menu.Items.Add(folder); parent = folder; }
                foreach (var favorite in group) { var captured = favorite; var item = new MenuItem { Header = favorite.Name, ToolTip = favorite.Left + (String.IsNullOrEmpty(favorite.Right) ? "" : " ↔ " + favorite.Right) }; item.Click += async delegate {if(newTab)await OpenBookmarkTab(captured);else OpenFavorite(captured);}; parent.Items.Add(item); }
            }
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            FavoriteMenuAction(menu, L.Text("favorites.addCurrentFolder"), delegate { AddFavorite(false); }); FavoriteMenuAction(menu, L.Text("favorites.savePanelPair"), delegate { AddFavorite(true); }); FavoriteMenuAction(menu, L.Text("favorites.manage"), ManageFavorites);
            menu.PlacementTarget = active; menu.IsOpen = true;
        }
        static void FavoriteMenuAction(ContextMenu menu, string title, Action action) { var item = new MenuItem { Header = title }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        void ManageFavorites()
        {
            var window = new Window { Owner = this, Title = L.Text("favorites.title"), Width = 620, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner }; FluentTheme.Dialog(window);
            var body = new DockPanel { Margin = new Thickness(12) }; window.Content = body;
            var list = new ListBox { Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), DisplayMemberPath = "" };
            var actions = FluentTheme.Actions(); DockPanel.SetDock(actions, Dock.Bottom); body.Children.Add(actions); body.Children.Add(list);
            Action reload = delegate { list.ItemsSource = null; list.ItemsSource = favorites; };
            actions.Children.Add(CopyUi.Button(L.Text("action.editDialog"), delegate { var item = list.SelectedItem as FavoriteLocation; if (item == null) return; string name = Ask(L.Text("favorites.shortTitle"), L.Text("common.nameLabel"), item.Name); if (String.IsNullOrWhiteSpace(name)) return; string group = Ask(L.Text("favorites.shortTitle"), "Skupina:", item.Group); if (group == null) return; item.Name = name; item.Group = group; SaveFavorites(); reload(); }));
            actions.Children.Add(CopyUi.Button(L.Text("action.remove"), delegate { var item = list.SelectedItem as FavoriteLocation; if (item == null) return; favorites.Remove(item); SaveFavorites(); reload(); }));
            actions.Children.Add(CopyUi.Button(L.Text("action.open"), delegate { var item = list.SelectedItem as FavoriteLocation; if (item != null) { window.Close(); OpenFavorite(item); } }));
            actions.Children.Add(CopyUi.Button(L.Text("action.close"), window.Close)); reload(); window.ShowDialog();
        }
        internal void CopyNames(bool full)
        {
            var entries = active.Selected(); if (entries.Count == 0) return;
            try { Clipboard.SetText(String.Join(Environment.NewLine, entries.Select(x => full ? active.IsRemote ? active.Remote.Display(x.FullPath) : active.IsArchive ? active.Archive.Display + "\\" + x.FullPath.TrimStart('/').Replace('/', '\\') : x.FullPath : FilePanel.EntryName(x)))); }
            catch (ExternalException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
            catch (UnauthorizedAccessException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
        }
        internal async void CopyClipboard(bool cut)
        {
            if(active.IsExt){MessageBox.Show(this,L.Text("ext.copy" )+" (F5)",L.Text("ext.title"));return;}
            if (active.IsRemote || active.IsArchive) {
                if (cut) { MessageBox.Show(this, L.Text("clipboard.remoteMoveUnsupportedHelp"), L.Text("clipboard.title")); return; }
                var entries = active.Selected().ToArray(); if (entries.Length == 0) return;
                virtualClipboardId = Guid.NewGuid().ToString("N"); virtualClipboardEntries = entries; virtualClipboardRemote = active.Remote; virtualClipboardArchive = active.Archive; virtualClipboardDisplay = active.DisplayPath;
                try { var data = new DataObject(); data.SetData(VirtualClipboardFormat, virtualClipboardId); data.SetText(String.Join(Environment.NewLine, entries.Select(x => active.IsRemote ? active.Remote.Display(x.FullPath) : active.Archive.Display + "\\" + x.FullPath.TrimStart('/')))); if(!await ClipboardTextWriter.TryExecuteAsync(()=>Clipboard.SetDataObject(data,true)))MessageBox.Show(this,L.Text("smart.clipboardBusy"),L.Text("clipboard.unavailableTitle")); }
                catch (ExternalException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); } catch (UnauthorizedAccessException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
                return;
            }
            var paths = active.Selected().Select(x => x.FullPath).ToArray(); if (paths.Length == 0) return;
            try { var data=CreateFileClipboard(paths,cut);if(!await ClipboardTextWriter.TryExecuteAsync(()=>Clipboard.SetDataObject(data,true)))MessageBox.Show(this,L.Text("smart.clipboardBusy"),L.Text("clipboard.unavailableTitle")); }
            catch (ExternalException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
            catch (UnauthorizedAccessException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
        }
        internal static IDataObject CreateFileClipboard(string[] paths, bool cut) { var data = new DataObject(DataFormats.FileDrop, paths); data.SetData(CutFormat, new MemoryStream(BitConverter.GetBytes(cut ? 2 : 5))); return data; }
        internal static bool ClipboardIsMove(IDataObject data)
        {
            var stream = data.GetData(CutFormat) as MemoryStream; if (stream == null || stream.Length < 4) return false; var bytes = stream.ToArray(); return (BitConverter.ToInt32(bytes, 0) & 2) != 0;
        }
        internal void PasteClipboard()
        {
            if(RejectExtWrite())return;
            if (active.IsProjection) { MessageBox.Show(this, L.Text("projection.destinationRejected"), ApplicationBrand.Name); return; }
            try { var data = Clipboard.GetDataObject(); if (data == null) return;
                if (data.GetDataPresent(VirtualClipboardFormat)) { if ((string)data.GetData(VirtualClipboardFormat) != virtualClipboardId || virtualClipboardEntries == null) { MessageBox.Show(this, L.Text("clipboard.sourceInstanceUnavailable"), L.Text("clipboard.title")); return; } PasteVirtualClipboard(active); return; }
                if (!data.GetDataPresent(DataFormats.FileDrop)) return; var paths = data.GetData(DataFormats.FileDrop) as string[]; if (paths != null) QueueDroppedFiles(active, paths, ClipboardIsMove(data)); }
            catch (ExternalException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
            catch (UnauthorizedAccessException ex) { MessageBox.Show(this, ex.Message, L.Text("clipboard.unavailableTitle")); }
        }
        async void PasteVirtualClipboard(FilePanel target)
        {
            if(target.IsExt){MessageBox.Show(this,L.Text("ext.readOnly"),L.Text("ext.title"));return;}
            if (target.LocalLoading || target.IsLoading || target.CurrentPath == null) return;
            var entries = virtualClipboardEntries; var sourceRemote = virtualClipboardRemote; var sourceArchive = virtualClipboardArchive; string sourceDisplay = virtualClipboardDisplay;
            var targetRemote = target.Remote; var targetArchive = target.Archive; string destination = target.CurrentPath, prefix = target.ArchivePrefix;
            if (sourceRemote != null && targetRemote != null && sourceArchive == null && targetArchive == null) { MessageBox.Show(this, L.Text("clipboard.serverRelayUnavailableHelp"), L.Text("clipboard.title")); return; }
            if (sourceArchive != null || targetArchive != null) {
                if (targetArchive != null && !ArchiveEngine.Writable(targetArchive)) { MessageBox.Show(this, L.Text("archive.readOnlyClipboardWarning"), L.Text("archive.shortTitle")); return; }
                if (sourceArchive != null && !ArchivePassword(sourceArchive, entries.Select(x => x.FullPath.TrimStart('/')))) return;
                if (targetArchive != null && !ArchivePassword(targetArchive, targetArchive.Items.Select(x => x.Path))) return;
                CopyOptions options = new CopyOptions(); NetworkCopyRule rule;
                if (sourceRemote != null || targetRemote != null) { var dialog = new NetworkCopyDialog(this, entries.Length, sourceDisplay, target.DisplayPath, sourceRemote == null ? null : sourceRemote.Profile, targetRemote == null ? null : targetRemote.Profile); if (dialog.ShowDialog() != true) return; rule = dialog.Rule; }
                else { var dialog = new ArchiveCopyDialog(this, entries.Length, sourceDisplay, target.DisplayPath, targetArchive != null); if (dialog.ShowDialog() != true) return; options = dialog.Options; rule = options.Rule == CopyRule.SkipAll ? NetworkCopyRule.Skip : NetworkCopyRule.Overwrite; }
                await RunArchiveTransfer(entries, sourceArchive, targetArchive, prefix, destination, options, rule, ArchiveSettings.Load(), sourceRemote, targetRemote, target);
            } else {
                var dialog = new NetworkCopyDialog(this, entries.Length, sourceDisplay, target.DisplayPath, sourceRemote.Profile, null); if (dialog.ShowDialog() != true) return;
                await StartNetworkCopy(entries, sourceRemote, true, destination, target, dialog.Rule);
            }
        }
        internal async void QueueDroppedFiles(FilePanel target, string[] paths, bool move)
        {
            if(target.IsExt){MessageBox.Show(this,L.Text("ext.readOnly"),L.Text("ext.title"));return;}
            if (target.IsProjection) { MessageBox.Show(this, L.Text("projection.destinationRejected"), ApplicationBrand.Name); return; }
            if (target.LocalLoading || target.IsLoading || target.CurrentPath == null) return;
            string destination = target.CurrentPath; List<Entry> entries = null; Exception failure = null;
            var remote = target.Remote; var document = target.Archive; string archivePrefix = target.ArchivePrefix, targetDisplay = target.DisplayPath;
            try { entries = await Task.Run(delegate { return paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(p => { var info = new FileInfo(p); var attributes = File.GetAttributes(p); bool dir = (attributes & FileAttributes.Directory) != 0; return new Entry { FullPath = Path.GetFullPath(p), Name = Path.GetFileName(p), IsDirectory = dir, Bytes = dir ? 0 : info.Length }; }).ToList(); }); }
            catch (Exception ex) { failure = ex; }
            if (failure != null) { MessageBox.Show(this, failure.Message, L.Text("clipboard.pasteFailed")); return; }
            if (entries.Count == 0) return;
            if (remote != null || document != null) {
                if (move) { MessageBox.Show(this, L.Text("clipboard.remoteDestinationMoveUnsupported"), L.Text("action.paste")); return; }
                if (document != null) {
                    if (!ArchiveEngine.Writable(document)) { MessageBox.Show(this, L.Text("archive.readOnlyClipboardWarning"), L.Text("archive.shortTitle")); return; }
                    if (!ArchivePassword(document, document.Items.Select(x => x.Path))) return;
                    var dialog = new ArchiveCopyDialog(this, entries.Count, String.Join("; ", entries.Select(x => x.FullPath)), targetDisplay, true); if (dialog.ShowDialog() != true) return;
                    await RunArchiveTransfer(entries.ToArray(), null, document, archivePrefix, destination, dialog.Options, dialog.Options.Rule == CopyRule.SkipAll ? NetworkCopyRule.Skip : NetworkCopyRule.Overwrite, ArchiveSettings.Load(), null, null, target);
                } else {
                    var dialog = new NetworkCopyDialog(this, entries.Count, String.Join("; ", entries.Select(x => x.FullPath)), targetDisplay, null, remote.Profile); if (dialog.ShowDialog() != true) return;
                    await StartNetworkCopy(entries.ToArray(), remote, false, destination, target, dialog.Rule);
                }
                return;
            }
            var setup = new CopySetupDialog(this, entries, destination, copyTargets, move); if (setup.ShowDialog() != true) return;
            setup.Options.Move = move;
            QueueExternalCopy(entries.Select(x => x.FullPath).ToArray(), setup.Options);
        }
        internal void QueueExternalCopy(string[] paths, CopyOptions options)
        {
            EnqueueCopy(new CopyJob { Sources = paths, Options = options }); if (!copyRunning) RunCopyQueue();
        }
        async void QueuePanelDrop(FilePanel source, FilePanel target, Entry[] entries, bool move)
        {
            if(source.IsExt||target.IsExt){MessageBox.Show(this,L.Text("ext.copy")+" (F5)",L.Text("ext.title"));return;}
            if (target.IsProjection) { MessageBox.Show(this, L.Text("projection.destinationRejected"), ApplicationBrand.Name); return; }
            if (source == target || source.IsLoading || target.IsLoading || source.LocalLoading || target.LocalLoading) return;
            if (source.IsArchive || target.IsArchive) { if (move) { MessageBox.Show(this, L.Text("archive.moveUnsupportedClipboardHelp"), L.Text("archive.shortTitle")); return; } await TransferArchiveEntries(source, target, entries); }
            else if (source.IsRemote || target.IsRemote) await TransferNetworkEntries(move, source, target, entries);
            else QueueDroppedFiles(target, entries.Select(x => x.FullPath).ToArray(), move);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ShellExecution
        {
            public int Size; public uint Mask; public IntPtr Window;
            [MarshalAs(UnmanagedType.LPWStr)] public string Verb, File, Parameters, Directory;
            public int Show; public IntPtr Instance, IdList;
            [MarshalAs(UnmanagedType.LPWStr)] public string Class;
            public IntPtr ClassKey; public uint HotKey; public IntPtr Icon, Process;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool ShellExecuteEx(ref ShellExecution execution);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct OpenAsInfo { [MarshalAs(UnmanagedType.LPWStr)] public string File, Class; public uint Flags; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHOpenWithDialog(IntPtr owner, ref OpenAsInfo info);
        internal static void ShellAction(IntPtr owner, string path, string verb)
        {
            if (verb == "openas") { var open = new OpenAsInfo { File = path, Flags = 5 }; int result = SHOpenWithDialog(owner, ref open); if (result < 0 && result != unchecked((int)0x80004004) && result != unchecked((int)0x800704C7)) Marshal.ThrowExceptionForHR(result); return; }
            var info = new ShellExecution { Size = Marshal.SizeOf(typeof(ShellExecution)), Mask = 0x0000000C, Window = owner, Verb = verb, File = path, Show = 1 };
            if (!ShellExecuteEx(ref info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        internal void ShowProperties() { WindowsAction("properties"); }
        internal void OpenWith() { WindowsAction("openas"); }
        void WindowsAction(string verb)
        {
            if (active.IsRemote || active.IsArchive) { MessageBox.Show(this, L.Text("shell.propertiesSupportedLocations"), "Windows"); return; }
            var entries = active.Selected(); if (entries.Count == 0) return;
            if (verb == "openas" && entries.Any(x => x.IsDirectory)) { MessageBox.Show(this, L.Text("shell.openWithRequiresFile"), "Windows"); return; }
            if (entries.Count != 1) { MessageBox.Show(this, L.Text("selection.exactlyOneRequired"), "Windows"); return; }
            try { ShellAction(new WindowInteropHelper(this).Handle, entries[0].FullPath, verb); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("shell.actionFailed")); }
        }
        internal void ChangeAttributes()
        {
            if (active.IsRemote || active.IsArchive) { MessageBox.Show(this, L.Text("attributes.supportedLocations"), L.Text("attributes.title")); return; }
            var entries = active.Selected(); if (entries.Count == 0) return;
            var sourceCursor = active.List.SelectedItem as Entry;
            var other = active == left ? right : left; var targetCursor = other.List.SelectedItem as Entry;
            new AttributesDialog(this, entries.Select(x => x.FullPath).ToArray(), delegate(string[] paths, AttributeChanges changes) { ApplyAttributes(paths, changes); },
                sourceCursor == null || sourceCursor.IsParent ? null : sourceCursor.FullPath,
                other.IsRemote || other.IsArchive || targetCursor == null || targetCursor.IsParent ? null : targetCursor.FullPath).ShowDialog();
        }
        async void ApplyAttributes(string[] paths, AttributeChanges changes)
        {
            using(var control=new CopyControl()){
            bool cancel = false; var activity = AddCopyActivity(L.Text("attributes.changeDialogTitle"), delegate { cancel = true;control.Cancel(); });activity.CompactDock=true;
            activity.PauseAction=delegate{control.Pause(!control.Paused);};activity.IsPaused=delegate{return control.Paused;};
            var result = await Task.Run(delegate { return AttributeChanges.Apply(paths, changes, delegate { try{control.Check();return cancel;}catch(OperationCanceledException){return true;} }); });
            activity.Record.Items.AddRange(result); activity.Finish(cancel ? L.Code("state.cancelled") : L.Text("attributes.changedErrorCountPrefix") + result.Count(x => x.State == L.Code("state.error")), !cancel && !result.Any(x => x.State == L.Code("state.error")));
            var failed = result.Where(x => x.State == L.Code("state.error")).Select(x => x.Source).ToArray(); if (failed.Length > 0) activity.Record.Retry = delegate { ApplyAttributes(failed, changes); };
            left.Refresh(); right.Refresh();
            }
        }
    }
}
