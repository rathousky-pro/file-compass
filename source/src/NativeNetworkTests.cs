using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public static class NativeNetworkTests
    {
        public static int SshTransfer(string workspace, int scpPort, int sftpPort)
        {
            var passed = new List<string>();
            Action<bool, string> check = delegate(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); };
            string report = Path.Combine(workspace, "ssh-transfer-test-result.txt");
            try
            {
                foreach (var protocol in new[] { NetworkProtocol.SCP, NetworkProtocol.SFTP })
                {
                    var profile = new NetworkProfile { Protocol = protocol, Host = "127.0.0.1",
                        Port = protocol == NetworkProtocol.SCP ? scpPort : sftpPort, UserName = "tester",
                        Path = "/", SshAgent = false };
                    using (var probe = new RemoteConnection(profile))
                        profile.HostKeyFingerprint = probe.Session.ScanFingerprint(probe.Options(""), "SHA-256");
                    foreach (var key in new[] { "id_encrypted", "id_v2.ppk", "id_v3.ppk" })
                    {
                        profile.PrivateKeyPath = Path.Combine(workspace, key);
                        using (var wrong = new RemoteConnection(profile))
                        {
                            // A valid account password must never conceal a failed explicit key attempt.
                            var options = wrong.Options("test-password");
                            options.SshPrivateKeyPassphrase = "wrong phrase";
                            bool rejected = false;
                            try { wrong.Open(options); }
                            catch (SessionRemoteException ex) { rejected = ex.InnerException is SshAuthenticationException; }
                            check(rejected, protocol + " reports rejected key separately from account password (" + key + ")");
                        }
                        using (var connection = new RemoteConnection(profile))
                        {
                            var options = connection.Options("");
                            options.SshPrivateKeyPassphrase = "fixture key phrase";
                            connection.Open(options);
                            var listing = connection.Session.ListDirectory("/").Files;
                            check(listing.Any(x => x.Name == "český soubor.txt"), protocol + " lists entries with " + key);
                            var link = listing.Single(x => x.Name == "folder link");
                            check(link.FileType == 'L' && !link.IsDirectory && link.LinksToDirectory &&
                                !listing.Single(x => x.Name == "broken link").LinksToDirectory &&
                                !listing.Single(x => x.Name == "file link").LinksToDirectory,
                                protocol + " detects directory links while preserving link type (" + key + ")");
                            check(connection.Session.ListDirectory(link.FullName).Files.Count > 0 &&
                                connection.Session.GetFileInfo(link.FullName).FileType == 'L',
                                protocol + " navigates through a link and stats the link itself (" + key + ")");
                            using (var worker = connection.TransferConnection())
                            {
                                worker.OpenTransfer();
                                string target = Path.Combine(workspace, protocol + "-" + key + ".txt");
                                worker.Session.GetFiles("/český soubor.txt", target).Check();
                                check(File.ReadAllText(target) == "Příliš žluťoučký kůň", protocol + " transfer worker reuses key credentials (" + key + ")");
                            }
                        }
                    }
                }
                File.WriteAllLines(report, passed.Concat(new[] { "PASS: " + passed.Count + " checks" }));
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllLines(report, passed.Concat(new[] { "FAIL: " + ex }));
                return 1;
            }
        }

        public static int Scp(string workspace, int port)
        {
            Directory.CreateDirectory(workspace); var passed = new List<string>();
            Action<bool, string> check = delegate(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); };
            try
            {
                var profile = new NetworkProfile { Protocol = NetworkProtocol.SCP, Host = "127.0.0.1", Port = port, UserName = "tester", Path = "/" };
                using (var browsing = new RemoteConnection(profile))
                {
                    var options = browsing.Options("test-password"); options.SshHostKeyFingerprint = browsing.Session.ScanFingerprint(options, "SHA-256");
                    profile.HostKeyFingerprint = options.SshHostKeyFingerprint; browsing.Profile.HostKeyFingerprint = profile.HostKeyFingerprint;
                    using (var wrong = new RemoteConnection(profile))
                    {
                        var bad = wrong.Options("test-password"); bad.SshHostKeyFingerprint = "ssh-rsa 2048 AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
                        bool rejected = false; try { wrong.Open(bad); } catch (SessionRemoteException) { rejected = true; }
                        check(rejected, "SCP refuses mismatched host key");
                    }
                    // Previous WinSCP profiles used padded SHA256 fingerprints.
                    options.SshHostKeyFingerprint = options.SshHostKeyFingerprint.TrimEnd('=') + "=";
                    browsing.Open(options);
                    check(browsing.Session.ListDirectory("/").Files.Any(x => x.Name == "český soubor.txt"), "real SCP browsing via SSH shell and Czech filenames");
                    check(!browsing.Session.FileExists("/SFTP-WAS-REQUESTED"), "SCP does not request SFTP subsystem");
                    string downloaded = Path.Combine(workspace, "český soubor.txt");
                    browsing.Session.GetFiles("/český soubor.txt", downloaded).Check();
                    check(File.ReadAllText(downloaded) == "Příliš žluťoučký kůň", "genuine SCP file download exact content");
                    string source = Path.Combine(workspace, "quoted's [1] $.txt"); File.WriteAllText(source, "SCP upload české znaky");
                    using (var worker = browsing.TransferConnection())
                    {
                        worker.OpenTransfer();
                        check(worker.Session.ListDirectory("/sub").Files.Single().Name == "nested.txt", "SCP independent worker preserves host key and browsing session");
                        var entry = new Entry { FullPath = source }; var result = new NetworkCopyResult();
                        int prompts = 0; var denied = new NetworkCopyResult();
                        NetworkTransfers.Copy(entry, null, worker, "/no-write", NetworkCopyRule.Overwrite, denied, null, null, null,
                            delegate(CopyFailure failure) { prompts++; return CopyErrorChoice.Skip; });
                        check(prompts == 1 && denied.Skipped == 1 && worker.Session.FileExists("/sub"), "native SCP permission error offers skip and keeps browsing usable");
                        NetworkTransfers.Copy(entry, null, worker, "/", NetworkCopyRule.Overwrite, result);
                        check(result.Copied == 1 && worker.Session.GetFileInfo("/" + Path.GetFileName(source)).Length == new FileInfo(source).Length, "SCP upload handles literal quotes spaces brackets and dollar sign");
                        string roundtrip = Path.Combine(workspace, "roundtrip.txt");
                        worker.Session.GetFiles("/" + Path.GetFileName(source), roundtrip).Check();
                        check(File.ReadAllText(roundtrip) == File.ReadAllText(source), "SCP upload round trip exact bytes");
                        File.WriteAllText(source, "new content");
                        NetworkTransfers.Copy(entry, null, worker, "/", NetworkCopyRule.Skip, result);
                        worker.Session.GetFiles("/" + Path.GetFileName(source), roundtrip).Check();
                        check(File.ReadAllText(roundtrip) == "SCP upload české znaky" && result.Skipped == 1, "SCP skip keeps existing destination");
                        NetworkTransfers.Copy(entry, null, worker, "/", NetworkCopyRule.Overwrite, result);
                        worker.Session.GetFiles("/" + Path.GetFileName(source), roundtrip).Check();
                        check(File.ReadAllText(roundtrip) == "new content", "SCP overwrite replaces existing destination");
                        string folder = Path.Combine(workspace, "directory upload"); Directory.CreateDirectory(Path.Combine(folder, "nested"));
                        File.WriteAllText(Path.Combine(folder, "nested", "one.txt"), "nested data");
                        NetworkTransfers.Copy(new Entry { FullPath = folder, IsDirectory = true }, null, worker, "/", NetworkCopyRule.Overwrite);
                        check(worker.Session.FileExists("/directory upload/nested/one.txt"), "SCP recursive directory upload");
                        var target = Path.Combine(workspace, "downloads"); Directory.CreateDirectory(target);
                        NetworkTransfers.Copy(new Entry { FullPath = "/directory upload", IsDirectory = true }, worker, null, target, NetworkCopyRule.Overwrite);
                        check(File.ReadAllText(Path.Combine(target, "directory upload", "nested", "one.txt")) == "nested data", "SCP recursive directory download");
                        worker.Session.MoveFile("/directory upload", "/renamed");
                        DeleteOperations.Remote(worker.Session, "/renamed");
                        check(!worker.Session.FileExists("/renamed"), "SCP rename and recursive deletion");
                        check(!worker.Session.ListDirectory("/").Files.Any(x => x.Name.StartsWith(".FileCompass-")), "successful SCP operations clean remote staging files");
                    }
                    string original = Path.Combine(workspace, "original.bin"); File.WriteAllText(original, "preserve me");
                    bool cancelled = false;
                    using (var worker = browsing.TransferConnection())
                    {
                        worker.OpenTransfer();
                        worker.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { if (e.FileProgress > .05) e.Cancel = true; };
                        try { worker.Session.GetFiles("/slow.bin", original); } catch (OperationCanceledException) { cancelled = true; }
                    }
                    check(cancelled && File.ReadAllText(original) == "preserve me", "SCP cancellation keeps original local destination");
                    check(!Directory.GetFiles(workspace, ".FileCompass-download-*").Any() && browsing.Session.FileExists("/slow.bin"), "SCP cancellation cleans staging and preserves browsing session");
                    string cancelUpload = Path.Combine(workspace, "slow.bin"); File.WriteAllBytes(cancelUpload, new byte[1048576]);
                    cancelled = false;
                    using (var worker = browsing.TransferConnection())
                    {
                        worker.OpenTransfer();
                        worker.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { if (e.FileProgress > .05) e.Cancel = true; };
                        try { worker.Session.PutFiles(cancelUpload, "/slow.bin"); } catch (OperationCanceledException) { cancelled = true; }
                    }
                    check(cancelled && browsing.Session.GetFileInfo("/slow.bin").Length == 8388608, "cancelled SCP overwrite preserves original remote file");
                    check(!browsing.Session.ListDirectory("/").Files.Any(x => x.Name.StartsWith(".FileCompass-")), "cancelled SCP upload cleans remote staging");
                    check(!browsing.Session.FileExists("/SFTP-WAS-REQUESTED"), "all SCP file operations completed without any SFTP fallback");
                }
                File.WriteAllLines(Path.Combine(workspace, "scp-test-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "scp-test-result.txt"), "FAIL: " + ex); return 1; }
        }
    }
}


