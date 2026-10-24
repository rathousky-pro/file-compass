using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FileCompass
{
    internal static class NextStageTests
    {
        internal static int Seed(string root)
        {
            var queue = new DurableQueue(root); string source = Path.Combine(root,"recover-source"),target = Path.Combine(root,"recover-target");
            var job = queue.Begin("Local",new[] { source },target,new CopyOptions { Target = target,Rule = CopyRule.OverwriteAll },NetworkCopyRule.Reject,false,null,null);
            queue.Plan(job,Directory.GetFiles(source).Select(x => new DurableFile { Source = x,Target = Path.Combine(target,Path.GetFileName(x)),Bytes = new FileInfo(x).Length,Modified = File.GetLastWriteTimeUtc(x) }));
            queue.Done(job,Path.Combine(source,"first.txt"));
            // Exit without Dispose: only the OS releases the claim, as after an unexpected exit.
            Environment.Exit(0); return 0;
        }
        internal static void Draw(Window window,string file)
        {
            window.UpdateLayout(); var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right),(int)Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom),96,96,PixelFormats.Pbgra32);
            var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(window.Background,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight)); bitmap.Render(visual); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) encoder.Save(stream);
        }
        internal static int Run(string root)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)); Directory.CreateDirectory(root); var checks = new List<string>();
            Action<bool,string> check = delegate(bool valid,string label) { if (!valid) throw new Exception(label); checks.Add(label); }; MainWindow main = null;
            try {
                string first = Directory.CreateDirectory(Path.Combine(root,"find-first")).FullName,second = Directory.CreateDirectory(Path.Combine(root,"find-second")).FullName;
                File.WriteAllText(Path.Combine(first,"same-a.txt"),"same content"); File.WriteAllText(Path.Combine(second,"same-b.txt"),"same content"); File.WriteAllText(Path.Combine(second,"other.txt"),"different!!!");
                var hits = new List<SearchHit>(); var warnings = new List<string>(); var options = new FileSearchOptions { Root = first + ";" + second + ";" + first,Mask = "*.txt" };
                FileSearchEngine.Local(options,new string[0],CancellationToken.None,hits.Add,warnings.Add); check(hits.Count == 3 && hits.Select(x => x.Path).Distinct().Count() == 3,"multiple search roots deduplicate overlapping paths");
                options.Duplicates = true; hits.Clear(); FileSearchEngine.Local(options,new string[0],CancellationToken.None,hits.Add,warnings.Add); check(hits.Count == 2 && hits.Select(x => x.DuplicateGroup).Distinct().Count() == 1,"duplicates compare content, rejecting different bytes of equal size");
                string readonlyFile = Path.Combine(first,"same-a.txt"); File.SetLastWriteTimeUtc(readonlyFile,new DateTime(2026,1,2,12,0,0,DateTimeKind.Utc)); File.SetAttributes(readonlyFile,File.GetAttributes(readonlyFile) | FileAttributes.ReadOnly);
                options.Duplicates = false; options.RequiredAttributes = FileAttributes.ReadOnly; options.ModifiedFrom = new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc); options.ModifiedTo = new DateTime(2026,1,3,0,0,0,DateTimeKind.Utc); hits.Clear(); FileSearchEngine.Local(options,new string[0],CancellationToken.None,hits.Add,warnings.Add); check(hits.Count == 1 && hits[0].Path == readonlyFile,"attribute and inclusive date filters apply together"); File.SetAttributes(readonlyFile,FileAttributes.Normal);
                string encoded = Path.Combine(first,"encoded.txt"); File.WriteAllText(encoded,"Příliš žluťoučký kůň",Encoding.GetEncoding(1250)); options = new FileSearchOptions { Root = encoded,Text = "žluťoučký",EncodingCodePage = 1250 }; hits.Clear(); FileSearchEngine.Local(options,new string[0],CancellationToken.None,hits.Add,warnings.Add); check(hits.Count == 1,"search reads selected legacy text encoding");
                string large = Path.Combine(first,"large.txt"); using (var stream = new FileStream(large,FileMode.Create,FileAccess.Write)) { stream.SetLength(68L * 1048576); stream.Position = stream.Length - 6; stream.Write(Encoding.ASCII.GetBytes("needle"),0,6); }
                options = new FileSearchOptions { Root = large,Text = "needle",MaximumTextBytes = 0 }; hits.Clear(); FileSearchEngine.Local(options,new string[0],CancellationToken.None,hits.Add,warnings.Add); check(hits.Count == 1,"large text is streamed beyond old 64 MiB limit");
                SearchAdvanced.SaveProfiles(new List<SearchProfile> { new SearchProfile { Name = "Testovací profil",Options = options } }); var saved = SearchAdvanced.LoadProfiles().Single(); check(saved.Name == "Testovací profil" && saved.Options.MaximumTextBytes == 0 && saved.Options.Root == large,"search profile round-trip preserves paths and large-text settings");
                string source = Directory.CreateDirectory(Path.Combine(root,"recover-source")).FullName,target = Directory.CreateDirectory(Path.Combine(root,"recover-target")).FullName; File.WriteAllText(Path.Combine(source,"first.txt"),"first"); File.WriteAllText(Path.Combine(source,"second.txt"),"second"); File.WriteAllText(Path.Combine(target,"first.txt"),"newer target must survive");
                var child = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--durable-seed-test \"" + root + "\"") { UseShellExecute = false,CreateNoWindow = true }); if (!child.WaitForExit(15000) || child.ExitCode != 0) throw new Exception("Durable seed child failed");
                using (var queue = new DurableQueue(root)) {
                    var job = queue.Pending().Single(); check(job.Files.Count == 2 && job.Files.Count(x => x.Done) == 1,"fresh process recovers plan and durable per-file completion receipts");
                    using (var other = new DurableQueue(root)) check(other.Pending().Length == 0,"another instance cannot claim a queue already owned by this process");
                    check(!String.Join("",Directory.GetFiles(Path.Combine(root,"transfer-queue"),"*.xml").Select(File.ReadAllText)).Contains("Password"),"durable plans contain no passwords or encrypted credentials");
                }
                var image = new DrawingVisual(); using (var draw = image.RenderOpen()) draw.DrawRectangle(Brushes.Orange,null,new Rect(0,0,50,30)); var bitmap = new RenderTargetBitmap(50,30,96,96,PixelFormats.Pbgra32); bitmap.Render(image); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(root,"picture.png"))) encoder.Save(stream);
                main = new MainWindow(root) { Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false }; main.Show(); NavigationTests.WaitPanels(main); NavigationTests.Wait(main.VerifyNextStage(root,check)); main.Close(); main = null;
                File.WriteAllLines(Path.Combine(root,"next-stage-result.txt"),checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root,"next-stage-result.txt"),"FAIL after " + checks.Count + " checks\n" + String.Join("\n",checks) + "\n" + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyNextStage(string root,Action<bool,string> check)
        {
            ShowDurableQueue(); await Task.Delay(50); NextStageTests.Draw(recoveryWindow,Path.Combine(root,"queue-recovery-pending.png")); recoveryWindow.Close();
            var pending = durableQueue.Pending().Single(); await RestoreTransfer(pending); while (copyRunning) await Task.Delay(20); await operationJournal.PendingWrites;
            string target = Path.Combine(root,"recover-target"); check(File.ReadAllText(Path.Combine(target,"first.txt")) == "newer target must survive" && File.ReadAllText(Path.Combine(target,"second.txt")) == "second","restored local queue copies only unfinished files and preserves completed targets"); check(durableQueue.Pending().Length == 0 && !Directory.GetFiles(Path.Combine(root,"transfer-queue"),"*.xml").Any(),"completed recovered job is removed from durable queue");
            string changedFile = Path.Combine(root,"changed.txt"); File.WriteAllText(changedFile,"before"); var changed = durableQueue.Begin("Local",new[] { changedFile },target,new CopyOptions { Target = target },NetworkCopyRule.Reject,false,null,null); durableQueue.Plan(changed,new[] { new DurableFile { Source = changedFile,Target = Path.Combine(target,"changed.txt"),Bytes = new FileInfo(changedFile).Length,Modified = File.GetLastWriteTimeUtc(changedFile) } }); durableQueue.Finish(changed,false); File.WriteAllText(changedFile,"changed after plan"); bool refused = false; try { await RestoreTransfer(changed); } catch (IOException) { refused = true; } check(refused && !File.Exists(Path.Combine(target,"changed.txt")),"changed local source blocks restored copy before any target write"); durableQueue.Remove(changed);
            string moveRoot = Directory.CreateDirectory(Path.Combine(root,"move-recovery")).FullName; string moveFile = Path.Combine(moveRoot,"move.txt"); File.WriteAllText(moveFile,"move me"); var moving = durableQueue.Begin("Local",new[] { moveRoot },target,new CopyOptions { Target = target,Move = true },NetworkCopyRule.Reject,true,null,null); durableQueue.Plan(moving,new[] { new DurableFile { Source = moveRoot,Target = Path.Combine(target,"move-recovery"),Directory = true },new DurableFile { Source = moveFile,Target = Path.Combine(target,"move-recovery","move.txt"),Bytes = new FileInfo(moveFile).Length,Modified = File.GetLastWriteTimeUtc(moveFile) } }); durableQueue.Finish(moving,false); await RestoreTransfer(moving); while (copyRunning) await Task.Delay(20); check(!Directory.Exists(moveRoot) && File.ReadAllText(Path.Combine(target,"move-recovery","move.txt")) == "move me","recovered local F6 verifies file and cleans only empty source directories");
            left.SetDisplay("Thumbnails"); UpdateLayout(); var deadline = DateTime.UtcNow.AddSeconds(8); Entry picture = null;
            while (DateTime.UtcNow < deadline) { picture = left.List.Items.Cast<Entry>().FirstOrDefault(x => x.FullPath == Path.Combine(root,"picture.png")); if (picture != null && picture.Thumbnail is BitmapSource && !Object.ReferenceEquals(picture.Thumbnail,picture.Icon)) break; await Task.Delay(50); }
            check(picture != null && picture.Thumbnail is BitmapSource && !Object.ReferenceEquals(picture.Thumbnail,picture.Icon),"thumbnail mode decodes visible image on worker and releases its file"); using (File.Open(picture.FullPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
            var selection = left.List.SelectedItem; left.SetDisplay("Short"); UpdateLayout(); check(left.List.View == null && left.List.ItemTemplate != null && left.List.SelectedItem == selection,"short list preserves cursor and command selection");
            var testPanel = new FilePanel(delegate { }); var testWindow = new Window { Width = 620,Height = 430,Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false,Content = testPanel,Owner = this,ShowInTaskbar = false };
            testPanel.List.ItemsSource = Enumerable.Range(0,5000).Select(i => new Entry { Name = "Item " + i,FullPath = "E:\\test-" + i + ".png",IsLink = true,Icon = DesktopIcons.Drawing("document") }).ToArray(); testWindow.Show(); testPanel.SetDisplay("Thumbnails"); testWindow.UpdateLayout(); await Task.Delay(50);
            check(Enumerable.Range(0,5000).Count(i => testPanel.List.ItemContainerGenerator.ContainerFromIndex(i) != null) < 100,"thumbnail grid realizes fewer than 100 containers for 5000 entries"); testPanel.List.SelectedIndex = 4999; testPanel.List.ScrollIntoView(testPanel.List.SelectedItem); testWindow.UpdateLayout(); await Task.Delay(50); check(testPanel.List.ItemContainerGenerator.ContainerFromIndex(4999) != null,"thumbnail grid scrolls directly to last item without realizing entire folder"); testPanel.SetDisplay("Short"); testWindow.UpdateLayout(); check(testPanel.DisplayColumns > 1,"short list uses multiple compact columns"); testWindow.Close();
            left.SetTree(true); right.SetDisplay("Thumbnails"); UpdateLayout(); await Task.Delay(300); var setting = new PanelView(); left.SavePanelView(setting); check(setting.Display == "Short" && setting.Tree,"save view includes display mode and folder tree");
            NextStageTests.Draw(this,Path.Combine(root,"panels-next-stage.png")); left.SetDisplay("Details"); left.SetTree(false); left.RestorePanelView(setting); check(left.DisplayMode == "Short","panel view restores selected display mode");
            ShowDurableQueue(); NextStageTests.Draw(recoveryWindow,Path.Combine(root,"queue-recovery.png")); recoveryWindow.Close();
            var search = new FileSearchWindow(this,root,true,false,(o,t,f,w) => 0,h => { }); search.Left = -18000; search.Top = -18000; search.WindowStartupLocation = WindowStartupLocation.Manual; search.Show(); await Task.Delay(100); NextStageTests.Draw(search,Path.Combine(root,"search-next-stage.png")); check(FluentUiTests.Children<TabItem>(search).Any(x => (string)x.Header == "Profily"),"search dialog exposes saved-profile page"); search.Close();
            string exitRoot = Directory.CreateDirectory(Path.Combine(root,"save-exit")).FullName; string exitSource = Path.Combine(exitRoot,"large.bin"); using (var stream = File.Create(exitSource)) stream.SetLength(128L * 1048576);
            var closing = new MainWindow(exitRoot) { Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false }; closing.Show(); await Task.WhenAll(closing.left.NavigationTask,closing.right.NavigationTask); await closing.VerifyQueueExit(exitSource,exitRoot,check);
        }
        internal async Task VerifyQueueExit(string source,string root,Action<bool,string> check)
        {
            EnqueueCopy(new CopyJob { Sources = new[] { source },Options = new CopyOptions { Target = Path.Combine(root,"first") } }); EnqueueCopy(new CopyJob { Sources = new[] { source },Options = new CopyOptions { Target = Path.Combine(root,"second") } }); RunCopyQueue(); copyControl.Pause(true);
            bool closed = false; Closed += delegate { closed = true; }; SaveQueueAndExit(); var end = DateTime.UtcNow.AddSeconds(12); while (!closed && DateTime.UtcNow < end) await Task.Delay(50);
            check(closed && File.Exists(source),"Save queue and exit cancels paused local worker without deleting source");
            using (var reopened = new DurableQueue(DurableQueue.TestDirectory)) { var saved = reopened.Pending(); check(saved.Length == 2,"Save queue and exit preserves both active and waiting requests for next process"); foreach (var item in saved) reopened.Remove(item); }
        }
    }
}

