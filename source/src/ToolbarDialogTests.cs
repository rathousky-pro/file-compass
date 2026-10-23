using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CommanderCZ {
internal static class ToolbarDialogTests {
internal static int Run(string root) {
Directory.CreateDirectory(root); var checks=new List<string>(); MainWindow main=null;
Action<bool,string> check=delegate(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);};
try {
var eta=new TransferRateEstimate();check(!eta.Remaining(1048576,1,30*1048576).HasValue,"ETA waits for a meaningful measurement");double? estimate=null;for(int i=1;i<=10;i++)estimate=eta.Remaining(i*1048576L,i*2,30*1048576);check(estimate>=50&&estimate<=70,"ETA includes per-file gaps instead of wire-speed spikes");check(eta.Remaining(10*1048576,22,30*1048576)>=estimate,"ETA increases when transfer stalls between files");
var dense=new CompressionRatioTrend();for(int n=0;n<=1000;n++)dense.Add(50+n/30.0,n/1000.0);check(dense.SampleCount>100&&dense.Completion==1,"dense progress updates retain the connected full-operation history");
System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
string a=Path.Combine(root,"left"), b=Path.Combine(root,"right"); Directory.CreateDirectory(a);Directory.CreateDirectory(b);File.WriteAllText(Path.Combine(a,"one.txt"),"one"); File.WriteAllText(Path.Combine(a,"two.txt"),"two");
check(NetworkTransport.Session.OwnedStagePath("/data/.FileCompass-upload-id.part",null)=="/data/.FileCompass-upload-id.part","new partial uses FileCompass branding");
check(NetworkTransport.Session.OwnedStagePath("/data/.FileCompass-upload-id.part","/data/.CommanderCZ-upload-id.part")=="/data/.CommanderCZ-upload-id.part","legacy owned upload remains resumable");
check(NetworkTransport.Session.OwnedStagePath("/data/.FileCompass-relay-id.part","/else/.CommanderCZ-relay-id.part")=="/data/.FileCompass-relay-id.part","legacy path fallback rejects another directory");
main=new MainWindow(a,new ProgramSettings{AutoRefresh=false}){Width=1500,Height=850,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
var panels=FluentUiTests.Children<FilePanel>(main).ToArray();var left=panels[0];var right=panels[1];Directory.CreateDirectory(Path.Combine(a,"returned"));left.Navigate(Path.Combine(a,"returned"),true);NavigationTests.Wait(left.NavigationTask);left.Up();NavigationTests.Wait(left.NavigationTask);check(((Entry)left.List.SelectedItem).FullPath==Path.Combine(a,"returned"),"up selects the folder just exited");left.Navigate(Path.Combine(a,"returned"),true);NavigationTests.Wait(left.NavigationTask);left.Back();NavigationTests.Wait(left.NavigationTask);check(((Entry)left.List.SelectedItem).FullPath==Path.Combine(a,"returned"),"back selects the folder just exited");right.Navigate(b,true);NavigationTests.Wait(right.NavigationTask);
var invert=FluentUiTests.Children<Button>(main).First(x=>Convert.ToString(x.ToolTip)==L.Text("selection.invertMarks")+" (Num *)");
left.MarkAll(false);invert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(left.List.Items.Cast<Entry>().Where(x=>!x.IsParent).All(x=>x.IsMarked),"toolbar inverts unmarked files");invert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(left.List.Items.Cast<Entry>().All(x=>!x.IsMarked),"second inversion restores marks");
main.OpenFolderInOtherPanel(left);NavigationTests.Wait(right.NavigationTask);check(right.CurrentPath==a,"left to right folder command");right.Navigate(b,true);NavigationTests.Wait(right.NavigationTask);main.OpenFolderInOtherPanel(right);NavigationTests.Wait(left.NavigationTask);check(left.CurrentPath==b,"right to left folder command");
foreach(string mode in new[]{"Light","Dark"}) {
AppearanceTheme.Apply(mode);AppearanceTheme.SetBackdrop("MicaAlt");
var queue=new NetworkQueueWindow(main,delegate{return new NetworkTransferJob[0];},null);queue.Show();queue.UpdateLayout();check(queue.ActualHeight<360,"empty network queue compact "+mode);check(FluentUiTests.Children<GroupBox>(queue).Count()==3,"network queue has inner surface "+mode);NextStageTests.Draw(queue,Path.Combine(root,"network-"+mode+".png"));queue.Close();
var pendingJob=new NetworkTransferJob(new NetworkProfile { Name="Test SFTP" },true);
var busy=new NetworkQueueWindow(main,delegate{return new[]{pendingJob};},pendingJob);busy.Show();busy.UpdateLayout();check(FluentUiTests.Children<Button>(busy).Count(x=>Convert.ToString(x.Content)==L.Text("copy.backgroundAction"))==1 && !FluentUiTests.Children<Button>(busy).Any(x=>Convert.ToString(x.Content)==L.Text("action.close")),"network queue has a single background action instead of close "+mode);check(busy.Width>700,"network queue expands for transfers "+mode);pendingJob.Progress("/test.bin",.5,40*1048576);NavigationTests.Wait(System.Threading.Tasks.Task.Delay(550));check(FluentUiTests.Children<CompressionRatioTrend>(busy).Single().Current==40,"network speed chart follows selected job metrics "+mode);NextStageTests.Draw(busy,Path.Combine(root,"network-busy-"+mode+".png"));var speedChoice=FluentUiTests.Children<ComboBox>(busy).First(x=>x.IsEditable);double beforeHeight=busy.ActualHeight;speedChoice.IsDropDownOpen=true;NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));check(busy.ActualHeight==beforeHeight,"speed presets popup leaves layout height unchanged "+mode);check(speedChoice.Items.Count>=8,"speed offers multiple editable presets "+mode);speedChoice.IsDropDownOpen=false;speedChoice.Text="12345";check(speedChoice.Text=="12345","speed accepts a custom numeric value "+mode);FluentUiTests.Children<Button>(busy).First(x=>Convert.ToString(x.Content)==L.Text("action.set")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(pendingJob.SpeedLimit==12345L*1024,"custom speed is applied to the selected transfer "+mode);
typeof(NetworkQueueWindow).GetField("completing",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(busy,true);busy.Close();
var setup=new CopySetupDialog(main,new[]{new Entry { FullPath=Path.Combine(a,"one.txt"),Name="one.txt",Bytes=3 }},b,new string[0],false,new ProgramSettings()); setup.Show();setup.UpdateLayout();check(setup.ActualHeight<600,"copy setup compact "+mode);check(setup.CreateOptions(false).Target.TrimEnd('\\')==b.TrimEnd('\\'),"copy setup preserves destination "+mode);NextStageTests.Draw(setup,Path.Combine(root,"setup-"+mode+".png"));var advanced=FluentUiTests.Children<Expander>(setup).First();advanced.IsExpanded=true;NavigationTests.Wait(System.Threading.Tasks.Task.Delay(350));setup.UpdateLayout();check(advanced.ActualHeight>150,"advanced copy options reveal controls "+mode);check(setup.ActualHeight<=setup.MaxHeight,"advanced copy options fit screen "+mode);NextStageTests.Draw(setup,Path.Combine(root,"setup-expanded-"+mode+".png"));setup.Close();
using(var cancellation=new System.Threading.CancellationTokenSource()) {
var archive=new ArchiveProgressDialog(main,"Test komprimace",cancellation);archive.Show();archive.UpdateCurrentFile("video.mp4");archive.Update("Komprimování",.5,true);
foreach(long packed in new[]{80L,115L,65L}){archive.UpdateCompression(new ArchiveCompressionProgress {Original=200,Processed=100,Packed=packed,Percent=packed==80?5:packed==115?50:95});NavigationTests.Wait(System.Threading.Tasks.Task.Delay(550));}
archive.UpdateLayout();var trend=FluentUiTests.Children<CompressionRatioTrend>(archive).Single();check(trend.SampleCount==3&&trend.Completion==.95&&trend.Current==65&&trend.ScaleMaximum>=115,"compression trend retains growth above 100 and a later decrease "+mode);check(archive.ActualHeight<=archive.MaxHeight,"compression trend dialog fits screen "+mode);NextStageTests.Draw(archive,Path.Combine(root,"ratio-"+mode+".png"));archive.Finish();
}
var confirm=new DeleteConfirmationDialog(main,new List<Entry>{new Entry{FullPath=Path.Combine(a,"one.txt"),Name="one.txt"}},true,false,a);confirm.Show();confirm.UpdateLayout();check(confirm.ActualHeight<360,"deletion confirmation compact "+mode);NextStageTests.Draw(confirm,Path.Combine(root,"delete-"+mode+".png"));confirm.Close();
var control=new CopyControl();var progress=new CopyProgressDialog(main,control);progress.Show();
foreach(long rate in new[]{20L,80L,35L}) { progress.Update(new CopyUpdate {Source="soubor.bin",Target="cil.bin",BytesPerSecond=rate*1048576,TotalDone=(rate==35?95:rate)*1048576,TotalBytes=104857600,FileTotal=104857600,TotalFiles=1,Phase=L.Code("copy.title")});NavigationTests.Wait(System.Threading.Tasks.Task.Delay(550)); }
var speedGraph=FluentUiTests.Children<CompressionRatioTrend>(progress).Single();check(speedGraph.Completion==.95&&speedGraph.Current==35&&speedGraph.SampleCount>=3&&speedGraph.ScaleMaximum>=80,"copy speed graph follows measured changes in MB/s "+mode);
progress.UpdateLayout();check(progress.ActualHeight>=290 && progress.ActualHeight<=progress.MaxHeight,"copy progress fits fixed rows and inner surface "+mode);NextStageTests.Draw(progress,Path.Combine(root,"copy-"+mode+".png"));progress.Finish();
var activities=(StackPanel)typeof(MainWindow).GetField("copyActivities",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
activities.Children.Add(new CopyActivity("Mazání: one.txt",delegate{}));
typeof(MainWindow).GetMethod("ShowActivityDetails",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);
var details=Application.Current.Windows.Cast<Window>().First(x=>x!=main && x.Title==L.Text("queue.progressAndQueue"));details.UpdateLayout();check(details.ActualHeight<300,"single deletion activity fits compact details "+mode);NextStageTests.Draw(details,Path.Combine(root,"activity-"+mode+".png"));details.Close();activities.Children.Clear();
Exception modalError=null;
main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate {var w=Application.Current.Windows.Cast<Window>().First(x=>x!=main && x.Title==L.Text("queue.copyTitle"));try{w.UpdateLayout();NextStageTests.Draw(w,Path.Combine(root,"queue-"+mode+".png"));check(w.ActualHeight<360,"empty local queue compact "+mode+" height="+w.ActualHeight+" min="+w.MinHeight+" size="+w.SizeToContent+" desired="+((FrameworkElement)w.Content).DesiredSize);}catch(Exception ex){modalError=ex;}finally{w.Close();}}));
typeof(MainWindow).GetMethod("ShowCopyQueue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);if(modalError!=null)throw modalError;
var jobType=typeof(MainWindow).GetNestedType("CopyJob",BindingFlags.NonPublic);
Func<string,object> makeJob=delegate(string filename){var j=Activator.CreateInstance(jobType,true);jobType.GetField("Sources").SetValue(j,new[]{Path.Combine(a,filename)});jobType.GetField("Options").SetValue(j,new CopyOptions { Target=b });return j;};
var runningField=typeof(MainWindow).GetField("runningCopyJob",BindingFlags.Instance|BindingFlags.NonPublic);runningField.SetValue(main,makeJob("running.bin"));
var activeField=typeof(MainWindow).GetField("copyRunning",BindingFlags.Instance|BindingFlags.NonPublic);activeField.SetValue(main,true);
var queueObject=typeof(MainWindow).GetField("copyJobs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);queueObject.GetType().GetMethod("Enqueue").Invoke(queueObject,new[]{makeJob("waiting.bin")});
main.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{var w=Application.Current.Windows.Cast<Window>().First(x=>x!=main && x.Title==L.Text("queue.copyTitle"));try{w.UpdateLayout();check(FluentUiTests.Children<GroupBox>(w).Count()==2,"running and waiting queues have separate panels "+mode);check(FluentUiTests.Children<ListBox>(w).Any(x=>x.Items.Count==1),"queued job appears in waiting list "+mode);check(FluentUiTests.Children<TextBlock>(w).Any(x=>x.Text.Contains("running.bin")),"active source appears in running panel "+mode);NextStageTests.Draw(w,Path.Combine(root,"queue-populated-"+mode+".png"));}catch(Exception ex){modalError=ex;}finally{w.Close();}}));
typeof(MainWindow).GetMethod("ShowCopyQueue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(main,null);queueObject.GetType().GetMethod("Clear").Invoke(queueObject,null);activeField.SetValue(main,false);runningField.SetValue(main,null);if(modalError!=null)throw modalError;

}
File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count}));return 0;
}catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}finally{if(main!=null)main.Close();}
}
}}
