using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace CommanderCZ
{
    internal static class ArchiveFormatsTests
    {
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

