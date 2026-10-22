using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace CommanderCZ
{
    internal sealed class InstanceCoordinator : IDisposable
    {
        readonly Mutex mutex;
        readonly string pipeName;
        readonly object gate = new object();
        readonly Queue<string> pending = new Queue<string>();
        readonly CancellationTokenSource stopping = new CancellationTokenSource();
        NamedPipeServerStream current;
        Action<string> handler;
        internal readonly bool Primary;
        internal string PipeName { get { return pipeName; } }
        internal InstanceCoordinator(string identity)
        {
            string key;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 24);
            pipeName = "CommanderCZ-instance-" + key;
            var security = new MutexSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new MutexAccessRule(WindowsIdentity.GetCurrent().User, MutexRights.FullControl, AccessControlType.Allow));
            bool created; mutex = new Mutex(false, @"Local\" + pipeName, out created, security);
            try { Primary = mutex.WaitOne(0); } catch (AbandonedMutexException) { Primary = true; }
            if (Primary) Task.Run(new Func<Task>(Listen));
        }
        internal static string Identity
        { get { return WindowsIdentity.GetCurrent().User.Value + "|" + System.Diagnostics.Process.GetCurrentProcess().SessionId + "|" + Path.GetFullPath(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ToUpperInvariant(); } }
        internal static string FolderArgument(string[] args)
        {
            if (args.Length == 0) return "";
            if (args.Length != 1 || args[0].StartsWith("--", StringComparison.Ordinal) || String.IsNullOrWhiteSpace(args[0])) throw new ArgumentException(L.Text("instance.invalidArguments"));
            return ValidateFolder(Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[0])));
        }
        internal static string ValidateFolder(string folder)
        {
            if (folder == "") return folder;
            if (folder == null || folder.Length > 16000 || folder.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || !Path.IsPathRooted(folder)) throw new IOException(L.Text("instance.invalidArguments"));
            return Path.GetFullPath(folder);
        }
        internal void Attach(Action<string> action)
        {
            string[] queued;
            lock (gate) { handler = action; queued = pending.ToArray(); pending.Clear(); }
            foreach (string folder in queued) action(folder);
        }
        internal void Forward(string folder)
        {
            folder = ValidateFolder(folder);
            try {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous)) {
                    pipe.Connect(5000); uint pid; if (GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out pid)) AllowSetForegroundWindow(pid);
                    using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true)) { byte[] bytes = Encoding.UTF8.GetBytes(folder); writer.Write(bytes.Length); writer.Write(bytes); writer.Flush(); }
                    var reply = new byte[1]; var read = pipe.ReadAsync(reply, 0, 1);
                    if (!read.Wait(5000) || read.Result != 1 || reply[0] != 1) throw new IOException(L.Text("instance.forwardFailed"));
                }
            } catch (IOException ex) { throw new IOException(L.Text("instance.forwardFailed"), ex); }
            catch (TimeoutException ex) { throw new IOException(L.Text("instance.forwardFailed"), ex); }
            catch (AggregateException ex) { throw new IOException(L.Text("instance.forwardFailed"), ex); }
            catch (UnauthorizedAccessException ex) { throw new IOException(L.Text("instance.forwardFailed"), ex); }
        }
        async Task Listen()
        {
            while (!stopping.IsCancellationRequested) {
                bool retry = false;
                try {
                    var acl = new PipeSecurity(); acl.SetAccessRuleProtection(true, false);
                    acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
                    acl.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, acl)) {
                        lock (gate) { if (stopping.IsCancellationRequested) return; current = pipe; }
                        await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                        var read = Task.Run(delegate { using (var reader = new BinaryReader(pipe, Encoding.UTF8, true)) { int length = reader.ReadInt32(); if (length < 0 || length > 32768) throw new IOException(); byte[] bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new IOException(); return ValidateFolder(new UTF8Encoding(false, true).GetString(bytes)); } });
                        if (await Task.WhenAny(read, Task.Delay(3000)).ConfigureAwait(false) != read) { Observe(read); continue; }
                        string folder = await read.ConfigureAwait(false); Action<string> action;
                        lock (gate) { action = handler; if (action == null) { if (pending.Count >= 32) throw new IOException(); pending.Enqueue(folder); } }
                        if (action != null) action(folder);
                        pipe.WriteByte(1); pipe.Flush();
                    }
                } catch (IOException) { retry = true; } catch (ObjectDisposedException) { } catch (ArgumentException) { } catch (NotSupportedException) { } catch (UnauthorizedAccessException) { retry = true; }
                finally { lock (gate) current = null; }
                if (retry && !stopping.IsCancellationRequested) await Task.Delay(100).ConfigureAwait(false);
            }
        }
        static void Observe(Task task) { task.ContinueWith(t => { var error = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
        public void Dispose()
        {
            stopping.Cancel(); lock (gate) { if (current != null) current.Dispose(); }
            if (Primary) mutex.ReleaseMutex(); mutex.Dispose();
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint process);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint process);
    }
}

