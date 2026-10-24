using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public sealed class RemoteConnection : IDisposable
    {
        public readonly NetworkProfile Profile;
        public Session Session { get; private set; }
        SessionOptions login;
        public void Open(SessionOptions options) { if(!String.IsNullOrEmpty(Profile.CloudAccountId))CloudRuntime.ApplyEndpoint(Profile,options); Session.Open(options); login = options; }
        public RemoteConnection TransferConnection()
        {
            if (login == null) throw new InvalidOperationException(L.Text("network.transferCredentialsMissing"));
            var connection = new RemoteConnection(Profile);
            connection.login = connection.Options(login.Password);
            if (Profile.FilenameEncoding == 2 && login.Protocol == Protocol.Ftp) {
                connection.login.AddRawSettings("Utf", login.Raw("Utf", "1"));
                connection.login.AddRawSettings("UtfAuto", login.Raw("UtfAuto", "1"));
            }
            connection.login.SshHostKeyFingerprint = login.SshHostKeyFingerprint;
            return connection;
        }
        public void OpenTransfer() { if(!String.IsNullOrEmpty(Profile.CloudAccountId))CloudRuntime.ApplyEndpoint(Profile,login); Session.Open(login); }
        internal void ReopenTransfer(System.Threading.CancellationToken token)
        {
            Session.Dispose(); token.ThrowIfCancellationRequested(); if(!String.IsNullOrEmpty(Profile.CloudAccountId))CloudRuntime.ApplyEndpoint(Profile,login); Session = new Session();
            using (token.Register(delegate { Session.Abort(); })) { token.ThrowIfCancellationRequested(); Session.Open(login); token.ThrowIfCancellationRequested(); }
        }
        internal void ConfigureRelay() { login.AddRawSettings("TransientTransfer", "1"); }
        internal RemoteDirectoryInfo ReconnectDirectory(string path)
        {
            if (login == null) throw new IOException(L.Text("network.resumeCredentialsMissing"));
            // Release the failed FTP browser connection before logging in again,
            // so recovery does not temporarily consume another server slot.
            // Transfer workers own independent Session instances.
            if (login.Protocol == FileCompass.NetworkTransport.Protocol.Ftp) Session.Dispose();
            if(!String.IsNullOrEmpty(Profile.CloudAccountId))CloudRuntime.ApplyEndpoint(Profile,login); var replacement = new Session();
            try {
                replacement.Open(login); var listing = replacement.ListDirectory(path);
                var previous = Session; Session = replacement; replacement = null; previous.Dispose();
                return listing;
            } finally { if (replacement != null) replacement.Dispose(); }
        }
        public RemoteConnection(NetworkProfile profile)
        {
            Profile = profile.Clone();
            string plugin = "network/FileCompassNetwork.dll";
            if (!File.Exists(System.IO.Path.Combine(AppPaths.Resource("plugins"), plugin)))
                throw new IOException(L.Text("network.pluginMissingPrefix") + plugin + L.Text("network.restorePluginFolderSuffix"));
            Session = new Session();
        }
        public SessionOptions Options(string password)
        {
            if (Profile.Protocol != NetworkProtocol.FTP && !Profile.IsSshTransfer) throw new ArgumentException(L.Text("network.profilePanelUnsupported"));
            var options = new SessionOptions { Protocol = Profile.Protocol == NetworkProtocol.SCP ? FileCompass.NetworkTransport.Protocol.Scp : Profile.Protocol == NetworkProtocol.SFTP ? FileCompass.NetworkTransport.Protocol.Sftp : FileCompass.NetworkTransport.Protocol.Ftp,
                HostName = Profile.Host, PortNumber = Profile.Port,
                UserName = Profile.Protocol == NetworkProtocol.FTP && String.IsNullOrWhiteSpace(Profile.UserName) ? "anonymous" : Profile.UserName,
                Password = password, Timeout = TimeSpan.FromSeconds(Profile.TimeoutSeconds) };
            if (Profile.IsSshTransfer)
            {
                if (!String.IsNullOrWhiteSpace(Profile.HostKeyFingerprint)) options.SshHostKeyFingerprint = Profile.HostKeyFingerprint;
                options.SshPrivateKeyPath = Profile.PrivateKeyPath;
                options.AddRawSettings("Compression", Profile.SshCompression ? "on" : "off");
                options.AddRawSettings("Agent", Profile.SshAgent ? "on" : "off");
                options.AddRawSettings("KeepAlive", Profile.SshKeepAliveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                options.FtpSecure = Profile.FtpTls ? Profile.FtpImplicitTls ? FtpSecure.Implicit : FtpSecure.Explicit : FtpSecure.None;
                options.FtpMode = Profile.FtpPassive ? FtpMode.Passive : FtpMode.Active;
                options.AddRawSettings("FtpUseMlsd", TriState(Profile.FtpMlsd));
                options.AddRawSettings("FtpListAll", TriState(Profile.FtpHidden));
                if (!String.IsNullOrEmpty(Profile.FtpCommands)) options.AddRawSettings("PostLoginCommands", Profile.FtpCommands.Replace("\r", ""));
                if (Profile.FtpTls) options.TlsClientCertificatePath = Profile.ClientCertificate;
            }
            options.AddRawSettings("Utf", Profile.FilenameEncoding.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (Profile.ProxyMethod != 0)
            {
                options.AddRawSettings("ProxyMethod", Profile.ProxyMethod.ToString(System.Globalization.CultureInfo.InvariantCulture));
                options.AddRawSettings("ProxyHost", Profile.ProxyHost);
                options.AddRawSettings("ProxyPort", Profile.ProxyPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
                options.AddRawSettings("ProxyUsername", Profile.ProxyUser);
                options.AddRawSettings("ProxyPassword", NetworkSettingsStore.Unprotect(Profile.EncryptedProxyPassword));
            }
            return options;
        }
        static string TriState(int value) { return value == 0 ? "off" : value == 1 ? "on" : "auto"; }
        public string Display(string path) { if(!String.IsNullOrEmpty(Profile.CloudAccountId))return Profile.CloudProvider+" · "+Profile.Name+":"+path; return Profile.Protocol.ToString().ToLowerInvariant() + "://" + Profile.Host + ":" + Profile.Port + path; }
        public static string Normalize(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "/";
            if (path.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new IOException(L.Text("network.invalidRemotePath"));
            var parts = new System.Collections.Generic.List<string>();
            foreach (var part in path.Split('/'))
            {
                if (part == "" || part == ".") continue;
                if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else parts.Add(part);
            }
            return "/" + String.Join("/", parts);
        }
        public static string Combine(string path, string name) { return Normalize(path.TrimEnd('/') + "/" + name); }
        public static string Parent(string path) { return Normalize(path + "/.."); }
        public static string Name(string path) { return path.TrimEnd('/').Split('/').Last(); }
        public static void ValidateRemoteName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(new[] { '/', '\\', '\r', '\n', '\0' }) >= 0)
                throw new IOException(L.Text("network.itemNameWithoutPathRequired"));
        }
        internal static string DownloadTarget(string local, string name)
        {
            ValidateRemoteName(name);
            FileOperations.ValidateName(name);
            if (!local.EndsWith("\\") && !local.EndsWith("/") && !Directory.Exists(local)) return Path.GetFullPath(local);
            string root = Path.GetFullPath(local).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("network.invalidRemotePath"));
            return target;
        }
        public void Dispose() { Session.Dispose(); login = null; }
    }

    public static partial class WindowsNetwork
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NetResource { public int Scope, Type, DisplayType, Usage; public string LocalName, RemoteName, Comment, Provider; }
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetAddConnection2(ref NetResource resource, string password, string userName, int flags);
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetCancelConnection2(string name, int flags, bool force);
        public static void DisconnectSmb(NetworkProfile profile)
        {
            string share = profile.Path.Trim('/', '\\').Replace('/', '\\').Split('\\')[0];
            int result = WNetCancelConnection2("\\\\" + profile.Host + "\\" + share, 0, false);
            if (result != 0 && result != 2250) throw new Win32Exception(result);
        }

        public static string ConnectSmb(NetworkProfile profile, string password)
        {
            string relative = profile.Path.Trim('/', '\\').Replace('/', '\\');
            string share = relative.Split('\\')[0];
            var resource = new NetResource { Type = 1, RemoteName = "\\\\" + profile.Host + "\\" + share };
            int result = WNetAddConnection2(ref resource, String.IsNullOrEmpty(profile.UserName) ? null : password,
                String.IsNullOrEmpty(profile.UserName) ? null : profile.UserName, 0);
            if (result != 0)
            {
                if (result == 1219) throw new IOException(L.Text("network.windowsCredentialsConflict"));
                throw new Win32Exception(result);
            }
            return "\\\\" + profile.Host + "\\" + relative;
        }
        public static string ConnectNfs(NetworkProfile profile)
        {
            if (DriveInfo.GetDrives().Any(x => x.Name.StartsWith(profile.DriveLetter, StringComparison.OrdinalIgnoreCase)))
                throw new IOException(L.Text("drive.namePrefix") + profile.DriveLetter + L.Text("drive.occupiedLetterSuffix"));
            string mount = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mount.exe");
            if (!File.Exists(mount)) throw new IOException(L.Text("nfs.clientNotEnabledHelp"));
            string share = "\\\\" + profile.Host + "\\" + profile.Path.Trim('/', '\\').Replace('/', '\\');
            Run(mount, (profile.NfsAnonymous ? "-o anon " : "") + Quote(share) + " " + profile.DriveLetter);
            return profile.DriveLetter + "\\";
        }
        public static void UnmountNfs(string drive)
        {
            Run(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "umount.exe"), drive);
        }
        static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
        static void Run(string executable, string arguments)
        {
            var info = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(); throw new IOException(L.Text("network.commandTimeout")); }
                if (process.ExitCode != 0) throw new IOException(L.Text("nfs.statusPrefix") + output.Result + " " + error.Result);
            }
        }
    }
}

