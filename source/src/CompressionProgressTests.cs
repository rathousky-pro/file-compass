using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace FileCompass
{
    internal static class CompressionProgressTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            try {
                string input = Path.Combine(root, "input.bin"); var block = new byte[1048576]; new Random(517).NextBytes(block); using (var stream = File.Create(input)) for (int i = 0; i < 32; i++) stream.Write(block, 0, block.Length);
                var selected = new[] { input, Path.Combine(root, "second.bin"), Path.Combine(root, "third.bin") }; File.WriteAllBytes(selected[1], block); File.WriteAllBytes(selected[2], block); File.WriteAllText(Path.Combine(root, "unselected.txt"), "must not be packed");
                var settings = new ArchiveSettings { WinRarPath = ArchiveSettings.FindWinRar() ?? "" };
                foreach (string extension in String.IsNullOrEmpty(settings.WinRarPath) ? new[] { ".zip", ".7z" } : new[] { ".zip", ".7z", ".rar" }) {
                    string target = Path.Combine(root, "result" + extension); var reports = new List<ArchiveCompressionProgress>();
                    var clock = System.Diagnostics.Stopwatch.StartNew(); long firstReport = -1; bool locked = false;
                    ArchiveEngine.Create(target, selected, settings, CancellationToken.None, delegate(string text) { ArchiveCompressionProgress update; if (ArchiveCompressionProgress.TryParse(text, out update)) { reports.Add(update); if (firstReport < 0) { firstReport = clock.ElapsedMilliseconds; try { using (File.Open(input, FileMode.Open, FileAccess.Write, FileShare.None)) { } } catch (IOException) { locked = true; } } } });
                    if (!reports.Any(x => !x.Complete && x.Percent > 0 && x.Percent < 99)) throw new Exception("No real intermediate compression progress for " + extension);
                    if (extension != ".rar" && !reports.Any(x => x.Processed > 0 && x.Processed <= x.Original)) throw new Exception("Direct compressor did not report actual bytes read.");
                    var final = reports.Last(); if (!final.Complete || final.Original != selected.Sum(x => new FileInfo(x).Length) || final.Packed != new FileInfo(target).Length || final.Percent != 100) throw new Exception("Final compression sizes incorrect: " + extension);
                    var document = ArchiveEngine.Open(target); if (document.Items.Sum(x => x.Bytes) != final.Original || document.Items.Count(x => !x.Directory) != 3 || document.Items.Any(x => x.Path.Contains("unselected"))) throw new Exception("Archive contents mismatch.");
                    if (!locked) throw new Exception("Direct sources were not protected against changes.");
                    checks.Add(extension + " packs exactly three selected files, locks originals and reports real progress; first update after " + firstReport + " ms");
                }
                if (!new ArchiveCompressionProgress { Percent = 100, Original = 1000, Packed = 250, Complete = true }.Summary().Contains(25.0.ToString("F1"))) throw new Exception("Savings calculation incorrect.");
                if (!new ArchiveCompressionProgress { Percent = 100, Original = 1000, Packed = 1100, Complete = true }.Summary().Contains(110.0.ToString("F1"))) throw new Exception("Growing archive must show negative savings.");
                if (new ArchiveCompressionProgress { Complete = true }.Summary().Contains("NaN")) throw new Exception("Empty archive ratio invalid.");
                checks.Add("exact savings, archive growth and empty input handled");
                var example = new ArchiveCompressionProgress { Original = 1900L * 1048576, Processed = 200L * 1048576, Packed = 160L * 1048576, Percent = 10 };
                if (!example.Summary().Contains(80.0.ToString("F1")) || !example.Summary().Contains(CopyUi.Bytes(200L * 1048576))) throw new Exception("160 / 200 MiB must show 80 percent, independently of whole input size."); checks.Add("160 / 200 MiB shows 80 percent against processed input");
                File.WriteAllLines(Path.Combine(root, "compression-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllLines(Path.Combine(root, "compression-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
        }
    }
}

