using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
namespace FileCompass {
 internal static class UpdateRegistry {
    internal static readonly object Gate=new object();
    static readonly Dictionary<string,int> users=new Dictionary<string,int>();
    static readonly Dictionary<string,InstalledUpdate> pins=new Dictionary<string,InstalledUpdate>();
    static string pinnedProfile;
    static void CheckProfile(){if(pinnedProfile!=Index){pins.Clear();users.Clear();pinnedProfile=Index;}}
    internal static string Root{get{return Path.Combine(AppPaths.DataDirectory,"updates");}}
    static string Index{get{return Path.Combine(AppPaths.ConfigDirectory,"updates.xml");}}
    internal static string StateError="";
    static UpdatePreferences ReadStrict(){var state=File.Exists(Index)?ConfigurationIntegrity.Read<UpdatePreferences>(Index):new UpdatePreferences();if(state.Available==null||state.Active==null||state.Previous==null||state.Pending==null||state.RestoreBundled==null||state.Active.Count>4||state.Pending.Count>4||state.Previous.Count>4||state.RestoreBundled.Any(x=>!VendorUpdates.Ids.Contains(x)))throw new IOException(L.Text("updates.invalidState"));foreach(var package in state.Active.Concat(state.Previous).Concat(state.Pending))Validate(package);return state;}
    internal static UpdatePreferences State(){lock(Gate){try{var state=ReadStrict();StateError="";return state;}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is InvalidOperationException||ex is ArgumentException||ex is System.Security.Cryptography.CryptographicException){StateError=ex.Message;return new UpdatePreferences{Daily=false};}}}
    internal static void Edit(Action<UpdatePreferences> edit){lock(Gate){var state=ReadStrict();edit(state);ConfigurationIntegrity.Write(Index,state);}}
    internal static string SafePath(string root,string relative){
        if(String.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.IndexOf(':')>=0||relative.Split('\\','/').Any(x=>x==".."||x=="."||x.Length==0))throw new IOException(L.Text("updates.invalidPath"));
        string result=Path.GetFullPath(Path.Combine(root,relative));if(!result.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("updates.pathEscapes"));PathSafety.RejectLinks(result);return result;
    }
    static void Validate(InstalledUpdate package){
        if(package==null||!VendorUpdates.Ids.Contains(package.Id))throw new IOException(L.Text("updates.unknownInstalled"));
        if(!Regex.IsMatch(package.Version??"",@"^\d+(\.\d+){1,3}$")||!Regex.IsMatch(package.Folder??"",@"^packages\\"+package.Id+@"\\[a-f0-9]{64}(-[a-f0-9]{32})?$"))throw new IOException(L.Text("updates.invalidReceipt"));
        SafePath(Root,package.Folder);var expected=VendorUpdates.Files(package.Id);if(package.Files==null||package.Files.Count!=expected.Length||expected.Any(x=>package.Files.Count(f=>f.Relative==x)!=1)||package.Files.Any(x=>x.Size<1||x.Size>512L*1048576||x.Hash!=VendorUpdates.Sha(x.Hash)))throw new IOException(L.Text("updates.incompleteReceipt"));
    }
    internal static string Resolve(string relative){lock(Gate){CheckProfile();string id=VendorUpdates.Ids.FirstOrDefault(x=>VendorUpdates.Files(x).Contains(relative));InstalledUpdate package=null;if(id!=null&&!pins.TryGetValue(id,out package)){package=State().Active.FirstOrDefault(x=>x.Id==id);pins[id]=package;}if(package==null)return SafePath(PluginIntegrity.PluginRoot,relative);Validate(package);return SafePath(SafePath(Root,package.Folder),relative);}}
    internal static bool Expected(string path,out string hash){lock(Gate){CheckProfile();hash=null;path=Path.GetFullPath(path);var state=State();foreach(var package in state.Active.Concat(state.Previous).Concat(state.Pending).Concat(pins.Values.Where(x=>x!=null))){Validate(package);foreach(var file in package.Files)if(String.Equals(path,SafePath(SafePath(Root,package.Folder),file.Relative),StringComparison.OrdinalIgnoreCase)){hash=file.Hash;return true;}}return false;}}
    internal static IDisposable Acquire(IEnumerable<string> relatives){lock(Gate){var lease=new Lease();try{foreach(string relative in relatives){string path=Resolve(relative),expected;if(!Expected(path,out expected)&&!PluginHashes.Expected.TryGetValue(relative,out expected))throw new IOException(L.Text("updates.untrustedPlugin")+relative);PathSafety.RejectLinks(path);var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);lease.Files.Add(file);if(VendorUpdates.Hash(file)!=expected.ToLowerInvariant())throw new IOException(L.Text("updates.pluginIntegrity")+relative);string id=VendorUpdates.Ids.FirstOrDefault(x=>VendorUpdates.Files(x).Contains(relative));if(id!=null){if(!users.ContainsKey(id))users[id]=0;users[id]++;lease.Ids.Add(id);}}return lease;}catch{lease.Dispose();throw;}}}
    sealed class Lease:IDisposable{internal List<FileStream> Files=new List<FileStream>();internal List<string> Ids=new List<string>();bool disposed;public void Dispose(){lock(Gate){if(disposed)return;disposed=true;foreach(var file in Files)file.Dispose();foreach(var id in Ids)users[id]--;}}}
    internal static bool Busy(string id){lock(Gate){int count;return users.TryGetValue(id,out count)&&count>0;}}
    internal static bool RequiresRestart(string id){
        if(id=="rclone")return true;
        if(id!="7zip")return false;
        try{using(var process=Process.GetCurrentProcess())return process.Modules.Cast<ProcessModule>().Any(x=>String.Equals(x.ModuleName,"7z.dll",StringComparison.OrdinalIgnoreCase));}catch{return true;}
    }
    internal static void Verify(InstalledUpdate package){Validate(package);foreach(var entry in package.Files){string path=SafePath(SafePath(Root,package.Folder),entry.Relative);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))if(stream.Length!=entry.Size||VendorUpdates.Hash(stream)!=entry.Hash)throw new IOException(L.Text("updates.changedPrepared"));}}
    internal static bool Activate(string id,bool startup=false){lock(Gate){
        if(Busy(id)||(!startup&&RequiresRestart(id)))return false;
        var state=State();var package=state.Pending.FirstOrDefault(x=>x.Id==id);if(package==null)return false;Verify(package);
        var old=state.Active.FirstOrDefault(x=>x.Id==id);state.Previous.RemoveAll(x=>x.Id==id);if(old!=null)state.Previous.Add(old);state.Active.RemoveAll(x=>x.Id==id);state.Active.Add(package);state.Pending.Remove(package);ConfigurationIntegrity.Write(Index,state);CheckProfile();pins[id]=package;return true;
    }}
    internal static void Rollback(string id){lock(Gate){if(Busy(id))throw new IOException(L.Text("updates.waiting"));var state=State();var old=state.Previous.FirstOrDefault(x=>x.Id==id);if(old!=null)Verify(old);state.Pending.RemoveAll(x=>x.Id==id);state.RestoreBundled.Remove(id);if(RequiresRestart(id)){if(old!=null)state.Pending.Add(old);else state.RestoreBundled.Add(id);ConfigurationIntegrity.Write(Index,state);return;}var current=state.Active.FirstOrDefault(x=>x.Id==id);state.Active.RemoveAll(x=>x.Id==id);state.Previous.RemoveAll(x=>x.Id==id);if(old!=null)state.Active.Add(old);if(current!=null)state.Previous.Add(current);ConfigurationIntegrity.Write(Index,state);CheckProfile();pins[id]=old;}}
    internal static void Startup(){try{Edit(state=>{foreach(string id in state.RestoreBundled){var current=state.Active.FirstOrDefault(x=>x.Id==id);state.Active.RemoveAll(x=>x.Id==id);state.Previous.RemoveAll(x=>x.Id==id);if(current!=null)state.Previous.Add(current);lock(Gate){CheckProfile();pins[id]=null;}}state.RestoreBundled.Clear();});foreach(string id in VendorUpdates.Ids){try{Activate(id,true);}catch(Exception ex){System.Diagnostics.Trace.WriteLine("Update activation: "+ex.Message);}}}catch(Exception ex){StateError=ex.Message;System.Diagnostics.Trace.WriteLine("Update state: "+ex.Message);}}
    internal static InstalledUpdate Stage(VendorUpdate update,string archive,CancellationToken token,Action<string> report){
        token.ThrowIfCancellationRequested();
        if(!VendorUpdates.ValidOrigin(update)||VendorUpdates.Sha(update.Hash)!=update.Hash)throw new IOException(L.Text("updates.invalidVendor"));
        using(var stream=new FileStream(archive,FileMode.Open,FileAccess.Read,FileShare.Read))if(VendorUpdates.Hash(stream)!=update.Hash)throw new IOException(L.Text("updates.checksumMismatch"));
        string folder="packages\\"+update.Id+"\\"+update.Hash;string target=SafePath(Root,folder);
        if(Directory.Exists(target))folder+="-"+Guid.NewGuid().ToString("N");target=SafePath(Root,folder);Directory.CreateDirectory(target);
        var receipt=new InstalledUpdate{Id=update.Id,Version=update.Version,Folder=folder,Source=update.Source,PackageHash=update.Hash};
        try{
            var document=ArchiveEngine.Open(archive,null,token);var selected=new List<string>();
            foreach(string relative in VendorUpdates.Files(update.Id)){
                string name=Path.GetFileName(relative);var matches=document.Items.Where(x=>!x.Directory&&String.Equals(Path.GetFileName(x.Path.Replace('/','\\')),name,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(update.Id=="smart"&&matches.Length>1)matches=matches.Where(x=>x.Path.IndexOf("bin64/",StringComparison.OrdinalIgnoreCase)>=0||x.Path.IndexOf("bin64\\",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                if(matches.Length!=1||matches[0].Bytes<1||matches[0].Bytes>512L*1048576)throw new IOException(L.Text("updates.requiredFileMissing")+name);selected.Add(matches[0].Path);
            }
            string extracted=SafePath(target,"extract");ArchiveEngine.Extract(document,selected,extracted,token,report);
            for(int i=0;i<selected.Count;i++){
                token.ThrowIfCancellationRequested();string source=SafePath(extracted,selected[i].Replace('/','\\'));if(!VendorUpdates.Is64Bit(source))throw new IOException(L.Text("updates.invalidArchitecture"));
                string relative=VendorUpdates.Files(update.Id)[i],destination=SafePath(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination);
                using(var file=File.OpenRead(destination))receipt.Files.Add(new UpdateFile{Relative=relative,Size=file.Length,Hash=VendorUpdates.Hash(file)});
            }
            Verify(receipt);VendorUpdates.Probe(receipt,token);PathSafety.DeleteTree(extracted);token.ThrowIfCancellationRequested();Edit(state=>{state.RestoreBundled.Remove(update.Id);state.Pending.RemoveAll(x=>x.Id==update.Id);state.Pending.Add(receipt);});return receipt;
        }catch{if(target.StartsWith(Path.GetFullPath(Root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))PathSafety.DeleteTree(target);throw;}
    }
 }
}
