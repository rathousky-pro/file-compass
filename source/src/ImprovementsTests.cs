using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CommanderCZ
{
    internal static class ImprovementsTests
    {
        static CopyResult Copy(string source, string target, bool move, CopyRule rule = CopyRule.OverwriteAll, Action<CopyUpdate> report = null)
        { using (var control = new CopyControl()) return new CopyEngine(new CopyOptions { Target = target, Move = move, Rule = rule }, control, x => new CopyDecision(CopyChoice.Overwrite), report).Run(new[] { source }); }
        internal static int Run(string directory)
        {
            Directory.CreateDirectory(directory); FluentTheme.Ensure(); var checks = new List<string>();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Action<bool, string> check = delegate(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); File.AppendAllText(Path.Combine(directory, "progress.txt"), label + "\r\n"); };
            string root = Directory.CreateDirectory(Path.Combine(directory, "cases-" + Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                string output = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;
                string source = Directory.CreateDirectory(Path.Combine(root, "merge")).FullName;
                File.WriteAllText(Path.Combine(source, "new.txt"), "new data"); File.WriteAllText(Path.Combine(source, "same.txt"), "new content");
                string destination = Directory.CreateDirectory(Path.Combine(output, "merge")).FullName; File.WriteAllText(Path.Combine(destination, "same.txt"), "old content");
                var skipped = Copy(source, output, true, CopyRule.SkipAll);
                check(skipped.Errors.Count == 0 && skipped.Copied == 1 && skipped.Skipped == 1, "F6 merges directories and reports collision skips");
                check(!File.Exists(Path.Combine(source, "new.txt")) && File.Exists(Path.Combine(source, "same.txt")), "only verified moved files disappear from source");
                check(File.ReadAllText(Path.Combine(destination, "same.txt")) == "old content", "skipped destination is preserved");
                var overwritten = Copy(source, output, true); check(overwritten.Copied == 1 && !Directory.Exists(source) && File.ReadAllText(Path.Combine(destination, "same.txt")) == "new content", "F6 overwrite finishes merge and removes empty source directory");
                string fast = Directory.CreateDirectory(Path.Combine(root, "fast-directory")).FullName; File.WriteAllText(Path.Combine(fast, "fast.txt"), "same volume"); bool copiedBytes = false;
                var fastResult = Copy(fast, output, true, CopyRule.OverwriteAll, update => { if (update.Phase == "Kopírování") copiedBytes = true; });
                check(fastResult.Copied == 1 && !copiedBytes && !Directory.Exists(fast), "same-volume move uses native rename without copying file data");
                string empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
                var emptyResult = Copy(empty, output, true); check(emptyResult.Errors.Count == 0 && !Directory.Exists(empty) && Directory.Exists(Path.Combine(output, "empty")), "empty directory moves safely");
                string readOnlyFile = Path.Combine(root, "readonly.txt"); File.WriteAllText(readOnlyFile, "readonly content"); File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
                var readonlyMove = Copy(readOnlyFile, output, true); check(readonlyMove.Errors.Count == 0 && !File.Exists(readOnlyFile) && (File.GetAttributes(Path.Combine(output, "readonly.txt")) & FileAttributes.ReadOnly) != 0, "read-only source moves safely and destination keeps original attribute");
                string lockedFile = Path.Combine(root, "locked.txt"); File.WriteAllText(lockedFile, "preserve source");
                CopyResult failed;
                using (var locked = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (var control = new CopyControl())
                    failed = new CopyEngine(new CopyOptions { Target = output, Move = true }, control, x => new CopyDecision(CopyChoice.Overwrite), null, x => CopyErrorChoice.Skip).Run(new[] { lockedFile });
                check(File.ReadAllText(lockedFile) == "preserve source" && !File.Exists(Path.Combine(output, "locked.txt")), "locked move preserves source and creates no partial destination");
                check(failed.Items.Single().State == "Chyba" && !String.IsNullOrEmpty(failed.Items.Single().Error), "skipped IO error remains retryable in history");
                string cancelFile = Path.Combine(root, "cancel.bin"); File.WriteAllBytes(cancelFile, new byte[2 * 1024 * 1024]);
                using (var control = new CopyControl())
                {
                    var cancelled = new CopyEngine(new CopyOptions { Target = output, Move = true, Times = false }, control, x => new CopyDecision(CopyChoice.Overwrite), update => { if (update.Phase == "Ověřování SHA-256") control.Cancel(); }).Run(new[] { cancelFile });
                    check(cancelled.Cancelled && File.Exists(cancelFile) && !File.Exists(Path.Combine(output, "cancel.bin")), "cancellation during verification retains source and original destination");
                }
                string filtered = Directory.CreateDirectory(Path.Combine(root, "filtered")).FullName;
                File.WriteAllText(Path.Combine(filtered, "keep.txt"), "move"); File.WriteAllText(Path.Combine(filtered, "stay.bin"), "stay");
                using (var control = new CopyControl()) new CopyEngine(new CopyOptions { Target = output, Move = true, Filter = "*.txt" }, control, x => new CopyDecision(CopyChoice.Overwrite), null).Run(new[] { filtered });
                check(File.Exists(Path.Combine(filtered, "stay.bin")) && !File.Exists(Path.Combine(filtered, "keep.txt")), "move filter never removes excluded sources");
                if (!String.Equals(Path.GetPathRoot(Path.GetTempPath()), Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                {
                    string cross = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CommanderCZ-move-" + Guid.NewGuid().ToString("N"))).FullName;
                    try { File.WriteAllText(Path.Combine(cross, "cross.txt"), "cross volume"); var moved = Copy(cross, output, true); check(moved.Errors.Count == 0 && !Directory.Exists(cross) && File.ReadAllText(Path.Combine(output, Path.GetFileName(cross), "cross.txt")) == "cross volume", "real directory move across different Windows volumes"); }
                    finally { if (Directory.Exists(cross)) Directory.Delete(cross, true); }
                }
                string large = Directory.CreateDirectory(Path.Combine(root, "large")).FullName;
                for (int i = 0; i < 7000; i++) File.WriteAllText(Path.Combine(large, "item-" + i + ".txt"), "x");
                var panel = new FilePanel(delegate { });
                var watch = System.Diagnostics.Stopwatch.StartNew(); panel.Navigate(large, true); watch.Stop();
                check(watch.ElapsedMilliseconds < 1000 && panel.LocalLoading && !panel.IsLoading, "large folder navigation returns immediately without disabling whole application");
                bool dispatcherRan = false; panel.Dispatcher.BeginInvoke(new Action(delegate { dispatcherRan = true; }));
                NavigationTests.Wait(panel.NavigationTask);
                check(dispatcherRan && panel.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 7000, "dispatcher stays responsive while 7000 entries and icons load");
                check(panel.List.Items.Cast<Entry>().All(x => x.Icon == null || x.Icon.IsFrozen), "background icons are safe frozen resources");
                panel.Navigate(large, false); var previous = panel.NavigationTask; panel.Navigate(output, true); NavigationTests.Wait(panel.NavigationTask); NavigationTests.Wait(previous);
                check(panel.CurrentPath == output && !panel.LocalLoading && panel.List.IsEnabled, "obsolete directory response cannot overwrite newer navigation");
                panel.Navigate(root, false); NavigationTests.Wait(panel.NavigationTask); panel.Navigate(output, true); NavigationTests.Wait(panel.NavigationTask); panel.Back(); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == root, "async Back waits before changing history"); panel.Forward(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == output, "async Forward restores navigation history");
                panel.Navigate(Path.Combine(root, "missing"), false); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == output && panel.List.IsEnabled && !panel.LocalLoading, "unavailable folder preserves previous view and unlocks list");
                panel.CancelNavigation();
                var profile = new NetworkProfile { Protocol = NetworkProtocol.SFTP, Host = "localhost", SshAgent = true, SshKeepAliveSeconds = 30 };
                using (var connection = new RemoteConnection(profile)) { var options = connection.Options("test"); check(options.Raw("Agent", "off") == "on" && options.Raw("KeepAlive", "0") == "30", "SFTP profile forwards agent and keepalive to native transport"); }
                profile.Protocol = NetworkProtocol.SCP; profile.SshAgent = false; profile.SshKeepAliveSeconds = 0;
                using (var connection = new RemoteConnection(profile)) { var options = connection.Options("test"); check(options.Raw("Agent", "on") == "off" && options.Raw("KeepAlive", "1") == "0", "SCP honors disabled agent and keepalive"); }
                var networkJob = new NetworkTransferJob(profile, false); networkJob.Reserve();
                var batch = new NetworkCopyBatch { Entry = new Entry { FullPath = lockedFile } }; batch.Items.Add(new NetworkQueueItem { Source = lockedFile, Destination = "/locked.txt", State = "Ve frontě", Bytes = 3 }); networkJob.AddPrepared(new[] { batch }); networkJob.PlanningDone(null, lockedFile, "/"); networkJob.ResolveError = failure => CopyErrorChoice.Skip;
                networkJob.HandleError(new CopyFailure { Source = lockedFile, Target = "/locked.txt", Error = new IOException("locked fixture") });
                check(networkJob.SnapshotItems().Single().State.StartsWith("Chyba"), "skipped network IO error retains failed state for history retry"); networkJob.Finish("Dokončeno s chybami"); networkJob.Cancellation.Dispose();
                var planning = new NetworkTransferJob(profile, false); planning.Reserve(); planning.PlanningDone(new IOException("planning error"), "joined sources", "/", new[] { new Entry { FullPath = lockedFile }, new Entry { FullPath = cancelFile } }, NetworkCopyRule.Overwrite);
                check(planning.SnapshotItems().Length == 2 && planning.SnapshotItems().All(x => x.Policy == "Přepsat" && x.Destination.StartsWith("/")), "failed network planning preserves separate sources, literal destinations and policy"); planning.Cancellation.Dispose();
                var preferences = new ProgramSettings { WindowWidth = 1120, WindowHeight = 680, SplitRatio = .37, LeftFolder = root, RightFolder = output };
                preferences.LeftView.Sort = "Size"; preferences.LeftView.Descending = true; preferences.LeftView.Widths = new[] { 315.0, 90, 105, 150, 70 };
                preferences.Save(Path.Combine(root, "settings.xml")); var loaded = ProgramSettings.Load(Path.Combine(root, "settings.xml"));
                check(loaded.WindowWidth == 1120 && loaded.SplitRatio == .37 && loaded.LeftView.Sort == "Size" && loaded.LeftView.Widths[0] == 315, "window geometry, split, sort and column widths survive XML round trip");
                var rich = new MainWindow(null, loaded) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; rich.Show(); NavigationTests.WaitPanels(rich); rich.UpdateLayout();
                var actualView = new ProgramSettings(); rich.CaptureView(actualView);
                check(rich.Width == 1120 && Math.Abs(actualView.SplitRatio - .37) < .01 && actualView.LeftView.Sort == "Size" && actualView.LeftView.Descending && actualView.LeftView.Widths[0] == 315, "real window restores splitter, sorting and saved column width without auto-resize overwriting it");
                rich.Width = 1250; rich.UpdateLayout(); rich.CaptureView(actualView); check(actualView.LeftView.Widths[0] == 315, "manual column widths survive window resizing"); rich.Close();
                string journalPath = Path.Combine(root, "history.xml"); var journal = new OperationJournal(journalPath);
                var record = new OperationRecord { Title = "Kopírování", State = "Dokončeno s chybami", Summary = "1 OK / 1 chyba", Ended = DateTime.Now, Items = failed.Items };
                record.Retry = delegate { }; journal.Records.Add(record); journal.Save(); NavigationTests.Wait(journal.PendingWrites);
                var restoredJournal = new OperationJournal(journalPath); check(restoredJournal.Records.Count == 1 && restoredJournal.Records[0].Items.Single().Source == lockedFile, "history and failure paths survive restart");
                check(restoredJournal.Records[0].Retry == null && !File.ReadAllText(journalPath).Contains("Password"), "persisted history excludes runtime delegates and passwords");
                check(OperationJournal.Log(journal.Records).Contains("Chyba") && OperationJournal.Log(journal.Records).Contains(lockedFile), "exported log includes individual failed file and error");
                journal.Records.Add(new OperationRecord { Title = "Nedokončeno" }); journal.Save(); NavigationTests.Wait(journal.PendingWrites);
                check(new OperationJournal(journalPath).Records.Last().State == "Přerušeno při ukončení", "unfinished journal entries become interrupted after restart");
                var main = new MainWindow(root) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; main.Show(); main.Hide(); NavigationTests.WaitPanels(main); main.VerifyImprovements(root, output, failed.Items, check); main.Close();
                var missing = new MainWindow(null, new ProgramSettings { LeftFolder = Path.Combine(root, "missing-start"), RightFolder = output, WindowWidth = -1, SplitRatio = Double.NaN });
                NavigationTests.WaitPanels(missing); check(missing.ViewFolders()[0] != null && missing.ViewFolders()[1] == output && missing.Width == 1440, "missing saved folder falls back asynchronously and corrupt geometry is sanitized"); missing.Close();
                File.WriteAllLines(Path.Combine(directory, "improvements-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "improvements-result.txt"), "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
        }
    }
    public partial class MainWindow
    {
        internal void VerifyImprovements(string root, string output, List<OperationItem> failures, Action<bool, string> check)
        {
            var record = new OperationRecord { Ended = DateTime.Now }; record.Items.AddRange(failures);
            string untouched = Path.Combine(output, "done.txt"); File.WriteAllText(untouched, "already done"); record.Items.Add(new OperationItem { Source = "missing completed source", Target = untouched, State = "Dokončeno" });
            ConfigureCopyRetry(record, new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll });
            record.Retry(); DateTime limit = DateTime.Now.AddSeconds(20);
            while (copyRunning && DateTime.Now < limit) { Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(1); }
            check(!copyRunning && File.ReadAllText(Path.Combine(output, "locked.txt")) == "preserve source" && File.ReadAllText(untouched) == "already done", "history retry copies only failed files and leaves completed files untouched");
            var waitingA = new CopyJob { Sources = new[] { "queued A" }, Options = new CopyOptions { Target = output } }; var waitingB = new CopyJob { Sources = new[] { "queued B" }, Options = new CopyOptions { Target = output } };
            EnqueueCopy(waitingA); EnqueueCopy(waitingB); waitingA.Record.Cancel();
            check(copyJobs.Count == 1 && waitingA.Record.State == "Zrušeno" && waitingB.Record.State == "Ve frontě", "waiting jobs appear in common history and cancel independently"); CancelWaitingCopies(); check(copyJobs.Count == 0 && waitingB.Record.State == "Zrušeno", "cancel-all retains cancelled waiting jobs in history");
            int cancelled = 0; var local = AddCopyActivity("Místní kopírování", delegate { cancelled++; }); var archive = AddCopyActivity("Archiv", delegate { cancelled++; });
            summaryCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); check(cancelled == 2, "common cancel button cancels local and archive operations");
            local.Finish("Zrušeno", false); archive.Finish("Zrušeno", false); check(local.Record.State == "Zrušeno" && archive.Record.Ended != default(DateTime), "common lifecycle distinguishes cancelled and finished operations");
            var contrast = AddCopyActivity("FTP · první procenta", delegate { }); contrast.Update("Přenos", .9, .01);
            check(((System.Windows.Media.SolidColorBrush)summaryLabel.Foreground).Color == ((System.Windows.Media.SolidColorBrush)FluentTheme.Text).Color && summaryLabel.Effect == null, "summary uses readable dark text above its separate progress track");
            var speedJob = new NetworkTransferJob(new NetworkProfile { Protocol = NetworkProtocol.FTP, Host = "localhost" }, true) { Activity = contrast };
            var speedBatch = new NetworkCopyBatch { Entry = new Entry { FullPath = "/speed.bin" }, Destination = root, Bytes = 100 * 1048576L };
            speedBatch.Items.Add(new NetworkQueueItem { Source = "/speed.bin", Bytes = speedBatch.Bytes, State = "Ve frontě" });
            speedJob.AddPrepared(new[] { speedBatch }); speedJob.Progress("/speed.bin", .25, 10 * 1048576); networkJobs.Add(speedJob); RefreshCopySummary();
            check(summaryTransferInfo.Visibility == Visibility.Visible && summaryTransferInfo.Text.Contains("10,0 MB/s") && summaryTransferInfo.Text.EndsWith("—"), "summary displays network rate without premature ETA");
            copySummary.Measure(new Size(600, 44)); copySummary.Arrange(new Rect(0, 0, 600, 44)); copySummary.UpdateLayout();
            check(summaryTransferInfo.TransformToAncestor(copySummary).Transform(new Point(summaryTransferInfo.ActualWidth, 0)).X <= summaryCancel.TransformToAncestor(copySummary).Transform(new Point(0, 0)).X && summaryTransferInfo.ActualWidth > 100, "speed and time fit immediately before cancel in a compact summary");
            var secondSpeedJob = new NetworkTransferJob(new NetworkProfile { Protocol = NetworkProtocol.SFTP, Host = "localhost" }, true) { Activity = contrast };
            var secondSpeedBatch = new NetworkCopyBatch { Entry = new Entry { FullPath = "/second.bin" }, Destination = root, Bytes = 200 * 1048576L };
            secondSpeedBatch.Items.Add(new NetworkQueueItem { Source = "/second.bin", Bytes = secondSpeedBatch.Bytes, State = "Ve frontě" });
            secondSpeedJob.AddPrepared(new[] { secondSpeedBatch }); secondSpeedJob.Progress("/second.bin", .2, 20 * 1048576); networkJobs.Add(secondSpeedJob); RefreshCopySummary();
            check(summaryTransferInfo.Text.Contains("30,0 MB/s") && summaryTransferInfo.Text.EndsWith("—"), "parallel transfer speeds sum while ETA waits for measurements");
            var addedSpeedBatch = new NetworkCopyBatch { Entry = new Entry { FullPath = "/added.bin" }, Destination = root, Bytes = 100 * 1048576L }; speedJob.AddPrepared(new[] { addedSpeedBatch }); RefreshCopySummary();
            check(summaryTransferInfo.Text.EndsWith("—"), "adding work does not invent an unmeasured completion estimate");
            speedJob.Reserve(); RefreshCopySummary(); check(summaryTransferInfo.Text.EndsWith("—"), "ETA stays unknown while additional files are being scanned"); speedJob.PlanningDone(null, "", "");
            speedJob.Progress("/speed.bin", .25, 0, "Ověřování zdroje"); RefreshCopySummary(); check(summaryTransferInfo.Text.Contains("20,0 MB/s") && summaryTransferInfo.Text.EndsWith("—"), "verification does not reuse old speed or promise an invalid completion time");
            speedJob.Progress("/speed.bin", .25, 10 * 1048576); speedJob.Cancel(); RefreshCopySummary(); check(summaryTransferInfo.Text.Contains("20,0 MB/s") && summaryTransferInfo.Text.EndsWith("—"), "cancelled transfer stops contributing its old speed and ETA");
            speedJob.Finish("Zrušeno"); RefreshCopySummary(); check(summaryTransferInfo.Text.Contains("20,0 MB/s") && summaryTransferInfo.Text.EndsWith("—"), "completed jobs no longer affect speed or ETA");
            networkJobs.Remove(secondSpeedJob); networkJobs.Remove(speedJob); speedJob.Cancellation.Dispose(); secondSpeedJob.Cancellation.Dispose();
            var screenshotSpeedJob = new NetworkTransferJob(new NetworkProfile { Protocol = NetworkProtocol.FTP, Host = "localhost" }, true) { Activity = contrast }; screenshotSpeedJob.AddPrepared(new[] { speedBatch }); screenshotSpeedJob.Progress("/speed.bin", .25, 10 * 1048576); networkJobs.Add(screenshotSpeedJob); RefreshCopySummary();
            copySummary.Measure(new Size(950, 44)); copySummary.Arrange(new Rect(0, 0, 950, 44)); copySummary.UpdateLayout();
            var summaryBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(950, 44, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var summaryDrawing = new System.Windows.Media.DrawingVisual(); using (var canvas = summaryDrawing.RenderOpen()) { canvas.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, 950, 44)); canvas.DrawRectangle(new System.Windows.Media.VisualBrush(copySummary), null, new Rect(0, 0, 950, 44)); } summaryBitmap.Render(summaryDrawing);
            var summaryEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); summaryEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(summaryBitmap)); using (var stream = File.Create(Path.Combine(root, "transfer-low-progress.png"))) summaryEncoder.Save(stream);
            networkJobs.Remove(screenshotSpeedJob); screenshotSpeedJob.Cancellation.Dispose(); RefreshCopySummary(); check(summaryTransferInfo.Visibility == Visibility.Collapsed, "network speed and ETA are hidden for local-only operations");
            ShowActivityDetails(); var parallel = AddCopyActivity("SFTP · souběžně", delegate { }); parallel.Update("Přenos", .6, .1);
            contrast.Finish("Dokončeno", true);
            check(copySummary.Visibility == Visibility.Visible && !copyActivities.Children.Contains(contrast) && copyActivities.Children.Contains(parallel), "finished activity disappears while parallel transfer remains visible");
            parallel.Finish("Dokončeno", true);
            check(copySummary.Visibility == Visibility.Collapsed && activityDetails == null, "last completion hides summary and closes details");
            check(operationJournal.Records.Contains(contrast.Record) && contrast.Record.State == "Dokončeno", "completion remains available in operation history");
            var queueJob = new NetworkTransferJob(new NetworkProfile { Name = "Test fronty", Protocol = NetworkProtocol.FTP, Host = "localhost" }, true);
            var queuePreview = new NetworkQueueWindow(this, delegate { return new[] { queueJob }; }, queueJob); queuePreview.Show(); queueJob.Finish("Dokončeno");
            DateTime queueLimit = DateTime.UtcNow.AddSeconds(3); while (queuePreview.IsVisible && DateTime.UtcNow < queueLimit) { Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); Thread.Sleep(5); }
            check(!queuePreview.IsVisible, "network queue closes when its last running job finishes"); queueJob.Cancellation.Dispose();
            ShowOperationHistory(); Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); historyWindow.UpdateLayout();
            var historyList = FluentUiTests.Children<ListBox>(historyWindow).Single(); historyList.SelectedItem = operationJournal.Records.First(x => x.Copy != null);
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); historyWindow.UpdateLayout();
            var historyContent = (FrameworkElement)historyWindow.Content;
            check(FluentUiTests.Children<Button>(historyWindow).All(x => x.ActualHeight > 0 && x.TransformToAncestor(historyContent).Transform(new Point(0, x.ActualHeight)).Y <= historyContent.ActualHeight), "history action buttons fit real window layout");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(historyContent.ActualWidth + 20), (int)(historyContent.ActualHeight + 20), 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(historyContent);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(root, "operation-history.png"))) encoder.Save(stream); historyWindow.Close();
            check(operationJournal.Records.Any(x => x.Items.Any(i => i.Source.EndsWith("locked.txt") && i.State == "Dokončeno")), "real copy operation contributes successful file detail to common history");
            var settings = new ProgramSettings(); Show(); UpdateLayout(); CaptureView(settings); Hide();
            check(settings.WindowWidth > 0 && settings.LeftView.Widths.Length == 5 && settings.SplitRatio > 0 && settings.SplitRatio < 1, "Save View captures actual window, splitter and panel columns");
            NavigationTests.Wait(operationJournal.PendingWrites);
        }
    }
}



