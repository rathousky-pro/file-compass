using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class DiskAnalyzerTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();DiskAnalyzerWindow window=null; string boundaryFixture=null;
            string originalLanguage=L.Language, languageFolder=AppPaths.Resource("languages"), originalTheme=AppearanceTheme.Mode;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);checks.Add(label);};
            try {
                FluentTheme.Ensure();var clock=new DateTime(2026,10,22,12,0,0,DateTimeKind.Utc);
                string fixture=Path.Combine(root,"fixture-"+Guid.NewGuid().ToString("N")), scope=Path.Combine(fixture,"selected"), child=Path.Combine(scope,"child"), deep=Path.Combine(child,"deep");Directory.CreateDirectory(deep);Directory.CreateDirectory(Path.Combine(scope,"empty"));
                Action<string,long,int> file=(path,size,age)=>{using(var stream=new FileStream(path,FileMode.Create,FileAccess.Write))stream.SetLength(size);File.SetLastWriteTimeUtc(path,clock.AddDays(-age));};
                file(Path.Combine(fixture,"outside.bin"),9000,0);file(Path.Combine(scope,"a.TXT"),100,1);file(Path.Combine(child,"b.txt"),2000,10);file(Path.Combine(deep,"c.bin"),3000000,50);file(Path.Combine(deep,"no-extension"),0,400);
                var result=DiskAnalysis.Scan(scope,CancellationToken.None,null,clock);
                check(result.Root.Bytes==3002100&&result.Root.Files==4,"Only the selected subfolder is scanned; all nested levels are included");
                check(result.Folders==4&&result.Root.Children.Single(x=>x.Path==child).Bytes==3002000,"Directory totals propagate through the full tree including empty folders");
                check(result.Root.DirectBytes==100&&result.Root.DirectFiles==1,"Direct files are separate from recursive folder totals");
                check(result.Extensions[".txt"].Bytes==2100&&result.Extensions[".txt"].Files==2,"Extensions are grouped case insensitively");
                check(result.Extensions.Values.Sum(x=>x.Files)==4&&result.Extensions.Values.Sum(x=>x.Bytes)==result.Root.Bytes,"Extension totals match the scan");
                check(result.Ages[0].Files==1&&result.Ages[1].Files==1&&result.Ages[2].Files==1&&result.Ages[4].Files==1,"Age groups use last modification and include empty files");
                check(result.Sizes[0].Files==2&&result.Sizes[1].Files==1&&result.Sizes[2].Files==1,"File-size groups use binary size boundaries");
                check(result.Largest.Count==4&&result.Largest[0].Bytes==3000000,"Largest files are sorted by size");
                var cancelled=new CancellationTokenSource();cancelled.Cancel();var partial=DiskAnalysis.Scan(scope,cancelled.Token,null,clock);check(partial.Cancelled&&partial.Root.Files==0,"Cancellation returns explicitly partial results");
                bool invalid=false;try{DiskAnalysis.Scan(Path.Combine(fixture,"missing"),CancellationToken.None,null);}catch(DirectoryNotFoundException){invalid=true;}check(invalid,"A missing scan root is reported");
                string many=Path.Combine(fixture,"many");Directory.CreateDirectory(many);for(int i=0;i<140;i++)file(Path.Combine(many,"f"+i+".bin"),i,0);
                var top=DiskAnalysis.Scan(many,CancellationToken.None,null,clock);check(top.Largest.Count==100&&top.Largest[0].Bytes==139&&top.Largest[99].Bytes==40,"Only the largest 100 file records are retained while all files are counted");
                check(top.Root.Files==140&&top.Root.Bytes==9730,"Top-file limit does not truncate aggregate totals");
                string boundaries=boundaryFixture=Path.Combine(fixture,"boundaries");Directory.CreateDirectory(boundaries);long[] sizes={0,1024,1048576,104857600,1073741824L,10737418240L};int[] ages={0,7,30,90,365,1095};
                // Sparse test files avoid allocating the logical 11 GiB boundary fixture.
                for(int i=0;i<6;i++) { string name=Path.Combine(boundaries,i+".dat");using(var stream=new FileStream(name,FileMode.Create,FileAccess.Write)){uint returned;if(!DeviceIoControl(stream.SafeFileHandle,0x900c4,IntPtr.Zero,0,IntPtr.Zero,0,out returned,IntPtr.Zero)) throw new IOException("Sparse fixture setup failed");stream.SetLength(sizes[i]);}File.SetLastWriteTimeUtc(name,clock.AddDays(-ages[i])); }
                var edges=DiskAnalysis.Scan(boundaries,CancellationToken.None,null,clock);check(edges.Ages.All(x=>x.Files==1)&&edges.Sizes.All(x=>x.Files==1),"Exact age and size boundaries fall into the next bucket");
                string junction=Path.Combine(scope,"external-link");var link=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+junction+"\" \""+fixture+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});link.WaitForExit();
                check(link.ExitCode==0,"Recursive junction fixture can be created");
                var linked=DiskAnalysis.Scan(scope,CancellationToken.None,null,clock);check(linked.Root.Bytes==result.Root.Bytes&&linked.Skipped==1&&linked.Issues.Any(x=>x.Contains(junction)),"Junctions are skipped, reported, and cannot escape the root or form cycles");
                AppearanceTheme.Apply("Light");window=new DiskAnalyzerWindow(null,scope,null){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();NavigationTests.Wait(window.ScanTask);window.UpdateLayout();
                check(window.Result!=null&&window.Result.Root.Files==4,"Analyzer window starts scanning the supplied current folder automatically");
                for(int group=0;group<4;group++)for(int metric=0;metric<2;metric++) {window.GroupChoice.SelectedIndex=group;window.MetricChoice.SelectedIndex=metric;window.UpdateChart();check(Math.Abs(window.Chart.Rows.Sum(x=>x.Value)-(metric==0?result.Root.Bytes:result.Root.Files))<.01,"Chart totals match recursive scope: group "+group+", metric "+metric);}
                window.GroupChoice.SelectedIndex=2;window.MetricChoice.SelectedIndex=0;window.ChartChoice.SelectedIndex=0;window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"age-pie.png"));
                window.ChartChoice.SelectedIndex=1;check(!window.Chart.Pie,"Bar and pie chart switch shares the same data");window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"age-bars.png"));
                var treeRoot=(TreeViewItem)window.Folders.Items[0];var childItem=treeRoot.Items.Cast<TreeViewItem>().Single(x=>((DiskFolder)x.Tag).Path==child);window.GroupChoice.SelectedIndex=0;childItem.IsSelected=true;
                check(window.Chart.Rows.Sum(x=>x.Bytes)==3002000,"Selecting a nested tree folder drills the folder chart into that folder");
                var originalResult=window.Result;var synthetic=new DiskAnalysis{Root=new DiskFolder{Path=scope,Bytes=140,Files=14}};for(int i=0;i<14;i++)synthetic.Extensions[".type"+i]=new DiskBucket{Name=".type"+i,Bytes=10,Files=1};
                window.Result=synthetic;window.GroupChoice.SelectedIndex=1;window.UpdateChart();check(window.Chart.Rows.Count==10&&window.Chart.Rows.Sum(x=>x.Bytes)==140&&window.Chart.Rows.Last().Files==5,"Small categories are grouped into Other without losing totals");
                window.Result=new DiskAnalysis{Root=new DiskFolder{Path=scope}};window.UpdateChart();window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"empty.png"));check(window.Chart.Rows.Count==0,"Empty folders produce an empty chart without invalid percentages");
                window.Result=originalResult;window.GroupChoice.SelectedIndex=0;window.UpdateChart();AppearanceTheme.Apply("Dark");window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"dark.png"));window.Close();window=null;
                L.LoadForTests(languageFolder,"en");check(L.Text("analyzer.title")=="Disk space analyzer"&&L.Text("analyzer.byAge")=="By age","Analyzer labels are available in English");
                window=new DiskAnalyzerWindow(null,scope,null){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Width=800,Height=560};window.Show();NavigationTests.Wait(window.ScanTask);window.UpdateLayout();NextStageTests.Draw(window,Path.Combine(root,"english-small.png"));
                check(window.Chart.ActualWidth>200&&window.Chart.ActualHeight>=100,"Compact window retains a usable chart");
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            } catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}
            finally{if(boundaryFixture!=null)for(int i=0;i<6;i++){string file=Path.Combine(boundaryFixture,i+".dat");if(File.Exists(file))File.Delete(file);}if(window!=null)window.Close();AppearanceTheme.Apply(originalTheme);L.LoadForTests(languageFolder,originalLanguage);}
        }
        internal static void CheckFolderAlignment(DiskAnalyzerWindow window,string root,Action<bool,string> check) {
            var treeRoot=(TreeViewItem)window.Folders.Items[0];
            foreach(TreeViewItem child in treeRoot.Items)if(child.Tag!=null)child.IsExpanded=true;
            NavigationTests.Wait(System.Threading.Tasks.Task.Delay(50));window.UpdateLayout();
                var folderRows=AdvancedToolsTests.Visual<System.Windows.Controls.Grid>(window.Folders).Where(x=>Convert.ToString(x.Tag)=="analyzer.folderRow").ToArray();
                check(folderRows.Length>=4,"Folder rows retain lazy expansion and display nested folders");
                Func<int,double[]> rightEdges=column=>folderRows.Select(row=>((TextBlock)row.Children[column]).TranslatePoint(new Point(((TextBlock)row.Children[column]).ActualWidth,0),window.Folders).X).ToArray();
                var sizeEdges=rightEdges(1);var countEdges=rightEdges(2);
                check(sizeEdges.Max()-sizeEdges.Min()<1&&countEdges.Max()-countEdges.Min()<1,"Size and file-count columns align across parent, child and grandchild rows");
                check(folderRows.All(row=>row.ColumnDefinitions.Count==3)&&ScrollViewer.GetHorizontalScrollBarVisibility(window.Folders)==ScrollBarVisibility.Disabled,"Folder names use a separate flexible column without horizontal overflow");
                NextStageTests.Draw(window,Path.Combine(root,"aligned-folders.png"));

        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle handle,uint code,IntPtr input,uint inputSize,IntPtr output,uint outputSize,out uint returned,IntPtr overlapped);
    }
}
