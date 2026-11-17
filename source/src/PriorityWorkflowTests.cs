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
        static void VerifyPanelComparison(MainWindow main, FilePanel left, FilePanel right, string folder, string root, List<string> checks)
        {
            string target = Directory.CreateDirectory(Path.Combine(root, "comparison")).FullName;
            File.WriteAllText(Path.Combine(target, "one.txt"), "different size");
            File.WriteAllText(Path.Combine(target, "right.txt"), "right only");
            right.Navigate(target, false);
            NavigationTests.Wait(right.NavigationTask);

            main.CompareOpenPanels();
            var leftRows = left.PanelComparisonRows();
            var rightRows = right.PanelComparisonRows();
            Check(leftRows.Any(x => x.FullName == "one.txt" && x.Difference == PanelDifference.Different) &&
                leftRows.Any(x => x.FullName == "nested" && x.Difference == PanelDifference.LeftOnly) &&
                rightRows.Any(x => x.FullName == "right.txt" && x.Difference == PanelDifference.RightOnly),
                "panel comparison marks size differences and items unique to each side", checks);
            var filter = FluentUiTests.Children<ComboBox>(left).Single(combo =>
                combo.Items.OfType<ComboBoxItem>().Any(item => Equals(item.Tag, PanelDifference.Different)));
            filter.SelectedIndex = 3;
            Check(left.List.Items.Cast<Entry>().Where(row => !row.IsParent).Single().FullName == "one.txt",
                "comparison filter updates the actual list", checks);
            var mark = FluentUiTests.Children<Button>(left).Single(button => Equals(button.Content, L.Text("panelCompare.mark")));
            mark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(left.Selected().Single().FullName == "one.txt", "comparison marks feed the normal file selection", checks);

            right.Navigate(folder, false);
            NavigationTests.Wait(right.NavigationTask);
            Check(leftRows.All(x => x.Difference == PanelDifference.None) && rightRows.All(x => x.Difference == PanelDifference.None),
                "changing either folder invalidates the comparison on both sides", checks);
        }

        static void VerifyTabComparison(MainWindow main, FilePanel left, FilePanel right, string root, List<string> checks)
        {
            string a = Directory.CreateDirectory(Path.Combine(root, "tab-first")).FullName;
            string b = Directory.CreateDirectory(Path.Combine(root, "tab-second")).FullName;
            var stamp = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            Action<string,string,string,DateTime> put = (folder,name,body,modified) =>
            {
                string path = Path.Combine(folder,name);
                File.WriteAllText(path,body);
                File.SetLastWriteTimeUtc(path,modified);
            };
            put(a,"size.txt","a",stamp); put(b,"size.txt","long",stamp);
            put(a,"date.txt","a",stamp); put(b,"date.txt","a",stamp.AddMinutes(1));
            put(a,"content.txt","a",stamp); put(b,"content.txt","b",stamp);
            put(a,"missing.txt","a",stamp);
            string nested = Directory.CreateDirectory(Path.Combine(a,"nested")).FullName;
            Directory.CreateDirectory(Path.Combine(b,"nested"));
            put(nested,"nested.txt","a",stamp);
            using (var x = new SyncLocation(a))
            using (var y = new SyncLocation(b))
            using (var control = new CopyControl())
            {
                Func<TabComparisonCriteria,bool,string[]> rows = (criteria,recursive) =>
                    TabComparison.Run(x,y,criteria,recursive,false,control).Select(row => row.Relative).ToArray();
                Check(rows(TabComparisonCriteria.Size,false).SequenceEqual(new[]{"size.txt"}),"tab comparison can isolate size changes",checks);
                Check(rows(TabComparisonCriteria.Date,false).SequenceEqual(new[]{"date.txt"}),"tab comparison can isolate modification time changes",checks);
                Check(rows(TabComparisonCriteria.Content,false).SequenceEqual(new[]{"content.txt","size.txt"}),"tab comparison detects content differences with equal size and time",checks);
                Check(rows(TabComparisonCriteria.Missing,false).SequenceEqual(new[]{"missing.txt"}) &&
                    rows(TabComparisonCriteria.Missing,true).Contains("nested/nested.txt"),"tab comparison compares one level or includes descendants",checks);
            }

            var matches = new Entry();
            matches.SetDifference(PanelDifference.Same);
            var different = new Entry();
            different.SetDifference(PanelDifference.Different);
            Check(matches.ComparisonBrush != null && different.ComparisonBrush != null &&
                matches.ComparisonBrush != different.ComparisonBrush,"comparison states have distinct row colors",checks);
            string previousRight = right.CurrentPath;
            string previousLeft = left.CurrentPath;
            int originalLeftTabs = left.TabCount;
            NavigationTests.Wait(right.OpenLocationTab(new SavedTab{Path=a}));
            NavigationTests.Wait(right.OpenLocationTab(new SavedTab{Path=b}));
            var locations = right.ComparisonLocations(false).ToArray();
            var first = locations.Single(item => item.Path == a);
            var second = locations.Single(item => item.Path == b);
            Check(!first.Active && second.Active,"inactive and active tabs in the same panel are selectable",checks);
            var dialog=new TabComparisonWindow(main,locations,first,second)
            {
                WindowStartupLocation=WindowStartupLocation.Manual, Left=-18000, Top=-18000, ShowActivated=false
            };
            dialog.Show();
            try
            {
                NavigationTests.Wait(dialog.RunComparison());
                Check(FluentUiTests.Children<ListView>(dialog).Single().Items.Cast<SyncRow>().Count()==3,
                    "the comparison dialog displays differences for the two selected tabs",checks);
                NextStageTests.Draw(dialog,Path.Combine(root,"compare-tabs.png"));
            }
            finally { dialog.Close(); }
            NavigationTests.Wait(main.ShowTabComparisonInPanels(first,second,dialog.Rows));
            var displayedFirst = right.PanelComparisonRows();
            var displayedSecond = left.PanelComparisonRows();
            Check(right.CurrentPath == a && left.CurrentPath == b &&
                displayedFirst.Single(row => row.FullName == "missing.txt").Difference == PanelDifference.RightOnly &&
                displayedSecond.Single(row => row.FullName == "size.txt").ComparisonBrush == ComparisonColors.Different &&
                displayedSecond.Single(row => row.FullName == "content.txt").ComparisonBrush == ComparisonColors.Same,
                "results from two tabs on one side are projected into both panels with physical side labels and colors", checks);
            NextStageTests.Draw(main,Path.Combine(root,"colored-comparison.png"));
            if (left.TabCount > originalLeftTabs)
            {
                left.CloseTab(left.CurrentTabIndex); NavigationTests.Wait(left.TabNavigationTask);
            }
            left.Navigate(previousLeft,false); NavigationTests.Wait(left.NavigationTask);
            right.CloseTab(right.CurrentTabIndex); NavigationTests.Wait(right.TabNavigationTask);
            right.CloseTab(right.CurrentTabIndex); NavigationTests.Wait(right.TabNavigationTask);
            right.Navigate(previousRight,false); NavigationTests.Wait(right.NavigationTask);
        }

        static void VerifySavedFolderView(FilePanel panel, string folder, string child, List<string> checks)
        {
            var owner = (MainWindow)Window.GetWindow(panel);
            var menuButton = FluentUiTests.Children<Button>(owner).Single(button =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "PanelViewMenu");
            panel.FocusFiles();
            Check(Equals((menuButton.Content as Image)?.Tag, "tree"), "the view menu uses the folder tree toolbar icon", checks);
            Check(!FluentUiTests.Children<Button>(panel).Any(button =>
                Equals(button.Content, L.Text("columns.defaultView")) || Equals(button.Content,L.Text("projection.includeChildren"))),
                "view buttons no longer appear below the tabs", checks);
            panel.SetDisplay("Details");
            var grid = (GridView)panel.List.View;
            var header = (GridViewColumnHeader)grid.Columns[0].Header;
            header.ContextMenu.IsOpen = true;
            var grouping = header.ContextMenu.Items.OfType<MenuItem>().Single(item =>
                Equals(item.Header, L.Text("columns.groupBy")));
            ((MenuItem)grouping.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var save = header.ContextMenu.Items.OfType<MenuItem>().Single(item =>
                Equals(item.Header, L.Text("columns.saveChildren")));
            save.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            header.ContextMenu.IsOpen = false;
            Check(panel.FolderViewCaption == L.Text("columns.savedView"), "saving a view updates the panel indicator immediately", checks);

            panel.SetAutomaticFolderViews(false);
            panel.Navigate(child, false);
            NavigationTests.Wait(panel.NavigationTask);
            var current = new PanelView();
            panel.SavePanelView(current);
            Check(current.GroupBy == "Extension" && panel.FolderViewCaption == L.Text("columns.savedView"),
                "saved rules apply to descendants and keep grouping with automatic detection off", checks);
            menuButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var menu = menuButton.ContextMenu;
            Check(menu != null && menu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, L.Text("panel.folderTree"))) &&
                menu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, L.Text("projection.includeChildren"))),
                "toolbar menu offers folder tree and flat view", checks);
            var flat = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header,L.Text("projection.includeChildren")));
            menu.IsOpen = false;
            flat.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            NavigationTests.Wait(panel.NavigationTask);
            Check(panel.IsFlatView && panel.CurrentPath == child, "the toolbar toggles flat view on the active panel", checks);
            menuButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            menu = menuButton.ContextMenu;
            var reset = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, L.Text("columns.useDefault")));
            reset.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false;
            NavigationTests.Wait(panel.NavigationTask);
            NavigationTests.Wait(Task.Delay(50));
            panel.SavePanelView(current);
            Check(!panel.IsFlatView && String.IsNullOrEmpty(current.GroupBy) && panel.FolderViewCaption == L.Text("columns.defaultView"),
                "using the default view temporarily removes inherited grouping", checks);
            Check(FolderViewRules.Load().Match(child) != null, "using the default does not delete the saved rule", checks);
        }

        static async Task VerifyLabelEditor(MainWindow main, FilePanel panel, string folder, string output, List<string> checks)
        {
            var entry = panel.List.Items.Cast<Entry>().Single(item => item.FullPath == folder);
            panel.SelectContextEntry(entry);
            var tabFace = FluentUiTests.Children<Border>(panel).First(item => Equals(item.Tag,"panel-tab-face"));
            Check(tabFace.ContextMenu.Items.OfType<MenuItem>().All(item => item.Icon != null),
                "tab context menu has action icons", checks);
            Check(panel.List.ContextMenu.Items.OfType<MenuItem>().Where(item => Equals(item.Tag,"tags.edit") ||
                Equals(item.Tag,"clipboard.copyFullPaths")).Count(item => item.Icon != null) == 2,
                "file context shows label and copy-path icons", checks);

            var opened = new TaskCompletionSource<bool>();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int attempts = 0;
            timer.Tick += delegate
            {
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(window =>
                    window.IsVisible && window.Owner == main && Equals(window.Title,L.Text("tags.edit")));
                if (dialog == null)
                {
                    if (++attempts >= 100)
                    {
                        timer.Stop();
                        opened.TrySetException(new TimeoutException("Label editor did not open"));
                    }
                    return;
                }
                timer.Stop();
                try
                {
                    var input = FluentUiTests.Children<TextBox>(dialog).Single(text => text.IsVisible);
                    input.Text = "old; Custom";
                    Func<string,ComboBox> combo = key => FluentUiTests.Children<ComboBox>(dialog).Single(box =>
                        System.Windows.Automation.AutomationProperties.GetName(box) == L.Text(key));
                    var label = combo("tags.colorLabel");
                    label.SelectedItem = "Custom";
                    var colors = combo("tags.color");
                    colors.SelectedItem = colors.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag,"#754CAB"));
                    Check(label.Items.Cast<string>().Contains("Old") && colors.Items.Count >= 9,
                        "the editor normalizes Old and shows a color palette for a custom label",checks);
                    NextStageTests.Draw(dialog,Path.Combine(output,"label-colors.png"));
                    var save = FluentUiTests.Children<Button>(dialog).Single(button => Equals(button.Content,L.Text("action.save")));
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    opened.TrySetResult(true);
                }
                catch (Exception ex) { dialog.Close(); opened.TrySetException(ex); }
            };
            timer.Start();
            main.EditTags();
            await opened.Task;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                FileTagStore.Current.Snapshot(out var palette);
                if (palette.TryGetValue("custom",out var value) && value == "#754CAB" && panel.NavigationTask != null)
                {
                    await panel.NavigationTask;
                    var refreshed = panel.List.Items.Cast<Entry>().Single(item => item.FullPath == folder);
                    if (refreshed.TagColor("Custom") == value)
                    {
                        Check(refreshed.TagNames.Contains("Old"), "saved custom color appears in the refreshed folder row", checks);
                        return;
                    }
                }
                await Task.Delay(50);
            }
            throw new TimeoutException("Saved label color did not appear in the panel");
        }

        static void VerifyLabelSearch(MainWindow main, FilePanel panel, string folder, string taggedFolder, string otherFile, string output, List<string> checks)
        {
            FileTagStore.Current.Change(new[] { otherFile }, new[] { "Custom variant", "Unused custom" }, 0);
            FileTagStore.Current.Change(new[] { otherFile }, new[] { "Unused custom" }, 2);
            var buttons = FluentUiTests.Children<Button>(main).ToArray();
            var favorites = buttons.Single(button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "PanelFavorites");
            var labels = buttons.Single(button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "PanelLabels");
            var toolbar = (Panel)favorites.Parent;
            Check(toolbar.Children[toolbar.Children.IndexOf(favorites) + 1] == labels &&
                labels.Content is Image icon && Equals(icon.Tag, "label") && icon.Source != null,
                "label icon sits directly beside favorite folders", checks);
            Exception failure = null;
            bool selected = false;
            int attempts = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += delegate
            {
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(window =>
                    window.IsVisible && window.Owner == main && window.Title == L.Text("tags.results"));
                var choices = dialog == null ? new Button[0] : FluentUiTests.Children<Button>(dialog).ToArray();
                Func<string,Button> find = name => choices.FirstOrDefault(button =>
                    System.Windows.Automation.AutomationProperties.GetName(button) == name);
                var custom = find("Custom");
                if (custom == null && ++attempts < 100) return;
                timer.Stop();
                try
                {
                    Check(custom != null && custom.IsEnabled && find("Custom variant")?.IsEnabled == true,
                        "labels assigned to existing items are selectable", checks);
                    Check(find("Unused custom")?.IsEnabled == false && find(L.Text("tags.waiting"))?.IsEnabled == false,
                        "unused custom and built-in labels are shown and disabled", checks);
                    NextStageTests.Draw(dialog, Path.Combine(output, "label-search.png"));
                    custom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    selected = true;
                }
                catch (Exception ex) { failure = ex; dialog?.Close(); }
            };
            timer.Start();
            try { labels.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { timer.Stop(); }
            if (failure != null) throw failure;
            Check(selected, "the toolbar opens the label picker", checks);
            NavigationTests.Wait(panel.NavigationTask);
            Check(panel.Projection.ExactLabel && panel.List.Items.Cast<Entry>().Where(item => !item.IsParent)
                .Select(item => item.FullPath).SequenceEqual(new[] { taggedFolder }),
                "clicking a label shows exact matches, including tagged folders", checks);
            panel.Navigate(folder, false);
            NavigationTests.Wait(panel.NavigationTask);
        }

        internal static int Run(string root)
        {
            AppPaths.TestProfileDirectory = Path.Combine(root, "app-profile");
            Directory.CreateDirectory(root); string oldRoot = WorkflowStore.TestRoot, oldTags = FileTagStore.TestPath; var checks = new List<string>(); MainWindow main = null;
            try
            {
                WorkflowStore.TestRoot = Path.Combine(root, "profile"); FileTagStore.TestPath = Path.Combine(root, "tags.xml");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)); FluentTheme.Ensure();
                string folder = Directory.CreateDirectory(Path.Combine(root, "files")).FullName, nested = Directory.CreateDirectory(Path.Combine(root, "files", "nested")).FullName;
                string first = Path.Combine(folder, "one.txt"), second = Path.Combine(nested, "two.txt"); File.WriteAllText(first, "one"); File.WriteAllText(second, "two");
                FileTagStore.Current.Change(new[] { nested }, new[] { "Vyřídit", "old" }, 0);
                var labeledFolder = new Entry { FullPath = nested, IsDirectory = true };
                FileTagStore.Apply(new[] { labeledFolder });
                Check(labeledFolder.TagBadges.Length == 2 && labeledFolder.TagBadges[0].Color != labeledFolder.TagBadges[1].Color,
                    "folder tags persist and display distinct colors", checks);
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
                NavigationTests.Wait(VerifyLabelEditor(main,left,nested,root,checks));
                VerifyLabelSearch(main,left,folder,nested,first,root,checks);
                int tabCount = left.TabCount;
                Check(left.ToggleFlatView(), "flat listing can be toggled from a local folder", checks);
                NavigationTests.Wait(left.NavigationTask);
                Check(left.IsProjection && left.TabCount == tabCount && left.List.Items.Cast<Entry>().Any(x => x.FullPath == renamed && x.FlatLocation == "nested"), "flat listing shows locations in the original tab", checks);
                Check(left.ToggleFlatView(), "flat listing can be turned off", checks);
                NavigationTests.Wait(left.NavigationTask);
                Check(!left.IsProjection && left.CurrentPath == folder && left.TabCount == tabCount, "turning off flat view returns to the original folder", checks);
                VerifyPanelComparison(main, left, right, folder, root, checks);
                VerifyTabComparison(main, left, right, root, checks);
                VerifySavedFolderView(left, folder, nested, checks);
                left.Navigate(folder, false); NavigationTests.Wait(left.NavigationTask);
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
                left.VerifyPanelInteractions(root, (valid, label) => Check(valid, label, checks));
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

