using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FileCompass
{
    public static class SshTerminal
    {
        public static ProcessStartInfo CreateLaunch(NetworkProfile profile)
        {
            if (String.IsNullOrWhiteSpace(profile.HostKeyFingerprint)) throw new IOException(L.Text("ssh.explicitHostKeyRequired"));
            var key = ProbeKey(profile);
            VerifyHostKey(profile, key.Item2);
            string root = AppPaths.DataPathForTerminal();
            string hosts = Path.Combine(root, Guid.NewGuid().ToString("N") + ".known_hosts");
            File.WriteAllText(hosts, "[" + profile.Host + "]:" + profile.Port + " " + key.Item2 + "\n" + profile.Host + " " + key.Item2 + "\n");
            return PrepareLaunch(profile, hosts);
        }
        internal static void VerifyHostKey(NetworkProfile profile, string hostKey)
        {
            if (!FileCompass.NetworkTransport.NativeSsh.Trusted(profile.HostKeyFingerprint, Convert.FromBase64String(hostKey.Split(' ')[1])))
                throw new IOException(L.Text("sshfs.serverFingerprintMismatch"));
        }
        internal static Tuple<string, string> ProbeKey(NetworkProfile profile)
        {
            profile.Validate();
            if (!profile.CanOpenSsh) throw new ArgumentException(L.Text("ssh.supportedTerminalProtocols"));
            var transfer = profile.Clone(); transfer.Protocol = NetworkProtocol.SFTP;
            using (var connection = new RemoteConnection(transfer))
                return connection.Session.ScanSshfsKey(connection.Options(null));
        }
        internal static ProcessStartInfo PrepareLaunch(NetworkProfile profile, string knownHosts)
        {
            profile.Validate();
            if (!profile.CanOpenSsh)
                throw new ArgumentException(L.Text("ssh.supportedTerminalProtocols"));
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string ssh = FindExecutable(Path.Combine(system, "OpenSSH", "ssh.exe"), "ssh.exe");
            if (ssh == null) throw new IOException(L.Text("ssh.openSshClientMissing"));
            string shell = profile.UsePowerShell7 ?
                FindExecutable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"), "pwsh.exe") :
                Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (shell == null || !File.Exists(shell)) throw new IOException(L.Text("ssh.selectedPowerShellMissing"));
            string encoded = EncodeCommand(BuildCommand(profile, ssh, knownHosts));
            string shellArguments = "-NoLogo -NoProfile -NoExit -EncodedCommand " + encoded;
            string terminal = profile.UseWindowsTerminal ? FindExecutable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe"), "wt.exe") : null;
            // Terminal switches and title contain only fixed text. All user values are inside the encoded PowerShell command.
            return new ProcessStartInfo(terminal ?? shell, terminal == null ? shellArguments :
                "-w new new-tab --title SSH " + QuoteWindowsArgument(shell) + " " + shellArguments)
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                WindowStyle = ProcessWindowStyle.Normal
            };
        }
        public static void Open(NetworkProfile profile)
        {
            if(!String.IsNullOrEmpty(profile.CloudAccountId))throw new IOException(L.Text("cloud.noTerminal")); var launch = CreateLaunch(profile);
            try { using (var process = Process.Start(launch)) { } }
            catch (System.ComponentModel.Win32Exception)
            {
                if (!launch.FileName.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase)) throw;
                var fallback = profile.Clone(); fallback.UseWindowsTerminal = false;
                using (var process = Process.Start(CreateLaunch(fallback))) { }
            }
        }
        internal static string FindExecutable(string preferred, string name)
        {
            if (File.Exists(preferred)) return preferred;
            return null;
        }
        internal static string BuildCommand(NetworkProfile profile, string executable, string knownHosts)
        {
            if (String.IsNullOrWhiteSpace(knownHosts)) throw new IOException(L.Text("ssh.explicitHostKeyRequired"));
            var arguments = new List<string> { "-p", profile.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-F", "NUL", "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + "\"" + Path.GetFullPath(knownHosts).Replace('\\', '/') + "\"",
                "-o", "GlobalKnownHostsFile=NUL", "-o", "KnownHostsCommand=none", "-o", "UpdateHostKeys=no", "-o", "VerifyHostKeyDNS=no", "-o", "CheckHostIP=no" };
            string algorithm = (profile.HostKeyFingerprint ?? "").Split(' ')[0];
            if (algorithm.StartsWith("ssh-", StringComparison.Ordinal) || algorithm.StartsWith("ecdsa-", StringComparison.Ordinal))
            { arguments.Add("-o"); arguments.Add("HostKeyAlgorithms=" + (algorithm == "ssh-rsa" ? "rsa-sha2-512,rsa-sha2-256" : algorithm)); }
            if (!String.IsNullOrWhiteSpace(profile.UserName)) { arguments.Add("-l"); arguments.Add(profile.UserName); }
            if (!String.IsNullOrWhiteSpace(profile.PrivateKeyPath)) { arguments.Add("-i"); arguments.Add(profile.PrivateKeyPath); }
            arguments.Add("-o"); arguments.Add("ConnectTimeout=" + profile.TimeoutSeconds);
            if (profile.SshCompression) arguments.Add("-C");
            if (profile.SshKeepAliveSeconds > 0) { arguments.Add("-o"); arguments.Add("ServerAliveInterval=" + profile.SshKeepAliveSeconds); }
            if (!profile.SshAgent) { arguments.Add("-o"); arguments.Add("IdentityAgent=none"); }
            arguments.Add("--"); arguments.Add(profile.Host);
            return "$Host.UI.RawUI.WindowTitle = " + QuotePowerShell(L.Text("ssh.windowTitlePrefix") + profile.Name) + "\r\n" +
                "$sshInfo = New-Object System.Diagnostics.ProcessStartInfo\r\n" +
                "$sshInfo.FileName = " + QuotePowerShell(executable) + "\r\n" +
                "$sshInfo.Arguments = " + QuotePowerShell(String.Join(" ", arguments.Select(QuoteWindowsArgument))) + "\r\n" +
                "$sshInfo.UseShellExecute = $false\r\n$sshProcess = $null\r\n" +
                "try { $sshProcess = [System.Diagnostics.Process]::Start($sshInfo); $sshProcess.WaitForExit(); $global:LASTEXITCODE = $sshProcess.ExitCode\r\n" +
                "} finally { if ($null -ne $sshProcess) { $sshProcess.Dispose() }; [System.IO.File]::Delete(" + QuotePowerShell(knownHosts) + ") }\r\n" +
                L.Code("internal.sshExitStatusScript");
        }
        internal static string EncodeCommand(string command) { return Convert.ToBase64String(Encoding.Unicode.GetBytes(command)); }
        static string QuotePowerShell(string value) { return "'" + (value ?? "").Replace("'", "''") + "'"; }
        // Executable paths cannot contain quotes and do not end in a directory separator.
        internal static string QuoteWindowsArgument(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1); else result.Append('\\', slashes);
                result.Append(c); slashes = 0;
            }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }
    }
}

