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
  public string Date {get{return Modified.ToLocalTime().ToString("g");}}
  public string Match {get{return L.Text(Digest == null ? "duplicates.criteriaMatch" : "duplicates.contentMatch");}}
 }
 internal sealed class DuplicateOptions {
  internal bool Content=true, Name, Size, Date, Extension, Recursive=true, IncludeHidden, IncludeEmpty=true, IgnoreCase=true, IgnoreExtension;
  internal long Minimum, Maximum=Int64.MaxValue;
  internal DateTime? From, To;
  internal string[] Extensions=new string[0], Excluded=new string[0];
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
  static string Sample(DuplicateFile file,CancellationToken token) {
   token.ThrowIfCancellationRequested();
   if((File.GetAttributes(file.Path)&FileAttributes.ReparsePoint)!=0)throw new IOException(L.Text("duplicates.changed"));
   using(var input=new FileStream(file.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
   using(var hash=SHA256.Create()){
    if(input.Length!=file.Bytes||File.GetLastWriteTimeUtc(file.Path)!=file.Modified)throw new IOException(L.Text("duplicates.changed"));
    var buffer=new byte[65536];int count=ReadSample(input,buffer);hash.TransformBlock(buffer,0,count,null,0);
    if(input.Length>buffer.Length){input.Position=Math.Max(count,input.Length-buffer.Length);count=ReadSample(input,buffer);hash.TransformBlock(buffer,0,count,null,0);}
    hash.TransformFinalBlock(new byte[0],0,0);token.ThrowIfCancellationRequested();
    if(input.Length!=file.Bytes||File.GetLastWriteTimeUtc(file.Path)!=file.Modified)throw new IOException(L.Text("duplicates.changed"));
    return Convert.ToBase64String(hash.Hash);
   }
  }
  static int ReadSample(Stream input,byte[] buffer){int count=0,n;while(count<buffer.Length&&(n=input.Read(buffer,count,buffer.Length-count))>0)count+=n;return count;}
  internal static List<DuplicateFile> Scan(string[] roots,CancellationToken token,Action<string> progress,Action<string> warning) {
   return Scan(roots,new DuplicateOptions(),token,progress,warning);
  }
  internal static List<DuplicateFile> Scan(string[] roots,DuplicateOptions options,CancellationToken token,Action<string> progress,Action<string> warning,Action<List<DuplicateFile>> foundGroup=null) {
   if(!options.Content&&!options.Name&&!options.Size&&!options.Date&&!options.Extension)throw new ArgumentException(L.Text("duplicates.selectCriterion"));
   var candidates=new List<DuplicateFile>();var identities=new HashSet<string>(StringComparer.Ordinal);var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var excluded=options.Excluded.Select(System.IO.Path.GetFullPath).Select(x=>x.TrimEnd('\\')+"\\").ToArray();
   var pending=new Stack<Tuple<string,int>>(roots.Select(x=>Tuple.Create(System.IO.Path.GetFullPath(x),0)).Reverse());
   int scanned=0,sampled=0,hashed=0;
   while(pending.Count>0){
    token.ThrowIfCancellationRequested();var next=pending.Pop();string path=next.Item1;if(!visited.Add(path)||excluded.Any(x=>(path.TrimEnd('\\')+"\\").StartsWith(x,StringComparison.OrdinalIgnoreCase)))continue;
    try{
     progress(L.Text("duplicates.phaseScan")+" "+(++scanned)+" · "+path);var attrs=File.GetAttributes(path);if((attrs&FileAttributes.ReparsePoint)!=0)continue;
     if(!options.IncludeHidden && (attrs&(FileAttributes.Hidden|FileAttributes.System))!=0 && next.Item2>0)continue;
     if((attrs&FileAttributes.Directory)!=0){if(next.Item2==0||options.Recursive)foreach(var child in Directory.EnumerateFileSystemEntries(path))pending.Push(Tuple.Create(child,next.Item2+1));}
     else{var file=new FileInfo(path);if(file.Length<options.Minimum||file.Length>options.Maximum||(!options.IncludeEmpty&&file.Length==0)||
       (options.From.HasValue&&file.LastWriteTime<options.From.Value)||(options.To.HasValue&&file.LastWriteTime>options.To.Value)||
       (options.Extensions.Length>0&&!options.Extensions.Contains(file.Extension.TrimStart('.'),StringComparer.OrdinalIgnoreCase)))continue;
      string identity=Identity(path);if(identities.Add(identity))candidates.Add(new DuplicateFile{Path=path,Bytes=file.Length,Modified=file.LastWriteTimeUtc,Identity=identity});}
    }catch(IOException ex){warning(path+": "+ex.Message);}catch(UnauthorizedAccessException ex){warning(path+": "+ex.Message);}
   }
   var result=new List<DuplicateFile>();int group=0;
   Func<string,string> normalize=s=>options.IgnoreCase?s.ToUpperInvariant():s;
   foreach(var size in candidates.GroupBy(x=>Tuple.Create(options.Content||options.Size?x.Bytes:0L,options.Name?normalize(options.IgnoreExtension?System.IO.Path.GetFileNameWithoutExtension(x.Path):x.Name):"",options.Date?x.Modified.Ticks:0L,options.Extension?normalize(System.IO.Path.GetExtension(x.Path)):"")).Where(x=>x.Count()>1)){
    var likely=new List<DuplicateFile>();
    if(options.Content){
     var samples=new Dictionary<string,List<DuplicateFile>>();
     foreach(var file in size){try{progress(L.Text("duplicates.phaseSample")+" "+(++sampled)+" · "+file.Path);string sample=Sample(file,token);List<DuplicateFile> list;if(!samples.TryGetValue(sample,out list))samples[sample]=list=new List<DuplicateFile>();list.Add(file);}catch(IOException ex){warning(file.Path+": "+ex.Message);}catch(UnauthorizedAccessException ex){warning(file.Path+": "+ex.Message);}}
     likely.AddRange(samples.Values.Where(x=>x.Count>1).SelectMany(x=>x));
    }else likely.AddRange(size);
    var hashes=new Dictionary<string,List<DuplicateFile>>();
    foreach(var file in likely){
     token.ThrowIfCancellationRequested();
     try{progress(L.Text(options.Content?"duplicates.phaseHash":"duplicates.phaseGroup")+" "+(++hashed)+" · "+file.Path);if(options.Content)file.Digest=Digest(file.Path,file.Bytes,file.Modified,token);
      string key=file.Digest??"";List<DuplicateFile> list;if(!hashes.TryGetValue(key,out list))hashes[key]=list=new List<DuplicateFile>();list.Add(file);
     }catch(IOException ex){warning(file.Path+": "+ex.Message);}catch(UnauthorizedAccessException ex){warning(file.Path+": "+ex.Message);}
    }
    foreach(var equal in hashes.Values.Where(x=>x.Count>1)){
     token.ThrowIfCancellationRequested();
     group++;var matches=equal.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToList();foreach(var file in matches){file.Group=group;result.Add(file);}if(foundGroup!=null)foundGroup(matches);
    }
   }
   return result;
  }
  internal static void SelectCopies(IEnumerable<DuplicateFile> files) {
   foreach(var group in files.GroupBy(x=>x.Group)){bool first=true;foreach(var file in group){file.Selected=!first;first=false;}}
  }
  internal static void Recycle(List<DuplicateFile> files,CancellationToken token,Action<string> recycle) {
   var selected=files.Where(x=>x.Selected).ToArray();
   if(selected.Any(x=>String.IsNullOrEmpty(x.Digest)))throw new IOException(L.Text("duplicates.requireContent"));
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
