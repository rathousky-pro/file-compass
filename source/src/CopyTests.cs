using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public static class CopyTests
    {
        static void Check(bool value, string label) { if (!value) throw new Exception(label); }
        static CopyResult Run(string[] sources, CopyOptions options, Func<CopyConflict, CopyDecision> decide = null, Action<CopyUpdate> update = null)
        {
            using (var control = new CopyControl())
                return new CopyEngine(options, control, decide ?? (c => new CopyDecision(CopyChoice.Overwrite)), update).Run(sources);
        }
        public static int Execute(string workspace)
        {
            string root = Path.Combine(Path.GetFullPath(workspace), "copy-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var passed = new List<string>();
            Action<bool, string> assert = delegate(bool value, string label) { Check(value, label); passed.Add(label); };
            try
            {
                string input = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
                string output = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
                string a = Path.Combine(input, "český.txt"), b = Path.Combine(input, "druhý.txt"), da = Path.Combine(output, "český.txt"), db = Path.Combine(output, "druhý.txt");
                string scanSource = Directory.CreateDirectory(Path.Combine(root, "scan-source")).FullName;
                for (int i = 0; i < 16; i++) File.WriteAllText(Path.Combine(scanSource, i + ".txt"), "data");
                using (var scanControl = new CopyControl())
                {
                    var scanUpdates = new List<CopyUpdate>();
                    var scanResult = new CopyEngine(new CopyOptions { Target = Path.Combine(root, "scan-cancelled") }, scanControl, null, delegate(CopyUpdate u) {
                        scanUpdates.Add(u);
                        if (u.Phase == "Načítání souborů…" && u.TotalFiles > 0) scanControl.Cancel();
                        // Ensure a periodic update is due while enumerating this small fixture.
                        if (scanUpdates.Count == 1) System.Threading.Thread.Sleep(90);
                    }).Run(new[] { scanSource });
                    assert(scanResult.Cancelled && scanUpdates.Any(x => x.TotalFiles > 0 && x.TotalFiles < 16) && !Directory.Exists(Path.Combine(root, "scan-cancelled")), "folder inventory reports discovered files and can be cancelled before copying");
                }
                using (var scanControl = new CopyControl())
                {
                    var engine = new CopyEngine(new CopyOptions { Target = Path.Combine(root, "scan-completed"), Verify = true }, scanControl, null, null);
                    int planned = 0; engine.PlanReady = delegate(List<DurableFile> plan) { planned = plan.Count(x => !x.Directory); };
                    var scanResult = engine.Run(new[] { scanSource });
                    assert(scanResult.Copied == 16 && scanResult.Errors.Count == 0 && planned == 16 && File.ReadAllText(Path.Combine(root, "scan-completed", "scan-source", "0.txt")) == "data", "folder inventory reaches durable plan and copies nested files");
                }
                File.WriteAllText(a, "NEW"); File.WriteAllText(b, "TWO"); File.WriteAllText(da, "OLD"); File.WriteAllText(db, "OLD2");
                int asked = 0;
                var result = Run(new[] { a, b }, new CopyOptions { Target = output, Verify = true }, c => { asked++; return new CopyDecision(CopyChoice.OverwriteAll); });
                assert(result.Copied == 2 && result.Errors.Count == 0 && asked == 1 && File.ReadAllText(da) == "NEW" && File.ReadAllText(db) == "TWO", "overwrite-all + SHA256 + single prompt");
                File.WriteAllText(da, "KEEP"); asked = 0;
                result = Run(new[] { a, b }, new CopyOptions { Target = output }, c => { asked++; return new CopyDecision(CopyChoice.SkipAll); });
                assert(result.Skipped == 2 && asked == 1 && File.ReadAllText(da) == "KEEP", "skip-all leaves originals");
                DateTime now = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(a, now); File.SetLastWriteTimeUtc(da, now.AddHours(-1));
                File.SetLastWriteTimeUtc(b, now); File.SetLastWriteTimeUtc(db, now.AddHours(1));
                result = Run(new[] { a, b }, new CopyOptions { Target = output, Rule = CopyRule.Older });
                assert(result.Copied == 1 && result.Skipped == 1 && File.GetLastWriteTimeUtc(da) == now, "older targets only; newer skipped; timestamp retained");
                File.WriteAllText(da, "SAME TIME"); File.SetLastWriteTimeUtc(da, now);
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.Older });
                assert(result.Skipped == 1 && File.ReadAllText(da) == "SAME TIME", "equal timestamps are not older");
                result = Run(new[] { a }, new CopyOptions { Target = output }, c => new CopyDecision(CopyChoice.Rename, "nové jméno.txt"));
                assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "nové jméno.txt")) == "NEW" && File.ReadAllText(da) == "SAME TIME", "manual rename preserves old target");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.RenameAll });
                assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "český (2).txt")) == "NEW", "automatic collision rename");
                File.WriteAllText(da, "PREFIX:");
                result = Run(new[] { a }, new CopyOptions { Target = output, Verify = true }, c => new CopyDecision(CopyChoice.Append));
                assert(result.Copied == 1 && result.Errors.Count == 0 && File.ReadAllText(da) == "PREFIX:NEW", "append + verified staged content");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.Larger });
                assert(result.Skipped == 1, "larger-source condition");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.Smaller });
                assert(result.Copied == 1 && File.ReadAllText(da) == "NEW", "smaller-source condition");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.Different });
                assert(result.Skipped == 1, "different by size/time skips matching metadata");
                result = Run(new[] { a }, new CopyOptions { Target = Path.Combine(output, "explicit.bin"), TargetIsFile = true, Verify = true });
                assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "explicit.bin")) == "NEW", "single-file destination name");
                File.SetAttributes(da, FileAttributes.ReadOnly);
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll });
                assert(result.Errors.Count == 1 && (File.GetAttributes(da) & FileAttributes.ReadOnly) != 0, "read-only target protected by default");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll, ReadOnly = true });
                assert(result.Copied == 1 && result.Errors.Count == 0 && File.ReadAllText(da) == "NEW", "explicit read-only overwrite");
                result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll, Permissions = true });
                assert(result.Copied == 1 && result.Errors.Count == 0 && File.GetAccessControl(a, AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access) == File.GetAccessControl(da, AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access), "NTFS access permissions");
                string nested = Directory.CreateDirectory(Path.Combine(input, "folder")).FullName;
                File.WriteAllText(Path.Combine(nested, "keep.txt"), "MATCH"); File.WriteAllText(Path.Combine(nested, "skip.tmp"), "SKIP");
                result = Run(new[] { nested }, new CopyOptions { Target = output, Filter = "*.* | *.tmp" });
                assert(result.Copied == 1 && File.Exists(Path.Combine(output, "folder", "keep.txt")) && !File.Exists(Path.Combine(output, "folder", "skip.tmp")), "recursive filter exclusions");
                File.WriteAllText(Path.Combine(nested, "keep.txt"), "MERGED");
                result = Run(new[] { nested }, new CopyOptions { Target = output, Filter = "*.txt", Rule = CopyRule.OverwriteAll });
                assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "folder", "keep.txt")) == "MERGED", "merge existing directories");
                string mismatch = Directory.CreateDirectory(Path.Combine(input, "mismatch")).FullName;
                File.WriteAllText(Path.Combine(mismatch, "child.txt"), "CHILD"); File.WriteAllText(Path.Combine(output, "mismatch"), "FILE");
                result = Run(new[] { mismatch }, new CopyOptions { Target = output }, c => new CopyDecision(CopyChoice.Rename, "renamed-folder"));
                assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "mismatch")) == "FILE" && File.Exists(Path.Combine(output, "renamed-folder", "child.txt")), "file-directory collision renamed safely");
                result = Run(new[] { a }, new CopyOptions { Target = input, Rule = CopyRule.OverwriteAll });
                assert(result.Errors.Count == 1 && File.ReadAllText(a) == "NEW", "same file refused");
                result = Run(new[] { nested }, new CopyOptions { Target = Path.Combine(nested, "inside") });
                assert(result.Errors.Count == 1 && !Directory.Exists(Path.Combine(nested, "inside")), "recursive self-copy refused before creation");
                using (var locked = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    result = Run(new[] { a }, new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll });
                assert(result.Errors.Count == 1 && File.ReadAllText(da) == "NEW", "locked source preserves original target");
                using (var control = new CopyControl())
                using (var locked = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    int prompts = 0;
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), null,
                        failure => { prompts++; Check(failure.Source == a && (failure.Error.HResult & 65535) == 32, "sharing violation source"); return CopyErrorChoice.Skip; }).Run(new[] { a });
                    assert(prompts == 1 && result.Skipped == 1 && result.Errors.Count == 0 && File.ReadAllText(da) == "NEW", "locked file skip preserves target");
                }
                using (var control = new CopyControl())
                using (var locked = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    int prompts = 0;
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), null,
                        failure => { prompts++; locked.Dispose(); return CopyErrorChoice.Retry; }).Run(new[] { a });
                    assert(prompts == 1 && result.Copied == 1 && result.Skipped == 0 && result.Errors.Count == 0 && File.ReadAllText(da) == "NEW" && !Directory.GetFiles(output, ".commandercz-*").Any(), "retry succeeds after lock release without partial files");
                }
                string lockedSecond = Path.Combine(input, "locked-second.txt"), good = Path.Combine(input, "good-after-lock.txt");
                string changing = Path.Combine(input, "retry-changing.txt"); File.WriteAllText(changing, "OLD");
                using (var control = new CopyControl())
                using (var locked = new FileStream(changing, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    result = new CopyEngine(new CopyOptions { Target = output }, control, c => new CopyDecision(CopyChoice.Overwrite), null,
                        failure => { locked.Dispose(); File.WriteAllText(changing, "NEW LONGER CONTENT"); return CopyErrorChoice.Retry; }).Run(new[] { changing });
                    assert(result.Copied == 1 && File.ReadAllText(Path.Combine(output, "retry-changing.txt")) == "NEW LONGER CONTENT", "retry refreshes size when source changed while locked");
                }
                File.WriteAllText(lockedSecond, "SECOND"); File.WriteAllText(good, "GOOD");
                using (var control = new CopyControl())
                using (var locked = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                using (var otherLocked = new FileStream(lockedSecond, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    int prompts = 0;
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), null,
                        failure => { prompts++; return CopyErrorChoice.SkipAll; }).Run(new[] { a, lockedSecond, good });
                    assert(prompts == 1 && result.Skipped == 2 && result.Copied == 1 && File.ReadAllText(Path.Combine(output, "good-after-lock.txt")) == "GOOD", "skip all errors keeps copying unlocked files");
                }
                using (var control = new CopyControl())
                using (var locked = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), null,
                        failure => CopyErrorChoice.Cancel).Run(new[] { a, good });
                    assert(result.Cancelled && result.Copied == 0 && File.ReadAllText(da) == "NEW", "error dialog cancel stops task and preserves original target");
                }
                string large = Path.Combine(input, "large.bin"), largeTarget = Path.Combine(output, "large.bin");
                File.WriteAllBytes(large, new byte[8 * 1024 * 1024]); File.WriteAllText(largeTarget, "ORIGINAL");
                CopyUpdate sample = null;
                using (var control = new CopyControl())
                {
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), u =>
                    { if (u.FileDone > 0 && u.Phase == "Kopírování") { sample = u; control.Cancel(); } }).Run(new[] { large });
                }
                assert(result.Cancelled && File.ReadAllText(largeTarget) == "ORIGINAL" && !Directory.GetFiles(output, ".commandercz-*").Any(), "cancel mid-overwrite preserves target and cleans temp");
                using (var control = new CopyControl())
                {
                    result = new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), u =>
                    { if (u.FileDone > 0 && u.Phase == "Kopírování") File.WriteAllText(largeTarget, "CHANGED BY OTHER PROCESS"); }).Run(new[] { large });
                }
                assert(result.Errors.Count == 1 && File.ReadAllText(largeTarget) == "CHANGED BY OTHER PROCESS", "external destination change blocks commit");
                using (var control = new CopyControl())
                {
                    control.Pause(true);
                    var task = Task.Run(() => new CopyEngine(new CopyOptions { Target = output, Rule = CopyRule.OverwriteAll }, control, c => new CopyDecision(CopyChoice.Overwrite), null).Run(new[] { a }));
                    bool waited = !task.Wait(150); control.Pause(false); Check(task.Wait(10000), "resume timeout");
                    assert(waited && task.Result.Copied == 1, "pause and resume");
                }
                using (var control = new CopyControl())
                {
                    control.Pause(true); control.Cancel();
                    result = new CopyEngine(new CopyOptions { Target = output }, control, c => new CopyDecision(CopyChoice.Overwrite), null).Run(new[] { a });
                    assert(result.Cancelled, "cancel while paused");
                }
                Window owner = null;
                var setup = new CopySetupDialog(owner, new[] { new Entry { FullPath = large, Name = "large.bin", Bytes = new FileInfo(large).Length } }, output, new string[0]);
                Render(setup, Path.Combine(workspace, "copy-setup.png")); setup.Close();
                var conflict = new CopyConflictDialog(owner, new CopyConflict { Source = a, Target = da, SourceSize = new FileInfo(a).Length, TargetSize = new FileInfo(da).Length, SourceTime = File.GetLastWriteTime(a), TargetTime = File.GetLastWriteTime(da) });
                Render(conflict, Path.Combine(workspace, "copy-conflict.png")); conflict.Close();
                using (var control = new CopyControl())
                {
                    var progress = new CopyProgressDialog(owner, control); progress.Update(sample); Render(progress, Path.Combine(workspace, "copy-progress.png")); progress.Finish();
                }
                passed.Add("all three WPF copy dialogs rendered");
                var errorDialog = new CopyErrorDialog(null, new CopyFailure { Source = "C:\\hiberfil.sys", Target = "E:\\Temp\\hiberfil.sys", Error = new IOException("Locked", unchecked((int)0x80070020)) });
                Render(errorDialog, Path.Combine(workspace, "copy-error.png")); errorDialog.Close(); passed.Add("copy error dialog with four choices rendered");
                File.WriteAllText(Path.Combine(workspace, "copy-test-result.txt"), "PASS: " + passed.Count + " checks\r\n" + String.Join("\r\n", passed));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "copy-test-result.txt"), "FAIL after " + passed.Count + " checks:\r\n" + ex); return 1; }
            finally
            {
                string allowed = Path.GetFullPath(workspace).TrimEnd('\\') + "\\";
                if (!Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid cleanup path");
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
        static void Render(Window window, string destination)
        {
            FluentTheme.Prepare(window);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(window.Width - 16, Double.PositiveInfinity));
            var size = content.DesiredSize; content.Arrange(new Rect(0, 0, window.Width - 16, size.Height)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.Width - 16, (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual(); using (var context = visual.RenderOpen()) context.DrawRectangle(CompactTheme.Surface, null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Render(visual); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(destination)) encoder.Save(stream);
        }
    }
}

