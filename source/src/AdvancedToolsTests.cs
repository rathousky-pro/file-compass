using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FileCompass
{
    internal static class AdvancedToolsTests
    {
        internal static IEnumerable<T> Visual<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T) yield return (T)root;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Visual<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
        }
        static readonly CancellationToken none = CancellationToken.None;
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var passed = new List<string>();
            Action<bool,string> check = (valid,label) => { if (!valid) throw new Exception(label); passed.Add(label); File.AppendAllText(Path.Combine(root,"advanced-tools-progress.txt"),label + Environment.NewLine); };
            MainWindow main = null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                TestSync(root,check); TestParts(root,check); TestImages(root,check); TestArchives(root,check);
                main = new MainWindow(root,new ProgramSettings { AutoRefresh = false }) { Width = 1300, Height = 820, ShowActivated = false, ShowInTaskbar = false, Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main); NavigationTests.Wait(main.VerifyAdvancedTools(root,check));
                File.WriteAllLines(Path.Combine(root,"advanced-tools-result.txt"),passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllLines(Path.Combine(root,"advanced-tools-result.txt"),new[] { "FAIL after " + passed.Count + " checks" }.Concat(passed).Concat(new[] { ex.ToString() })); return 1; }
            finally { if (main != null) main.Close(); }
        }
        static void Write(string root,string name,string text,DateTime stamp) { string file = Path.Combine(root,name); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,text); File.SetLastWriteTimeUtc(file,stamp); }
        static void TestSync(string root,Action<bool,string> check)
        {
            string left = Directory.CreateDirectory(Path.Combine(root,"sync-left")).FullName, right = Directory.CreateDirectory(Path.Combine(root,"sync-right")).FullName;
            DateTime old = new DateTime(2026,10,1,0,0,0,DateTimeKind.Utc), newer = old.AddDays(2);
            Write(left,"left.txt","left",old); Write(right,"right.txt","right",old);
            Write(left,"left-new.txt","new left",newer); Write(right,"left-new.txt","old right",old);
            Write(left,"right-new.txt","old left",old); Write(right,"right-new.txt","new right",newer);
            Write(left,"conflict.txt","aaaa",old); Write(right,"conflict.txt","bbbb",old);
            Directory.CreateDirectory(Path.Combine(right,"nested","empty")); Write(right,"nested/child.txt","child",old);
            var plan = DirectorySync.Compare(left,right,true,false,delegate {},true);
            check(plan.Bidirectional && !plan.Mirror && plan.Rows.Single(x => x.Relative == "left.txt").Direction == "→" && plan.Rows.Single(x => x.Relative == "right.txt").Direction == "←","bidirectional plan copies one-sided files in both directions without deleting");
            check(plan.Rows.Single(x => x.Relative == "left-new.txt").Direction == "→" && plan.Rows.Single(x => x.Relative == "right-new.txt").Direction == "←","newer files determine each individual direction");
            check(plan.Rows.Single(x => x.Relative == "conflict.txt").Direction == "≠","different same-time content remains an unresolved conflict");
            bool blocked = false; using (var control = new CopyControl()) try { DirectorySync.Apply(plan,control,delegate {}); } catch (IOException) { blocked = true; }
            check(blocked && !File.Exists(Path.Combine(left,"right.txt")),"unresolved conflicts prevent every write");
            plan.Rows.Single(x => x.Relative == "conflict.txt").ChosenAction = "←";
            plan.Rows.Single(x => x.Relative == "left-new.txt").ChosenAction = L.Text("sync.skipAction");
            using (var control = new CopyControl()) check(DirectorySync.Apply(plan,control,delegate {}).All(x => x.State == L.Code("state.completed")),"bidirectional execution completes through existing copy engines");
            check(File.ReadAllText(Path.Combine(left,"conflict.txt")) == "bbbb" && File.ReadAllText(Path.Combine(left,"right-new.txt")) == "new right" && File.Exists(Path.Combine(right,"left.txt")) && File.Exists(Path.Combine(left,"right.txt")),"individual resolutions and both copy directions are applied");
            check(File.ReadAllText(Path.Combine(right,"left-new.txt")) == "old right","individually skipped file remains unchanged");
            check(File.ReadAllText(Path.Combine(left,"nested","child.txt")) == "child" && Directory.Exists(Path.Combine(left,"nested","empty")),"reverse synchronization creates nested and empty directories");
            Write(right,"stale.txt","before",old); var stale = DirectorySync.Compare(left,right,true,false,delegate {},true); Write(right,"stale.txt","after",newer);
            blocked = false; using (var control = new CopyControl()) try { DirectorySync.Apply(stale,control,delegate {}); } catch (IOException) { blocked = true; }
            check(blocked && !File.Exists(Path.Combine(left,"stale.txt")),"both endpoints are checked for stale snapshots before any write");
            using (var a = new SyncLocation(left)) using (var b = new SyncLocation(right)) using (var control = new CopyControl()) {
                var remotePlan = RemoteDirectorySync.Compare(a,b,true,false,control,true);
                foreach (var row in remotePlan.Rows.Where(x => x.Direction == "≠")) row.ChosenAction = "→";
                var results = DirectorySync.Apply(remotePlan,control,delegate {});
                check(results.All(x => x.State == L.Code("state.completed")) && File.ReadAllText(Path.Combine(left,"stale.txt")) == "after","shared remote/local synchronization access supports bidirectional plans");
            }
        }
        static void TestParts(string root,Action<bool,string> check)
        {
            string dir = Directory.CreateDirectory(Path.Combine(root,"parts")).FullName, source = Path.Combine(dir,"data.bin"), prefix = Path.Combine(dir,"pieces.bin");
            var data = new byte[250013]; new Random(12345).NextBytes(data); File.WriteAllBytes(source,data);
            var outputs = FileParts.Split(source,prefix,100000,none,delegate {});
            check(outputs.Count == 4 && new FileInfo(prefix+".001").Length == 100000 && new FileInfo(prefix+".003").Length == 50013 && File.Exists(source),"split creates sized volumes and a manifest while preserving source");
            string combined = Path.Combine(dir,"combined.bin"); FileParts.Combine(prefix+".002",combined,none,delegate {});
            check(FileParts.Hash(combined,none) == FileParts.Hash(source,none),"combine discovers the full numeric sequence and verifies exact bytes");
            bool rejected = false; try { FileParts.Combine(prefix+".001",combined,none,delegate {}); } catch (IOException) { rejected = true; }
            check(rejected && File.ReadAllBytes(combined).SequenceEqual(data),"combine never overwrites an existing output");
            byte[] middle = File.ReadAllBytes(prefix+".002"); File.Delete(prefix+".002");
            rejected = false; string bad = Path.Combine(dir,"bad.bin"); try { FileParts.Combine(prefix+".001",bad,none,delegate {}); } catch (IOException) { rejected = true; }
            check(rejected && !File.Exists(bad),"missing middle part is rejected before publishing output"); File.WriteAllBytes(prefix+".002",middle);
            middle[0] ^= 127; File.WriteAllBytes(prefix+".002",middle);
            rejected = false; try { FileParts.Combine(prefix+".001",bad,none,delegate {}); } catch (IOException) { rejected = true; }
            check(rejected && !File.Exists(bad),"corrupted part fails SHA-256 validation and leaves no partial output");
            middle[0] ^= 127; File.WriteAllBytes(prefix+".002",middle);
            byte[] tail = File.ReadAllBytes(prefix+".003"); File.Delete(prefix+".003"); rejected = false;
            try { FileParts.Combine(prefix+".001",bad,none,delegate {}); } catch (IOException) { rejected = true; }
            check(rejected,"manifest detects a missing last volume"); File.WriteAllBytes(prefix+".003",tail);
            using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); bool cancelled = false; try { FileParts.Split(source,Path.Combine(dir,"cancel"),1000,cancel.Token,delegate {}); } catch (OperationCanceledException) { cancelled = true; } check(cancelled && !File.Exists(Path.Combine(dir,"cancel.001")),"cancelled split leaves source and output namespace intact"); }
            using(var cancel=new CancellationTokenSource()) { string cancelled=Path.Combine(dir,"cancelled-combine");bool stopped=false;try{FileParts.Combine(prefix+".001",cancelled,cancel.Token,text=>cancel.Cancel());}catch(OperationCanceledException){stopped=true;}check(stopped && !File.Exists(cancelled),"cancelling during combine never publishes a partial output"); }
            check(FileParts.ParseSize("4 GB") == 4294967296L && FileParts.ParseSize("700 MB") == 734003200L,"volume sizes use 64-bit arithmetic for four-gigabyte presets");
            string empty = Path.Combine(dir,"empty"); File.WriteAllBytes(empty,new byte[0]); FileParts.Split(empty,empty,1024,none,delegate {}); FileParts.Combine(empty+".001",empty+".joined",none,delegate {});
            check(new FileInfo(empty+".joined").Length == 0,"empty files split and combine correctly");
        }
        internal static void TestImages(string root,Action<bool,string> check)
        {
            string dir = Directory.CreateDirectory(Path.Combine(root,"images")).FullName, source = Path.Combine(dir,"picture.png");
            var pixels = new byte[32*16*4]; for (int i=0;i<pixels.Length;i+=4) { pixels[i]=25;pixels[i+1]=100;pixels[i+2]=220;pixels[i+3]=(byte)(i%3==0?0:255); }
            var bitmap = BitmapSource.Create(32,16,96,96,PixelFormats.Bgra32,null,pixels,32*4);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(source)) png.Save(stream);
            string original = FileParts.Hash(source,none);
            foreach(string format in new[]{"PNG","JPEG","BMP","TIFF"}) {
                string output = Directory.CreateDirectory(Path.Combine(dir,format)).FullName;
                var options = new ImageToolOptions { OutputFolder=output,Format=format,Rotation=90,ResizeMode=2,Percent=50 };
                ImageTools.Process(new[]{source},options,none,delegate {});
                using(var stream=File.OpenRead(ImageTools.Output(source,options))) {
                    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                    check(decoder.Frames[0].PixelWidth==8 && decoder.Frames[0].PixelHeight==16,"rotate, percentage resize and conversion produce correct pixels: "+format);
                }
            }
            check(FileParts.Hash(source,none)==original,"image processing preserves the original file");
            string maxDir=Directory.CreateDirectory(Path.Combine(dir,"maximum")).FullName;
            var maximum=new ImageToolOptions { OutputFolder=maxDir,Format="PNG",ResizeMode=1,MaxEdge=12 }; ImageTools.Process(new[]{source},maximum,none,delegate {});
            using(var stream=File.OpenRead(ImageTools.Output(source,maximum))) { var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0]; check(frame.PixelWidth==12 && frame.PixelHeight==6,"maximum edge preserves the aspect ratio"); }
            string multi=Path.Combine(dir,"multi.tiff"); var tiff=new TiffBitmapEncoder(); tiff.Frames.Add(BitmapFrame.Create(bitmap));tiff.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(multi))tiff.Save(stream);
            var multiOptions=new ImageToolOptions { OutputFolder=dir,Format="TIFF",ResizeMode=0,Rotation=180 };ImageTools.Process(new[]{multi},multiOptions,none,delegate {});
            using(var stream=File.OpenRead(ImageTools.Output(multi,multiOptions)))check(BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames.Count==2,"TIFF editing preserves every frame");
            string exif=Path.Combine(dir,"orientation.jpg");var jpeg=new JpegBitmapEncoder();var metadata=new BitmapMetadata("jpg");metadata.SetQuery("/app1/ifd/{ushort=274}",(ushort)6);jpeg.Frames.Add(BitmapFrame.Create(bitmap,null,metadata,null));using(var stream=File.Create(exif))jpeg.Save(stream);
            var oriented=new ImageToolOptions{OutputFolder=dir,Format="PNG",ResizeMode=0};ImageTools.Process(new[]{exif},oriented,none,delegate{});
            using(var stream=File.OpenRead(ImageTools.Output(exif,oriented))){var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];check(frame.PixelWidth==16 && frame.PixelHeight==32,"EXIF orientation is normalized before resizing and manual rotation");}
            bool rejected=false;try{ImageTools.Process(new[]{multi},new ImageToolOptions{OutputFolder=dir,Format="PNG"},none,delegate{});}catch(IOException){rejected=true;}
            check(rejected && !File.Exists(Path.Combine(dir,"multi_upraveno.png")),"conversion refuses to silently drop extra frames");
        }
        static void TestArchives(string root,Action<bool,string> check)
        {
            string dir=Directory.CreateDirectory(Path.Combine(root,"archives")).FullName,source=Path.Combine(dir,"random.bin");var bytes=new byte[160013];new Random(789).NextBytes(bytes);File.WriteAllBytes(source,bytes);
            foreach(string format in new[]{"zip","7z"}) {
                string target=Path.Combine(dir,"volumes."+format);
                ArchiveEngine.Create(target,new[]{source},ArchiveSettings.Load(),none,delegate{},new ArchivePackOptions{VolumeBytes=32768,Level=0});
                var archive=ArchiveEngine.Open(target+".001",null,none);ArchiveEngine.Test(archive,none);
                check(archive.MultiVolume && archive.Items.Any(x=>x.Path=="random.bin"),"split "+format+" archive opens and tests as the original archive contents");
                using(var extracted=new ArchiveTemp()){ArchiveEngine.Extract(archive,new[]{"random.bin"},extracted.Path,none);check(File.ReadAllBytes(Path.Combine(extracted.Path,"random.bin")).SequenceEqual(bytes),"split "+format+" archive extracts exact original bytes");}
            }
            string encrypted=Path.Combine(dir,"encrypted.7z");ArchiveEngine.Create(encrypted,new[]{source},ArchiveSettings.Load(),none,delegate{},new ArchivePackOptions{VolumeBytes=16384,Level=0,Password="tajné heslo"});
            var encryptedArchive=ArchiveEngine.Open(encrypted+".001","tajné heslo",none);ArchiveEngine.Test(encryptedArchive,none);
            check(encryptedArchive.Items.Any(x=>x.Path=="random.bin"),"encrypted split 7z archive opens and tests with its password");
            string altered=Path.Combine(dir,"changed-during-pack.bin");File.WriteAllBytes(altered,bytes);string changedTarget=Path.Combine(dir,"changed.zip");bool changed=false,stopped=false;
            try{ArchiveEngine.Create(changedTarget,new[]{altered},ArchiveSettings.Load(),none,text=>{if(!changed && text.StartsWith("Díl ",StringComparison.Ordinal)){changed=true;File.WriteAllText(altered,"changed while packing");}},new ArchivePackOptions{Move=true,VolumeBytes=32768,Level=0});}catch(IOException){stopped=true;}
            check(changed && stopped && File.ReadAllText(altered)=="changed while packing" && File.Exists(changedTarget+".001"),"source changes stop move deletion while retaining the verified archive");
            ArchiveEngine.Test(ArchiveEngine.Open(changedTarget+".001",null,none),none);
            string moving=Directory.CreateDirectory(Path.Combine(dir,"moving")).FullName;Directory.CreateDirectory(Path.Combine(moving,"empty"));File.WriteAllText(Path.Combine(moving,"text.txt"),"verified move");
            string packed=Path.Combine(dir,"moved.zip");ArchiveEngine.Create(packed,new[]{moving},ArchiveSettings.Load(),none,delegate{},new ArchivePackOptions{Move=true});
            check(!Directory.Exists(moving) && File.Exists(packed),"move removes source only after archive test and extracted-content verification");
            var moved=ArchiveEngine.Open(packed,null,none);using(var extracted=new ArchiveTemp()){ArchiveEngine.Extract(moved,moved.Items.Select(x=>x.Path),extracted.Path,none);check(File.ReadAllText(Path.Combine(extracted.Path,"moving","text.txt"))=="verified move" && Directory.Exists(Path.Combine(extracted.Path,"moving","empty")),"verified move retains files and empty folders");}
            foreach(string format in new[]{"tar","gz"}) { string streamSource=Path.Combine(dir,"move-source-"+format+".txt");File.WriteAllText(streamSource,"stream data");string target=Path.Combine(dir,"moved-stream."+format);ArchiveEngine.Create(target,new[]{streamSource},ArchiveSettings.Load(),none,delegate{},new ArchivePackOptions{Move=true});check(!File.Exists(streamSource) && File.Exists(target),"verified move supports "+format+" output"); }
            string unsafeFile=Path.Combine(dir,"do-not-delete.txt");File.WriteAllText(unsafeFile,"keep");bool rejected=false;
            try{ArchiveEngine.Create(Path.Combine(dir,"unsafe.zip"),new[]{unsafeFile},ArchiveSettings.Load(),none,delegate{},new ArchivePackOptions{Move=true,Recursive=false});}catch(IOException){rejected=true;}
            check(rejected && File.Exists(unsafeFile),"nonrecursive move is rejected without deleting source");
            string cancelled=Path.Combine(dir,"cancelled.zip");using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stoppedBefore=false;try{ArchiveEngine.Create(cancelled,new[]{unsafeFile},ArchiveSettings.Load(),cancel.Token,delegate{},new ArchivePackOptions{Move=true});}catch(OperationCanceledException){stoppedBefore=true;}check(stoppedBefore && File.Exists(unsafeFile) && !File.Exists(cancelled),"cancelled archive move keeps every source");}
        }
    }
    public partial class MainWindow
    {
        internal async Task VerifyAdvancedTools(string root,Action<bool,string> check)
        {
            string disk=Directory.CreateDirectory(Path.Combine(root,"disk")).FullName;
            Directory.CreateDirectory(Path.Combine(disk,"large"));Directory.CreateDirectory(Path.Combine(disk,"small"));
            File.WriteAllBytes(Path.Combine(disk,"large","a.bin"),new byte[3000]);File.WriteAllBytes(Path.Combine(disk,"small","b.bin"),new byte[1000]);
            left.Navigate(disk,false);await left.NavigationTask;SetActive(left);await left.AnalyzeDiskUsage();UpdateLayout();
            var entries=left.List.Items.Cast<Entry>().Where(x=>!x.IsParent).ToArray();
            check(entries.Single(x=>x.FullName=="large").Bytes==3000 && Math.Abs(entries.Single(x=>x.FullName=="large").DiskPercent-75)<.01,"disk analysis computes folder totals and a 75-percent share");
            check(AdvancedToolsTests.Visual<ProgressBar>(left).Any(x=>x.IsVisible && x.Maximum==100),"disk analysis renders percentage bars in the file panel");
            NextStageTests.Draw(this,Path.Combine(root,"disk-usage.png"));left.ToggleDiskUsage();
            check(entries.All(x=>x.DiskUsageVisibility==Visibility.Collapsed),"second disk-usage command clears percentage bars");
            File.WriteAllText(Path.Combine(root,"sync-left","ui-only-left.txt"),"preview");
            var sync=new DirectorySyncWindow(this,Path.Combine(root,"sync-left"),Path.Combine(root,"sync-right"),true){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
            sync.Show();var mode=FluentUiTests.Children<ComboBox>(sync).Single(x=>x.Items.Count==3 && x.Items.Cast<object>().Any(y=>(y as string)==L.Text("sync.bidirectional")));mode.SelectedIndex=2;await sync.Compare();sync.UpdateLayout();
            check(AdvancedToolsTests.Visual<ComboBox>(sync).Any(x=>x.Items.Cast<object>().Any(y=>(y as string)=="→" || (y as string)=="←")),"synchronization preview exposes per-file action choices");
            var action = AdvancedToolsTests.Visual<ComboBox>(sync).First(x=>x.Items.Cast<object>().Any(y=>(y as string)=="→"));
            action.SelectedItem = L.Text("sync.skipAction");
            check(((SyncRow)action.DataContext).Action==L.Text("action.keep"),"changing a row dropdown updates its planned action");
            action.SelectedItem = "→";
            NextStageTests.Draw(sync,Path.Combine(root,"bidirectional-sync.png"));sync.Close();
            string image=Path.Combine(root,"images","picture.png");var images=new ImageToolsDialog(this,new[]{image},Path.GetDirectoryName(image)){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};images.Show();images.UpdateLayout();NextStageTests.Draw(images,Path.Combine(root,"image-tools.png"));images.Close();
            var archive=new ArchivePackDialog(this,1,Path.Combine(root,"options.zip")){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};archive.Show();archive.UpdateLayout();
            check(FluentUiTests.Children<CheckBox>(archive).Single(x=>(x.Content as string)==L.Text("archive.multiVolume")).IsEnabled && FluentUiTests.Children<CheckBox>(archive).Single(x=>(x.Content as string)==L.Text("archive.moveInto")).IsEnabled,"archive dialog enables volume and verified-move options");
            NextStageTests.Draw(archive,Path.Combine(root,"archive-options.png"));archive.Close();
        }
    }
}
