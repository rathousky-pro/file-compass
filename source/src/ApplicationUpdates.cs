using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using FileCompass.Updating;
namespace FileCompass {
 public sealed class PendingApplicationUpdate {public string Folder;public VendorUpdate Package;}
 internal static class ApplicationUpdates {
  internal const string Repository="rathousky-pro/file-compass-web",AssetName="FileCompass-win-x64.zip";
  internal const string Home="https://github.com/"+Repository+"/releases";
  internal const string Api="https://api.github.com/repos/"+Repository+"/releases?per_page=100";
  static string PendingPath {get{return Path.Combine(AppPaths.ConfigDirectory,"application-update.xml");}}
  internal static string CurrentChannel {get{return BuildInfo.DisplayVersion==BuildInfo.Version?"":BuildInfo.DisplayVersion.Substring(BuildInfo.Version.Length+1);}}
  internal static bool IsNewer(VendorUpdate update){return update!=null&&UpdateContract.ValidIdentity(update.Version,update.Channel,update.Build)&&UpdateContract.Compare(update.Version,update.Channel,update.Build,BuildInfo.Version,CurrentChannel,BuildInfo.Build)>0;}
  internal static bool ValidOrigin(VendorUpdate update){return update!=null&&update.Id=="app"&&UpdateContract.ValidIdentity(update.Version,update.Channel,update.Build)&&update.FileName==AssetName&&update.Url=="https://github.com/"+Repository+"/releases/download/"+Tag(update.Version,update.Channel,update.Build)+"/"+AssetName;}
  internal static string Tag(string version,string channel,string build){return "v"+version+(String.IsNullOrEmpty(channel)?"":"-"+channel)+"-build."+build;}
  internal static VendorUpdate Parse(string json,string channel){
   var releases=new JavaScriptSerializer{MaxJsonLength=4*1048576}.Deserialize<Dictionary<string,object>[]>(json);if(releases==null)throw new IOException(L.Text("updates.invalidVersion"));var candidates=new List<VendorUpdate>();
   foreach(var release in releases){if(VendorUpdates.Get(release,"draft").Equals("True",StringComparison.OrdinalIgnoreCase))continue;var match=Regex.Match(VendorUpdates.Get(release,"tag_name"),@"^v(?<version>\d+\.\d+\.\d+)(?:-(?<channel>alpha|beta|rc))?-build\.(?<build>\d{8}\.[0-9]{2,9})$");if(!match.Success)continue;
    string releaseChannel=match.Groups["channel"].Value;if(String.IsNullOrEmpty(channel)&&(!String.IsNullOrEmpty(releaseChannel)||VendorUpdates.Get(release,"prerelease").Equals("True",StringComparison.OrdinalIgnoreCase)))continue;if(!String.IsNullOrEmpty(releaseChannel)&&releaseChannel!=channel)continue;
    var item=new VendorUpdate{Id="app",Name="File Compass",Version=match.Groups["version"].Value,Channel=releaseChannel,Build=match.Groups["build"].Value,Source=Home,Notes=VendorUpdates.Get(release,"body"),FileName=AssetName};if(!UpdateContract.ValidIdentity(item.Version,item.Channel,item.Build))continue;
    object raw;if(!release.TryGetValue("assets",out raw)||raw==null)throw new IOException(L.Text("updates.noPackages"));var assets=((IEnumerable)raw).Cast<Dictionary<string,object>>().Where(x=>VendorUpdates.Get(x,"name")==AssetName).ToArray();
    if(assets.Length!=1){item.Error=L.Text("updates.ambiguousPackage");candidates.Add(item);continue;}
    var asset=assets[0];item.Url=VendorUpdates.Get(asset,"browser_download_url");long size;Int64.TryParse(VendorUpdates.Get(asset,"size"),NumberStyles.Integer,CultureInfo.InvariantCulture,out size);item.Size=size;if(!ValidOrigin(item)||size<1||size>768L*1048576)item.Error=L.Text("updates.invalidVendor");
    string digest=VendorUpdates.Get(asset,"digest");if(Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))item.Hash=digest.Substring(7).ToLowerInvariant();else item.Error=L.Text("updates.noChecksum");candidates.Add(item);
   }
   if(candidates.Count==0)return new VendorUpdate{Id="app",Name="File Compass",Source=Home,Notes=L.Text("updates.appNoRelease")};
   candidates.Sort((a,b)=>-UpdateContract.Compare(a.Version,a.Channel,a.Build,b.Version,b.Channel,b.Build));return candidates[0];
  }
  internal static VendorUpdate Check(CancellationToken token){return Parse(UpdateNetwork.Text(Api,token),CurrentChannel);}
  internal static PendingApplicationUpdate Pending(){if(!File.Exists(PendingPath))return null;var pending=ConfigurationIntegrity.Read<PendingApplicationUpdate>(PendingPath);if(pending==null||!Regex.IsMatch(pending.Folder??"",@"^application\\[a-f0-9]{32}$")||!ValidOrigin(pending.Package))throw new IOException(L.Text("updates.invalidReceipt"));UpdateRegistry.SafePath(UpdateRegistry.Root,pending.Folder);return IsNewer(pending.Package)?pending:null;}
  internal static string Folder(PendingApplicationUpdate pending){return UpdateRegistry.SafePath(UpdateRegistry.Root,pending.Folder);}
  internal static ReleaseManifest Stage(VendorUpdate item,string zipPath,string directory,CancellationToken token){
   if(!ValidOrigin(item)||!IsNewer(item)||VendorUpdates.Sha(item.Hash)!=item.Hash)throw new IOException(L.Text("updates.invalidVendor"));
   using(var locked=new FileStream(zipPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
    if(locked.Length!=item.Size||VendorUpdates.Hash(locked)!=item.Hash)throw new IOException(L.Text("updates.checksumMismatch"));
    using(var zip=new ZipArchive(locked,ZipArchiveMode.Read,true)){
     if(zip.Entries.Count>21000)throw new IOException(L.Text("updates.downloadLimit"));var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
     foreach(var entry in zip.Entries){token.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');if(name.EndsWith("/")){UpdateContract.SafePath(directory,name.TrimEnd('/'));continue;}if(!UpdateContract.AllowedFile(name)||!names.Add(name)||(entry.ExternalAttributes&0x400)==0x400||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new IOException(L.Text("updates.invalidPath"));UpdateContract.SafePath(directory,name);total=checked(total+entry.Length);if(entry.Length>768L*1048576||total>3L*1024*1048576)throw new IOException(L.Text("updates.downloadLimit"));}
     var metadata=zip.Entries.SingleOrDefault(x=>x.FullName.Replace('\\','/')==UpdateContract.ManifestName);if(metadata==null||metadata.Length>8*1048576)throw new IOException(L.Text("updates.incompleteReceipt"));ReleaseManifest manifest;using(var reader=new StreamReader(metadata.Open()))manifest=new JavaScriptSerializer{MaxJsonLength=8*1048576}.Deserialize<ReleaseManifest>(reader.ReadToEnd());UpdateContract.Validate(manifest);
     if(manifest.Version!=item.Version||(manifest.Channel??"")!=(item.Channel??"")||manifest.Build!=item.Build||names.Count!=manifest.Files.Count+1||manifest.Files.Any(x=>!names.Contains(x.Relative)))throw new IOException(L.Text("updates.unexpectedVersion"));
     foreach(var entry in zip.Entries.Where(x=>!x.FullName.Replace('\\','/').EndsWith("/"))){token.ThrowIfCancellationRequested();string target=UpdateContract.SafePath(directory,entry.FullName.Replace('\\','/'));Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] buffer=new byte[65536];long count=0;int read;while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();count+=read;if(count>entry.Length)throw new IOException(L.Text("updates.downloadLimit"));output.Write(buffer,0,read);}}}
     UpdateContract.Verify(directory,manifest);return manifest;
    }
   }
  }
  internal static void Download(VendorUpdate item,CancellationToken token,Action<string> report){
   CloudStore.SecureDirectory(UpdateRegistry.Root);string relative="application\\"+Guid.NewGuid().ToString("N"),job=UpdateRegistry.SafePath(UpdateRegistry.Root,relative);Directory.CreateDirectory(job);CloudStore.SecureDirectory(job);string zip=Path.Combine(job,AssetName);bool staged=false;
   try{using(var output=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None))UpdateNetwork.Download(item.Url,output,768L*1048576,token,(received,total)=>report(Math.Min(89,89.0*received/Math.Max(1,item.Size)).ToString("F0")+"%"));report(L.Text("updates.verifying"));Stage(item,zip,Path.Combine(job,"package"),token);token.ThrowIfCancellationRequested();ConfigurationIntegrity.Write(PendingPath,new PendingApplicationUpdate{Folder=relative,Package=item});staged=true;report("100%");}
   finally{if(File.Exists(zip))File.Delete(zip);if(!staged)PathSafety.DeleteTree(job);}
  }
  internal static string PrepareInstall(PendingApplicationUpdate pending){
   string root=Folder(pending),package=Path.Combine(root,"package"),job=Path.Combine(root,"attempt-"+Guid.NewGuid().ToString("N")),target=Path.GetFullPath(AppPaths.ProgramDirectory);Directory.CreateDirectory(job);var manifest=UpdateContract.Read<ReleaseManifest>(Path.Combine(package,UpdateContract.ManifestName));if(manifest.Version!=pending.Package.Version||manifest.Build!=pending.Package.Build||(manifest.Channel??"")!=(pending.Package.Channel??""))throw new IOException(L.Text("updates.unexpectedVersion"));UpdateContract.Verify(package,manifest);UpdateContract.RejectLinks(target);
   // Discover denied writes before closing the running application.
   foreach(string parent in manifest.Files.Select(x=>Path.GetDirectoryName(UpdateContract.SafePath(target,x.Relative))).Distinct(StringComparer.OrdinalIgnoreCase)){string existing=parent;while(!Directory.Exists(existing))existing=Path.GetDirectoryName(existing);string probe=Path.Combine(existing,".file-compass-update-"+Guid.NewGuid().ToString("N"));using(var output=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None)){}File.Delete(probe);}
   string worker=Path.Combine(job,"FileCompass.Updater.exe");using(var resource=typeof(ApplicationUpdates).Assembly.GetManifestResourceStream("FileCompass.UpdateWorker.exe"))using(var output=new FileStream(worker,FileMode.Create,FileAccess.Write,FileShare.None)){if(resource==null)throw new IOException("Missing update worker.");resource.CopyTo(output);}
   using(var current=Process.GetCurrentProcess())UpdateContract.Write(Path.Combine(job,"request.json"),new InstallRequest{ParentId=current.Id,ParentStarted=current.StartTime.ToUniversalTime().Ticks,Target=target,Package=package,OriginalHash=UpdateContract.Hash(current.MainModule.FileName),ManifestHash=UpdateContract.Hash(Path.Combine(package,UpdateContract.ManifestName)),Release=manifest});
   File.WriteAllText(Path.Combine(job,"request.sha256"),UpdateContract.Hash(Path.Combine(job,"request.json")));
   File.WriteAllText(Path.Combine(job,"restore.ps1"),"$ErrorActionPreference = 'Stop'\r\n& (Join-Path $PSScriptRoot 'FileCompass.Updater.exe') --rollback (Join-Path $PSScriptRoot 'request.json') (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'request.sha256') -Raw).Trim()\r\n");
   foreach(string signal in new[]{"start.signal","cancel.signal"})if(File.Exists(Path.Combine(job,signal)))File.Delete(Path.Combine(job,signal));return job;
  }
 }
 public partial class MainWindow {
  bool applicationUpdateClosing;
  internal async Task<bool> InstallApplicationUpdate(){
   if(applicationUpdateClosing||UpdateWorkRunning)return false;var pending=ApplicationUpdates.Pending();if(pending==null)return false;applicationUpdateClosing=true;EventHandler closed=null;Process worker=null;string job=null;
   try{
    await operationJournal.PendingWrites;if(UpdateWorkRunning)return false;
    job=await Task.Run(()=>ApplicationUpdates.PrepareInstall(pending));if(UpdateWorkRunning)return false;
    string request=Path.Combine(job,"request.json");worker=Process.Start(new ProcessStartInfo(Path.Combine(job,"FileCompass.Updater.exe"),SshTerminal.QuoteWindowsArgument(request)+" "+SshTerminal.QuoteWindowsArgument(UpdateContract.Hash(request))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=job});
    closed=delegate{File.WriteAllText(Path.Combine(job,"start.signal"),"Install requested by File Compass.");};Closed+=closed;Close();if(!UpdateHostClosed){Closed-=closed;File.WriteAllText(Path.Combine(job,"cancel.signal"),"Application close was cancelled.");return false;}return true;
   }finally{if(!UpdateHostClosed&&closed!=null)Closed-=closed;if(worker!=null)worker.Dispose();applicationUpdateClosing=false;}
  }
 }
}
