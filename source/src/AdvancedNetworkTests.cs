using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompass
{
    internal static partial class RelayTests
    {
        static NetworkTransferJob PlannedNormal(RemoteConnection remote,bool download,string source,string target,bool move,NetworkCopyRule rule)
        {
            var job = new NetworkTransferJob(remote.Profile,download) { Move = move }; job.Reserve();
            using (var planner = remote.TransferConnection()) job.AddPrepared(NetworkCopyWorker.Prepare(planner,new[] { new Entry { FullPath = source } },download,target,rule,job.Cancellation.Token));
            job.PlanningDone(null,"",target); return job;
        }
        static void RunNormal(NetworkTransferJob job,RemoteConnection remote)
        { using (var worker = remote.TransferConnection()) NetworkCopyWorker.Run(job,worker); job.Finish("Dokončeno"); }
        static void AdvancedChecks(RemoteConnection source,RemoteConnection target,RemoteConnection sftp,string directory,Action<bool,string> check)
        {
            string local = Path.Combine(directory,"advanced.txt"), output = Path.Combine(directory,"advanced-output.txt"); File.WriteAllText(local,"move contents");
            target.Session.CreateDirectory("/advanced-move"); var localMove = PlannedNormal(target,false,local,"/advanced-move",true,NetworkCopyRule.Overwrite); RunNormal(localMove,target);
            target.Session.GetFiles("/advanced-move/advanced.txt",output).Check(); check(!File.Exists(local) && File.ReadAllText(output) == "move contents","F6 local to FTP verifies transfer and deletes original through guarded file identity");
            var localFolder = Path.Combine(directory,"download-move"); Directory.CreateDirectory(localFolder); RunNormal(PlannedNormal(target,true,"/advanced-move/advanced.txt",localFolder,true,NetworkCopyRule.Overwrite),target);
            check(!target.Session.FileExists("/advanced-move/advanced.txt") && File.ReadAllText(Path.Combine(localFolder,"advanced.txt")) == "move contents","F6 FTP to local removes source only after guarded destination verification");
            File.WriteAllText(local,"remote move"); source.Session.PutFiles(local,"/move-cross.txt").Check(); var move = Planned(source,target,new[] { new Entry { FullPath = "/move-cross.txt" } },"/advanced-move",NetworkCopyRule.Overwrite); move.Move = true; Run(move,source,target);
            target.Session.GetFiles("/advanced-move/move-cross.txt",output).Check(); check(!source.Session.FileExists("/move-cross.txt") && File.ReadAllText(output) == "remote move","F6 between FTP servers preserves exact data and then removes source");
            source.Session.CreateDirectory("/same-server-target"); source.Session.PutFiles(local,"/same-server-move.txt").Check(); move = Planned(source,source,new[] { new Entry { FullPath = "/same-server-move.txt" } },"/same-server-target",NetworkCopyRule.Overwrite); move.Move = true; Run(move,source,source);
            check(!source.Session.FileExists("/same-server-move.txt") && source.Session.FileExists("/same-server-target/same-server-move.txt") && move.RelayBufferPeak == 0,"same-server F6 uses server rename without relaying file contents");
            source.Session.PutFiles(local,"/skip-move.txt").Check(); target.Session.PutFiles(local,"/advanced-move/skip-move.txt").Check(); move = Planned(source,target,new[] { new Entry { FullPath = "/skip-move.txt" } },"/advanced-move",NetworkCopyRule.Skip); move.Move = true; Run(move,source,target);
            check(source.Session.FileExists("/skip-move.txt"),"F6 skipped target never deletes source");
            target.Session.CreateDirectory("/cancel-move"); move = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/cancel-move",NetworkCopyRule.Overwrite); move.Move = true; move.KeepPartialOnCancel = true;
            move.Changed = delegate { if (!move.Cancellation.IsCancellationRequested && move.RelayUploaded > 524288) move.Cancel(); }; bool stopped = false; try { Run(move,source,target); } catch (OperationCanceledException) { stopped = true; }
            check(stopped && source.Session.GetFileInfo("/slow.bin").Length == 8388608 && !target.Session.FileExists("/cancel-move/slow.bin"),"cancelled F6 retains source and does not expose partial target");
            string tree = Path.Combine(directory,"move-tree"); Directory.CreateDirectory(Path.Combine(tree,"empty")); File.WriteAllText(Path.Combine(tree,"one.txt"),"one");
            RunNormal(PlannedNormal(sftp,false,tree,"/",true,NetworkCopyRule.Overwrite),sftp); check(!Directory.Exists(tree) && sftp.Session.FileExists("/move-tree/one.txt") && sftp.Session.GetFileInfo("/move-tree/empty").IsDirectory,"F6 folder to SFTP moves files and removes only empty source folders");
            File.WriteAllText(local,"original edit"); source.Session.PutFiles(local,"/edit-me.txt").Check(); using (var edit = new RemoteEditDocument(source,"/edit-me.txt",Path.Combine(directory,"edits"))) {
                edit.Download(CancellationToken.None); File.WriteAllText(edit.LocalPath,"edited locally"); edit.Save(CancellationToken.None); source.Session.GetFiles("/edit-me.txt",output).Check(); check(File.ReadAllText(output) == "edited locally","F4 saves edited local copy back through protected remote staging");
                File.WriteAllText(local,"newer remotely"); File.SetLastWriteTime(local,edit.Expected.LastWriteTime); source.Session.PutFiles(local,"/edit-me.txt").Check(); File.WriteAllText(edit.LocalPath,"my unsaved edit"); bool refused = false; try { edit.Save(CancellationToken.None); } catch (IOException) { refused = true; }
                source.Session.GetFiles("/edit-me.txt",output).Check(); check(refused && File.ReadAllText(output) == "newer remotely" && File.ReadAllText(edit.LocalPath) == "my unsaved edit","F4 detects changed remote content even with equal length and timestamp, preserving local edits");
            }
            var controlled = new NetworkTransferJob(source.Profile,false,target.Profile); controlled.BeginItem(new NetworkQueueItem()); controlled.Pause(true); var flow = new NetworkFlow(controlled); var wait = Task.Factory.StartNew(flow.Check); Thread.Sleep(150); check(!wait.IsCompleted,"pause blocks transfer worker without blocking UI thread"); controlled.Pause(false); check(wait.Wait(1000),"continue wakes a paused transfer worker"); controlled.EndItem();
            controlled.BeginItem(new NetworkQueueItem()); controlled.Limit(131072); var clock = System.Diagnostics.Stopwatch.StartNew(); flow = new NetworkFlow(controlled); flow.Consume(65536); check(clock.ElapsedMilliseconds >= 400 && clock.ElapsedMilliseconds < 2000,"speed limit throttles actual transferred bytes"); controlled.EndItem();
            target.Session.CreateDirectory("/automatic-retry"); File.WriteAllText(local,"arm connection drop"); source.Session.PutFiles(local,"/__drop_relay_once__").Check(); var retry = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" } },"/automatic-retry",NetworkCopyRule.Overwrite); bool reconnect = false;
            retry.Changed = delegate { if (retry.Update().Text.Contains("Obnova")) reconnect = true; }; Run(retry,source,target); target.Session.GetFiles("/automatic-retry/slow.bin",output).Check(); var bytes = File.ReadAllBytes(output);
            check(reconnect && retry.RelayResumeOffset > 0 && bytes.Length == 8388608 && bytes.Select((x,i) => x == (byte)(i % 256)).All(x => x),"real mid-file FTP drop reconnects automatically and resumes exact contents (reconnected=" + reconnect + ", offset=" + retry.RelayResumeOffset + ")");
            target.Session.CreateDirectory("/cancel-one"); var cancelOne = Planned(source,target,new[] { new Entry { FullPath = "/slow.bin" },new Entry { FullPath = "/tree" } },"/cancel-one",NetworkCopyRule.Overwrite); bool cancelled = false;
            cancelOne.Changed = delegate { if (!cancelled && cancelOne.RelayUploaded > 524288) { cancelled = true; cancelOne.CancelItem(cancelOne.SnapshotItems().First(x => x.Source == "/slow.bin")); } }; Run(cancelOne,source,target);
            check(cancelled && !cancelOne.Cancellation.IsCancellationRequested && cancelOne.SnapshotItems().Any(x => x.Source == "/slow.bin" && x.State == "Zrušeno") && target.Session.FileExists("/cancel-one/tree/český [soubor].txt"),"cancel one active file leaves job running and copies following files");
            var order = Planned(source,target,new[] { new Entry { FullPath = "/tree" } },"/advanced-move",NetworkCopyRule.Skip); var queued = order.SnapshotItems().First(x => !x.Directory); order.CancelItem(queued); check(order.SnapshotItems().Any(x => x.Source == queued.Source && x.State == "Zrušeno"),"cancel pending item changes queue without cancelling the job");
            source.Session.CreateDirectory("/sort-source"); foreach (string name in new[] { "a.txt","b.txt","c.txt" }) source.Session.PutFiles(local,"/sort-source/" + name).Check();
            order = Planned(source,target,new[] { new Entry { FullPath = "/sort-source" } },"/advanced-move",NetworkCopyRule.Overwrite); queued = order.SnapshotItems().Single(x => x.Source == "/sort-source/c.txt"); order.Reorder(queued,-1);
            var sequence = new System.Collections.Generic.List<string>(); order.Changed = delegate { foreach (var done in order.SnapshotItems().Where(x => !x.Directory && x.State == "Dokončeno")) if (!sequence.Contains(done.Source)) sequence.Add(done.Source); }; Run(order,source,target);
            check(sequence.SequenceEqual(new[] { "/sort-source/a.txt","/sort-source/c.txt","/sort-source/b.txt" }),"reordering pending files changes actual execution order");
            order = Planned(source,target,new[] { new Entry { FullPath = "/sort-source/a.txt" },new Entry { FullPath = "/sort-source/b.txt" } },"/advanced-move",NetworkCopyRule.Skip); order.Reorder(order.SnapshotItems().Single(x => x.Source == "/sort-source/b.txt"),-1);
            check(order.Take(CancellationToken.None).Entry.FullPath == "/sort-source/b.txt","reordering waiting roots changes queue order");
        }
    }
}

