using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal static class PriorityWorkflowTests
    {
        static void Check(bool valid, string message, List<string> checks) { if (!valid) throw new Exception(message); checks.Add(message); }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); string oldRoot = WorkflowStore.TestRoot, oldTags = FileTagStore.TestPath; var checks = new List<string>(); MainWindow main = null;
            try
            {
                WorkflowStore.TestRoot = Path.Combine(root, "profile"); FileTagStore.TestPath = Path.Combine(root, "tags.xml");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)); FluentTheme.Ensure();
                string folder = Directory.CreateDirectory(Path.Combine(root, "files")).FullName, nested = Directory.CreateDirectory(Path.Combine(root, "files", "nested")).FullName;
                string first = Path.Combine(folder, "one.txt"), second = Path.Combine(nested, "two.txt"); File.WriteAllText(first, "one"); File.WriteAllText(second, "two");
                FileBasket.Add(new[] { first, second, first }); Check(FileBasket.Load().Paths.Count == 2, "basket persists references without duplicates", checks);
                Check(File.ReadAllText(first) == "one" && File.ReadAllText(second) == "two", "adding to basket never copies or modifies source bytes", checks);
                FileBasket.Remove(new[] { first }); Check(File.Exists(first) && FileBasket.Load().Paths.Count == 1, "removing from basket keeps the original file", checks);
                string renamed = Path.Combine(nested, "renamed.txt"); BatchRenameEngine.Apply(new[] { new RenameRow { Path = second, OldName = "two.txt", NewName = "renamed.txt" } }, CancellationToken.None);
                Check(FileBasket.Load().Paths.Single() == renamed, "basket follows a real rename", checks);
                var paths = ProjectionFiles.Scan(folder, CancellationToken.None); Check(paths.Length == 2 && paths.Contains(first) && paths.Contains(renamed), "flat view includes nested files with original full paths", checks);
                var rows = ProjectionFiles.Rows(paths, folder, CancellationToken.None); Check(rows.Any(x => x.FullPath == renamed && x.LocationName.Contains("nested")), "flat rows expose their containing folder", checks);
                var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool stopped = false; try { ProjectionFiles.Scan(folder, cancelled.Token); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "recursive scans honor cancellation", checks);
                main = new MainWindow(folder, new ProgramSettings { AutoRefresh = false }) { ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main);
                var panels = FluentUiTests.Children<FilePanel>(main).ToArray(); var left = panels[0]; var right = panels[1];
                left.ShowProjection(new ProjectionSpec { Kind = "Flat", Root = folder }); NavigationTests.Wait(left.NavigationTask);
                Check(left.IsProjection && left.List.Items.Cast<Entry>().Any(x => x.FullPath == renamed), "flat listing opens in an actual panel tab", checks);
                left.Navigate(folder, false); NavigationTests.Wait(left.NavigationTask); Check(!left.IsProjection, "normal navigation exits projection mode", checks);
                left.ShowProjection(new ProjectionSpec { Kind = "Basket", Root = folder }); NavigationTests.Wait(left.NavigationTask);
                Check(left.Projection.Kind == "Basket" && left.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 1, "basket opens as a reusable panel tab", checks);
                var specTabs = left.SaveTabs(); left.Navigate(folder, false); NavigationTests.Wait(left.NavigationTask); left.RestoreTabs(specTabs, specTabs.Length - 1); NavigationTests.Wait(left.NavigationTask);
                Check(left.IsProjection && left.Projection.Kind == "Basket", "saved tabs restore their projection type", checks);
                left.Navigate(nested, false); right.Navigate(folder, false); NavigationTests.WaitPanels(main); left.SetQuickFilter("*.txt");
                var workspace = main.CaptureWorkspace("Fotografie"); WorkflowStore.Save("workspaces.xml", new WorkspaceData { Items = new List<Workspace> { workspace } });
                var reloaded = WorkflowStore.Load<WorkspaceData>("workspaces.xml").Items.Single();
                left.Navigate(folder, false); right.Navigate(nested, false); NavigationTests.WaitPanels(main); left.SetQuickFilter(""); NavigationTests.Wait(main.ApplyWorkspace(reloaded));
                Check(left.CurrentPath == nested && right.CurrentPath == folder && left.QuickFilter == "*.txt", "workspace restores both folders and independent active filters", checks);
                Check(left.TabCount == workspace.Left.Length && right.TabCount == workspace.Right.Length, "workspace restores both complete tab sets", checks);
                var huge = Enumerable.Range(0, 20000).Select(i => new Entry { Name = "file" + i, Extension = "txt", FullPath = Path.Combine(folder, "file" + i + ".txt"), Icon = DesktopIcons.Drawing("document") }).ToList();
                left.SetQuickFilter(""); left.List.ItemsSource = huge; left.SetDisplay("Thumbnails"); main.UpdateLayout(); NavigationTests.Wait(Task.Delay(100)); main.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
                var tiles = FilePanel.VisualChild<VirtualFileTiles>(left.List); int count = tiles == null ? 0 : VisualTreeHelper.GetChildrenCount(tiles);
                Check(count > 0 && count < 200, "20,000 thumbnails create only viewport containers", checks);
                Check(left.LastThumbnailScanCount < 200 && left.LastThumbnailScanCount > 0, "thumbnail scheduling scans only realized rows instead of 20,000 items", checks);
                left.SetDisplay("Details"); left.Navigate(folder, false); NavigationTests.Wait(left.NavigationTask);
                bool invalid = false; try { EverythingSearch.Validate("x\n--test"); } catch (ArgumentException) { invalid = true; } Check(invalid, "Everything rejects malformed multiline queries", checks);
                var watch = Stopwatch.StartNew();
                try { var search = EverythingSearch.Search("__FileVoyager_nonexistent_test_" + Guid.NewGuid().ToString("N"), CancellationToken.None); NavigationTests.Wait(search); Check(search.Result.Length == 0, "Everything real IPC returns an empty result for a unique absent name", checks); }
                catch (IOException ex) { Check(ex.Message.Contains(L.Text("everything.notRunning")) || ex.Message.Contains(L.Text("everything.timeout")), "missing or unavailable Everything reports a bounded error and native search remains available", checks); }
                Check(watch.Elapsed < TimeSpan.FromSeconds(20), "Everything helper cannot hang the UI indefinitely", checks);
                NetworkTests.Render(main, Path.Combine(root, "priority-workflows.png")); main.Close(); main = null;
                File.WriteAllLines(Path.Combine(root, "priority-workflow-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "priority-workflow-result.txt"), "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
            finally { if (main != null) main.Close(); WorkflowStore.TestRoot = oldRoot; FileTagStore.TestPath = oldTags; }
        }
    }
}

