#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;
namespace FileCompass {
 internal static class MediaProgressEtaTests {
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();MediaConverterWindow window=null;Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);checks.Add(text);};
   try{
    var batch=new MediaBatchEstimate(new[]{60.0,60,60,60},4);for(int i=0;i<4;i++){batch.Start(i,0);batch.Update(i,.5);}check(!batch.Remaining(1).HasValue,"ETA waits for measurement warmup");check(Math.Abs(batch.Remaining(10).Value-10)<.001,"four concurrent files use per-worker progress rather than four serial durations");
    var tail=new MediaBatchEstimate(new[]{100.0,10,10,10},4);for(int i=0;i<4;i++)tail.Start(i,0);for(int i=1;i<4;i++)tail.Finish(i,10,true);tail.Update(0,.5);check(Math.Abs(tail.Remaining(50).Value-50)<.001,"a long final file retains its own remaining duration after other workers finish");
    var queued=new MediaBatchEstimate(new[]{100.0,100,100,100},2);queued.Start(0,0);queued.Start(1,0);queued.Update(0,.5);queued.Update(1,.5);check(Math.Abs(queued.Remaining(50).Value-150)<.001,"ETA schedules queued files onto available worker slots");
    var weighted=new MediaBatchEstimate(new[]{10.0,90},2);weighted.Start(0,0);weighted.Finish(0,10,true);check(Math.Abs(weighted.Fraction-.1)<.001,"overall progress weights original duration instead of equal file counts");weighted.Start(1,10);weighted.Update(1,.99);check(weighted.Finishing,"finishing state avoids a misleading countdown during final verification");weighted.Finish(1,100,true);check(weighted.Remaining(100)==0&&weighted.Fraction==1&&!weighted.Finishing,"finished batches have no remaining work");
    var errors=new MediaBatchEstimate(new[]{100.0,100},1);errors.Start(0,0);errors.Finish(0,1,false);errors.Start(1,1);check(!errors.Remaining(5).HasValue,"fast failures do not inflate the measured conversion speed");check(MediaBatchEstimate.Time(90061)=="25:01:01","remaining duration supports more than twenty-four hours");
    System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();
    foreach(string language in new[]{"cs-CZ","en","de","es","fr","nl","pl","pt"}){
     L.ChangeLanguage(language);window=new MediaConverterWindow(null,new string[0],root,false,delegate{},delegate{}){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NavigationTests.Wait(window.AnalysisTask);
     for(int i=0;i<8;i++)window.Rows.Add(new MediaRow{Path=Path.Combine(root,"long-name-audio-"+i+".mp3"),Summary="mp3 · 2 ch · 48000 Hz · 01:15:58",Status=L.Text("media.converting"),Progress=i==0?100:24,Image=false});
     window.RemainingTime.Text=L.Format("media.etaRemaining","00:12:34");window.RemainingTime.Visibility=Visibility.Visible;var bar=AdvancedToolsTests.Visual<ProgressBar>(window).Single();bar.Visibility=Visibility.Visible;bar.Value=40;
     var queue=AdvancedToolsTests.Visual<DataGrid>(window).Single();
     foreach(int width in new[]{1180,780}){window.Width=width;window.Height=width==780?540:820;queue.FontSize=width==780?16:12;window.UpdateLayout();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(80));window.UpdateLayout();
      check(queue.Columns.Count==5&&((System.Windows.Data.Binding)((DataGridTextColumn)queue.Columns[4]).Binding).Path.Path=="ProgressText",language+" "+width+": percentages have a separate right column");
      check(queue.Columns.Sum(x=>x.ActualWidth)<=queue.ActualWidth-6&&!AdvancedToolsTests.Visual<ScrollViewer>(queue).Any(x=>x.ComputedHorizontalScrollBarVisibility==Visibility.Visible),language+" "+width+": right percentage column stays inside the viewport without horizontal scrolling");
      if(language=="cs-CZ")NextStageTests.Draw(window,Path.Combine(root,"layout-before-check-"+width+".png"));var percent=AdvancedToolsTests.Visual<TextBlock>(queue).First(x=>x.IsVisible&&x.Text=="100 %");percent.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));check(percent.TextTrimming==TextTrimming.None&&percent.ActualWidth>=percent.DesiredSize.Width-percent.Margin.Left-percent.Margin.Right-1,language+" "+width+": 100 percent fits without ellipsis (actual="+percent.ActualWidth+", desired="+percent.DesiredSize.Width+", font="+percent.FontSize+", column="+queue.Columns[4].ActualWidth+", queueHeight="+queue.ActualHeight+")");
      if(queue.ActualHeight<64)NextStageTests.Draw(window,Path.Combine(root,language+"-too-short.png"));check(queue.ActualHeight>=64&&window.RemainingTime.IsVisible,language+" "+width+": ETA preserves a usable queue at compact height");
      if(language=="cs-CZ")NextStageTests.Draw(window,Path.Combine(root,"percent-eta-"+width+".png"));
     }queue.FontSize=20;window.UpdateLayout();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(80));window.UpdateLayout();var largePercent=AdvancedToolsTests.Visual<TextBlock>(queue).First(x=>x.IsVisible&&x.Text=="100 %");largePercent.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));check(largePercent.ActualWidth>=largePercent.DesiredSize.Width-largePercent.Margin.Left-largePercent.Margin.Right-1,language+": live font change preserves full percentages without window resizing");window.Close();window=null;
    }
    L.ChangeLanguage("cs-CZ");File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(window!=null)window.Close();}
  }
 }
}
#endif
