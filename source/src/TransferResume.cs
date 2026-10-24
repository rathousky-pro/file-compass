using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FileCompass.NetworkTransport
{
    public sealed partial class Session
    {
        internal static string OwnedStagePath(string expected, string saved) {
            int marker = expected.LastIndexOf(".FileCompass-", StringComparison.Ordinal);
            if (marker >= 0 && saved == expected.Substring(0, marker) + ".CommanderCZ-" + expected.Substring(marker + ".FileCompass-".Length)) return saved;
            return expected;
        }
        internal long ResumeProbeBytes { get; private set; }
        internal bool HasCheckpoint(bool upload, string source, string target)
        {
            using (var checkpoint = new TransferCheckpoint(options, upload, upload ? Path.GetFullPath(source) : RemoteConnection.Normalize(source), upload ? RemoteConnection.Normalize(target) : Path.GetFullPath(target))) {
                if (checkpoint.Data.Stage != null) return true;
                if (upload) {
                    string folder = RemoteConnection.Parent(target);
                    foreach (string prefix in new[] { ".FileCompass-upload-", ".CommanderCZ-upload-" }) {
                        string stage = RemoteConnection.Combine(folder, prefix + checkpoint.Data.Identity + ".part");
                        if (FileExists(stage)) throw new IOException(L.Text("resume.unownedPartialPathPrefix") + stage);
                    }
                }
                return false;
            }
        }
        void LocalTarget(TransferCheckpoint checkpoint, string path, bool validate, string stage = null, long length = 0, DateTime modified = default(DateTime))
        {
            TransferCheckpoint.RejectLink(path); bool exists = File.Exists(path); var old = exists ? new FileInfo(path) : null;
            if (Directory.Exists(path)) throw new IOException(L.Text("resume.targetIsFolderPrefix") + path);
            if (validate) checkpoint.ValidateTarget(exists, exists ? old.Length : 0, exists ? old.LastWriteTime : DateTime.MinValue);
            else checkpoint.Initialize(stage, length, modified, null, exists, exists ? old.Length : 0, exists ? old.LastWriteTime : DateTime.MinValue);
        }
        void RemoteTarget(TransferCheckpoint checkpoint, string path, bool validate, string stage = null, long length = 0, DateTime modified = default(DateTime), string hash = null)
        {
            bool exists = FileExists(path); var old = exists ? GetFileInfo(path) : null;
            if (exists && (old.IsDirectory || old.FileType == 'L')) throw new IOException(L.Text("transfer.targetNotRegularFilePrefix") + path);
            if (validate) checkpoint.ValidateTarget(exists, exists ? old.Length : 0, exists ? old.LastWriteTime : DateTime.MinValue);
            else checkpoint.Initialize(stage, length, modified, hash, exists, exists ? old.Length : 0, exists ? old.LastWriteTime : DateTime.MinValue);
        }
        TransferResult DownloadCheckpointed(string remote, string local, bool remove, TransferOptions transfer)
        {
            var info = GetFileInfo(remote); if (info.IsDirectory || info.FileType == 'L') throw new IOException(L.Text("transfer.regularSourceFileRequiredPrefix") + remote);
            string target = RemoteConnection.DownloadTarget(local, info.Name);
            using (var checkpoint = new TransferCheckpoint(options, false, RemoteConnection.Normalize(remote), target))
            {
                string stageFolder = Path.Combine(Path.GetDirectoryName(target), ".FileCompass-partials");
                TransferCheckpoint.RejectLink(stageFolder); Directory.CreateDirectory(stageFolder); File.SetAttributes(stageFolder, File.GetAttributes(stageFolder) | FileAttributes.Hidden);
                string stage = OwnedStagePath(Path.Combine(stageFolder, checkpoint.Data.Identity + ".part"), checkpoint.Data.Stage); TransferCheckpoint.RejectLink(stage);
                if (checkpoint.Data.Stage != null && !String.Equals(checkpoint.Data.Stage, stage, StringComparison.Ordinal)) throw new IOException(L.Text("resume.invalidPartialPath"));
                bool resume = transfer != null && transfer.Resume && checkpoint.Data.Stage != null;
                if (checkpoint.Data.Stage == null && File.Exists(stage)) throw new IOException(L.Text("resume.unownedPartialPathPrefix") + stage);
                if (resume) { checkpoint.ValidateSource(info.Length, info.LastWriteTime, null); LocalTarget(checkpoint, target, true); checkpoint.ValidateDownload(delegate { Progress(remote, checkpoint.Data.Offset, info.Length, Stopwatch.StartNew(), checkpoint.Data.Offset, L.Text("resume.verifyingPartialCopy")); }); }
                else
                {
                    if (File.Exists(stage)) File.Delete(stage);
                    LocalTarget(checkpoint, target, false, stage, info.Length, info.LastWriteTime);
                }
                long offset = checkpoint.Data.Offset; var timer = Stopwatch.StartNew(); Progress(remote, offset, info.Length, timer, offset);
                using (var output = new FileStream(stage, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                using (var writer = new CheckpointWriteStream(output, checkpoint))
                {
                    output.Position = offset;
                    try
                    {
                        if (offset < info.Length)
                        {
                            if (options.Protocol == Protocol.Ftp) Curl.Download(remote, writer, info.Length, delegate(long done) { Progress(remote, done, info.Length, timer, offset); }, offset);
                            else ssh.Transfer(remote, writer, info.Length, false, delegate(long done) { Progress(remote, done, info.Length, timer, offset); }, offset);
                        }
                    }
                    finally { writer.Checkpoint(); }
                }
                CheckAbort(); var after = GetFileInfo(remote); checkpoint.ValidateSource(after.Length, after.LastWriteTime, null);
                if (new FileInfo(stage).Length != info.Length) throw new IOException(L.Text("resume.partialLengthMismatch"));
                LocalTarget(checkpoint, target, true);
                if (transfer == null || transfer.PreserveTimestamp) File.SetLastWriteTime(stage, info.LastWriteTime);
                if (File.Exists(target)) File.Replace(stage, target, null); else File.Move(stage, target);
                checkpoint.Clear(); if (remove) RemoveFiles(remote); Completed(info.Length); return new TransferResult();
            }
        }
        TransferResult UploadCheckpointed(string local, string remote, bool remove, TransferOptions transfer)
        {
            ResumeProbeBytes = 0;
            local = Path.GetFullPath(local); TransferCheckpoint.RejectLink(local);
            using (var input = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = input.Length; DateTime modified = File.GetLastWriteTime(local);
                string target = RemoteConnection.Normalize(remote.EndsWith("/") ? RemoteConnection.Combine(remote, Path.GetFileName(local)) : remote);
                using (var checkpoint = new TransferCheckpoint(options, true, local, target))
                {
                    string folder = RemoteConnection.Parent(target), stage = RemoteConnection.Combine(folder, ".FileCompass-upload-" + checkpoint.Data.Identity + ".part");
                    stage = OwnedStagePath(stage, checkpoint.Data.Stage);
                    string backup = RemoteConnection.Combine(folder, ".FileCompass-old-" + Guid.NewGuid().ToString("N"));
                    if (checkpoint.Data.Stage != null && checkpoint.Data.Stage != stage) throw new IOException(L.Text("resume.invalidPartialPath"));
                    bool resume = transfer != null && transfer.Resume && checkpoint.Data.Stage != null; long offset = 0;
                    Progress(local, 0, length, Stopwatch.StartNew(), 0, resume ? L.Text("resume.preparingStatus") : L.Text("resume.preparingUpload"));
                    if (checkpoint.Data.Stage == null && FileExists(stage)) throw new IOException(L.Text("resume.unownedPartialPathPrefix") + stage);
                    if (resume)
                    {
                        checkpoint.ValidateSource(length, modified, null);
                        checkpoint.ValidateUpload(input, delegate { Progress(local, 0, length, Stopwatch.StartNew(), 0, L.Text("resume.checkingSourceEdgeBlocks")); });
                        RemoteTarget(checkpoint, target, true);
                        if (FileExists(stage))
                        {
                            var partial = GetFileInfo(stage); if (partial.IsDirectory || partial.FileType == 'L' || partial.Length > length) throw new IOException(L.Text("resume.remotePartialNotRegularFile"));
                            offset = partial.Length;
                            if (offset > 0)
                            {
                                foreach (long start in new[] { 0L, Math.Max(0, offset - 65536) }.Distinct())
                                {
                                    CheckAbort(); int count = (int)Math.Min(65536, offset - start);
                                    Progress(local, offset, length, Stopwatch.StartNew(), offset, L.Text("resume.shortEdgeBlockCheck"));
                                    using (var verification = new HashWriteStream())
                                    {
                                        if (options.Protocol == Protocol.Ftp) Curl.ReadRange(stage, verification, start, count);
                                        else ssh.Transfer(stage, verification, start + count, false, delegate { CheckAbort(); }, start);
                                        ResumeProbeBytes += verification.Length;
                                        input.Position = start;
                                        if (verification.Finish() != TransferCheckpoint.Hash(input, count, CheckAbort)) throw new IOException(L.Text("resume.remotePartialDiffersFromSource"));
                                    }
                                }
                            }
                        }
                        checkpoint.SetUploadPrefix(input, offset, CheckAbort);
                    }
                    else
                    {
                        if (FileExists(stage)) { var partial = GetFileInfo(stage); if (partial.IsDirectory || partial.FileType == 'L') throw new IOException(L.Text("resume.partialFolderOrLinkRejected")); RemoveFiles(stage); }
                        RemoteTarget(checkpoint, target, false, stage, length, modified, null);
                    }
                    WriteUploadInfo(stage, local, target, checkpoint.Data);
                    input.Position = offset; bool oldMoved = false, committed = false; var timer = Stopwatch.StartNew(); Progress(local, offset, length, timer, offset);
                    try
                    {
                        if (offset < length || length == 0)
                        {
                            using (var reader = new CheckpointReadStream(input, checkpoint, CheckAbort))
                            {
                                try
                                {
                                    if (options.Protocol == Protocol.Ftp) Curl.Upload(stage, reader, length, delegate(long done) { Progress(local, done, length, timer, offset); }, offset);
                                    else ssh.Transfer(stage, reader, length, true, delegate(long done) { Progress(local, done, length, timer, offset); }, offset);
                                }
                                finally { reader.Checkpoint(); }
                            }
                        }
                        CheckAbort(); if (GetFileInfo(stage).Length != length) throw new IOException(L.Text("transfer.remoteTargetSizeMismatch"));
                        if (File.GetLastWriteTime(local) != modified || input.Length != length) throw new IOException(L.Text("transfer.sourceChangedDuringTransfer"));
                        if (transfer == null || transfer.PreserveTimestamp) SetTime(stage, modified);
                        RemoteTarget(checkpoint, target, true); CheckAbort();
                        if (FileExists(target)) { MoveFile(target, backup); oldMoved = true; }
                        MoveFile(stage, target); committed = true; checkpoint.Clear();
                        try { if (FileExists(stage + ".fcinfo.xml")) RemoveFiles(stage + ".fcinfo.xml"); } catch { /* Data is committed; leftover descriptive metadata is harmless. */ }
                        if (oldMoved) { RemoveFiles(backup); oldMoved = false; }
                        if (remove) File.Delete(local); Completed(length); return new TransferResult();
                    }
                    finally
                    {
                        // Ordinary cancellation deliberately keeps the stage and does not
                        // reconnect just to delete it. Only commit rollback needs a session.
                        if (oldMoved)
                            try
                            {
                                using (var cleanup = new Session())
                                {
                                    cleanup.Open(options);
                                    if (!committed && !cleanup.FileExists(target)) cleanup.MoveFile(backup, target);
                                    if (committed && cleanup.FileExists(backup)) cleanup.RemoveFiles(backup);
                                }
                            }
                            catch { /* The original target remains recoverable in backup. */ }
                    }
                }
            }
        }
    }
    internal sealed class HashWriteStream : Stream
    {
        readonly SHA256 hash = SHA256.Create(); long length;
        internal string Finish() { hash.TransformFinalBlock(new byte[0], 0, 0); return Convert.ToBase64String(hash.Hash); }
        public override void Write(byte[] b, int o, int n) { hash.TransformBlock(b, o, n, null, 0); length += n; }
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead { get { return false; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return true; } }
        public override long Length { get { return length; } } public override long Position { get { return length; } set { throw new NotSupportedException(); } }
        public override void Flush() { } public override int Read(byte[] b, int o, int n) { throw new NotSupportedException(); }
        public override long Seek(long o, SeekOrigin s) { throw new NotSupportedException(); } public override void SetLength(long n) { throw new NotSupportedException(); }
    }
}

