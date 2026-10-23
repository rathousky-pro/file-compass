using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal static class NetworkRelay
    {
        internal static void ValidateRelation(NetworkProfile source, NetworkProfile target, string path, string destination, bool directory)
        {
            bool sameServer = String.Equals(source.Host,target.Host,StringComparison.OrdinalIgnoreCase) && source.Port == target.Port && source.UserName == target.UserName && (source.Protocol == target.Protocol || (source.IsSshTransfer && target.IsSshTransfer));
            if (!sameServer) return;
            path = RemoteConnection.Normalize(path); destination = RemoteConnection.Normalize(destination);
            if (String.Equals(path,destination,StringComparison.OrdinalIgnoreCase) || (directory && destination.StartsWith(path.TrimEnd('/') + "/",StringComparison.OrdinalIgnoreCase))) throw new IOException(L.Text("transfer.sameOrNestedTargetRejected"));
        }
        internal static List<NetworkCopyBatch> Prepare(RemoteConnection source, NetworkProfile target, Entry[] entries, string destination, NetworkCopyRule rule, CancellationToken token)
        {
            var batches = new List<NetworkCopyBatch>();
            using (token.Register(delegate { source.Session.Abort(); })) {
                token.ThrowIfCancellationRequested(); source.OpenTransfer();
                foreach (var entry in entries) {
                    var batch = new NetworkCopyBatch { Entry = new Entry { FullPath = RemoteConnection.Normalize(entry.FullPath), IsDirectory = entry.IsDirectory }, Destination = RemoteConnection.Normalize(destination), Rule = rule };
                    Scan(source,target,batch.Entry.FullPath,batch.Destination,batch,token); batch.Entry.IsDirectory = batch.Items[0].Directory; batches.Add(batch);
                }
            } return batches;
        }
        static void Scan(RemoteConnection source, NetworkProfile target, string path, string parent, NetworkCopyBatch batch, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var info = source.Session.GetFileInfo(path); if (info.FileType == 'L') throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + path);
            string name = RemoteConnection.Name(path); FileOperations.ValidateName(name); string destination = RemoteConnection.Combine(parent,name); ValidateRelation(source.Profile,target,path,destination,info.IsDirectory);
            long bytes = info.IsDirectory ? 0 : checked(info.Length * 2);
            batch.Items.Add(new NetworkQueueItem { Source = path, Destination = destination, Directory = info.IsDirectory, Bytes = bytes, Relay = true, Modified = info.LastWriteTime, State = L.Code("state.queued"), Policy = batch.Rule == NetworkCopyRule.Resume ? L.Code("action.resumeTransfer") : batch.Rule == NetworkCopyRule.Overwrite ? L.Code("action.overwrite") : batch.Rule == NetworkCopyRule.Reject ? L.Text("conflict.reject") : L.Code("action.skip") }); batch.Bytes = checked(batch.Bytes + bytes);
            if (info.IsDirectory) foreach (var file in source.Session.ListDirectory(path).Files.Where(x => x.Name != "." && x.Name != "..")) Scan(source,target,file.FullName,destination,batch,token);
        }
        static bool Same(RemoteFileInfo a, RemoteFileInfo b) { return a == null ? b == null : b != null && a.FileType == b.FileType && a.Length == b.Length && a.LastWriteTime == b.LastWriteTime; }
        internal static NetworkCopyResult Run(NetworkTransferJob job, RemoteConnection source, RemoteConnection target)
        { return NetworkWork.Run(job,source,target); }
        internal static void StreamFile(NetworkTransferJob job, RemoteConnection source, RemoteConnection target, NetworkQueueItem item, RemoteFileInfo before, RemoteFileInfo previous, bool resume)
        {
            try {
            job.Progress(item.Source,0,0,resume ? L.Text("transfer.preparingResumeChecks") : L.Text("transfer.preparingStatus"),item.Destination);
            using (var lease = target.Session.PrepareRelay(source.Session,item.Source,item.Destination,before,previous,resume)) {
            var token = job.ItemToken; var timer = System.Diagnostics.Stopwatch.StartNew(); var progressGate = new object();
            long offset = lease.Offset; job.RelayResumeOffset = offset;
            job.RelayProbeBytes += source.Session.ResumeProbeBytes + target.Session.ResumeProbeBytes;
            job.RelayDownloaded = job.RelayUploaded = offset;
            Action<bool,long> report = delegate(bool upload,long bytes) {
                lock (progressGate) {
                    if (upload) Interlocked.Exchange(ref job.RelayUploaded,bytes); else Interlocked.Exchange(ref job.RelayDownloaded,bytes);
                    double processed = (double)job.RelayDownloaded + job.RelayUploaded;
                    job.Progress(item.Source,before.Length == 0 ? 1 : Math.Min(1,processed / (2.0 * before.Length)),(int)Math.Min(Int32.MaxValue,Math.Max(0,processed - 2.0 * offset) / Math.Max(.001,timer.Elapsed.TotalSeconds)),L.Text("transfer.relayDirectionLabel"),item.Destination);
                }
            };
            report(false,offset);
            using (var pipe = new RelayPipe(token)) {
                var producer = Task.Factory.StartNew(delegate {
                    try {
                        source.Session.DownloadRelay(item.Source,pipe,before.Length,delegate(long bytes) { report(false,bytes); },offset);
                        token.ThrowIfCancellationRequested(); if (!Same(before,source.Session.GetFileInfo(item.Source))) throw new IOException(L.Text("transfer.sourceChangedDuringTransfer"));
                        pipe.Complete();
                    } catch (Exception ex) { pipe.Complete(ex); throw; }
                },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
                try {
                    target.Session.UploadRelay(pipe,before.Length,before.LastWriteTime,item.Destination,previous,delegate(long bytes) { report(true,bytes); },delegate { producer.GetAwaiter().GetResult(); },delegate(Exception error) { pipe.Complete(error); source.Session.Abort(); },lease,delegate { return token.IsCancellationRequested && !job.KeepPartialOnCancel; });
                } catch (Exception ex) {
                    pipe.Complete(ex); source.Session.Abort();
                    try { producer.GetAwaiter().GetResult(); } catch { }
                    token.ThrowIfCancellationRequested(); throw;
                } finally { job.RelayBufferPeak = Math.Max(job.RelayBufferPeak,pipe.PeakBytes); }
            }
            }
            } catch {
                if (job.Cancellation.IsCancellationRequested && !job.KeepPartialOnCancel) job.PartialCleanupError = target.Session.DiscardRelayPartial(source.Session,item.Source,item.Destination);
                throw;
            }
        }
    }
    public partial class MainWindow
    {
        internal async Task<NetworkTransferJob> StartNetworkRelay(Entry[] entries, RemoteConnection source, RemoteConnection target, string destination, FilePanel targetPanel = null, NetworkCopyRule rule = NetworkCopyRule.Skip, bool move = false,DurableTransfer recovery = null)
        {
            entries = entries.Where(x => !x.IsParent).Select(x => new Entry { FullPath = x.FullPath, IsDirectory = x.IsDirectory, Bytes = x.Bytes }).ToArray();
            var durable = recovery ?? durableQueue.Begin("Relay",entries.Select(x => x.FullPath).ToArray(),destination,null,rule,move,source.Profile,target.Profile);
            var planner = source.TransferConnection(); var job = recovery == null ? networkJobs.AsEnumerable().Reverse().FirstOrDefault(x => x.Move == move && x.MatchesRelay(source,target) && x.Reserve()) : null;
            if (job == null) {
                job = new NetworkTransferJob(source.Profile,false,target.Profile) { Move = move }; job.Reserve(); networkJobs.Add(job); networkCopies++; var captured = job;
                job.ResolveError = delegate(CopyFailure failure) { captured.Cancellation.Token.ThrowIfCancellationRequested(); return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate { var error = new CopyErrorDialog(this,failure); error.ShowDialog(); return error.Choice; })); };
                job.Activity = AddCopyActivity(L.Text("transfer.serverRelayTitlePrefix") + job.Caption,job.Cancel,delegate { ShowNetworkQueue(captured); });
                job.Activity.ConfirmCancellation = delegate { return ConfirmNetworkCancellation(new[] { captured }); };
                job.Activity.Record.RetryInterrupted = true;
                job.Activity.Record.Retry = async delegate {
                    var failed = captured.SnapshotItems().Where(x => x.State.StartsWith(L.Code("state.error")) || x.State == L.Code("state.interrupted") || x.State == L.Code("state.cancelled")).ToArray();
                    foreach (var item in failed.Where(x => !failed.Any(d => d.Directory && d.Source != x.Source && x.Source.StartsWith(d.Source.TrimEnd('/') + "/",StringComparison.Ordinal))))
                        await StartNetworkRelay(new[] { new Entry { FullPath = item.Source, IsDirectory = item.Directory } },source,target,RemoteConnection.Parent(item.Destination),null,(captured.KeepPartialOnCancel || item.Policy == L.Code("action.resumeTransfer") || item.State.StartsWith(L.Code("state.error"))) ? NetworkCopyRule.Resume : item.Policy == L.Code("action.overwrite") ? NetworkCopyRule.Overwrite : NetworkCopyRule.Skip,captured.Move);
                };
                var updates = new Progress<NetworkCopyUpdate>(delegate(NetworkCopyUpdate update) { captured.Activity.Update(update.Text,update.File,update.Total,update.Preparing); });
                job.Changed = delegate { ((IProgress<NetworkCopyUpdate>)updates).Report(captured.Update()); };
                job.Completion = RunNetworkJob(job,source.TransferConnection(),target.TransferConnection());
            }
            AttachDurable(job,durable); ShowNetworkQueue(job);
            await job.QueuePlanning(delegate {
                Exception error = null;
                try { using (planner) { var prepared = recovery != null && recovery.Files.Count > 0 ? RecoveredBatches(recovery) : NetworkRetry.Plan(planner,delegate(RemoteConnection worker) { return NetworkRelay.Prepare(worker,target.Profile,entries,destination,rule,job.Cancellation.Token); },job); if (recovery == null || recovery.Files.Count == 0) PersistPrepared(durable,prepared); job.AddPrepared(prepared); } }
                catch (Exception ex) { error = ex; }
                finally { job.PlanningDone(error,String.Join("; ",entries.Select(x => x.FullPath)),destination,entries,rule); }
            });
            await job.Completion;
            if (targetPanel != null && targetPanel.Remote == target && targetPanel.CurrentPath == destination && !targetPanel.IsLoading) targetPanel.Refresh();
            return job;
        }
    }
}

