using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal static class SshfsTests
    {
        internal static int KeepOnce(string directory, int port)
        {
            try
            {
                var p = new NetworkSettingsStore(Path.Combine(directory, "keep-profile.xml")).Settings.Connections.Single();
                using (var probe = new Session())
                {
                    var key = probe.ScanSshfsKey(new SessionOptions { Protocol = Protocol.Sftp, HostName = p.Host, PortNumber = port, Timeout = TimeSpan.FromSeconds(10), SshHostKeyFingerprint = p.HostKeyFingerprint });
                    SshfsMount.Mount(p, NetworkSettingsStore.Unprotect(p.EncryptedPassword), key.Item2);
                }
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "keep-error.txt"), ex.ToString()); return 1; }
        }
        internal static int Run(string directory, int port)
        {
            Directory.CreateDirectory(directory); var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string text) { if (!ok) throw new Exception(text); checks.Add(text); };
            var profile = new NetworkProfile { Protocol = NetworkProtocol.SSHFS, Name = "SSHFS test", Host = "127.0.0.1", Port = port, UserName = "tester", Path = "/", DriveLetter = WindowsNetwork.FreeDriveLetters().First(), SshfsKeepMounted = false };
            string testKey = Path.Combine(Path.GetDirectoryName(directory), "id_test"); if (File.Exists(testKey)) profile.PrivateKeyPath = testKey;
            string result = Path.Combine(directory, "sshfs-test-result.txt");
            try
            {
                profile.Validate(); check(SshfsMount.Executable() != null, "installed SSHFS-Win detected without installing or modifying drivers");
                Tuple<string, string> key; using (var probe = new Session()) key = probe.ScanSshfsKey(new SessionOptions { Protocol = Protocol.Sftp, HostName = profile.Host, PortNumber = port, Timeout = TimeSpan.FromSeconds(10) });
                profile.HostKeyFingerprint = key.Item1;
                var launch = SshfsMount.Launch(profile, Path.Combine(directory, "test.known_hosts"));
                check(launch.Arguments.Contains("StrictHostKeyChecking=yes") && !launch.Arguments.Contains("test-password") && launch.RedirectStandardInput && !launch.UseShellExecute, "SSHFS pins host key and passes credentials through stdin without a shell");
                var wrong = profile.Clone(); wrong.HostKeyFingerprint = "ssh-rsa invalid"; bool rejected = false;
                try { SshfsMount.Mount(wrong, "test-password", key.Item2); } catch (IOException) { rejected = true; }
                check(rejected && WindowsNetwork.FreeDriveLetters().Contains(profile.DriveLetter), "mismatched SSH host key rejected before creating any drive");
                string root = SshfsMount.Mount(profile, "test-password", key.Item2);
                check(File.ReadAllText(Path.Combine(root, "český soubor.txt")) == "Příliš žluťoučký kůň", "real SSHFS disk reads Czech filename and content through WinFsp");
                check(SshfsMount.Connected(profile) && FilePanel.RememberedDrives().ContainsKey(root), "managed SSHFS drive is discoverable with its network address");
                string created = Path.Combine(root, "commander-sshfs-test.txt"); File.WriteAllText(created, "verified SSHFS write");
                check(File.ReadAllText(created) == "verified SSHFS write", "real SSHFS disk writes exact content");
                File.Move(created, Path.Combine(root, "renamed-test.txt")); File.Delete(Path.Combine(root, "renamed-test.txt")); check(!File.Exists(created), "real SSHFS disk supports rename and deletion");
                check(CopyEngine.NetworkPath(root), "copy engine recognizes actual SSHFS drive as a network path");
                string localMove = Path.Combine(directory, "move-test.txt"); File.WriteAllText(localMove, "network move without rereading");
                var phases = new List<string>();
                using (var control = new CopyControl())
                {
                    var moved = new CopyEngine(new CopyOptions { Target = root, Move = true }, control, delegate { return new CopyDecision(CopyChoice.Overwrite); }, delegate(CopyUpdate p) { phases.Add(p.Phase); }).Run(new[] { localMove });
                    check(moved.Errors.Count == 0 && moved.Copied == 1 && !File.Exists(localMove) && File.ReadAllText(Path.Combine(root, "move-test.txt")) == "network move without rereading" && !phases.Contains("Ověřování SHA-256"), "real move to SSHFS finishes without automatic full SHA reread");
                }
                phases.Clear();
                using (var control = new CopyControl())
                {
                    var moved = new CopyEngine(new CopyOptions { Target = directory, Move = true }, control, delegate { return new CopyDecision(CopyChoice.Overwrite); }, delegate(CopyUpdate p) { phases.Add(p.Phase); }).Run(new[] { Path.Combine(root, "move-test.txt") });
                    check(moved.Errors.Count == 0 && moved.Copied == 1 && !File.Exists(Path.Combine(root, "move-test.txt")) && File.ReadAllText(localMove) == "network move without rereading" && !phases.Contains("Ověřování SHA-256"), "real move from SSHFS validates size and source metadata without rereading content: copied=" + moved.Copied + ", source=" + File.Exists(Path.Combine(root, "move-test.txt")) + ", target=" + File.Exists(localMove) + ", phases=" + String.Join(",", phases) + ", errors=" + String.Join("; ", moved.Errors));
                }
                check(SshfsMount.Mount(profile, "test-password", key.Item2) == root, "reopening same profile reuses mapped disk without another mount");
                SshfsMount.Disconnect(profile); check(!SshfsMount.Connected(profile), "explicit disconnect removes only the test-owned disk");
                SshfsMount.Mount(profile, "test-password", key.Item2); SshfsMount.CloseTemporaryMounts(); check(!SshfsMount.Connected(profile), "closing temporary mounts leaves no test drive behind");
                var keep = profile.Clone(); keep.SshfsKeepMounted = true; keep.EncryptedPassword = NetworkSettingsStore.Protect("test-password");
                new NetworkSettingsStore(Path.Combine(directory, "keep-profile.xml")).Put(keep);
                var start = new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, "--sshfs-keep-test " + SshTerminal.QuoteWindowsArgument(directory) + " " + port) { UseShellExecute = false, CreateNoWindow = true };
                using (var child = System.Diagnostics.Process.Start(start)) { if (!child.WaitForExit(30000)) { child.Kill(); throw new Exception("Persistent mount test timeout."); } check(child.ExitCode == 0, "separate Commander process mounts a persistent SSHFS drive"); }
                check(SshfsMount.Connected(profile) && File.ReadAllText(Path.Combine(profile.DriveLetter + "\\", "český soubor.txt")) == "Příliš žluťoučký kůň", "SSHFS remains usable after the creating Commander process exits");
                SshfsMount.Disconnect(profile); check(!SshfsMount.Connected(profile), "a new Commander process can safely disconnect the persisted mount using recorded process identity");
                File.WriteAllLines(result, checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(result, "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
            finally { try { SshfsMount.Disconnect(profile); } catch { } }
        }
    }
}

