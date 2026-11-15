using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace FileCompass
{
    internal static class ArchiveFormatsTests
    {
        internal static int Detection(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            try {
                SharpSevenZip.SharpSevenZipBase.SetLibraryPath(ArchiveEngine.SevenZipLibraryPath);
                string folder = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
                File.WriteAllText(Path.Combine(folder, "payload.txt"), "archive detection test");
                string zip = Path.Combine(root, "source.zip"); if (File.Exists(zip)) File.Delete(zip);
                System.IO.Compression.ZipFile.CreateFromDirectory(folder, zip);
                var open = typeof(ArchiveWorker).GetMethod("Open", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var execute = typeof(ArchiveWorker).GetMethod("Execute", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                foreach (string name in new[] { "archive", "archive.custom", "archive.rar" }) {
                    string path = Path.Combine(root, name); File.Copy(zip, path, true);
                    using (var reader = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { path, null }))
                        if (reader.FilesCount != 1) throw new IOException("Wrong entry count: " + name);
                    string output = Path.Combine(root, "out-" + name); Directory.CreateDirectory(output);
                    execute.Invoke(null, new object[] { new[] { "x", "-o" + output, "--", path }, "", root, new ArchiveBudget(new ArchiveSettings()), (Action<string>)(x => { }) });
                    if (File.ReadAllText(Path.Combine(output, "payload.txt")) != "archive detection test") throw new IOException("Wrong extracted contents: " + name);
                    checks.Add(name + " detected and extracted");
                }
                string encrypted = Path.Combine(root, "encrypted.custom"); if (File.Exists(encrypted)) File.Delete(encrypted);
                var compressor = new SharpSevenZip.SharpSevenZipCompressor { ArchiveFormat = SharpSevenZip.OutArchiveFormat.SevenZip, EncryptHeaders = true };
                compressor.CompressFilesEncrypted(encrypted, "test-password", Path.Combine(folder, "payload.txt"));
                try { using (var ignored = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { encrypted, null })) { } throw new Exception("Encrypted headers did not request a password"); }
                catch (System.Reflection.TargetInvocationException ex) { if (!(ex.InnerException is ArchivePasswordException)) throw; }
                using (var reader = (SharpSevenZip.SharpSevenZipExtractor)open.Invoke(null, new object[] { encrypted, "test-password" }))
                    if (reader.FilesCount != 1) throw new IOException("Encrypted archive could not be opened");
                checks.Add("renamed encrypted archive requests and accepts its password");
                string invalid = Path.Combine(root, "ordinary.dat"); File.WriteAllText(invalid, "not an archive");
                try { open.Invoke(null, new object[] { invalid, null }); throw new Exception("Non-archive accepted"); }
                catch (System.Reflection.TargetInvocationException ex) { if (!(ex.InnerException is IOException) || ex.InnerException is ArchivePasswordException) throw; }
                checks.Add("ordinary file reports a format error, not a password request");
                string fixture = Environment.GetEnvironmentVariable("FILECOMPASS_ARCHIVE_FIXTURE");
                if (!String.IsNullOrWhiteSpace(fixture)) {
                    string output = Path.Combine(root, "real-fixture"); Directory.CreateDirectory(output);
                    execute.Invoke(null, new object[] { new[] { "x", "-o" + output, "--", fixture }, "", root, new ArchiveBudget(new ArchiveSettings()), (Action<string>)(x => { }) });
                    using (var zipReader = System.IO.Compression.ZipFile.OpenRead(fixture)) {
                        var sample = zipReader.Entries.First(x => !x.FullName.EndsWith("/", StringComparison.Ordinal));
                        string extracted = ArchiveEngine.DiskPath(output, sample.FullName);
                        using (var hash = System.Security.Cryptography.SHA256.Create()) using (var a = sample.Open()) using (var b = File.OpenRead(extracted))
                            if (!hash.ComputeHash(a).SequenceEqual(hash.ComputeHash(b))) throw new IOException("Fixture contents differ");
                    }
                    checks.Add("real renamed ZIP extracts and matches its content");
                }
                File.WriteAllLines(Path.Combine(root, "archive-detection-result.txt"), checks.Concat(new[] { "PASS" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-detection-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            Action<bool,string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                var settings = new ArchiveSettings(); settings.Save();
                string folder = Directory.CreateDirectory(Path.Combine(root, "vstup")).FullName;
                string file = Path.Combine(folder, "český.txt"); File.WriteAllText(file, String.Concat(Enumerable.Repeat("Původní data!", 10000)));
                Directory.CreateDirectory(Path.Combine(folder, "prázdná"));
                foreach (string extension in new[] { "zip", "7z", "tar", "gz", "bz2", "xz", "tar.gz", "tar.bz2", "tar.xz", "tgz", "tbz2", "txz" }) {
                    bool single = new[] { "gz", "bz2", "xz" }.Contains(extension);
                    string archive = Path.Combine(root, "balík." + extension); var updates = new List<string>();
                    ArchiveEngine.Create(archive, new[] { single ? file : folder }, settings, CancellationToken.None, updates.Add);
                    var doc = ArchiveEngine.Open(archive); ArchiveEngine.Test(doc, CancellationToken.None);
                    check(doc.Items.Count > 0, extension + " correct format opens and passes integrity test: " + doc.Type);
                    using (var temp = new ArchiveTemp()) {
                        ArchiveEngine.Extract(doc, doc.Items.Select(x => x.Path).ToArray(), temp.Path, CancellationToken.None);
                        string payload = Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories).First();
                        if (!single && extension != "zip" && extension != "7z" && extension != "tar") {
                            var tar = ArchiveEngine.Open(payload); check(tar.Type == "tar", extension + " contains a real TAR");
                            using (var inside = new ArchiveTemp()) { ArchiveEngine.Extract(tar, tar.Items.Select(x => x.Path), inside.Path, CancellationToken.None); check(File.ReadAllText(Directory.GetFiles(inside.Path, "*", SearchOption.AllDirectories).Single()) == File.ReadAllText(file), extension + " nested payload roundtrip"); }
                        } else check(File.ReadAllText(payload) == File.ReadAllText(file), extension + " exact payload roundtrip");
                    }
                    check(updates.Count > 1, extension + " reports progress");
                }
                bool rejected = false; try { ArchiveEngine.Create(Path.Combine(root, "bad.gz"), new[] { folder }, settings, CancellationToken.None); } catch (IOException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(root, "bad.gz")), "single-stream format rejects folders without creating output");
                var tarDoc = ArchiveEngine.Open(Path.Combine(root, "balík.tar"));
                check(!ArchiveEngine.Writable(tarDoc), "TAR existing archive remains read-only rather than allowing incomplete updates");
                check(ArchivePackDialog.ChangeFormat("balík.tar.gz", "zip") == "balík.zip", "switching compound format removes whole extension");
                check(ArchiveEngine.OutputSuffix("BALÍK.TAR.GZ") == ".tar.gz", "separate archives retain compound extension");
                bool cancelled = false; using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); try { ArchiveEngine.Create(Path.Combine(root, "cancel.tar.xz"), new[] { folder }, settings, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; } }
                check(cancelled && !File.Exists(Path.Combine(root, "cancel.tar.xz")), "cancelled creation leaves no target");
                File.WriteAllLines(Path.Combine(root, "archive-formats-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-formats-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
    }
}

