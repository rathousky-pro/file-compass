using System;
using System.IO;
namespace CommanderCZ
{
    internal sealed class ArchiveReadProgressStream : Stream
    {
        readonly Stream input; readonly Action<long> progress; long highest;
        internal ArchiveReadProgressStream(Stream input, Action<long> progress) { this.input = input; this.progress = progress; }
        public override int Read(byte[] buffer, int offset, int count) { int read = input.Read(buffer, offset, count); long position = input.Position; if (position > highest) { progress(position - highest); highest = position; } return read; }
        public override bool CanRead { get { return true; } } public override bool CanSeek { get { return input.CanSeek; } } public override bool CanWrite { get { return false; } }
        public override long Length { get { return input.Length; } } public override long Position { get { return input.Position; } set { input.Position = value; } }
        public override long Seek(long offset, SeekOrigin origin) { return input.Seek(offset, origin); } public override void Flush() { } public override void SetLength(long value) { throw new NotSupportedException(); } public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
    }
}

