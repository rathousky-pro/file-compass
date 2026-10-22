using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CommanderCZ
{
    public enum NetworkProtocol { SMB, FTP, SFTP, NFS, SSH, SCP, SSHFS }

    public class NetworkProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = L.Text("network.newConnectionTitle");
        public NetworkProtocol Protocol;
        public string CloudAccountId="",CloudProvider=""; public string Host = "";
        public int Port = 22;
        public string Path = "/";
        public string UserName = "";
        public string EncryptedPassword = "";
        public string PrivateKeyPath = "";
        public string HostKeyFingerprint = "";
        public bool FtpTls;
        public bool FtpPassive = true;
        public bool FtpImplicitTls;
        public string LocalPath = "";
        public int TimeoutSeconds = 20;
        public int FilenameEncoding = 2;
        public int FtpMlsd = 2;
        public int FtpHidden = 2;
        public string FtpCommands = "";
        public string ClientCertificate = "";
        public int ProxyMethod;
        public string ProxyHost = "";
        public int ProxyPort = 8080;
        public string ProxyUser = "";
        public string EncryptedProxyPassword = "";
        public bool SshCompression;
        public bool SshAgent = true;
        public int SshKeepAliveSeconds;
        public string DriveLetter = "Z:";
        public bool NfsAnonymous = true;
        public bool UseWindowsTerminal = true;
        public bool UsePowerShell7;
        public bool SshfsConnectOnStartup;
        public bool SshfsKeepMounted = true;
        [XmlIgnore] public bool IsSshTransfer { get { return Protocol == NetworkProtocol.SFTP || Protocol == NetworkProtocol.SCP; } }
        [XmlIgnore] public bool CanOpenSsh { get { return Protocol == NetworkProtocol.SSH || IsSshTransfer || Protocol == NetworkProtocol.SSHFS; } }
        [XmlIgnore] public string DisplayLabel {get{return String.IsNullOrEmpty(CloudAccountId)?Protocol+" · "+Host:CloudProvider+" · "+Name;}} public override string ToString() { return Name + "  (" + Protocol + ")"; }
        public NetworkProfile Clone() { return (NetworkProfile)MemberwiseClone(); }
        public void Validate()
        {
            if (String.IsNullOrWhiteSpace(Name)) throw new ArgumentException(L.Text("network.connectionNameRequired"));
            if (String.IsNullOrWhiteSpace(Host) || Host.IndexOfAny(new[] { '/', '\\', '"', '\r', '\n', ' ', '\t' }) >= 0)
                throw new ArgumentException(L.Text("network.hostWithoutSchemeRequired"));
            if (!Enum.IsDefined(typeof(NetworkProtocol), Protocol)) throw new ArgumentException(L.Text("network.invalidProtocol"));
            if (TimeoutSeconds < 5 || TimeoutSeconds > 600) throw new ArgumentException(L.Text("network.timeoutRangeRequired"));
            if (FilenameEncoding < 0 || FilenameEncoding > 2 || FtpMlsd < 0 || FtpMlsd > 2 || FtpHidden < 0 || FtpHidden > 2) throw new ArgumentException(L.Text("network.invalidAdvancedOption"));
            if (ProxyMethod < 0 || ProxyMethod > 3) throw new ArgumentException(L.Text("proxy.invalidType"));
            if (ProxyMethod != 0 && (Protocol == NetworkProtocol.FTP || IsSshTransfer) &&
                (String.IsNullOrWhiteSpace(ProxyHost) || ProxyHost.IndexOfAny(new[] { '/', '\\', '"', '\r', '\n', ' ', '\t' }) >= 0 || ProxyPort < 1 || ProxyPort > 65535))
                throw new ArgumentException(L.Text("proxy.hostAndPortRequired"));
            if (SshKeepAliveSeconds < 0 || SshKeepAliveSeconds > 3600) throw new ArgumentException(L.Text("ssh.keepAliveRangeRequired"));
            if (!String.IsNullOrEmpty(LocalPath) && !Directory.Exists(LocalPath)) throw new ArgumentException(L.Text("network.localFolderMissing"));
            if (Protocol == NetworkProtocol.FTP && FtpTls && !String.IsNullOrEmpty(ClientCertificate) && !File.Exists(ClientCertificate)) throw new ArgumentException(L.Text("network.clientCertificateMissing"));
            if ((Protocol == NetworkProtocol.FTP || CanOpenSsh) && (Port < 1 || Port > 65535))
                throw new ArgumentException(L.Text("network.portRangeRequired"));
            if ((IsSshTransfer || Protocol == NetworkProtocol.SSHFS) && String.IsNullOrWhiteSpace(UserName)) throw new ArgumentException(L.Text("network.usernameRequired"));
            if (Path == null || Path.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0) throw new ArgumentException(L.Text("network.invalidPath"));
            if (Protocol == NetworkProtocol.SMB && String.IsNullOrWhiteSpace(Path.Trim('/', '\\')))
                throw new ArgumentException(L.Text("smb.shareNameRequired"));
            if (Protocol == NetworkProtocol.NFS)
            {
                if (String.IsNullOrWhiteSpace(Path.Trim('/', '\\'))) throw new ArgumentException(L.Text("nfs.exportPathRequired"));
                if (DriveLetter == null || DriveLetter.Length != 2 || DriveLetter[0] < 'D' || DriveLetter[0] > 'Z' || DriveLetter[1] != ':')
                    throw new ArgumentException(L.Text("nfs.driveLetterRequired"));
            }
            if (!String.IsNullOrEmpty(PrivateKeyPath) && CanOpenSsh && !File.Exists(PrivateKeyPath))
                throw new ArgumentException(L.Text("ssh.privateKeyFileMissing"));
            if (Protocol == NetworkProtocol.SSHFS) SshfsMount.Validate(this);
            if (HostKeyFingerprint == "*" || (!String.IsNullOrEmpty(HostKeyFingerprint) && HostKeyFingerprint.IndexOf(';') >= 0))
                throw new ArgumentException(L.Text("ssh.explicitHostKeyRequired"));
        }
    }

    public class NetworkSettings
    {
        public int Version = 1;
        public List<NetworkProfile> Connections = new List<NetworkProfile>();
    }

    public sealed class NetworkSettingsStore
    {
        public readonly string FilePath;
        public NetworkSettings Settings { get; private set; }
        public NetworkSettingsStore(string path)
        {
            FilePath = path; Settings = new NetworkSettings();
            if (!File.Exists(path)) return;
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                Settings = (NetworkSettings)new XmlSerializer(typeof(NetworkSettings)).Deserialize(reader);
            if (Settings.Version != 1 || Settings.Connections == null) throw new IOException(L.Text("network.unsupportedConfigurationFormat"));
        }
        public static string DefaultPath
        {
            get { return System.IO.Path.Combine(AppPaths.ConfigDirectory, "connections.xml"); }
        }
        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(NetworkSettings)).Serialize(stream, Settings); stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public void Put(NetworkProfile profile)
        {
            profile.Validate();
            if (profile.Protocol == NetworkProtocol.SSH) { profile = profile.Clone(); profile.EncryptedPassword = ""; }
            var previous = Settings.Connections.ToList();
            Settings.Connections.RemoveAll(x => x.Id == profile.Id); Settings.Connections.Add(profile);
            try { Save(); } catch { Settings.Connections = previous; throw; }
        }
        public void Remove(NetworkProfile profile)
        {
            var previous = Settings.Connections.ToList(); Settings.Connections.RemoveAll(x => x.Id == profile.Id);
            try { Save(); } catch { Settings.Connections = previous; throw; }
        }
        // Aplikační entropie: jiná aplikace téhož uživatele nerozšifruje hesla holým voláním DPAPI. Záznamy z dřívějších verzí
        // (bez entropie) se stále načtou; s entropií se uloží až při dalším uložení hesla.
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CommanderCZ.connections.v2");
        public static string Protect(string password)
        {
            return String.IsNullOrEmpty(password) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));
        }
        public static string Unprotect(string password)
        {
            if (String.IsNullOrEmpty(password)) return "";
            try
            {
                byte[] blob = Convert.FromBase64String(password);
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser)); }
                catch (CryptographicException) { return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser)); }
            }
            catch (Exception ex) { throw new IOException(L.Text("network.savedPasswordCannotBeDecrypted"), ex); }
        }
    }
}

