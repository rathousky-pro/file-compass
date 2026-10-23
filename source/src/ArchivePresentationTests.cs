using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CommanderCZ
{
    internal static class ArchivePresentationTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var checks = new List<string>(); MainWindow window = null;
            Action<bool,string> check = delegate(bool valid,string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                window = new MainWindow(root); window.Show(); NavigationTests.WaitPanels(window);
                NavigationTests.Wait(window.VerifyArchivePresentation(root, check));
                File.WriteAllLines(Path.Combine(root, "archive-presentation-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-presentation-result.txt"), "FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { if (window != null) window.Close(); }
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyArchivePresentation(string root, Action<bool,string> check)
        {
            foreach (string title in new[] { "Komprimování", "Extrahování" })
            using (var release = new ManualResetEventSlim(false)) {
                var task = ArchiveOperation(title, delegate(CancellationToken token, Action<string> progress) { progress(title == "Komprimování" ? new ArchiveCompressionProgress { Percent = 50, Original = 1000000, Packed = 125000 }.Encode() : "50%"); release.Wait(token); });
                try {
                    var dialog = Application.Current.Windows.OfType<ArchiveProgressDialog>().Single();
                    check(dialog.IsVisible && copySummary.Visibility == Visibility.Collapsed, title + " opens in its own progress window before backgrounding");
                    await Task.Delay(40);
                    if (title == "Komprimování") check(FluentUiTests.Children<TextBlock>(dialog).Any(x => x.Text.Contains("25,0 %")), "compression savings use processed bytes and remain separate from 50 percent progress");
                    FluentUiTests.Children<Button>(dialog).Single(x => (string)x.Content == "Minimalizovat").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    check(!dialog.IsVisible && copySummary.Visibility == Visibility.Visible, title + " enters bottom summary only after minimize");
                    ShowSummaryDetails(); check(dialog.IsVisible && copySummary.Visibility == Visibility.Collapsed, title + " bottom summary restores the same progress window");
                    NextStageTests.Draw(dialog, Path.Combine(root, title == "Komprimování" ? "compress-progress.png" : "extract-progress.png"));
                } finally { release.Set(); NavigationTests.Wait(task); }
                check(!Application.Current.Windows.OfType<ArchiveProgressDialog>().Any() && archiveOperations == 0, title + " closes progress window when work completes");
            }
            using (var release = new ManualResetEventSlim(false)) {
                var task = ArchiveOperation("Síťový archiv", delegate(CancellationToken token, Action<string> progress) { progress("25%"); release.Wait(token); }, null, true);
                try { check(copySummary.Visibility == Visibility.Visible && !Application.Current.Windows.OfType<ArchiveProgressDialog>().Any(x => x.IsVisible), "network archive starts directly in the bottom summary"); }
                finally { release.Set(); NavigationTests.Wait(task); }
            }
            var panel = FluentUiTests.Children<FilePanel>(this).First();
            var tab = FluentUiTests.Children<Button>(panel).Single(x => x.ToolTip is string && x.ToolTip.ToString().Contains("Nová záložka"));
            check(!tab.IsVisible, "single redundant path tab above drives is hidden");
            panel.NewTab(); UpdateLayout(); check(tab.IsVisible, "real multiple tabs remain available");
            var network = new NetworkTransferJob(new NetworkProfile { Protocol = NetworkProtocol.FTP, Host = "localhost" }, true);
            network.Activity = AddCopyActivity("Síťový přenos", network.Cancel); networkJobs.Add(network);
            try { ShowNetworkQueue(network); check(!network.Activity.Backgrounded && networkQueue != null && networkQueue.IsVisible, "network transfer opens its detailed queue immediately"); }
            finally { network.Finish("Dokončeno"); network.Activity.Finish("Dokončeno",true); networkJobs.Remove(network); network.Cancellation.Dispose(); }
        }
    }
}

