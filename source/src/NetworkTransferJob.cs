using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompass
{
    public sealed class NetworkQueueItem
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public string State { get; set; }
        public long Bytes { get; set; }
        public bool Directory { get; set; }
        public bool Relay { get; set; }
        internal bool ResumeIfAvailable;
        public DateTime Modified { get; set; }
        public string Size { get { return Directory ? "<DIR>" : CopyUi.Bytes(Relay ? Bytes / 2 : Bytes); } }
        public string Policy { get; set; }
        public NetworkQueueItem Snapshot() { return (NetworkQueueItem)MemberwiseClone(); }
    }
    public sealed class NetworkCopyBatch
    {
        public Entry Entry;
        public string Destination;
        public NetworkCopyRule Rule;
        public long Bytes;
        public readonly List<NetworkQueueItem> Items = new List<NetworkQueueItem>();
    }
    internal sealed class NetworkTransferMetrics
    {
        internal long BytesPerSecond;
        internal double? RemainingSeconds;
    }
    public sealed partial class NetworkTransferJob
    {
        internal long RelayDownloaded, RelayUploaded;
        readonly Stopwatch transferClock = new Stopwatch();
        readonly TransferRateEstimate remainingEstimate = new TransferRateEstimate();
        long measuredTraffic;
        internal void MeasureTraffic(int bytes) { if(bytes>0)System.Threading.Interlocked.Add(ref measuredTraffic,bytes); }
        internal readonly List<DurableTransfer> DurableRequests = new List<DurableTransfer>();
        internal Action<NetworkQueueItem> PersistDone;
        internal int RelayBufferPeak;
        internal long RelayResumeOffset, RelayProbeBytes;
        internal volatile bool KeepPartialOnCancel;
        internal string PartialCleanupError;
        readonly object gate = new object();
        readonly Queue<NetworkCopyBatch> waiting = new Queue<NetworkCopyBatch>();
        readonly List<NetworkQueueItem> items = new List<NetworkQueueItem>();
        readonly Dictionary<string, long> sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
        int planning;
        Task planningTask = Task.FromResult(true);
        bool accepting = true, finished;
        long totalBytes, doneBytes;
        double fileFraction;
        string current = "", phase = L.Text("transfer.preparingStatus");
        string currentKey = "";
        int speed;
        long lastProgressNotification;
        public readonly NetworkProfile Profile;
        public readonly NetworkProfile RelayTarget;
        public readonly bool Download;
        public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        public Task Completion;
        public Action Changed;
        public CopyActivity Activity;
        public Func<CopyFailure, CopyErrorChoice> ResolveError;
        bool skipErrors;
        public CopyErrorChoice HandleError(CopyFailure failure)
        {
            Cancellation.Token.ThrowIfCancellationRequested();
            var choice = skipErrors ? CopyErrorChoice.Skip : ResolveError(failure); if (choice == CopyErrorChoice.SkipAll) skipErrors = true;
            if (choice == CopyErrorChoice.Skip || choice == CopyErrorChoice.SkipAll)
            {
                lock (gate) { var item = items.FirstOrDefault(x => x.Source == failure.Source && (x.State == L.Code("state.queued") || x.State == L.Code("state.copying"))); if (item != null) { item.State = L.Code("error.messagePrefix") + failure.Error.Message; doneBytes += item.Bytes; } }
                Notify();
            }
            return choice;
        }
        public NetworkTransferJob(NetworkProfile profile, bool download, NetworkProfile relayTarget = null) { Profile = profile.Clone(); Download = download; RelayTarget = relayTarget == null ? null : relayTarget.Clone(); }
        public bool Matches(RemoteConnection connection, bool download)
        {
            var p = connection.Profile;
            return RelayTarget == null && Download == download && SameProfile(Profile,p);
        }
        static bool SameProfile(NetworkProfile a, NetworkProfile b) { return a.Id == b.Id && a.Protocol == b.Protocol && a.Host == b.Host && a.Port == b.Port && a.UserName == b.UserName && a.HostKeyFingerprint == b.HostKeyFingerprint && a.PrivateKeyPath == b.PrivateKeyPath && a.FtpTls == b.FtpTls && a.FtpImplicitTls == b.FtpImplicitTls; }
        public bool MatchesRelay(RemoteConnection source, RemoteConnection target) { return RelayTarget != null && SameProfile(Profile,source.Profile) && SameProfile(RelayTarget,target.Profile); }
        public string Caption { get { return Profile.Protocol + " · " + Profile.Name + (RelayTarget == null ? "" : L.Text("common.routeArrow") + RelayTarget.Protocol + " · " + RelayTarget.Name); } }
        public bool Reserve()
        {
            lock (gate) { if (!accepting || finished || Cancellation.IsCancellationRequested) return false; planning++; return true; }
        }
        public Task QueuePlanning(Action action)
        {
            lock (gate) { planningTask = planningTask.ContinueWith(delegate { action(); }, TaskScheduler.Default); return planningTask; }
        }
        public Task PlanningCompletion { get { lock (gate) return planningTask; } }
        public void AddPrepared(IEnumerable<NetworkCopyBatch> batches)
        {
            lock (gate)
            {
                foreach (var batch in batches)
                {
                    string key = batch.Entry.FullPath + "\0" + (Download ? System.IO.Path.GetFullPath(batch.Destination).ToUpperInvariant() : batch.Destination);
                    if (!roots.Add(key)) continue;
                    waiting.Enqueue(batch); totalBytes += batch.Bytes;
                    foreach (var item in batch.Items) { items.Add(item); if (!item.Directory) sizes[RelayTarget == null ? item.Source : item.Source + "\0" + item.Destination] = item.Bytes; }
                }
            }
            Notify();
        }
        public void PlanningDone(Exception error, string source, string destination, Entry[] original = null, NetworkCopyRule rule = NetworkCopyRule.Skip)
        {
            lock (gate)
            {
                if (error != null)
                {
                    if (original == null) items.Add(new NetworkQueueItem { Source = source, Destination = destination, State = L.Code("queue.loadFailedPrefix") + error.Message });
                    else foreach (var entry in original) items.Add(new NetworkQueueItem { Source = entry.FullPath, Destination = Download ? System.IO.Path.Combine(destination, RemoteConnection.Name(entry.FullPath)) : RemoteConnection.Combine(destination, System.IO.Path.GetFileName(entry.FullPath)), Directory = entry.IsDirectory, Bytes = entry.Bytes, State = error is OperationCanceledException ? L.Code("state.cancelled") : L.Code("queue.loadFailedPrefix") + error.Message, Policy = rule == NetworkCopyRule.Resume ? L.Code("action.resumeTransfer") : rule == NetworkCopyRule.Overwrite ? L.Code("action.overwrite") : L.Code("action.skip") });
                }
                planning--; Monitor.PulseAll(gate);
            }
            Notify();
        }
        public NetworkCopyBatch Take(CancellationToken token)
        {
            lock (gate)
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (waiting.Count > 0) return waiting.Dequeue();
                    if (planning == 0) { accepting = false; return null; }
                    phase = L.Text("queue.loadingAddedItems"); Monitor.Wait(gate, 100);
                }
            }
        }
        public void Begin(NetworkCopyBatch batch)
        {
            lock (gate) { phase = L.Code("copy.title"); current = batch.Entry.FullPath; fileFraction = 0;
                foreach (var item in batch.Items) if (item.Directory) item.State = L.Code("state.copying"); }
            Notify();
        }
        public void Progress(string source, double fraction, int cps, string transferPhase = null, string destination = null)
        {
            bool notify;
            lock (gate)
            {
                current = source; currentKey = destination == null ? source : source + "\0" + destination; fileFraction = fraction; speed = cps; phase = transferPhase ?? L.Code("copy.title");
                var item = items.FirstOrDefault(x => x.Source == source && (destination == null || x.Destination == destination) && x.State == L.Code("state.queued")); if (item != null) item.State = L.Code("state.copying");
                // Fast transfers can report thousands of blocks per second. Keep
                // accurate state, but do not fill the UI dispatcher with updates.
                long now = Stopwatch.GetTimestamp();
                notify = now - lastProgressNotification >= Stopwatch.Frequency / 10 || fraction >= 1;
                if (notify) lastProgressNotification = now;
            }
            if (notify) Notify();
        }
        public void FileDone(string source, bool skipped)
        { CompleteFile(source, null, skipped); }
        public void RelayFileDone(string source, string destination, bool skipped)
        { CompleteFile(source, destination, skipped); }
        void CompleteFile(string source, string destination, bool skipped)
        {
            NetworkQueueItem completed = null;
            lock (gate)
            {
                var item = items.FirstOrDefault(x => !x.Directory && x.Source == source && (destination == null || x.Destination == destination) && (x.State == L.Code("state.queued") || x.State == L.Code("state.copying")));
                if (item != null) { item.State = skipped ? L.Code("state.skipped") : L.Code("state.completed"); doneBytes += item.Bytes; completed = item.Snapshot(); }
                fileFraction = 0;
            }
            if (completed != null && PersistDone != null) PersistDone(completed); Notify();
        }
        public void RelayDirectoryDone(NetworkQueueItem item) { lock (gate) item.State = L.Code("state.completed"); Notify(); }
        public CopyErrorChoice RelayError(NetworkQueueItem item, Exception error)
        {
            Cancellation.Token.ThrowIfCancellationRequested(); if (ResolveError == null) throw error;
            var choice = skipErrors ? CopyErrorChoice.Skip : ResolveError(new CopyFailure { Source = item.Source, Target = item.Destination, Error = error });
            if (choice == CopyErrorChoice.SkipAll) skipErrors = true;
            if (choice == CopyErrorChoice.Skip || choice == CopyErrorChoice.SkipAll) { lock (gate) { item.State = L.Code("error.messagePrefix") + error.Message; doneBytes += item.Bytes; fileFraction = 0; } Notify(); }
            return choice;
        }
        public void BatchDone(NetworkCopyBatch batch)
        {
            lock (gate)
            {
                foreach (var item in batch.Items)
                    if (item.State == L.Code("state.queued") || item.State == L.Code("state.copying")) { item.State = item.Directory ? L.Code("state.completed") : L.Code("state.skipped"); if (!item.Directory) doneBytes += item.Bytes; }
            }
            if (PersistDone != null) foreach (var item in batch.Items.Where(x => x.Directory && x.State == L.Code("state.completed"))) PersistDone(item); Notify();
        }
        public void Finish(string state)
        {
            lock (gate)
            {
                accepting = false; finished = true; phase = state; fileFraction = 0;
                foreach (var item in items) if (item.State == L.Code("state.copying") || item.State == L.Code("state.queued")) item.State = state;
            }
            Notify();
        }
        public void Cancel()
        {
            lock (gate) { if (finished) return; phase = L.Text("transfer.cancellingTitle"); }
            Cancellation.Cancel(); Notify();
        }
        public bool Finished { get { lock (gate) return finished; } }
        public long TotalBytes { get { lock (gate) return totalBytes; } }
        public int Planning { get { lock (gate) return planning; } }
        public NetworkQueueItem[] SnapshotItems() { lock (gate) return items.Select(x => x.Snapshot()).ToArray(); }
        internal NetworkTransferMetrics Metrics()
        {
            lock (gate)
            {
                long size; sizes.TryGetValue(currentKey, out size);
                long remaining = Math.Max(0, totalBytes - doneBytes - (long)(size * Math.Max(0, Math.Min(1, fileFraction))));
                bool transferring = !Paused && !finished && !Cancellation.IsCancellationRequested && (phase == L.Code("copy.title") || (RelayTarget != null && (phase.StartsWith(L.Text("transfer.downloading")) || phase.StartsWith(L.Text("transfer.uploading")))));
                long rate = transferring ? Math.Max(0, speed) : 0;
                return new NetworkTransferMetrics { BytesPerSecond = rate,
                    RemainingSeconds = !Paused && !finished && !Cancellation.IsCancellationRequested && planning == 0 && totalBytes > 0 ? remainingEstimate.Remaining(System.Threading.Interlocked.Read(ref measuredTraffic), transferClock.Elapsed.TotalSeconds, remaining) : null };
            }
        }
        public NetworkCopyUpdate Update()
        {
            lock (gate)
            {
                long size; sizes.TryGetValue(currentKey, out size);
                long processed = Math.Min(totalBytes, doneBytes + (long)(size * fileFraction));
                double total = totalBytes > 0 ? (double)processed / totalBytes : finished ? 1 : 0;
                var active=items.FirstOrDefault(x=>x.Source==current&&(RelayTarget==null||currentKey==x.Source+"\0"+x.Destination));
                return new NetworkCopyUpdate { File = fileFraction, Total = total, Preparing = totalBytes == 0 && !finished,
                    CurrentSource=current,CurrentDestination=active==null?"":active.Destination,Phase=Paused?L.Code("state.paused"):phase,BytesDone=processed,TotalBytes=totalBytes,
                    FilesDone=items.Count(x=>!x.Directory&&(x.State==L.Code("state.completed")||x.State==L.Code("state.skipped"))),TotalFiles=items.Count(x=>!x.Directory),
                    Text = Caption + " · " + (Move ? L.Text("transfer.moveStatusPrefix") : "") + (Paused ? L.Code("state.paused") : phase) + " · " + current + L.Text("copy.currentFilePrefix") + (100 * fileFraction).ToString("F0") + L.Text("progress.percentAndTotalSeparator") + (100 * total).ToString("F0") + "% · " + CopyUi.Bytes(processed) + " / " + CopyUi.Bytes(totalBytes) + " · " + ((Paused ? 0 : speed) / 1048576.0).ToString("F2") + " MB/s" + (planning > 0 ? L.Text("queue.calculatingAddedItemsSuffix") : "") };
            }
        }
        public string Summary { get { lock (gate) { long size; sizes.TryGetValue(currentKey, out size); long processed = Math.Min(totalBytes, doneBytes + (long)(size * fileFraction)); return Caption + " · " + (RelayTarget != null ? L.Text("transfer.streamingThroughMemory") : Download ? L.Text("transfer.downloading") : L.Text("transfer.uploading")) + " · " + phase + " · " + CopyUi.Bytes(processed) + " / " + CopyUi.Bytes(totalBytes) + (planning > 0 ? L.Text("queue.loadingAddedItemsSuffix") : ""); } } }
        public override string ToString() { return Summary; }
        void Notify() { var changed = Changed; if (changed != null) changed(); }
    }
}
