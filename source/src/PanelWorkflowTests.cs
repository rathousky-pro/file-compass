using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CommanderCZ
{
    internal static class PanelWorkflowTests
    {
        internal static void Until(Func<bool> condition)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(20);
            while (!condition() && DateTime.UtcNow < limit) { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(5); }
            if (!condition()) throw new TimeoutException("Workflow test timed out.");
        }
        internal static int Run(string directory)
        {
            var checks = new List<string>(); Action<bool, string> check = delegate(bool valid, string text) { if (!valid) throw new Exception(text); checks.Add(text); };
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                Directory.CreateDirectory(directory); string root = Path.Combine(directory, "root"), child = Path.Combine(root, "Děti"), target = Path.Combine(directory, "target");
                Directory.CreateDirectory(child); Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(root, "alpha.txt"), "alpha"); File.WriteAllText(Path.Combine(root, "beta.txt"), "beta"); File.WriteAllText(Path.Combine(root, "image.png"), "image"); File.WriteAllText(Path.Combine(root, "README"), "readme");
                var main = new MainWindow(root) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false };
                main.Show(); NavigationTests.WaitPanels(main); main.UpdateLayout(); main.VerifyPanelWorkflows(root, child, target, directory, check);
                NavigationTests.Wait(main.JournalWritesForWorkflowTests); main.Close();
                File.WriteAllLines(Path.Combine(directory, "navigation-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "navigation-result.txt"), "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
        }
    }
    public partial class MainWindow
    {
        internal System.Threading.Tasks.Task JournalWritesForWorkflowTests { get { return operationJournal.PendingWrites; } }
        internal void VerifyPanelWorkflows(string root, string child, string target, string directory, Action<bool, string> check)
        {
            ApplyRefreshSettings(false, false);
            check(left.TabCount == 1 && right.TabCount == 1, "each panel starts with a tab");
            var alpha = left.List.Items.Cast<Entry>().First(x => x.FullPath.EndsWith("alpha.txt"));
            left.List.SelectedItem = alpha; left.ToggleMark(alpha); left.NewTab();
            check(left.TabCount == 2 && left.CurrentTabIndex == 1, "new tab is selected");
            left.Navigate(child, true); NavigationTests.Wait(left.NavigationTask); left.SwitchTab(0); NavigationTests.Wait(left.TabNavigationTask);
            check(left.CurrentPath == root && left.List.SelectedItem is Entry && ((Entry)left.List.SelectedItem).FullPath == alpha.FullPath && left.Selected().Any(x => x.FullPath == alpha.FullPath), "switch restores directory, cursor and marks");
            left.MoveTab(0, 1); check(left.CurrentTabIndex == 1 && left.SaveTabs()[1].Path == root, "tab reordering keeps active tab");
            left.LockTab(1); left.Navigate(child, true); NavigationTests.Wait(left.NavigationTask); left.SwitchTab(0); NavigationTests.Wait(left.TabNavigationTask); left.SwitchTab(1); NavigationTests.Wait(left.TabNavigationTask);
            check(left.CurrentPath == root && left.SaveTabs()[1].Locked && left.SaveTabs()[1].Home == root, "locked tab returns to its initial directory");
            left.CloseTab(0); check(left.TabCount == 1 && left.CurrentPath == root, "closing another tab preserves location"); left.CloseTab(0); check(left.TabCount == 1, "last tab cannot be closed");
            left.MarkAll(false); left.SetQuickFilter("txt"); check(left.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 2, "typing filters file names");
            check(left.List.Items.Cast<Entry>().Any(x => x.IsParent), "filter retains parent row");
            left.SetQuickFilter("*.png"); check(left.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 1 && ((Entry)left.List.Items.Cast<Entry>().Last()).Extension == "png", "wildcard quick filter");
            left.SetQuickFilter(""); check(left.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 5, "clearing filter restores all rows");
            check(FilePanel.MaskMatches("README", "*.*") && FilePanel.MaskMatches("a.pdf", "*.txt;*.pdf") && !FilePanel.MaskMatches("a.png", "*.txt"), "selection masks include extensionless and multiple patterns");
            left.MarkMask("*.txt", true); check(left.Selected().Count == 2, "mask marks matching items"); left.MarkAll(false); left.RestoreSelection(); check(left.Selected().Count == 2, "restore previous marked selection");
            left.MarkAll(false); left.List.SelectedItem = left.List.Items.Cast<Entry>().First(x => x.Extension == "txt"); left.SelectExtension(); check(left.Selected().Count == 2, "same extension selection");
            left.RememberSelection(); left.Navigate(child, true); NavigationTests.Wait(left.NavigationTask); left.Navigate(root, true); NavigationTests.Wait(left.NavigationTask); left.RestoreSelection(); check(left.Selected().Count == 2, "stored selection survives leaving and returning");
            var settings = new ProgramSettings(); CaptureView(settings); settings.RememberFolders(root, target); settings.Favorites.Add(new FavoriteLocation { Name = "Dvojice", Group = "Práce", Left = root, Right = target, LeftLocation = new SavedTab { Path = root }, RightLocation = new SavedTab { Path = target } });
            string config = Path.Combine(directory, "settings-test.xml"); settings.Save(config); var read = ProgramSettings.Load(config);
            check(read.LeftTabs.Length == 1 && read.LeftTabs[0].Locked && read.LeftTabIndex == 0, "saved tab and lock round trip");
            check(read.Favorites[0].Group == "Práce" && read.Favorites[0].RightLocation.Path == target, "favorite groups and panel pairs round trip");
            left.RestoreTabs(new[] { new SavedTab { Path = root }, new SavedTab { Path = child } }, 1); NavigationTests.Wait(left.NavigationTask); check(left.TabCount == 2 && left.CurrentPath == child && left.CurrentTabIndex == 1, "restore tab set and active tab");
            left.SwitchTab(0); NavigationTests.Wait(left.TabNavigationTask); left.MarkAll(false); SetActive(left);
            left.List.SelectedItem = left.List.Items.Cast<Entry>().First(x => x.FullPath.EndsWith("alpha.txt"));
            var clipboard = CreateFileClipboard(new[] { Path.Combine(root, "alpha.txt") }, false); check(clipboard.GetDataPresent(DataFormats.FileDrop) && !ClipboardIsMove(clipboard), "copy exposes Windows FileDrop format");
            check(ClipboardIsMove(CreateFileClipboard(new[] { Path.Combine(root, "alpha.txt") }, true)), "cut publishes Preferred DropEffect move");
            check(FilePanel.EntryName((Entry)left.List.SelectedItem) == "alpha.txt", "copy full file name including extension"); check(((Entry)left.List.SelectedItem).FullPath == Path.Combine(root, "alpha.txt"), "copy absolute path");
            var snapshot = CurrentLocation(left); left.Navigate(child, true); NavigationTests.Wait(left.NavigationTask); check(snapshot.Path == root, "favorite captures independent location rather than mutable tab");
            right.Navigate(target, true); NavigationTests.Wait(right.NavigationTask); SetActive(left); OpenSameFolder(); NavigationTests.Wait(right.NavigationTask); check(right.CurrentPath == child, "open same folder in other panel");
            OpenFavorite(new FavoriteLocation { Left = root, Right = target }); NavigationTests.Wait(left.NavigationTask); NavigationTests.Wait(right.NavigationTask); check(left.CurrentPath == root && right.CurrentPath == target, "favorite opens both panels");
            string attr = Path.Combine(root, "beta.txt"); DateTime stamp = new DateTime(2025, 2, 3, 4, 5, 6); var changes = new AttributeChanges { ReadOnly = true, Hidden = true, Modified = stamp };
            var changed = AttributeChanges.Apply(new[] { attr }, changes, delegate { return false; }); check(changed.Single().State == "Dokončeno" && File.GetLastWriteTime(attr) == stamp && (File.GetAttributes(attr) & (FileAttributes.Hidden | FileAttributes.ReadOnly)) == (FileAttributes.Hidden | FileAttributes.ReadOnly), "attributes and time apply correctly");
            AttributeChanges.Apply(new[] { attr }, new AttributeChanges { ReadOnly = false }, delegate { return false; }); check((File.GetAttributes(attr) & FileAttributes.Hidden) != 0 && (File.GetAttributes(attr) & FileAttributes.ReadOnly) == 0, "tri-state preserves unspecified attributes");
            File.SetAttributes(attr, FileAttributes.Normal);
            string recursiveRoot = Path.Combine(directory, "attribute-tree"), recursiveChild = Path.Combine(recursiveRoot, "child"); Directory.CreateDirectory(recursiveChild);
            string recursiveFile = Path.Combine(recursiveChild, "file.txt"); File.WriteAllText(recursiveFile, "keep");
            AttributeChanges.Apply(new[] { recursiveRoot }, new AttributeChanges { Modified = stamp }, delegate { return false; });
            check(File.GetLastWriteTime(recursiveFile) != stamp, "attribute operation excludes descendants by default");
            var recursiveResults = AttributeChanges.Apply(new[] { recursiveRoot, recursiveFile }, new AttributeChanges { Recursive = true, Modified = stamp, ReadOnly = false }, delegate { return false; });
            check(recursiveResults.Count == 3 && recursiveResults.All(x => x.State == "Dokončeno") && File.GetLastWriteTime(recursiveFile) == stamp && File.GetLastWriteTime(recursiveChild) == stamp && File.ReadAllText(recursiveFile) == "keep", "recursive attributes include nested files and directories exactly once without changing content");
            check(AttributeChanges.Apply(new[] { Path.Combine(root, "missing") }, changes, delegate { return false; }).Single().State == "Chyba", "attribute failure identifies item");
            check(AttributeChanges.Apply(new[] { attr }, changes, delegate { return true; }).Count == 0, "attribute cancellation stops before mutation");
            check(AttributesDialog.Parse("13.10.2026 12:30:00").Value.Hour == 12 && AttributesDialog.Parse("") == null, "explicit Czech timestamp and unchanged value");
            bool invalid = false; try { AttributesDialog.Parse("bad"); } catch (FormatException) { invalid = true; } check(invalid, "invalid timestamp is rejected");
            QueueExternalCopy(new[] { Path.Combine(root, "alpha.txt") }, new CopyOptions { Target = target, Rule = CopyRule.OverwriteAll }); PanelWorkflowTests.Until(delegate { return !copyRunning; });
            check(File.ReadAllText(Path.Combine(target, "alpha.txt")) == "alpha" && File.Exists(Path.Combine(root, "alpha.txt")), "clipboard/drop queue uses safe copy engine");
            string moving = Path.Combine(root, "move.txt"); File.WriteAllText(moving, "move"); QueueExternalCopy(new[] { moving }, new CopyOptions { Target = target, Rule = CopyRule.OverwriteAll, Move = true, Verify = true }); PanelWorkflowTests.Until(delegate { return !copyRunning; });
            check(!File.Exists(moving) && File.ReadAllText(Path.Combine(target, "move.txt")) == "move", "external move uses verified engine");
            NavigationTests.WaitPanels(this); left.Navigate(root, false); NavigationTests.Wait(left.NavigationTask); left.SetQuickFilter("txt"); left.MarkMask("alpha.txt", true); left.List.SelectedItem = left.List.Items.Cast<Entry>().First(x => x.FullPath.EndsWith("alpha.txt"));
            ApplyRefreshSettings(true, false); PanelWorkflowTests.Until(delegate { return left.WatcherActive; });
            string fresh = Path.Combine(root, "new.txt"); File.WriteAllText(fresh, "created outside panel");
            PanelWorkflowTests.Until(delegate { return !left.LocalLoading && left.List.Items.Cast<Entry>().Any(x => x.FullPath == fresh); });
            check(left.Selected().Any(x => x.FullPath.EndsWith("alpha.txt")) && ((Entry)left.List.SelectedItem).FullPath.EndsWith("alpha.txt"), "watcher refresh preserves cursor, marks and filter");
            check(!left.List.Items.Cast<Entry>().Any(x => x.Extension == "png"), "watcher preserves active filter");
            File.Delete(fresh); PanelWorkflowTests.Until(delegate { return !left.LocalLoading && !left.List.Items.Cast<Entry>().Any(x => x.FullPath == fresh); }); check(true, "watcher reacts to deletion");
            ApplyRefreshSettings(false, false); check(!left.WatcherActive && !right.WatcherActive, "refresh can be disabled and watchers disposed");
            left.MarkAll(false); left.SetQuickFilter(""); right.SetQuickFilter("");
            left.Navigate(child, true); NavigationTests.Wait(left.NavigationTask); right.Navigate(target, true); NavigationTests.Wait(right.NavigationTask);
            string disk = Path.GetPathRoot(root); right.SelectDrive(disk); NavigationTests.Wait(right.NavigationTask);
            check(right.CurrentPath == child && active == right, "disk selection opens the other panel folder on the same disk and activates the selected panel");
            right.Navigate(target, true); NavigationTests.Wait(right.NavigationTask); left.SelectDrive(disk); NavigationTests.Wait(left.NavigationTask);
            check(left.CurrentPath == target && active == left, "disk folder mirroring works in both directions");
            check(FolderOnDisk(left, "Q:\\") == "Q:\\", "different disk keeps its root without probing or adopting another drive folder");
            left.Navigate(root, true); NavigationTests.Wait(left.NavigationTask); SetActive(left);
            string renameSource = Path.Combine(root, "before-rename.txt"); File.WriteAllText(renameSource, "rename content");
            var renameSetup = new CopySetupDialog(this, new[] { new Entry { FullPath = renameSource } }, target, new string[0], true);
            var renameDestination = FluentUiTests.Children<ComboBox>(renameSetup).First(); renameDestination.Text = "after-rename.txt";
            var renameOptions = renameSetup.CreateOptions(false);
            check(renameOptions.Move && renameOptions.TargetIsFile && renameOptions.Target == Path.Combine(root, "after-rename.txt"), "F6 bare name selects an exact destination in the source folder");
            using (var control = new CopyControl()) { var result = new CopyEngine(renameOptions, control, null, null).Run(new[] { renameSource }); check(result.Errors.Count == 0 && !File.Exists(renameSource) && File.ReadAllText(renameOptions.Target) == "rename content" && !Directory.Exists(renameOptions.Target), "F6 rename changes the file name without creating a wrapper folder"); }
            renameDestination.Text = target + "\\*.*"; check(!renameSetup.CreateOptions(false).TargetIsFile && renameSetup.CreateOptions(false).Target.TrimEnd('\\') == target.TrimEnd('\\'), "F6 default wildcard destination remains a normal folder move");
            renameDestination.Text = Path.Combine(target, "new-folder") + "\\"; check(!renameSetup.CreateOptions(false).TargetIsFile, "explicit trailing separator requests a new destination folder"); renameSetup.Close();
            string renameFolder = Path.Combine(root, "before-folder"); Directory.CreateDirectory(renameFolder); File.WriteAllText(Path.Combine(renameFolder, "nested.txt"), "nested");
            var folderSetup = new CopySetupDialog(this, new[] { new Entry { FullPath = renameFolder, IsDirectory = true } }, target, new string[0], true);
            FluentUiTests.Children<ComboBox>(folderSetup).First().Text = "after-folder"; var folderOptions = folderSetup.CreateOptions(false);
            using (var control = new CopyControl()) { var result = new CopyEngine(folderOptions, control, null, null).Run(new[] { renameFolder }); check(result.Errors.Count == 0 && !Directory.Exists(renameFolder) && File.ReadAllText(Path.Combine(root, "after-folder", "nested.txt")) == "nested" && !Directory.Exists(Path.Combine(root, "after-folder", "before-folder")), "F6 renames a whole folder without adding another nesting level"); } folderSetup.Close();
            Activate(); left.List.Focus(); check(left.List.IsKeyboardFocusWithin, "file list can receive keyboard focus after rename dialog");
            left.Refresh(); NavigationTests.Wait(left.NavigationTask); check(left.List.IsEnabled && left.List.IsKeyboardFocusWithin, "refresh after rename restores keyboard focus for arrow navigation");
            string queuedRename = Path.Combine(root, "before-queued-rename.txt"), queuedTarget = Path.Combine(root, "after-queued-rename.txt"); File.WriteAllText(queuedRename, "queued rename"); left.Refresh(); NavigationTests.Wait(left.NavigationTask);
            left.List.SelectedItem = left.List.Items.Cast<Entry>().First(x => x.FullPath == queuedRename); left.List.Focus();
            EnqueueCopy(new CopyJob { Sources = new[] { queuedRename }, Options = new CopyOptions { Target = queuedTarget, TargetIsFile = true, Move = true, Verify = true } }); RunCopyQueue(); PanelWorkflowTests.Until(delegate { return !copyRunning; }); NavigationTests.Wait(left.NavigationTask);
            check(!File.Exists(queuedRename) && File.ReadAllText(queuedTarget) == "queued rename" && left.List.IsKeyboardFocusWithin, "real background F6 queue rename retains a usable focused file list");
            int beforeArrow = left.List.SelectedIndex; var focusedRow = System.Windows.Input.Keyboard.FocusedElement as UIElement;
            focusedRow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(left.List), Environment.TickCount, System.Windows.Input.Key.Down) { RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent });
            check(left.List.SelectedIndex == beforeArrow + 1, "Down arrow moves the selection after background rename finishes without refocusing manually");
            var sizeEntry = left.List.Items.Cast<Entry>().First(x => x.FullPath == Path.Combine(root, "after-folder"));
            left.List.SelectedItem = sizeEntry; left.FocusFiles();
            var sizeRow = (UIElement)left.List.ItemContainerGenerator.ContainerFromItem(sizeEntry);
            sizeRow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(left.List), Environment.TickCount, System.Windows.Input.Key.Space) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            PanelWorkflowTests.Until(delegate { return sizeEntry.SizeKnown; });
            check(sizeEntry.Bytes == 6 && sizeEntry.IsMarked && !sizeEntry.SizeLoading, "Space routed from a focused file row calculates folder bytes and marks it");
            left.SetQuickFilter(""); UpdateLayout();
            var panel = (FrameworkElement)Content; var bitmap = new RenderTargetBitmap((int)panel.ActualWidth, (int)panel.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(directory, "panels.png"))) encoder.Save(stream);
            check(FluentUiTests.Children<Button>(left).Where(x => x.ToolTip != null && x.ToolTip.ToString().Contains("Nová záložka")).All(x => x.ActualHeight > 0), "tab controls have real layout");
        }
    }
}

