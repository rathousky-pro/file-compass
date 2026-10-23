using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace CommanderCZ.NetworkTransport
{
    public sealed class TransferBlock
    {
        public int Length; public string Hash;
    }
    public sealed class TransferCheckpointData
    {
        public string Identity, Stage, SourceHash;
        public string OriginalName, SourcePath, TargetPath;
        public long Length, Modified, Offset;
        public bool TargetExists; public long TargetLength, TargetModified;
        public List<TransferBlock> Blocks = new List<TransferBlock>();
    }
    // A checkpoint contains no credentials. Its exclusive lease also prevents two
    // independent transfer workers from writing to the same staging file.
    internal sealed class TransferCheckpoint : IDisposable
    {
        internal static string TestRoot;
        internal TransferCheckpointData Data;
        readonly string path; readonly FileStream lease;
        readonly string sourcePath, targetPath;
        internal TransferCheckpoint(SessionOptions options, bool upload, string source, string target)
        {
            sourcePath = source; targetPath = target;
            string identity = options.Protocol + "\n" + options.FtpSecure + "\n" + options.HostName.ToLowerInvariant() + "\n" + options.PortNumber + "\n" + options.UserName + "\n" + options.SshHostKeyFingerprint + "\n" + upload + "\n" + source + "\n" + target;
            string id = Digest(Encoding.UTF8.GetBytes(identity));
            string root = options.Raw("ResumeDirectory", TestRoot ?? System.IO.Path.Combine(AppPaths.ConfigDirectory, "transfers"));
            Directory.CreateDirectory(root); RejectLink(root);
            path = Path.Combine(root, id + ".xml"); string lockPath = Path.Combine(root, id + ".lock"); RejectLink(lockPath); RejectLink(path);
            try { lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) { throw new IOException(L.Text("resume.fileAlreadyTransferred"), ex); }
            try
            {
                if (File.Exists(path)) Data = FileIntegrity.ReadXml<TransferCheckpointData>(path);
                if (Data != null && Data.Identity != id) throw new IOException(L.Text("resume.invalidRecord"));
                if (Data == null) Data = new TransferCheckpointData { Identity = id };
            }
            catch { lease.Dispose(); throw; }
        }
        internal static void RejectLink(string path)
        { if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("resume.partialLinkRejectedPrefix") + path); }
        internal void ValidateSource(long length, DateTime modified, string hash)
        {
            if (Data.Stage != null && (Data.Length != length || Data.Modified != modified.ToUniversalTime().Ticks || (hash != null && Data.SourceHash != hash)))
                throw new IOException(L.Text("resume.sourceChangedSinceInterruption"));
        }
        internal void Initialize(string stage, long length, DateTime modified, string hash, bool exists, long targetLength, DateTime targetModified)
        {
            Data = new TransferCheckpointData { Identity = Data.Identity, Stage = stage, Length = length, Modified = modified.ToUniversalTime().Ticks, SourceHash = hash,
                SourcePath = sourcePath, TargetPath = targetPath, OriginalName = Path.GetFileName(sourcePath),
                TargetExists = exists, TargetLength = targetLength, TargetModified = targetModified.ToUniversalTime().Ticks };
            Save();
        }
        internal void ValidateTarget(bool exists, long length, DateTime modified)
        {
            if (exists != Data.TargetExists || (exists && (length != Data.TargetLength || modified.ToUniversalTime().Ticks != Data.TargetModified)))
                throw new IOException(L.Text("resume.targetChangedPartialPreserved"));
        }
        internal void Save()
        {
            FileIntegrity.WriteXml(path, Data);
        }
        internal void Clear() { if (File.Exists(path)) File.Delete(path); FileIntegrity.DeleteSignature(path); }
        internal void ValidateDownload(Action check)
        {
            RejectLink(Data.Stage);
            if (!File.Exists(Data.Stage)) { if (Data.Offset != 0) throw new IOException(L.Text("resume.partialFileMissing")); return; }
            using (var input = new FileStream(Data.Stage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                long size = 0;
                for (int index = 0; index < Data.Blocks.Count; index++)
                {
                    var block = Data.Blocks[index];
                    check(); if (block.Length <= 0 || block.Length > CheckpointWriteStream.BlockSize) throw new IOException(L.Text("resume.invalidPartialBlock"));
                    if (index == 0 || index == Data.Blocks.Count - 1)
                    { input.Position = size; if (Hash(input, block.Length, check) != block.Hash) throw new IOException(L.Text("resume.partialFileCorrupt")); }
                    size += block.Length;
                }
                if (size != Data.Offset || size > Data.Length || input.Length < size) throw new IOException(L.Text("resume.invalidPartialLength"));
                // A hard process termination may leave bytes beyond the last durable checkpoint.
                input.SetLength(size); input.Flush(true);
            }
        }
        internal void ValidateUpload(Stream input, Action check)
        {
            input.Position = 0;
            if (Data.SourceHash != null)
            {
                // Legacy manifests had only a whole-file digest. Source metadata and
                // the bounded comparison to the remote prefix validate migration;
                // never reread the entire source just to resume it.
                check(); return;
            }
            else
            {
                long size = 0;
                for (int index = 0; index < Data.Blocks.Count; index++)
                {
                    var block = Data.Blocks[index];
                    check();
                    if (block.Length <= 0 || block.Length > CheckpointWriteStream.BlockSize || size + block.Length > Data.Length)
                        throw new IOException(L.Text("resume.invalidPartialBlock"));
                    if ((index == 0 || index == Data.Blocks.Count - 1) && block.Hash != null)
                    { input.Position = size; if (Hash(input, block.Length, check) != block.Hash) throw new IOException(L.Text("resume.sourceChangedOverwriteRequired")); }
                    size += block.Length;
                }
                if (size != Data.Offset) throw new IOException(L.Text("resume.invalidPartialLength"));
            }
            input.Position = 0;
        }
        internal void SetUploadPrefix(Stream input, long offset, Action check)
        {
            // Keep existing block hashes. Only a shortened boundary block needs a
            // reread. Bytes received after a hard crash have no durable hash; their
            // endpoints were compared remotely, and null marks this weaker coverage.
            if (offset < 0 || offset > Data.Length) throw new IOException(L.Text("resume.invalidResumeLength"));
            var previous = Data.Blocks; var blocks = new List<TransferBlock>(); long position = 0;
            foreach (var block in previous)
            {
                check(); if (position >= offset) break;
                if (block.Length <= 0 || block.Length > CheckpointWriteStream.BlockSize) throw new IOException(L.Text("resume.invalidPartialBlock"));
                int count = (int)Math.Min(block.Length, offset - position);
                if (count == block.Length) blocks.Add(block);
                else { input.Position = position; blocks.Add(new TransferBlock { Length = count, Hash = Hash(input, count, check) }); }
                position += count;
            }
            while (position < offset) { check(); int count = (int)Math.Min(CheckpointWriteStream.BlockSize, offset - position); blocks.Add(new TransferBlock { Length = count }); position += count; }
            Data.SourceHash = null; Data.Blocks = blocks; Data.Offset = offset;
            Save(); input.Position = offset;
        }
        internal static string Digest(byte[] bytes) { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(bytes)).Replace('/', '_').Replace('+', '-').TrimEnd('='); }
        internal static string Hash(Stream input, long length, Action check)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = new byte[65536]; long left = length;
                while (left > 0) { check(); int n = input.Read(bytes, 0, (int)Math.Min(left, bytes.Length)); if (n == 0) throw new IOException(L.Text("resume.fileEndedDuringVerification")); hash.TransformBlock(bytes, 0, n, null, 0); left -= n; }
                hash.TransformFinalBlock(new byte[0], 0, 0); return Convert.ToBase64String(hash.Hash);
            }
        }
        public void Dispose() { lease.Dispose(); }
    }
    // Hash the bytes supplied to the transport, rather than reading a network
    // source once in full before sending it. A cancellation also saves its short tail.
    internal sealed class CheckpointReadStream : Stream
    {
        readonly Stream input; readonly TransferCheckpoint checkpoint; readonly Action check;
        SHA256 hash = SHA256.Create(); int count; long saved;
        readonly Stopwatch timer = Stopwatch.StartNew();
        internal CheckpointReadStream(Stream input, TransferCheckpoint checkpoint, Action check)
        { this.input = input; this.checkpoint = checkpoint; this.check = check; saved = checkpoint.Data.Offset; }
        void EndBlock()
        {
            if (count == 0) return;
            hash.TransformFinalBlock(new byte[0], 0, 0);
            checkpoint.Data.Blocks.Add(new TransferBlock { Length = count, Hash = Convert.ToBase64String(hash.Hash) });
            checkpoint.Data.Offset += count; hash.Dispose(); hash = SHA256.Create(); count = 0;
        }
        public override int Read(byte[] bytes, int offset, int length)
        {
            check(); int n = input.Read(bytes, offset, length); int left = n;
            while (left > 0)
            {
                int part = Math.Min(left, CheckpointWriteStream.BlockSize - count);
                hash.TransformBlock(bytes, offset, part, null, 0); count += part; offset += part; left -= part;
                if (count == CheckpointWriteStream.BlockSize)
                {
                    EndBlock();
                    if (checkpoint.Data.Offset - saved >= 67108864 || timer.ElapsedMilliseconds >= 2000)
                    { checkpoint.Save(); saved = checkpoint.Data.Offset; timer.Restart(); }
                }
            }
            return n;
        }
        internal void Checkpoint() { EndBlock(); checkpoint.Save(); }
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead { get { return true; } } public override bool CanSeek { get { return true; } } public override bool CanWrite { get { return false; } }
        public override long Length { get { return input.Length; } }
        public override long Position { get { return input.Position; } set { if (value != input.Position) throw new NotSupportedException(L.Text("resume.seekWithoutVerificationRejected")); } }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) { throw new NotSupportedException(); }
        public override void Write(byte[] b, int o, int n) { throw new NotSupportedException(); }
        public override void SetLength(long n) { throw new NotSupportedException(); }
    }
    // Only fully hashed and flushed blocks enter the atomic manifest. Abrupt
    // termination can lose a short tail, but cannot append unverified data.
    internal sealed class CheckpointWriteStream : Stream
    {
        internal const int BlockSize = 1048576;
        readonly FileStream file; readonly TransferCheckpoint checkpoint; SHA256 hash = SHA256.Create(); int count;
        readonly Stopwatch timer = Stopwatch.StartNew(); long saved;
        internal CheckpointWriteStream(FileStream file, TransferCheckpoint checkpoint) { this.file = file; this.checkpoint = checkpoint; saved = checkpoint.Data.Offset; }
        void EndBlock()
        {
            if (count == 0) return;
            hash.TransformFinalBlock(new byte[0], 0, 0); checkpoint.Data.Blocks.Add(new TransferBlock { Length = count, Hash = Convert.ToBase64String(hash.Hash) });
            checkpoint.Data.Offset += count; hash.Dispose(); hash = SHA256.Create(); count = 0;
        }
        public override void Write(byte[] bytes, int offset, int length)
        {
            while (length > 0) { int n = Math.Min(length, BlockSize - count); file.Write(bytes, offset, n); hash.TransformBlock(bytes, offset, n, null, 0); count += n; offset += n; length -= n;
                if (count == BlockSize) { EndBlock(); if (checkpoint.Data.Offset - saved >= 4194304 || timer.ElapsedMilliseconds >= 2000) Save(); } }
        }
        void Save() { file.Flush(true); checkpoint.Save(); saved = checkpoint.Data.Offset; timer.Restart(); }
        internal void Checkpoint() { EndBlock(); Save(); }
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead { get { return false; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return true; } }
        public override long Length { get { return file.Length; } } public override long Position { get { return file.Position; } set { throw new NotSupportedException(); } }
        public override void Flush() { file.Flush(); } public override int Read(byte[] b, int o, int n) { throw new NotSupportedException(); }
        public override long Seek(long o, SeekOrigin s) { throw new NotSupportedException(); } public override void SetLength(long n) { throw new NotSupportedException(); }
    }
}

