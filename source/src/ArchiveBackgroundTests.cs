using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        internal void VerifyArchiveCopyPresentation(string workspace)
        {
            string source = Directory.CreateDirectory(Path.Combine(workspace, "filter-source")).FullName;
            File.WriteAllText(Path.Combine(source, "keep.txt"), "verified text");
            File.SetAttributes(Path.Combine(source, "keep.txt"), FileAttributes.ReadOnly);
            File.WriteAllText(Path.Combine(source, "omit.bin"), "excluded");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            using (var temp = new ArchiveTemp())
            {
                CopyArchiveInputs(new[] { source }, temp.Path, new CopyOptions { Filter = "*.txt", EmptyFolders = false, Verify = true }, System.Threading.CancellationToken.None, delegate { });
                string copied = Path.Combine(temp.Path, "filter-source");
                if (File.ReadAllText(Path.Combine(copied, "keep.txt")) != "verified text" || (File.GetAttributes(Path.Combine(copied, "keep.txt")) & FileAttributes.ReadOnly) != 0 || File.Exists(Path.Combine(copied, "omit.bin")) || Directory.Exists(Path.Combine(copied, "empty")))
                    throw new Exception("Archive input filter, verification or empty-folder option failed.");
            }
            File.SetAttributes(Path.Combine(source, "keep.txt"), FileAttributes.Normal);
            var first = AddCopyActivity("FTP · test 1", delegate { }); first.Update("FTP · soubor", .8, .35);
            var second = AddCopyActivity("SFTP · test 2", delegate { }); second.Update("SFTP · soubor", .5, .2);
            if (copyActivities.Parent != null || copySummary.Visibility != System.Windows.Visibility.Visible)
                throw new Exception("Activity list must be available only in details, with one visible summary.");
            ShowActivityDetails();
            if (copyActivities.Parent == null) throw new Exception("Activity details did not open.");
            NetworkTests.Render(activityDetails, Path.Combine(workspace, "transfer-detail.png")); activityDetails.Close();
            if (copyActivities.Parent != null) throw new Exception("Activity details did not detach their list.");
            first.Finish("Dokončeno", true); second.Finish("Dokončeno", true);
            var preview = new DualCopyProgress { Width = 320, Height = 34 }; preview.Update(.8, .35, false);
            preview.Measure(new System.Windows.Size(320, 34)); preview.Arrange(new System.Windows.Rect(0, 0, 320, 34));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(320, 34, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(preview);
            byte[] pixels = new byte[320 * 34 * 4]; bitmap.CopyPixels(pixels, 320 * 4, 0);
            int light = (1 * 320 + 20) * 4, dark = (32 * 320 + 20) * 4, background = (32 * 320 + 200) * 4, gap = (16 * 320 + 20) * 4;
            if (pixels[light] != 244 || pixels[light + 1] != 197 || pixels[dark] != 162 || pixels[dark + 1] != 87 || pixels[background] != 243 || pixels[gap + 3] != 0)
                throw new Exception("Dual progress must use separate thin file and total lines with an unfilled gap.");
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(workspace, "dual-progress.png"))) encoder.Save(stream);
        }
        internal async Task VerifyArchiveBackground(string workspace)
        {
            string input = Path.Combine(workspace, "background-archive.bin");
            var data = new byte[1024 * 1024]; new Random(517).NextBytes(data);
            using (var stream = File.Create(input)) for (int i = 0; i < 16; i++) stream.Write(data, 0, data.Length);
            string target = Path.Combine(workspace, "background.7z");
            string elsewhere = Directory.CreateDirectory(Path.Combine(workspace, "elsewhere")).FullName;
            int ticks = 0; bool intermediate = false, usable = true;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += delegate
            {
                ticks++;
                usable &= !busy && left.IsEnabled && right.IsEnabled && actions.All(x => x.IsEnabled);
                intermediate |= copyActivities.Children.Cast<CopyActivity>().Any(x => x.TotalFraction > 0 && x.TotalFraction < 1);
                if (ticks == 1) { left.Navigate(elsewhere, false); right.Navigate(elsewhere, false); SwapPanels(); }
            };
            timer.Start();
            bool success;
            var log = new System.Collections.Generic.List<string>();
            try { success = await ArchiveOperation("Test archivace na pozadí", delegate(System.Threading.CancellationToken token, Action<string> progress) { ArchiveEngine.Create(target, new[] { input }, new ArchiveSettings(), token, delegate(string line) { lock (log) log.Add(line); progress(line); }); }); }
            finally { timer.Stop(); }
            if (!success || ticks < 2 || !usable || !intermediate || left.CurrentPath != elsewhere || right.CurrentPath != elsewhere || archiveOperations != 0)
                throw new Exception("Archive background operation blocks controls, loses navigation or does not report intermediate progress: " + ticks + "/" + intermediate + " · Output: " + String.Join(" | ", log));
            if (!ArchiveEngine.Open(target).Items.Any(x => x.Path == "background-archive.bin" && x.Bytes == 16L * 1024 * 1024)) throw new Exception("Archive background content mismatch");
        }
    }
}

