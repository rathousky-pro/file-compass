using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace FileCompass
{
    internal static class ArchiveMemoryTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); string previousPath = ArchiveSettings.TestPath;
            try {
                var high = ArchiveMemoryPlan.Choose(1024, 8L * 1073741824, 100, 9, false);
                var low = ArchiveMemoryPlan.Choose(256, 8L * 1073741824, 100, 9, false);
                var fallback = ArchiveMemoryPlan.Choose(1024, 8L * 1073741824, 100, 9, true);
                if (high.DictionaryMiB > 32 || high.Threads > 2 || low.DictionaryMiB >= high.DictionaryMiB || fallback.DictionaryMiB > 2 || fallback.Threads != 1) throw new Exception("Adaptive compression plan exceeds conservative bounds.");
                checks.Add("dictionary and thread count follow memory limit; fallback is smaller and single threaded");
                bool rejected = false; try { ArchiveMemoryPlan.Choose(64, 8L * 1073741824, 0, 9, false); } catch (ArchiveMemoryException) { rejected = true; } if (!rejected) throw new Exception("Insufficient process budget not rejected.");
                rejected = false; try { ArchiveMemoryPlan.Choose(1024, 100L * 1048576, 0, 9, false); } catch (ArchiveMemoryException) { rejected = true; } if (!rejected) throw new Exception("Low available system memory not rejected."); checks.Add("insufficient process and system memory rejected before codec allocation");
                if (!ArchiveMemoryPlan.IsFailure(new IOException("native E_OUTOFMEMORY")) || !ArchiveMemoryPlan.IsFailure(new OutOfMemoryException()) || ArchiveMemoryPlan.IsFailure(new IOException("wrong password"))) throw new Exception("Memory error classification incorrect."); checks.Add("native and managed memory failures distinguished from password errors");
                ArchiveSettings.TestPath = Path.Combine(root, "settings.xml"); var settings = new ArchiveSettings { MaximumMemoryMiB = 256 }; settings.Save();
                string source = Path.Combine(root, "source.bin"), target = Path.Combine(root, "bounded.7z"); var block = new byte[1048576]; new Random(617).NextBytes(block); using (var stream = File.Create(source)) for (int i = 0; i < 16; i++) stream.Write(block, 0, block.Length);
                ArchiveEngine.Create(target, new[] { source }, settings, CancellationToken.None, null, new ArchivePackOptions { Level = 9 });
                if (!ArchiveEngine.Open(target).Items.Any(x => x.Bytes == 16L * 1048576)) throw new Exception("Bounded compression output invalid."); checks.Add("maximum compression completes under hard 256 MiB process limit and archive verifies");
                byte[] previous = File.ReadAllBytes(target); settings.MaximumMemoryMiB = 64; settings.Save(); rejected = false;
                try { ArchiveEngine.Create(target, new[] { source }, settings, CancellationToken.None, null, new ArchivePackOptions { Level = 9 }); } catch (ArchiveMemoryException) { rejected = true; }
                if (!rejected || !previous.SequenceEqual(File.ReadAllBytes(target)) || new FileInfo(source).Length != 16L * 1048576 || Directory.GetFiles(root, ".FileCompass-*").Length != 0) throw new Exception("Rejected compression changed original data or retained partial output."); checks.Add("memory refusal preserves source and existing archive and cleans partial output");
                File.WriteAllLines(Path.Combine(root, "memory-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllLines(Path.Combine(root, "memory-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
            finally { ArchiveSettings.TestPath = previousPath; }
        }
    }
}

