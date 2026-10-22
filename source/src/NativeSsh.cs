using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace CommanderCZ.NetworkTransport
{
    internal sealed class NativeSsh : IDisposable
    {
        readonly object sync = new object();
        Timer keepAlive;
        IntPtr session, sftp;
        Socket socket;
        readonly SessionOptions options;
        readonly Action check; readonly Action<int> traffic;
        readonly Encoding encoding;
        internal string Fingerprint;
        internal string KnownHostKey;
        internal NativeSsh(SessionOptions settings, Action cancellation, Action<int> charge = null) { traffic = charge; options = settings; check = cancellation; encoding = settings.Raw("Utf", "2") == "0" ? Encoding.Default : Encoding.UTF8; }
        internal static bool Trusted(string expected, byte[] key)
        {
            if (String.IsNullOrWhiteSpace(expected) || expected.Trim() == "*") return false;
            string algorithm = KeyAlgorithm(key), value = expected.Trim(), hash = value.Substring(value.LastIndexOf(' ') + 1);
            if (value.Contains(" ") && !value.StartsWith(algorithm + " ", StringComparison.Ordinal)) return false;
            using (var sha = SHA256.Create()) if (hash.Replace("SHA256:", "").TrimEnd('=') == Convert.ToBase64String(sha.ComputeHash(key)).TrimEnd('=')) return true;
            using (var md5 = MD5.Create()) return hash.Replace("MD5:", "").Equals(BitConverter.ToString(md5.ComputeHash(key)).Replace('-', ':').ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }
        static string KeyAlgorithm(byte[] key) { if (key.Length < 4) throw new IOException(L.Text("ssh.invalidKey")); int n = (key[0] << 24) | (key[1] << 16) | (key[2] << 8) | key[3]; if (n < 1 || n > key.Length - 4) throw new IOException(L.Text("ssh.invalidKey")); return Encoding.ASCII.GetString(key, 4, n); }
        internal void OpenCore(bool probe)
        {
            NativeNetwork.Ensure(); check();
            int proxy = Int32.Parse(options.Raw("ProxyMethod", "0"));
            string host = proxy == 0 ? options.HostName : options.Raw("ProxyHost", ""); int port = proxy == 0 ? options.PortNumber : Int32.Parse(options.Raw("ProxyPort", "8080"));
            var addresses = Resolve(host); Exception last = null;
            foreach (var address in addresses)
            {
                check(); socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp); socket.NoDelay = true;
                try { var ar = socket.BeginConnect(address, port, null, null); var timer = Stopwatch.StartNew(); while (!ar.IsCompleted) { check(); if (timer.Elapsed > options.Timeout) throw new TimeoutException(L.Text("ssh.serverNotResponding")); Thread.Sleep(20); } socket.EndConnect(ar); last = null; break; }
                catch (Exception ex) { socket.Close(); last = ex; check(); }
            }
            if (last != null) throw new IOException(L.Text("ssh.connectionFailed"), last);
            if (proxy != 0) Proxy(proxy);
            socket.Blocking = false; session = NativeNetwork.libssh2_session_init_ex(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (session == IntPtr.Zero) throw new IOException(L.Text("ssh.sessionCreationFailed"));
            NativeNetwork.libssh2_session_set_blocking(session, 0);
            if (options.Raw("Compression", "off") == "on") { NativeNetwork.libssh2_session_flag(session, 2, 1); NativeNetwork.libssh2_session_method_pref(session, 6, NativeNetwork.Bytes("zlib@openssh.com,zlib,none")); NativeNetwork.libssh2_session_method_pref(session, 7, NativeNetwork.Bytes("zlib@openssh.com,zlib,none")); }
            if (!String.IsNullOrWhiteSpace(options.SshHostKeyFingerprint) && options.SshHostKeyFingerprint.Contains(" "))
            { string algorithm = options.SshHostKeyFingerprint.Split(' ')[0]; if (algorithm == "ssh-rsa") algorithm = "rsa-sha2-512,rsa-sha2-256,ssh-rsa"; NativeNetwork.libssh2_session_method_pref(session, 1, NativeNetwork.Bytes(algorithm)); }
            Retry(delegate { return NativeNetwork.libssh2_session_handshake(session, socket.Handle); });
            UIntPtr length; int type; IntPtr hostKey = NativeNetwork.libssh2_session_hostkey(session, out length, out type);
            if (hostKey == IntPtr.Zero) throw new IOException(L.Text("ssh.serverKeyMissing")); var key = new byte[checked((int)length.ToUInt64())]; Marshal.Copy(hostKey, key, 0, key.Length);
            using (var sha = SHA256.Create()) Fingerprint = KeyAlgorithm(key) + " " + Convert.ToBase64String(sha.ComputeHash(key)).TrimEnd('=');
            KnownHostKey = KeyAlgorithm(key) + " " + Convert.ToBase64String(key);
            if (probe) return;
            if (!Trusted(options.SshHostKeyFingerprint, key)) throw new SessionRemoteException(L.Text("ssh.hostKeyMismatch"));
            var user = NativeNetwork.Bytes(options.UserName); int auth = options.Raw("Agent", "off") == "on" ? AgentAuth(user) : -1;
            if (auth != 0 && !String.IsNullOrEmpty(options.SshPrivateKeyPath))
            { byte[] privateKey = File.ReadAllBytes(options.SshPrivateKeyPath); try { auth = Retry(delegate { return NativeNetwork.libssh2_userauth_publickey_frommemory(session, user, (UIntPtr)(user.Length - 1), IntPtr.Zero, UIntPtr.Zero, privateKey, (UIntPtr)privateKey.Length, IntPtr.Zero); }, false); } finally { Array.Clear(privateKey, 0, privateKey.Length); } }
            if (auth != 0)
            { var password = NativeNetwork.Bytes(options.Password); try { auth = Retry(delegate { return NativeNetwork.libssh2_userauth_password_ex(session, user, (uint)user.Length - 1, password, (uint)password.Length - 1, IntPtr.Zero); }, false); } finally { Array.Clear(password, 0, password.Length); } }
            if (auth != 0)
            {
                NativeNetwork.Keyboard reply = delegate(IntPtr name, int nl, IntPtr instruction, int il, int count, IntPtr prompts, IntPtr responses, IntPtr data)
                {
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr prompt = Marshal.ReadIntPtr(prompts,i*16); int promptLength = Math.Max(0,Math.Min(65536,Marshal.ReadInt32(prompts,i*16+8)));
                        var label = new byte[promptLength]; if(promptLength>0) Marshal.Copy(prompt,label,0,promptLength); string text=Encoding.UTF8.GetString(label);
                        var bytes=NativeNetwork.Bytes(text.IndexOf("password",StringComparison.OrdinalIgnoreCase)>=0 || text.IndexOf(L.Code("ssh.passwordPromptMatch"),StringComparison.OrdinalIgnoreCase)>=0 ? options.Password : "");
                        IntPtr memory = NativeNetwork.cn_alloc((UIntPtr)bytes.Length);
                        if(memory!=IntPtr.Zero) Marshal.Copy(bytes,0,memory,bytes.Length);
                        Marshal.WriteIntPtr(responses,i*16,memory); Marshal.WriteInt32(responses,i*16+8,memory==IntPtr.Zero ? 0 : bytes.Length-1); Array.Clear(bytes,0,bytes.Length);
                    }
                };
                auth = Retry(delegate { return NativeNetwork.libssh2_userauth_keyboard_interactive_ex(session, user, (uint)user.Length - 1, reply); }, false); GC.KeepAlive(reply);
            }
            if (auth != 0) throw new SessionRemoteException(L.Text("ssh.authenticationRejected"));
            int seconds; if (Int32.TryParse(options.Raw("KeepAlive", "0"), out seconds) && seconds > 0)
            {
                NativeNetwork.libssh2_keepalive_config(session, 0, (uint)Math.Max(2, seconds));
                keepAlive = new Timer(delegate { if (!Monitor.TryEnter(sync)) return; try { if (session != IntPtr.Zero) { int next; if (NativeNetwork.libssh2_keepalive_send(session, out next) < 0) Abort(); } } finally { Monitor.Exit(sync); } }, null, 1000, 1000);
            }
        }
        internal void Open(bool probe) { lock (sync) { OpenCore(probe); } }
        internal string Command(string command) { lock (sync) { return CommandCore(command); } }
        internal RemoteFileInfo Stat(string path, bool missing = false) { lock (sync) { return StatCore(path, missing); } }
        internal RemoteDirectoryInfo List(string path) { lock (sync) { return ListCore(path); } }
        internal void Mkdir(string path) { lock (sync) { MkdirCore(path); } }
        internal void Rename(string source, string target) { lock (sync) { RenameCore(source, target); } }
        internal void Remove(string path, bool directory) { lock (sync) { RemoveCore(path, directory); } }
        internal void SetTime(string path, DateTime time) { lock (sync) { SetTimeCore(path, time); } }
        internal void Transfer(string path, Stream local, long size, bool upload, Action<long> progress, long offset = 0) { lock (sync) { TransferCore(path, local, size, upload, progress, offset); } }
        int AgentAuth(byte[] user)
        {
            IntPtr agent = NativeNetwork.libssh2_agent_init(session); if (agent == IntPtr.Zero) return -1;
            try
            {
                if (Retry(delegate { return NativeNetwork.libssh2_agent_connect(agent); }, false) != 0 || Retry(delegate { return NativeNetwork.libssh2_agent_list_identities(agent); }, false) != 0) return -1;
                IntPtr previous = IntPtr.Zero, identity;
                while (NativeNetwork.libssh2_agent_get_identity(agent, out identity, previous) == 0)
                { check(); IntPtr current = identity; if (Retry(delegate { return NativeNetwork.libssh2_agent_userauth(agent, user, current); }, false) == 0) return 0; previous = identity; }
                return -1;
            }
            finally { NativeNetwork.libssh2_agent_disconnect(agent); NativeNetwork.libssh2_agent_free(agent); }
        }
        IPAddress[] Resolve(string host)
        {
            var ar=Dns.BeginGetHostAddresses(host,null,null); var timer=Stopwatch.StartNew();
            while(!ar.IsCompleted) { check(); if(timer.Elapsed>options.Timeout) throw new TimeoutException(L.Text("ssh.dnsTimeout")); Thread.Sleep(20); }
            check(); return Dns.EndGetHostAddresses(ar);
        }
        void Proxy(int type)
        {
            socket.Blocking = true; socket.ReceiveTimeout = socket.SendTimeout = (int)options.Timeout.TotalMilliseconds;
            var host = Encoding.UTF8.GetBytes(options.HostName); var user = Encoding.UTF8.GetBytes(options.Raw("ProxyUsername", "")); var password = Encoding.UTF8.GetBytes(options.Raw("ProxyPassword", ""));
            Action<byte[]> send = delegate(byte[] b) { int offset = 0; while (offset < b.Length) { check(); offset += socket.Send(b, offset, b.Length - offset, SocketFlags.None); } };
            Func<int, byte[]> receive = delegate(int count) { var b = new byte[count]; int offset = 0; while (offset < count) { check(); int n = socket.Receive(b, offset, count - offset, SocketFlags.None); if (n == 0) throw new IOException(L.Text("proxy.connectionClosed")); offset += n; } return b; };
            if (type == 1)
            {
                var ip = Array.Find(Resolve(options.HostName), x => x.AddressFamily == AddressFamily.InterNetwork); if (ip == null) throw new IOException(L.Text("proxy.socks4RequiresIpv4"));
                var b = new List<byte>(new byte[] {4,1,(byte)(options.PortNumber >> 8),(byte)options.PortNumber}); b.AddRange(ip.GetAddressBytes()); b.AddRange(user); b.Add(0); send(b.ToArray()); if (receive(8)[1] != 90) throw new IOException(L.Text("proxy.socks4ConnectionRejected"));
            }
            else if (type == 2)
            {
                send(user.Length == 0 ? new byte[] {5,1,0} : new byte[] {5,2,0,2}); var chosen = receive(2); if (chosen[0] != 5 || chosen[1] == 255) throw new IOException(L.Text("proxy.socks5AuthenticationRejected"));
                if (chosen[1] == 2) { if (user.Length > 255 || password.Length > 255) throw new IOException(L.Text("proxy.credentialsTooLong")); var b = new List<byte>(new byte[] {1,(byte)user.Length}); b.AddRange(user); b.Add((byte)password.Length); b.AddRange(password); send(b.ToArray()); if (receive(2)[1] != 0) throw new IOException(L.Text("proxy.socks5LoginRejected")); }
                else if (chosen[1] != 0) throw new IOException(L.Text("proxy.socks5AuthenticationUnsupported"));
                if (host.Length > 255) throw new IOException(L.Text("proxy.hostnameTooLong")); var request = new List<byte>(new byte[] {5,1,0,3,(byte)host.Length}); request.AddRange(host); request.Add((byte)(options.PortNumber >> 8)); request.Add((byte)options.PortNumber); send(request.ToArray()); var result = receive(4); if (result[1] != 0) throw new IOException(L.Text("proxy.socks5DestinationRejected")); receive(result[3] == 1 ? 6 : result[3] == 4 ? 18 : receive(1)[0] + 2);
            }
            else
            {
                string target = (options.HostName.Contains(":") ? "[" + options.HostName + "]" : options.HostName) + ":" + options.PortNumber;
                string request = "CONNECT " + target + " HTTP/1.1\r\nHost: " + target + "\r\n";
                if (user.Length != 0) request += L.Code("proxy.basicAuthorizationHeader") + Convert.ToBase64String(Encoding.UTF8.GetBytes(options.Raw("ProxyUsername", "") + ":" + options.Raw("ProxyPassword", ""))) + "\r\n";
                send(Encoding.ASCII.GetBytes(request + "\r\n")); var response = new StringBuilder(); while (!response.ToString().EndsWith("\r\n\r\n")) { if (response.Length > 32768) throw new IOException(L.Text("proxy.invalidResponse")); response.Append((char)receive(1)[0]); }
                if (!response.ToString().Split('\n')[0].Contains(" 200 ")) throw new IOException(L.Text("proxy.httpTunnelRejected"));
            }
        }
        internal int Retry(Func<int> action, bool throwing = true)
        {
            var timer = Stopwatch.StartNew(); int result;
            while (true) { check(); result = action(); if (result != -37) break; if (timer.Elapsed > options.Timeout) throw new TimeoutException(L.Text("ssh.operationTimeout")); Thread.Sleep(10); }
            if (throwing && result < 0) throw Error(); return result;
        }
        // Pointer operations must not be repeated after they succeeded.
        IntPtr Obtain(Func<IntPtr> action)
        { IntPtr result = IntPtr.Zero; Retry(delegate { result = action(); return result == IntPtr.Zero ? NativeNetwork.libssh2_session_last_errno(session) : 0; }); if (result == IntPtr.Zero) throw Error(); return result; }
        Exception Error() { int code = NativeNetwork.libssh2_session_last_errno(session); if (new[] {-7,-13,-30,-43,-45}.Contains(code)) return new TransientNetworkException(L.Text("ssh.connectionInterruptedPrefix") + code + ")."); IntPtr text; int length; NativeNetwork.libssh2_session_last_error(session, out text, out length, 0); return new SessionRemoteException("SSH/SFTP: " + (text == IntPtr.Zero ? L.Text("error.operationFailed") : Marshal.PtrToStringAnsi(text)) + (sftp == IntPtr.Zero ? "" : " (" + NativeNetwork.libssh2_sftp_last_error(sftp) + ")")); }
        internal string CommandCore(string command)
        {
            var channel = Obtain(delegate { return NativeNetwork.libssh2_channel_open_ex(session, NativeNetwork.Bytes("session"), 7, 2097152, 32768, IntPtr.Zero, 0); });
            try
            {
                var bytes = encoding.GetBytes(command); Retry(delegate { return NativeNetwork.libssh2_channel_process_startup(channel, NativeNetwork.Bytes("exec"), 4, bytes, (uint)bytes.Length); });
                using (var output = new MemoryStream()) using (var errors = new MemoryStream())
                {
                    var buffer = new byte[32768]; var idle = Stopwatch.StartNew();
                    while (true)
                    {
                        check(); bool received = false;
                        for (int stream = 0; stream < 2; stream++) { long n = NativeNetwork.libssh2_channel_read_ex(channel, stream, buffer, (UIntPtr)buffer.Length).ToInt64(); if (n > 0) { (stream == 0 ? output : errors).Write(buffer, 0, (int)n); received = true; } else if (n < 0 && n != -37) throw Error(); }
                        if (received) idle.Restart(); else if (NativeNetwork.libssh2_channel_eof(channel) != 0) break; else { if (idle.Elapsed > options.Timeout) throw new TimeoutException(L.Text("ssh.commandNotResponding")); Thread.Sleep(10); }
                    }
                    Retry(delegate { return NativeNetwork.libssh2_channel_wait_closed(channel); });
                    if (NativeNetwork.libssh2_channel_get_exit_status(channel) != 0) throw new SessionRemoteException(encoding.GetString(errors.ToArray())); return encoding.GetString(output.ToArray());
                }
            }
            finally { if (socket != null && socket.Connected) { try { Retry(delegate { return NativeNetwork.libssh2_channel_free(channel); }); } catch { } } }
        }
        void Sftp() { if (sftp == IntPtr.Zero) sftp = Obtain(delegate { return NativeNetwork.libssh2_sftp_init(session); }); }
        byte[] PathBytes(string path) { return encoding.GetBytes(path); }
        RemoteFileInfo Info(string path, NativeNetwork.Attributes attr)
        { uint mode = attr.Permissions & 61440; return new RemoteFileInfo { Name = RemoteConnection.Name(path), FullName = path, Length = (long)attr.Size, FileType = mode == 40960 ? 'L' : mode == 16384 ? 'D' : '-', LastWriteTime = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(attr.Mtime).ToLocalTime(), FilePermissions = Convert.ToString(attr.Permissions & 4095, 8) }; }
        internal RemoteFileInfo StatCore(string path, bool missing = false)
        {
            Sftp(); var b = PathBytes(path); var attrs = new NativeNetwork.Attributes(); int result = Retry(delegate { return NativeNetwork.libssh2_sftp_stat_ex(sftp, b, (uint)b.Length, 1, ref attrs); }, false);
            if (result < 0) { uint error = NativeNetwork.libssh2_sftp_last_error(sftp); if (missing && (error == 2 || error == 10)) return null; throw Error(); } return Info(path, attrs);
        }
        internal RemoteDirectoryInfo ListCore(string path)
        {
            Sftp(); var b = PathBytes(path); var directory = Obtain(delegate { return NativeNetwork.libssh2_sftp_open_ex(sftp, b, (uint)b.Length, 0, 0, 1); }); var result = new RemoteDirectoryInfo();
            try { var buffer = new byte[65536]; while (true) { var attrs = new NativeNetwork.Attributes(); int count = Retry(delegate { return NativeNetwork.libssh2_sftp_readdir_ex(directory, buffer, (UIntPtr)buffer.Length, IntPtr.Zero, UIntPtr.Zero, out attrs); }); if (count == 0) break; string name = encoding.GetString(buffer, 0, count); if (name != "." && name != "..") result.Files.Add(Info(RemoteConnection.Combine(path, name), attrs)); } }
            finally { Retry(delegate { return NativeNetwork.libssh2_sftp_close_handle(directory); }); } return result;
        }
        internal void MkdirCore(string path) { Sftp(); var b = PathBytes(path); Retry(delegate { return NativeNetwork.libssh2_sftp_mkdir_ex(sftp,b,(uint)b.Length,493); }); }
        internal void RenameCore(string source, string target) { Sftp(); var a = PathBytes(source); var b = PathBytes(target); Retry(delegate { return NativeNetwork.libssh2_sftp_rename_ex(sftp,a,(uint)a.Length,b,(uint)b.Length,0); }); }
        internal void RemoveCore(string path, bool directory) { Sftp(); var b = PathBytes(path); Retry(delegate { return directory ? NativeNetwork.libssh2_sftp_rmdir_ex(sftp,b,(uint)b.Length) : NativeNetwork.libssh2_sftp_unlink_ex(sftp,b,(uint)b.Length); }); }
        internal void SetTimeCore(string path, DateTime time) { Sftp(); var b = PathBytes(path); var attrs = new NativeNetwork.Attributes { Flags = 8, Atime = (uint)(time.ToUniversalTime() - new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds }; attrs.Mtime = attrs.Atime; Retry(delegate { return NativeNetwork.libssh2_sftp_stat_ex(sftp,b,(uint)b.Length,2,ref attrs); }); }
        internal void TransferCore(string path, Stream local, long size, bool upload, Action<long> progress, long offset = 0)
        {
            if (offset < 0 || offset > size || (offset != 0 && options.Protocol == Protocol.Scp)) throw new IOException(L.Text("scp.offsetResumeUnsupported"));
            bool scp = options.Protocol == Protocol.Scp; IntPtr handle; var b = PathBytes(path + (scp ? "\0" : ""));
            if (scp)
            {
                long remoteSize = 0;
                handle = Obtain(delegate { return upload ? NativeNetwork.libssh2_scp_send64(session,b,420,size,0,0) : NativeNetwork.cn_scp_recv(session,b,out remoteSize); });
                if (!upload && remoteSize != size) { Abort(); throw new IOException(L.Text("scp.sourceSizeChanged")); }
            }
            else { Sftp(); handle = Obtain(delegate { return NativeNetwork.libssh2_sftp_open_ex(sftp,b,(uint)b.Length,upload ? (offset > 0 ? 10u : 26u) : 1u,420,0); }); if(offset>0) { NativeNetwork.libssh2_sftp_seek64(handle,(ulong)offset); if(upload) local.Position=offset; } }
            var buffer = new byte[65536]; var pin = GCHandle.Alloc(buffer,GCHandleType.Pinned); bool complete = false;
            try
            {
                long done = offset; progress(offset);
                while (done < size)
                {
                    check(); int count = (int)Math.Min(buffer.Length,size-done);
                    if (upload)
                    {
                        count = local.Read(buffer,0,count); if (count == 0) throw new IOException(L.Text("transfer.sourceSizeChanged"));
                        int blockOffset = 0;
                        while (blockOffset < count)
                        {
                            int current = blockOffset;
                            int written = Retry(delegate { var ptr = IntPtr.Add(pin.AddrOfPinnedObject(),current); return (int)(scp ? NativeNetwork.libssh2_channel_write_ex(handle,0,ptr,(UIntPtr)(count-current)) : NativeNetwork.libssh2_sftp_write(handle,ptr,(UIntPtr)(count-current))).ToInt64(); });
                            if (written == 0) throw new IOException(L.Text("transfer.serverNotAcceptingData")); blockOffset += written; done += written; if (traffic != null) traffic(written); progress(done);
                        }
                    }
                    else
                    {
                        int received = Retry(delegate { return (int)(scp ? NativeNetwork.libssh2_channel_read_ex(handle,0,buffer,(UIntPtr)count) : NativeNetwork.libssh2_sftp_read(handle,buffer,(UIntPtr)count)).ToInt64(); });
                        if (received == 0) throw new IOException(L.Text("transfer.endedPrematurely")); local.Write(buffer,0,received); done += received; if (traffic != null) traffic(received); progress(done);
                    }
                }
                if (scp)
                {
                    buffer[0]=0; Retry(delegate { return (int)NativeNetwork.libssh2_channel_write_ex(handle,0,pin.AddrOfPinnedObject(),(UIntPtr)1).ToInt64(); });
                    if(upload) { int ack = Retry(delegate { return (int)NativeNetwork.libssh2_channel_read_ex(handle,0,buffer,(UIntPtr)1).ToInt64(); }); if(ack!=1 || buffer[0]!=0) throw new SessionRemoteException(L.Text("scp.transferNotConfirmed")); }
                    Retry(delegate { return NativeNetwork.libssh2_channel_send_eof(handle); });
                    Retry(delegate { return NativeNetwork.libssh2_channel_wait_eof(handle); });
                    Retry(delegate { return NativeNetwork.libssh2_channel_wait_closed(handle); });
                    if(NativeNetwork.libssh2_channel_get_exit_status(handle)!=0) throw new SessionRemoteException(L.Text("scp.serverTransferFailed"));
                }
                complete = true;
            }
            finally
            {
                pin.Free(); if (!complete) Abort();
                if(complete) Retry(delegate { return scp ? NativeNetwork.libssh2_channel_free(handle) : NativeNetwork.libssh2_sftp_close_handle(handle); });
            }
        }
        internal void Abort() { var s = socket; if (s != null) { try { s.Close(); } catch { } } }
        public void Dispose() { if (keepAlive != null) keepAlive.Dispose(); Abort(); lock (sync) { if (sftp != IntPtr.Zero) { NativeNetwork.libssh2_sftp_shutdown(sftp); sftp = IntPtr.Zero; } if (session != IntPtr.Zero) { NativeNetwork.libssh2_session_free(session); session = IntPtr.Zero; } } }
    }
}


