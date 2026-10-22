using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    public sealed class NetworkCopyUpdate
    {
        public string Text;
        public double File, Total;
        public bool Preparing;
    }
    public static class NetworkCopyWorker
    {
        public static List<NetworkCopyBatch> Prepare(RemoteConnection connection, Entry[] entries, bool download, string destination, NetworkCopyRule rule, CancellationToken token)
        {
            var batches = new List<NetworkCopyBatch>();
            using (token.Register(delegate { try { connection.Session.Abort(); } catch { } }))
            {
                token.ThrowIfCancellationRequested(); connection.OpenTransfer();
                foreach (var entry in entries)
                {
                    var batch = new NetworkCopyBatch { Entry = entry, Destination = destination, Rule = rule };
                    Scan(connection, download, entry.FullPath, destination, batch, token);
                    batch.Entry.IsDirectory = batch.Items[0].Directory; batches.Add(batch);
                }
            }
            return batches;
        }
        static void Scan(RemoteConnection connection, bool download, string source, string destination, NetworkCopyBatch batch, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); bool directory; long bytes;
            if (download)
            {
                var info = connection.Session.GetFileInfo(source);
                if (info.FileType == 'L') throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + source);
                directory = info.IsDirectory; bytes = directory ? 0 : info.Length;
                FileOperations.ValidateName(RemoteConnection.Name(source));
            }
            else
            {
                var attributes = File.GetAttributes(source);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + source);
                directory = (attributes & FileAttributes.Directory) != 0; bytes = directory ? 0 : new FileInfo(source).Length;
            }
            string target = download ? Path.Combine(destination, RemoteConnection.Name(source)) : RemoteConnection.Combine(destination, Path.GetFileName(source));
            batch.Items.Add(new NetworkQueueItem { Source = source, Destination = target, Bytes = bytes, Modified = download ? connection.Session.GetFileInfo(source).LastWriteTime : File.GetLastWriteTime(source), Directory = directory, State = L.Code("state.queued"), Policy = batch.Rule == NetworkCopyRule.Resume ? L.Code("action.resumeTransfer") : batch.Rule == NetworkCopyRule.Overwrite ? L.Code("action.overwrite") : L.Code("action.skip") });
            batch.Bytes += bytes;
            if (!directory) return;
            if (download)
                foreach (var child in connection.Session.ListDirectory(source).Files)
                { if (child.Name != "." && child.Name != "..") Scan(connection, true, child.FullName, target, batch, token); }
            else foreach (var child in Directory.GetFileSystemEntries(source)) Scan(connection, false, child, target, batch, token);
        }
        public static NetworkCopyResult Run(NetworkTransferJob job, RemoteConnection connection)
        { return NetworkWork.Run(job,job.Download ? connection : null,job.Download ? null : connection); }
        public static NetworkCopyResult Run(RemoteConnection connection, Entry[] entries, bool download, string destination,
            CancellationToken token, IProgress<NetworkCopyUpdate> progress, NetworkCopyRule rule = NetworkCopyRule.Skip)
        {
            long totalBytes = 0, completedBytes = 0, currentBytes = 0;
            var fileSizes = new Dictionary<string, long>(StringComparer.Ordinal);
            var result = new NetworkCopyResult();
            connection.Session.FileTransferred += delegate(object sender, TransferEventArgs e) { if (e.Error == null) completedBytes += e.Length; };
            int completed = 0;
            // Register progress before the independent transfer session is opened.
            connection.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e)
            {
                e.Cancel = token.IsCancellationRequested;
                long fileBytes; fileSizes.TryGetValue(e.FileName, out fileBytes);
                double fraction = totalBytes > 0 ? (completedBytes + fileBytes * e.FileProgress) / totalBytes : (completed + e.OverallProgress) / entries.Length;
                progress.Report(new NetworkCopyUpdate { File = e.FileProgress, Total = fraction,
                    Text = connection.Profile.Protocol + " · " + (e.Phase == null ? "" : e.Phase + " · ") + e.FileName + L.Text("copy.currentFilePrefix") + (100 * e.FileProgress).ToString("F0") + L.Text("progress.percentAndTotalSeparator") + (100 * fraction).ToString("F0") + "% · " + (e.CPS / 1048576.0).ToString("F2") + " MB/s" });
            };
            token.ThrowIfCancellationRequested();
            progress.Report(new NetworkCopyUpdate { Text = L.Text("transfer.connectingStatus"), Preparing = true });
            using (token.Register(delegate { try { connection.Session.Abort(); } catch { } }))
            {
                try
                {
                    connection.OpenTransfer(); token.ThrowIfCancellationRequested();
                    progress.Report(new NetworkCopyUpdate { Text = L.Text("transfer.calculatingSize"), Preparing = true });
                    var sizes = new List<long>();
                    foreach (var entry in entries)
                    {
                        token.ThrowIfCancellationRequested(); long bytes = 0;
                        if (download)
                        {
                            if (entry.IsDirectory)
                                foreach (var file in connection.Session.EnumerateRemoteFiles(entry.FullPath, null, CommanderCZ.NetworkTransport.EnumerationOptions.AllDirectories))
                                { token.ThrowIfCancellationRequested(); if (file.FileType == 'L') throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + file.FullName); bytes += file.Length; fileSizes[file.FullName] = file.Length; }
                            else { bytes = connection.Session.GetFileInfo(entry.FullPath).Length; fileSizes[entry.FullPath] = bytes; }
                        }
                        else bytes = LocalSize(entry.FullPath, token, fileSizes);
                        sizes.Add(bytes); totalBytes += bytes;
                    }
                    for (int i = 0; i < entries.Length; i++)
                    {
                        token.ThrowIfCancellationRequested(); currentBytes = sizes[i];
                        long before = completedBytes;
                        NetworkTransfers.Copy(entries[i], download ? connection : null, download ? null : connection, destination, rule, result,
                            delegate(long bytes) { completedBytes += bytes; }, token.ThrowIfCancellationRequested);
                        completedBytes = before + currentBytes; completed++;
                        progress.Report(new NetworkCopyUpdate { Text = L.Text("transfer.transferredPrefix") + completed + " / " + entries.Length + L.Text("common.itemCountSuffix"), File = 1,
                            Total = totalBytes > 0 ? (double)completedBytes / totalBytes : (double)completed / entries.Length });
                    }
                }
                catch { token.ThrowIfCancellationRequested(); throw; }
            }
            return result;
        }
        static long LocalSize(string path, CancellationToken token, Dictionary<string, long> sizes)
        {
            token.ThrowIfCancellationRequested(); var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + path);
            if ((attributes & FileAttributes.Directory) == 0) { long bytes = new FileInfo(path).Length; sizes[path] = bytes; return bytes; }
            long size = 0; foreach (var child in Directory.GetFileSystemEntries(path)) size += LocalSize(child, token, sizes); return size;
        }
    }
}


