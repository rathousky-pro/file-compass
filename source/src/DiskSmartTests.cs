using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
    internal static class DiskSmartTests {
        const string Nvme="{\"device\":{\"protocol\":\"NVMe\"},\"model_name\":\"KINGSTON NVMe — ukázka\",\"serial_number\":\"TEST-SERIAL\",\"firmware_version\":\"TEST1\",\"user_capacity\":{\"bytes\":1024209543168},\"nvme_version\":{\"string\":\"NVMe 1.4\"},\"smart_support\":{\"available\":true},\"smart_status\":{\"passed\":true},\"power_on_time\":{\"hours\":20677},\"power_cycle_count\":511,\"nvme_smart_health_information_log\":{\"critical_warning\":0,\"temperature\":51,\"available_spare\":100,\"available_spare_threshold\":10,\"percentage_used\":6,\"data_units_read\":70500000,\"data_units_written\":110000000,\"host_reads\":123456,\"host_writes\":654321,\"controller_busy_time\":123,\"power_cycles\":511,\"power_on_hours\":20677,\"unsafe_shutdowns\":27,\"media_errors\":0,\"num_err_log_entries\":1092}}";
        const string Ata="{\"model_name\":\"SATA disk — ukázka\",\"user_capacity\":{\"bytes\":2000000000000},\"smart_status\":{\"passed\":true},\"rotation_rate\":7200,\"temperature\":{\"current\":42},\"ata_smart_attributes\":{\"table\":[{\"id\":5,\"name\":\"Reallocated_Sector_Ct\",\"value\":100,\"worst\":100,\"thresh\":10,\"when_failed\":\"-\",\"raw\":{\"value\":0,\"string\":\"0\"}},{\"id\":197,\"name\":\"Current_Pending_Sector\",\"value\":100,\"worst\":100,\"thresh\":0,\"when_failed\":\"-\",\"raw\":{\"value\":0,\"string\":\"0\"}}]}}";
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
                AppearanceTheme.Apply("Light");window=new DiskSmartWindow(null,false){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();window.Disks.ItemsSource=new[]{nvme,ata,empty};window.Disks.SelectedIndex=0;window.UpdateLayout();
                check(window.Attributes.Items.Count==15&&window.Attributes.Columns[2].Visibility==Visibility.Collapsed,"NVMe view presents raw log without empty ATA normalization columns");NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();check(window.Attributes.Columns[1].ActualWidth>=220&&window.Attributes.Columns[5].ActualWidth>=160,"SMART table columns remain readable after layout");NextStageTests.Draw(window,Path.Combine(root,"nvme-light.png"));
                window.Disks.SelectedIndex=1;check(window.Attributes.Columns[2].Visibility==Visibility.Visible,"Selecting SATA restores normalized attribute columns");AppearanceTheme.Apply("Dark");window.UpdateLayout();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));window.UpdateLayout();check(window.Attributes.Columns.Where(x=>x.Visibility==Visibility.Visible).Sum(x=>x.ActualWidth)<=window.Attributes.ActualWidth,"Switching NVMe to SATA keeps all columns inside the table");NextStageTests.Draw(window,Path.Combine(root,"ata-dark.png"));window.Close();window=null;
                L.LoadForTests(languagePath,"en");window=new DiskSmartWindow(null,false){Left=-18000,Top=-18000,Width=820,Height=650,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();window.Disks.ItemsSource=new[]{SmartDisk.Parse("/dev/sdb","nvme",Nvme)};window.Disks.SelectedIndex=0;window.UpdateLayout();check(window.Title=="Disk health / SMART","English SMART interface is localized");check(window.Attributes.ActualHeight>=100,"Small SMART window retains usable diagnostic rows");check(L.Text("analyzer.menuGroup")=="Disk tools","Disk tools menu includes health diagnostics");NextStageTests.Draw(window,Path.Combine(root,"english-small.png"));
                if(live){var disks=SmartData.Collect(CancellationToken.None,null);check(disks.Count>0,"Real physical disk enumeration succeeds");check(disks.Any(x=>x.Type=="nvme"&&x.Attributes.Count>0),"Real NVMe SMART log is readable");check(disks.Any(x=>x.Type=="ata"&&x.Attributes.Count>0),"Real SATA SMART attributes are readable");File.WriteAllText(Path.Combine(root,"live-summary.txt"),"Devices: "+disks.Count+"; readable: "+disks.Count(x=>x.Attributes.Count>0)+"; mapped volumes: "+disks.Count(x=>x.Letters.Length>0));}
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex);return 1;}
            finally{if(window!=null)window.Close();L.LoadForTests(languagePath,original);AppearanceTheme.Apply(theme);}
        }
    }
}
