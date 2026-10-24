using System;
using System.IO;
using System.Threading;

namespace FileCompass
{
    // One producer and one consumer. Backpressure limits memory independently of file size.
    internal sealed class RelayPipe : Stream
    {
        internal const int Capacity = 512 * 1024;
        readonly byte[] buffer = new byte[Capacity];
        readonly object gate = new object();
        readonly CancellationToken token;
        readonly CancellationTokenRegistration registration;
        int head, count; bool complete; Exception failure;
        internal int PeakBytes { get; private set; }
        internal RelayPipe(CancellationToken cancellation)
        {
            token = cancellation;
            registration = token.Register(delegate { lock (gate) Monitor.PulseAll(gate); });
        }
        void Check() { token.ThrowIfCancellationRequested(); if (failure != null) throw new IOException(L.Text("relay.otherServerFailedPrefix") + failure.Message, failure); }
        internal void Complete(Exception error = null) { lock (gate) { failure = failure ?? error; complete = true; Monitor.PulseAll(gate); } }
        public override int Read(byte[] output, int offset, int length)
        {
            if (length == 0) return 0;
            lock (gate) {
                while (count == 0 && !complete) { Check(); Monitor.Wait(gate); }
                Check(); if (count == 0) return 0;
                int take = Math.Min(length, Math.Min(count, Capacity - head));
                Buffer.BlockCopy(buffer, head, output, offset, take); head = (head + take) % Capacity; count -= take;
                Monitor.PulseAll(gate); return take;
            }
        }
        public override void Write(byte[] input, int offset, int length)
        {
            lock (gate) while (length > 0) {
                while (count == Capacity && !complete) { Check(); Monitor.Wait(gate); }
                Check(); if (complete) throw new IOException(L.Text("relay.transferEnded"));
                int tail = (head + count) % Capacity, take = Math.Min(length, Math.Min(Capacity - count, Capacity - tail));
                Buffer.BlockCopy(input, offset, buffer, tail, take); count += take; offset += take; length -= take;
                PeakBytes = Math.Max(PeakBytes, count); Monitor.PulseAll(gate);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { Complete(new ObjectDisposedException("RelayPipe")); registration.Dispose(); Array.Clear(buffer, 0, buffer.Length); } base.Dispose(disposing); }
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }
}

