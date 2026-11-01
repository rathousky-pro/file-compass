#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace FileCompass {
    internal static class AnalyzerStartSmartKindTests {
        static void Open(Window window){window.Left=-18000;window.Top=-18000;window.WindowStartupLocation=WindowStartupLocation.Manual;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();window.UpdateLayout();}
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();DiskAnalyzerWindow analyzer=null;DiskSmartWindow smart=null;
            Action<bool,string> check=(yes,message)=>{if(!yes)throw new Exception(message);checks.Add(message);};
            try{
                System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs-CZ");FluentTheme.Ensure();AppearanceTheme.Apply("Light");
                string folder=Directory.CreateDirectory(Path.Combine(root,"files")).FullName;File.WriteAllBytes(Path.Combine(folder,"small.bin"),new byte[128]);File.WriteAllBytes(Path.Combine(folder,"large.bin"),new byte[4096]);
                analyzer=new DiskAnalyzerWindow(null,folder,null);Open(analyzer);NavigationTests.Wait(Task.Delay(180));analyzer.UpdateLayout();
                check(analyzer.ScanTask==null&&analyzer.Result==null&&analyzer.AnalyzerPages.SelectedIndex==1,"default analyzer opens its scope/filter page without starting or reading a scan");
                check(analyzer.AnalyzerRequiredWidth<1600,"initial settings page measures result headers without treating the unarranged split as window chrome");
                string minimum="0"+System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator+"002";analyzer.FilterSize.Text=minimum;NavigationTests.Wait(Task.Delay(80));
                check(analyzer.Result==null&&analyzer.ScanTask==null,"editing a minimum size does not start analysis");NextStageTests.Draw(analyzer,Path.Combine(root,"analyzer-ready.png"));
                var start=FluentUiTests.Children<Button>(analyzer).Single(x=>Convert.ToString(x.Content)==L.Text("analyzer.start"));check(start.IsDefault&&start.IsEnabled,"Start analysis is the explicit default action");start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(analyzer.ScanTask);analyzer.UpdateLayout();
                check(analyzer.AnalyzerPages.SelectedIndex==0&&analyzer.Result.Root.Files==1&&analyzer.Result.Root.Bytes==4096,"first Start applies the preconfigured size filter and opens results");
                check(Convert.ToString(start.Content)==L.Text("analyzer.rescan"),"completed analysis offers a repeat action");
                var previous=analyzer.Result;var scan=analyzer.ScanTask;analyzer.FilterSize.Text="0";check(analyzer.PendingParameters&&Object.ReferenceEquals(previous,analyzer.Result)&&Object.ReferenceEquals(scan,analyzer.ScanTask),"changing parameters keeps previous results and marks them as pending");
                analyzer.FilterSize.Text=minimum;check(!analyzer.PendingParameters,"restoring original parameters clears the pending indication");
                analyzer.FilterSize.Text="0";analyzer.AnalyzerPages.SelectedIndex=1;analyzer.UpdateLayout();analyzer.FilterSize.Focus();analyzer.FilterSize.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(analyzer),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});NavigationTests.Wait(analyzer.ScanTask);
                check(analyzer.Result.Root.Files==2&&!analyzer.PendingParameters,"Enter in a single-line parameter starts a new analysis using the new values");
                previous=analyzer.Result;analyzer.AnalyzerPages.SelectedIndex=1;analyzer.FilterSize.Text="invalid";start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(analyzer.ScanTask);check(Object.ReferenceEquals(previous,analyzer.Result)&&analyzer.AnalyzerPages.SelectedIndex==1,"invalid parameters do not clear prior results or leave the settings page");analyzer.Close();analyzer=null;

                var hdd=SmartDisk.Parse("/dev/hdd","ata","{\"device\":{\"protocol\":\"ATA\"},\"rotation_rate\":7200,\"model_name\":\"WDC HDD\",\"smart_status\":{\"passed\":true},\"temperature\":{\"current\":42}}");
                var ssd=SmartDisk.Parse("/dev/ssd","sat","{\"device\":{\"protocol\":\"ATA\"},\"rotation_rate\":0,\"model_name\":\"SATA SSD\",\"smart_status\":{\"passed\":true},\"temperature\":{\"current\":38}}");
                var nvme=SmartDisk.Parse("/dev/nvme","NVME","{\"device\":{\"protocol\":\"NVMe\"},\"model_name\":\"NVMe storage\",\"smart_status\":{\"passed\":true},\"temperature\":{\"current\":44}}");
                var usb=SmartDisk.Parse("/dev/usb","usbjmicron","{\"model_name\":\"USB bridge\"}");var unknown=SmartDisk.Parse("/dev/unknown","ata","{\"model_name\":\"Name contains SSD but type unknown\"}");
                check(hdd.KindCode=="hdd"&&ssd.KindCode=="ssd"&&nvme.KindCode=="nvme","HDD, SSD and NVMe are identified from diagnostic rotation/protocol metadata");
                check(usb.KindCode=="usb"&&unknown.KindCode=="unknown","USB with unknown media and absent metadata are not guessed from model names");
                check(SmartDisk.Parse("/dev/scsi","scsi","{\"device\":{\"protocol\":\"SCSI\"},\"rotation_rate\":0}").KindCode=="scsi","ambiguous SCSI rotation zero is not falsely labeled SSD");
                check(SmartDisk.Parse("/dev/reserved","scsi","{\"rotation_rate\":65535}").KindCode=="scsi","reserved rotation values are not labeled rotating HDDs");
                check(new[]{hdd.KindIcon,ssd.KindIcon,nvme.KindIcon}.Distinct().Count()==3&&new[]{hdd.KindBrush.ToString(),ssd.KindBrush.ToString(),nvme.KindBrush.ToString()}.Distinct().Count()==3,"three primary kinds have distinct vector icons and restrained accent colors");
                check(hdd.KindHint.Contains(7200.ToString("N0"))&&hdd.Health=="good"&&ssd.Health=="good"&&nvme.Health=="good","kind hints include rotation where known and do not alter health interpretation");
                hdd.Letters="E: F:";ssd.Letters="C: D:";nvme.Letters="G:";usb.Letters="H:";
                smart=new DiskSmartWindow(null,false);Open(smart);smart.SetResults(new List<SmartDisk>{hdd,ssd,nvme,usb,unknown});NavigationTests.Wait(Task.Delay(120));smart.UpdateLayout();
                NextStageTests.Draw(smart,Path.Combine(root,"smart-kinds-before-selection.png"));var labels=AdvancedToolsTests.Visual<TextBlock>(smart.Disks).Where(x=>x.IsVisible).Select(x=>x.Text).ToArray();check(!new[]{"HDD","SSD","NVMe","USB",L.Text("smart.kindUnknown")}.Any(labels.Contains),"disk tiles keep health and temperature without repeating kind badges: "+String.Join(" | ",labels));
                smart.Disks.SelectedItem=ssd;smart.UpdateLayout();check(smart.Snapshot.Model==ssd.Model,"selected disk summary retains its model and diagnostic snapshot");check(!AdvancedToolsTests.Visual<System.Windows.Controls.Image>(smart).Any(x=>Object.ReferenceEquals(x.Source,ssd.KindIcon)),"selected disk headline contains no redundant hardware-kind icon");NextStageTests.Draw(smart,Path.Combine(root,"smart-kinds-light.png"));AppearanceTheme.Apply("Dark");smart.UpdateLayout();NextStageTests.Draw(smart,Path.Combine(root,"smart-kinds-dark.png"));smart.Close();smart=null;
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(analyzer!=null)analyzer.Close();if(smart!=null)smart.Close();AppearanceTheme.Apply("Light");}
        }
    }
}
#endif
