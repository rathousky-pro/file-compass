using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public static class NetworkResumeTests
    {
        sealed class CountingSource : MemoryStream
        {
            internal long BytesRead;
            internal CountingSource(byte[] bytes) : base(bytes) { }
            public override int Read(byte[] bytes, int offset, int length) { int count = base.Read(bytes, offset, length); BytesRead += count; return count; }
        }
        static SessionOptions Options(string workspace, int port, bool sftp, string fingerprint = null)
        {
            var profile = new NetworkProfile { Protocol = sftp ? NetworkProtocol.SFTP : NetworkProtocol.FTP, Host = "127.0.0.1", Port = port, UserName = "tester", HostKeyFingerprint = fingerprint ?? "" };
            var options = new RemoteConnection(profile).Options("test-password");
            options.AddRawSettings("ResumeDirectory", Path.Combine(workspace, "checkpoints"));
            string ca = Path.Combine(Path.GetDirectoryName(workspace), "ca.pem");
            if (!sftp && File.Exists(ca)) { options.FtpSecure = FtpSecure.Explicit; options.AddRawSettings("TlsCaFile", ca); }
            return options;
        }
        public static int Once(string[] args)
        {
            try
            {
                var options = Options(args[1], Int32.Parse(args[2]), Boolean.Parse(args[3]), args[6]); double initial = -1;
                using (var session = new Session()) { session.Open(options); session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { if (initial < 0) initial = e.FileProgress; }; session.GetFiles(args[4], args[5], false, new TransferOptions { Resume = true }); }
                File.WriteAllText(Path.Combine(args[1], "restart-result.txt"), initial.ToString(System.Globalization.CultureInfo.InvariantCulture)); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(args[1], "restart-result.txt"), ex.ToString()); return 1; }
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static bool Rejected(Action action, string text)
        { try { action(); return false; } catch (IOException ex) { return ex.Message.Contains(text); } }
        internal static int NameOnly(string root,int port) {
            Directory.CreateDirectory(root);var checks=new List<string>();
            Action<bool,string> check=delegate(bool v,string m){if(!v)throw new Exception(m);checks.Add(m);};
            try {
                var profile=new NetworkProfile{Protocol=NetworkProtocol.FTP,Host="127.0.0.1",Port=port,UserName="tester"};
                using(var connection=new RemoteConnection(profile)) {
                    connection.Open(Options(root,port,false));
                    check(connection.Session.ListDirectory("/").Files.Any(x=>x.Name=="český soubor.txt"),"automatic listing decodes Czech UTF-8 without advertised FEAT");
                    string file=Path.Combine(root,"Pepíček Žluťoučký 50.txt");File.WriteAllText(file,"Příliš žluťoučký kůň");
                    using(var worker=connection.TransferConnection()) {
                        worker.OpenTransfer();NetworkTransfers.Copy(new Entry{FullPath=file},null,worker,"/",NetworkCopyRule.Resume);
                    }
                    check(connection.Session.ListDirectory("/").Files.Any(x=>x.Name==Path.GetFileName(file)),"worker upload and final rename preserve exact Czech filename");
                    var result=new NetworkCopyResult();NetworkTransfers.Copy(new Entry{FullPath=file},null,connection,"/",NetworkCopyRule.Resume,result);
                    check(result.Skipped==1&&result.Copied==0,"repeating resume recognizes completed Czech-named file");
                    string copy=Path.Combine(root,"downloaded.txt");connection.Session.GetFiles("/"+Path.GetFileName(file),copy);
                    check(File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(file)),"Czech-named file downloads byte-exactly");
                }
                File.WriteAllLines(Path.Combine(root,"ftp-name-test-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"ftp-name-test-result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}
        }
        internal static int QueueOnly(string root,int port) {
            Directory.CreateDirectory(root);var checks=new List<string>(); MainWindow main=null;
            try {
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=System.Windows.WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                var profile=new NetworkProfile { Protocol=NetworkProtocol.FTP,Host="127.0.0.1",Port=port,UserName="tester" };
                using(var browsing=new RemoteConnection(profile)){browsing.Open(Options(root,port,false));NavigationTests.Wait(main.VerifyFtpResumeQueue(browsing,root,delegate(bool v,string m){if(!v)throw new Exception(m);checks.Add(m);}));}
                File.WriteAllLines(Path.Combine(root,"ftp-queue-resume-test-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"ftp-queue-resume-test-result.txt"),String.Join("\n",checks)+"\n"+ex);return 1;}finally{if(main!=null)main.Close();}
        }
        static TransferCheckpointData Read(string path)
        { return FileIntegrity.ReadXml<TransferCheckpointData>(path); }
        static string Hash(string path) { using (var input = File.OpenRead(path)) return TransferCheckpoint.Hash(input, input.Length, delegate { }); }
        public static int Run(string workspace, int port, bool sftp)
        {
            Directory.CreateDirectory(workspace); var passed = new List<string>();
            Action<bool, string> assert = delegate(bool v, string m) { Check(v, m); passed.Add(m); };
            string resultFile = Path.Combine(workspace, (sftp ? "sftp" : "ftp") + "-resume-test-result.txt");
            var profile = new NetworkProfile { Protocol = sftp ? NetworkProtocol.SFTP : NetworkProtocol.FTP, Host = "127.0.0.1", Port = port, UserName = "tester" };
            var options = Options(workspace, port, sftp);
            Func<Session> open = delegate { var session = new Session(); session.Open(options); return session; };
            Action<Session, string, string, double> cancelDownload = delegate(Session session, string source, string target, double fraction)
            {
                session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { e.Cancel = e.FileProgress >= fraction; };
                bool cancelled = false; try { session.GetFiles(source, target, false, new TransferOptions { Resume = true }); } catch (OperationCanceledException) { cancelled = true; }
                assert(cancelled, "download cancellation is reported");
            };
            Action<Session, string, string> cancelUpload = delegate(Session session, string source, string target)
            {
                session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { e.Cancel = e.FileProgress >= .25; };
                bool cancelled = false; try { session.PutFiles(source, target); } catch (OperationCanceledException) { cancelled = true; }
                assert(cancelled, "upload cancellation is reported");
            };
            try
            {
                using (var data = new CountingSource(new byte[24 * 1048576]))
                using (var checkpoint = new TransferCheckpoint(options, true, "bounded-source-test", "/bounded-source-test"))
                {
                    checkpoint.Initialize("/bounded-source-part", data.Length, DateTime.UtcNow, null, false, 0, DateTime.MinValue);
                    string digest = TransferCheckpoint.Hash(data, 1048576, delegate { });
                    checkpoint.Data.Blocks = Enumerable.Range(0, 24).Select(x => new TransferBlock { Length = 1048576, Hash = digest }).ToList(); checkpoint.Data.Offset = data.Length;
                    data.BytesRead = 0; checkpoint.ValidateUpload(data, delegate { });
                    assert(data.BytesRead <= 2 * 1048576, "resume validates only boundary source blocks, not all 24 MiB of the prefix");
                    data.BytesRead = 0; checkpoint.SetUploadPrefix(data, data.Length - 123, delegate { });
                    assert(data.BytesRead <= 1048576 && checkpoint.Data.Blocks.Sum(x => (long)x.Length) == data.Length - 123, "adjusting server offset reuses saved hashes and rereads at most one shortened block");
                    checkpoint.Clear();
                }
                if (sftp) using (var probe = new Session()) { options.SshHostKeyFingerprint = probe.ScanFingerprint(options, "SHA-256"); profile.HostKeyFingerprint = options.SshHostKeyFingerprint; }
                string target = Path.Combine(workspace, "slow.bin");
                using (var session = open()) cancelDownload(session, "/slow.bin", target, .18);
                string manifest = Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single(); var first = Read(manifest);
                assert(first.Offset > 0 && first.Offset < first.Length && !File.Exists(target), "cancelled download retains owned partial without exposing final target");
                assert(!File.ReadAllText(manifest).Contains("test-password"), "durable manifest contains no credentials");
                if (File.Exists(Path.Combine(Path.GetDirectoryName(workspace), "no-resume.txt")))
                {
                    using (var session = open()) assert(Rejected(delegate { session.GetFiles("/slow.bin", target, false, new TransferOptions { Resume = true }); }, "libcurl"), "server rejecting REST fails explicitly without restarting or overwriting");
                    assert(!File.Exists(target) && Read(manifest).Offset == first.Offset, "rejected download resume preserves its partial and checkpoint");
                    using (var session = open()) session.GetFiles("/slow.bin", target);
                    string refusedSource = Path.Combine(workspace, "refused-upload.bin"); File.WriteAllBytes(refusedSource, File.ReadAllBytes(target).Take(2097152).ToArray());
                    using (var session = open()) cancelUpload(session, refusedSource, "/refused-upload.bin");
                    var refusedUpload = Read(Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single());
                    using (var session = open()) assert(Rejected(delegate { session.PutFiles(refusedSource, "/refused-upload.bin", false, new TransferOptions { Resume = true }); }, "libcurl"), "server rejecting APPE fails explicitly without overwriting");
                    using (var session = open()) assert(session.FileExists(refusedUpload.Stage) && !session.FileExists("/refused-upload.bin"), "rejected upload resume preserves staged data");
                    File.WriteAllLines(resultFile, passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
                }
                // Emulate bytes flushed after the last manifest before a hard process termination.
                using (var tail = new FileStream(first.Stage, FileMode.Append)) tail.Write(new byte[] { 9, 8, 7 }, 0, 3);
                using (var session = open()) cancelDownload(session, "/slow.bin", target, .4);
                var second = Read(manifest); assert(second.Offset > first.Offset, "fresh session resumes and can be cancelled a second time after truncating unconfirmed tail");
                double initial = -1;
                var restartArguments = new[] { "--resume-once-test", workspace, port.ToString(), sftp.ToString(), "/slow.bin", target, options.SshHostKeyFingerprint ?? "" };
                var restartInfo = new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, String.Join(" ", restartArguments.Select(x => "\"" + x.Replace("\"", "") + "\""))) { UseShellExecute = false, CreateNoWindow = true };
                using (var restarted = System.Diagnostics.Process.Start(restartInfo)) { if (!restarted.WaitForExit(45000)) throw new IOException("Restart test timeout."); assert(restarted.ExitCode == 0, "a separate application process resumes the durable checkpoint"); }
                initial = Double.Parse(File.ReadAllText(Path.Combine(workspace, "restart-result.txt")), System.Globalization.CultureInfo.InvariantCulture);
                byte[] expected = new byte[8388608]; for (int i = 0; i < expected.Length; i++) expected[i] = (byte)i;
                using (var source = new MemoryStream(expected)) assert(Hash(target) == TransferCheckpoint.Hash(source, expected.Length, delegate { }), "resumed download has byte-exact content");
                assert(initial > .18 && !File.Exists(manifest) && !File.Exists(second.Stage), "progress includes retained bytes and committed download removes manifest and stage");
                string corruptTarget = Path.Combine(workspace, "corrupt.bin");
                using (var session = open()) cancelDownload(session, "/slow.bin", corruptTarget, .12);
                var damaged = Read(Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single());
                using (var file = new FileStream(damaged.Stage, FileMode.Open, FileAccess.Write)) file.WriteByte(99);
                using (var session = open()) assert(Rejected(delegate { session.GetFiles("/slow.bin", corruptTarget, false, new TransferOptions { Resume = true }); }, "poškozený"), "corrupted local partial is rejected without committing target");
                using (var session = open()) session.GetFiles("/slow.bin", corruptTarget);
                assert(Hash(corruptTarget) == Hash(target), "explicit overwrite recovers from damaged checkpoint");
                string sourceFile = Path.Combine(workspace, "upload.bin"); File.WriteAllBytes(sourceFile, expected.Take(2097152).ToArray());
                using (var session = open()) cancelUpload(session, sourceFile, "/resume-upload.bin");
                var upload = Read(Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single());
                assert(upload.SourceHash == null && upload.Offset > 0 && upload.Offset < upload.Length && upload.Blocks.Sum(x => (long)x.Length) == upload.Offset,
                    "upload starts sending before reading the whole source and saves hashes of the streamed prefix on cancellation");
                long remoteOffset;
                using (var session = open()) { remoteOffset = session.GetFileInfo(upload.Stage).Length; assert(remoteOffset > 0 && !session.FileExists("/resume-upload.bin"), "cancelled upload preserves remote staging and original target"); }
                assert(upload.OriginalName == Path.GetFileName(sourceFile) && upload.SourcePath == sourceFile && upload.TargetPath == "/resume-upload.bin", "local resume record identifies original source and destination");
                using (var session = open()) {
                    string metadataFile=Path.Combine(workspace,"partial-info.xml");
                    session.GetFiles(upload.Stage+".fcinfo.xml",metadataFile);
                    PartialTransferInfo info;using(var input=File.OpenRead(metadataFile)) info=(PartialTransferInfo)new System.Xml.Serialization.XmlSerializer(typeof(PartialTransferInfo)).Deserialize(input);
                    assert(info.OriginalName==Path.GetFileName(sourceFile) && info.TargetPath=="/resume-upload.bin" && info.SourceLength==new FileInfo(sourceFile).Length && info.CheckpointIdentity==upload.Identity,"remote sidecar identifies its exact source name, target, size and owned checkpoint");
                    assert(!File.ReadAllText(metadataFile).Contains("test-password") && !File.ReadAllText(metadataFile).Contains(sourceFile),"remote metadata contains neither credentials nor the private local path");
                }
                initial = -1;
                using (var session = open()) { session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { if (e.FileProgress > 0 && initial < 0) initial = e.FileProgress; }; session.PutFiles(sourceFile, "/resume-upload.bin", false, new TransferOptions { Resume = true }); assert(session.ResumeProbeBytes > 0 && session.ResumeProbeBytes <= 131072, "real upload resume reads at most 128 KiB remotely instead of the entire partial"); }
                string uploaded = Path.Combine(workspace, "uploaded.bin"); using (var session = open()) session.GetFiles("/resume-upload.bin", uploaded);
                assert(initial >= (double)remoteOffset / new FileInfo(sourceFile).Length && Hash(uploaded) == Hash(sourceFile), "upload resumes at verified remote offset with byte-exact result");
                assert(Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Length == 0, "completed upload and download clear durable checkpoints");
                using (var session = open()) cancelUpload(session, sourceFile, "/legacy-upload.bin");
                string legacyManifest = Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single();
                var legacy = Read(legacyManifest); legacy.SourceHash = Hash(sourceFile); legacy.Blocks.Clear(); legacy.Offset = 0;
                using (var file = File.Create(legacyManifest)) new System.Xml.Serialization.XmlSerializer(typeof(TransferCheckpointData)).Serialize(file, legacy);
                IntegrityTests.SignLegacyXml(legacyManifest);
                using (var session = open()) session.PutFiles(sourceFile, "/legacy-upload.bin", false, new TransferOptions { Resume = true });
                using (var session = open()) session.GetFiles("/legacy-upload.bin", uploaded);
                assert(!File.Exists(legacyManifest) && Hash(uploaded) == Hash(sourceFile), "whole-file checkpoints from older versions remain resumable after migration to streaming hashes");
                using (var session = open()) cancelUpload(session, sourceFile, "/changed-source.bin");
                DateTime originalTime = File.GetLastWriteTime(sourceFile); using (var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Write)) source.WriteByte(77); File.SetLastWriteTime(sourceFile, originalTime);
                using (var session = open()) assert(Rejected(delegate { session.PutFiles(sourceFile, "/changed-source.bin", false, new TransferOptions { Resume = true }); }, "Zdroj se"), "source change is rejected even when upload size and timestamp remain identical");
                string changedManifest = Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single();
                using (var session = open()) session.PutFiles(sourceFile, "/changed-source.bin");
                assert(!File.Exists(changedManifest), "explicit overwrite replaces changed upload checkpoint");
                using (var session = open()) cancelUpload(session, sourceFile, "/corrupt-upload.bin");
                var corruptUpload = Read(Directory.GetFiles(Path.Combine(workspace, "checkpoints"), "*.xml").Single());
                string wrong = Path.Combine(workspace, "wrong.bin"); File.WriteAllBytes(wrong, Enumerable.Repeat((byte)42, 65536).ToArray());
                using (var session = open()) session.PutFiles(wrong, corruptUpload.Stage);
                using (var session = open()) assert(Rejected(delegate { session.PutFiles(sourceFile, "/corrupt-upload.bin", false, new TransferOptions { Resume = true }); }, "liší"), "corrupted remote upload prefix is rejected");
                using (var session = open()) session.PutFiles(sourceFile, "/corrupt-upload.bin");
                string oldTarget = Path.Combine(workspace, "existing.bin"); File.WriteAllText(oldTarget, "original target");
                using (var session = open()) cancelDownload(session, "/slow.bin", oldTarget, .12);
                assert(File.ReadAllText(oldTarget) == "original target", "cancelled overwrite preserves original local target");
                File.WriteAllText(oldTarget, "edited while cancelled");
                using (var session = open()) assert(Rejected(delegate { session.GetFiles("/slow.bin", oldTarget, false, new TransferOptions { Resume = true }); }, "Cílový soubor"), "target edited during interruption is not silently overwritten");
                using (var session = open())
                using (var cp = new TransferCheckpoint(options, false, "/slow.bin", oldTarget))
                { assert(Rejected(delegate { using (var competing = new TransferCheckpoint(options, false, "/slow.bin", oldTarget)) { } }, "jiná úloha"), "exclusive lease rejects competing writers"); }
                using (var session = open()) session.PutFiles(wrong, "/slow.bin");
                using (var session = open()) assert(Rejected(delegate { session.GetFiles("/slow.bin", oldTarget, false, new TransferOptions { Resume = true }); }, "Zdroj se"), "changed remote metadata rejects download resume");
                string batch = Path.Combine(workspace, "batch"); Directory.CreateDirectory(batch); File.WriteAllText(Path.Combine(batch, "a.txt"), "first"); File.WriteAllBytes(Path.Combine(batch, "b.bin"), expected.Take(2097152).ToArray()); Directory.CreateDirectory(Path.Combine(batch, "sub")); File.WriteAllText(Path.Combine(batch, "sub", "z.txt"), "last");
                using (var connection = new RemoteConnection(profile)) { connection.Open(options); NetworkTransfers.Copy(new Entry { FullPath = batch, IsDirectory = true }, null, connection, "/", NetworkCopyRule.Overwrite); }
                string batchTarget = Path.Combine(workspace, "batch-target"); Directory.CreateDirectory(batchTarget);
                using (var connection = new RemoteConnection(profile))
                {
                    connection.Open(options); connection.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { e.Cancel = e.FileName.EndsWith("/b.bin") && e.FileProgress >= .25; };
                    try { NetworkTransfers.Copy(new Entry { FullPath = "/batch", IsDirectory = true }, connection, null, batchTarget, NetworkCopyRule.Overwrite); } catch (OperationCanceledException) { }
                }
                string firstTarget = Path.Combine(batchTarget, "batch", "a.txt"); assert(File.Exists(firstTarget), "batch preserves already completed files"); File.SetLastWriteTime(firstTarget, new DateTime(2001, 1, 1));
                var batchResult = new NetworkCopyResult(); initial = -1;
                using (var connection = new RemoteConnection(profile))
                {
                    connection.Open(options); connection.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { if (e.FileName.EndsWith("/b.bin") && initial < 0) initial = e.FileProgress; };
                    NetworkTransfers.Copy(new Entry { FullPath = "/batch", IsDirectory = true }, connection, null, batchTarget, NetworkCopyRule.Resume, batchResult);
                }
                assert(batchResult.Skipped == 1 && batchResult.Copied == 2 && initial > 0 && File.GetLastWriteTime(firstTarget).Year == 2001, "folder resume skips completed file, resumes interrupted file, and copies missing nested file");
                assert(Hash(Path.Combine(batchTarget, "batch", "b.bin")) == Hash(Path.Combine(batch, "b.bin")) && File.ReadAllText(Path.Combine(batchTarget, "batch", "sub", "z.txt")) == "last", "resumed recursive batch contains correct files");
                using (var connection = new RemoteConnection(profile))
                {
                    connection.Open(options); connection.Session.CreateDirectory("/upload-batch");
                    connection.Session.FileTransferProgress += delegate(object sender, FileTransferProgressEventArgs e) { e.Cancel = e.FileName.EndsWith("b.bin") && e.FileProgress >= .25; };
                    bool cancelled = false; try { NetworkTransfers.Copy(new Entry { FullPath = batch, IsDirectory = true }, null, connection, "/upload-batch", NetworkCopyRule.Overwrite); } catch (OperationCanceledException) { cancelled = true; }
                    assert(cancelled && connection.Session != null, "recursive upload interruption retains earlier completed files and current stage");
                }
                var uploadBatchResult = new NetworkCopyResult();
                using (var connection = new RemoteConnection(profile))
                {
                    connection.Open(options); NetworkTransfers.Copy(new Entry { FullPath = batch, IsDirectory = true }, null, connection, "/upload-batch", NetworkCopyRule.Resume, uploadBatchResult);
                    string uploadedBatch = Path.Combine(workspace, "batch-uploaded.bin"); connection.Session.GetFiles("/upload-batch/batch/b.bin", uploadedBatch);
                    assert(uploadBatchResult.Skipped == 1 && uploadBatchResult.Copied == 2 && Hash(uploadedBatch) == Hash(Path.Combine(batch, "b.bin")) && connection.Session.FileExists("/upload-batch/batch/sub/z.txt"), "recursive upload resume skips completed file, resumes interrupted file, and sends missing nested file");
                }
                if (!sftp) {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                    var main=new MainWindow(workspace,new ProgramSettings{AutoRefresh=false}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=System.Windows.WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                    try {using(var browsing=new RemoteConnection(profile)){browsing.Open(options);NavigationTests.Wait(main.VerifyFtpResumeQueue(browsing,workspace,assert));}}
                    finally {main.Close();}
                }
                File.WriteAllLines(resultFile, passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(resultFile, "FAIL after " + passed.Count + " checks\n" + String.Join("\n", passed) + "\n" + ex); return 1; }
        }
    }
}

