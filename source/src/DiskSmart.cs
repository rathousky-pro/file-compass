using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Management;

namespace FileCompass {
    internal sealed class SmartAttribute {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Current { get; set; }
        public string Worst { get; set; }
        public string Threshold { get; set; }
        public string Raw { get; set; }
        public string State { get; set; }
        public string DisplayName { get { return String.IsNullOrEmpty(State)?Name:Name+" · "+State; } }
        public string Normalized { get { return Current+" / "+Worst+" / "+Threshold; } }
    }
    internal sealed partial class SmartDisk {
        internal string Device, Type, RawJson, Error, Letters="", Health="unknown";
        internal DateTime CollectedUtc=DateTime.UtcNow;
        internal Dictionary<string,object> Data;
        internal readonly List<SmartAttribute> Attributes = new List<SmartAttribute>();
        internal decimal? Temperature, Remaining;
        internal string Model { get { return SmartData.Text(SmartData.Get(Data,"model_name"),Device); } }
        public string Label { get { return Model + (Letters.Length>0?"  ·  "+Letters:""); } }
        public string TileModel { get { return Model; } }
        public string TileTemperature { get { return Temperature.HasValue?Temperature.Value.ToString("N0")+" °C":"—"; } }
        public string TileVolumes { get { return !String.IsNullOrWhiteSpace(Letters)?Letters:Device; } }
        public string TileHealth { get { return L.Text("smart."+Health)+(Remaining.HasValue?" · "+Remaining.Value.ToString("N0")+" %":""); } }
        public System.Windows.Media.Brush TileHealthBrush { get { return CompactTheme.ThemeBrush(Health=="good"?"#12623C":Health=="caution"?"#704600":Health=="bad"?"#9E2031":"#596572"); } }
        public System.Windows.Media.Brush TileTemperatureBrush { get { return Temperature.HasValue?FluentTheme.Accent:FluentTheme.Muted; } }
        public string TileHint { get { return Model+Environment.NewLine+KindLabel+" · "+TileVolumes+" · "+Device+Environment.NewLine+TileHealth+" · "+TileTemperature+Environment.NewLine+SmartData.Capacity(SmartData.Get(Data,"user_capacity","bytes"))+(String.IsNullOrWhiteSpace(Error)?"":Environment.NewLine+Error); } }

        internal static SmartDisk Parse(string device,string type,string json) {
            var disk=new SmartDisk{Device=device,Type=type,RawJson=json,Data=SmartData.Parse(json)};var data=disk.Data;
            disk.Temperature=SmartData.Number(SmartData.Get(data,"temperature","current"));
            object passed=SmartData.Get(data,"smart_status","passed");if(passed is bool)disk.Health=(bool)passed?"good":"bad";
            var nvme=SmartData.Get(data,"nvme_smart_health_information_log") as Dictionary<string,object>;
            if(nvme!=null) {
                if(!disk.Temperature.HasValue)disk.Temperature=SmartData.Number(SmartData.Get(nvme,"temperature"));
                decimal? used=SmartData.Number(SmartData.Get(nvme,"percentage_used"));if(used.HasValue&&used.Value>=0)disk.Remaining=Math.Min(100,Math.Max(0,100-used.Value));
                decimal? warning=SmartData.Number(SmartData.Get(nvme,"critical_warning"));
                if(warning.GetValueOrDefault()>0&&disk.Health!="bad")disk.Health="caution";
                if(used>=100&&disk.Health!="bad")disk.Health="caution";
                decimal? spare=SmartData.Number(SmartData.Get(nvme,"available_spare")), spareThreshold=SmartData.Number(SmartData.Get(nvme,"available_spare_threshold"));if(spare<spareThreshold&&disk.Health!="bad")disk.Health="caution";
                decimal? media=SmartData.Number(SmartData.Get(nvme,"media_errors"));if(media.GetValueOrDefault()>0&&disk.Health!="bad")disk.Health="caution";
                string[] keys={"critical_warning","temperature","available_spare","available_spare_threshold","percentage_used","data_units_read","data_units_written","host_reads","host_writes","controller_busy_time","power_cycles","power_on_hours","unsafe_shutdowns","media_errors","num_err_log_entries","warning_temp_time","critical_comp_time"};
                for(int i=0;i<keys.Length;i++)if(nvme.ContainsKey(keys[i]))disk.Attributes.Add(new SmartAttribute{Id=(i+1).ToString("X2"),Name=L.Text("smart.nvme."+keys[i]),Current="—",Worst="—",Threshold="—",Raw=SmartData.Text(nvme[keys[i]]),State=(keys[i]=="critical_warning"||keys[i]=="media_errors")&&SmartData.Number(nvme[keys[i]]).GetValueOrDefault()>0?L.Text("smart.caution"):""});
            }
            foreach(var row in SmartData.Array(SmartData.Get(data,"ata_smart_attributes","table"))) {
                int id=(int)SmartData.Number(SmartData.Get(row,"id")).GetValueOrDefault();decimal? raw=SmartData.Number(SmartData.Get(row,"raw","value"));
                string failed=SmartData.Text(SmartData.Get(row,"when_failed"),"");decimal? normalized=SmartData.Number(SmartData.Get(row,"value")),threshold=SmartData.Number(SmartData.Get(row,"thresh"));bool failing=failed=="now"||(threshold>0&&normalized<=threshold);
                bool concern=((id==5||id==197||id==198)&&raw.GetValueOrDefault()>0)||failed=="past";
                if(failing)disk.Health="bad";else if(concern&&disk.Health!="bad")disk.Health="caution";
                string key="smart.ata."+id, name;try{name=L.Text(key);}catch(KeyNotFoundException){name=SmartData.Text(SmartData.Get(row,"name"));}
                disk.Attributes.Add(new SmartAttribute{Id=id.ToString("X2"),Name=name,Current=SmartData.Text(SmartData.Get(row,"value")),Worst=SmartData.Text(SmartData.Get(row,"worst")),Threshold=SmartData.Text(SmartData.Get(row,"thresh")),Raw=SmartData.Text(SmartData.Get(row,"raw","string"),SmartData.Text(SmartData.Get(row,"raw","value"))),State=failing?L.Text("smart.bad"):concern?L.Text("smart.caution"):""});
            }
            var errors=SmartData.Array(SmartData.Get(data,"smartctl","messages")).Where(x=>SmartData.Text(SmartData.Get(x,"severity"),"")=="error").Select(x=>SmartData.Text(SmartData.Get(x,"string"))).ToArray();
            if(errors.Length>0)disk.Error=String.Join("\n",errors);
            if(SmartData.Get(data,"model_name")==null&&disk.Attributes.Count==0)disk.Health="unknown";
            return disk;
        }
    }
    internal static class SmartData {
        internal static Dictionary<string,object> Parse(string json) { return new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=100}.Deserialize<Dictionary<string,object>>(json); }
        internal static object Get(object value,params string[] keys) { foreach(string key in keys){var map=value as Dictionary<string,object>;if(map==null||!map.TryGetValue(key,out value))return null;}return value; }
        internal static IEnumerable<Dictionary<string,object>> Array(object value) { var list=value as IEnumerable;if(list==null||value is string)yield break;foreach(var item in list){var map=item as Dictionary<string,object>;if(map!=null)yield return map;} }
        internal static string Text(object value,string fallback="—") { return value==null?fallback:Convert.ToString(value,CultureInfo.InvariantCulture); }
        internal static decimal? Number(object value) { decimal number;return value!=null&&Decimal.TryParse(Text(value),NumberStyles.Float,CultureInfo.InvariantCulture,out number)?number:(decimal?)null; }
        internal static string Metric(object value,string unit="") { decimal? number=Number(value);return number.HasValue?number.Value.ToString("N0")+unit:L.Text("smart.unavailable"); }
        internal static string Capacity(object value) { decimal? bytes=Number(value);return bytes.HasValue?ByteSize.Format(bytes.Value):L.Text("smart.unavailable"); }
        internal static bool Administrator { get { using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } }
        internal static string ToolPath { get { return UpdateRegistry.Resolve("smart\\smartctl.exe"); } }
        const string ToolHash="B5DB94E5082C042BE44994B7A4FA8F7B5C8E713B2AB1C9A560D8F7A7995EA27D";
        internal static void ValidateTool() { using(var file=File.OpenRead(ToolPath))using(var hash=SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")!=ToolHash)throw new IOException(L.Text("smart.toolInvalid")); }
        internal static string Run(string arguments,CancellationToken token) {
            using(var trusted=PluginIntegrity.Acquire("smart\\smartctl.exe"))
            using(var process=new Process{StartInfo=new ProcessStartInfo(ToolPath,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}) {
                process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();var timer=Stopwatch.StartNew();
                while(!process.WaitForExit(100)){try{OperationPause.HoldChild(process,token,timer);}catch(OperationCanceledException){if(!process.HasExited)process.Kill();throw;}if(token.IsCancellationRequested||timer.ElapsedMilliseconds>30000){try{process.Kill();}catch(InvalidOperationException){}token.ThrowIfCancellationRequested();throw new TimeoutException(L.Text("smart.timeout"));}}
                process.WaitForExit();string json=output.GetAwaiter().GetResult();string message=error.GetAwaiter().GetResult();
                if(String.IsNullOrWhiteSpace(json))throw new IOException(message.Length>0?message:L.Text("smart.noData"));return json;
            }
        }
        internal static List<SmartDisk> Collect(CancellationToken token,Action<string> progress) {
            ValidateTool();var disks=new List<SmartDisk>();var scan=Parse(Run("--scan -j",token));
            foreach(var device in Array(Get(scan,"devices"))) {
                token.ThrowIfCancellationRequested();string name=Text(Get(device,"name"),""),type=Text(Get(device,"type"),"");
                if(!Regex.IsMatch(name,@"^/dev/[a-zA-Z0-9_/.:-]+$")||!Regex.IsMatch(type,@"^[a-zA-Z0-9_,+.:-]+$"))continue;
                if(progress!=null)progress(name);
                try{disks.Add(SmartDisk.Parse(name,type,Run("-a -j -d "+type+" "+name,token)));}
                catch(IOException ex){disks.Add(new SmartDisk{Device=name,Type=type,Error=ex.Message,Data=new Dictionary<string,object>()});}
                catch(TimeoutException ex){disks.Add(new SmartDisk{Device=name,Type=type,Error=ex.Message,Data=new Dictionary<string,object>()});}
            }
            try {
                using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))using(var drives=query.Get())foreach(ManagementObject drive in drives)using(drive) {
                    int index=Convert.ToInt32(drive["Index"]); string name="/dev/sd"+DiskSuffix(index);var disk=disks.FirstOrDefault(x=>x.Device==name);if(disk==null)continue;
                    var letters=new List<string>();using(var partitions=drive.GetRelated("Win32_DiskPartition"))foreach(ManagementObject partition in partitions)using(partition)using(var volumes=partition.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject volume in volumes)using(volume)letters.Add(Convert.ToString(volume["DeviceID"]));disk.Letters=String.Join(" ",letters.Distinct().OrderBy(x=>x));
                }
            }catch(ManagementException){}catch(UnauthorizedAccessException){}catch(System.Runtime.InteropServices.COMException){}
            return disks;
        }
        static string DiskSuffix(int index){string value="";do{value=(char)('a'+index%26)+value;index=index/26-1;}while(index>=0);return value;}
    }
}
