using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace FileCompass
{
    internal static class ArchiveUpgradeTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var passed = new List<string>();
            Action<bool, string> check = delegate(bool ok, string text) { if (!ok) throw new Exception(text); passed.Add(text); };
            Action<Action, string> reject = delegate(Action action, string text) { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } check(rejected, text); };
            string original = ArchiveSettings.TestPath;
            try
            {
                var settings = new ArchiveSettings { WinRarPath = ArchiveSettings.FindWinRar() ?? "" }; settings.Save();
                string first = Path.Combine(root, "first.txt"), second = Path.Combine(root, "second.txt"); File.WriteAllText(first, "FIRST"); File.WriteAllText(second, "SECOND");
                string rar = Path.Combine(root, "plain.rar");
                if (settings.WinRarPath.Length > 0) { ArchiveEngine.Create(rar, new[] { first }, settings, CancellationToken.None); check(ArchiveEngine.Open(rar).Items.Count == 1, "plain RAR remains readable through callback library"); }
                string secret = "Secret-" + Guid.NewGuid().ToString("N");
                foreach (string format in new[] { "zip", "7z" })
                {
                    string file = Path.Combine(root, "encrypted." + format);
                    ArchiveEngine.Create(file, new[] { first, second }, settings, CancellationToken.None, null, new ArchivePackOptions { Password = secret });
                    var doc = ArchiveEngine.Open(file, secret); ArchiveEngine.Test(doc, CancellationToken.None);
                    check(doc.Items.All(x => x.Encrypted), format + " password callback creates encrypted payload");
                    doc.Password = "wrong"; reject(delegate { ArchiveEngine.Test(doc, CancellationToken.None); }, format + " rejects wrong callback password");
                    check(doc.Password == null, format + " clears rejected password for retry");
                }
                var plain = Path.Combine(root, "plain.zip"); ArchiveEngine.Create(plain, new[] { first, second }, settings, CancellationToken.None);
                settings.MaximumItems = 1; settings.Save(); reject(delegate { ArchiveEngine.Open(plain); }, "item count budget rejects archive before extraction");
                settings.MaximumItems = 100000; settings.Save();
                string huge = Path.Combine(root, "expanded.zip"); using (var file = File.Create(huge)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create)) using (var stream = zip.CreateEntry("large.bin").Open()) { var zeros = new byte[2 * 1048576]; stream.Write(zeros, 0, zeros.Length); }
                settings.MaximumExtractedMiB = 1; settings.Save(); reject(delegate { ArchiveEngine.Open(huge); }, "expanded size budget rejects high compression ratio payload");
                using (var memory = new MemoryStream())
                using (var output = new ArchiveOutputStream(memory, new ArchiveBudget(settings), null))
                {
                    var block = new byte[700000]; output.Write(block, 0, block.Length);
                    reject(delegate { output.Write(block, 0, block.Length); }, "actual output stream enforces cumulative byte budget");
                    check(memory.Length == block.Length, "rejected output block is not written");
                }
                settings.MaximumExtractedMiB = 65536; settings.MinimumFreeMiB = Math.Min(16777216, new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace / 1048576 + 100); settings.Save();
                var document = ArchiveEngine.Open(plain);
                reject(delegate { ArchiveEngine.Extract(document, document.Items.Select(x => x.Path), Path.Combine(root, "no-space"), CancellationToken.None); }, "free disk reserve rejects extraction");
                settings.MinimumFreeMiB = 0; settings.MaximumListingMiB = 1; settings.Save();
                string many = Path.Combine(root, "many.zip"); using (var file = File.Create(many)) using (var zip = new ZipArchive(file, ZipArchiveMode.Create)) for (int i = 0; i < 3000; i++) zip.CreateEntry(i.ToString("D5") + new string('x', 180) + ".txt");
                reject(delegate { ArchiveEngine.Open(many); }, "listing budget bounds text sent to main process");
                settings.MaximumListingMiB = 16; settings.Save();
                settings.MaximumNesting = 1; settings.Save(); reject(delegate { ArchiveEngine.CheckNesting(new ArchiveDocument()); }, "nested archives obey configured depth"); settings.MaximumNesting = 8; settings.Save();
                string tempRoot = Path.Combine(Path.GetTempPath(), "FileCompass-archives"); var previousTemps = new HashSet<string>(Directory.GetDirectories(tempRoot));
                byte[] random = new byte[16 * 1048576]; using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(random); string expensive = Path.Combine(root, "random.bin"); File.WriteAllBytes(expensive, random); random = null;
                settings.MaximumMemoryMiB = 64; settings.MaximumSeconds = 15; settings.Save();
                string limited = Path.Combine(root, "limited.7z");
                bool memoryLimited = false;
                try { ArchiveEngine.Create(limited, new[] { expensive }, settings, CancellationToken.None, null, new ArchivePackOptions { Level = 9 }); }
                catch (IOException ex) { memoryLimited = ex.ToString().IndexOf("memory", StringComparison.OrdinalIgnoreCase) >= 0 || ex.ToString().IndexOf("pamě", StringComparison.OrdinalIgnoreCase) >= 0; if (!memoryLimited) throw; }
                check(memoryLimited, "native compression is confined by OS memory budget");
                check(!File.Exists(limited), "failed memory limited job does not publish archive");
                settings.MaximumMemoryMiB = 1024; settings.MaximumSeconds = 1; settings.Save();
                bool timedOut = false;
                try { ArchiveEngine.Create(limited, new[] { expensive }, settings, CancellationToken.None, null, new ArchivePackOptions { Level = 9 }); }
                catch (IOException ex) { timedOut = ex.ToString().IndexOf("časov", StringComparison.OrdinalIgnoreCase) >= 0; if (!timedOut) throw; }
                check(timedOut, "hard timeout stops costly native compression");
                check(!File.Exists(limited), "timed out job does not publish archive");
                check(!Directory.GetDirectories(tempRoot).Except(previousTemps).Any(), "parent cleans worker plaintext scratch after forced termination");
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.Cancel(); bool cancelled = false; try { BatchRenameEngine.Preview(new[] { first }, new RenameOptions(), cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                    check(cancelled, "rename preview accepts cancellation before metadata work");
                }
                settings.MaximumSeconds = 600; settings.Save();
                File.WriteAllLines(Path.Combine(root, "archive-upgrade-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-upgrade-result.txt"), "FAIL after " + passed.Count + " checks\n" + String.Join("\n", passed) + "\n" + ex); return 1; }
            finally { ArchiveSettings.TestPath = original; }
        }
    }
}

