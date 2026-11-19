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

namespace FileCompass
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
            check(left.FolderTreeVisible && !right.FolderTreeVisible && right.IsVisible, "the left tree stays local in side-by-side mode");
            SetActive(right);
            toolbarFolderViewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var viewMenu = toolbarFolderViewButton.ContextMenu;
            var tree = viewMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, L.Text("panel.folderTree")));
            viewMenu.IsOpen = false;
            tree.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            if (right.TreeLoadingTask != null) await right.TreeLoadingTask;
            check(panelLayout == "SideBySide" && left.FolderTreeVisible && right.FolderTreeVisible &&
                left.IsVisible && right.IsVisible, "the toolbar toggles only the right tree and preserves the two-panel layout");
            left.ToggleTree();
            SetPanelLayout("Stacked"); UpdateLayout();
            check(!left.FolderTreeVisible && right.FolderTreeVisible, "stacking preserves each panel's tree preference");
            SetActive(left);
            SetPanelLayout("Single"); if (left.TreeLoadingTask != null) await left.TreeLoadingTask;
            check(FluentUiTests.Children<TreeView>(left).Single(tree => System.Windows.Automation.AutomationProperties.GetName(tree) != L.Text("networkScan.tree")).IsVisible, "returning to one panel restores Explorer navigation");
        }
    }
    public partial class FilePanel
    {
        internal async Task VerifyExplorerTree(string root, Action<bool, string> check)
        {
            string current = Directory.CreateDirectory(Path.Combine(root, "Current")).FullName;
            string child = Directory.CreateDirectory(Path.Combine(current, "Child")).FullName;
            string grandchild = Directory.CreateDirectory(Path.Combine(child, "Grandchild")).FullName;
            Directory.CreateDirectory(Path.Combine(root, "Sibling"));
            File.WriteAllText(Path.Combine(current, "file.txt"), "file");

            Navigate(current, true);
            await NavigationTask;
            await TreeLoadingTask;
            var currentNode = folderTree.Items.OfType<TreeViewItem>().Single();
            check(WindowsShell.SamePath((string)currentNode.Tag, current) && currentNode.IsExpanded && currentNode.IsSelected,
                "the only expanded tree root is the current folder");
            var childNode = currentNode.Items.OfType<TreeViewItem>().Single();
            check(WindowsShell.SamePath((string)childNode.Tag, child), "the tree lists subfolders and omits files, siblings and ancestors");
            await LoadTreeNode(childNode);
            check(childNode.Items.OfType<TreeViewItem>().Any(node => WindowsShell.SamePath(node.Tag as string, grandchild)),
                "deeper descendants load on demand");

            string added = Directory.CreateDirectory(Path.Combine(current, "New")).FullName;
            Refresh();
            await NavigationTask;
            await TreeLoadingTask;
            check(ReferenceEquals(currentNode, folderTree.Items[0]) &&
                currentNode.Items.OfType<TreeViewItem>().Any(node => WindowsShell.SamePath(node.Tag as string, added)),
                "refreshing the current folder preserves its root and updates its child listing");

            childNode.IsSelected = true;
            await NavigationTask;
            await TreeLoadingTask;
            check(WindowsShell.SamePath(CurrentPath, child) &&
                WindowsShell.SamePath((string)folderTree.Items.OfType<TreeViewItem>().Single().Tag, child),
                "selecting a child navigates and re-roots the tree");
            Up();
            await NavigationTask;
            await TreeLoadingTask;
            check(WindowsShell.SamePath((string)folderTree.Items.OfType<TreeViewItem>().Single().Tag, current),
                "navigating upwards roots the tree at the new current folder");

            NetworkBrowser.TestProvider = path => new List<Entry> {
                new Entry { Name = "Tree test server", FullPath = @"\\tree-test-server", IsDirectory = true, Icon = DesktopIcons.StockIcon(17) }
            };
            Navigate(NetworkBrowser.Root, true);
            await NavigationTask;
            await TreeLoadingTask;
            var network = folderTree.Items.OfType<TreeViewItem>().Single();
            check((string)network.Tag == NetworkBrowser.Root && network.Items.OfType<TreeViewItem>().Any(node => node.Tag as string == @"\\tree-test-server"),
                "the network location also stays scoped to the current root");
            NetworkBrowser.TestProvider = null;
            Navigate(current, true);
            await NavigationTask;
            await TreeLoadingTask;
        }
    }
}
