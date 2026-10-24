using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace FileCompass
{
    internal static class PathSafety
    {
        [StructLayout(LayoutKind.Sequential)] struct Information { internal uint Attributes; internal System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; internal uint Volume, High, Low, Links, IndexHigh, IndexLow; }
        [DllImport("kernel32.dll", EntryPoint="CreateFileW", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle Open(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle file,out Information info);
        internal static void RejectLinks(string path)
        {
            for (string current=Path.GetFullPath(path);current!=null;current=Path.GetDirectoryName(current))
            {
                try { if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Symbolic links and junctions are not allowed here: "+current); }
                catch(FileNotFoundException){} catch(DirectoryNotFoundException){}
            }
        }
        internal static IDisposable LockAncestors(string path)
        {
            var lease=new Handles();
            try {
                var paths=new Stack<string>(); for(string p=Path.GetDirectoryName(Path.GetFullPath(path));p!=null;p=Path.GetDirectoryName(p))paths.Push(p);
                foreach(string p in paths) {
                    var handle=Open(p,0x80,3,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
                    if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();if(error==2||error==3)continue;throw new IOException("Cannot protect parent directory: "+p,new Win32Exception(error));}
                    lease.Items.Add(handle); Information info;
                    if(!GetFileInformationByHandle(handle,out info))throw new Win32Exception(Marshal.GetLastWin32Error());
                    if((info.Attributes&0x400)!=0)throw new IOException("Symbolic links and junctions are not allowed here: "+p);
                }
                return lease;
            }catch{lease.Dispose();throw;}
        }
        internal static void DeleteTree(string path)
        {
            DeleteOperations.Validate(path,false);
            using(LockAncestors(path)) {
                FileAttributes attributes;
                try{attributes=File.GetAttributes(path);}catch(FileNotFoundException){return;}catch(DirectoryNotFoundException){return;}
                if((attributes&FileAttributes.Directory)!=0){
                    if((attributes&FileAttributes.ReparsePoint)==0)foreach(string child in Directory.EnumerateFileSystemEntries(path))DeleteTree(child);
                    Directory.Delete(path,false);
                }else File.Delete(path);
            }
        }
        sealed class Handles:IDisposable { internal readonly List<SafeFileHandle> Items=new List<SafeFileHandle>(); public void Dispose(){for(int i=Items.Count-1;i>=0;i--)Items[i].Dispose();Items.Clear();} }
    }
}
