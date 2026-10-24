using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FileCompass
{
    public partial class FilePanel
    {
        readonly Dictionary<string, HashSet<string>> savedSelection = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        FileSystemWatcher folderWatcher;
        readonly DispatcherTimer watchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        int watchVersion, watchDirty;
        long lastWatchChange;
        bool autoRefresh = true, watchNetwork;
        internal Func<bool> DeferAutomaticRefresh;
        string watchedPath;
        Point dragStart;
        bool leftDragReady;
        DateTime rightPressed;
        Point rightStart;
        bool rightMoved;
        internal Action<FilePanel, string[], bool> FilesDropped;
        internal Action<FilePanel, FilePanel, Entry[], bool> PanelFilesDropped;
        internal bool WatcherActive { get { return folderWatcher != null; } }
        internal void InitializePanelTools()
        {
            List.AllowDrop = true;
            List.PreviewTextInput += delegate(object s, TextCompositionEventArgs e) {
                if (Keyboard.Modifiers != ModifierKeys.None || String.IsNullOrEmpty(e.Text) || LocalLoading || IsLoading) return;
                SetQuickFilter(quickText.Text + e.Text); e.Handled = true;
            };
            List.PreviewKeyDown += delegate(object s, KeyEventArgs e) {
                if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) { List.ContextMenu.PlacementTarget = List; List.ContextMenu.IsOpen = true; e.Handled = true; return; }
                if (e.Key == Key.Escape && quickBar.Visibility == Visibility.Visible) { SetQuickFilter(""); e.Handled = true; }
                if (e.Key == Key.Back && quickBar.Visibility == Visibility.Visible && quickText.Text.Length > 0) { SetQuickFilter(quickText.Text.Substring(0, quickText.Text.Length - 1)); e.Handled = true; }
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Home || e.Key == Key.End || e.Key == Key.PageUp || e.Key == Key.PageDown)) {
                    int old = List.SelectedIndex, next = old;
                    if (e.Key == Key.Up) next -= DisplayColumns; if (e.Key == Key.Down) next += DisplayColumns; if (e.Key == Key.Home) next = 0; if (e.Key == Key.End) next = List.Items.Count - 1;
                    if (e.Key == Key.PageUp) next -= Math.Max(1, (int)(List.ActualHeight / 21) - 1); if (e.Key == Key.PageDown) next += Math.Max(1, (int)(List.ActualHeight / 21) - 1);
                    next = Math.Max(0, Math.Min(next, List.Items.Count - 1)); if (old >= 0 && next >= 0) { RememberSelection(); MarkRange((Entry)List.Items[old], (Entry)List.Items[next]); List.SelectedIndex = next; List.ScrollIntoView(List.SelectedItem); } e.Handled = true;
                }
                if (e.Key == Key.Add && Keyboard.Modifiers == ModifierKeys.None) { SelectMask(true); e.Handled = true; }
                if (e.Key == Key.Subtract && Keyboard.Modifiers == ModifierKeys.None) { SelectMask(false); e.Handled = true; }
            };
            // The existing click handler owns focus/marking. Observe handled clicks for drag detection.
            List.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(delegate(object s, MouseButtonEventArgs e) { dragStart = e.GetPosition(List); leftDragReady = RowAt(dragStart) != null && e.ClickCount == 1; }), true);
            List.AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(delegate(object s, MouseButtonEventArgs e) { rightPressed = DateTime.UtcNow; rightStart = e.GetPosition(List); rightMoved = false; }), true);
            List.AddHandler(UIElement.PreviewMouseRightButtonUpEvent, new MouseButtonEventHandler(delegate(object s, MouseButtonEventArgs e) { if (!rightMoved && (DateTime.UtcNow - rightPressed > TimeSpan.FromMilliseconds(450) || (!IsRemote && !IsArchive && NetworkBrowser.CanMap(List.SelectedItem as Entry))) && List.ContextMenu != null) { List.ContextMenu.PlacementTarget = List; List.ContextMenu.IsOpen = true; } }), true);
            List.PreviewMouseLeftButtonUp += delegate { leftDragReady = false; };
            List.PreviewMouseMove += delegate(object s, MouseEventArgs e) {
                if (e.RightButton == MouseButtonState.Pressed && (e.GetPosition(List) - rightStart).Length > SystemParameters.MinimumHorizontalDragDistance) rightMoved = true;
                if (!leftDragReady || e.LeftButton != MouseButtonState.Pressed || LocalLoading || IsLoading) return;
                var position = e.GetPosition(List); if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                leftDragReady = false; var files = Selected().Select(x => x.FullPath).ToArray(); if (files.Length == 0) return;
                var data = new DataObject(); if (!IsRemote && !IsArchive) data.SetData(DataFormats.FileDrop, files); data.SetData(L.Code("clipboard.panelDataFormat"), this); data.SetData("FileCompass.Entries", Selected().ToArray());
                DragDrop.DoDragDrop(List, data, DragDropEffects.Copy);
            };
            List.DragOver += delegate(object s, DragEventArgs e) {
                bool supported = !LocalLoading && !IsLoading && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(L.Code("clipboard.panelDataFormat"))) && CurrentPath != null;
                e.Effects = supported ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
            };
            List.Drop += delegate(object s, DragEventArgs e) {
                if (LocalLoading || IsLoading) return;
                if (e.Data.GetDataPresent(L.Code("clipboard.panelDataFormat"))) { var source = e.Data.GetData(L.Code("clipboard.panelDataFormat")) as FilePanel; var entries = e.Data.GetData("FileCompass.Entries") as Entry[]; if (source != null && entries != null && PanelFilesDropped != null) PanelFilesDropped(source, this, entries, (e.KeyStates & DragDropKeyStates.ShiftKey) != 0); e.Effects = DragDropEffects.Copy; e.Handled = true; return; }
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths == null) return;
                bool move = e.Data.GetDataPresent(L.Code("clipboard.panelDataFormat")) && (e.KeyStates & DragDropKeyStates.ShiftKey) != 0;
                if (FilesDropped != null) FilesDropped(this, paths, move); e.Effects = DragDropEffects.Copy; e.Handled = true;
            };
            var context = new ContextMenu();
            var mapShare = new MenuItem { Header = L.Text("drive.mapFolderAsDrive") };
            mapShare.Click += delegate { var selected = List.SelectedItem as Entry; var main = Window.GetWindow(this) as MainWindow; if (main != null && !IsRemote && !IsArchive && NetworkBrowser.CanMap(selected)) main.MapNetworkFolder(selected.FullPath); };
            context.Items.Add(mapShare);
            context.Opened += delegate { mapShare.Visibility = !IsRemote && !IsArchive && NetworkBrowser.CanMap(List.SelectedItem as Entry) ? Visibility.Visible : Visibility.Collapsed; };
            AddTabAction(context, L.Text("shell.propertiesShortcut"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.ShowProperties(); });
            AddTabAction(context, L.Text("action.openWith"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.OpenWith(); });
            AddTabAction(context, L.Text("attributes.changeMenu"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.ChangeAttributes(); });
            context.Items.Add(new Separator());
            AddTabAction(context, L.Text("action.copy"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.CopyClipboard(false); });
            AddTabAction(context, L.Text("action.cut"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.CopyClipboard(true); });
            AddTabAction(context, L.Text("clipboardFile.title"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.PasteClipboardAsFile(); }); AddTabAction(context, L.Text("action.paste"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.PasteClipboard(); });
            AddTabAction(context, L.Text("clipboard.copyFullPaths"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.CopyNames(true); });
            AddTabAction(context, L.Text("tags.edit"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.EditTags(); });
            context.Items.Add(new Separator());
            var analyze = new MenuItem { Header = L.Text("analyzer.title"), ToolTip = L.Text("analyzer.help") };
            analyze.Click += delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.AnalyzeFolder(this); };
            context.Items.Add(analyze);
            context.Opened += delegate { analyze.IsEnabled = !LocalLoading && !IsLoading && !IsArchive && !IsRemote && !IsVirtualBrowser && !String.IsNullOrEmpty(CurrentPath); };
            AddTabAction(context, L.Text("action.compareFiles"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.CompareFilesByContent(); });
            AddTabAction(context, L.Text("action.createChecksums"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.CreateChecksumsDialog(); });
            AddTabAction(context, L.Text("action.verifyChecksums"), delegate { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.VerifyChecksumsDialog(); });
            List.ContextMenu = context;
            watchTimer.Tick += delegate {
                if (DeferAutomaticRefresh != null && DeferAutomaticRefresh()) return;
                if (Interlocked.CompareExchange(ref watchDirty, 0, 0) == 0 || DateTime.UtcNow.Ticks - Interlocked.Read(ref lastWatchChange) < TimeSpan.FromMilliseconds(800).Ticks || LocalLoading || IsLoading || rightDragging || Mouse.LeftButton == MouseButtonState.Pressed) return;
                Interlocked.Exchange(ref watchDirty, 0); if (!IsRemote && !IsArchive && CurrentPath == watchedPath) Refresh();
            };
            Loaded += delegate { watchTimer.Start(); ConfigureWatcher(); }; Unloaded += delegate { DisposePanelTools(); };
        }
        string SelectionKey { get { return (IsRemote ? Remote.Profile.Id + ":" : IsArchive ? Archive.FilePath + ":" : "") + CurrentPath; } }
        internal void RememberSelection() { savedSelection[SelectionKey] = new HashSet<string>(List.Items.Cast<Entry>().Where(x => x.IsMarked).Select(x => x.FullPath), StringComparer.Ordinal); }
        internal void RestoreSelection() { HashSet<string> paths; if (!savedSelection.TryGetValue(SelectionKey, out paths)) return; foreach (Entry item in List.Items) item.IsMarked = paths.Contains(item.FullPath); UpdateStatus(); }
        internal void MarkMask(string mask, bool mark) { RememberSelection(); foreach (Entry item in List.Items) if (!item.IsParent && (maskIncludesFolders || !item.IsDirectory) && MaskMatches(EntryName(item), mask)) item.IsMarked = mark; UpdateStatus(); }
        internal void SelectMask(bool mark) { string mask = NavigationPrompt(mark ? L.Text("selection.markMaskTitle") : L.Text("selection.unmarkMaskTitle"), L.Text("selection.maskPrompt"), "*.*"); if (mask != null) MarkMask(mask, mark); }
        internal void SelectExtension() { var item = List.SelectedItem as Entry; if (item == null || item.IsDirectory) return; RememberSelection(); foreach (Entry other in List.Items) if (!other.IsDirectory && String.Equals(item.Extension, other.Extension, StringComparison.OrdinalIgnoreCase)) other.IsMarked = true; UpdateStatus(); }
        internal void SetRefreshPolicy(bool enabled, bool network) { autoRefresh = enabled; watchNetwork = network; watchedPath = null; ConfigureWatcher(); }
        internal void DisposePanelTools() { watchVersion++; watchTimer.Stop(); var previous = folderWatcher; folderWatcher = null; watchedPath = null; if (previous != null) previous.Dispose(); completions.IsOpen = false; }
        internal async void ConfigureWatcher()
        {
            if (watchedPath == CurrentPath && folderWatcher != null && !IsRemote && !IsArchive && autoRefresh) return;
            int version = ++watchVersion; var old = folderWatcher; folderWatcher = null; if (old != null) old.Dispose(); watchedPath = null;
            if (!autoRefresh || IsProjection || IsRemote || IsArchive || IsVirtualBrowser || CurrentPath == null) return;
            string path = CurrentPath; bool allowNetwork = watchNetwork; FileSystemWatcher candidate = null;
            try {
                candidate = await Task.Run(delegate {
                    bool network = path.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network;
                    if (network && !allowNetwork) return null;
                    var watcher = new FileSystemWatcher(path) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.Attributes };
                    return watcher;
                });
                if (version != watchVersion || path != CurrentPath) { if (candidate != null) candidate.Dispose(); return; }
                if (candidate == null) return;
                FileSystemEventHandler changed = delegate { Interlocked.Exchange(ref lastWatchChange, DateTime.UtcNow.Ticks); Interlocked.Exchange(ref watchDirty, 1); };
                candidate.Changed += changed; candidate.Created += changed; candidate.Deleted += changed; candidate.Renamed += delegate { changed(null, null); };
                candidate.Error += delegate { changed(null, null); };
                folderWatcher = candidate; watchedPath = path; candidate.EnableRaisingEvents = true; watchTimer.Start();
            } catch (Exception ex) { if (candidate != null) candidate.Dispose(); if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)) throw; }
        }
    }
}

