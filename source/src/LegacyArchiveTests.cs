using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
namespace CommanderCZ
{
    internal static class LegacyArchiveTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            Action<bool,string> check = delegate(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); };
            try {
                string tools = Path.GetFullPath(Path.Combine(root, "..", "tools"));
                var settings = new ArchiveSettings { WinRarPath = ArchiveSettings.FindWinRar() ?? "", ArjPath = Path.Combine(tools, "arj/files/ARJ32.EXE"), LhaPath = Path.Combine(tools, "lha/bin/lha.exe") }; settings.Save();
                check(ArchiveSettings.Load().ArjPath == settings.ArjPath && ArchiveSettings.Load().LhaPath == settings.LhaPath, "external paths survive saving");
                string input = Directory.CreateDirectory(Path.Combine(root, "input")).FullName;
                Directory.CreateDirectory(Path.Combine(input, "empty"));
                File.WriteAllText(Path.Combine(input, "test.txt"), "Exact payload 123.");
                Directory.CreateDirectory(Path.Combine(input, "nested")); File.WriteAllText(Path.Combine(input, "nested/second.txt"), "nested payload");
                foreach (string extension in new[] { "arj", "lzh" }) {
                    string target = Path.Combine(root, "test." + extension);
                    ArchiveEngine.Create(target, new[] { input }, settings, CancellationToken.None);
                    var doc = ArchiveEngine.Open(target); ArchiveEngine.Test(doc, CancellationToken.None);
                    check(doc.Items.Any(x => x.Path == "input/nested/second.txt"), extension + " real executable preserves nested paths and contents");
                    check(!ArchiveEngine.Writable(doc), extension + " existing archive stays read-only");
                    var missing = new ArchiveSettings(); bool rejected = false;
                    try { ArchiveEngine.Create(target, new[] { input }, missing, CancellationToken.None); } catch (IOException) { rejected = true; }
                    check(rejected && File.Exists(target), extension + " missing executable leaves existing archive intact");
                }
                string unicode = Directory.CreateDirectory(Path.Combine(root, "unicode")).FullName;
                File.WriteAllText(Path.Combine(unicode, "český soubor.txt"), "Czech names must be preserved exactly");
                foreach (string extension in new[] { "arj", "lzh" }) {
                    string target = Path.Combine(root, "unicode." + extension); File.WriteAllText(target, "existing destination");
                    try { ArchiveEngine.Create(target, new[] { unicode }, settings, CancellationToken.None); check(ArchiveEngine.Open(target).Items.Any(x => x.Path == "unicode/český soubor.txt"), extension + " preserves Czech names"); }
                    catch (IOException ex) { check(File.ReadAllText(target) == "existing destination", extension + " safely refuses incompatible Czech names and preserves destination: " + ex.Message); }
                }
                foreach (string extension in String.IsNullOrEmpty(settings.WinRarPath) ? new[] { "zip", "7z", "tar", "gz" } : new[] { "zip", "rar", "7z", "tar", "gz" }) {
                    string target = Path.Combine(root, "normal." + extension);
                    ArchiveEngine.Create(target, new[] { Path.Combine(input, "test.txt") }, settings, CancellationToken.None);
                    var normal = ArchiveEngine.Open(target); ArchiveEngine.Test(normal, CancellationToken.None);
                    using (var extracted = new ArchiveTemp()) { ArchiveEngine.Extract(normal, normal.Items.Select(x => x.Path).ToArray(), extracted.Path, CancellationToken.None); check(File.ReadAllText(Directory.GetFiles(extracted.Path, "*", SearchOption.AllDirectories).Single()) == "Exact payload 123.", extension + " standard menu format roundtrip"); }
                }
                var dialog = new ArchivePackDialog(null, 1, Path.Combine(root, "test.zip"));
                var formats = FluentUiTests.Children<RadioButton>(dialog).Select(x => (string)x.Content).ToArray();
                check(formats.SequenceEqual(new[] { "ZIP", "RAR*", "7-ZIP", "TAR", "GZ", "ARJ*", "LHA*" }), "creation menu contains only requested and legacy formats");
                NetworkTests.Render(dialog, Path.Combine(root, "pack.png")); dialog.Close();
                File.WriteAllLines(Path.Combine(root, "legacy-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "legacy-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
    }
}

