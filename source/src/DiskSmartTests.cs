using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class DiskSmartTests {
  static bool SmallDesktop {get{return Environment.GetEnvironmentVariable("FILECOMPASS_TEST_SMALL_DESKTOP")=="1";}}
        const string Nvme="{\"device\":{\"protocol\":\"NVMe\"},\"model_name\":\"KINGSTON NVMe — ukázka\",\"serial_number\":\"TEST-SERIAL\",\"firmware_version\":\"TEST1\",\"user_capacity\":{\"bytes\":1024209543168},\"nvme_version\":{\"string\":\"NVMe 1.4\"},\"smart_support\":{\"available\":true},\"smart_status\":{\"passed\":true},\"power_on_time\":{\"hours\":20677},\"power_cycle_count\":511,\"nvme_smart_health_information_log\":{\"critical_warning\":0,\"temperature\":51,\"available_spare\":100,\"available_spare_threshold\":10,\"percentage_used\":6,\"data_units_read\":70500000,\"data_units_written\":110000000,\"host_reads\":123456,\"host_writes\":654321,\"controller_busy_time\":123,\"power_cycles\":511,\"power_on_hours\":20677,\"unsafe_shutdowns\":27,\"media_errors\":0,\"num_err_log_entries\":1092}}";
        const string Ata="{\"model_name\":\"SATA disk — ukázka\",\"user_capacity\":{\"bytes\":2000000000000},\"smart_status\":{\"passed\":true},\"rotation_rate\":7200,\"temperature\":{\"current\":42},\"ata_smart_attributes\":{\"table\":[{\"id\":5,\"name\":\"Reallocated_Sector_Ct\",\"value\":100,\"worst\":100,\"thresh\":10,\"when_failed\":\"-\",\"raw\":{\"value\":0,\"string\":\"0\"}},{\"id\":197,\"name\":\"Current_Pending_Sector\",\"value\":100,\"worst\":100,\"thresh\":0,\"when_failed\":\"-\",\"raw\":{\"value\":0,\"string\":\"0\"}}]}}";
        static double Luminance(System.Windows.Media.Color color) {
            Func<byte,double> linear=x=>{double v=x/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);};
            return .2126*linear(color.R)+.7152*linear(color.G)+.0722*linear(color.B);
        }
        static double Contrast(TextBlock value) {
            var card=System.Windows.LogicalTreeHelper.GetParent(System.Windows.LogicalTreeHelper.GetParent(value)) as Border;
            var foreground=(System.Windows.Media.SolidColorBrush)value.Foreground;var background=(System.Windows.Media.SolidColorBrush)card.Background;
            double a=Luminance(foreground.Color),b=Luminance(background.Color);return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
        }
        static bool NoScroll(DataGrid grid) {
            var scroll=AdvancedToolsTests.Visual<ScrollViewer>(grid).FirstOrDefault();
            return scroll!=null&&scroll.ScrollableHeight<.5&&scroll.ScrollableWidth<.5;
        }
        static SmartDisk ManyAttributes(int count) {
            var disk=SmartDisk.Parse("/dev/sdz","ata",Ata);disk.Data["model_name"]="SMART test disk – "+count+" parameters";disk.Attributes.Clear();
            for(int i=0;i<count;i++)disk.Attributes.Add(new SmartAttribute{Id=(i+1).ToString("X2"),Name=i==8?"SMART parametr s delším názvem výrobce":"SMART parametr "+(i+1),Current="100",Worst="99",Threshold="10",Raw=i==2?"414 (Average 455)":i==count-1?"18446744073709551615":(i*111).ToString(),State=""});
            return disk;
        }
        static void PresentationAndExport(DiskSmartWindow window,string root,Action<bool,string> check) {
            L.ChangeLanguage("cs");LiveLanguage.Refresh();AppearanceTheme.Apply("Light");window.Width=1360;window.Height=900;
            var disk=ManyAttributes(30);window.Disks.ItemsSource=new[]{disk};window.Disks.SelectedIndex=0;
            NavigationTests.Wait(System.Threading.Tasks.Task.Delay(200));window.UpdateLayout();
            check(window.HealthValue.FontSize>=24&&window.TemperatureValue.FontSize>=30,"Health and temperature are prominent");
            check(Contrast(window.HealthValue)>=4.5&&Contrast(window.TemperatureValue)>=4.5,"Health and temperature meet readable contrast in light theme");
            check(window.Attributes.Items.Count+window.AttributesRight.Items.Count==30,"Adaptive tables retain all thirty attributes");
            if(window.ActualWidth>=1300&&window.ActualHeight>=850)check(NoScroll(window.Attributes)&&(!window.SplitAttributes||NoScroll(window.AttributesRight)),"Thirty SATA attributes fit without scrolling at normal window size");else check(window.Attributes.ActualHeight>=100,"A constrained native desktop retains usable SMART rows; actual="+window.ActualWidth+"x"+window.ActualHeight);
            check(window.Snapshot.Information.Count==12&&window.Snapshot.Information.All(x=>x.Value!=null),"All drive properties remain available");
            NextStageTests.Draw(window,Path.Combine(root,"smart-30-light.png"));
            window.Width=1120;window.Height=790;NavigationTests.Wait(System.Threading.Tasks.Task.Delay(180));window.UpdateLayout();
            NextStageTests.Draw(window,Path.Combine(root,"smart-30-compact.png"));
            if(window.ActualWidth>=1100&&window.ActualHeight>=760)check(window.SplitAttributes&&window.Attributes.Items.Count==15&&window.AttributesRight.Items.Count==15,"Standard-width view balances attributes across two tables; split="+window.SplitAttributes+"; width="+window.Attributes.ActualWidth+"; height="+window.Attributes.ActualHeight);else check(window.Attributes.Items.Count+window.AttributesRight.Items.Count==30,"Constrained desktop preserves all thirty SMART attributes");
            foreach(var grid in new[]{window.Attributes,window.AttributesRight}.Where(g=>g.Items.Count>0)){var scroll=AdvancedToolsTests.Visual<ScrollViewer>(grid).First();scroll.ScrollToEnd();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(70));grid.UpdateLayout();var finalAttributeRow=grid.ItemContainerGenerator.ContainerFromIndex(grid.Items.Count-1) as DataGridRow;check(finalAttributeRow!=null&&finalAttributeRow.TranslatePoint(new Point(0,finalAttributeRow.ActualHeight),grid).Y<=grid.ActualHeight+1,"last SMART attribute remains reachable at compact size with the disk picker visible");}
            NextStageTests.Draw(window,Path.Combine(root,"smart-30-compact.png"));
            AppearanceTheme.Apply("Dark");check(Contrast(window.HealthValue)>=4.5&&Contrast(window.TemperatureValue)>=4.5,"Status cards retain contrast in dark theme");NextStageTests.Draw(window,Path.Combine(root,"smart-30-dark.png"));
            var snapshot=window.Snapshot;var report=SmartReports.Build(snapshot);
            var texts=FluentUiTests.Children<TextBlock>(report).Select(t=>t.Text).ToArray();
            check(texts.Contains(disk.Attributes.Last().Raw)&&texts.Contains(disk.Attributes.Last().Name),"Full report includes the final attribute and complete long counter");
            string png=Path.Combine(root,"smart-report.png"),pdf=Path.Combine(root,"smart-report.pdf");
            SmartReports.Png(png,snapshot);SmartReports.Pdf(pdf,snapshot);
            using(var input=File.OpenRead(png)){var image=System.Windows.Media.Imaging.BitmapDecoder.Create(input,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];check(image.PixelWidth==SmartReports.ReportWidth*2&&image.PixelHeight>=report.ActualHeight*2,"PNG contains the entire report at double resolution");}
            using(var document=UglyToad.PdfPig.PdfDocument.Open(pdf)){check(document.NumberOfPages==1&&document.GetPage(1).Width>document.GetPage(1).Height,"Normal PDF report is a valid landscape page");check(document.GetPage(1).GetImages().Any(),"PDF embeds the report image");}
            var many=SmartSnapshot.Create(ManyAttributes(85));var pages=SmartReports.PdfRows(many);
            check(pages.Sum(p=>p.Count)==85&&pages.SelectMany(p=>p).Select(a=>a.Id).Distinct().Count()==85,"PDF page planning includes every attribute exactly once");
            var last=SmartReports.Build(many,pages.Last(),64);check(FluentUiTests.Children<TextBlock>(last).Any(t=>t.Text==many.Attributes.Last().Raw),"Last PDF page includes the final attribute");
            string multi=Path.Combine(root,"smart-report-85.pdf");SmartReports.Pdf(multi,many);
            using(var document=UglyToad.PdfPig.PdfDocument.Open(multi)){check(document.NumberOfPages==3&&Enumerable.Range(1,3).All(i=>document.GetPage(i).GetImages().Any()),"Long results export as a complete three-page PDF");}
            var empty=SmartSnapshot.Create(SmartDisk.Parse("/dev/none","ata","{}"));SmartReports.Pdf(Path.Combine(root,"smart-unknown.pdf"),empty);
            using(var document=UglyToad.PdfPig.PdfDocument.Open(Path.Combine(root,"smart-unknown.pdf"))){check(document.NumberOfPages==1,"Missing SMART data still produces an honest unknown-state report");}
            L.ChangeLanguage("en");LiveLanguage.Refresh();check(window.HealthValue.Text==L.Text("smart.good")&&window.Title==L.Text("smart.title"),"Open SMART summary follows live language changes");
            check(L.Text("smart.exportFilter").Contains("*.png")&&L.Text("smart.exportFilter").Contains("*.pdf"),"Report picker offers both requested formats");
        }


        static void TilesAndClipboard(DiskSmartWindow window,string root,Action<bool,string> check) {
            L.ChangeLanguage("cs");LiveLanguage.Refresh();AppearanceTheme.Apply("Light");window.Width=1120;window.Height=790;
            var good=SmartDisk.Parse("/dev/sdb","nvme",Nvme);good.Letters="C: D:";
            var caution=SmartDisk.Parse("/dev/sda","ata",Ata.Replace("\"passed\":true","\"passed\":false"));caution.Letters="E:";
            var unknown=SmartDisk.Parse("/dev/usb","ata","{}");
            window.SetResults(new List<SmartDisk>{good,caution,unknown});NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();
            check(window.Disks.Items.Count==3&&window.Disks.ItemContainerGenerator.ContainerFromIndex(2)!=null,"All disk tiles are generated in one row");
            var tile=(ListBoxItem)window.Disks.ItemContainerGenerator.ContainerFromIndex(0);var labels=AdvancedToolsTests.Visual<TextBlock>(tile).Select(t=>t.Text).ToArray();
            check(labels.Contains(good.TileHealth)&&labels.Contains("51 °C")&&labels.Contains("C: D:")&&labels.Contains(good.Model),"Tile exposes health, temperature, volumes and model");
            check(unknown.TileTemperature=="—"&&unknown.TileVolumes==unknown.Device,"Unavailable temperature and unmapped devices remain honest");
            var second=(ListBoxItem)window.Disks.ItemContainerGenerator.ContainerFromIndex(1);second.IsSelected=true;window.UpdateLayout();
            check(window.Snapshot.Device==caution.Device&&window.HealthValue.Text==L.Text("smart.bad"),"Selecting a tile switches the detailed report to that disk");
            var frame=(Border)second.Template.FindName("TileFrame",second);
            check(((System.Windows.Media.SolidColorBrush)frame.BorderBrush).Color==((System.Windows.Media.SolidColorBrush)FluentTheme.Accent).Color&&((string)second.ToolTip).Contains(caution.Model),"Selected tile has an accent border and complete model tooltip");
            NextStageTests.Draw(window,Path.Combine(root,"disk-tiles-light.png"));
            AppearanceTheme.Apply("Dark");NextStageTests.Draw(window,Path.Combine(root,"disk-tiles-dark.png"));
            L.ChangeLanguage("en");LiveLanguage.Refresh();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(50));window.UpdateLayout();
            second=(ListBoxItem)window.Disks.ItemContainerGenerator.ContainerFromIndex(1);
            labels=AdvancedToolsTests.Visual<TextBlock>(second).Select(t=>t.Text).ToArray();
            check(window.Disks.SelectedItem==caution&&labels.Contains(caution.TileHealth)&&caution.TileHealth==L.Text("smart.bad"),"Live language updates tiles while retaining selected disk; labels="+String.Join("|",labels)+"; expected="+caution.TileHealth+"; selected="+(window.Disks.SelectedItem==caution));
            var renewed=SmartDisk.Parse("/dev/sda","ata",Ata.Replace("42","43"));renewed.Letters="E:";
            window.SetResults(new List<SmartDisk>{unknown,renewed,good});
            check(window.Disks.SelectedItem==renewed&&window.Snapshot.TemperatureText.Contains("43"),"Refresh preserves selected device across reordered fresh results");
            var many=Enumerable.Range(0,12).Select(i=>SmartDisk.Parse("/dev/disk"+i,"ata",Ata)).ToList();window.SetResults(many);window.Disks.SelectedIndex=11;NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();
            var scroll=AdvancedToolsTests.Visual<ScrollViewer>(window.Disks).First();
            check(scroll.ScrollableWidth>0&&scroll.HorizontalOffset>0&&scroll.ScrollableHeight==0,"Many disks scroll horizontally and selected tile stays in view");
            NextStageTests.Draw(window,Path.Combine(root,"disk-tiles-many.png"));
            window.SetResults(new List<SmartDisk>{good,renewed,unknown});window.Disks.SelectedIndex=1;
            int attempts=0;string copied=null;Func<int,System.Threading.Tasks.Task> noPause=ms=>System.Threading.Tasks.Task.FromResult(0);
            NavigationTests.Wait(window.CopyReportAsync(text=>{attempts++;if(attempts<3)throw new System.Runtime.InteropServices.ExternalException("Busy",unchecked((int)0x800401D0));copied=text;},noPause));
            check(attempts==3&&copied.Contains(renewed.Model)&&window.Feedback==L.Text("smart.copied")&&window.CopyEnabled,"Busy clipboard retries then copies the selected disk report successfully");
            attempts=0;NavigationTests.Wait(window.CopyReportAsync(text=>{attempts++;throw new System.Runtime.InteropServices.ExternalException("Busy",unchecked((int)0x800401D0));},noPause));
            check(attempts==8&&window.Feedback==L.Text("smart.clipboardBusy")&&!window.Feedback.Contains("0x")&&window.CopyEnabled,"Persistently busy clipboard ends after bounded retries with a useful message");
            attempts=0;NavigationTests.Wait(window.CopyReportAsync(text=>{attempts++;throw new System.Runtime.InteropServices.ExternalException("Other failure",unchecked((int)0x80070005));},noPause));
            check(attempts==1&&window.Feedback==L.Text("smart.copyFailed")&&window.CopyEnabled,"Unexpected clipboard failures are handled without raw COM errors");
            var gate=new System.Threading.Tasks.TaskCompletionSource<bool>();attempts=0;copied=null;
            var task=window.CopyReportAsync(text=>{attempts++;if(attempts==1)throw new System.Runtime.InteropServices.ExternalException("Busy",unchecked((int)0x800401D0));copied=text;},ms=>gate.Task);
            check(!window.CopyEnabled&&!task.IsCompleted,"Clipboard retry does not block the window and disables duplicate copy actions");
            window.Disks.SelectedIndex=0;gate.SetResult(true);NavigationTests.Wait(task);
            check(copied.Contains(renewed.Model)&&!copied.Contains(good.Model)&&window.CopyEnabled,"Pending copy retains the original disk snapshot after selection changes");
            var token=new CancellationTokenSource();token.Cancel();bool cancelled=false;attempts=0;
            try{ClipboardTextWriter.TryWriteAsync("test",token.Token,text=>attempts++,noPause).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}finally{token.Dispose();}
            check(cancelled&&attempts==0,"Cancelled clipboard copy never writes after the window closes");
            AppearanceTheme.Apply("Light");
        }
        internal static int Run(string root,bool live) {
            Directory.CreateDirectory(root);var checks=new List<string>();DiskSmartWindow window=null;string original=L.Language,languagePath=AppPaths.Resource("languages"),theme=AppearanceTheme.Mode;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
            try {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));FluentTheme.Ensure();SmartData.ValidateTool();
                var nvme=SmartDisk.Parse("/dev/sdb","nvme",Nvme);nvme.Letters="C: D:";check(nvme.Health=="good"&&nvme.Remaining==94&&nvme.Temperature==51,"NVMe reports actual wear and temperature without estimating missing values");
                check(nvme.Attributes.Count==15&&nvme.Attributes.Any(x=>x.Id=="0F"),"NVMe health log is exposed as 15 named parameters");
                check(SmartDisk.Parse("/dev/sdb","nvme",Nvme.Replace("\"percentage_used\":6","\"percentage_used\":105")).Remaining==0,"Wear over 100 percent clamps remaining endurance to zero");
                check(SmartDisk.Parse("/dev/sdb","nvme",Nvme.Replace("\"percentage_used\":6","\"percentage_used\":105")).Health=="caution","Exhausted NVMe endurance is flagged");
                check(SmartDisk.Parse("/dev/sdb","nvme",Nvme.Replace("\"critical_warning\":0","\"critical_warning\":1")).Health=="caution","Critical warning is not hidden by an overall passed status");
                check(SmartDisk.Parse("/dev/sdb","nvme",Nvme.Replace("\"media_errors\":0","\"media_errors\":4")).Health=="caution","NVMe media integrity errors are flagged");
                check(SmartDisk.Parse("/dev/sdb","nvme",Nvme.Replace("\"passed\":true","\"passed\":false")).Health=="bad","SMART failure takes precedence over other indicators");
                var ata=SmartDisk.Parse("/dev/sda","ata",Ata);check(ata.Health=="good"&&!ata.Remaining.HasValue&&ata.Attributes.Count==2,"ATA normalized values are preserved without fabricating a health percentage");
                check(SmartDisk.Parse("/dev/sda","ata",Ata.Replace("\"id\":5","\"id\":253")).Attributes[0].Name=="Reallocated_Sector_Ct","Unknown vendor attributes retain original labels instead of failing");
                check(ata.Attributes[0].Current=="100"&&ata.Attributes[0].Threshold=="10"&&ata.Attributes[0].Raw=="0","ATA current, worst, threshold and raw columns parse correctly");
                check(SmartDisk.Parse("/dev/sda","ata",Ata.Replace("\"value\":0,\"string\":\"0\"","\"value\":1,\"string\":\"1\"")).Health=="caution","Nonzero reallocated or pending sectors are flagged");
                check(SmartDisk.Parse("/dev/sda","ata",Ata.Replace("\"value\":100","\"value\":5")).Health=="bad","Crossing an ATA threshold is flagged even without when_failed");
                var empty=SmartDisk.Parse("/dev/sdz","ata","{\"smartctl\":{\"exit_status\":2,\"messages\":[{\"severity\":\"error\",\"string\":\"Access denied\"}]}}");check(empty.Health=="unknown"&&!empty.Temperature.HasValue&&empty.Error.Contains("Access denied"),"Permission failures remain unknown with a diagnostic instead of a good health result");
                check(SmartDisk.Parse("/dev/sdz","nvme","{}").Remaining==null,"Missing wear is shown as unavailable");
                check(SmartData.Number("18446744073709551616")==18446744073709551616m,"Counters larger than UInt64 retain decimal precision");
                AppearanceTheme.Apply("Light");window=new DiskSmartWindow(null,false){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.MaxWidth=SmallDesktop?1044:Double.PositiveInfinity;window.MaxHeight=SmallDesktop?788:Double.PositiveInfinity;window.Show();window.MaxWidth=SmallDesktop?1044:Double.PositiveInfinity;window.MaxHeight=SmallDesktop?788:Double.PositiveInfinity;window.Width=1360;window.Height=900;window.UpdateLayout();window.Disks.ItemsSource=new[]{nvme,ata,empty};window.Disks.SelectedIndex=0;window.UpdateLayout();
                check(window.Attributes.Items.Count==15&&window.Attributes.Columns[2].Visibility==Visibility.Collapsed,"NVMe view presents raw log without empty ATA normalization columns");NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();check(window.Attributes.Columns[1].ActualWidth>=220&&window.Attributes.Columns[5].ActualWidth>=(window.ActualWidth>=1200?160:80),"SMART table columns remain readable after layout; window="+window.ActualWidth+"x"+window.ActualHeight+"; name="+window.Attributes.Columns[1].ActualWidth+"; raw="+window.Attributes.Columns[5].ActualWidth);NextStageTests.Draw(window,Path.Combine(root,"nvme-light.png"));
                window.Disks.SelectedIndex=1;check(window.Attributes.Columns[2].Visibility==Visibility.Visible||window.Attributes.Columns[7].Visibility==Visibility.Visible&&((System.Windows.Data.Binding)((DataGridTextColumn)window.Attributes.Columns[7]).Binding).Path.Path=="Normalized","Selecting SATA restores normalized values in separate or compact columns");AppearanceTheme.Apply("Dark");window.UpdateLayout();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();check(window.Attributes.Columns.Where(x=>x.Visibility==Visibility.Visible).Sum(x=>x.ActualWidth)<=window.Attributes.ActualWidth,"Switching NVMe to SATA keeps all columns inside the table");NextStageTests.Draw(window,Path.Combine(root,"ata-dark.png"));window.Close();window=null;
                L.LoadForTests(languagePath,"en");window=new DiskSmartWindow(null,false){Left=-18000,Top=-18000,Width=820,Height=650,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.MaxWidth=SmallDesktop?1044:Double.PositiveInfinity;window.MaxHeight=SmallDesktop?788:Double.PositiveInfinity;window.Show();window.Disks.ItemsSource=new[]{SmartDisk.Parse("/dev/sdb","nvme",Nvme)};window.Disks.SelectedIndex=0;window.UpdateLayout();check(window.Title=="Disk health / SMART","English SMART interface is localized");check(window.Attributes.ActualHeight>=100,"Small SMART window retains usable diagnostic rows");check(L.Text("analyzer.menuGroup")=="Disk tools","Disk tools menu includes health diagnostics");NextStageTests.Draw(window,Path.Combine(root,"english-small.png"));
                PresentationAndExport(window,root,check);
                TilesAndClipboard(window,root,check);
                if(live){var disks=SmartData.Collect(CancellationToken.None,null);check(disks.Count>0,"Real physical disk enumeration succeeds");check(disks.Any(x=>x.Type=="nvme"&&x.Attributes.Count>0),"Real NVMe SMART log is readable");check(disks.Any(x=>x.Type=="ata"&&x.Attributes.Count>0),"Real SATA SMART attributes are readable");File.WriteAllText(Path.Combine(root,"live-summary.txt"),"Devices: "+disks.Count+"; readable: "+disks.Count(x=>x.Attributes.Count>0)+"; mapped volumes: "+disks.Count(x=>x.Letters.Length>0));}
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}
            finally{if(window!=null)window.Close();L.LoadForTests(languagePath,original);AppearanceTheme.Apply(theme);}
        }
    }
}
