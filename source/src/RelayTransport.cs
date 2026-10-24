using System;
using System.IO;

namespace FileCompass.NetworkTransport
{
    public sealed partial class Session
    {
        internal void DownloadRelay(string path, Stream output, long length, Action<long> progress, long offset = 0)
        {
            CheckAbort();
            var bounded = new RelayLengthWriter(output,length - offset);
            if (offset < length || length == 0) {
                if (options.Protocol == Protocol.Ftp) Curl.Download(path, bounded, length, progress,offset);
                else ssh.Transfer(path, bounded, length, false, progress,offset);
            }
            if (bounded.Remaining != 0) throw new IOException(L.Text("relay.sourceShrankDuringTransfer"));
            CheckAbort();
        }
        static bool SameRelayFile(RemoteFileInfo a, RemoteFileInfo b)
        { return a == null ? b == null : b != null && a.FileType == b.FileType && a.Length == b.Length && a.LastWriteTime == b.LastWriteTime; }
        void ValidateRelayTarget(string target, RemoteFileInfo expected)
        { if (!SameRelayFile(expected, FileExists(target) ? GetFileInfo(target) : null)) throw new IOException(L.Text("transfer.changedTargetOverwriteRejected")); }
        internal void UploadRelay(Stream input, long length, DateTime modified, string target, RemoteFileInfo expected, Action<long> progress, Action validateSource, Action<Exception> stopSource, RelayLease lease = null, Func<bool> discard = null)
        {
            string folder = RemoteConnection.Parent(target), temporary = RemoteConnection.Combine(folder, ".FileCompass-upload-" + Guid.NewGuid().ToString("N")), backup = RemoteConnection.Combine(folder, ".FileCompass-old-" + Guid.NewGuid().ToString("N"));
            bool staged = false, oldMoved = false, committed = false;
            long offset = lease == null ? 0 : lease.Offset;
            if (lease != null) { temporary = lease.Stage; input = new RelayOffsetReader(input,length,offset); }
            try {
                ValidateRelayTarget(target, expected); staged = true;
                if (offset < length || length == 0) {
                    if (options.Protocol == Protocol.Ftp) Curl.Upload(temporary, input, length, progress,offset);
                    else ssh.Transfer(temporary, input, length, true, progress,offset);
                }
                CheckAbort();
                // Upload may stop reading at the advertised length: explicitly join
                // the producer and validate the source before publishing its bytes.
                validateSource(); CheckAbort();
                if (GetFileInfo(temporary).Length != length) throw new IOException(L.Text("transfer.remoteTargetSizeMismatch"));
                SetTime(temporary, modified); CheckAbort(); ValidateRelayTarget(target, expected);
                if (expected != null) { MoveFile(target, backup); oldMoved = true; }
                MoveFile(temporary, target); committed = true; staged = false;
                if (lease != null) lease.Checkpoint.Clear();
                if (oldMoved) { RemoveFiles(backup); oldMoved = false; }
            } catch (Exception ex) { stopSource(ex); throw; } finally {
                bool removeStage = lease == null || (discard != null && discard());
                if ((staged && removeStage) || oldMoved) try {
                    using (var cleanup = new Session()) {
                        var originalTimeout = options.Timeout;
                        try { if (options.Timeout > TimeSpan.FromSeconds(5)) options.Timeout = TimeSpan.FromSeconds(5); cleanup.Open(options);
                            // A foreign file created in the rename gap is never deleted.
                            if (oldMoved && !committed && !cleanup.FileExists(target)) { cleanup.MoveFile(backup, target); oldMoved = false; }
                            if (staged && removeStage) {
                                if (cleanup.FileExists(temporary)) { var partial = cleanup.GetFileInfo(temporary); if (partial.IsDirectory || partial.FileType == 'L') throw new IOException(L.Text("resume.partialPathNotRegularFile")); cleanup.RemoveFiles(temporary); }
                                if (lease != null) lease.Checkpoint.Clear();
                            }
                            if (oldMoved && committed && cleanup.FileExists(backup)) cleanup.RemoveFiles(backup);
                        } finally { options.Timeout = originalTimeout; }
                    }
                } catch { /* Preserve a recoverable backup if the server is unreachable. */ }
            }
        }
    }
    // Detect a growing source before its extra bytes can fill a pipe whose
    // consumer already stopped at the originally advertised length.
    internal sealed class RelayLengthWriter : Stream
    {
        readonly Stream target;
        internal long Remaining { get; private set; }
        internal RelayLengthWriter(Stream output,long size) { target = output; Remaining = size; }
        public override void Write(byte[] bytes,int offset,int count) { if (count > Remaining) throw new IOException(L.Text("relay.sourceGrewDuringTransfer")); target.Write(bytes,offset,count); Remaining -= count; }
        public override bool CanRead { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] buffer,int offset,int count) { throw new NotSupportedException(); }
        public override long Seek(long offset,SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long length) { throw new NotSupportedException(); }
    }
}

