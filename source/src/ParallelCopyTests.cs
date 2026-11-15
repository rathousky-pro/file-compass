#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FileCompass
{
    internal static class ParallelCopyTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); MainWindow window = null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
                string target = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
                foreach (string name in new[] { "first.txt", "second.txt", "queued.txt" }) File.WriteAllText(Path.Combine(source, name), name);
                window = new MainWindow(source, new ProgramSettings { AutoRefresh = false }) { Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
                window.Show(); NavigationTests.WaitPanels(window);
                window.VerifyParallelLocalCopies(source, target);
                window.Close(); window = null;
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS: parallel copies, independent cancellation and queued operation"); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL: " + ex); return 1; }
            finally { if (window != null) window.Close(); }
        }
    }
    public partial class MainWindow
    {
        internal void VerifyParallelLocalCopies(string source, string target)
        {
            QueueExternalCopy(new[] { Path.Combine(source, "first.txt") }, new CopyOptions { Target = target, Rule = CopyRule.OverwriteAll });
            if (activeCopyJobs.Count != 1) throw new Exception("First copy did not start");
            var first = activeCopyJobs.Single().Value; first.Item2.TogglePause();
            QueueExternalCopy(new[] { Path.Combine(source, "second.txt") }, new CopyOptions { Target = target, Rule = CopyRule.OverwriteAll });
            QueueExternalCopy(new[] { Path.Combine(source, "queued.txt") }, new CopyOptions { Target = target, Rule = CopyRule.OverwriteAll, Queue = true });
            if (activeCopyJobs.Count != 2 || copyJobs.Count != 1) throw new Exception("Parallel and queued jobs were not separated");
            using (var gate = new ManualResetEventSlim(false)) {
                var archive1 = ArchiveOperation("archive one", delegate(CancellationToken token, Action<string> progress) { gate.Wait(token); }, startBackground:true);
                var archive2 = ArchiveOperation("archive two", delegate(CancellationToken token, Action<string> progress) { gate.Wait(token); }, startBackground:true);
                if (archiveOperations != 2 || activeCopyJobs.Count != 2) throw new Exception("Archive and copy operations did not start independently");
                gate.Set(); NavigationTests.Wait(Task.WhenAll(archive1, archive2));
                if (!archive1.Result || !archive2.Result || archiveOperations != 0) throw new Exception("Concurrent archive operations did not complete");
            }
            PanelWorkflowTests.Until(delegate { return File.Exists(Path.Combine(target, "second.txt")) && activeCopyJobs.Count == 1; });
            if (File.Exists(Path.Combine(target, "first.txt")) || File.Exists(Path.Combine(target, "queued.txt"))) throw new Exception("Paused or queued operation started early");
            first.Item1.Cancel();
            PanelWorkflowTests.Until(delegate { return !copyRunning; });
            if (!File.Exists(Path.Combine(target, "queued.txt")) || !File.Exists(Path.Combine(target, "second.txt"))) throw new Exception("Cancelling one operation affected other jobs");
            NavigationTests.Wait(operationJournal.PendingWrites);
        }
    }
}
#endif
