using System;
using System.Collections.Generic;
using System.Linq;
namespace FileCompass {
    internal sealed class SmartSnapshot {
        internal string Model,Device,HealthKey,HealthText,TemperatureText,RemainingText,Error,RawJson;
        internal bool Nvme;
        internal DateTime CollectedUtc;
        internal List<KeyValuePair<string,string>> Information;
        internal List<SmartAttribute> Attributes;
        internal static readonly string[] InfoKeys={"firmware","serial","interface","capacity","letters","standard","rotation","hours","cycles","read","written","features"};
        internal static SmartSnapshot Create(SmartDisk disk) {
            var data=disk.Data;var nvme=SmartData.Get(data,"nvme_smart_health_information_log");
            var snapshot=new SmartSnapshot{Model=disk.Model,Device=disk.Device,HealthKey=disk.Health,HealthText=L.Text("smart."+disk.Health),TemperatureText=disk.Temperature.HasValue?disk.Temperature.Value.ToString("N0")+" °C":L.Text("smart.unavailable"),RemainingText=disk.Remaining.HasValue?disk.Remaining.Value.ToString("N0")+" %":"",Nvme=nvme!=null,CollectedUtc=disk.CollectedUtc,RawJson=disk.RawJson,Error=disk.Error};
            decimal? rpm=SmartData.Number(SmartData.Get(data,"rotation_rate")),read=SmartData.Number(SmartData.Get(nvme,"data_units_read")),written=SmartData.Number(SmartData.Get(nvme,"data_units_written"));
            var features=new List<string>();if(SmartData.Get(data,"smart_support","available") as bool?==true)features.Add("SMART");if(SmartData.Get(data,"trim","supported") as bool?==true)features.Add("TRIM");
            string[] values={SmartData.Text(SmartData.Get(data,"firmware_version")),SmartData.Text(SmartData.Get(data,"serial_number")),SmartData.Text(SmartData.Get(data,"device","protocol"),disk.Type),SmartData.Capacity(SmartData.Get(data,"user_capacity","bytes")),disk.Letters.Length>0?disk.Letters:L.Text("smart.unavailable"),SmartData.Text(SmartData.Get(data,"nvme_version","string"),SmartData.Text(SmartData.Get(data,"ata_version","string"))),disk.KindCode=="hdd"?rpm.Value.ToString("N0")+" RPM":disk.KindCode=="ssd"?"SSD":disk.KindCode=="nvme"?"NVMe":L.Text("smart.unavailable"),SmartData.Metric(SmartData.Get(data,"power_on_time","hours")),SmartData.Metric(SmartData.Get(data,"power_cycle_count")),read.HasValue?SmartData.Capacity(read.Value*512000m):L.Text("smart.unavailable"),written.HasValue?SmartData.Capacity(written.Value*512000m):L.Text("smart.unavailable"),features.Count>0?String.Join(", ",features):"-"};
            snapshot.Information=InfoKeys.Select((key,i)=>new KeyValuePair<string,string>(key,values[i])).ToList();
            snapshot.Attributes=disk.Attributes.Select(a=>new SmartAttribute{Id=a.Id,Name=L.TranslateExisting(a.Name),Current=a.Current,Worst=a.Worst,Threshold=a.Threshold,Raw=a.Raw,State=L.TranslateExisting(a.State)}).ToList();
            return snapshot;
        }
        internal string TextReport() {
            return Model+" · "+Device+Environment.NewLine+CollectedUtc.ToLocalTime().ToString("g")+Environment.NewLine+L.Text("smart.health")+": "+HealthText+(RemainingText.Length>0?" · "+RemainingText:"")+Environment.NewLine+L.Text("smart.temperature")+": "+TemperatureText+Environment.NewLine+String.Join(Environment.NewLine,Information.Select(x=>L.Text("smart."+x.Key)+": "+x.Value))+Environment.NewLine+String.Join(Environment.NewLine,Attributes.Select(x=>String.Join("\t",new[]{x.Id,x.Name,x.Current,x.Worst,x.Threshold,x.Raw,x.State})))+Environment.NewLine+(Error??"");
        }
    }
}
