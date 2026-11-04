using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace FileCompass {
 public sealed class VendorUpdate {
    public string Id,Name,Version,Url,Hash,FileName,Source,Notes,Error,Build,Channel;
    public long Size;
 }
 public sealed class UpdateFile {public string Relative,Hash;public long Size;}
 public sealed class InstalledUpdate {public string Id,Version,Folder,Source,PackageHash;public List<UpdateFile> Files=new List<UpdateFile>();}
 public sealed class UpdatePreferences {
    public bool Daily=true;
    public DateTime LastAttemptUtc,LastSuccessUtc;
    public List<VendorUpdate> Available=new List<VendorUpdate>();
    public List<InstalledUpdate> Active=new List<InstalledUpdate>(),Previous=new List<InstalledUpdate>(),Pending=new List<InstalledUpdate>();
    public List<string> RestoreBundled=new List<string>();
 }
 internal static class UpdateNetwork {
    static UpdateNetwork(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;}
    internal static bool Allowed(string value){Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||!String.IsNullOrEmpty(uri.UserInfo))return false;return new[]{"api.github.com","github.com","release-assets.githubusercontent.com","objects.githubusercontent.com","www.gyan.dev","gyan.dev"}.Contains(uri.Host.ToLowerInvariant());}
    internal static void Download(string url,Stream target,long maximum,CancellationToken token,Action<long,long> report=null){
        try{Transfer(url,target,maximum,token,report);}catch(Exception){token.ThrowIfCancellationRequested();throw;}
    }
    static void Transfer(string url,Stream target,long maximum,CancellationToken token,Action<long,long> report){
        for(int redirects=0;redirects<6;redirects++){
            if(!Allowed(url))throw new IOException(L.Text("updates.unapprovedSource")+url);
            token.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(url);request.AllowAutoRedirect=false;request.Timeout=30000;request.ReadWriteTimeout=30000;request.UserAgent="FileCompass-Updater/1";request.Accept="application/vnd.github+json, */*";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            using(token.Register(request.Abort))using(var response=(HttpWebResponse)request.GetResponse()){
                int code=(int)response.StatusCode;if(code>=300&&code<400){url=new Uri(new Uri(url),response.Headers["Location"]).AbsoluteUri;continue;}
                if(response.ContentLength>maximum)throw new IOException(L.Text("updates.downloadLimit"));
                using(var input=response.GetResponseStream()){byte[] buffer=new byte[65536];long count=0;int read;while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();count+=read;if(count>maximum)throw new IOException(L.Text("updates.downloadLimit"));target.Write(buffer,0,read);if(report!=null)report(count,response.ContentLength);}return;}
            }
        }throw new IOException(L.Text("updates.redirectLimit"));
    }
    internal static string Text(string url,CancellationToken token){using(var output=new MemoryStream()){Download(url,output,4*1048576,token);return Encoding.UTF8.GetString(output.ToArray());}}
 }
 internal static class VendorUpdates {
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> versions=new System.Collections.Concurrent.ConcurrentDictionary<string,string>();
    internal static readonly string[] Ids={"7zip","ffmpeg","smart","rclone"};
    internal static string Name(string id){return id=="7zip"?"7-Zip":id=="ffmpeg"?"FFmpeg + FFprobe":id=="smart"?"smartmontools":id=="rclone"?"rclone":"File Compass";}
    internal static string[] Files(string id){switch(id){case "7zip":return new[]{"7zip\\7z.exe","7zip\\7z.dll"};case "ffmpeg":return new[]{"media\\ffmpeg.exe","media\\ffprobe.exe"};case "smart":return new[]{"smart\\smartctl.exe"};case "rclone":return new[]{"cloud\\rclone.exe"};default:throw new IOException(L.Text("updates.unsupportedPackage"));}}
    internal static string Home(string id){return id=="7zip"?"https://www.7-zip.org/download.html":id=="ffmpeg"?"https://ffmpeg.org/download.html":id=="smart"?"https://github.com/smartmontools/smartmontools/releases":"https://rclone.org/downloads/";}
    internal static string Get(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):"";}
    internal static string Sha(string text){var match=Regex.Match(text??"",@"(?i)\b[0-9a-f]{64}\b");if(!match.Success)throw new IOException(L.Text("updates.noChecksum"));return match.Value.ToLowerInvariant();}
    internal static VendorUpdate Github(string id,string json){
        var data=new JavaScriptSerializer{MaxJsonLength=4*1048576}.Deserialize<Dictionary<string,object>>(json);
        if(Get(data,"draft").Equals("True",StringComparison.OrdinalIgnoreCase)||Get(data,"prerelease").Equals("True",StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("updates.noStableRelease"));
        object raw;if(!data.TryGetValue("assets",out raw))throw new IOException(L.Text("updates.noPackages"));
        var assets=((System.Collections.IEnumerable)raw).Cast<Dictionary<string,object>>().ToArray();
        var candidates=assets.Where(a=>id=="7zip"?Regex.IsMatch(Get(a,"name"),@"^7z[0-9]+-x64\.exe$"):id=="smart"?Regex.IsMatch(Get(a,"name"),@"^smartmontools-.+\.win32-setup\.exe$"):Regex.IsMatch(Get(a,"name"),@"^rclone-v[0-9.]+-windows-amd64\.zip$")).ToArray();
        if(candidates.Length!=1)throw new IOException(L.Text("updates.ambiguousPackage"));
        var asset=candidates[0];string repository=id=="7zip"?"ip7z/7zip":id=="smart"?"smartmontools/smartmontools":"rclone/rclone";string url=Get(asset,"browser_download_url");
        if(!url.StartsWith("https://github.com/"+repository+"/releases/download/",StringComparison.Ordinal))throw new IOException(L.Text("updates.outsideRepository"));
        string version=Regex.Match(Get(data,"tag_name").Replace('_','.'),@"[0-9]+(?:\.[0-9]+)+").Value;if(version.Length==0)throw new IOException(L.Text("updates.invalidVersion"));
        var result=new VendorUpdate{Id=id,Name=Name(id),Version=version,Url=url,FileName=Get(asset,"name"),Size=Int64.Parse(Get(asset,"size"),CultureInfo.InvariantCulture),Source="https://github.com/"+repository,Notes=Get(data,"body")};
        string digest=Get(asset,"digest");if(Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$"))result.Hash=Sha(digest);else result.Error=L.Text("updates.noChecksum");return result;
    }
    internal static bool ValidOrigin(VendorUpdate update){
        if(update==null||!Ids.Contains(update.Id)||!UpdateNetwork.Allowed(update.Url))return false;
        if(update.Id=="ffmpeg")return update.Url=="https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z";
        string repo=update.Id=="7zip"?"ip7z/7zip":update.Id=="smart"?"smartmontools/smartmontools":"rclone/rclone";
        Uri uri;return Uri.TryCreate(update.Url,UriKind.Absolute,out uri)&&uri.AbsoluteUri==update.Url&&update.Url.StartsWith("https://github.com/"+repo+"/releases/download/",StringComparison.Ordinal)&&!update.Url.Contains("/../")&&update.Url.IndexOfAny(new[]{'%','?','#'})<0;
    }
    internal static VendorUpdate Check(string id,CancellationToken token){
        if(id=="ffmpeg"){
            string url="https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z";
            string version=UpdateNetwork.Text("https://www.gyan.dev/ffmpeg/builds/release-version",token).Trim();if(!Regex.IsMatch(version,@"^\d+(\.\d+){1,3}$"))throw new IOException(L.Text("updates.invalidFfmpegVersion"));
            return new VendorUpdate{Id=id,Name=Name(id),Version=version,Url=url,Hash=Sha(UpdateNetwork.Text(url+".sha256",token)),FileName="ffmpeg-release-full.7z",Source="https://www.gyan.dev/ffmpeg/builds/",Notes=L.Text("updates.ffmpegSource")};
        }
        string repository=id=="7zip"?"ip7z/7zip":id=="smart"?"smartmontools/smartmontools":"rclone/rclone";
        return Github(id,UpdateNetwork.Text("https://api.github.com/repos/"+repository+"/releases/latest",token));
    }
    internal static string Hash(Stream stream){using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();stream.Position=0;return hash;}}
    internal static bool Is64Bit(string path){using(var stream=File.OpenRead(path))using(var reader=new BinaryReader(stream)){if(stream.Length<128||reader.ReadUInt16()!=0x5a4d)return false;stream.Position=0x3c;long offset=reader.ReadInt32();if(offset<64||offset>stream.Length-6)return false;stream.Position=offset;return reader.ReadUInt32()==0x4550&&reader.ReadUInt16()==0x8664;}}
    static string ReadBounded(StreamReader reader){var text=new StringBuilder();char[] buffer=new char[512];int read;while((read=reader.Read(buffer,0,buffer.Length))>0){text.Append(buffer,0,read);if(text.Length>32768)throw new IOException(L.Text("updates.verificationLimit"));}return text.ToString();}
    internal static void Probe(InstalledUpdate receipt,CancellationToken token){
        foreach(var entry in receipt.Files.Where(x=>x.Relative.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))){
            string path=UpdateRegistry.SafePath(UpdateRegistry.SafePath(UpdateRegistry.Root,receipt.Folder),entry.Relative);
            using(var lease=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
                if(Hash(lease)!=entry.Hash)throw new IOException(L.Text("updates.changedBeforeCheck"));
                var start=new ProcessStartInfo(path,receipt.Id=="7zip"?"i":receipt.Id=="rclone"?"version":"-version"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(path)};
                using(var job=new ArchiveProcessJob(512))using(var process=new Process{StartInfo=start}){
                    process.Start();job.Assign(process);var output=Task.Run(()=>ReadBounded(process.StandardOutput));var errors=Task.Run(()=>ReadBounded(process.StandardError));var clock=Stopwatch.StartNew();
                    while(!process.WaitForExit(60)){if(token.IsCancellationRequested||clock.Elapsed>TimeSpan.FromSeconds(10)||output.IsFaulted||errors.IsFaulted){try{process.Kill();}catch{}token.ThrowIfCancellationRequested();throw new IOException(L.Text("updates.executableCheckFailed"));}}
                    string text=output.GetAwaiter().GetResult()+errors.GetAwaiter().GetResult();if(process.ExitCode!=0||!Regex.IsMatch(text,Regex.Escape(receipt.Version)+@"(?!\d)"))throw new IOException(L.Text("updates.unexpectedVersion"));
                }
            }
        }
    }
    internal static string InstalledVersion(string id){
        var record=UpdateRegistry.State().Active.FirstOrDefault(x=>x.Id==id);if(record!=null)return record.Version;
        string path=UpdateRegistry.Resolve(Files(id)[0]);if(!File.Exists(path))return L.Text("updates.missing");
        string version=FileVersionInfo.GetVersionInfo(path).ProductVersion??"";var match=Regex.Match(version,@"\d+(\.\d+){1,3}");if(match.Success)return match.Value;string cached;return versions.TryGetValue(path,out cached)?cached:L.Text("updates.bundled");
    }
    internal static void InspectInstalled(CancellationToken token){
        foreach(string id in Ids){token.ThrowIfCancellationRequested();if(InstalledVersion(id)!=L.Text("updates.bundled"))continue;
            using(PluginIntegrity.Acquire(Files(id)[0])){
                string path=UpdateRegistry.Resolve(Files(id)[0]);var start=new ProcessStartInfo(path,id=="7zip"?"i":id=="rclone"?"version":"-version"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(path)};
                using(var job=new ArchiveProcessJob(512))using(var process=new Process{StartInfo=start}){
                    process.Start();job.Assign(process);var output=Task.Run(()=>ReadBounded(process.StandardOutput));var errors=Task.Run(()=>ReadBounded(process.StandardError));var clock=Stopwatch.StartNew();
                    while(!process.WaitForExit(60)){if(token.IsCancellationRequested||clock.Elapsed>TimeSpan.FromSeconds(5)||output.IsFaulted||errors.IsFaulted){try{process.Kill();}catch{}token.ThrowIfCancellationRequested();throw new IOException(L.Text("updates.installedCheckTimeout"));}}
                    string text=output.GetAwaiter().GetResult()+errors.GetAwaiter().GetResult();var match=Regex.Match(text,@"(?:version\s+|rclone\s+v|7-Zip\s+)(?:n)?(\d+(?:\.\d+){1,3})",RegexOptions.IgnoreCase);if(process.ExitCode==0&&match.Success)versions[path]=match.Groups[1].Value;
                }
            }
        }
    }
 }
}
