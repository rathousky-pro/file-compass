using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FileCompass
{
    public static class FileToolsTests
    {
        static void Wait(Task task) { while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); task.GetAwaiter().GetResult(); }
        public static int Run(string workspace)
        {
            Directory.CreateDirectory(workspace); SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            string root = Directory.CreateDirectory(Path.Combine(Path.GetFullPath(workspace), "file-tools-" + Guid.NewGuid().ToString("N"))).FullName;
            var passed = new List<string>(); Action<bool, string> check = delegate(bool ok, string name) { if (!ok) throw new Exception(name); passed.Add(name); };
            Action<Action, string> reject = delegate(Action work, string name) { bool failed = false; try { work(); } catch { failed = true; } check(failed, name); };
            try
            {
                string folder = Directory.CreateDirectory(Path.Combine(root, "Složka")).FullName;
                string sub = Directory.CreateDirectory(Path.Combine(folder, "Podsložka")).FullName;
                string a = Path.Combine(folder, "Alpha.txt"), b = Path.Combine(folder, "Beta.bin"), c = Path.Combine(sub, "Žluťoučký.txt");
                File.WriteAllText(a, "hledaný text"); File.WriteAllBytes(b, new byte[23]); File.WriteAllText(c, "jiný text");
                long bytes = new FileInfo(a).Length + new FileInfo(b).Length + new FileInfo(c).Length;
                check(FilePanel.LocalFolderBytes(folder) == bytes, "recursive local directory byte count");
                var results = new List<SearchHit>(); var options = new FileSearchOptions { Root = folder, Mask = "*.txt" };
                FileSearchEngine.Local(options, new string[0], CancellationToken.None, results.Add, delegate { }); check(results.Count == 2 && results.Any(x => x.Path == c), "recursive file masks preserve Czech paths");
                results.Clear(); options.Depth = 0; FileSearchEngine.Local(options, new string[0], CancellationToken.None, results.Add, delegate { }); check(results.Count == 1 && results[0].Path == a, "search depth limits current directory");
                results.Clear(); options.Depth = Int32.MaxValue; options.Text = "HLEDANÝ"; FileSearchEngine.Local(options, new string[0], CancellationToken.None, results.Add, delegate { }); check(results.Count == 1 && results[0].Path == a, "case-insensitive UTF8 text search");
                results.Clear(); options.Text = null; options.SelectedOnly = true; FileSearchEngine.Local(options, new[] { sub }, CancellationToken.None, results.Add, delegate { }); check(results.Count == 1 && results[0].Path == c, "search restricted to selected folder");
                check(FileSearchEngine.Matches("Alpha.txt", new FileSearchOptions { Mask = "^Alpha\\.txt$", Regex = true }), "regular expression filenames");
                using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); reject(delegate { FileSearchEngine.Local(new FileSearchOptions { Root = folder }, new string[0], cancel.Token, delegate { }, delegate { }); }, "search cancellation"); }
                var rows = BatchRenameEngine.Preview(new[] { a, b }, new RenameOptions { Name = "[C]_[N1-3]", Start = 7, Step = 2, Digits = 3 });
                check(rows[0].NewName == "007_Alp.txt" && rows[1].NewName == "009_Bet.bin", "rename counters, ranges and extensions preview");
                byte[] originalA = File.ReadAllBytes(a), originalB = File.ReadAllBytes(b);
                BatchRenameEngine.Apply(rows, CancellationToken.None); check(File.ReadAllBytes(rows[0].NewPath).SequenceEqual(originalA) && File.ReadAllBytes(rows[1].NewPath).SequenceEqual(originalB), "batch rename preserves exact file content");
                BatchRenameEngine.Apply(rows.Select(x => new RenameRow { Path = x.NewPath, OldName = x.NewName, NewName = x.OldName }).ToArray(), CancellationToken.None); check(File.Exists(a) && File.Exists(b), "undo restores original names");
                reject(delegate { BatchRenameEngine.Preview(new[] { a, b }, new RenameOptions { Name = "same", Extension = "dat" }); }, "duplicate targets rejected before changes");
                reject(delegate { BatchRenameEngine.Preview(new[] { a }, new RenameOptions { Name = "CON", Extension = "txt" }); }, "reserved Windows filename rejected");
                string occupied = Path.Combine(folder, "occupied.txt"); File.WriteAllText(occupied, "keep");
                reject(delegate { BatchRenameEngine.Preview(new[] { a }, new RenameOptions { Name = "occupied" }); }, "existing unrelated target not overwritten");
                rows = BatchRenameEngine.Preview(new[] { a, b }, new RenameOptions { Name = "prefix_[N]" });
                using (var locked = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read))
                    reject(delegate { BatchRenameEngine.Apply(rows, CancellationToken.None); }, "locked source aborts batch rename");
                check(File.Exists(a) && File.Exists(b) && !File.Exists(rows[0].NewPath), "failed batch rolls back already staged files");
                var swaps = new[] { new RenameRow { Path = a, OldName = "Alpha.txt", NewName = "Beta.bin" }, new RenameRow { Path = b, OldName = "Beta.bin", NewName = "Alpha.txt" } };
                BatchRenameEngine.Apply(swaps, CancellationToken.None); check(File.ReadAllBytes(a).SequenceEqual(originalB) && File.ReadAllBytes(b).SequenceEqual(originalA), "two-phase rename supports name swaps without overwrite");
                BatchRenameEngine.Apply(swaps.Select(x => new RenameRow { Path = x.NewPath, OldName = x.NewName, NewName = x.OldName }).ToArray(), CancellationToken.None);
                File.Delete(occupied);
                var panel = new FilePanel(delegate { }); panel.Navigate(root, false); NavigationTests.Wait(panel.NavigationTask); var entry = panel.List.Items.Cast<Entry>().Single(x => x.FullPath == folder);
                panel.ToggleMark(entry); Wait(panel.CalculateFolderSize(entry)); check(entry.IsMarked && entry.SizeKnown && entry.Bytes == bytes && entry.Size != "<DIR>", "folder size updates row and preserves red marking");
                var preview = new Window { Width = 1050, Height = 260, Content = panel }; NetworkTests.Render(preview, Path.Combine(workspace, "folder-size.png")); preview.Content = null; preview.Close();
                string archive = Path.Combine(root, "folders.zip"); ArchiveEngine.Create(archive, new[] { folder }, new ArchiveSettings(), CancellationToken.None);
                Wait(panel.OpenArchive(archive)); var archivedFolder = panel.List.Items.Cast<Entry>().Single(x => x.IsDirectory && !x.IsParent);
                Wait(panel.CalculateFolderSize(archivedFolder)); check(archivedFolder.SizeKnown && archivedFolder.Bytes == bytes, "folder size in archive uses file metadata without extraction"); panel.DisposeArchives();
                var renameDialog = new BatchRenameWindow(null, new[] { a, b }, delegate { }); NetworkTests.Render(renameDialog, Path.Combine(workspace, "batch-rename.png")); renameDialog.Close();
                var searchDialog = new FileSearchWindow(null, folder, true, true, delegate { return 0; }, delegate { }); NetworkTests.Render(searchDialog, Path.Combine(workspace, "file-search.png")); searchDialog.Close();
                var main = new MainWindow(root); main.VerifyToolbarThemeChanges(workspace); main.Close(); passed.Add("new rename icon fits closed toolbar tiles in all themes");
                string diffLeft = Path.Combine(folder, "DiffA.txt"), diffRight = Path.Combine(folder, "DiffB.txt");
                File.WriteAllText(diffLeft, "stejný řádek\npůvodní řádek\nodebraný řádek\nzávěr");
                File.WriteAllText(diffRight, "stejný řádek\nupravený řádek\npřidaný řádek\nzávěr");
                var diffResult = FileDiffEngine.Compare(diffLeft, diffRight, 0, System.Text.Encoding.UTF8, false, false, CancellationToken.None);
                check(!diffResult.Identical && !diffResult.BinaryMode && diffResult.Rows.Any(x => x.Kind == DiffLineKind.Equal) && diffResult.Rows.Any(x => x.IsDifferent) && (diffResult.ModifiedCount + diffResult.AddedCount + diffResult.RemovedCount) > 0, "file content diff detects line changes and difference blocks");
                File.WriteAllText(diffLeft, "Abc   Def   \nDruhý"); File.WriteAllText(diffRight, "abc   def\nDruhý");
                var ignoredDiff = FileDiffEngine.Compare(diffLeft, diffRight, 0, System.Text.Encoding.UTF8, true, true, CancellationToken.None);
                check(ignoredDiff.Identical && !ignoredDiff.Rows.Any(x => x.IsDifferent), "file diff respects ignore case and ignore trailing whitespace options");
                var binDiff = FileDiffEngine.Compare(a, b, 2, System.Text.Encoding.UTF8, false, false, CancellationToken.None);
                check(binDiff.BinaryMode && !binDiff.Identical && binDiff.DifferentByteBlocks > 0 && binDiff.Rows.Count > 0, "binary hex comparison detects byte differences");
                var hexPreview = PreviewData.Load(b, System.Text.Encoding.UTF8, CancellationToken.None, 0, PreviewMode.Hex);
                check(hexPreview.IsBinary && hexPreview.HexText.StartsWith("0000000000  00 00") && hexPreview.TotalLength == 23, "hex preview mode formats offsets and byte columns");
                var binPreview = PreviewData.Load(b, System.Text.Encoding.UTF8, CancellationToken.None, 5, PreviewMode.Binary);
                check(binPreview.Offset == 5 && binPreview.ReadBytes == 18 && binPreview.TotalLength == 23, "paged preview loads from arbitrary byte offset");
                byte[] parsedHex;
                check(PreviewData.TryParseHexPattern("hex: 68 6C 65 64", out parsedHex) && parsedHex.Length == 4 && PreviewData.FindInFile(a, parsedHex, 0, CancellationToken.None) == 0, "streaming file search locates hex/byte pattern in file");
                string shaFile = Path.Combine(folder, "soubory.sha256"), sfvFile = Path.Combine(folder, "soubory.sfv");
                var createdSha = FileChecksums.CreateManifests(new[] { a, b, c }, folder, ChecksumAlgorithm.Sha256, shaFile, false, CancellationToken.None, null);
                var verifiedSha = FileChecksums.VerifyManifests(new[] { shaFile }, CancellationToken.None, null, null);
                check(createdSha.Length == 1 && verifiedSha.Count == 3 && verifiedSha.All(x => x.IsOk), "SHA-256 manifest creation and verification succeed for nested files");
                FileChecksums.CreateManifests(new[] { a, b }, folder, ChecksumAlgorithm.Crc32, sfvFile, false, CancellationToken.None, null);
                check(FileChecksums.VerifyManifests(new[] { sfvFile }, CancellationToken.None, null, null).All(x => x.IsOk), "SFV CRC32 manifest creation and verification succeed");
                File.WriteAllText(a, "změněný obsah po podpisu");
                check(!FileChecksums.VerifyManifests(new[] { shaFile }, CancellationToken.None, null, null).Single(x => x.RelativePath == "Alpha.txt").IsOk, "checksum verification detects modified file content");
                File.WriteAllText(a, "hledaný text");
                string traversalManifest = Path.Combine(sub, "unsafe.sha256");
                File.WriteAllText(traversalManifest, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *..\\Alpha.txt\r\n");
                check(!FileChecksums.VerifyManifests(new[] { traversalManifest }, CancellationToken.None, null, null).Single().IsOk, "checksum verifier blocks parent directory traversal");
                panel.ShowProjection(new ProjectionSpec { Kind = "Search", Root = folder, Query = "*.txt", Paths = new[] { a, c } });
                NavigationTests.Wait(panel.NavigationTask);
                check(panel.IsProjection && panel.Projection.Kind == "Search" && panel.List.Items.Cast<Entry>().Count(x => !x.IsParent) == 2 && panel.List.Items.Cast<Entry>().Any(x => x.FullPath == c && x.LocationName.Contains("Podsložka")), "search feed to panel displays virtual search results with relative paths");
                File.WriteAllLines(Path.Combine(workspace, "file-tools-test-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "file-tools-test-result.txt"), "FAIL after " + passed.Count + " checks:\n" + String.Join("\n", passed) + "\n" + ex); return 1; }
            finally { if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(workspace).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe cleanup."); Directory.Delete(root, true); }
        }
    }
}

