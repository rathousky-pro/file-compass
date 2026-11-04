using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FileCompass
{
    public static class ArchiveTests
    {
        static void Wait(Task task)
        { while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); task.GetAwaiter().GetResult(); }
        public static int Run(string workspace)
        {
            Directory.CreateDirectory(workspace);
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var passed = new List<string>(); var skipped = new List<string>();
            Action<bool, string> check = delegate(bool ok, string name) { if (!ok) throw new Exception(name); passed.Add(name); };
            Action<Action, string> reject = delegate(Action action, string name) { bool failed = false; try { action(); } catch (Exception) { failed = true; } check(failed, name); };
            string root = Path.Combine(Path.GetFullPath(workspace), "archive-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var settings = new ArchiveSettings { WinRarPath = ArchiveSettings.FindWinRar() ?? "" };
                string config = Path.Combine(root, "settings.xml"); settings.Save(config);
                check(ArchiveSettings.Load(config).WinRarPath == settings.WinRarPath, "WinRAR path survives config restart");
                string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
                string folder = Directory.CreateDirectory(Path.Combine(source, "složka")).FullName;
                Directory.CreateDirectory(Path.Combine(folder, "prázdná"));
                string nested = Path.Combine(folder, "český [1].txt"); File.WriteAllText(nested, "původní obsah");
                string neighbour = Path.Combine(source, "soused.txt"); File.WriteAllText(neighbour, "zachovat");
                string secondPanel = Directory.CreateDirectory(Path.Combine(root, "second-panel")).FullName;
                check(Path.GetDirectoryName(MainWindow.DefaultArchiveTarget(new[] { folder, neighbour }, source, secondPanel)) == secondPanel, "compression defaults to destination panel rather than source panel");
                string childDir = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName; File.WriteAllText(Path.Combine(childDir, "nested.txt"), "deep");
                string shallow = Path.Combine(secondPanel, "shallow.zip");
                ArchiveEngine.Create(shallow, new[] { folder }, settings, CancellationToken.None, null, new ArchivePackOptions { Recursive = false, Level = 0 });
                var shallowDoc = ArchiveEngine.Open(shallow);
                check(shallowDoc.Items.Any(x => x.Path.EndsWith("český [1].txt")) && !shallowDoc.Items.Any(x => x.Path.Contains("child")), "disabled recursion packs immediate files only at compression level zero");
                string flat = Path.Combine(secondPanel, "flat.7z");
                ArchiveEngine.Create(flat, new[] { folder }, settings, CancellationToken.None, null, new ArchivePackOptions { Paths = false, Level = 9 });
                check(ArchiveEngine.Open(flat).Items.All(x => !x.Path.Contains('/')) && ArchiveEngine.Open(flat).Items.Any(x => x.Path == "nested.txt"), "disabled paths flattens recursively into archive root at level nine");
                string noBase = Path.Combine(secondPanel, "no-base.zip");
                ArchiveEngine.Create(noBase, new[] { folder }, settings, CancellationToken.None, null, new ArchivePackOptions { ExcludeBase = true });
                check(ArchiveEngine.Open(noBase).Items.Any(x => x.Path == "child/nested.txt") && !ArchiveEngine.Open(noBase).Items.Any(x => x.Path.StartsWith("složka/")), "separate folder archive can omit its base directory");
                foreach (string protectedFormat in new[] { "zip", "7z", "rar" })
                {
                    string protectedFile = Path.Combine(secondPanel, "protected." + protectedFormat);
                    if (protectedFormat == "rar") { reject(delegate { ArchiveEngine.Create(protectedFile, new[] { neighbour }, settings, CancellationToken.None, null, new ArchivePackOptions { Password = "test-password" }); }, "encrypted RAR creation is refused without leaking password to a process"); continue; }
                    ArchiveEngine.Create(protectedFile, new[] { neighbour }, settings, CancellationToken.None, null, new ArchivePackOptions { Password = "test-password", Level = 6 });
                    var protectedDoc = ArchiveEngine.Open(protectedFile, "test-password"); ArchiveEngine.Test(protectedDoc, CancellationToken.None);
                    check(protectedDoc.Items.Any(x => x.Encrypted), protectedFormat + " new compression dialog password produces verified encrypted payload");
                    protectedDoc.Password = "incorrect";
                    reject(delegate { ArchiveEngine.Test(protectedDoc, CancellationToken.None); }, protectedFormat + " compression password rejects incorrect password");
                }
                File.WriteAllText(Path.Combine(childDir, "český [1].txt"), "duplicate");
                reject(delegate { ArchiveEngine.Create(Path.Combine(secondPanel, "collision.zip"), new[] { folder }, settings, CancellationToken.None, null, new ArchivePackOptions { Paths = false }); }, "flattened duplicate filenames are refused");
                Directory.Delete(childDir, true);
                var packDialog = new ArchivePackDialog(null, 9, MainWindow.DefaultArchiveTarget(new[] { folder, neighbour }, source, secondPanel));
                NetworkTests.Render(packDialog, Path.Combine(workspace, "archive-pack.png")); packDialog.Close();
                foreach (string format in new[] { "zip", "7z", "rar" })
                {
                    if (format == "rar" && settings.WinRarPath.Length == 0) { skipped.Add("SKIP: RAR creation/edit integration requires an optional local WinRAR installation; ZIP/7z and RAR password refusal remain tested."); continue; }
                    string file = Path.Combine(root, "Archiv." + format);
                    ArchiveEngine.Create(file, new[] { folder, neighbour }, settings, CancellationToken.None);
                    var document = ArchiveEngine.Open(file);
                    ArchiveEngine.Test(document, CancellationToken.None);
                    passed.Add(format + " full integrity test succeeds");
                    check(document.Items.Any(x => x.Path == "složka/český [1].txt") && document.Items.Any(x => x.Directory && x.Path == "složka/prázdná"), format + " creation preserves Czech names and empty directories");
                    string output = Directory.CreateDirectory(Path.Combine(root, "extract-" + format)).FullName;
                    ArchiveEngine.Extract(document, new[] { "složka" }, output, CancellationToken.None);
                    check(File.ReadAllText(Path.Combine(output, "složka", "český [1].txt")) == "původní obsah" && !File.Exists(Path.Combine(output, "soused.txt")), format + " extracts only selected directory with exact content");
                    File.WriteAllText(nested, "změněný obsah");
                    ArchiveEngine.Add(document, "", new[] { folder }, false, settings, CancellationToken.None);
                    using (var temp = new ArchiveTemp())
                    { ArchiveEngine.Extract(document, new[] { "složka/český [1].txt" }, temp.Path, CancellationToken.None); check(File.ReadAllText(ArchiveEngine.DiskPath(temp.Path, "složka/český [1].txt")) == "původní obsah", format + " skip preserves existing content"); }
                    ArchiveEngine.Add(document, "", new[] { folder }, true, settings, CancellationToken.None);
                    using (var temp = new ArchiveTemp())
                    { ArchiveEngine.Extract(document, new[] { "složka/český [1].txt" }, temp.Path, CancellationToken.None); check(File.ReadAllText(ArchiveEngine.DiskPath(temp.Path, "složka/český [1].txt")) == "změněný obsah", format + " overwrite updates existing member"); }
                    ArchiveEngine.ReplaceMember(document, "složka/český [1].txt", neighbour, settings, CancellationToken.None);
                    using (var temp = new ArchiveTemp())
                    { ArchiveEngine.Extract(document, new[] { "složka/český [1].txt" }, temp.Path, CancellationToken.None); check(File.ReadAllText(ArchiveEngine.DiskPath(temp.Path, "složka/český [1].txt")) == "zachovat", format + " edited file saved to exact internal path"); }
                    ArchiveEngine.Rename(document, "složka", "přejmenováno", settings, CancellationToken.None);
                    check(document.Items.Any(x => x.Path == "přejmenováno/český [1].txt") && !document.Items.Any(x => x.Path.StartsWith("složka/")), format + " directory renamed in single transaction");
                    ArchiveEngine.Remove(document, new[] { "přejmenováno/český [1].txt" }, settings, CancellationToken.None);
                    check(!document.Items.Any(x => x.Path == "přejmenováno/český [1].txt") && document.Items.Any(x => x.Path == "soused.txt"), format + " delete selects literal brackets and preserves neighbour");
                    byte[] original = File.ReadAllBytes(file);
                    using (var cancellation = new CancellationTokenSource())
                    {
                        bool cancelled = false;
                        try { ArchiveEngine.Add(document, "new", new[] { neighbour }, true, settings, cancellation.Token, delegate { cancellation.Cancel(); }); }
                        catch (OperationCanceledException) { cancelled = true; }
                        check(cancelled && original.SequenceEqual(File.ReadAllBytes(file)), format + " cancellation during archiver preserves original bytes");
                    }
                    File.WriteAllText(nested, "původní obsah");
                    ArchiveEngine.Remove(document, document.Items.Select(x => x.Path).ToArray(), settings, CancellationToken.None);
                    check(File.Exists(file) && ArchiveEngine.Open(file).Items.Count == 0, format + " deleting all items retains valid empty container");
                    ArchiveEngine.Add(document, "", new[] { neighbour }, false, settings, CancellationToken.None);
                    check(ArchiveEngine.Open(file).Items.Any(x => x.Path == "soused.txt"), format + " empty archive accepts new content");
                }
                string innerFile = Path.Combine(root, "inner.7z"); ArchiveEngine.Create(innerFile, new[] { neighbour }, settings, CancellationToken.None);
                string outerFile = Path.Combine(root, "outer.zip"); ArchiveEngine.Create(outerFile, new[] { innerFile }, settings, CancellationToken.None);
                var outer = ArchiveEngine.Open(outerFile);
                var stale = ArchiveEngine.Open(outerFile);
                ArchiveEngine.Add(outer, "added", new[] { neighbour }, false, settings, CancellationToken.None);
                byte[] freshOuter = File.ReadAllBytes(outerFile);
                reject(delegate { ArchiveEngine.Add(stale, "", new[] { neighbour }, true, settings, CancellationToken.None); }, "stale archive refuses write after external change");
                check(freshOuter.SequenceEqual(File.ReadAllBytes(outerFile)), "stale write preserves updated archive bytes");
                using (var temp = new ArchiveTemp())
                {
                    ArchiveEngine.Extract(outer, new[] { "inner.7z" }, temp.Path, CancellationToken.None);
                    var inner = ArchiveEngine.Open(Path.Combine(temp.Path, "inner.7z")); inner.Parent = outer; inner.ParentMember = "inner.7z";
                    File.WriteAllText(neighbour, "nested edit"); ArchiveEngine.ReplaceMember(inner, "soused.txt", neighbour, settings, CancellationToken.None);
                    using (var verify = new ArchiveTemp())
                    {
                        ArchiveEngine.Extract(ArchiveEngine.Open(outerFile), new[] { "inner.7z" }, verify.Path, CancellationToken.None);
                        var actualInner = ArchiveEngine.Open(Path.Combine(verify.Path, "inner.7z"));
                        using (var content = new ArchiveTemp())
                        { ArchiveEngine.Extract(actualInner, new[] { "soused.txt" }, content.Path, CancellationToken.None); check(File.ReadAllText(Path.Combine(content.Path, "soused.txt")) == "nested edit", "nested 7z edit propagates into outer zip"); }
                    }
                }
                string malicious = Path.Combine(root, "traversal.zip");
                string corrupt = Path.Combine(root, "corrupt.zip");
                using (var stream = File.Create(corrupt)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("payload.txt", CompressionLevel.NoCompression).Open())) writer.Write("UNIQUE-CHECKSUM-PAYLOAD");
                byte[] corruptBytes = File.ReadAllBytes(corrupt);
                byte[] marker = System.Text.Encoding.ASCII.GetBytes("UNIQUE-CHECKSUM-PAYLOAD");
                int payloadIndex = Enumerable.Range(0, corruptBytes.Length - marker.Length + 1).First(i => corruptBytes.Skip(i).Take(marker.Length).SequenceEqual(marker));
                corruptBytes[payloadIndex] ^= 1; File.WriteAllBytes(corrupt, corruptBytes);
                var corruptDoc = ArchiveEngine.Open(corrupt);
                reject(delegate { ArchiveEngine.Test(corruptDoc, CancellationToken.None); }, "integrity test detects corrupt payload despite readable directory");
                using (var cancelledTest = new CancellationTokenSource())
                { cancelledTest.Cancel(); reject(delegate { ArchiveEngine.Test(corruptDoc, cancelledTest.Token); }, "integrity test respects cancellation"); }
                using (var stream = File.Create(malicious)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create)) using (var writer = new StreamWriter(zip.CreateEntry("../escape.txt").Open())) writer.Write("unsafe");
                reject(delegate { ArchiveEngine.Open(malicious); }, "traversal path rejected before extraction");
                string linkArchive = Path.Combine(root, "symlink.zip");
                using (var stream = File.Create(linkArchive)) using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                { var entry = zip.CreateEntry("link"); entry.ExternalAttributes = unchecked((int)0xA1FF0000); using (var writer = new StreamWriter(entry.Open())) writer.Write("../../outside"); }
                byte[] linkBytes = File.ReadAllBytes(linkArchive);
                for (int i = 0; i < linkBytes.Length - 6; i++)
                    if (linkBytes[i] == 0x50 && linkBytes[i + 1] == 0x4b && linkBytes[i + 2] == 1 && linkBytes[i + 3] == 2) linkBytes[i + 5] = 3; // Unix creator, matching symlink attributes.
                File.WriteAllBytes(linkArchive, linkBytes);
                reject(delegate { ArchiveEngine.Open(linkArchive); }, "archive symbolic link rejected before extraction");
                foreach (string bad in new[] { "/absolute", "C:/absolute", "dir/../escape", "file:stream", "CON.txt", "newline\nname" }) reject(delegate { ArchiveEngine.SafeMember(bad); }, "reject unsafe member " + bad.Replace("\n", "\\n"));
                string encrypted = Path.Combine(root, "encrypted.7z");
                ArchiveEngine.Run(ArchiveEngine.SevenZipPath, new[] { "a", "-psecret", "-mhe=on", encrypted, neighbour }, root, CancellationToken.None);
                reject(delegate { ArchiveEngine.Open(encrypted); }, "encrypted header requires password");
                check(ArchiveEngine.Open(encrypted, "secret").Items.Any(x => x.Path == "soused.txt"), "encrypted archive opens with password");
                var encryptedDoc = ArchiveEngine.Open(encrypted, "secret");
                ArchiveEngine.Test(encryptedDoc, CancellationToken.None); passed.Add("encrypted integrity test succeeds with password");
                encryptedDoc.Password = "wrong";
                reject(delegate { ArchiveEngine.Test(encryptedDoc, CancellationToken.None); }, "integrity test rejects wrong password");
                check(encryptedDoc.Password == null, "wrong integrity-test password is cleared"); encryptedDoc.Password = "secret";
                ArchiveEngine.Add(encryptedDoc, "secret-dir", new[] { neighbour }, true, settings, CancellationToken.None);
                check(ArchiveEngine.Open(encrypted, "secret").Items.Any(x => x.Path == "secret-dir/soused.txt"), "encrypted archive accepts update and keeps password");
                var panel = new FilePanel(delegate { }); panel.Navigate(root, false); NavigationTests.Wait(panel.NavigationTask); Wait(panel.OpenArchive(outerFile));
                check(panel.IsArchive && panel.List.Items.Cast<Entry>().Any(x => x.FullPath == "/inner.7z"), "archive enters panel without extracting content");
                bool? viewOnlyRequest = null;
                Action<FilePanel, Entry, bool, bool> requested = delegate(FilePanel p, Entry item, bool edit, bool viewOnly) { viewOnlyRequest = viewOnly; };
                panel.ArchiveFileRequested += requested;
                panel.List.SelectedItem = panel.List.Items.Cast<Entry>().First(x => !x.IsDirectory && !x.IsParent);
                panel.ViewSelected(); check(viewOnlyRequest == true, "archive F3 requests safe viewing only");
                panel.OpenSelected(); check(viewOnlyRequest == false, "archive Enter explicitly requests opening, separately from F3");
                panel.ArchiveFileRequested -= requested;
                panel.NewTab(); panel.Navigate(root, false); NavigationTests.Wait(panel.NavigationTask);
                panel.SwitchTab(0); Wait(panel.TabNavigationTask);
                check(panel.IsArchive && panel.Archive.FilePath == outerFile && panel.List.Items.Cast<Entry>().Any(x => x.FullPath == "/inner.7z"), "archive tab restores document and listing after local navigation");
                check(panel.SaveTabs()[0].ArchiveFile == outerFile && panel.SaveTabs()[1].Path == root, "saved archive and local tabs preserve distinct locations");
                panel.CloseTab(1); check(panel.IsArchive && panel.TabCount == 1, "closing local tab leaves archive active");
                string openedPath = panel.DisplayPath;
                var preview = new Window { Width = 800, Content = panel }; NetworkTests.Render(preview, Path.Combine(workspace, "archive-panel.png")); preview.Content = null; preview.Close();
                panel.Up(); NavigationTests.Wait(panel.NavigationTask); check(!panel.IsArchive && panel.CurrentPath == root, "up from archive returns to physical directory");
                var archiveSettings = new ArchiveSettingsDialog(null); NetworkTests.Render(archiveSettings, Path.Combine(workspace, "archive-settings.png")); archiveSettings.Close();
                var extraction = new ArchiveExtractDialog(null, new[] { outerFile }, root);
                NetworkTests.Render(extraction, Path.Combine(workspace, "archive-extract.png")); extraction.Close();
                panel.DisposeArchives();
                var archiveCopy = new ArchiveCopyDialog(null, 2, source, outerFile + "\\", true);
                NetworkTests.Render(archiveCopy, Path.Combine(workspace, "archive-copy-options.png")); archiveCopy.Close();
                var main = new MainWindow(root); main.Show(); main.Hide(); NavigationTests.WaitPanels(main); Wait(main.VerifyArchiveBackground(root)); main.VerifyArchiveCopyPresentation(workspace); var closed = new TaskCompletionSource<bool>(); main.Closed += delegate { closed.TrySetResult(true); }; main.Close(); Wait(closed.Task);
                passed.Add("archive copy filtering and verification, single summary, modeless details and overlaid progress colours");
                passed.Add("real archive background compression reports progress while panels navigate and swap");
                File.WriteAllLines(Path.Combine(workspace, "archive-test-result.txt"), passed.Concat(skipped).Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "archive-test-result.txt"), "FAIL after " + passed.Count + " checks:\n" + String.Join("\n", passed) + "\n" + ex); return 1; }
            finally
            {
                string expected = Path.GetFullPath(workspace).TrimEnd('\\') + "\\";
                if (!Path.GetFullPath(root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe test cleanup");
                Directory.Delete(root, true);
            }
        }
    }
}

