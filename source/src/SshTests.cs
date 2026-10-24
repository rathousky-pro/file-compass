using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FileCompass
{
    public static class SshTests
    {
        public static int Run(string workspace)
        {
            string root = Path.Combine(Path.GetFullPath(workspace), "ssh-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); var passed = new List<string>();
            Action<bool, string> check = delegate(bool value, string text) { if (!value) throw new Exception(text); passed.Add(text); };
            try
            {
                string key = Path.Combine(root, "klíč se jménem's $(Write-Output INJECTED).pem"); File.WriteAllText(key, "test fixture only");
                string hosts = Path.Combine(root, "known hosts's.txt");
                var profile = new NetworkProfile { Protocol = NetworkProtocol.SSH, Name = "Server'; $(Write-Output INJECTED)", Host = "server.example",
                    UserName = "user'; $(Write-Output INJECTED)", Port = 2222, PrivateKeyPath = key, UseWindowsTerminal = false, UsePowerShell7 = false,
                    EncryptedPassword = NetworkSettingsStore.Protect("SECRET-DO-NOT-PASS") };
                var store = new NetworkSettingsStore(Path.Combine(root, "connections.xml")); store.Put(profile);
                var saved = new NetworkSettingsStore(store.FilePath).Settings.Connections.Single();
                check(saved.Protocol == NetworkProtocol.SSH && saved.Host == profile.Host && saved.Port == 2222 && saved.UserName == profile.UserName &&
                    saved.PrivateKeyPath == key && !saved.UseWindowsTerminal && !saved.UsePowerShell7, "SSH server, user, port, key and terminal choice persist");
                check(saved.EncryptedPassword == "", "SSH saved profile contains no password");
                var info = SshTerminal.PrepareLaunch(profile, hosts);
                check(info.FileName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) && info.UseShellExecute && info.Arguments.Contains("-NoExit"), "built-in PowerShell launch keeps interactive terminal open");
                check(!info.Arguments.Contains(profile.Host) && !info.Arguments.Contains(profile.UserName), "profile fields travel inside encoded command");
                string encoded = info.Arguments.Substring(info.Arguments.IndexOf("-EncodedCommand ") + 16);
                string decoded = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
                check(decoded.Contains("\"-p\" \"2222\"") && decoded.Contains("\"--\" \"server.example\"") && !decoded.Contains("SECRET-DO-NOT-PASS") && !decoded.Contains(profile.EncryptedPassword), "SSH command sets arguments and carries no password");
                var terminalProfile = profile.Clone(); terminalProfile.UseWindowsTerminal = true;
                var terminal = SshTerminal.PrepareLaunch(terminalProfile, hosts);
                check(terminal.FileName.EndsWith("wt.exe", StringComparison.OrdinalIgnoreCase) ? terminal.Arguments.StartsWith("-w new new-tab --title SSH ") :
                    terminal.FileName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase), "Windows Terminal selected or standalone PowerShell fallback");
                var sftp = profile.Clone(); sftp.Protocol = NetworkProtocol.SFTP; sftp.HostKeyFingerprint = "";
                check(SshTerminal.PrepareLaunch(sftp, hosts) != null, "existing SFTP profile can open SSH terminal after host verification");
                var scp = profile.Clone(); scp.Protocol = NetworkProtocol.SCP; scp.HostKeyFingerprint = "";
                check(SshTerminal.PrepareLaunch(scp, hosts) != null, "SCP profile can open SSH terminal after host verification");
                bool rejected = false; var invalid = profile.Clone(); invalid.Port = 65536;
                try { SshTerminal.PrepareLaunch(invalid, hosts); } catch (ArgumentException) { rejected = true; }
                check(rejected, "invalid SSH port rejected");
                invalid = profile.Clone(); invalid.Protocol = NetworkProtocol.FTP; rejected = false;
                try { SshTerminal.PrepareLaunch(invalid, hosts); } catch (ArgumentException) { rejected = true; }
                check(rejected, "FTP profile cannot launch SSH");

                // Exercise PowerShell's real parser and native argv handling, without contacting a server.
                string recorderSource = Path.Combine(root, "ArgumentRecorder.cs"), recorder = Path.Combine(root, "ArgumentRecorder.exe");
                File.WriteAllText(recorderSource,
                    "using System; using System.IO; public static class Recorder { public static int Main(string[] args) { File.WriteAllLines(Environment.GetEnvironmentVariable(\"FILECOMPASS_SSH_TEST_OUTPUT\"), args); return 0; } }");
                string compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
                Execute(new ProcessStartInfo(compiler, "/nologo /target:exe /out:\"" + recorder + "\" \"" + recorderSource + "\""), null);
                string output = Path.Combine(root, "arguments.txt");
                string command = SshTerminal.BuildCommand(profile, recorder, hosts);
                var shell = new ProcessStartInfo(info.FileName, "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + SshTerminal.EncodeCommand(command));
                shell.EnvironmentVariables["FILECOMPASS_SSH_TEST_OUTPUT"] = output;
                string log = Execute(shell, null);
                if(!File.Exists(output)) throw new IOException("SSH argument recorder did not produce output: " + log);
                var args = File.ReadAllLines(output);
                var isolation = new[] { "-p", "2222", "-F", "NUL", "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=\"" + hosts.Replace('\\', '/') + "\"", "-o", "GlobalKnownHostsFile=NUL", "-o", "KnownHostsCommand=none", "-o", "UpdateHostKeys=no", "-o", "VerifyHostKeyDNS=no", "-o", "CheckHostIP=no" };
                check(args.SequenceEqual(isolation.Concat(new[] { "-l", profile.UserName, "-i", key, "-o", "ConnectTimeout=20", "--", "server.example" })), "real PowerShell preserves literal SSH arguments and enforces isolated host verification");
                check(!log.Contains("INJECTED"), "profile values do not execute extra PowerShell commands");
                var defaults = profile.Clone(); defaults.UserName = ""; defaults.PrivateKeyPath = "";
                shell = new ProcessStartInfo(info.FileName, "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + SshTerminal.EncodeCommand(SshTerminal.BuildCommand(defaults, recorder, hosts)));
                shell.EnvironmentVariables["FILECOMPASS_SSH_TEST_OUTPUT"] = output;
                Execute(shell, null);
                check(File.ReadAllLines(output).SequenceEqual(isolation.Concat(new[] { "-o", "ConnectTimeout=20", "--", "server.example" })), "no username or key uses isolated OpenSSH defaults and agent");
                defaults.SshCompression = true; defaults.SshAgent = false; defaults.SshKeepAliveSeconds = 90;
                shell = new ProcessStartInfo(info.FileName, "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + SshTerminal.EncodeCommand(SshTerminal.BuildCommand(defaults, recorder, hosts)));
                shell.EnvironmentVariables["FILECOMPASS_SSH_TEST_OUTPUT"] = output; Execute(shell, null);
                check(File.ReadAllLines(output).SequenceEqual(isolation.Concat(new[] { "-o", "ConnectTimeout=20", "-C", "-o", "ServerAliveInterval=90", "-o", "IdentityAgent=none", "--", "server.example" })), "SSH compression keepalive and agent options passed literally to terminal");
                string ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
                string resolved = Execute(new ProcessStartInfo(ssh, "-F NUL -G -p 2222 -l tester -- server.example"), null);
                check(resolved.Contains("user tester") && resolved.Contains("port 2222") && resolved.Contains("hostname server.example"), "real OpenSSH accepts generated port, user and option terminator");
                File.WriteAllLines(Path.Combine(workspace, "ssh-test-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "ssh-test-result.txt"), "FAIL: " + ex); return 1; }
            finally
            {
                if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(workspace).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Test cleanup outside workspace.");
                Directory.Delete(root, true);
            }
        }
        static string Execute(ProcessStartInfo info, string unused)
        {
            info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = info.RedirectStandardError = true;
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(20000)) { process.Kill(); throw new IOException("Test process timed out."); }
                string log = output.Result + error.Result;
                if (process.ExitCode != 0) throw new IOException("Test process failed: " + log);
                return log;
            }
        }
    }
}

