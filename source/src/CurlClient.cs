using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace FileCompass.NetworkTransport
{
    // Each worker owns its handles. Only multi_wakeup is called from cancellation.
    internal sealed class CurlClient : IDisposable
    {
        readonly SessionOptions options;
        readonly Action check;
        readonly Action<int> traffic;
        readonly object gate = new object();
        IntPtr easy, multi;
        readonly Dictionary<IntPtr,int> memory = new Dictionary<IntPtr,int>();
        IntPtr quotes;
        IntPtr errorBuffer;
        Exception failure;
        bool configured;
        long outputLimit = Int64.MaxValue, transferTotal;
        NativeNetwork.Io write, read;
        NativeNetwork.Io header;
        MemoryStream replies;
        string serverError;
        NativeNetwork.Progress progress;
        internal CurlClient(SessionOptions settings, Action cancellation, Action<int> charge = null)
        {
            if(settings.Protocol!=Protocol.Ftp) throw new ArgumentException("Tento klient podporuje FTP/FTPS.");
            options = settings; check = cancellation; traffic = charge; NativeNetwork.Ensure();
            easy = NativeNetwork.curl_easy_init(); multi = NativeNetwork.curl_multi_init();
            if (easy == IntPtr.Zero || multi == IntPtr.Zero) { Dispose(); throw new IOException(L.Text("network.sessionCreationFailed")); }
        }
        internal static void DownloadHttps(string url, Stream output, System.Threading.CancellationToken token, Action<long, long> report)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(L.Text("download.httpsRequired"));
            using (var client = new CurlClient(new SessionOptions { Protocol = Protocol.Ftp }, token.ThrowIfCancellationRequested))
            {
                client.outputLimit = 128L * 1024 * 1024;
                client.Text(10002, url); client.Text(10018, "FileCompass/1.0 SSHFS-setup");
                client.Text(10318, "https"); client.Text(10319, "https"); // Restrict original URL and every redirect.
                client.Number(52, 1); client.Number(68, 8); client.Number(45, 1);
                client.Number(64, 1); client.Number(81, 2); client.Number(216, 16); // Windows trust store, no TLS verification bypass.
                client.Number(156, 30000); client.Number(155, 600000); client.Number(19, 1); client.Number(20, 30); client.Number(99, 1);
                client.Run(null, output, -1, delegate(long done) { report(done, client.transferTotal); });
            }
        }
        void Result(int result) { if (result != 0) { string message = "libcurl: " + Marshal.PtrToStringAnsi(NativeNetwork.curl_easy_strerror(result)) + (errorBuffer==IntPtr.Zero ? "" : L.Text("common.errorSeparator") + Marshal.PtrToStringAnsi(errorBuffer)) + (String.IsNullOrEmpty(serverError) ? "" : L.Text("ftp.serverResponsePrefix") + serverError); if (new[] {5,6,7,18,28,52,55,56}.Contains(result) || (!String.IsNullOrEmpty(serverError) && new[] {"421","425","426","450","451","452"}.Any(x => serverError.StartsWith(x)))) throw new TransientNetworkException(message); throw new SessionRemoteException(message); } }
        void Number(int option, int value) { Result(NativeNetwork.cn_setopt_long(easy, option, value)); }
        void Pointer(int option, IntPtr value) { Result(NativeNetwork.cn_setopt_ptr(easy, option, value)); }
        void Text(int option, string value)
        { var bytes = NativeNetwork.Bytes(value); IntPtr ptr = Marshal.AllocHGlobal(bytes.Length); memory.Add(ptr,bytes.Length); try { Marshal.Copy(bytes, 0, ptr, bytes.Length); Pointer(option, ptr); } finally {Array.Clear(bytes,0,bytes.Length);} }
        void FileBlob(int option,string path)
        {
            // LibreSSL file APIs use ANSI paths on Windows. Read through .NET so
            // certificates work in Czech-named directories too.
            byte[] bytes=File.ReadAllBytes(path); IntPtr data=Marshal.AllocHGlobal(bytes.Length),blob=Marshal.AllocHGlobal(24);
            memory.Add(data,bytes.Length); memory.Add(blob,24); try {Marshal.Copy(bytes,0,data,bytes.Length); Marshal.WriteIntPtr(blob,0,data); Marshal.WriteInt64(blob,8,bytes.Length); Marshal.WriteInt32(blob,16,1); Pointer(option,blob);}finally{Array.Clear(bytes,0,bytes.Length);}
        }
        string Url(string path, bool directory)
        {
            if (path.IndexOfAny(new[] {'\0','\r','\n'}) >= 0) throw new IOException(L.Text("network.invalidRemotePath"));
            var encoding = options.Raw("Utf", "2") == "0" ? Encoding.Default : Encoding.UTF8;
            var result = new StringBuilder();
            foreach (byte b in encoding.GetBytes(RemoteConnection.Normalize(path)))
                if (b == '/' || b >= 'a' && b <= 'z' || b >= 'A' && b <= 'Z' || b >= '0' && b <= '9' || b == '-' || b == '_' || b == '.' || b == '~') result.Append((char)b);
                else result.Append('%').Append(b.ToString("X2"));
            string protocol = options.FtpSecure == FtpSecure.Implicit ? "ftps" : "ftp";
            string host = options.HostName.Contains(":") ? "[" + options.HostName + "]" : options.HostName;
            string escaped = result.ToString(); if (directory && !escaped.EndsWith("/")) escaped += "/";
            return protocol + "://" + host + ":" + options.PortNumber + ("/%2f" + escaped.TrimStart('/'));
        }
        void Configure(string path, bool directory)
        {
            check(); failure = null; Number(44,0); Number(46,0); Number(50,0); Result(NativeNetwork.cn_setopt_off(easy,30116,0)); Pointer(10007,IntPtr.Zero); Pointer(10036,IntPtr.Zero); Pointer(10028,IntPtr.Zero);
            errorBuffer=Marshal.AllocHGlobal(256); memory.Add(errorBuffer,256); Marshal.Copy(new byte[256],0,errorBuffer,256); Pointer(10010,errorBuffer);
            Text(10002, Url(path, directory));
            if (options.Raw("Utf", "2") == "1") {
                quotes = NativeNetwork.curl_slist_append(quotes, Encoding.ASCII.GetBytes("*OPTS UTF8 ON\0"));
                if (quotes == IntPtr.Zero) throw new OutOfMemoryException();
                Pointer(10028, quotes);
            }
            if(configured) return; Text(10173, options.UserName); Text(10174, options.Password);
            Number(156, checked((int)options.Timeout.TotalMilliseconds)); Number(324, checked((int)options.Timeout.TotalMilliseconds));
            Number(19, 1); Number(20, Math.Max(1, (int)options.Timeout.TotalSeconds)); Number(99, 1); Number(213, 1);
            int proxy = Int32.Parse(options.Raw("ProxyMethod", "0"));
            Text(10004, proxy == 0 ? "" : options.Raw("ProxyHost", ""));
            if (proxy != 0)
            { Number(59, Int32.Parse(options.Raw("ProxyPort", "8080"))); Number(101, proxy == 1 ? 4 : proxy == 2 ? 7 : 0); Text(10177, ""); Text(10175, options.Raw("ProxyUsername", "")); Text(10176, options.Raw("ProxyPassword", "")); if (proxy == 3) Number(61, 1); }
            if (options.Protocol == Protocol.Ftp)
            {
                if (options.FtpMode == FtpMode.Active) Text(10017, "-");
                Number(119, options.FtpSecure == FtpSecure.None ? 0 : 3); Number(64, 1); Number(81, 2); Number(216,16);
                if(!String.IsNullOrEmpty(options.Raw("TlsCaFile",""))) FileBlob(40309,options.Raw("TlsCaFile",""));
                if (!String.IsNullOrEmpty(options.TlsClientCertificatePath))
                { string extension = Path.GetExtension(options.TlsClientCertificatePath).ToLowerInvariant(); FileBlob(40291, options.TlsClientCertificatePath); Text(10086, extension == ".pfx" || extension == ".p12" ? "P12" : extension == ".cer" || extension == ".der" ? "DER" : "PEM"); if (extension == ".pem") FileBlob(40292, options.TlsClientCertificatePath); }
            }
            configured=true;
        }
        void Run(Stream input, Stream output, long length, Action<long> report)
        {
            serverError = null;
            var buffer = new byte[65536]; long done = 0;
            write = delegate(IntPtr data, UIntPtr size, UIntPtr count, IntPtr user)
            {
                try { check(); int n = checked((int)(size.ToUInt64() * count.ToUInt64())); if (done > outputLimit - n) throw new IOException(L.Text("download.installerSizeLimit")); int offset = 0; while (offset < n) { int block = Math.Min(buffer.Length, n - offset); Marshal.Copy(IntPtr.Add(data, offset), buffer, 0, block); if (output != null) output.Write(buffer, 0, block); offset += block; } done += n; if (traffic != null) traffic(n); return (UIntPtr)n; }
                catch (Exception ex) { failure = ex; return UIntPtr.Zero; }
            };
            read = delegate(IntPtr data, UIntPtr size, UIntPtr count, IntPtr user)
            {
                try { check(); int n = input == null ? 0 : input.Read(buffer, 0, (int)Math.Min((ulong)buffer.Length, size.ToUInt64() * count.ToUInt64())); Marshal.Copy(buffer, 0, data, n); done += n; if (traffic != null) traffic(n); return (UIntPtr)n; }
                catch (Exception ex) { failure = ex; return (UIntPtr)268435456; }
            };
            progress = delegate(IntPtr user, long dt, long dd, long ut, long ud)
            { try { check(); transferTotal = dt; if (report != null) report(input == null ? dd : ud); return 0; } catch (Exception ex) { failure = ex; return 1; } };
            header = delegate(IntPtr data,UIntPtr size,UIntPtr count,IntPtr user)
            { try { int n=checked((int)(size.ToUInt64()*count.ToUInt64())); var bytes=new byte[n]; Marshal.Copy(data,bytes,0,n); if(replies!=null) replies.Write(bytes,0,n); string line=Encoding.UTF8.GetString(bytes).Trim(); if(line.Length >= 3 && (line[0]=='4' || line[0]=='5') && Char.IsDigit(line[1]) && Char.IsDigit(line[2])) { if (!String.IsNullOrEmpty(options.Password)) line = line.Replace(options.Password, "***"); serverError = line.Length > 1000 ? line.Substring(0,1000) : line; } return (UIntPtr)n; } catch(Exception ex) { failure=ex; return UIntPtr.Zero; } };
            Pointer(20079,Marshal.GetFunctionPointerForDelegate(header));
            Pointer(20011, Marshal.GetFunctionPointerForDelegate(write)); Pointer(20012, Marshal.GetFunctionPointerForDelegate(read)); Pointer(20219, Marshal.GetFunctionPointerForDelegate(progress)); Number(43, 0);
            bool added = false;
            try
            {
                int error = NativeNetwork.curl_multi_add_handle(multi, easy); if (error != 0) throw new IOException(L.Text("network.operationStartFailedPrefix") + error); added = true;
                int running, ready, left; IntPtr message;
                while (true)
                {
                    check(); error = NativeNetwork.curl_multi_perform(multi, out running); if (error != 0) throw new IOException(L.Text("network.operationFailedPrefix") + error);
                    while ((message = NativeNetwork.curl_multi_info_read(multi, out left)) != IntPtr.Zero)
                        if (Marshal.ReadInt32(message) == 1) { check(); if (failure != null) throw failure; Result(Marshal.ReadInt32(message, 16)); if (length >= 0 && done != length) throw new IOException(L.Text("transfer.sizeMismatch")); return; }
                    error = NativeNetwork.curl_multi_poll(multi, IntPtr.Zero, 0, 50, out ready); if (error != 0) throw new IOException(L.Text("network.waitFailedPrefix") + error);
                }
            }
            finally
            {
                if (added) NativeNetwork.curl_multi_remove_handle(multi, easy);
                GC.KeepAlive(write); GC.KeepAlive(read); GC.KeepAlive(progress); GC.KeepAlive(header);
            }
        }
        void Release()
        { Pointer(10028,IntPtr.Zero); Pointer(10010,IntPtr.Zero); Pointer(20011,IntPtr.Zero); Pointer(20012,IntPtr.Zero); Pointer(20219,IntPtr.Zero); Pointer(20079,IntPtr.Zero); if (quotes != IntPtr.Zero) { NativeNetwork.curl_slist_free_all(quotes); quotes = IntPtr.Zero; } foreach (var ptr in memory) SensitiveMemory.Free(ptr.Key,ptr.Value); memory.Clear(); errorBuffer=IntPtr.Zero; write = read = header = null; progress = null; }
        internal byte[] Listing(string path, string command)
        { try { Configure(path, true); Text(10036, command); using (var result = new MemoryStream()) { Run(null, result, -1, null); return result.ToArray(); } } finally { Release(); } }
        internal void Commands(params string[] commands)
        {
            try { Configure("/", true); Number(44, 1); foreach (string command in commands) { if (command.IndexOfAny(new[] {'\r','\n','\0'}) >= 0) throw new IOException(L.Text("ftp.invalidCommand")); var enc = options.Raw("Utf", "2") == "0" ? Encoding.Default : Encoding.UTF8; var next = NativeNetwork.curl_slist_append(quotes, enc.GetBytes(command + "\0")); if (next == IntPtr.Zero) throw new OutOfMemoryException(); quotes = next; } Pointer(10028, quotes); Run(null, null, -1, null); } finally { Release(); }
        }
        internal string Reply(string command)
        { using(var response=new MemoryStream()) { replies=response; try { Commands(command); return (options.Raw("Utf","2")=="0" ? Encoding.Default : Encoding.UTF8).GetString(response.ToArray()); } finally { replies=null; } } }
        internal void Download(string path, Stream target, long size, Action<long> report, long offset = 0)
        { try { Configure(path, false); Result(NativeNetwork.cn_setopt_off(easy,30116,offset)); Run(null, target, size-offset, report == null ? null : new Action<long>(delegate(long done) { report(offset+done); })); } finally { Release(); } }
        internal void ReadRange(string path, Stream target, long offset, int count)
        { try { Configure(path, false); Text(10007, offset + "-" + (offset + count - 1)); Run(null, target, count, null); } finally { Release(); } }
        internal void Upload(string path, Stream source, long size, Action<long> report, long offset = 0)
        { try { Configure(path, false); Number(46, 1); if(offset>0) { source.Position=offset; Number(50,1); } Result(NativeNetwork.cn_setopt_off(easy, 30115, size-offset)); Run(source, null, size-offset, report == null ? null : new Action<long>(delegate(long done) { report(offset+done); })); } finally { Release(); } }
        internal void Abort() { lock (gate) if (multi != IntPtr.Zero) NativeNetwork.curl_multi_wakeup(multi); }
        public void Dispose()
        { IntPtr oldMulti; lock(gate) { oldMulti=multi; multi=IntPtr.Zero; } if(easy!=IntPtr.Zero) { Release(); NativeNetwork.curl_easy_cleanup(easy); easy=IntPtr.Zero; } if(oldMulti!=IntPtr.Zero) NativeNetwork.curl_multi_cleanup(oldMulti); }
    }
}

