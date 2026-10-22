using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        internal async Task VerifyBackgroundCopy(RemoteConnection connection, string workspace, RemoteConnection concurrentFtp = null)
        {
            await left.AttachRemote(connection);
            string destination = Directory.CreateDirectory(Path.Combine(workspace, "background-target")).FullName;
            right.Navigate(destination, false);
            var entry = left.List.Items.Cast<Entry>().Single(x => x.FullPath == "/slow.bin");
            int ticks = 0; bool intermediate = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            timer.Tick += delegate
            {
                ticks++;
                if (!intermediate && copyActivities.Children.Cast<CopyActivity>().Any(x => x.TotalFraction > 0 && x.TotalFraction < 1))
                {
                    intermediate = true;
                    var content = (System.Windows.FrameworkElement)Content;
                    content.Measure(new System.Windows.Size(1440, 800)); content.Arrange(new System.Windows.Rect(0, 0, 1440, 800)); content.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1440, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(content);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(workspace, "background-copy.png"))) encoder.Save(stream);
                }
            };
            timer.Start();
            try
            {
                var copy = StartNetworkCopy(new[] { entry }, left.Remote, true, destination, right);
                await Task.Delay(200);
                if (copy.IsCompleted) await copy;
                if (copy.IsCompleted || busy || !left.IsEnabled || !right.IsEnabled || actions.Any(x => !x.IsEnabled)) throw new Exception("Transfer blocked controls or ended before navigation test.");
                var job = networkJobs.Single();
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (job.Update().File <= 0 || job.Update().File >= 1)
                { if (copy.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Transfer ended before live queue expansion test."); await Task.Delay(30); }
                if (job.TotalBytes != 8388608) throw new Exception("Initial queue byte count is incorrect.");
                Task ftpCopy = null;
                string ftpDestination = Directory.CreateDirectory(Path.Combine(workspace, "ftp-concurrent-target")).FullName;
                if (concurrentFtp != null) ftpCopy = StartNetworkCopy(new[] { new Entry { FullPath = "/slow.bin" } }, concurrentFtp, true, ftpDestination);
                string existing = Directory.CreateDirectory(Path.Combine(destination, "sub", "nested")).FullName;
                File.WriteAllText(Path.Combine(existing, "one.bin"), "old content");
                var append = StartNetworkCopy(new[] { new Entry { FullPath = "/sub", IsDirectory = true } }, left.Remote, true, destination, null, NetworkCopyRule.Overwrite);
                while (job.TotalBytes == 8388608)
                { if (copy.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Queue did not grow while original transfer was running."); await Task.Delay(20); }
                if (job.TotalBytes != 10485760 || networkJobs.Count != (concurrentFtp == null ? 1 : 2)) throw new Exception("Append started another job or total byte count is incorrect.");
                var duplicate = StartNetworkCopy(new[] { new Entry { FullPath = "/sub", IsDirectory = true } }, left.Remote, true, destination);
                while (job.Planning > 0) await Task.Delay(20);
                if (job.TotalBytes != 10485760 || job.SnapshotItems().Count(x => !x.Directory) != 3) throw new Exception("Duplicate queue selection inflated the byte total.");
                if (ftpCopy != null && (ftpCopy.IsCompleted || networkCopies != 2)) throw new Exception("FTP and SFTP did not run concurrently.");
                var queuePreview = new NetworkQueueWindow(null, delegate { return networkJobs.ToArray(); }, job);
                NetworkTests.Render(queuePreview, Path.Combine(workspace, "network-queue.png")); queuePreview.Close();
                left.Navigate("/sub", true);
                while (left.IsLoading) await Task.Delay(30);
                if (left.CurrentPath != "/sub") throw new Exception("Navigation failed during transfer.");
                left.DisconnectRemote();
                string elsewhere = Directory.CreateDirectory(Path.Combine(workspace, "elsewhere")).FullName;
                right.Navigate(elsewhere, false); SwapPanels();
                await Task.WhenAll(copy, append, duplicate);
                if (ftpCopy != null) { await ftpCopy; if (new FileInfo(Path.Combine(ftpDestination, "slow.bin")).Length != 8388608) throw new Exception("Concurrent FTP copy content is incomplete."); }
                if (ticks < 15 || !intermediate) throw new Exception("Missing live byte progress or responsive dispatcher.");
                if (new FileInfo(Path.Combine(destination, "slow.bin")).Length != 8388608 || File.Exists(Path.Combine(elsewhere, "slow.bin"))) throw new Exception("Transfer destination changed with browsing panels.");
                if (!File.ReadAllBytes(Path.Combine(destination, "sub", "nested", "one.bin")).All(x => x == 65) || !File.ReadAllBytes(Path.Combine(destination, "sub", "two.bin")).All(x => x == 66)) throw new Exception("Appended folder or its overwrite policy failed.");
                if (right.IsRemote || left.CurrentPath != elsewhere) throw new Exception("Completion changed browsing panels.");
                if (concurrentFtp != null)
                {
                    string cancelledDestination = Directory.CreateDirectory(Path.Combine(workspace, "queue-cancelled-target")).FullName;
                    var cancelledCopy = StartNetworkCopy(new[] { new Entry { FullPath = "/slow.bin" } }, concurrentFtp, true, cancelledDestination);
                    var cancelledJob = networkJobs.Last();
                    deadline = DateTime.UtcNow.AddSeconds(15);
                    while (cancelledJob.Update().File <= 0) { if (cancelledCopy.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Cancel test did not enter transfer."); await Task.Delay(30); }
                    cancelledJob.Cancel(); await cancelledCopy;
                    if (!cancelledJob.Finished || !cancelledJob.Summary.Contains("Zrušeno") || !concurrentFtp.Session.FileExists("/slow.bin")) throw new Exception("Queue cancellation damaged browsing session or failed to cancel.");
                    concurrentFtp.Session.CreateDirectory("/queue-upload");
                    var upload = StartNetworkCopy(new[] { new Entry { FullPath = Path.Combine(destination, "slow.bin") } }, concurrentFtp, false, "/queue-upload");
                    var uploadJob = networkJobs.Last(); deadline = DateTime.UtcNow.AddSeconds(15);
                    while (uploadJob.Update().File <= 0) { if (upload.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Upload ended before expansion test."); await Task.Delay(30); }
                    var uploadAppend = StartNetworkCopy(new[] { new Entry { FullPath = Path.Combine(destination, "sub"), IsDirectory = true } }, concurrentFtp, false, "/queue-upload", null, NetworkCopyRule.Overwrite);
                    while (uploadJob.TotalBytes == 8388608) { if (upload.IsCompleted || DateTime.UtcNow > deadline) throw new Exception("Upload queue did not grow during transfer."); await Task.Delay(30); }
                    if (uploadJob.TotalBytes != 10485760 || networkJobs.Last() != uploadJob) throw new Exception("Upload addition started another job or inflated byte total.");
                    await Task.WhenAll(upload, uploadAppend);
                    if (concurrentFtp.Session.GetFileInfo("/queue-upload/slow.bin").Length != 8388608 || concurrentFtp.Session.GetFileInfo("/queue-upload/sub/nested/one.bin").Length != 1048576) throw new Exception("Upload queue did not transfer nested files.");
                    concurrentFtp.Session.CreateDirectory("/queue-error-cancel");
                    using (var locked = new FileStream(Path.Combine(destination, "slow.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        var errorCopy = StartNetworkCopy(new[] { new Entry { FullPath = Path.Combine(destination, "slow.bin") } }, concurrentFtp, false, "/queue-error-cancel");
                        var errorJob = networkJobs.Last(); errorJob.ResolveError = delegate { return CopyErrorChoice.Cancel; };
                        await errorCopy;
                        if (!errorJob.Finished || !errorJob.Cancellation.IsCancellationRequested || !errorJob.Summary.Contains("Zrušeno")) throw new Exception("Error-dialog cancellation did not cancel the network job.");
                    }
                }
            }
            finally { timer.Stop(); }
        }
    }
}

