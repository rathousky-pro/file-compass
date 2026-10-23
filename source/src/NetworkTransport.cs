using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CommanderCZ.NetworkTransport
{
    public enum Protocol { Ftp, Sftp, Scp }
    public enum FtpMode { Passive, Active }
    public enum FtpSecure { None, Explicit, Implicit }
    public enum TransferMode { Binary }
    public enum OverwriteMode { Overwrite }
    public enum EnumerationOptions { AllDirectories }
    public sealed class SessionOptions
    {
        public Protocol Protocol;
        public string HostName, UserName, Password, SshHostKeyFingerprint, SshPrivateKeyPath, TlsClientCertificatePath;
        public int PortNumber;
        public TimeSpan Timeout;
        public FtpMode FtpMode; public FtpSecure FtpSecure;
        internal Dictionary<string, string> RawSettings { get; private set; }
        public SessionOptions() { RawSettings = new Dictionary<string, string>(); }
        public void AddRawSettings(string key, string value) { RawSettings[key] = value; }
        internal string Raw(string key, string fallback) { string value; return RawSettings.TryGetValue(key, out value) ? value : fallback; }
    }
    public sealed class RemoteFileInfo
    {
        public string Name, FullName, FilePermissions;
        public long Length; public DateTime LastWriteTime;
        public char FileType;
        public bool IsDirectory { get { return FileType == 'D'; } }
        public bool IsParentDirectory { get { return Name == ".."; } }
        public bool IsThisDirectory { get { return Name == "."; } }
    }
    public sealed class RemoteDirectoryInfo { public readonly List<RemoteFileInfo> Files = new List<RemoteFileInfo>(); }
    public sealed class SessionRemoteException : IOException
    {
        public SessionRemoteException(string message) : base(message) { }
        public SessionRemoteException(string message, Exception inner) : base(message, inner) { }
    }
    public sealed class TransferOptions
    {
        public TransferMode TransferMode; public OverwriteMode OverwriteMode; public bool PreserveTimestamp = true, Resume;
    }
    public sealed class TransferResult { public void Check() { } }
    public sealed class FileTransferProgressEventArgs : EventArgs
    {
        public string FileName, Phase; public double FileProgress, OverallProgress; public int CPS; public bool Cancel;
    }
    public sealed class TransferEventArgs : EventArgs { public Exception Error; public long Length; }
    // These paths are always literal; no wildcard expansion is performed by the transport.
    public static class RemotePath
    {
        public static string EscapeFileMask(string path) { return path; }
        public static string EscapeOperationMask(string path) { return path; }
    }
    public sealed partial class Session : IDisposable
    {
        SessionOptions options; NativeSsh ssh; CurlClient curl; volatile bool aborted;
        internal NetworkFlow Flow;
        internal void Traffic(int bytes) { if (Flow != null) Flow.Consume(bytes); }
        bool? ftpMlst;
        public event EventHandler<FileTransferProgressEventArgs> FileTransferProgress;
        public event EventHandler<TransferEventArgs> FileTransferred;
        void CheckAbort() { if (aborted) throw new OperationCanceledException(); if (Flow != null) Flow.Check(); if (aborted) throw new OperationCanceledException(); }
        CurlClient Curl { get { CheckAbort(); if (curl == null) curl = new CurlClient(options, CheckAbort,Traffic); return curl; } }
        public void Open(SessionOptions settings)
        {
            CheckAbort(); options = settings;
            try
            {
                if (settings.Protocol == Protocol.Ftp)
                {
                    if(settings.Raw("Utf","2")=="2")
                    {
                        string capabilities=""; try { capabilities=Curl.Reply("FEAT"); } catch(SessionRemoteException) { }
                        settings.AddRawSettings("UtfAuto", "1");
                        settings.AddRawSettings("Utf", "1");
                    }
                    if(settings.Raw("Utf","2")=="1") { try { Curl.Commands("OPTS UTF8 ON"); } catch(SessionRemoteException) { } }
                    var commands = settings.Raw("PostLoginCommands", "").Split('\n').Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
                    Curl.Commands(commands);
                }
                else { ssh = new NativeSsh(settings, CheckAbort,Traffic); ssh.Open(false); }
                CheckAbort();
            }
            catch (Exception ex) { Dispose(); CheckAbort(); throw new SessionRemoteException(L.Text("network.connectionToPrefix") + settings.HostName + " selhalo: " + ex.Message, ex); }
        }
        public string ScanFingerprint(SessionOptions settings, string algorithm)
        { using (var probe = new NativeSsh(settings, CheckAbort)) { probe.Open(true); return probe.Fingerprint; } }
        internal Tuple<string, string> ScanSshfsKey(SessionOptions settings)
        { using (var probe = new NativeSsh(settings, CheckAbort)) { probe.Open(true); return Tuple.Create(probe.Fingerprint, probe.KnownHostKey); } }
        string Command(string command) { CheckAbort(); return ssh.Command(command); }
        public RemoteDirectoryInfo ListDirectory(string path)
        {
            CheckAbort(); path = RemoteConnection.Normalize(path);
            if (options.Protocol == Protocol.Sftp) return ssh.List(path);
            var result = new RemoteDirectoryInfo();
            if (options.Protocol == Protocol.Scp) { result.Files.AddRange(ShellListing(path)); return result; }
            string mode = options.Raw("FtpUseMlsd", "auto"); bool machine = mode != "off"; byte[] data;
            try { data = Curl.Listing(path, machine ? "MLSD" : options.Raw("FtpListAll", "auto") == "on" ? "LIST -a" : "LIST"); }
            catch (SessionRemoteException) { if (!machine || mode == "on") throw; machine = false; data = Curl.Listing(path, options.Raw("FtpListAll", "auto") == "on" ? "LIST -a" : "LIST"); }
            string listing;
            if (options.Raw("UtfAuto", "0") == "1") {
                try { listing = new UTF8Encoding(false,true).GetString(data); if (data.Any(x=>x>=128)) options.AddRawSettings("Utf","1"); }
                catch (DecoderFallbackException) { listing=Encoding.Default.GetString(data); options.AddRawSettings("Utf","0"); }
            } else listing = (options.Raw("Utf", "2") == "0" ? Encoding.Default : Encoding.UTF8).GetString(data);
            foreach (string line in listing.Split('\n'))
            {
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("total ")) continue;
                string name; char type; long size = 0; DateTime time = new DateTime(1970,1,1); string permissions = "";
                if (machine)
                {
                    int split = line.IndexOf(' '); if (split < 0) throw new SessionRemoteException(L.Text("ftp.invalidMlsdListing")); name = line.Substring(split + 1).TrimEnd('\r'); type = '-';
                    bool special = false;
                    foreach (string fact in line.Substring(0,split).Split(';'))
                    { int eq = fact.IndexOf('='); if (eq < 0) continue; string key = fact.Substring(0,eq).ToLowerInvariant(), value = fact.Substring(eq+1);
                      if (key == "type") { special = value == "cdir" || value == "pdir"; type = value == "dir" ? 'D' : value.IndexOf("slink", StringComparison.OrdinalIgnoreCase) >= 0 ? 'L' : '-'; }
                      else if (key == "size") Int64.TryParse(value,out size);
                      else if (key == "modify") { DateTime utc; if (DateTime.TryParseExact(value.Split('.')[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc)) time = utc.ToLocalTime(); }
                      else if (key == "unix.mode") permissions = value;
                    }
                    if (special) continue;
                }
                else
                {
                    var match = Regex.Match(line, @"^([dl-][rwxstST-]{9}[+@.]?)\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(\w+)\s+(\d+)\s+([\d:]+)\s(.*)$");
                    if (match.Success)
                    { name = match.Groups[6].Value.TrimEnd('\r'); type = match.Groups[1].Value[0] == 'd' ? 'D' : match.Groups[1].Value[0] == 'l' ? 'L' : '-'; size = Int64.Parse(match.Groups[2].Value); permissions = match.Groups[1].Value;
                      if (type == 'L') { int arrow = name.LastIndexOf(" -> ",StringComparison.Ordinal); if (arrow >= 0) name = name.Substring(0,arrow); }
                      string date = match.Groups[3].Value+" "+match.Groups[4].Value+" "+match.Groups[5].Value; bool recent = date.Contains(":");
                      if (!DateTime.TryParseExact(date,recent ? "MMM d HH:mm" : "MMM d yyyy",CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out time)) time = new DateTime(1970,1,1); else if (recent && time > DateTime.Now.AddDays(1)) time = time.AddYears(-1);
                    }
                    else
                    { match = Regex.Match(line, @"^(\d{2}-\d{2}-\d{2,4})\s+(\d{2}:\d{2}[AP]M)\s+(<DIR>|\d+)\s+(.*)\r?$"); if (!match.Success) throw new SessionRemoteException(L.Text("ftp.unsupportedListingFormat")); name = match.Groups[4].Value.TrimEnd('\r'); type = match.Groups[3].Value == "<DIR>" ? 'D' : '-'; if (type != 'D') size = Int64.Parse(match.Groups[3].Value); DateTime.TryParseExact(match.Groups[1].Value+" "+match.Groups[2].Value,new[]{"MM-dd-yy hh:mmtt","MM-dd-yyyy hh:mmtt"},CultureInfo.InvariantCulture,DateTimeStyles.None,out time); }
                }
                if (name != "." && name != "..") result.Files.Add(new RemoteFileInfo { Name=name, FullName=RemoteConnection.Combine(path,name), Length=Math.Max(0,size), FileType=type, LastWriteTime=time, FilePermissions=permissions });
            }
            return result;
        }
        internal static string Quote(string value)
        {
            if (value == null || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new IOException(L.Text("network.invalidRemotePath"));
            return "'" + value.Replace("'", "'\\''") + "'";
        }
        IEnumerable<RemoteFileInfo> ShellListing(string path)
        {
            // GNU find is lossless for filenames with whitespace; a traditional Unix ls
            // fallback permits browsing servers with BusyBox/BSD userland.
            string listing;
            try
            {
                listing = Command("LC_ALL=C find " + Quote(path) + " -mindepth 1 -maxdepth 1 -printf '%y\\0%s\\0%T@\\0%f\\0'");
                var parts = listing.Split('\0'); var files = new List<RemoteFileInfo>();
                for (int i = 0; i + 3 < parts.Length; i += 4)
                {
                    long length; double timestamp;
                    if (!Int64.TryParse(parts[i + 1], out length) || !Double.TryParse(parts[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp)) throw new IOException(L.Text("scp.invalidListing"));
                    files.Add(new RemoteFileInfo { Name = parts[i + 3], FullName = RemoteConnection.Combine(path, parts[i + 3]), Length = length,
                        FileType = parts[i] == "d" ? 'D' : parts[i] == "l" ? 'L' : '-', LastWriteTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime(), FilePermissions = "" });
                }
                return files;
            }
            catch (SessionRemoteException) { listing = Command("LC_ALL=C ls -lan -- " + Quote(path)); }
            var fallback = new List<RemoteFileInfo>();
            foreach (string line in listing.Split('\n'))
            {
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("total ")) continue;
                var match = Regex.Match(line, @"^([dl-][rwxstST-]{9}[+@.]?)\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(\w+)\s+(\d+)\s+([\d:]+)\s(.*)$");
                if (!match.Success) throw new SessionRemoteException(L.Text("scp.unsupportedListingFormat"));
                string name = match.Groups[6].Value.TrimEnd('\r');
                if (match.Groups[1].Value[0] == 'l') { int arrow = name.LastIndexOf(" -> ", StringComparison.Ordinal); if (arrow >= 0) name = name.Substring(0, arrow); }
                string date = match.Groups[3].Value + " " + match.Groups[4].Value + " " + match.Groups[5].Value;
                DateTime time; bool recent = date.IndexOf(':') >= 0;
                if (!DateTime.TryParseExact(date, recent ? "MMM d HH:mm" : "MMM d yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time)) time = new DateTime(1970, 1, 1);
                else if (recent && time > DateTime.Now.AddDays(1)) time = time.AddYears(-1);
                fallback.Add(new RemoteFileInfo { Name = name, FullName = RemoteConnection.Combine(path, name), Length = Int64.Parse(match.Groups[2].Value),
                    LastWriteTime = time, FileType = match.Groups[1].Value[0] == 'd' ? 'D' : match.Groups[1].Value[0] == 'l' ? 'L' : '-', FilePermissions = match.Groups[1].Value });
            }
            return fallback;
        }
        public RemoteFileInfo GetFileInfo(string path)
        {
            CheckAbort(); path = RemoteConnection.Normalize(path);
            if (path == "/") return new RemoteFileInfo { Name="/", FullName="/", FileType='D', LastWriteTime=new DateTime(1970,1,1) };
            if (options.Protocol == Protocol.Sftp) return ssh.Stat(path);
            if(options.Protocol==Protocol.Ftp && ftpMlst!=false)
            {
                try
                {
                    string response=Curl.Reply("MLST " + path);
                    var match=Regex.Match(response,@"(?m)^\s*([^\s]*=[^\s]*;) (.*)\r?$");
                    if(match.Success)
                    {
                        ftpMlst=true; var info=new RemoteFileInfo { Name=RemoteConnection.Name(path),FullName=path,FileType='-',LastWriteTime=new DateTime(1970,1,1),FilePermissions="" };
                        foreach(string fact in match.Groups[1].Value.Split(';')) { int eq=fact.IndexOf('='); if(eq<0) continue; string key=fact.Substring(0,eq).ToLowerInvariant(),value=fact.Substring(eq+1);
                          if(key=="type") info.FileType=value.Equals("dir",StringComparison.OrdinalIgnoreCase) ? 'D' : value.IndexOf("slink",StringComparison.OrdinalIgnoreCase)>=0 ? 'L' : '-';
                          else if(key=="size") Int64.TryParse(value,out info.Length);
                          else if(key=="modify") { DateTime utc; if(DateTime.TryParseExact(value.Split('.')[0],"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out utc)) info.LastWriteTime=utc.ToLocalTime(); }
                          else if(key=="unix.mode") info.FilePermissions=value;
                        }
                        return info;
                    }
                    ftpMlst=false;
                }
                catch(SessionRemoteException) { if(ftpMlst!=true) ftpMlst=false; }
            }
            var found = ListDirectory(RemoteConnection.Parent(path)).Files.FirstOrDefault(x => x.FullName == path);
            if (found == null) throw new SessionRemoteException(L.Text("network.remoteItemMissingPrefix") + path); return found;
        }
        public bool FileExists(string path)
        { CheckAbort(); path=RemoteConnection.Normalize(path); if(path=="/") return true; if(options.Protocol==Protocol.Sftp) return ssh.Stat(path,true)!=null; return ListDirectory(RemoteConnection.Parent(path)).Files.Any(x=>x.FullName==path); }
        public void CreateDirectory(string path)
        { CheckAbort(); if(options.Protocol==Protocol.Ftp) Curl.Commands("MKD " + path); else if(options.Protocol==Protocol.Sftp) ssh.Mkdir(path); else Command("mkdir -- " + Quote(path)); }
        public void MoveFile(string source, string target)
        { CheckAbort(); if(options.Protocol==Protocol.Ftp) Curl.Commands("RNFR " + source,"RNTO " + target); else if(options.Protocol==Protocol.Sftp) ssh.Rename(source,target); else Command("mv -- " + Quote(source) + " " + Quote(target)); }
        public IEnumerable<RemoteFileInfo> EnumerateRemoteFiles(string path, string mask, EnumerationOptions enumeration)
        { foreach(var file in ListDirectory(path).Files) { if(file.IsParentDirectory || file.IsThisDirectory) continue; if(file.IsDirectory) foreach(var child in EnumerateRemoteFiles(file.FullName,mask,enumeration)) yield return child; else yield return file; } }
        public TransferResult RemoveFiles(string path)
        {
            DeleteOperations.Validate(path,true); var info=GetFileInfo(path); CheckAbort();
            if(options.Protocol==Protocol.Scp) Command("rm -rf -- " + Quote(path));
            else { if(info.IsDirectory) foreach(var child in ListDirectory(path).Files.Where(x=>!x.IsThisDirectory && !x.IsParentDirectory)) RemoveFiles(child.FullName);
              if(options.Protocol==Protocol.Ftp) Curl.Commands((info.IsDirectory ? "RMD " : "DELE ") + path); else ssh.Remove(path,info.IsDirectory); }
            return new TransferResult();
        }
        void SetTime(string path, DateTime modified)
        { if(options.Protocol==Protocol.Ftp) Curl.Commands("MFMT " + modified.ToUniversalTime().ToString("yyyyMMddHHmmss",CultureInfo.InvariantCulture) + " " + path); else if(options.Protocol==Protocol.Sftp) ssh.SetTime(path,modified); else Command("TZ=UTC touch -t " + modified.ToUniversalTime().ToString("yyyyMMddHHmm.ss",CultureInfo.InvariantCulture) + " -- " + Quote(path)); }
        void Progress(string name, long transferred, long total, Stopwatch timer, long existing = 0, string phase = null)
        {
            CheckAbort(); var handler = FileTransferProgress;
            if (handler == null) return;
            var e = new FileTransferProgressEventArgs { FileName = name, Phase = phase, FileProgress = total > 0 ? Math.Min(1, (double)transferred / total) : 1,
                OverallProgress = total > 0 ? Math.Min(1, (double)transferred / total) : 1, CPS = (int)Math.Min(Int32.MaxValue, Math.Max(0, transferred-existing) / Math.Max(.001, timer.Elapsed.TotalSeconds)) };
            handler(this, e); if (e.Cancel) throw new OperationCanceledException(); CheckAbort();
        }
        public TransferResult GetFiles(string remote, string local, bool remove = false, TransferOptions transfer = null)
        {
            if (options.Protocol != Protocol.Scp && options.Raw("TransientTransfer","0") != "1") return DownloadCheckpointed(remote, local, remove, transfer);
            var info = GetFileInfo(remote); if (info.FileType == 'L' || info.IsDirectory) throw new IOException(L.Text("transfer.regularSourceFileRequiredPrefix") + remote);
            string target = local.EndsWith("\\") || Directory.Exists(local) ? Path.Combine(local, info.Name) : local;
            string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)), ".FileCompass-download-" + Guid.NewGuid().ToString("N"));
            var timer = Stopwatch.StartNew();
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if(options.Protocol==Protocol.Ftp) Curl.Download(remote, output, info.Length, delegate(long done) { Progress(remote, done, info.Length, timer); });
                    else ssh.Transfer(remote, output, info.Length, false, delegate(long done) { Progress(remote, done, info.Length, timer); });
                    output.Flush(true);
                }
                CheckAbort(); if (transfer == null || transfer.PreserveTimestamp) File.SetLastWriteTime(temporary, info.LastWriteTime);
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
                if (remove) RemoveFiles(remote); Completed(info.Length); return new TransferResult();
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public TransferResult PutFiles(string local, string remote, bool remove = false, TransferOptions transfer = null)
        {
            if (options.Protocol != Protocol.Scp && options.Raw("TransientTransfer","0") != "1") return UploadCheckpointed(local, remote, remove, transfer);
            // Open the source before creating any remote file: local sharing violations
            // remain retryable without disrupting the independent browsing connection.
            using (var input = new FileStream(local, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = input.Length; string target = remote.EndsWith("/") ? RemoteConnection.Combine(remote, Path.GetFileName(local)) : remote;
                string folder = RemoteConnection.Parent(target), temporary = RemoteConnection.Combine(folder, ".FileCompass-upload-" + Guid.NewGuid().ToString("N")),
                    backup = RemoteConnection.Combine(folder, ".FileCompass-old-" + Guid.NewGuid().ToString("N"));
                bool staged = false, oldMoved = false, committed = false; var timer = Stopwatch.StartNew();
                bool transient = options.Raw("TransientTransfer","0") == "1";
                bool targetExisted = transient && FileExists(target); var targetBefore = targetExisted ? GetFileInfo(target) : null;
                try
                {
                    staged = true;
                    if(options.Protocol==Protocol.Ftp) Curl.Upload(temporary, input, length, delegate(long done) { Progress(local, done, length, timer); });
                    else ssh.Transfer(temporary, input, length, true, delegate(long done) { Progress(local, done, length, timer); });
                    CheckAbort(); if (GetFileInfo(temporary).Length != length) throw new IOException(L.Text("transfer.remoteTargetSizeMismatch"));
                    if (transfer == null || transfer.PreserveTimestamp) SetTime(temporary, File.GetLastWriteTime(local));
                    CheckAbort();
                    if (transient) {
                        bool existsNow = FileExists(target); var targetNow = existsNow ? GetFileInfo(target) : null;
                        if (existsNow != targetExisted || (existsNow && (targetNow.Length != targetBefore.Length || targetNow.LastWriteTime != targetBefore.LastWriteTime || targetNow.FileType != targetBefore.FileType))) throw new IOException(L.Text("transfer.changedTargetOverwriteRejected"));
                    }
                    if (FileExists(target)) { var old = GetFileInfo(target); if (old.IsDirectory || old.FileType == 'L') throw new IOException(L.Text("transfer.targetNotRegularFilePrefix") + target); MoveFile(target, backup); oldMoved = true; }
                    MoveFile(temporary, target); committed = true; staged = false;
                    if (oldMoved) { RemoveFiles(backup); oldMoved = false; }
                    if (remove) File.Delete(local); Completed(length); return new TransferResult();
                }
                catch
                {
                    if (oldMoved && !committed && !aborted)
                    {
                        try { MoveFile(backup, target); oldMoved = false; }
                        catch (Exception ex) { throw new IOException(L.Text("transfer.originalPreservedAtPrefix") + backup + ": " + ex.Message, ex); }
                    }
                    throw;
                }
                finally
                {
                    // A cancelled transport may be disconnected; cleanup uses a new session
                    // with the same pinned host key and cannot touch the browsing session.
                    if (staged || oldMoved)
                        try
                        {
                            using (var cleanup = new Session())
                            {
                                if (transient && aborted && options.Timeout > TimeSpan.FromSeconds(5)) options.Timeout = TimeSpan.FromSeconds(5);
                                cleanup.Open(options);
                                if (oldMoved && !committed) { if (cleanup.FileExists(target)) cleanup.RemoveFiles(target); cleanup.MoveFile(backup, target); }
                                if (staged && cleanup.FileExists(temporary)) cleanup.RemoveFiles(temporary);
                                if (oldMoved && committed && cleanup.FileExists(backup)) cleanup.RemoveFiles(backup);
                            }
                        }
                        catch { /* Keep recoverable remote paths; never destroy the backup. */ }
                }
            }
        }
        void Completed(long length) { var handler = FileTransferred; if (handler != null) handler(this, new TransferEventArgs { Length = length }); }
        public void Abort() { aborted=true; var c=curl; if(c!=null) c.Abort(); var s=ssh; if(s!=null) s.Abort(); }
        public void Dispose() { if(curl!=null) { curl.Dispose(); curl=null; } if(ssh!=null) { ssh.Dispose(); ssh=null; } }
    }
}

