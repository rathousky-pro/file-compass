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
        int treeGeneration;
        bool suppressTreeExpansion;

        void EnsureTreeRoot()
        {
            string path = CurrentPath;
            if (String.IsNullOrEmpty(path))
            {
                ResetTree(null);
                TreeLoadingTask = Task.CompletedTask;
                return;
            }

            string key = (IsArchive ? "archive:" : IsRemote ? Remote.Profile.Id + ":" : "folder:") + path;
            bool sameRoot = String.Equals(treeRootKey, key, IsRemote ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)
                && folderTree.Items.Count > 0;
            if (IsArchive)
            {
                if (!sameRoot)
                {
                    ResetTree(key);
                    folderTree.Items.Add(new TreeViewItem { Header = L.Text("tree.archiveUnavailable"), IsEnabled = false });
                }
                TreeLoadingTask = Task.CompletedTask;
                return;
            }

            TreeViewItem root;
            if (sameRoot)
            {
                root = (TreeViewItem)folderTree.Items[0];
                loadedTreeNodes.Remove(root);
            }
            else
            {
                ResetTree(key);
                root = FolderNode(path, CurrentTreeFolderName(path));
                folderTree.Items.Add(root);
                suppressTreeExpansion = true;
                revealingTree = true;
                try
                {
                    root.IsSelected = true;
                    root.IsExpanded = true;
                }
                finally
                {
                    revealingTree = false;
                    suppressTreeExpansion = false;
                }
                root.BringIntoView();
            }
            TreeLoadingTask = LoadTreeNode(root);
        }

        string CurrentTreeFolderName(string path)
        {
            if (IsShellBrowser && !String.IsNullOrEmpty(ShellLocationName)) return ShellLocationName;
            if (path == NetworkBrowser.Root) return L.Text("network.shortTitle");
            if (IsRemote && path == "/") return Remote.Profile.DisplayLabel;
            var pieces = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return pieces.Length == 0 || path.Length <= 3 ? path : pieces[pieces.Length - 1];
        }

        void ResetTree(string key)
        {
            treeGeneration++;
            treeRootKey = key;
            folderTree.Items.Clear();
            treeLoads.Clear();
            loadedTreeNodes.Clear();
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
            task = ReadTreeNode(node); if (!task.IsCompleted) treeLoads[node] = task; return task;
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

    }
}
