using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
namespace CommanderCZ {
    internal static class DiskAnalyzerUpgradeTests {
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();var indexes=new List<string>();DiskAnalyzerWindow window=null;string language=L.Language,theme=AppearanceTheme.Mode;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
            try{
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));FluentTheme.Ensure();
                string folder=Directory.CreateDirectory(Path.Combine(root,"fixture-"+Guid.NewGuid().ToString("N"))).FullName;string video=Directory.CreateDirectory(Path.Combine(folder,"videos")).FullName,excluded=Directory.CreateDirectory(Path.Combine(folder,"cache")).FullName,docs=Directory.CreateDirectory(Path.Combine(folder,"docs&more")).FullName;DateTime clock=DateTime.UtcNow;
                Action<string,int,int> write=(path,length,age)=>{File.WriteAllBytes(path,new byte[length]);File.SetLastWriteTimeUtc(path,clock.AddDays(-age));};
                write(Path.Combine(video,"old.mp4"),4096,500);write(Path.Combine(video,"recent.mkv"),8192,2);write(Path.Combine(video,"small.mp4"),10,500);write(Path.Combine(excluded,"skip.mp4"),9000,500);write(Path.Combine(docs,"notes.pdf"),2000,10);write(Path.Combine(folder,"future.jpg"),500,-2);
                for(int i=0;i<1100;i++)write(Path.Combine(docs,"file-"+i+".txt"),2,40);
                var options=new DiskScanOptions{StoreIndex=true,Category="video",MinimumBytes=1024,MinimumAgeDays=365,Masks="*.mp4;*.mkv",Exclusions="cache",SnapshotIntervalMilliseconds=0};var partials=new List<DiskAnalysis>();
                var filtered=DiskAnalysis.Scan(folder,CancellationToken.None,null,clock,options,x=>partials.Add(x));indexes.Add(filtered.IndexPath);
                check(filtered.Root.Files==1&&filtered.Root.Bytes==4096,"Category, minimum size, modification age, masks and directory exclusions combine with AND");
                check(partials.Count>0&&partials.All(x=>x.IsPartial)&&partials[0].Root.Files<=filtered.Root.Files,"Live snapshots are emitted before completion and marked partial");
                check(partials.Any(x=>x.Largest.Count>0&&x.Categories.Count>0),"Live snapshots include largest files and category aggregates");
                long indexed=0;check(DiskFileIndex.Page(filtered.IndexPath,x=>true,0,1000,CancellationToken.None,out indexed).Single().Path.EndsWith("old.mp4")&&indexed==1,"Index contains all and only matching files");
                var all=DiskAnalysis.Scan(folder,CancellationToken.None,null,clock,new DiskScanOptions{StoreIndex=true});indexes.Add(all.IndexPath);
                check(all.Root.Files==1106&&all.Categories.Values.Sum(x=>x.Files)==1106,"Default scan includes future timestamps and category totals match");
                var first=DiskFileIndex.Page(all.IndexPath,x=>true,0,1000,CancellationToken.None,out indexed);var second=DiskFileIndex.Page(all.IndexPath,x=>true,1,1000,CancellationToken.None,out indexed);
                check(indexed==1106&&first.Count==1000&&second.Count==106&&!first.Select(x=>x.Path).Intersect(second.Select(x=>x.Path)).Any(),"Paged file list covers every file beyond top 100 without duplicates");
                check(DiskScanOptions.FileCategory("a.cs")=="code"&&DiskScanOptions.FileCategory("a.jpg")=="images"&&DiskScanOptions.FileCategory("a.zip")=="archives","File types map to useful categories");
                var relative=new DiskScanOptions{Exclusions="docs&more"};check(relative.Exclude(folder,docs)&&!relative.Exclude(folder,video),"Folder-name exclusions prune the intended subtree");
                check(new DiskScanOptions{Exclusions=docs}.Exclude(folder,Path.Combine(docs,"nested")),"Absolute exclusions match subtree boundaries");
                var before=DiskAnalysisReports.Capture(all);string snapshot=Path.Combine(root,"before.fca");DiskAnalysisReports.SaveSnapshot(snapshot,all);var loaded=DiskAnalysisReports.LoadSnapshot(snapshot);check(loaded.Folders.Count==before.Folders.Count&&loaded.Root==folder,"Snapshot round trip preserves directory totals and scope");
                write(Path.Combine(video,"growth.mp4"),777,1);var after=DiskAnalysis.Scan(folder,CancellationToken.None,null,clock);var differences=DiskAnalysisReports.Compare(loaded,DiskAnalysisReports.Capture(after));check(differences.Any(x=>x.Path=="videos"&&x.Delta==777)&&differences.Single(x=>x.Path==".").Delta==777,"Comparison detects recursive growth with relative folder paths");
                bool rejected=false;try{DiskAnalysisReports.Compare(loaded,DiskAnalysisReports.Capture(filtered));}catch(IOException){rejected=true;}check(rejected,"Different filter settings cannot be compared as storage growth");
                var unsafeSnapshot=DiskAnalysisReports.Capture(after);unsafeSnapshot.Partial=true;rejected=false;try{DiskAnalysisReports.Compare(loaded,unsafeSnapshot);}catch(IOException){rejected=true;}check(rejected,"Partial scans cannot produce misleading comparisons");
                string csv=Path.Combine(root,"files.csv");DiskAnalysisReports.CsvReport(csv,all,CancellationToken.None);check(File.ReadLines(csv).Count()==1107&&File.ReadAllText(csv).Contains("docs&more"),"CSV exports the full index rather than only top files");
                window=new DiskAnalyzerWindow(null,folder,null){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NavigationTests.Wait(window.ScanTask);indexes.Add(window.Result.IndexPath);window.UpdateLayout();
                check(window.GroupChoice.Items.Count==5,"Analyzer offers category grouping alongside folder, extension, age and size");window.GroupChoice.SelectedIndex=4;window.MetricChoice.SelectedIndex=0;window.UpdateLayout();
                var row=window.Chart.Rows.Single(x=>x.Keys.Contains("analyzer.category.video"));window.SelectChartRow(row);NavigationTests.Wait(window.MatchesTask);check(window.MatchingFiles.Items.Count==5,"Clicking a chart category lists its actual files");
                window.MetricChoice.SelectedIndex=1;window.UpdateLayout();var other=window.Chart.Rows.Single(x=>x.Key=="__other");window.SelectChartRow(other);NavigationTests.Wait(window.MatchesTask);check(window.MatchingFiles.Items.Count==6,"Other slice includes every grouped category, including small video and image groups");
                window.GroupChoice.SelectedIndex=1;window.MetricChoice.SelectedIndex=0;window.UpdateLayout();
                check(window.Chart.Rows.Select(x=>x.Color.ToString()).Distinct().Count()==window.Chart.Rows.Count,"Visible chart categories have distinct colors");
                double radius=Math.Min(window.Chart.ActualWidth,window.Chart.ActualHeight)/2-12,angle=-Math.PI/2+window.Chart.Rows[0].Value/window.Chart.Rows.Sum(x=>x.Value)*Math.PI;var center=new Point(window.Chart.ActualWidth/2,window.Chart.ActualHeight/2);
                check(window.Chart.Hit(center)==null&&window.Chart.Hit(new Point(center.X+radius*.78*Math.Cos(angle),center.Y+radius*.78*Math.Sin(angle))).Key==window.Chart.Rows[0].Key,"Ring hit testing selects the correct segment and ignores the center hole");
                var duplicateButton=AdvancedToolsTests.Visual<System.Windows.Controls.Button>(window).First(x=>Convert.ToString(x.Content)==L.Text("duplicates.title"));duplicateButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var duplicate=Application.Current.Windows.OfType<DuplicateWindow>().Single();check(AdvancedToolsTests.Visual<System.Windows.Controls.TextBox>(duplicate).Any(x=>x.Text==folder),"Duplicate finder opens directly with the analyzed folder");duplicate.Close();
                var bytes=DiskAnalysisReports.Png(window.Chart);File.WriteAllBytes(Path.Combine(root,"chart.png"),bytes);check(bytes.Length>100&&bytes[0]==137&&bytes[1]==80,"Chart exports a valid high resolution PNG");
                DiskAnalysisReports.HtmlReport(Path.Combine(root,"report.html"),window.Result,"Test <report>","Scope & chart",window.Chart.Rows,bytes);string html=File.ReadAllText(Path.Combine(root,"report.html"));check(html.Contains("Test &lt;report&gt;")&&html.Contains("data:image/png;base64,")&&html.Contains("docs&amp;more"),"HTML report escapes paths and embeds the chart");
                var chartSet=window.CaptureReportCharts();DiskAnalysisReports.HtmlReportSet(Path.Combine(root,"full-report.html"),window.Result,chartSet);check(chartSet.Count==5&&System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(root,"full-report.html")),"data:image/png;base64,").Count==5,"HTML report includes all five chart groupings");
                var filterExpander=AdvancedToolsTests.Visual<System.Windows.Controls.Expander>(window).Single(x=>Convert.ToString(x.Header)==L.Text("analyzer.filters"));filterExpander.IsExpanded=true;NavigationTests.Wait(Task.Delay(300));window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"filters-expanded.png"));filterExpander.IsExpanded=false;
                window.FilterCategory.SelectedIndex=2;window.FilterSize.Text="0.001".Replace(".",System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);window.FilterAge.Text="365";window.FilterMasks.Text="*.mp4;*.mkv";window.FilterExclusions.Text="cache";NavigationTests.Wait(window.Scan());check(window.Result.Root.Files==1,"Combined filters can be applied from the actual analyzer controls");
                window.FilterCategory.SelectedIndex=0;window.FilterSize.Text="0";window.FilterAge.Text="0";window.FilterMasks.Text="";window.FilterExclusions.Text="";NavigationTests.Wait(window.Scan());
                var snapshots=new List<DiskAnalysis>();var stopped=new CancellationTokenSource();var cancelled=DiskAnalysis.Scan(folder,stopped.Token,null,clock,new DiskScanOptions{StoreIndex=true,SnapshotIntervalMilliseconds=0},x=>{snapshots.Add(x);if(x.Root.Files>=2)stopped.Cancel();});indexes.Add(cancelled.IndexPath);check(cancelled.Cancelled&&cancelled.Root.Files>=2&&cancelled.Root.Files<1107,"Cancellation retains consistent live partial results");
                check(snapshots.Select(x=>x.Root.Files).SequenceEqual(snapshots.Select(x=>x.Root.Files).OrderBy(x=>x))&&snapshots.All(x=>x.Root.Bytes==x.Categories.Values.Sum(b=>b.Bytes)),"Published live totals are monotonic and internally consistent");
                if(window.MatchesTask!=null)NavigationTests.Wait(window.MatchesTask);check(window.MatchingFiles.Items.Count==1000,"Fast rescan refreshes the visible file page instead of leaving it empty");NextStageTests.Draw(window,Path.Combine(root,"upgraded-light.png"));AppearanceTheme.Apply("Dark");window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"upgraded-dark.png"));window.Close();window=null;
                L.LoadForTests(AppPaths.Resource("languages"),"en");window=new DiskAnalyzerWindow(null,folder,null){Left=-18000,Top=-18000,Width=850,Height=650,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NavigationTests.Wait(window.ScanTask);window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"english-small.png"));check(window.Chart.ActualHeight>=100,"Compact English layout retains usable chart and wrapping actions");
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}
            finally{if(window!=null)window.Close();foreach(string index in indexes)DiskFileIndex.Delete(index);L.LoadForTests(AppPaths.Resource("languages"),language);AppearanceTheme.Apply(theme);}
        }
    }
}
