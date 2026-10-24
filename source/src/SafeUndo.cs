using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32.SafeHandles;
namespace FileCompass {
 internal sealed class UndoReceipt {
  internal string Source,Target,Hash;internal bool Directory,Move,RestoreFolder;internal uint Volume,High,Low;internal long Created,Length,Written;
  internal static UndoReceipt Capture(string path,string source,bool move,byte[] hash=null){
   using(var h=SafeUndo.Open(path,false)){var i=SafeUndo.Info(h);return new UndoReceipt{Source=source,Target=path,Move=move,Directory=(i.Attributes&16)!=0,Volume=i.Volume,High=i.High,Low=i.Low,Created=i.Creation,Length=((long)i.SizeHigh<<32)|i.SizeLow,Written=i.Write,Hash=hash==null?null:Convert.ToBase64String(hash)};}
  }
  internal static UndoReceipt Committed(UndoReceipt receipt,string target){if(receipt==null)return null;try{using(var h=SafeUndo.Open(target,false)){var i=SafeUndo.Info(h);if(receipt.Volume!=i.Volume||receipt.High!=i.High||receipt.Low!=i.Low)return null;receipt.Target=target;receipt.Created=i.Creation;return receipt;}}catch{return null;}}
  internal bool Matches(SafeUndo.FileInfo i){return Volume==i.Volume&&High==i.High&&Low==i.Low&&Created==i.Creation;}
 }
 internal sealed class UndoGroup {internal string Title;internal List<UndoReceipt> Items;internal List<UndoReceipt> OriginalItems;internal DateTime Created=DateTime.Now;public override string ToString(){return Title+" ("+Items.Count+")";} }
 internal static class SafeUndo {
  static readonly object gate=new object();static readonly List<UndoGroup> groups=new List<UndoGroup>();
  internal static UndoReceipt[] Remaining(UndoGroup group){lock(gate)return group.Items.ToArray();}
  internal static UndoGroup[] Groups(){lock(gate)return groups.ToArray();}
  internal static UndoGroup Record(string title,IEnumerable<UndoReceipt> entries){var items=entries.Where(x=>x!=null).Take(5000).ToList();if(items.Count==0)return null;lock(gate){var group=new UndoGroup{Title=title,Items=items,OriginalItems=items.ToList()};groups.Insert(0,group);if(groups.Count>20)groups.RemoveAt(groups.Count-1);return group;}}
  [StructLayout(LayoutKind.Sequential,Pack=4)]internal struct FileInfo{internal uint Attributes;internal long Creation,Access,Write;internal uint Volume,SizeHigh,SizeLow,Links,High,Low;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInfo info);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle handle,int kind,IntPtr info,uint bytes);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileTime(SafeFileHandle file,IntPtr created,IntPtr accessed,ref long written);
  internal static SafeFileHandle Open(string path,bool delete){CopyEngine.RejectLinks(path);var h=CreateFile(path,delete?0x80010100u:0x80000000u,delete?1u:7u,IntPtr.Zero,3,0x02200000,IntPtr.Zero);if(h.IsInvalid){h.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());}return h;}
  internal static FileInfo Info(SafeFileHandle h){FileInfo i;if(!GetFileInformationByHandle(h,out i))throw new Win32Exception(Marshal.GetLastWin32Error());if((i.Attributes&1024)!=0)throw new IOException(L.Text("tools1.undoChanged"));return i;}
  static void Attributes(SafeFileHandle handle,uint attributes){var p=Marshal.AllocHGlobal(40);try{for(int i=0;i<40;i++)Marshal.WriteByte(p,i,0);Marshal.WriteInt32(p,32,(int)(attributes==0?128:attributes));if(!SetFileInformationByHandle(handle,0,p,40))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{Marshal.FreeHGlobal(p);}}
  static void Delete(SafeFileHandle handle){uint attributes=Info(handle).Attributes;bool readOnly=(attributes&1)!=0;if(readOnly)Attributes(handle,attributes&~1u);var p=Marshal.AllocHGlobal(4);try{Marshal.WriteInt32(p,1);if(!SetFileInformationByHandle(handle,4,p,4)){var error=new Win32Exception(Marshal.GetLastWin32Error());if(readOnly)Attributes(handle,attributes);throw error;}}finally{Marshal.FreeHGlobal(p);}}
  static void Rename(SafeFileHandle handle,string path){byte[] name=System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(path));int offset=IntPtr.Size==8?20:12;var p=Marshal.AllocHGlobal(offset+name.Length+2);try{for(int j=0;j<offset+name.Length+2;j++)Marshal.WriteByte(p,j,0);Marshal.WriteInt32(p,IntPtr.Size==8?16:8,name.Length);Marshal.Copy(name,0,IntPtr.Add(p,offset),name.Length);if(!SetFileInformationByHandle(handle,3,p,(uint)(offset+name.Length+2)))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{Marshal.FreeHGlobal(p);}}
  internal static void RestoreByCopy(SafeFileHandle h,Stream stream,string source,CancellationToken token){
       // The verified target stays in place if restoring the original fails.
       PathSafety.RejectLinks(source);
       using(PathSafety.LockAncestors(source)){
       System.IO.Directory.CreateDirectory(Path.GetDirectoryName(source));CopyEngine.RejectLinks(source);
       using(PathSafety.LockAncestors(source))
       using(var restored=CreateFile(source,0xC0010000,0,IntPtr.Zero,1,0x00200000,IntPtr.Zero)){
        if(restored.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
        using(var output=new FileStream(restored,FileAccess.ReadWrite))try{
         byte[] buffer=new byte[1048576];int read;while((read=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();output.Write(buffer,0,read);}output.Flush(true);output.Position=0;stream.Position=0;
         using(var sha=SHA256.Create()){var original=sha.ComputeHash(stream);var copy=sha.ComputeHash(output);if(!original.SequenceEqual(copy))throw new IOException(L.Text("tools1.undoChanged"));}
         long written=Info(h).Write;if(!SetFileTime(restored,IntPtr.Zero,IntPtr.Zero,ref written))throw new Win32Exception(Marshal.GetLastWin32Error());Attributes(restored,Info(h).Attributes&39u);
        }catch{Delete(restored);throw;}
       }

   }
  }
  static readonly SemaphoreSlim applying=new SemaphoreSlim(1,1);
  internal static void Apply(UndoGroup group,CancellationToken token){if(!applying.Wait(0))throw new IOException(L.Text("app.fileOperationBusy"));try{ApplyCore(group,token);}finally{applying.Release();}}
  internal static void ApplySelection(UndoGroup original,UndoReceipt[] selected,CancellationToken token){if(!applying.Wait(0))throw new IOException(L.Text("app.fileOperationBusy"));try{lock(gate)if(selected.Length==0||selected.Any(x=>!original.Items.Contains(x)))throw new IOException(L.Text("undoHistory.stale"));var subset=new UndoGroup{Title=original.Title,Items=selected.ToList(),OriginalItems=selected.ToList()};try{ApplyCore(subset,token);}finally{lock(gate)foreach(var receipt in selected.Where(x=>!subset.Items.Contains(x)))original.Items.Remove(receipt);}}finally{applying.Release();}}
  static void ApplyCore(UndoGroup group,CancellationToken token){
   // Successful receipts are removed immediately; a later failure leaves the remaining work reviewable.
   foreach(var r in group.Items.OrderBy(x=>x.Directory?1:0).ThenByDescending(x=>x.Target.Length).ToArray()){
    token.ThrowIfCancellationRequested();CopyEngine.RejectLinks(r.Target);
    if(r.Move){CopyEngine.RejectLinks(r.Source);if(File.Exists(r.Source)||System.IO.Directory.Exists(r.Source))throw new IOException(L.Text("tools1.undoCollision")+" "+r.Source);}
    using(PathSafety.LockAncestors(r.Target))
    using(r.Move||r.RestoreFolder?PathSafety.LockAncestors(r.Source):null)
    using(var h=Open(r.Target,true)){
     if(!r.Matches(Info(h)))throw new IOException(L.Text("tools1.undoChanged")+" "+r.Target);
     if(r.Directory){if(r.Move){System.IO.Directory.CreateDirectory(Path.GetDirectoryName(r.Source));using(PathSafety.LockAncestors(r.Source))Rename(h,r.Source);}else {if(r.RestoreFolder){PathSafety.RejectLinks(r.Source);System.IO.Directory.CreateDirectory(r.Source);}if(System.IO.Directory.EnumerateFileSystemEntries(r.Target).Any())throw new IOException(L.Text("tools1.undoNotEmpty")+" "+r.Target);Delete(h);}}
     else using(var stream=new FileStream(h,FileAccess.Read)){
      if(r.Hash!=null){using(var sha=SHA256.Create()){byte[] buffer=new byte[1048576];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,n,null,0);}sha.TransformFinalBlock(new byte[0],0,0);if(Convert.ToBase64String(sha.Hash)!=r.Hash)throw new IOException(L.Text("tools1.undoChanged")+" "+r.Target);}stream.Position=0;}
      if(!r.Move)Delete(h);
      else if(String.Equals(Path.GetPathRoot(r.Target),Path.GetPathRoot(r.Source),StringComparison.OrdinalIgnoreCase)){
       System.IO.Directory.CreateDirectory(Path.GetDirectoryName(r.Source));CopyEngine.RejectLinks(r.Source);using(PathSafety.LockAncestors(r.Source))try{Rename(h,r.Source);}catch(Win32Exception ex){if(ex.NativeErrorCode!=17)throw;RestoreByCopy(h,stream,r.Source,token);Delete(h);}
      }else{
       RestoreByCopy(h,stream,r.Source,token);
       Delete(h);
      }
     }
    }
    lock(gate)group.Items.Remove(r);
    if(r.Move)FileAssociations.Moved(r.Target,r.Source);
   }
   // Keep completed groups visible in the transaction history.
  }
 }
 public partial class MainWindow {
  void ShowSafeUndo(){OpenUndoTransactions();}

 }
}
