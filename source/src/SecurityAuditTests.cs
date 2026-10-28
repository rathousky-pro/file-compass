using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    internal static class SecurityAuditTests
    {
        internal static int Run(string root)
        {
            root = Path.GetFullPath(root); Directory.CreateDirectory(root);
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool valid, string label) { if (!valid) throw new Exception(label); checks.Add(label); };
            Action<Action, string> rejects = delegate(Action action, string label) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } check(rejected, label); };
            try
            {
                string downloads = Path.Combine(root, "downloads"); Directory.CreateDirectory(downloads);
                foreach (string name in new[] { "../outside.txt", "..\\outside.txt", "/outside.txt", "C:\\outside.txt", "safe/../../outside.txt", ".", "..", "file.txt:evil", "CON", "file. " })
                    rejects(delegate { RemoteConnection.DownloadTarget(downloads, name); }, "reject local download name " + name);
                check(RemoteConnection.DownloadTarget(downloads, "normal.txt") == Path.Combine(downloads, "normal.txt"), "valid download stays inside destination");
                foreach (string mode in new[] { "MLSD", "LIST", "DOS" })
                foreach (string name in new[] { "../outside", "..\\outside", "/absolute", "safe/../../outside" })
                {
                    string listing = mode == "MLSD" ? "type=dir;size=0; " + name : mode == "DOS" ? "10-23-26  05:00PM       <DIR> " + name : "drwxr-xr-x 1 user group 0 Oct 23 2026 " + name;
                    rejects(delegate { Session.ParseFtpListing("/folder", listing, mode == "MLSD"); }, "FTP " + mode + " parser rejects " + name);
                }
                check(Session.ParseFtpListing("/folder", "type=file;size=12; normal.txt", true).Files.Single().FullName == "/folder/normal.txt", "MLSD valid file keeps exact parent");
                check(Session.ParseFtpListing("/folder", "drwxr-xr-x 1 user group 0 Oct 23 2026 normal", false).Files.Single().FullName == "/folder/normal", "LIST valid directory keeps exact parent");
                bool loopback = CanUseLoopback();
                if (!loopback) checks.Add("SKIP: loopback sockets denied by execution environment; real FTP integration unavailable");
                if (loopback) foreach (string mode in new[] { "MLSD", "LIST", "DOS" })
                {
                    foreach (string name in new[] { "../outside", "..\\outside", "/absolute", "safe/../../outside" })
                    using (var server = new ListingServer(mode, name))
                    using (var session = new Session())
                    {
                        var options = new SessionOptions { Protocol = Protocol.Ftp, HostName = "127.0.0.1", PortNumber = server.Port, UserName = "test", Password = "fixture", Timeout = TimeSpan.FromSeconds(5) };
                        options.AddRawSettings("FtpUseMlsd", mode == "MLSD" ? "on" : "off");
                        session.Open(options);
                        rejects(delegate { session.ListDirectory("/folder"); }, "real FTP " + mode + " rejects " + name);
                        rejects(delegate { session.GetFiles("/folder/outside", downloads + "\\"); }, "FTP download rejects poisoned metadata " + mode + " " + name);
                        rejects(delegate { session.RemoveFiles("/folder"); }, "FTP recursive delete rejects poisoned listing " + mode + " " + name);
                        check(server.Deletes == 0, "no delete command sent outside requested subtree");
                    }
                }
                check(!File.Exists(Path.Combine(root, "outside.txt")), "malicious server never creates an escaped download");

                string input = Path.Combine(root, "payload.txt"), archive = Path.Combine(root, "internet.zip"); File.WriteAllText(input, "attachment fixture");
                using (var zip = new ZipArchive(File.Create(archive), ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("nested/payload.txt").Open())) writer.Write(File.ReadAllText(input));
                AttachmentSecurity.MarkInternet(archive);
                string extractedRoot = Path.Combine(root, "extracted");
                if (loopback)
                {
                    var document = ArchiveEngine.Open(archive);
                    ArchiveEngine.Extract(document, new[] { "nested/payload.txt" }, extractedRoot, CancellationToken.None);
                    check(true, "actual archive worker extraction completes");
                }
                else
                {
                    checks.Add("SKIP: child-process named-pipe isolation prevents archive worker integration");
                    string prepared = ArchiveEngine.DiskPath(extractedRoot, "nested/payload.txt"); Directory.CreateDirectory(Path.GetDirectoryName(prepared)); File.Copy(input, prepared);
                    ArchiveEngine.PropagateExtractedZone(new[] { new ArchiveItem { Path = "nested/payload.txt" } }, extractedRoot, AttachmentSecurity.ReadZone(archive));
                }
                string extracted = Path.Combine(extractedRoot, "nested", "payload.txt");
                check(AttachmentSecurity.ReadZone(extracted).Contains("ZoneId=3"), "extracted-file zone propagation preserves Internet zone");
                DateTime beforeZone = File.GetLastWriteTimeUtc(extracted); File.SetAttributes(extracted, FileAttributes.ReadOnly);
                AttachmentSecurity.MarkInternet(extracted);
                check(File.GetLastWriteTimeUtc(extracted) == beforeZone && (File.GetAttributes(extracted) & FileAttributes.ReadOnly) != 0, "zone propagation handles read-only files and preserves timestamps and attributes");
                File.SetAttributes(extracted, FileAttributes.Normal);
                string copiedRoot = Path.Combine(root, "copied"); Directory.CreateDirectory(copiedRoot);
                using (var control = new CopyControl())
                {
                    var result = new CopyEngine(new CopyOptions { Target = copiedRoot }, control, delegate { return new CopyDecision(CopyChoice.Cancel); }, delegate { }).Run(new[] { extracted });
                    check(result.Errors.Count == 0 && AttachmentSecurity.ReadZone(Path.Combine(copiedRoot, "payload.txt")).Contains("ZoneId=3"), "copy from extraction staging preserves Internet zone");
                }
                foreach (string extension in new[] { ".EXE", ".scr", ".lnk", ".bat", ".ps1", ".vbs", ".chm", ".docm", ".url", ".msi" })
                    check(AttachmentSecurity.RequiresLaunchConfirmation("attachment" + extension), "archive launch confirmation covers " + extension);
                check(!AttachmentSecurity.RequiresLaunchConfirmation("photo.png"), "ordinary image does not require executable confirmation");
                rejects(delegate { SshTerminal.CreateLaunch(new NetworkProfile { Protocol = NetworkProtocol.SSH, Host = "127.0.0.1", Name = "test",UserName="tester" }); }, "terminal refuses missing pin before contacting server");
                byte[] publicKey = Convert.FromBase64String("AAAAC3NzaC1lZDI1NTE5AAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                var pinned = new NetworkProfile { Protocol = NetworkProtocol.SSH, Name = "pinned", Host = "server.example" };
                using (var sha = SHA256.Create()) pinned.HostKeyFingerprint = "ssh-ed25519 " + Convert.ToBase64String(sha.ComputeHash(publicKey)).TrimEnd('=');
                string hostKey = "ssh-ed25519 " + Convert.ToBase64String(publicKey);
                SshTerminal.VerifyHostKey(pinned, hostKey); check(true, "terminal accepts only the pinned public key");
                publicKey[publicKey.Length - 1] ^= 1;
                rejects(delegate { SshTerminal.VerifyHostKey(pinned, "ssh-ed25519 " + Convert.ToBase64String(publicKey)); }, "terminal rejects changed server key before launch");
                var pins = new NetworkSettingsStore(Path.Combine(root, "pinned-profiles.xml")); pins.Put(pinned);
                check(new NetworkSettingsStore(pins.FilePath).Settings.Connections.Single().HostKeyFingerprint == pinned.HostKeyFingerprint, "SSH host pin survives profile save and reload");
                string fakePath = Path.Combine(root, "untrusted-path"); Directory.CreateDirectory(fakePath); File.WriteAllText(Path.Combine(fakePath, "ssh.exe"), "not executable");
                string oldPath = Environment.GetEnvironmentVariable("PATH");
                try { Environment.SetEnvironmentVariable("PATH", fakePath); check(SshTerminal.FindExecutable(Path.Combine(root, "absent", "ssh.exe"), "ssh.exe") == null, "terminal never discovers ssh.exe through PATH"); }
                finally { Environment.SetEnvironmentVariable("PATH", oldPath); }

                string pipeName = "FileCompass-sshfs-" + Guid.NewGuid().ToString("N"); var security = new PipeSecurity();
                security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
                using (var server = SshfsSetup.CreateInstallerPipe(pipeName, security))
                {
                    bool collision = false; try { using (SshfsSetup.CreateInstallerPipe(pipeName, security)) { } } catch (System.ComponentModel.Win32Exception) { collision = true; }
                    check(collision, "installer pipe requires first instance");
                    if (loopback) {
                    var wait = server.WaitForConnectionAsync();
                    using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        client.Connect(3000); wait.GetAwaiter().GetResult();
                        SshfsSetup.VerifyPipePeer(server, Process.GetCurrentProcess().Id, true);
                        SshfsSetup.VerifyPipePeer(client, Process.GetCurrentProcess().Id, false);
                        check(true, "installer validates actual client and server process IDs");
                        rejects(delegate { SshfsSetup.VerifyPipePeer(server, Int32.MaxValue, true); }, "installer rejects unexpected client PID");
                        rejects(delegate { SshfsSetup.VerifyPipePeer(client, Int32.MaxValue, false); }, "helper rejects unexpected server PID");
                    }
                    }
                    else checks.Add("SKIP: named-pipe connection denied by execution environment; runtime peer PID tests unavailable");
                }
                check(SshfsSetup.Helper(new[] { "--sshfs-install-helper", pipeName, root, "1" }) == 87, "old unauthenticated helper invocation rejected");
                if (loopback)
                {
                    var events = new List<SshfsSetupProgress>(); bool failedPackage = false;
                    File.WriteAllText(Path.Combine(root,SshfsSetup.Packages.Single(x=>x.Flag==1).FileName),"Test-only invalid MSI fixture; never install.");
                    try { SshfsSetup.Install(root, 1, CancellationToken.None, events.Add, true).GetAwaiter().GetResult(); } catch (IOException) { failedPackage = true; }
                    check(failedPackage && events.Any(x => x.Stage == "verify") && events.Any(x => x.Stage == "result"), "actual helper authenticates both peers before rejecting an invalid fixture package without installing");
                }
                else checks.Add("SKIP: child-process named-pipe isolation prevents installer helper integration");
                File.WriteAllLines(Path.Combine(root, "security-audit-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count(x => !x.StartsWith("SKIP:")) + " checks; skipped: " + checks.Count(x => x.StartsWith("SKIP:")) })); return 0;
            }
            catch (Exception ex) { File.WriteAllLines(Path.Combine(root, "security-audit-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
        }
        static bool CanUseLoopback()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try { listener.Start(); using (var client = new TcpClient()) { client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port); return true; } }
            catch (SocketException) { return false; }
            finally { listener.Stop(); }
        }
        sealed class ListingServer : IDisposable
        {
            readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0); readonly Task worker;
            TcpClient control; TcpListener data; volatile bool disposed;
            internal readonly int Port; internal int Deletes;
            internal ListingServer(string mode, string name)
            {
                listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                worker = Task.Run(delegate
                {
                    try
                    {
                        while (true) {
                        control = listener.AcceptTcpClient();
                        using (control) using (var stream = control.GetStream()) using (var reader = new StreamReader(stream, Encoding.ASCII))
                        using (var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
                        {
                            writer.WriteLine("220 fixture"); string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string cmd = line.Split(' ')[0];
                                if (cmd == "USER") writer.WriteLine("331 password");
                                else if (cmd == "PASS") writer.WriteLine("230 logged in");
                                else if (cmd == "PWD") writer.WriteLine("257 \"/\"");
                                else if (cmd == "EPSV" || cmd == "PASV")
                                {
                                    if (data != null) data.Stop(); data = new TcpListener(IPAddress.Loopback, 0); data.Start(); int port = ((IPEndPoint)data.LocalEndpoint).Port;
                                    writer.WriteLine(cmd == "EPSV" ? "229 Entering Extended Passive Mode (|||" + port + "|)" : "227 Entering Passive Mode (127,0,0,1," + port / 256 + "," + port % 256 + ")");
                                }
                                else if (cmd == "MLSD" || cmd == "LIST")
                                {
                                    writer.WriteLine("150 listing");
                                    using (var connection = data.AcceptTcpClient()) using (var output = new StreamWriter(connection.GetStream(), Encoding.ASCII) { NewLine = "\r\n" })
                                        output.WriteLine(mode == "MLSD" ? "type=dir;size=0; " + name : mode == "DOS" ? "10-23-26  05:00PM       <DIR> " + name : "drwxr-xr-x 1 user group 0 Oct 23 2026 " + name);
                                    data.Stop(); data = null; writer.WriteLine("226 complete");
                                }
                                else if (cmd == "MLST" && line == "MLST /folder") writer.WriteLine("250-Listing\r\n type=dir; /folder\r\n250 End");
                                else if (cmd == "MLST") writer.WriteLine("500 unsupported");
                                else if (cmd == "DELE" || cmd == "RMD") { Interlocked.Increment(ref Deletes); writer.WriteLine("250 deleted"); }
                                else if (cmd == "FEAT") writer.WriteLine("211 no features");
                                else if (cmd == "QUIT") { writer.WriteLine("221 bye"); break; }
                                else writer.WriteLine("200 OK");
                            }
                        }
                        }
                    }
                    catch (IOException) { } catch (SocketException) { } catch (ObjectDisposedException) { } catch(InvalidOperationException)when(disposed) { }
                });
            }
            public void Dispose() { disposed=true;listener.Stop(); if (control != null) control.Close(); if (data != null) data.Stop(); if (!worker.Wait(5000)) throw new IOException("FTP fixture did not stop."); }
        }
    }
}
