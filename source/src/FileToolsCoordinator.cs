using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        int fileToolsRunning;
        void FindFiles()
        {
            var panel = active; var archive = panel.Archive; var remote = panel.Remote;
            string root = panel.CurrentPath; string[] selected = panel.Selected().Select(x => x.FullPath).ToArray();
            ArchiveItem[] members = archive == null ? null : archive.Items.ToArray();
            RemoteConnection login = remote == null ? null : remote.TransferConnection();
            bool local = archive == null && remote == null;
            var window = new FileSearchWindow(this, root, local, selected.Length > 0,
                delegate(FileSearchOptions options, CancellationToken token, Action<SearchHit> found, Action<string> warning)
                {
                    token.ThrowIfCancellationRequested();
                    if (local) return FileSearchEngine.Local(options, selected, token, found, warning); if(MetadataSearch.Enabled(options))throw new IOException(L.Text("metaSearch.localOnly"));
                    if (login != null) { using (var connection = login.TransferConnection()) using (token.Register(delegate { connection.Session.Abort(); })) { token.ThrowIfCancellationRequested(); connection.OpenTransfer(); return FileSearchEngine.Remote(options, selected, connection, token, found); } }
                    if (SearchAdvanced.Roots(options.Root).Length != 1 || options.Root.Trim() != root) throw new IOException(L.Text("search.archiveRootHelp"));
                    int count = 0; string prefix = root.Trim('/') + "/"; if (prefix == "/") prefix = "";
                    foreach (var item in members)
                    {
                        token.ThrowIfCancellationRequested(); if (count >= 20000) break;
                        if (!item.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        string relative = item.Path.Substring(prefix.Length);
                        if (relative.Count(c => c == '/') > options.Depth) continue;
                        if (options.SelectedOnly && !selected.Any(x => item.Path == x.Trim('/') || item.Path.StartsWith(x.Trim('/') + "/", StringComparison.OrdinalIgnoreCase))) continue;
                        if (FileSearchEngine.Matches(Path.GetFileName(item.Path), options) && SearchAdvanced.Metadata(options,item.Modified.ToUniversalTime(),0) && (item.Directory || (item.Bytes >= options.Minimum && item.Bytes <= options.Maximum)))
                        { found(new SearchHit { Path = "/" + item.Path, Name = Path.GetFileName(item.Path), Bytes = item.Bytes, Directory = item.Directory }); count++; }
                    }
                    return count;
                }, delegate(SearchHit hit)
                {
                    if (remote != null && panel.Remote != remote) { MessageBox.Show(this, L.Text("search.reopenOriginalConnection"), L.Text("search.resultTitle")); return; }
                    if (archive != null && panel.Archive != archive) { MessageBox.Show(this, L.Text("search.reopenOriginalArchive"), L.Text("search.resultTitle")); return; }
                    string directory = hit.Directory ? hit.Path : local ? Path.GetDirectoryName(hit.Path) : RemoteConnection.Parent(hit.Path);
                    panel.Navigate(directory, true); SetActive(panel); panel.List.Focus();
                    if (!panel.IsLoading) panel.List.SelectedItem = panel.List.Items.Cast<Entry>().FirstOrDefault(x => x.FullPath.Equals(hit.Path, StringComparison.OrdinalIgnoreCase)) ?? panel.List.SelectedItem;
                },
                delegate(string[] paths, string queryLabel)
                {
                    if (!local || paths == null || paths.Length == 0) return;
                    string baseRoot = SearchAdvanced.Roots(root).FirstOrDefault() ?? (Directory.Exists(root) ? root : Path.GetDirectoryName(paths[0]) ?? "C:\\");
                    panel.ShowProjection(new ProjectionSpec { Kind = "Search", Root = baseRoot, Query = queryLabel, Paths = paths });
                    SetActive(panel); panel.List.Focus();
                },
                delegate(string leftFile, string rightFile)
                {
                    if (File.Exists(leftFile) && File.Exists(rightFile)) new FileDiffWindow(this, leftFile, rightFile).Show();
                });
            if (login != null) window.Closed += delegate { login.Dispose(); }; window.Show();
        }
        void MultiRename()
        {
            if (active.IsArchive || active.IsRemote) { MessageBox.Show(this, L.Text("rename.supportedLocationsHelp"), L.Text("rename.batchTitle")); return; }
            string[] paths = active.Selected().Select(x => x.FullPath).ToArray(); if (paths.Length == 0) return;
            var parents = paths.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            new BatchRenameWindow(this, paths, delegate { foreach (var panel in new[] { left, right }) if (!panel.IsRemote && !panel.IsArchive && parents.Contains(panel.CurrentPath, StringComparer.OrdinalIgnoreCase)) panel.Refresh(); }, delegate(bool running) { fileToolsRunning += running ? 1 : -1; }).Show();
        }
    }
}

