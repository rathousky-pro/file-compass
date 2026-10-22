using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CommanderCZ
{
    internal static class ExplorerTreeTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) {
                    Width = 1380, Height = 880, ShowActivated = false, ShowInTaskbar = false,
                    Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual
                };
                main.Show(); NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.VerifyExplorerTree(root, delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); }));
                File.WriteAllLines(Path.Combine(root, "explorer-tree-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) {
                File.WriteAllLines(Path.Combine(root, "explorer-tree-result.txt"), new[] { "FAIL after " + checks.Count + " checks" }.Concat(checks).Concat(new[] { ex.ToString() })); return 1;
            } finally { NetworkBrowser.TestProvider = null; if (main != null) main.Close(); }
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyExplorerTree(string root, Action<bool, string> check)
        {
            SetPanelLayout("Single"); if (left.TreeLoadingTask != null) await left.TreeLoadingTask;
            await left.VerifyExplorerTree(root, check);
            UpdateLayout(); NextStageTests.Draw(this, Path.Combine(root, "explorer-tree.png"));
            SetPanelLayout("SideBySide"); UpdateLayout();
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible && !FluentUiTests.Children<TreeView>(right).Single().IsVisible, "Explorer navigation remains exclusive to single-panel mode");
            SetPanelLayout("Stacked"); UpdateLayout();
            check(!FluentUiTests.Children<TreeView>(left).Single().IsVisible, "stacked panels do not reserve space for the tree");
            SetPanelLayout("Single"); if (left.TreeLoadingTask != null) await left.TreeLoadingTask;
            check(FluentUiTests.Children<TreeView>(left).Single().IsVisible, "returning to one panel restores Explorer navigation");
        }
    }
    public partial class FilePanel
    {
        internal async Task VerifyExplorerTree(string root, Action<bool, string> check)
        {
            var roots = folderTree.Items.OfType<TreeViewItem>().Where(x => x.Tag is string).ToArray();
            var computer = roots.Single(x => (string)x.Tag == WindowsShell.Computer);
            var home = roots.Single(x => (string)x.Tag == WindowsShell.Home);
            var network = roots.Single(x => (string)x.Tag == NetworkBrowser.Root);
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var desktop = roots.Single(x => WindowsShell.SamePath((string)x.Tag, desktopPath));
            check(roots.Length >= 4 && home != null && desktop != null && computer != null && network != null, "tree has Home, Desktop, This PC and Network roots");
            check(roots.All(x => x.Header is StackPanel && ((StackPanel)x.Header).Children.OfType<Image>().Single().Source != null), "every namespace root has an icon and a text label");
            check(roots.All(x => ((StackPanel)x.Header).Children.OfType<Image>().Single().Source.IsFrozen), "system icons are frozen before crossing from the shell worker to WPF");
            File.WriteAllLines(Path.Combine(root, "shell-roots.txt"), roots.Select(x => System.Windows.Automation.AutomationProperties.GetName(x) + "	" + x.Tag));
            await LoadTreeNode(computer);
            var drives = computer.Items.OfType<TreeViewItem>().Where(x => x.Tag is string).ToArray();
            string currentDrive = Path.GetPathRoot(root);
            File.WriteAllLines(Path.Combine(root, "computer-branches.txt"), computer.Items.OfType<TreeViewItem>().Select(x => (x.Tag ?? x.Header) + " " + x.ToolTip));
            check(drives.Any(x => WindowsShell.SamePath((string)x.Tag, currentDrive)), "This PC expands to real drives including the current disk");
            check(drives.All(x => x.Header is StackPanel && ((StackPanel)x.Header).Children.OfType<Image>().Single().Source != null), "drive branches carry Windows shell icons");
            computer.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(IsShellBrowser && CurrentPath == WindowsShell.Computer && List.Items.Cast<Entry>().Any(x => WindowsShell.SamePath(x.FullPath, currentDrive)), "clicking This PC displays its drives inside the file panel");
            check(Selected().Count == 0, "system overview entries are navigation targets rather than filesystem operation inputs");
            check(ReferenceEquals(computer, folderTree.Items.OfType<TreeViewItem>().Single(x => (x.Tag as string) == WindowsShell.Computer)), "virtual navigation preserves the namespace tree instead of rebuilding it");
            var rootDrive = List.Items.Cast<Entry>().Single(x => WindowsShell.SamePath(x.FullPath, currentDrive)); List.SelectedItem = rootDrive;
            OpenSelected(); await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(!IsShellBrowser && WindowsShell.SamePath(CurrentPath, currentDrive), "opening a drive from This PC enters its real directory");
            desktop.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(WindowsShell.SamePath(CurrentPath, desktopPath), "Desktop root opens the actual redirected Windows desktop");
            home.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(IsShellBrowser && CurrentPath == WindowsShell.Home && List.Items.Cast<Entry>().Any(x => x.IsDirectory), "Home opens Windows quick-access folders inside the panel");

            string nested = Directory.CreateDirectory(Path.Combine(root, "Folders", "Nested")).FullName;
            Navigate(nested, true); await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(folderTree.SelectedItem is TreeViewItem && WindowsShell.SamePath((string)((TreeViewItem)folderTree.SelectedItem).Tag, nested), "tree follows a nested path through the appropriate system root");
            check(ReferenceEquals(computer, folderTree.Items.OfType<TreeViewItem>().Single(x => (x.Tag as string) == WindowsShell.Computer)), "ordinary folder navigation keeps namespace roots and expanded branches");
            var selectedNode = (TreeViewItem)folderTree.SelectedItem;
            var parentNode = ItemsControl.ItemsControlFromItemContainer(selectedNode) as TreeViewItem;
            if (parentNode != null) { loadedTreeNodes.Remove(parentNode); await LoadTreeNode(parentNode); }
            await Task.Delay(80);
            check(CurrentPath == nested && !IsLoading && folderTree.SelectedItem == selectedNode, "refreshing a branch preserves selection without starting unintended navigation");
            string hidden = Directory.CreateDirectory(Path.Combine(root, "Folders", "Hidden")).FullName;
            File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
            var filtered = await BoundedShellRead(WindowsShell.Folders(Path.GetDirectoryName(nested), false, true));
            var unfiltered = await BoundedShellRead(WindowsShell.Folders(Path.GetDirectoryName(nested), true, true));
            check(!filtered.Any(x => x.Path == hidden) && unfiltered.Any(x => x.Path == hidden), "tree directory enumeration respects the hidden-files switch");
            var errorNode = FolderNode(Path.Combine(root, "does-not-exist"), "Missing");
            await LoadTreeNode(errorNode);
            check(errorNode.Items.OfType<TreeViewItem>().Any(x => x.Tag == null && !x.IsEnabled) && !loadedTreeNodes.Contains(errorNode), "unavailable branches show an error and remain retryable");

            NetworkBrowser.TestProvider = path => new List<Entry> {
                new Entry { Name = path == NetworkBrowser.Root ? "Tree test server" : "Data", FullPath = path == NetworkBrowser.Root ? @"\\tree-test-server" : @"\\tree-test-server\Data", IsDirectory = true, Icon = DesktopIcons.StockIcon(17) }
            };
            await LoadTreeNode(network);
            check(network.Items.OfType<TreeViewItem>().Any(x => (x.Tag as string) == @"\\tree-test-server"), "Network expands to discovered Windows servers");
            network.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            check(IsNetworkBrowser && folderTree.Items.OfType<TreeViewItem>().Any(x => (x.Tag as string) == WindowsShell.Computer), "opening Network retains Desktop and This PC in the tree");
            var server = network.Items.OfType<TreeViewItem>().Single(x => (x.Tag as string) == @"\\tree-test-server");
            server.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask; await LoadTreeNode(server);
            check(CurrentPath == @"\\tree-test-server" && server.Items.OfType<TreeViewItem>().Any(x => (x.Tag as string) == @"\\tree-test-server\Data"), "a network server opens and expands to shared folders");
            NetworkBrowser.TestProvider = null;
            Navigate(nested, true); await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            // Finish navigation before rendering, so cleanup cannot race a loading panel.
            computer.IsSelected = true; await NavigationTask; if (TreeLoadingTask != null) await TreeLoadingTask;
            await LoadTreeNode(computer); ExpandTreeWithoutLoad(computer);
            revealingTree = true;
            try {
                foreach (var item in roots.Where(x => x != computer)) item.IsExpanded = false;
                foreach (var item in computer.Items.OfType<TreeViewItem>()) item.IsExpanded = false;
            } finally { revealingTree = false; }
            folderTree.UpdateLayout();
            var scroll = FluentUiTests.Children<ScrollViewer>(folderTree).FirstOrDefault();
            if (scroll != null) scroll.ScrollToTop();
            check(roots.All(x => ((StackPanel)x.Header).Children.OfType<TextBlock>().Single().Text.Length > 0), "namespace labels come from Windows rather than empty placeholders");
        }
    }
}
