using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileCompass.NetworkTransport;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    internal static class NextRemoteTests
    {
        internal static int Execute(string root,int ftpPort,int targetPort,int sftpPort)
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher)); Directory.CreateDirectory(root); var checks = new System.Collections.Generic.List<string>();
            try { using (var source = RelayTests.Open(ftpPort)) using (var target = RelayTests.Open(targetPort)) using (var sftp = RelayTests.Open(sftpPort,true)) {
                source.Session.CreateDirectory("/tree"); string text = Path.Combine(root,"find.txt"); File.WriteAllText(text,"test"); source.Session.PutFiles(text,"/tree/find.txt").Check();
                Run(source,target,sftp,root,delegate(bool valid,string label) { if (!valid) throw new Exception(label); checks.Add(label); });
            } File.WriteAllLines(Path.Combine(root,"relay-result.txt"),checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root,"relay-result.txt"),"FAIL after " + checks.Count + " checks\n" + String.Join("\n",checks) + "\n" + ex); return 1; }
        }
        static SyncPlan Compare(SyncLocation source,SyncLocation target,bool content = false,bool mirror = false)
        { using (var control = new CopyControl()) return RemoteDirectorySync.Compare(source,target,content,mirror,control); }
        static System.Collections.Generic.List<OperationItem> Apply(SyncPlan plan)
        { using (var control = new CopyControl()) return RemoteDirectorySync.Apply(plan,control,delegate { },null); }
        internal static void Run(RemoteConnection ftp,RemoteConnection target,RemoteConnection sftp,string root,Action<bool,string> check)
        {
            string directory = Directory.CreateDirectory(Path.Combine(root,"next-remote")).FullName;
            string local = Directory.CreateDirectory(Path.Combine(directory,"source")).FullName, downloaded = Directory.CreateDirectory(Path.Combine(directory,"downloaded")).FullName;
            string name = "český [soubor].txt",path = Path.Combine(local,name); File.WriteAllText(path,"Příliš žluťoučký kůň",new UTF8Encoding(false)); File.SetLastWriteTimeUtc(path,new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc)); Directory.CreateDirectory(Path.Combine(local,"empty"));
            ftp.Session.CreateDirectory("/stage-sync"); sftp.Session.CreateDirectory("/stage-sync"); target.Session.CreateDirectory("/stage-recovery");
            using (var a = new SyncLocation(local)) using (var b = new SyncLocation("/stage-sync",ftp)) {
                var plan = Compare(a,b); check(plan.Rows.Count == 2 && plan.Rows.Any(x => x.Action == "Vytvořit složku"),"local to FTP synchronization plans files and empty folders"); var result = Apply(plan); check(result.All(x => x.State == "Dokončeno") && ftp.Session.GetFileInfo("/stage-sync/" + name).Length == new FileInfo(path).Length,"local to FTP synchronization performs protected upload");
                check(Compare(a,b,true).Rows.All(x => x.Action == "Ponechat"),"content comparison streams local and FTP bytes without temporary whole-file copies");
            }
            using (var a = new SyncLocation("/stage-sync",ftp)) using (var b = new SyncLocation("/stage-sync",sftp)) {
                check(Apply(Compare(a,b)).All(x => x.State == "Dokončeno") && sftp.Session.FileExists("/stage-sync/empty"),"FTP to SFTP synchronization streams files and creates empty folders");
                string other = Path.Combine(directory,"orphan.txt"); File.WriteAllText(other,"orphan"); sftp.Session.PutFiles(other,"/stage-sync/orphan.txt").Check(); var normal = Compare(a,b); check(normal.Rows.Single(x => x.Relative == "orphan.txt").Action == "Ponechat","remote synchronization never deletes target-only files by default");
                var mirror = Compare(a,b,false,true); check(mirror.Rows.Single(x => x.Relative == "orphan.txt").Action == "Odstranit" && Apply(mirror).All(x => x.State == "Dokončeno") && !sftp.Session.FileExists("/stage-sync/orphan.txt"),"explicit remote mirror deletes only reviewed target-only files");
                File.WriteAllText(other,"changed target"); var stale = Compare(a,b); sftp.Session.PutFiles(other,"/stage-sync/" + name).Check(); bool rejected = false; try { Apply(stale); } catch (IOException) { rejected = true; } check(rejected && sftp.Session.GetFileInfo("/stage-sync/" + name).Length == new FileInfo(other).Length,"changed target invalidates remote plan before writes");
                File.WriteAllText(other,"AAAA"); File.SetLastWriteTimeUtc(other,new DateTime(2026,10,2,12,0,0,DateTimeKind.Utc)); ftp.Session.PutFiles(other,"/stage-sync/equal.txt").Check(); File.WriteAllText(other,"BBBB"); File.SetLastWriteTimeUtc(other,new DateTime(2026,10,2,12,0,0,DateTimeKind.Utc)); sftp.Session.PutFiles(other,"/stage-sync/equal.txt").Check();
                check(Compare(a,b).Rows.Single(x => x.Relative == "equal.txt").Action == "Ponechat" && Compare(a,b,true).Rows.Single(x => x.Relative == "equal.txt").Action == "Přepsat","remote content comparison detects changed bytes with equal metadata");
            }
            using (var a = new SyncLocation("/stage-sync",sftp)) using (var b = new SyncLocation(downloaded)) check(Apply(Compare(a,b)).All(x => x.State == "Dokončeno") && Directory.Exists(Path.Combine(downloaded,"empty")) && File.Exists(Path.Combine(downloaded,name)),"SFTP to local synchronization downloads literal names and empty folders");
            using (var a = new SyncLocation("/stage-sync",ftp)) using (var b = new SyncLocation("/stage-sync/sub",ftp)) { bool rejected = false; try { Compare(a,b); } catch (IOException) { rejected = true; } check(rejected,"remote synchronization rejects nested roots on the same server"); }
            var preview = RemotePreview.Load(ftp,"/stage-sync/" + name,Encoding.UTF8,CancellationToken.None); check(preview.Text == "Příliš žluťoučký kůň","FTP quick preview decodes Czech text through a separate connection");
            string big = Path.Combine(directory,"large.txt"); File.WriteAllText(big,new string('A',300000)); sftp.Session.PutFiles(big,"/stage-sync/large.txt").Check(); preview = RemotePreview.Load(sftp,"/stage-sync/large.txt",Encoding.UTF8,CancellationToken.None); check(preview.Text.Length == 131072 && preview.Status.Contains("128 KiB"),"SFTP quick preview downloads only bounded text prefix"); ftp.Session.PutFiles(big,"/stage-sync/large.txt").Check(); check(RemotePreview.Load(ftp,"/stage-sync/large.txt",Encoding.UTF8,CancellationToken.None).Text.Length == 131072,"FTP partial preview stops at 128 KiB");
            string picture = Path.Combine(directory,"picture.png"); var visual = new DrawingVisual(); using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(Brushes.Orange,null,new Rect(0,0,25,15)); var pixels = new RenderTargetBitmap(25,15,96,96,PixelFormats.Pbgra32); pixels.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels)); using (var output = File.Create(picture)) encoder.Save(output); ftp.Session.PutFiles(picture,"/stage-sync/picture.png").Check(); check(RemotePreview.Load(ftp,"/stage-sync/picture.png",Encoding.UTF8,CancellationToken.None).Image != null,"remote quick preview decodes image and releases connection");
            using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); bool stopped = false; try { RemotePreview.Load(sftp,"/stage-sync/large.txt",Encoding.UTF8,cancel.Token); } catch (OperationCanceledException) { stopped = true; } check(stopped && sftp.Session.ListDirectory("/stage-sync").Files.Count > 0,"cancelled preview leaves panel browsing connection usable"); }
            var hits = new System.Collections.Generic.List<SearchHit>(); using (var connection = ftp.TransferConnection()) { connection.OpenTransfer(); FileSearchEngine.Remote(new FileSearchOptions { Root = "/stage-sync;/tree",Mask = "*.txt",ModifiedFrom = new DateTime(2020,1,1) },new string[0],connection,CancellationToken.None,hits.Add); } check(hits.Any(x => x.Path.StartsWith("/stage-sync/")) && hits.Any(x => x.Path.StartsWith("/tree/")),"remote search supports multiple roots with modification-date filtering");
            // Build and persist a request independently of a MainWindow, then re-open it through the real recovery UI coordinator.
            string recoveryRoot = Directory.CreateDirectory(Path.Combine(directory,"recovery-state")).FullName;
            using (var queue = new DurableQueue(recoveryRoot)) {
                var transfer = queue.Begin("Relay",new[] { "/stage-sync" },"/stage-recovery",null,NetworkCopyRule.Overwrite,false,ftp.Profile,target.Profile);
                System.Collections.Generic.List<NetworkCopyBatch> batches; using (var planner = ftp.TransferConnection()) batches = NetworkRelay.Prepare(planner,target.Profile,new[] { new Entry { FullPath = "/stage-sync",IsDirectory = true } },"/stage-recovery",NetworkCopyRule.Overwrite,CancellationToken.None);
                queue.Plan(transfer,batches.SelectMany(x => x.Items).Select(x => new DurableFile { Source = x.Source,Target = x.Destination,Directory = x.Directory,Bytes = x.Bytes / 2,Modified = x.Modified.ToUniversalTime() }));
                target.Session.CreateDirectory("/stage-recovery/stage-sync"); var completed = transfer.Files.First(x => !x.Directory && x.Source.EndsWith(name)); string kept = Path.Combine(directory,"kept.txt"); File.WriteAllText(kept,"KEEP COMPLETED TARGET"); target.Session.PutFiles(kept,completed.Target).Check(); queue.Done(transfer,completed.Source,completed.Target);
            }
            MainWindow main = null; string previousRoot = DurableQueue.TestDirectory; DurableQueue.TestDirectory = recoveryRoot;
            try {
                main = new MainWindow(recoveryRoot) { Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false }; main.Show(); NavigationTests.WaitPanels(main); NavigationTests.Wait(main.VerifyRecoveredNetwork(ftp,target,sftp,recoveryRoot,name,check)); main.Close(); main = null;
            } finally { DurableQueue.TestDirectory = previousRoot; if (main != null) main.Close(); }
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyRecoveredNetwork(RemoteConnection source,RemoteConnection target,RemoteConnection sftp,string root,string name,Action<bool,string> check)
        {
            networkStore = new NetworkSettingsStore(Path.Combine(root,"test-connections.xml")); foreach (var connection in new[] { source,target,sftp }) { var profile = connection.Profile.Clone(); profile.EncryptedPassword = NetworkSettingsStore.Protect("test-password"); networkStore.Put(profile); }
            var request = durableQueue.Pending().Single(); await RestoreTransfer(request);
            string received = Path.Combine(root,"received.txt"); target.Session.GetFiles("/stage-recovery/stage-sync/" + name,received).Check(); check(File.ReadAllText(received) == "KEEP COMPLETED TARGET" && target.Session.FileExists("/stage-recovery/stage-sync/large.txt"),"fresh recovered FTP relay preserves completed target and copies only unfinished files"); check(durableQueue.Pending().Length == 0 && !Directory.GetFiles(Path.Combine(root,"transfer-queue"),"*.xml").Any(),"successful recovered network request clears persistent plan");
            var altered = durableQueue.Begin("Relay",new[] { "/tree" },"/stage-recovery",null,NetworkCopyRule.Skip,false,source.Profile,target.Profile); altered.Running = false; altered.SourceIdentity = "changed"; bool refused = false; try { await RestoreTransfer(altered); } catch (IOException) { refused = true; } check(refused,"changed saved server identity blocks recovery before transfer"); durableQueue.Remove(altered);
            target.Session.CreateDirectory("/stage-recover-partial"); var partial = durableQueue.Begin("Relay",new[] { "/slow.bin" },"/stage-recover-partial",null,NetworkCopyRule.Overwrite,false,source.Profile,target.Profile);
            var interrupted = new NetworkTransferJob(source.Profile,false,target.Profile) { KeepPartialOnCancel = true }; interrupted.Reserve();
            var batches = await Task.Run(delegate { using (var planner = source.TransferConnection()) return NetworkRelay.Prepare(planner,target.Profile,new[] { new Entry { FullPath = "/slow.bin" } },"/stage-recover-partial",NetworkCopyRule.Overwrite,CancellationToken.None); }); PersistPrepared(partial,batches); interrupted.AddPrepared(batches); interrupted.PlanningDone(null,"/slow.bin","/stage-recover-partial");
            interrupted.Changed = delegate { if (!interrupted.Cancellation.IsCancellationRequested && Interlocked.Read(ref interrupted.RelayUploaded) > 1048576) interrupted.Cancel(); };
            bool stopped = await Task.Run(delegate { using (var first = source.TransferConnection()) using (var second = target.TransferConnection()) try { NetworkRelay.Run(interrupted,first,second); return false; } catch (OperationCanceledException) { return true; } }); durableQueue.Finish(partial,false); interrupted.Cancellation.Dispose();
            check(stopped && target.Session.ListDirectory("/stage-recover-partial").Files.Any(x => x.Name.StartsWith(".FileCompass-relay-")),"interruption leaves an owned partial and a durable request");
            durableQueue.Dispose(); durableQueue = new DurableQueue(root); var reopened = durableQueue.Pending().Single(); await RestoreTransfer(reopened); var resumed = networkJobs.Last();
            check(resumed.RelayResumeOffset > 0 && resumed.SnapshotItems().All(x => x.State == "Dokončeno") && target.Session.GetFileInfo("/stage-recover-partial/slow.bin").Length == 8388608,"reloaded durable queue resumes remote partial from saved stream offset");
            check(resumed.RelayProbeBytes <= 262144,"queue recovery verifies bounded edge blocks instead of rereading whole partial");
            var browser = source.TransferConnection(); await Task.Run(delegate { browser.OpenTransfer(); }); await left.AttachRemote(browser); left.Navigate("/stage-sync",true); while (left.IsLoading) await Task.Delay(20); left.List.SelectedItem = left.List.Items.Cast<Entry>().First(x => x.FullPath.EndsWith("/" + name)); SetActive(left); ToggleQuickPreview(); await quickPreview.LoadingTask; check(FluentUiTests.Children<TextBox>(quickPreview).Any(x => x.Text.Contains("žluťoučký")),"Ctrl+Q in a remote panel opens bounded remote preview"); CloseQuickPreview();
            var sync = new DirectorySyncWindow(this,new SyncLocation("/stage-sync",source),new SyncLocation("/stage-sync",sftp),true); sync.Left = -18000; sync.Top = -18000; sync.WindowStartupLocation = WindowStartupLocation.Manual; sync.Show(); await sync.Compare(); NextStageTests.Draw(sync,Path.Combine(root,"remote-sync.png")); check(FluentUiTests.Children<ListView>(sync).Single().Items.Count > 0,"remote synchronization window displays actual plan"); sync.Close();
            left.SetDisplay("Thumbnails"); UpdateLayout(); var thumbnail = left.List.Items.Cast<Entry>().First(x => x.FullPath.EndsWith("/picture.png")); var until = DateTime.UtcNow.AddSeconds(8); while (Object.ReferenceEquals(thumbnail.Thumbnail,thumbnail.Icon) && DateTime.UtcNow < until) await Task.Delay(50); check(thumbnail.Thumbnail is BitmapSource && !Object.ReferenceEquals(thumbnail.Thumbnail,thumbnail.Icon),"FTP thumbnail uses bounded independent preview and preserves browser session"); left.SetDisplay("Details");
            await operationJournal.PendingWrites;
        }
    }
}

