using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileCompass
{
    public sealed class ProjectionSpec { public string Kind, Root, Query; public string[] Paths; }
    public sealed class BasketData { public List<string> Paths = new List<string>(); }
    internal static class FileBasket
    {
        internal static BasketData Load() { return WorkflowStore.Load<BasketData>("basket.xml"); }
        internal static void Add(IEnumerable<string> paths)
        {
            WorkflowStore.Edit<BasketData>("basket.xml", delegate(BasketData data) { data.Paths = data.Paths.Concat(paths.Select(Path.GetFullPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); if (data.Paths.Count > 20000) throw new IOException(L.Text("projection.itemLimit")); });
        }
        internal static void Remove(IEnumerable<string> paths) { var remove = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase); WorkflowStore.Edit<BasketData>("basket.xml", data => data.Paths.RemoveAll(remove.Contains)); }
        internal static void Moved(IEnumerable<KeyValuePair<string,string>> moves)
        {
            var data = Load(); bool changed = false; var mappings = moves.OrderByDescending(x => x.Key.Length).ToArray();
            data.Paths = data.Paths.Select(path => { foreach (var pair in mappings) if (path.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) || path.StartsWith(pair.Key.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { changed = true; return pair.Value.TrimEnd('\\') + path.Substring(pair.Key.TrimEnd('\\').Length); } return path; }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (changed) WorkflowStore.Save("basket.xml", data);
        }
    }
    internal static class FileAssociations
    {
        internal static void Moved(string from, string to) { Moved(new[] { new KeyValuePair<string,string>(from, to) }); }
        internal static void Moved(IEnumerable<KeyValuePair<string,string>> moves)
        {
            var snapshot = moves.ToArray(); FileTagStore.Moved(snapshot);
            try { FileBasket.Moved(snapshot); } catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)) throw; FileTagStore.LastError = ex.Message; }
        }
    }
    internal static class ProjectionFiles
    {
        internal static string Title(ProjectionSpec spec) { return spec.Kind == "Flat" ? L.Text("projection.flat") : spec.Kind == "Everything" ? L.Text("everything.title") : spec.Kind == "Tags" ? L.Text("tags.results") : spec.Kind == "Search" ? L.Text("search.resultsPanelTitle") : L.Text("projection.basket"); }
        internal static string[] Tagged(string query)
        {
            var rows = FileTagStore.Current.Snapshot(); if (FileTagStore.LastError != null) throw new IOException(L.Text("tags.loadError") + " " + FileTagStore.LastError);
            return rows.Where(x => x.Tags != null && x.Tags.Any(tag => tag.IndexOf(query ?? "", StringComparison.CurrentCultureIgnoreCase) >= 0)).Select(x => x.Path).ToArray();
        }
        internal static string[] Scan(string root, CancellationToken token)
        {
            var paths = new List<string>(); var stack = new Stack<string>(); stack.Push(Path.GetFullPath(root));
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested(); string folder = stack.Pop();
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string path in Directory.EnumerateFileSystemEntries(folder))
                {
                    token.ThrowIfCancellationRequested(); var attributes = File.GetAttributes(path); if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) stack.Push(path); else paths.Add(path);
                    if (paths.Count > 100000 || stack.Count > 100000) throw new IOException(L.Text("projection.itemLimit"));
                }
            }
            return paths.ToArray();
        }
        internal static List<Entry> Rows(IEnumerable<string> paths, string root, CancellationToken token)
        {
            var rows = new List<Entry> { new Entry { Name = "[..]", FullPath = root, IsDirectory = true, IsParent = true, Icon = DesktopIcons.FileIcon(root, true) } };
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    FileSystemInfo info = Directory.Exists(path) ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path); if (!info.Exists) continue;
                    bool directory = (info.Attributes & FileAttributes.Directory) != 0; string extension = directory ? "" : Path.GetExtension(path).TrimStart('.'); string folder = Path.GetDirectoryName(path);
                    string location = folder.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ? folder.Substring(root.TrimEnd('\\').Length + 1) : folder.Equals(root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "" : folder;
                    string name = directory ? "[" + info.Name + "]" : extension.Length == 0 ? info.Name : Path.GetFileNameWithoutExtension(path);
                    rows.Add(new Entry { FullPath = info.FullName, Name = name, Extension = extension, IsDirectory = directory, IsLink = (info.Attributes & FileAttributes.ReparsePoint) != 0, IsHidden = (info.Attributes & FileAttributes.Hidden) != 0, IsSystem = (info.Attributes & FileAttributes.System) != 0, Bytes = directory ? 0 : ((FileInfo)info).Length, ModifiedTime = info.LastWriteTime, Modified = info.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), Attributes = info.Attributes.ToString(), Icon = DesktopIcons.FileIcon(path, directory), LocationName = String.IsNullOrEmpty(location) ? info.Name : location + "  ›  " + info.Name });
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            FileTagStore.Apply(rows); return rows;
        }
    }
    public partial class FilePanel
    {
        ProjectionSpec projection;
        internal bool IsProjection { get { return projection != null; } }
        internal ProjectionSpec Projection { get { return projection; } }
        internal void ShowProjection(ProjectionSpec spec, bool newTab = true)
        {
            if (LocalLoading || IsLoading) return;
            if (newTab) NewTab(); NavigationTask = LoadProjection(spec);
        }
        async Task LoadProjection(ProjectionSpec spec)
        {
            CancelNavigation(); var cancel = new CancellationTokenSource(); navigationCancellation = cancel; var token = cancel.Token; int request = ++navigationVersion;
            LocalLoading = true; List.IsEnabled = false; status.Text = L.Text("panel.loadingFolder");
            string root = null; Exception failure = null;
            try
            {
                if (spec == null || (spec.Kind != "Flat" && spec.Kind != "Basket" && spec.Kind != "Everything" && spec.Kind != "Tags" && spec.Kind != "Search")) throw new IOException(L.Text("projection.invalid"));
                root = String.IsNullOrEmpty(spec.Root) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : Path.GetFullPath(spec.Root);
                string[] paths = spec.Kind == "Search" ? (spec.Paths ?? new string[0]) : spec.Kind == "Everything" ? await EverythingSearch.Search(spec.Query, token).ConfigureAwait(false) : await Task.Run(() => spec.Kind == "Flat" ? ProjectionFiles.Scan(root, token) : spec.Kind == "Tags" ? ProjectionFiles.Tagged(spec.Query) : FileBasket.Load().Paths.ToArray(), token).ConfigureAwait(false);
                var rows = await Task.Run(() => ProjectionFiles.Rows(paths, root, token), token).ConfigureAwait(false);
                if (spec.Kind == "Tags") rows = rows.Where(x => x.IsParent || x.TagNames.Any(tag => tag.IndexOf(spec.Query ?? "", StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
                await Dispatcher.InvokeAsync(delegate
                {
                    if (request != navigationVersion || token.IsCancellationRequested) return;
                    if (IsArchive) CloseArchiveContext(); if (IsRemote) { RememberRemote(); Remote = null; }
                    projection = spec; CurrentPath = root; List.ItemsSource = rows; Sort(); if (rows.Count > 0) List.SelectedIndex = 0; ShowPath(); UpdateStatus();
                    capacity.Text = ProjectionFiles.Title(spec) + " · " + (rows.Count - 1) + L.Text("common.itemCountSuffix"); capacity.ToolTip = spec.Kind == "Basket" ? L.Text("projection.basketHelp") : spec.Query ?? root;
                    UpdateConnectionButtons(); if (PathChanged != null) PathChanged(this, EventArgs.Empty);
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            try
            {
                if (!Dispatcher.HasShutdownStarted) await Dispatcher.InvokeAsync(delegate { if (request == navigationVersion) { LocalLoading = false; List.IsEnabled = true; navigationCancellation = null; if (failure != null) { status.Text = failure.Message; status.ToolTip = failure.Message; } } });
            }
            finally { cancel.Dispose(); }
        }
    }
    public partial class MainWindow
    {
        void ShowFlatView()
        {
            if (active.IsRemote || active.IsArchive || active.IsVirtualBrowser || active.CurrentPath == null) { MessageBox.Show(this, L.Text("projection.localRequired"), ApplicationBrand.Name); return; }
            active.ShowProjection(new ProjectionSpec { Kind = "Flat", Root = active.CurrentPath });
        }
        void ShowBasket() { active.ShowProjection(new ProjectionSpec { Kind = "Basket", Root = active.SavedLocalFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }); }
        async void AddToBasket()
        {
            if (active.IsArchive || active.IsRemote) { MessageBox.Show(this, L.Text("projection.localRequired"), ApplicationBrand.Name); return; }
            var paths = active.Selected().Select(x => x.FullPath).ToArray(); if (paths.Length == 0) return;
            try { await Task.Run(() => FileBasket.Add(paths)); foreach (var panel in new[] { left, right }) if (panel.Projection != null && panel.Projection.Kind == "Basket") panel.Refresh(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("projection.basket")); }
        }
        async void RemoveFromBasket()
        {
            var panel = active; var paths = panel.Selected().Select(x => x.FullPath).ToArray();
            try { await Task.Run(() => FileBasket.Remove(paths)); panel.Refresh(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("projection.basket")); }
        }
        void SearchEverything()
        {
            string query = Ask(L.Text("everything.title"), L.Text("everything.prompt"), ""); if (query == null) return;
            try { query = EverythingSearch.Validate(query); active.ShowProjection(new ProjectionSpec { Kind = "Everything", Query = query, Root = active.SavedLocalFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("everything.title")); }
        }
        void ShowTaggedFiles()
        {
            string query = SettingsUx.Prompt(this,L.Text("tags.results"),"",L.Text("tags.searchHelp")+"\n\n"+L.Text("tags.globalPrompt"),"action.search"); if (query == null) return;
            active.ShowProjection(new ProjectionSpec { Kind = "Tags", Query = query.Trim().TrimStart('#'), Root = active.SavedLocalFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) });
        }
    }
}
