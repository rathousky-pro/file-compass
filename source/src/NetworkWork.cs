using System;
using System.IO;
using System.Linq;
using System.Threading;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    internal static class NetworkWork
    {
        internal static bool Same(RemoteFileInfo a,RemoteFileInfo b) { return a.FileType == b.FileType && a.Length == b.Length && a.LastWriteTime == b.LastWriteTime; }
        static void Attach(RemoteConnection connection,NetworkTransferJob job,bool report)
        {
            if (connection == null) return; connection.Session.Flow = new NetworkFlow(job);
            if (report) connection.Session.FileTransferProgress += delegate(object s,FileTransferProgressEventArgs e) { e.Cancel = job.ItemToken.IsCancellationRequested; job.Progress(e.FileName,e.FileProgress,e.CPS,e.Phase); };
        }
        static void Reopen(RemoteConnection source,RemoteConnection target,NetworkTransferJob job)
        { if (source != null) { source.ReopenTransfer(job.ItemToken); Attach(source,job,job.RelayTarget == null); } if (target != null) { target.ReopenTransfer(job.ItemToken); Attach(target,job,job.RelayTarget == null); } }
        internal static NetworkCopyResult Run(NetworkTransferJob job,RemoteConnection source,RemoteConnection target)
        {
            var result = new NetworkCopyResult(); var global = job.Cancellation.Token;
            Action abort = delegate { if (source != null) source.Session.Abort(); if (target != null) target.Session.Abort(); };
            using (global.Register(abort)) {
                NetworkCopyBatch batch = job.Take(global); if (batch == null) return result;
                int opening = 0;
                while (true) try {
                    if (opening == 0) { Attach(source,job,job.RelayTarget == null); Attach(target,job,job.RelayTarget == null); if (source != null) source.OpenTransfer(); if (target != null) target.OpenTransfer(); }
                    else Reopen(source,target,job);
                    break;
                } catch (Exception ex) { global.ThrowIfCancellationRequested(); if (!NetworkRetry.Transient(ex) || ++opening > 3) throw; global.WaitHandle.WaitOne(1000 * opening); }
                do {
                    job.Begin(batch); NetworkQueueItem item;
                    while ((item = job.NextItem(batch)) != null) {
                        global.ThrowIfCancellationRequested(); job.BeginItem(item); bool copied = false, skipped = false, retryResume = batch.Rule == NetworkCopyRule.Resume; int attempt = 0; RemoteFileInfo final = null;
                        LocalMoveGuard localGuard = null;
                        using (job.ItemToken.Register(abort)) try {
                            while (true) try {
                                job.ItemToken.ThrowIfCancellationRequested();
                                if (item.Directory) {
                                    if (item.ResumeIfAvailable && (source == null ? !Directory.Exists(item.Source) : !source.Session.FileExists(item.Source))) { job.RelayDirectoryDone(item); break; }
                                    if (source != null) { var folder = source.Session.GetFileInfo(item.Source); if (!folder.IsDirectory || folder.FileType == 'L') throw new IOException(L.Text("transfer.sourceFolderChanged")); }
                                    else if ((File.GetAttributes(item.Source) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.sourceFolderIsLink"));
                                    EnsureDirectory(item,job,target); job.RelayDirectoryDone(item); break;
                                }
                                if (!copied) {
                                    ValidateSource(item,job,source);
                                    if (job.Move && !job.Download && source == null && localGuard == null) localGuard = new LocalMoveGuard(item.Source,item.Bytes,item.Modified);
                                    var rule = retryResume ? NetworkCopyRule.Resume : batch.Rule;
                                    if (item.ResumeIfAvailable) {
                                        bool partial = source != null && target != null ? target.Session.HasRelayCheckpoint(source.Session,item.Source,item.Destination) : source != null ? source.Session.HasCheckpoint(false,item.Source,item.Destination) : target.Session.HasCheckpoint(true,item.Source,item.Destination);
                                        if (partial) rule = NetworkCopyRule.Resume;
                                    }
                                    if(target!=null&&!String.IsNullOrEmpty(target.Profile.CloudAccountId)&&rule==NetworkCopyRule.Resume)rule=batch.Rule==NetworkCopyRule.Resume?NetworkCopyRule.Skip:batch.Rule; if (source != null && target != null) {
                                        var before = source.Session.GetFileInfo(item.Source); var previous = target.Session.FileExists(item.Destination) ? target.Session.GetFileInfo(item.Destination) : null;
                                        if (before.Length != item.Bytes / 2 || before.LastWriteTime != item.Modified) throw new IOException(L.Text("transfer.sourceChangedSinceQueueLoad"));
                                        if (previous != null && (previous.IsDirectory || previous.FileType == 'L')) throw new IOException(L.Text("transfer.targetNotRegularFile"));
                                        bool partial = rule == NetworkCopyRule.Resume && target.Session.HasRelayCheckpoint(source.Session,item.Source,item.Destination);
                                        skipped = previous != null && (rule == NetworkCopyRule.Skip || (rule == NetworkCopyRule.Resume && !partial && previous.Length == before.Length));
                                        if (!skipped) {
                                            if (previous != null && rule == NetworkCopyRule.Reject) throw new IOException(L.Text("transfer.targetAlreadyExists"));
                                            bool sameServer = source.Profile.Protocol == target.Profile.Protocol && String.Equals(source.Profile.Host,target.Profile.Host,StringComparison.OrdinalIgnoreCase) && source.Profile.Port == target.Profile.Port && source.Profile.UserName == target.Profile.UserName && source.Profile.HostKeyFingerprint == target.Profile.HostKeyFingerprint;
                                            if (job.Move && previous == null && sameServer && !target.Session.HasRelayCheckpoint(source.Session,item.Source,item.Destination)) source.Session.MoveFile(item.Source,item.Destination);
                                            else NetworkRelay.StreamFile(job,source,target,item,before,previous,rule == NetworkCopyRule.Resume);
                                        }
                                    } else {
                                        var copiedResult = new NetworkCopyResult();
                                        NetworkTransfers.Copy(new Entry { FullPath = item.Source, IsDirectory = false },source,target,job.Download ? Path.GetDirectoryName(item.Destination) : RemoteConnection.Parent(item.Destination),rule,copiedResult,null,job.ItemToken.ThrowIfCancellationRequested);
                                        skipped = copiedResult.Skipped > 0;
                                    }
                                    copied = !skipped;
                                    if (copied && target != null) { final = target.Session.GetFileInfo(item.Destination); if (final.IsDirectory || final.FileType == 'L' || final.Length != item.Bytes / (item.Relay ? 2 : 1)) throw new IOException(L.Text("transfer.destinationCopySizeMismatch")); }
                                }
                                if (job.Move && copied) {
                                    job.ItemToken.ThrowIfCancellationRequested();
                                    if (target != null && !Same(final,target.Session.GetFileInfo(item.Destination))) throw new IOException(L.Text("transfer.changedDestinationSourcePreserved"));
                                    if (source != null) {
                                        if (target == null) {
                                            if ((File.GetAttributes(item.Destination) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.targetReplacedByLink"));
                                            using (var destination = new FileStream(item.Destination,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                                                if (destination.Length != item.Bytes || File.GetLastWriteTime(item.Destination) != item.Modified) throw new IOException(L.Text("transfer.changedTargetSourcePreserved"));
                                                if (source.Session.FileExists(item.Source)) source.Session.RemoveVerifiedFile(item.Source,item.Bytes,item.Modified);
                                            }
                                        } else if (source.Session.FileExists(item.Source)) source.Session.RemoveVerifiedFile(item.Source,item.Bytes / 2,item.Modified);
                                    } else { if (localGuard == null) throw new IOException(L.Text("transfer.sourceNotLockedForMove")); localGuard.Delete(); }
                                }
                                job.RelayFileDone(item.Source,item.Destination,skipped); if (skipped) result.Skipped++; else result.Copied++; break;
                            } catch (Exception ex) {
                                global.ThrowIfCancellationRequested(); if (job.ItemToken.IsCancellationRequested) { job.ItemCancelled(item); result.Skipped++; break; }
                                bool recovered = false;
                                while (!copied && NetworkRetry.Transient(ex) && attempt < 3) {
                                    attempt++; job.RetryStatus(item,attempt); if (job.ItemToken.WaitHandle.WaitOne(1000 * attempt)) job.ItemToken.ThrowIfCancellationRequested();
                                    try { Reopen(source,target,job); retryResume = true; recovered = true; break; } catch (Exception reconnect) { ex = reconnect; }
                                }
                                if (recovered) continue;
                                if (item.Directory) throw;
                                var choice = job.RelayError(item,ex);
                                if (choice == CopyErrorChoice.Cancel) { job.KeepPartialOnCancel = true; throw new OperationCanceledException(); }
                                if (choice == CopyErrorChoice.Retry) { Reopen(source,target,job); retryResume = true; continue; }
                                Reopen(source,target,job); result.Skipped++; break;
                            }
                        } catch (OperationCanceledException) { global.ThrowIfCancellationRequested(); if (job.ItemToken.IsCancellationRequested) { job.ItemCancelled(item); result.Skipped++; } else throw; }
                        finally { if (localGuard != null) localGuard.Dispose(); job.EndItem(); }
                        if (!global.IsCancellationRequested && (source != null || target != null)) {
                            // A cancelled item closes only its workers, not the whole job.
                            if (item.State == L.Code("state.cancelled")) Reopen(source,target,job);
                        }
                    }
                    if (job.Move) RemoveEmptySources(batch,job,source);
                    job.BatchDone(batch);
                } while ((batch = job.Take(global)) != null);
            } return result;
        }
        static void ValidateSource(NetworkQueueItem item,NetworkTransferJob job,RemoteConnection source)
        {
            long size = item.Bytes / (item.Relay ? 2 : 1);
            if (source != null) { var info = source.Session.GetFileInfo(item.Source); if (info.IsDirectory || info.FileType == 'L' || info.Length != size || info.LastWriteTime != item.Modified) throw new IOException(L.Text("transfer.sourceChangedSinceQueueLoad")); }
            else if ((File.GetAttributes(item.Source) & FileAttributes.ReparsePoint) != 0 || new FileInfo(item.Source).Length != size || File.GetLastWriteTime(item.Source) != item.Modified) throw new IOException(L.Text("transfer.sourceChangedSinceQueueLoad"));
        }
        static void EnsureDirectory(NetworkQueueItem item,NetworkTransferJob job,RemoteConnection target)
        {
            if (target == null) { if (File.Exists(item.Destination) || (Directory.Exists(item.Destination) && (File.GetAttributes(item.Destination) & FileAttributes.ReparsePoint) != 0)) throw new IOException(L.Text("transfer.unsafeDestinationFolder")); Directory.CreateDirectory(item.Destination); }
            else if (!target.Session.FileExists(item.Destination)) target.Session.CreateDirectory(item.Destination);
            else { var info = target.Session.GetFileInfo(item.Destination); if (!info.IsDirectory || info.FileType == 'L') throw new IOException(L.Text("transfer.targetNotRegularFolder")); }
        }
        static void RemoveEmptySources(NetworkCopyBatch batch,NetworkTransferJob job,RemoteConnection source)
        {
            foreach (var folder in batch.Items.Where(x => x.Directory).OrderByDescending(x => x.Source.Length)) {
                job.Cancellation.Token.ThrowIfCancellationRequested();
                if (batch.Items.Any(x => x.Source.StartsWith(folder.Source.TrimEnd('/','\\') + (source == null ? "\\" : "/"),source == null ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && x.State != L.Code("state.completed"))) continue;
                try { if (source == null) { if (Directory.Exists(folder.Source) && (File.GetAttributes(folder.Source) & FileAttributes.ReparsePoint) == 0 && !Directory.EnumerateFileSystemEntries(folder.Source).Any()) Directory.Delete(folder.Source,false); } else if (source.Session.FileExists(folder.Source)) source.Session.RemoveEmptyDirectory(folder.Source); }
                catch (IOException ex) { folder.State = L.Code("transfer.sourceFolderPreservedErrorPrefix") + ex.Message; }
            }
        }
    }
}

