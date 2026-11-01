#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace FileCompass {
 internal static class NetworkProgressUxTests {
  static void Open(Window w){w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=-18000;w.Top=-18000;w.ShowActivated=false;w.ShowInTaskbar=false;w.Show();NavigationTests.Wait(Task.Delay(130));w.UpdateLayout();}
  static Button FindButton(Window w,string key){return FluentUiTests.Children<Button>(w).Single(x=>(string)x.Content==L.Text(key));}
  static NetworkTransferJob Job(bool download, bool relay=false){
   var job=new NetworkTransferJob(new NetworkProfile{Name="KLADNO",Protocol=NetworkProtocol.FTP,Host="fixture.invalid"},download,relay?new NetworkProfile{Name="Záloha",Protocol=NetworkProtocol.SFTP,Host="fixture.invalid"}:null);
   var batch=new NetworkCopyBatch{Entry=new Entry{FullPath=download?"/Media/Video.mp4":@"F:\Video.mp4"},Destination=download?@"E:\Záloha":"/Media",Bytes=200};
   batch.Items.Add(new NetworkQueueItem{Source=batch.Entry.FullPath,Destination=download?@"E:\Záloha\Video.mp4":"/Media/Video.mp4",Bytes=100,State=L.Code("state.queued")});
   batch.Items.Add(new NetworkQueueItem{Source="/next.bin",Destination="/backup/next.bin",Bytes=100,State=L.Code("state.queued")});
   job.AddPrepared(new[]{batch});job.Begin(batch);job.Progress(batch.Entry.FullPath,.54,134217728,null,relay?batch.Items[0].Destination:null);return job;
  }
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();string motif=AppearanceTheme.Motif;var windows=new List<NetworkQueueWindow>();var allJobs=new List<NetworkTransferJob>();Action<bool,string> check=(yes,text)=>{if(!yes)throw new Exception(text);checks.Add(text);};try{SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs");FluentTheme.Ensure();AppearanceTheme.Apply("Light");
   foreach(string style in new[]{"Original","Balanced"}){AppearanceTheme.SetMotif(style);var first=Job(false);var second=Job(true,true);allJobs.Add(first);allJobs.Add(second);var list=new List<NetworkTransferJob>{first};var w=new NetworkQueueWindow(null,()=>list.ToArray(),first);windows.Add(w);Open(w);
    var bars=FluentUiTests.Children<LocalCopyProgressBar>(w).OrderByDescending(x=>x.Height).ToArray();check(bars.Length==2&&bars[0].Height==8&&bars[1].Height==3,style+": shared total and file progress hierarchy");
    var details=FluentUiTests.Children<Expander>(w).Single();check(!details.IsExpanded&&!FluentUiTests.Children<CompressionRatioTrend>(w).Single().IsVisible,style+": speed graph starts inside collapsed details");
    check(FluentUiTests.Children<TextBlock>(w).Any(x=>x.FontSize==32&&x.Text=="27 %"),style+": total progress uses total bytes rather than file percentage");
    check(first.Update().CurrentDestination=="/Media/Video.mp4"&&first.Update().BytesDone==54&&first.Update().TotalFiles==2,style+": structured report preserves current target and totals");
    NextStageTests.Draw(w,Path.Combine(root,style+"-network.png"));
    FindButton(w,"action.pause").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(first.Paused,style+": pause controls selected network job");FindButton(w,"action.resume").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!first.Paused,style+": resume restores selected network job");
    var limit=FluentUiTests.Children<ComboBox>(w).Single();limit.Text="1024";FindButton(w,"action.set").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(first.SpeedLimit==1048576,style+": speed limit remains functional");
    details.IsExpanded=true;w.UpdateLayout();check(FluentUiTests.Children<CompressionRatioTrend>(w).Single().IsVisible,style+": expanded details show speed graph");NextStageTests.Draw(w,Path.Combine(root,style+"-network-details.png"));
    list.Add(second);w.SelectJob(second);w.UpdateLayout();var selector=FluentUiTests.Children<ListBox>(w).First(x=>!(x is ListView));check(selector.IsVisible&&selector.Items.Count==2&&selector.SelectedItem==second,style+": simultaneous transfers remain selectable");check(second.Update().CurrentDestination==@"E:\Záloha\Video.mp4",style+": relay reports exact destination");check(limit.Text=="0",style+": switching transfer restores its own speed limit");
    w.SelectJob(first);w.UpdateLayout();NextStageTests.Draw(w,Path.Combine(root,style+"-network-multiple.png"));
    FindButton(w,"copy.backgroundAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!w.IsVisible,style+": background action hides window without cancelling transfer");check(!first.Cancellation.IsCancellationRequested,style+": background preserves active job");w.RestoreProgress();
    first.Finish(L.Code("state.completed"));second.Finish(L.Code("state.completed"));NavigationTests.Wait(Task.Delay(500));check(!w.IsVisible,style+": completion closes queue window");windows.Remove(w);
   }
   var empty=new NetworkQueueWindow(null,()=>new NetworkTransferJob[0],null);windows.Add(empty);Open(empty);check(FluentUiTests.Children<LocalCopyProgressBar>(empty).All(x=>!x.IsVisible),"empty queue does not show transfer progress");NextStageTests.Draw(empty,Path.Combine(root,"empty.png"));empty.Close();windows.Remove(empty);
   File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{foreach(var j in allJobs)j.Finish(L.Code("state.completed"));foreach(var w in windows)w.Close();foreach(var j in allJobs)j.Cancellation.Dispose();AppearanceTheme.SetMotif(motif);}}
 }
}
#endif
