using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace CommanderCZ
{
    internal sealed class TransientNetworkException : IOException { internal TransientNetworkException(string message) : base(message) { } }
    internal static class NetworkRetry
    {
        internal static System.Collections.Generic.List<NetworkCopyBatch> Plan(RemoteConnection template,Func<RemoteConnection,System.Collections.Generic.List<NetworkCopyBatch>> scan,NetworkTransferJob job)
        {
            int attempt = 0; var token = job.Cancellation.Token;
            while (true) {
                token.ThrowIfCancellationRequested();
                try { using (var worker = template.TransferConnection()) return scan(worker); }
                catch (Exception ex) { token.ThrowIfCancellationRequested(); if (!Transient(ex) || ++attempt > 3) throw;
                    job.Progress("",0,0,L.Text("queue.reloadingPrefix") + attempt + "/3"); if (token.WaitHandle.WaitOne(1000 * attempt)) token.ThrowIfCancellationRequested();
                }
            }
        }
        internal static bool Transient(Exception ex)
        { for (var e = ex; e != null; e = e.InnerException) if (e is TransientNetworkException || e is TimeoutException || e is System.Net.Sockets.SocketException) return true; return false; }
    }
    internal sealed class NetworkFlow
    {
        readonly NetworkTransferJob job; readonly Stopwatch timer = Stopwatch.StartNew(); long charged; int version = -1;
        internal NetworkFlow(NetworkTransferJob transfer) { job = transfer; }
        internal void Check()
        {
            var token = job.ItemToken; token.ThrowIfCancellationRequested();
            while (job.Paused) { if (token.WaitHandle.WaitOne(50)) token.ThrowIfCancellationRequested(); }
        }
        internal void Consume(int bytes)
        {
            Check(); int current = job.FlowVersion; if (version != current) { version = current; charged = 0; timer.Restart(); }
            long rate = job.SpeedLimit; if (rate == 0) return; charged += bytes;
            while (1000.0 * charged / rate > timer.Elapsed.TotalMilliseconds) { Check(); if (version != job.FlowVersion) { version = job.FlowVersion; charged = 0; timer.Restart(); return; } job.ItemToken.WaitHandle.WaitOne(25); }
        }
    }
    public sealed partial class NetworkTransferJob
    {
        internal bool Move;
        volatile bool paused; long speedLimit; int flowVersion;
        CancellationTokenSource fileCancellation; NetworkQueueItem activeItem;
        internal bool Paused { get { return paused; } }
        internal long SpeedLimit { get { return Interlocked.Read(ref speedLimit); } }
        internal int FlowVersion { get { return Volatile.Read(ref flowVersion); } }
        internal CancellationToken ItemToken { get { lock (gate) return fileCancellation == null ? Cancellation.Token : fileCancellation.Token; } }
        internal void Pause(bool value) { paused = value; Interlocked.Increment(ref flowVersion); Notify(); }
        internal void Limit(long bytesPerSecond) { if (bytesPerSecond < 0) throw new ArgumentOutOfRangeException(); Interlocked.Exchange(ref speedLimit,bytesPerSecond); Interlocked.Increment(ref flowVersion); Notify(); }
        internal void BeginItem(NetworkQueueItem item) { lock (gate) { activeItem = item; fileCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation.Token); } }
        internal void EndItem() { lock (gate) { activeItem = null; if (fileCancellation != null) fileCancellation.Dispose(); fileCancellation = null; } }
        internal NetworkQueueItem NextItem(NetworkCopyBatch batch)
        { lock (gate) return batch.Items.Where(x => x.State == L.Code("state.queued") || x.State == L.Code("state.copying")).OrderByDescending(x => x.Directory).ThenBy(x => items.IndexOf(x)).FirstOrDefault(); }
        internal void Reorder(NetworkQueueItem selected,int direction)
        {
            lock (gate) {
                var item = items.FirstOrDefault(x => x.Source == selected.Source && x.Destination == selected.Destination); if (item == null || item.Directory || item.State != L.Code("state.queued")) return;
                var choices = items.Where(x => !x.Directory && x.State == L.Code("state.queued")).ToArray(); int n = Array.IndexOf(choices,item), other = n + direction;
                if (other < 0 || other >= choices.Length) return;
                var batch = waiting.FirstOrDefault(x => x.Items.Contains(item)); var adjacent = waiting.FirstOrDefault(x => x.Items.Contains(choices[other]));
                if ((batch == null) != (adjacent == null)) return;
                if (batch != adjacent) {
                    var roots = waiting.ToList(); int a = roots.IndexOf(batch), b = roots.IndexOf(adjacent); roots[a] = adjacent; roots[b] = batch;
                    var old = items.ToList(); var queued = roots.SelectMany(x => x.Items).ToArray(); items.Clear(); items.AddRange(old.Where(x => !queued.Contains(x))); foreach (var root in roots) items.AddRange(root.Items.OrderBy(x => old.IndexOf(x)));
                    waiting.Clear(); foreach (var root in roots) waiting.Enqueue(root);
                } else { int a = items.IndexOf(item), b = items.IndexOf(choices[other]); items[a] = choices[other]; items[b] = item; }
            } Notify();
        }
        internal void CancelItem(NetworkQueueItem selected)
        {
            lock (gate) {
                var item = items.FirstOrDefault(x => x.Source == selected.Source && x.Destination == selected.Destination); if (item == null) return;
                var selectedItems = item.Directory ? items.Where(x => x == item || (x.Source.StartsWith(item.Source.TrimEnd('/','\\') + (Download || RelayTarget != null ? "/" : "\\"),Download || RelayTarget != null ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) && x.Destination.StartsWith(item.Destination.TrimEnd('/','\\') + (Download ? "\\" : "/"),Download ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))).ToArray() : new[] { item };
                foreach (var pending in selectedItems) {
                    if (pending == activeItem) { fileCancellation.Cancel(); continue; }
                    if (pending.State == L.Code("state.queued") || (pending.Directory && pending.State == L.Code("state.copying"))) { pending.State = L.Code("state.cancelled"); doneBytes += pending.Bytes; }
                }
            } Notify();
        }
        internal void ItemCancelled(NetworkQueueItem item) { lock (gate) { item.State = L.Code("state.cancelled"); doneBytes += item.Bytes; fileFraction = 0; } Notify(); }
        internal void RetryStatus(NetworkQueueItem item,int attempt)
        { Progress(item.Source,0,0,L.Text("transfer.reconnectingPrefix") + attempt + "/3",RelayTarget == null ? null : item.Destination); Notify(); }
    }
}

