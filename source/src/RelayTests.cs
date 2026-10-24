using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    internal static partial class RelayTests
    {
        static void PipeChecks(Action<bool,string> check)
        {
            using (var memory = new MemoryStream()) {
                var writer = new RelayLengthWriter(memory,1); bool refused = false;
                try { writer.Write(new byte[2],0,2); } catch (IOException) { refused = true; }
                check(refused && memory.Length == 0,"growing source is rejected before extra bytes can deadlock the pipe");
            }
            byte[] bytes = Enumerable.Range(0,RelayPipe.Capacity * 3 + 71).Select(i => (byte)(i % 251)).ToArray();
            using (var pipe = new RelayPipe(CancellationToken.None)) {
                var writer = Task.Factory.StartNew(delegate { for (int i = 0; i < bytes.Length; i += 7919) pipe.Write(bytes,i,Math.Min(7919,bytes.Length - i)); pipe.Complete(); },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                using (var result = new MemoryStream()) { var buffer = new byte[997]; int count; while ((count = pipe.Read(buffer,0,buffer.Length)) > 0) result.Write(buffer,0,count); writer.GetAwaiter().GetResult(); check(result.ToArray().SequenceEqual(bytes),"pipe handles fragmented chunks and ring wrap without byte loss"); }
                check(pipe.PeakBytes <= RelayPipe.Capacity,"pipe applies bounded backpressure");
            }
            foreach (bool writing in new[] { false,true }) using (var cancellation = new CancellationTokenSource()) using (var pipe = new RelayPipe(cancellation.Token)) {
                if (writing) pipe.Write(new byte[RelayPipe.Capacity],0,RelayPipe.Capacity);
                var blocked = Task.Factory.StartNew(delegate { try { if (writing) pipe.Write(new byte[1],0,1); else pipe.Read(new byte[1],0,1); return false; } catch (OperationCanceledException) { return true; } },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                cancellation.Cancel(); check(blocked.Wait(2000) && blocked.Result,"cancellation wakes a blocked " + (writing ? "producer" : "consumer"));
            }
            foreach (bool writing in new[] { false,true }) using (var pipe = new RelayPipe(CancellationToken.None)) {
                if (writing) pipe.Write(new byte[RelayPipe.Capacity],0,RelayPipe.Capacity);
                var blocked = Task.Factory.StartNew(delegate { try { if (writing) pipe.Write(new byte[1],0,1); else pipe.Read(new byte[1],0,1); return false; } catch (IOException) { return true; } },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                pipe.Complete(new IOException("test endpoint failed")); check(blocked.Wait(2000) && blocked.Result,"endpoint failure wakes a blocked " + (writing ? "producer" : "consumer"));
            }
        }
        internal static RemoteConnection Open(int port, bool ssh = false, bool scp = false)
        {
            var connection = new RemoteConnection(new NetworkProfile { Protocol = scp ? NetworkProtocol.SCP : ssh ? NetworkProtocol.SFTP : NetworkProtocol.FTP, Host = "127.0.0.1", Port = port, Name = scp ? "SCP cíl" : ssh ? "SFTP cíl" : "FTP " + port, UserName = "tester", Path = "/" });
            var options = connection.Options("test-password"); if (ssh || scp) { options.SshHostKeyFingerprint = connection.Session.ScanFingerprint(options,"SHA-256"); connection.Profile.HostKeyFingerprint = options.SshHostKeyFingerprint; }
            connection.Open(options); return connection;
        }
        static NetworkTransferJob Planned(RemoteConnection source, RemoteConnection target, Entry[] entries, string destination, NetworkCopyRule rule)
        {
            var job = new NetworkTransferJob(source.Profile,false,target.Profile); job.Reserve();
            using (var planner = source.TransferConnection()) job.AddPrepared(NetworkRelay.Prepare(planner,target.Profile,entries,destination,rule,job.Cancellation.Token));
            job.PlanningDone(null,"",destination); return job;
        }
        static void Run(NetworkTransferJob job, RemoteConnection source, RemoteConnection target)
        { using (var a = source.TransferConnection()) using (var b = target.TransferConnection()) NetworkRelay.Run(job,a,b); job.Finish("Dokončeno"); }
        internal static int Run(string directory, int first, int second, int third, int fourth = 0)
        {
            Directory.CreateDirectory(directory); var checks = new List<string>(); Action<bool,string> check = delegate(bool valid,string message) { if (!valid) throw new Exception(message); checks.Add(message); };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try { PipeChecks(check); using (var source = Open(first)) using (var target = Open(second)) using (var sftp = Open(third,true)) {
                var math = new NetworkTransferJob(source.Profile,false,target.Profile);
                var metricsBatch = new NetworkCopyBatch { Entry = new Entry { FullPath = "/same" }, Destination = "/", Bytes = 20 };
                metricsBatch.Items.Add(new NetworkQueueItem { Source = "/same", Destination = "/first", Bytes = 10, Relay = true, State = "Ve frontě" }); metricsBatch.Items.Add(new NetworkQueueItem { Source = "/same", Destination = "/second", Bytes = 10, Relay = true, State = "Ve frontě" });
                math.AddPrepared(new[] { metricsBatch }); math.Progress("/same",.5,100,"Stahování ze zdroje","/first"); check(math.Update().Total == .25,"relay progress accounts for both legs"); math.RelayFileDone("/same","/first",false); math.Progress("/same",.5,100,"Stahování ze zdroje","/second"); check(math.Update().Total == .75,"same source with different destinations has separate progress accounting");
                var dialog = new NetworkCopyDialog(null,1,source.Display("/tree"),target.Display("/relay"),source.Profile,target.Profile); NetworkTests.Render(dialog,Path.Combine(directory,"relay-dialog.png")); check(FluentUiTests.Children<System.Windows.Controls.Button>(dialog).Single(x => (string)x.Content == "Kopírovat a navázat").IsEnabled,"relay dialog enables safe resume and displays both endpoints"); dialog.Close();
                string input = Path.Combine(directory,"český [soubor].txt"); File.WriteAllText(input,"Příliš žluťoučký kůň"); source.Session.CreateDirectory("/tree"); source.Session.CreateDirectory("/tree/empty"); source.Session.PutFiles(input,"/tree/český [soubor].txt").Check();
                target.Session.CreateDirectory("/relay"); sftp.Session.CreateDirectory("/relay");
                var job = Planned(source,target,new[] { new Entry { FullPath = "/tree", IsDirectory = true } },"/relay",NetworkCopyRule.Skip);
                check(job.TotalBytes == new FileInfo(input).Length * 2,"relay total includes download plus upload while queue file size remains original");
                check(job.SnapshotItems().Single(x => !x.Directory).Size == CopyUi.Bytes(new FileInfo(input).Length),"queue displays actual source file size");
                Run(job,source,target); string output = Path.Combine(directory,"received.txt"); target.Session.GetFiles("/relay/tree/český [soubor].txt",output).Check();
                check(File.ReadAllText(output) == File.ReadAllText(input) && target.Session.GetFileInfo("/relay/tree/empty").IsDirectory,"FTP to other FTP copies Czech literal names and empty subfolders");
                check(source.Session.FileExists("/tree/český [soubor].txt") && job.SnapshotItems().All(x => x.State == "Dokončeno"),"source stays intact and all relay queue items finish");
                File.WriteAllText(input,"changed source"); source.Session.PutFiles(input,"/tree/český [soubor].txt").Check();
                Run(Planned(source,target,new[] { new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Skip),source,target); target.Session.GetFiles("/relay/tree/český [soubor].txt",output).Check();
                check(File.ReadAllText(output) != "changed source","skip policy preserves existing target file");
                Run(Planned(source,target,new[] { new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Overwrite),source,target); target.Session.GetFiles("/relay/tree/český [soubor].txt",output).Check(); check(File.ReadAllText(output) == "changed source","overwrite policy replaces existing target after complete staging");
                Run(Planned(source,sftp,new[] { new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Skip),source,sftp); sftp.Session.GetFiles("/relay/tree/český [soubor].txt",output).Check(); check(File.ReadAllText(output) == "changed source","FTP to SFTP uses same relay pipeline and pinned SSH identity");
                source.Session.CreateDirectory("/back"); Run(Planned(sftp,source,new[] { new Entry { FullPath = "/relay/tree" } },"/back",NetworkCopyRule.Skip),sftp,source); check(source.Session.GetFileInfo("/back/tree/empty").IsDirectory,"SFTP to FTP copies the directory tree back");
                if (fourth > 0) using (var scp = Open(fourth,false,true)) {
                    scp.Session.CreateDirectory("/relay"); Run(Planned(source,scp,new[] { new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Skip),source,scp); scp.Session.GetFiles("/relay/tree/český [soubor].txt",output).Check(); check(File.ReadAllText(output) == "changed source","FTP to SCP uses pinned shell transfer and preserves literal names");
                    source.Session.CreateDirectory("/scp-back"); Run(Planned(scp,source,new[] { new Entry { FullPath = "/relay/tree" } },"/scp-back",NetworkCopyRule.Skip),scp,source); check(source.Session.GetFileInfo("/scp-back/tree/empty").IsDirectory,"SCP to FTP uses same directory relay pipeline");
                }
                bool invalid = false; try { NetworkRelay.ValidateRelation(source.Profile,source.Profile,"/tree","/tree/copy",true); } catch (IOException) { invalid = true; } check(invalid,"copying a directory inside itself on one server is rejected");
                using (var planner = source.TransferConnection()) check(NetworkRelay.Prepare(planner,target.Profile,new[] { new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Resume,CancellationToken.None).Count == 1,"relay resume plans recursive directory transfers");
                File.WriteAllText(input,"first"); source.Session.PutFiles(input,"/changed-plan.txt").Check(); var changed = Planned(source,target,new[] { new Entry { FullPath = "/changed-plan.txt" } },"/relay",NetworkCopyRule.Overwrite); File.WriteAllText(input,"different size after planning"); source.Session.PutFiles(input,"/changed-plan.txt").Check(); invalid = false;
                try { Run(changed,source,target); } catch (IOException) { invalid = true; } check(invalid && !target.Session.FileExists("/relay/changed-plan.txt"),"changed source after planning is rejected before target write");
                File.WriteAllText(input,"original target"); target.Session.PutFiles(input,"/relay/slow.bin").Check();
                var streamed = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/relay",NetworkCopyRule.Overwrite); bool overlap = false;
                streamed.Changed = delegate { if (Interlocked.Read(ref streamed.RelayUploaded) > 0 && Interlocked.Read(ref streamed.RelayDownloaded) < 8388608) overlap = true; };
                Run(streamed,source,target); target.Session.GetFiles("/relay/slow.bin",output).Check(); var received = File.ReadAllBytes(output);
                check(overlap,"target uploads before the complete source file has downloaded");
                check(streamed.RelayBufferPeak > 0 && streamed.RelayBufferPeak <= RelayPipe.Capacity,"real relay uses at most 512 KiB in its bounded memory pipe");
                check(received.Length == 8388608 && received.Select((x,i) => x == (byte)(i % 256)).All(x => x),"streamed eight MiB file is byte exact");
                File.WriteAllText(input,""); source.Session.PutFiles(input,"/empty-file").Check(); Run(Planned(source,target,new[] { new Entry { FullPath = "/empty-file" } },"/relay",NetworkCopyRule.Overwrite),source,target);
                check(target.Session.GetFileInfo("/relay/empty-file").Length == 0,"streaming supports empty files");
                File.WriteAllText(input,"original target"); target.Session.PutFiles(input,"/relay/slow.bin").Check();
                foreach (bool duringUpload in new[] { false,true }) {
                    var cancelJob = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/relay",NetworkCopyRule.Overwrite);
                    cancelJob.Changed = delegate { if (!cancelJob.Cancellation.IsCancellationRequested && (duringUpload ? Interlocked.Read(ref cancelJob.RelayUploaded) : Interlocked.Read(ref cancelJob.RelayDownloaded)) > 524288) cancelJob.Cancel(); };
                    bool cancelled = false; try { Run(cancelJob,source,target); } catch (OperationCanceledException) { cancelled = true; } target.Session.GetFiles("/relay/slow.bin",output).Check();
                    check(cancelled && File.ReadAllText(output) == "original target" && source.Session.GetFileInfo("/slow.bin").Length == 8388608,"cancelling relay " + (duringUpload ? "upload" : "download") + " preserves source and previous target");
                    check(source.Session.ListDirectory("/").Files.Count > 0 && target.Session.ListDirectory("/").Files.Count > 0,"cancellation leaves both browsing sessions usable");
                    check(!target.Session.ListDirectory("/relay").Files.Any(x => x.Name.StartsWith(".FileCompass-relay-")),"reachable target cleans relay staging after discard cancellation");
                }
                using (var worker = target.TransferConnection()) {
                    worker.OpenTransfer(); var expected = worker.Session.GetFileInfo("/relay/slow.bin"); invalid = false;
                    using (var memory = new MemoryStream(new byte[65536])) try { worker.Session.UploadRelay(memory,memory.Length,DateTime.Now,"/relay/slow.bin",expected,delegate(long count) { },delegate { throw new IOException("source changed after final byte"); },delegate(Exception error) { }); } catch (IOException) { invalid = true; }
                    target.Session.GetFiles("/relay/slow.bin",output).Check(); check(invalid && File.ReadAllText(output) == "original target","source validation failure after upload prevents final target commit");
                    check(!target.Session.ListDirectory("/relay").Files.Any(x => x.Name.StartsWith(".FileCompass-upload-")),"source validation failure cleans the uploaded staging file");
                }
                target.Session.CreateDirectory("/failure-retry"); File.WriteAllText(input,"fault armed"); target.Session.PutFiles(input,"/__fail_relay_upload_once__").Check();
                var retry = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/failure-retry",NetworkCopyRule.Overwrite); int failures = 0;
                retry.ResolveError = delegate(CopyFailure error) { failures++; return failures == 1 ? CopyErrorChoice.Retry : CopyErrorChoice.Cancel; };
                Run(retry,source,target); check(failures == 1 && target.Session.GetFileInfo("/failure-retry/slow.bin").Length == 8388608,"target upload failure joins producer and retry uses fresh sessions successfully");
                File.WriteAllText(input,"fault armed"); source.Session.PutFiles(input,"/__fail_relay_download_once__").Check();
                var skipFailed = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" },new Entry { FullPath = "/tree" } },"/relay",NetworkCopyRule.Overwrite); failures = 0;
                skipFailed.ResolveError = delegate(CopyFailure error) { failures++; return CopyErrorChoice.Skip; };
                Run(skipFailed,source,target); target.Session.GetFiles("/relay/slow.bin",output).Check();
                check(failures == 1 && File.ReadAllText(output) == "original target" && skipFailed.SnapshotItems().Any(x => x.Source == "/tree/český [soubor].txt" && x.State == "Dokončeno"),"source download failure preserves original target and skip continues with fresh sessions");
                var raced = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/relay",NetworkCopyRule.Overwrite); bool edited = false;
                raced.Changed = delegate { if (!edited && raced.Update().Text.Contains("Nahrávání do cíle") && raced.Update().File > .6) { edited = true; File.WriteAllText(input,"edited while upload was running"); target.Session.PutFiles(input,"/relay/slow.bin").Check(); } };
                invalid = false; try { Run(raced,source,target); } catch (IOException) { invalid = true; } target.Session.GetFiles("/relay/slow.bin",output).Check(); check(edited && invalid && File.ReadAllText(output) == "edited while upload was running","target edited during relay upload is preserved instead of overwritten");
                target.Session.CreateDirectory("/live");
                ResumeChecks(source,target,sftp,directory,first,second,fourth,check);
                AdvancedChecks(source,target,sftp,directory,check);
                NextRemoteTests.Run(source,target,sftp,directory,check);
                var main = new MainWindow(directory) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false }; main.Show(); NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.VerifyRelayQueue(source,target,directory,check)); main.Close();
            } File.WriteAllLines(Path.Combine(directory,"relay-result.txt"),checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory,"relay-result.txt"),"FAIL after " + checks.Count + " checks\n" + String.Join("\n",checks) + "\n" + ex); return 1; }
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyRelayQueue(RemoteConnection source, RemoteConnection target, string directory, Action<bool,string> check)
        {
            var copying = StartNetworkRelay(new[] { new Entry { FullPath = "/slow.bin" } },source,target,"/live");
            var deadline = DateTime.UtcNow.AddSeconds(20); int ticks = 0;
            while (networkJobs.Count == 0 || networkJobs.Last().TotalBytes == 0) { if (copying.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Relay did not start"); await Task.Delay(20); ticks++; }
            var job = networkJobs.Last();
            check(networkQueue != null && networkQueue.IsVisible && !job.Activity.Backgrounded && copySummary.Visibility == Visibility.Collapsed,"copy opens foreground progress and stays out of bottom queue initially");
            networkQueue.WindowState = WindowState.Minimized;
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate { }));
            check(!networkQueue.IsVisible && job.Activity.Backgrounded && copySummary.Visibility == Visibility.Visible,"minimizing progress moves running transfer to bottom queue");
            ShowSummaryDetails(); check(networkQueue != null && activityDetails == null,"bottom transfer opens the detailed network queue directly"); networkQueue.Close();
            ChooseRelayStop(job,"Pokračovat v přenosu"); check(!job.Cancellation.IsCancellationRequested && !job.Activity.Record.CancelRequested,"continue in stop dialog leaves transfer and activity running");
            var append = StartNetworkRelay(new[] { new Entry { FullPath = "/tree" } },source,target,"/live");
            while (job.Planning > 0) { await Task.Delay(20); ticks++; }
            check(networkJobs.Count == 1 && job.TotalBytes > 16777216,"new F5 to same server pair expands the existing queue and byte total");
            var duplicate = StartNetworkRelay(new[] { new Entry { FullPath = "/tree" } },source,target,"/live"); long size = job.TotalBytes;
            while (job.Planning > 0) await Task.Delay(20); check(size == job.TotalBytes,"duplicate relay roots do not inflate totals");
            left.Navigate(directory,false); right.Navigate(directory,false);
            while (!copying.IsCompleted) { await Task.Delay(25); ticks++; }
            await Task.WhenAll(copying,append,duplicate);
            check(ticks > 10 && target.Session.GetFileInfo("/live/slow.bin").Length == 8388608 && target.Session.FileExists("/live/tree/český [soubor].txt"),"responsive UI and navigation retain fixed remote relay destination");
            check(job.SnapshotItems().All(x => x.State == "Dokončeno") && networkCopies == 0,"one relay job finishes in common queue and operation history");
            NavigationTests.Wait(operationJournal.PendingWrites);
            target.Session.CreateDirectory("/ui-interrupted"); var interruptedCopy = StartNetworkRelay(new[] { new Entry { FullPath = "/slow.bin" } },source,target,"/ui-interrupted");
            var stopDeadline = DateTime.UtcNow.AddSeconds(20); var stopped = networkJobs.Last();
            while (Interlocked.Read(ref stopped.RelayUploaded) <= 524288) { if (interruptedCopy.IsCompleted || DateTime.UtcNow > stopDeadline) throw new Exception("UI interruption did not reach upload"); await Task.Delay(20); }
            ChooseRelayStop(stopped,"Přerušit a ponechat pro navázání"); await interruptedCopy;
            check(stopped.KeepPartialOnCancel && stopped.Activity.Record.State == "Přerušeno" && target.Session.ListDirectory("/ui-interrupted").Files.Any(x => x.Name.StartsWith(".FileCompass-relay-")),"UI keep choice persists partial and records interrupted history state");
            ShowOperationHistory(); var records = FluentUiTests.Children<System.Windows.Controls.ListBox>(historyWindow).Single(); records.SelectedItem = stopped.Activity.Record;
            check(FluentUiTests.Children<System.Windows.Controls.Button>(historyWindow).Single(x => (string)x.Content == "Opakovat / navázat").IsEnabled,"history enables resume for the interrupted relay"); historyWindow.Close();
            await StartNetworkRelay(new[] { new Entry { FullPath = "/slow.bin" } },source,target,"/ui-interrupted",null,NetworkCopyRule.Resume);
            check(networkJobs.Last().RelayResumeOffset > 0 && networkJobs.Last().SnapshotItems().All(x => x.State == "Dokončeno"),"new UI F5 resumes interrupted data through the common queue");
            NavigationTests.Wait(operationJournal.PendingWrites);
            using (var localControl = new CopyControl()) {
                var dialog = new CopyProgressDialog(this,localControl); copyProgress = dialog;
                var activity = AddCopyActivity("Místní kopírování",localControl.Cancel,ShowCopyProgress); activity.Backgrounded = false;
                dialog.BecameBackground = delegate { activity.Backgrounded = true; RefreshCopySummary(); }; dialog.BecameForeground = delegate { activity.Backgrounded = false; RefreshCopySummary(); };
                dialog.Show(); RefreshCopySummary(); check(dialog.IsVisible && copySummary.Visibility == Visibility.Collapsed,"local copy progress also starts in its own foreground window");
                dialog.WindowState = WindowState.Minimized; Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate { })); check(!dialog.IsVisible && copySummary.Visibility == Visibility.Visible,"local progress minimizes into bottom queue"); ShowSummaryDetails(); check(dialog.IsVisible && !activity.Backgrounded && copySummary.Visibility == Visibility.Collapsed,"bottom queue restores local progress and removes its bottom indicator");
                dialog.Finish(); activity.Finish("Dokončeno",true); copyProgress = null;
            }
        }
        void ChooseRelayStop(NetworkTransferJob job,string label)
        {
            bool chosen = false; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += delegate { var dialog = OwnedWindows.OfType<NetworkStopDialog>().FirstOrDefault(); if (dialog == null) return; timer.Stop(); chosen = true; FluentUiTests.Children<System.Windows.Controls.Button>(dialog).Single(x => (string)x.Content == label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); };
            timer.Start(); try { StopNetworkJobs(new[] { job }); } finally { timer.Stop(); }
            if (!chosen) throw new Exception("Stop choice dialog was not opened");
        }
    }
}

