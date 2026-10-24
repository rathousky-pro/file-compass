using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
namespace FileCompass {
 internal sealed class DuplicateFile {
  public bool Selected {get;set;}
  public int Group {get;set;}
  public string Path {get;set;}
  public string DisplayPath {get;set;}
  public string Name {get{return System.IO.Path.GetFileName(Path);}}
  public long Bytes {get;set;}
  public DateTime Modified {get;set;}
  public string Digest {get;set;} internal string Identity;
  public string Size {get{return CopyUi.Bytes(Bytes);}}
 }
 internal static class DuplicateEngine {
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
  struct HandleInfo {
   public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;
   public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
  }
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]
  static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,out HandleInfo info);
  internal static string Identity(string path) {
   using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
    HandleInfo info;if(!GetFileInformationByHandle(input.SafeFileHandle,out info)||(info.IndexHigh==0&&info.IndexLow==0))throw new IOException(L.Text("duplicates.identityFailed"));
    return info.Volume+":"+info.IndexHigh+":"+info.IndexLow;
   }
  }
  internal static string Digest(string path,long bytes,DateTime modified,CancellationToken token) {
   if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException(L.Text("duplicates.changed"));
   using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
   using(var hash=SHA256.Create()){
    if(input.Length!=bytes||File.GetLastWriteTimeUtc(path)!=modified)throw new IOException(L.Text("duplicates.changed"));
    var buffer=new byte[131072];int read;
    while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();hash.TransformBlock(buffer,0,read,null,0);}
    hash.TransformFinalBlock(new byte[0],0,0);
    if(input.Length!=bytes||File.GetLastWriteTimeUtc(path)!=modified)throw new IOException(L.Text("duplicates.changed"));
    return Convert.ToBase64String(hash.Hash);
   }
  }
  internal static List<DuplicateFile> Scan(string[] roots,CancellationToken token,Action<string> progress,Action<string> warning) {
   var candidates=new List<DuplicateFile>();var identities=new HashSet<string>(StringComparer.Ordinal);var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var pending=new Stack<string>(roots.Select(System.IO.Path.GetFullPath).Reverse());
   while(pending.Count>0){
    token.ThrowIfCancellationRequested();string path=pending.Pop();if(!visited.Add(path))continue;
    try{
     progress(path);var attrs=File.GetAttributes(path);if((attrs&FileAttributes.ReparsePoint)!=0)continue;
     if((attrs&FileAttributes.Directory)!=0){foreach(var child in Directory.EnumerateFileSystemEntries(path))pending.Push(child);}
     else{var file=new FileInfo(path);string identity=Identity(path);if(identities.Add(identity))candidates.Add(new DuplicateFile{Path=path,Bytes=file.Length,Modified=file.LastWriteTimeUtc,Identity=identity});}
    }catch(IOException ex){warning(path+": "+ex.Message);}catch(UnauthorizedAccessException ex){warning(path+": "+ex.Message);}
   }
   var result=new List<DuplicateFile>();int group=0;
   foreach(var size in candidates.GroupBy(x=>x.Bytes).Where(x=>x.Count()>1)){
    var hashes=new Dictionary<string,List<DuplicateFile>>();
    foreach(var file in size){
     token.ThrowIfCancellationRequested();
     try{progress(file.Path);file.Digest=Digest(file.Path,file.Bytes,file.Modified,token);
      List<DuplicateFile> list;if(!hashes.TryGetValue(file.Digest,out list))hashes[file.Digest]=list=new List<DuplicateFile>();list.Add(file);
     }catch(IOException ex){warning(file.Path+": "+ex.Message);}catch(UnauthorizedAccessException ex){warning(file.Path+": "+ex.Message);}
    }
    foreach(var equal in hashes.Values.Where(x=>x.Count>1)){
     group++;foreach(var file in equal.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase)){file.Group=group;result.Add(file);}
    }
   }
   return result;
  }
  internal static void SelectCopies(IEnumerable<DuplicateFile> files) {
   foreach(var group in files.GroupBy(x=>x.Group)){bool first=true;foreach(var file in group){file.Selected=!first;first=false;}}
  }
  internal static void Recycle(List<DuplicateFile> files,CancellationToken token,Action<string> recycle) {
   var selected=files.Where(x=>x.Selected).ToArray();
   if(selected.Any(x=>!files.Any(y=>y.Group==x.Group&&!y.Selected)))throw new IOException(L.Text("duplicates.keepOne"));
   // Validate every planned deletion and retained representative before touching any file.
   foreach(var file in selected.Concat(files.Where(x=>!x.Selected&&selected.Any(y=>y.Group==x.Group))).Distinct()){
    token.ThrowIfCancellationRequested();
    if(Identity(file.Path)!=file.Identity||Digest(file.Path,file.Bytes,file.Modified,token)!=file.Digest)throw new IOException(L.Text("duplicates.changed")+" "+file.Path);
   }
   foreach(var file in selected){
    token.ThrowIfCancellationRequested();
    if(Identity(file.Path)!=file.Identity||Digest(file.Path,file.Bytes,file.Modified,token)!=file.Digest)throw new IOException(L.Text("duplicates.changed")+" "+file.Path);
    var keeper=files.First(x=>x.Group==file.Group&&!x.Selected);
    if(Identity(keeper.Path)!=keeper.Identity||Digest(keeper.Path,keeper.Bytes,keeper.Modified,token)!=file.Digest)throw new IOException(L.Text("duplicates.changed")+" "+keeper.Path);
    recycle(file.Path);files.Remove(file);
   }
  }
 }
}
