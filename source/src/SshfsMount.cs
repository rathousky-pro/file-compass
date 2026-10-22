using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    public class SshfsMountRecord
    {
        public string ProfileId, Drive, Remote, Executable;
        public int ProcessId; public long Started;
        public bool Keep;
    }
    internal static class SshfsMount
    {
        internal static string TestRoot;
        static readonly object gate = new object();
        static readonly HashSet<string> pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ownedHere = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static string Root { get { return TestRoot ?? System.IO.Path.Combine(AppPaths.ConfigDirectory, "sshfs"); } }
        internal static string Executable()
        {
            string plugin = Path.Combine(AppPaths.Resource("plugins"), "sshfs", "bin", "sshfs.exe"); if (File.Exists(plugin)) return plugin;
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            { string file = Path.Combine(root, "SSHFS-Win", "bin", "sshfs.exe"); if (File.Exists(file)) return file; }
            return null;
        }
        internal static string DependencyStatus()
        { int missing = SshfsSetup.MissingPackages(); return missing == 0 ? L.Text("sshfs.dependenciesAvailable") : L.Text("sshfs.missingDependencyPrefix") + ((missing & 1) != 0 ? "WinFsp" : "") + (missing == 3 ? " a " : "") + ((missing & 2) != 0 ? "SSHFS-Win" : "") + L.Text("sshfs.installDependencySuffix"); }
        internal static void Validate(NetworkProfile p)
        {
            if (p.DriveLetter == null || p.DriveLetter.Length != 2 || p.DriveLetter[0] < 'D' || p.DriveLetter[0] > 'Z' || p.DriveLetter[1] != ':') throw new ArgumentException(L.Text("sshfs.driveLetterRequired"));
            if (p.UserName.StartsWith("-") || p.UserName.IndexOfAny(new[] { '@', '!', '=', '/', '\\', ':', ' ', '\t', '\n', '\r', '\0' }) >= 0 || p.Host.IndexOfAny(new[] { '@', '!', '=', ':', '\0' }) >= 0) throw new ArgumentException(L.Text("sshfs.validUserAndHostRequired"));
            if (String.IsNullOrEmpty(p.Path) || !p.Path.StartsWith("/") || p.Path.Contains("\\") || p.Path.Split('/').Any(x => x == ".." || x == ".")) throw new ArgumentException(L.Text("sshfs.absoluteRemoteFolderRequired"));
        }
        internal static string Remote(NetworkProfile p)
        { return "\\\\sshfs.r\\" + p.UserName + "@" + p.Host + (p.Port == 22 ? "" : "!" + p.Port) + (p.Path.Trim('/') == "" ? "" : "\\" + p.Path.Trim('/').Replace('/', '\\')); }
        static string Canonical(string value) { return value.Replace("\\sshfs.kr\\", "\\sshfs.r\\").TrimEnd('\\'); }
        internal static bool SameRemote(string first, string second) { return String.Equals(Canonical(first), Canonical(second), StringComparison.OrdinalIgnoreCase); }
        internal static Dictionary<string, string> MappedDrives()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(Root)) return result;
            foreach (string file in Directory.GetFiles(Root, "?.xml"))
                try { var r = Read(Path.GetFileNameWithoutExtension(file) + ":"); using (var p = Owned(r)) if (p != null) result[r.Drive + "\\"] = r.Remote; } catch (IOException) { } catch (InvalidOperationException) { }
            return result;
        }
        static string OptionPath(string value) { return value.Replace("\\", "\\\\").Replace(",", "\\,"); }
        static string RecordPath(string drive) { return Path.Combine(Root, drive[0] + ".xml"); }
        static SshfsMountRecord Read(string drive)
        {
            string file = RecordPath(drive); if (!File.Exists(file)) return null;
            using (var input = System.Xml.XmlReader.Create(file, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null })) return (SshfsMountRecord)new XmlSerializer(typeof(SshfsMountRecord)).Deserialize(input);
        }
        static Process Owned(SshfsMountRecord r)
        {
            if (r == null) return null;
            try { var p = Process.GetProcessById(r.ProcessId); if (!p.HasExited && p.StartTime.ToUniversalTime().Ticks == r.Started && String.Equals(p.MainModule.FileName, r.Executable, StringComparison.OrdinalIgnoreCase) && String.Equals(r.Executable, Executable(), StringComparison.OrdinalIgnoreCase)) return p; p.Dispose(); }
            catch (ArgumentException) { } catch (InvalidOperationException) { } catch (Win32Exception) { }
            return null;
        }
        internal static bool Connected(NetworkProfile p)
        {
            p.Validate(); using (var process = Owned(Read(p.DriveLetter))) if (process != null) { var r = Read(p.DriveLetter); return Canonical(r.Remote) == Canonical(Remote(p)); }
            string value; return FilePanel.RememberedDrives().TryGetValue(p.DriveLetter + "\\", out value) && String.Equals(Canonical(value), Canonical(Remote(p)), StringComparison.OrdinalIgnoreCase);
        }
        internal static string CygwinPath(string path)
        { path = Path.GetFullPath(path); if (path.Length < 3 || path[1] != ':') throw new IOException(L.Text("sshfs.localKeyAndConfigRequired")); return "/cygdrive/" + Char.ToLowerInvariant(path[0]) + path.Substring(2).Replace('\\', '/'); }
        internal static ProcessStartInfo Launch(NetworkProfile p, string knownHosts)
        {
            p.Validate(); string exe = Executable(); if (exe == null) throw new IOException(DependencyStatus());
            string algorithm = p.HostKeyFingerprint.Split(' ')[0]; if (algorithm == "ssh-rsa") algorithm = "rsa-sha2-512,rsa-sha2-256,ssh-rsa";
            var args = new List<string> { "-f", "-p", p.Port.ToString(), "-o", "ssh_command=/usr/bin/ssh.exe -F /dev/null", "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + OptionPath(CygwinPath(knownHosts)), "-o", "HostKeyAlgorithms=" + OptionPath(algorithm), "-o", "ConnectTimeout=" + p.TimeoutSeconds, "-o", "ServerAliveInterval=" + p.SshKeepAliveSeconds, "-o", "ServerAliveCountMax=3", "--VolumePrefix=" + Remote(p).Substring(1), "-o", "volname=SSHFS,fstypename=SSHFS,rellinks" };
            if (p.SshCompression) args.Add("-C");
            args.Add("-o"); args.Add("idmap=user");
            if (String.IsNullOrEmpty(p.PrivateKeyPath)) { args.Add("-o"); args.Add("password_stdin,PreferredAuthentications=password,keyboard-interactive"); }
            else { args.Add("-o"); args.Add("IdentityFile=" + OptionPath(CygwinPath(p.PrivateKeyPath))); args.Add("-o"); args.Add("BatchMode=yes,IdentitiesOnly=yes"); }
            args.Add(p.UserName + "@" + p.Host + ":" + p.Path); args.Add(p.DriveLetter);
            if (TestRoot != null) args.Insert(0, "-d");
            var info = new ProcessStartInfo(exe, String.Join(" ", args.Select(SshTerminal.QuoteWindowsArgument))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe) };
            info.EnvironmentVariables["PATH"] = Path.GetDirectoryName(exe) + ";" + Environment.GetEnvironmentVariable("PATH");
            info.EnvironmentVariables["LANG"] = "en_US.UTF-8"; info.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8"; return info;
        }
        internal static string Mount(NetworkProfile p, string password, string hostKey)
        {
            p.Validate();
            lock (gate) { if (!pending.Add(p.DriveLetter)) throw new IOException(L.Text("sshfs.driveAlreadyConnecting")); }
            Process child = null;
            try
            {
                if (Connected(p)) return p.DriveLetter + "\\";
                if (!WindowsNetwork.FreeDriveLetters().Contains(p.DriveLetter)) throw new IOException(L.Text("drive.namePrefix") + p.DriveLetter + L.Text("sshfs.driveOccupiedSuffix"));
                if (String.IsNullOrWhiteSpace(hostKey) || !NativeSsh.Trusted(p.HostKeyFingerprint, Convert.FromBase64String(hostKey.Split(' ')[1]))) throw new IOException(L.Text("sshfs.serverFingerprintMismatch"));
                if (password != null && password.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new IOException(L.Text("sshfs.passwordNewlineRejected"));
                Directory.CreateDirectory(Root); string hosts = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".known_hosts");
                File.WriteAllText(hosts, "[" + p.Host + "]:" + p.Port + " " + hostKey + "\n" + p.Host + " " + hostKey + "\n");
                child = SshfsProcess.Start(Launch(p, hosts), String.IsNullOrEmpty(p.PrivateKeyPath) ? password ?? "" : null, hosts + ".log");
                Func<string> error = delegate { try { using (var file = new FileStream(hosts + ".log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { if (file.Length > 4096) file.Position = file.Length - 4096; using (var reader = new StreamReader(file)) return reader.ReadToEnd(); } } catch (IOException) { return ""; } };
                var timer = Stopwatch.StartNew();
                while (!DriveInfo.GetDrives().Any(x => x.Name.StartsWith(p.DriveLetter, StringComparison.OrdinalIgnoreCase)))
                { if (child.HasExited) throw new IOException(L.Text("sshfs.mountFailedExitCodePrefix") + child.ExitCode + ").\n" + error()); if (timer.Elapsed.TotalSeconds >= p.TimeoutSeconds) { child.Kill(); child.WaitForExit(3000); throw new TimeoutException(L.Text("sshfs.mountTimeoutPrefix") + error()); } Thread.Sleep(100); }
                var r = new SshfsMountRecord { ProfileId = p.Id, Drive = p.DriveLetter, Remote = Remote(p), Executable = child.MainModule.FileName, ProcessId = child.Id, Started = child.StartTime.ToUniversalTime().Ticks, Keep = p.SshfsKeepMounted };
                using (var file = File.Create(RecordPath(p.DriveLetter))) new XmlSerializer(typeof(SshfsMountRecord)).Serialize(file, r);
                lock (gate) ownedHere.Add(p.DriveLetter);
                child.Dispose(); child = null; return p.DriveLetter + "\\";
            }
            finally { if (child != null) { if (!child.HasExited) child.Kill(); child.Dispose(); } lock (gate) pending.Remove(p.DriveLetter); }
        }
        internal static void Disconnect(NetworkProfile p)
        {
            p.Validate(); var r = Read(p.DriveLetter);
            using (var process = Owned(r))
            {
                if (process != null)
                { if (Canonical(r.Remote) != Canonical(Remote(p))) throw new IOException(L.Text("sshfs.driveOwnedByAnotherConnection")); process.Kill(); process.WaitForExit(5000); File.Delete(RecordPath(p.DriveLetter)); return; }
            }
            if (Connected(p)) WindowsNetwork.DisconnectMappedDrive(p.DriveLetter);
        }
        internal static void CloseTemporaryMounts()
        {
            if (!Directory.Exists(Root)) return;
            string[] drives; lock (gate) drives = ownedHere.ToArray();
            foreach (string drive in drives)
                try { var r = Read(drive); if (r != null && !r.Keep) using (var p = Owned(r)) if (p != null) { p.Kill(); p.WaitForExit(5000); File.Delete(RecordPath(drive)); } } catch { }
        }
    }
    public static partial class WindowsNetwork
    {
        internal static void DisconnectMappedDrive(string drive)
        { int result = WNetCancelConnection2(drive, 1, false); if (result != 0 && result != 2250) throw new Win32Exception(result); }
    }
}

