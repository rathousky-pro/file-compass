using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileCompass
{
    public partial class FilePanel
    {
        Task shellTreeInitialization;
        int treeGeneration;
        bool suppressTreeExpansion;

        void EnsureTreeRoot()
        {
            if (CurrentPath == null) return;
            if (IsArchive) {
                treeGeneration++; treeRootKey = null; shellTreeInitialization = null;
                folderTree.Items.Clear(); folderTree.Items.Add(new TreeViewItem { Header = L.Text("tree.archiveUnavailable") }); return;
            }
            if (IsRemote) {
                string key = Remote.Profile.Id + "/";
                if (treeRootKey != key || folderTree.Items.Count == 0) {
                    ResetTree(key); folderTree.Items.Add(FolderNode("/", Remote.Profile.Name + " /"));
                }
                TreeLoadingTask = RevealFolderPath((TreeViewItem)folderTree.Items[0], CurrentPath, "/", ++treeRevealVersion);
                return;
            }
            if (treeRootKey != "windows" || folderTree.Items.Count == 0) {
                ResetTree("windows"); shellTreeInitialization = InitializeExplorerTree(treeGeneration);
            }
            TreeLoadingTask = RevealExplorerPath(CurrentPath, ++treeRevealVersion);
        }
        void ResetTree(string key)
        {
            treeGeneration++; treeRootKey = key; folderTree.Items.Clear(); treeLoads.Clear(); loadedTreeNodes.Clear();
            shellTreeInitialization = null;
        }
        async Task InitializeExplorerTree(int generation)
        {
            folderTree.Items.Add(new TreeViewItem { Header = L.Text("panel.loadingFolder"), IsEnabled = false });
            try {
                var roots = await BoundedShellRead(WindowsShell.Roots());
                if (generation != treeGeneration) return;
                folderTree.Items.Clear();
                foreach (var root in roots) folderTree.Items.Add(ShellFolderNode(root));
            } catch (Exception ex) {
                if (generation != treeGeneration) return;
                folderTree.Items.Clear();
                folderTree.Items.Add(FolderNode(WindowsShell.Home, L.Text("tree.home"), DesktopIcons.StockIcon(4)));
                folderTree.Items.Add(FolderNode(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), L.Text("tree.desktop"), DesktopIcons.StockIcon(34)));
                folderTree.Items.Add(FolderNode(WindowsShell.Computer, L.Text("tree.computer"), DesktopIcons.StockIcon(15)));
                folderTree.Items.Add(FolderNode(NetworkBrowser.Root, L.Text("network.shortTitle"), DesktopIcons.StockIcon(17)));
                folderTree.ToolTip = ex.Message;
            }
        }
        static async Task<ShellLocation[]> BoundedShellRead(Task<ShellLocation[]> task)
        {
            if (await Task.WhenAny(task, Task.Delay(15000)) != task) {
                var observe = task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new IOException(L.Text("tree.locationUnavailable"));
            }
            return await task;
        }
        TreeViewItem ShellFolderNode(ShellLocation item) { return FolderNode(item.Path, item.Name, item.Icon); }
        TreeViewItem FolderNode(string path, string name, ImageSource icon = null)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal };
            heading.Children.Add(new Image { Source = icon ?? DesktopIcons.StockIcon(3), Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true });
            heading.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            var node = new TreeViewItem { Header = heading, Tag = path, ToolTip = path, Padding = new Thickness(2, 3, 2, 3) };
            System.Windows.Automation.AutomationProperties.SetName(node, name);
            node.Items.Add(new TreeViewItem { Header = L.Text("action.loadMore"), IsEnabled = false });
            node.Expanded += async delegate(object sender, RoutedEventArgs e) { if (e.OriginalSource == node && !suppressTreeExpansion) await LoadTreeNode(node); };
            return node;
        }
        Task LoadTreeNode(TreeViewItem node)
        {
            if (loadedTreeNodes.Contains(node)) return Task.FromResult(0);
            Task task; if (treeLoads.TryGetValue(node, out task)) return task;
            task = ReadTreeNode(node); treeLoads[node] = task; return task;
        }
        async Task ReadTreeNode(TreeViewItem node)
        {
            int generation = treeGeneration; string path = (string)node.Tag;
            RemoteConnection worker = null;
            try {
                ShellLocation[] names;
                if (IsRemote) {
                    worker = Remote.TransferConnection();
                    names = await Task.Run(delegate {
                        worker.OpenTransfer();
                        return worker.Session.ListDirectory(path).Files.Where(x => x.IsDirectory && !x.IsParentDirectory && !x.IsThisDirectory && x.FileType != 'L' && (showHiddenFiles || !x.Name.StartsWith(".", StringComparison.Ordinal)))
                            .Select(x => new ShellLocation { Path = x.FullName, Name = x.Name, Directory = true, Icon = DesktopIcons.StockIcon(3) }).ToArray();
                    });
                } else if (NetworkBrowser.IsLocation(path)) {
                    var read = NetworkBrowser.Read(path);
                    if (await Task.WhenAny(read, Task.Delay(15000)) != read) {
                        var observe = read.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        throw new IOException(L.Text("tree.locationUnavailable"));
                    }
                    var listed=await read; if(path==NetworkBrowser.Root&&NetworkBrowser.TestProvider==null){var deadline=DateTime.UtcNow.AddSeconds(35);while(NetworkBrowser.DiscoveryRunning&&DateTime.UtcNow<deadline)await Task.Delay(350);listed=NetworkBrowser.Snapshot();}
                    names = listed.Where(x => !x.IsParent && x.NetworkDeviceTarget==null && x.NetworkServiceProfile==null).Select(x => new ShellLocation { Path = x.FullPath, Name = x.Name, Directory = true, Icon = DesktopIcons.StockIcon(NetworkBrowser.IsServer(x.FullPath) ? 15 : 4) }).ToArray();
                } else {
                    names = await BoundedShellRead(WindowsShell.IsLocation(path) ? WindowsShell.Read(path, true) : WindowsShell.Folders(path, showHiddenFiles, showSystemFiles));
                }
                if (generation != treeGeneration) return;
                UpdateTreeChildren(node, names.Where(x => x.Directory && (showHiddenFiles || !x.Hidden) && (showSystemFiles || !x.System)).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase), NetworkBrowser.IsLocation(path));
                loadedTreeNodes.Add(node);
            } catch (Exception ex) {
                if (generation == treeGeneration) {
                    var connected = node.Items.OfType<TreeViewItem>().Where(x => x.Tag is string).ToArray();
                    bool previous = revealingTree; revealingTree = true;
                    try {
                        foreach (var item in node.Items.OfType<TreeViewItem>().Where(x => x.Tag == null).ToArray()) node.Items.Remove(item);
                        node.Items.Add(new TreeViewItem { Header = L.Text("error.loadFailed"), ToolTip = ex.Message, IsEnabled = false });
                    } finally { revealingTree = previous; }
                }
            } finally { treeLoads.Remove(node); if (worker != null) worker.Dispose(); }
        }
        void UpdateTreeChildren(TreeViewItem parent, IEnumerable<ShellLocation> locations, bool retainConnected)
        {
            var existing = parent.Items.OfType<TreeViewItem>().Where(x => x.Tag is string).ToArray();
            var desired = locations.Select(x => existing.FirstOrDefault(n => IsRemote ? String.Equals((string)n.Tag, x.Path, StringComparison.Ordinal) : WindowsShell.SamePath((string)n.Tag, x.Path)) ?? ShellFolderNode(x)).ToList();
            if (retainConnected) foreach (var node in existing) if (!desired.Any(x => WindowsShell.SamePath((string)x.Tag, (string)node.Tag))) desired.Add(node);
            var selected = folderTree.SelectedItem as TreeViewItem;
            bool previous = revealingTree; revealingTree = true;
            try {
                foreach (var item in parent.Items.Cast<object>().Where(x => !desired.Contains(x as TreeViewItem)).ToArray()) parent.Items.Remove(item);
                for (int i = 0; i < desired.Count; i++) {
                    var node = desired[i];
                    if (i < parent.Items.Count && ReferenceEquals(parent.Items[i], node)) continue;
                    if (parent.Items.Contains(node)) parent.Items.Remove(node);
                    parent.Items.Insert(i, node);
                }
                if (selected != null) {
                    ItemsControl ancestor = selected;
                    while (ancestor != null && ancestor != folderTree) ancestor = ItemsControl.ItemsControlFromItemContainer(ancestor);
                    if (ancestor == folderTree) selected.IsSelected = true;
                }
            } finally { revealingTree = previous; }
        }

        async Task RevealExplorerPath(string path, int request)
        {
            if (shellTreeInitialization != null) await shellTreeInitialization;
            if (request != treeRevealVersion || !treeVisible || treeRootKey != "windows") return;
            var roots = folderTree.Items.OfType<TreeViewItem>().Where(x => x.Tag is string).ToArray();
            var exact = roots.FirstOrDefault(x => WindowsShell.SamePath((string)x.Tag, path));
            if (exact != null) { SelectTreeNode(exact, !WindowsShell.IsLocation(path) && !NetworkBrowser.IsLocation(path)); return; }
            if (WindowsShell.IsLocation(path)) return;
            string rootPath = Path.GetPathRoot(path);
            TreeViewItem node;
            if (path.StartsWith("\\\\", StringComparison.Ordinal)) {
                var network = roots.FirstOrDefault(x => (string)x.Tag == NetworkBrowser.Root); if (network == null) return;
                string serverPath = "\\\\" + path.Trim('\\').Split('\\')[0];
                var server = FindTreeChild(network, serverPath);
                if (server == null) { server = FolderNode(serverPath, serverPath.Trim('\\'), DesktopIcons.StockIcon(15)); network.Items.Add(server); }
                ExpandTreeWithoutLoad(network);
                if (NetworkBrowser.IsServer(path)) { SelectTreeNode(server, false); return; }
                node = FindTreeChild(server, rootPath);
                if (node == null) { node = FolderNode(rootPath, rootPath.TrimEnd('\\').Substring(serverPath.Length).Trim('\\'), DesktopIcons.StockIcon(4)); server.Items.Add(node); }
                ExpandTreeWithoutLoad(server);
            } else {
                node = roots.Where(x => !WindowsShell.IsLocation((string)x.Tag) && !NetworkBrowser.IsLocation((string)x.Tag) && WindowsShell.IsWithin(path, (string)x.Tag))
                    .OrderByDescending(x => ((string)x.Tag).Length).FirstOrDefault();
                if (node != null) rootPath = (string)node.Tag;
                else {
                    var computer = roots.FirstOrDefault(x => (string)x.Tag == WindowsShell.Computer); if (computer == null) return;
                    await LoadTreeNode(computer);
                    if (request != treeRevealVersion || !treeVisible) return;
                    ExpandTreeWithoutLoad(computer); node = FindTreeChild(computer, rootPath);
                    if (node == null) { node = FolderNode(rootPath, rootPath, DesktopIcons.WindowsDriveIcon(rootPath, false, true)); computer.Items.Add(node); }
                }
            }
            await RevealFolderPath(node, path, rootPath, request);
        }
        static TreeViewItem FindTreeChild(TreeViewItem parent, string path) { return parent.Items.OfType<TreeViewItem>().FirstOrDefault(x => x.Tag is string && WindowsShell.SamePath((string)x.Tag, path)); }
        void ExpandTreeWithoutLoad(TreeViewItem node) { suppressTreeExpansion = true; try { node.IsExpanded = true; } finally { suppressTreeExpansion = false; } }
        void SelectTreeNode(TreeViewItem node, bool expand)
        {
            revealingTree = true;
            try { node.IsSelected = true; if (expand) node.IsExpanded = true; node.BringIntoView(); }
            finally { revealingTree = false; }
        }
        async Task RevealFolderPath(TreeViewItem node, string path, string root, int request)
        {
            if (node == null) return;
            string current = root;
            foreach (string part in path.Substring(root.Length).Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)) {
                await LoadTreeNode(node);
                if (request != treeRevealVersion || !treeVisible) return;
                ExpandTreeWithoutLoad(node); current = current.TrimEnd('/', '\\') + (IsRemote ? "/" : "\\") + part;
                var child = node.Items.OfType<TreeViewItem>().FirstOrDefault(x => x.Tag is string && String.Equals(((string)x.Tag).TrimEnd('\\'), current.TrimEnd('\\'), IsRemote ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
                if (child == null) {
                    loadedTreeNodes.Remove(node); await LoadTreeNode(node);
                    if (request != treeRevealVersion || !treeVisible) return;
                    child = node.Items.OfType<TreeViewItem>().FirstOrDefault(x => x.Tag is string && (IsRemote ? String.Equals((string)x.Tag, current, StringComparison.Ordinal) : WindowsShell.SamePath((string)x.Tag, current)));
                }
                if (child == null) break; node = child;
            }
            if (request != treeRevealVersion || !treeVisible) return;
            SelectTreeNode(node, true);
        }
    }
}
