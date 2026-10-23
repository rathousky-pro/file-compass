using System;
using System.IO;
using System.Linq;

namespace CommanderCZ.NetworkTransport
{
    internal sealed class RelayLease : IDisposable
    {
        internal readonly TransferCheckpoint Checkpoint;
        internal readonly string Stage;
        readonly IDisposable targetLock;
        internal long Offset;
        internal RelayLease(TransferCheckpoint checkpoint,string stage,IDisposable destinationLock) { Checkpoint = checkpoint; Stage = stage; targetLock = destinationLock; }
        public void Dispose() { Checkpoint.Dispose(); targetLock.Dispose(); }
    }
    public sealed partial class Session
    {
        string RelayIdentity()
        { return options.Protocol + "\n" + options.FtpSecure + "\n" + options.HostName.ToLowerInvariant() + "\n" + options.PortNumber + "\n" + options.UserName + "\n" + options.SshHostKeyFingerprint; }
        TransferCheckpoint RelayCheckpoint(Session source,string path,string target)
        { return new TransferCheckpoint(options,true,"relay-v1\n" + source.RelayIdentity() + "\n" + RemoteConnection.Normalize(path),RemoteConnection.Normalize(target)); }
        internal bool HasRelayCheckpoint(Session source,string path,string target)
        { using (var checkpoint = RelayCheckpoint(source,path,target)) return checkpoint.Data.Stage != null; }
        internal string DiscardRelayPartial(Session source,string path,string target)
        {
            try {
                using (var checkpoint = RelayCheckpoint(source,path,target)) {
                    if (checkpoint.Data.Stage == null) return null;
                    string stage = RemoteConnection.Combine(RemoteConnection.Parent(target),".FileCompass-relay-" + checkpoint.Data.Identity + ".part");
                    stage = OwnedStagePath(stage, checkpoint.Data.Stage);
                    if (checkpoint.Data.Stage != stage) throw new IOException(L.Text("resume.invalidPartialPath"));
                    using (var destinationLock = new TransferCheckpoint(options,true,"relay-target-lock-v1",RemoteConnection.Normalize(target)))
                    using (var cleanup = new Session()) {
                        var originalTimeout = options.Timeout;
                        try { if (options.Timeout > TimeSpan.FromSeconds(5)) options.Timeout = TimeSpan.FromSeconds(5); cleanup.Open(options);
                            if (cleanup.FileExists(stage)) { var partial = cleanup.GetFileInfo(stage); if (partial.IsDirectory || partial.FileType == 'L') throw new IOException(L.Text("resume.partialPathNotRegularFile")); cleanup.RemoveFiles(stage); }
                            checkpoint.Clear();
                        } finally { options.Timeout = originalTimeout; }
                    }
                } return null;
            } catch (Exception ex) { return ex.Message; }
        }
        internal RelayLease PrepareRelay(Session source,string path,string target,RemoteFileInfo info,RemoteFileInfo expected,bool resume)
        {
            var checkpoint = RelayCheckpoint(source,path,target);
            TransferCheckpoint destinationLock = null;
            try {
                destinationLock = new TransferCheckpoint(options,true,"relay-target-lock-v1",RemoteConnection.Normalize(target));
                ResumeProbeBytes = 0; source.ResumeProbeBytes = 0;
                string stage = RemoteConnection.Combine(RemoteConnection.Parent(target),".FileCompass-relay-" + checkpoint.Data.Identity + ".part");
                stage = OwnedStagePath(stage, checkpoint.Data.Stage);
                var lease = new RelayLease(checkpoint,stage,destinationLock);
                bool owned = checkpoint.Data.Stage != null;
                if (owned && checkpoint.Data.Stage != stage) throw new IOException(L.Text("resume.invalidRelayPartialPath"));
                bool stageExists = FileExists(stage); RemoteFileInfo partial = stageExists ? GetFileInfo(stage) : null;
                if (partial != null && (partial.IsDirectory || partial.FileType == 'L')) throw new IOException(L.Text("resume.relayPartialNotRegularFile"));
                if (!owned && stageExists) throw new IOException(L.Text("resume.partialOwnershipMissingPrefix") + stage);
                if (resume && owned) {
                    checkpoint.ValidateSource(info.Length,info.LastWriteTime,null);
                    checkpoint.ValidateTarget(expected != null,expected == null ? 0 : expected.Length,expected == null ? DateTime.MinValue : expected.LastWriteTime);
                    if (stageExists && partial.Length > info.Length) throw new IOException(L.Text("resume.partialLargerThanSource"));
                    if (stageExists && options.Protocol != Protocol.Scp && source.options.Protocol != Protocol.Scp) {
                        long offset = partial.Length; ResumeProbeBytes = 0; source.ResumeProbeBytes = 0;
                        foreach (long start in new[] { 0L,Math.Max(0,offset - 65536) }.Distinct()) {
                            int count = (int)Math.Min(65536,offset - start); if (count == 0) continue;
                            if (RelayRangeHash(stage,start,count) != source.RelayRangeHash(path,start,count)) throw new IOException(L.Text("resume.partialDiffersFromSource"));
                        }
                        // Detect truncation or replacement while the short probes ran.
                        var after = GetFileInfo(stage); if (after.Length != partial.Length || after.LastWriteTime != partial.LastWriteTime || after.FileType != partial.FileType) throw new IOException(L.Text("resume.partialChangedDuringVerification"));
                        lease.Offset = offset; checkpoint.Data.Offset = offset; checkpoint.Save(); return lease;
                    }
                    // SCP cannot seek; restart only our owned staging file.
                }
                if (stageExists) RemoveFiles(stage);
                checkpoint.Initialize(stage,info.Length,info.LastWriteTime,null,expected != null,expected == null ? 0 : expected.Length,expected == null ? DateTime.MinValue : expected.LastWriteTime);
                return lease;
            } catch { checkpoint.Dispose(); if (destinationLock != null) destinationLock.Dispose(); throw; }
        }
        string RelayRangeHash(string path,long start,int count)
        {
            CheckAbort(); using (var hash = new HashWriteStream()) {
                if (options.Protocol == Protocol.Ftp) Curl.ReadRange(path,hash,start,count);
                else ssh.Transfer(path,hash,start + count,false,delegate { CheckAbort(); },start);
                ResumeProbeBytes += hash.Length; if (hash.Length != count) throw new IOException(L.Text("resume.verificationEndedEarly")); return hash.Finish();
            }
        }
    }
    internal sealed class RelayOffsetReader : Stream
    {
        readonly Stream input; readonly long length; long position;
        internal RelayOffsetReader(Stream stream,long size,long offset) { input = stream; length = size; position = offset; }
        public override int Read(byte[] bytes,int offset,int count) { int n = input.Read(bytes,offset,count); position += n; return n; }
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return position; } set { if (value != position) throw new NotSupportedException(L.Text("relay.streamCannotSeek")); } }
        public override void Flush() { }
        public override void Write(byte[] bytes,int offset,int count) { throw new NotSupportedException(); }
        public override long Seek(long offset,SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long size) { throw new NotSupportedException(); }
    }
}

