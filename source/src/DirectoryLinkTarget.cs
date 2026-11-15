using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FileCompass {
 internal static class DirectoryLinkTarget {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
  internal static string Resolve(string path) {
   try {
    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint)==0) return path;
    using(var handle=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)) {
     if(handle.IsInvalid)return path;
     var data=new byte[16384]; int length;
     if(!DeviceIoControl(handle,0x000900A8,IntPtr.Zero,0,data,data.Length,out length,IntPtr.Zero)||length<16)return path;
     uint tag=BitConverter.ToUInt32(data,0); int start;
     if(tag==0xA0000003)start=16; else if(tag==0xA000000C)start=20; else return path;
     int offset=BitConverter.ToUInt16(data,8), size=BitConverter.ToUInt16(data,10);
     if(size==0 || start+offset+size>length)return path;
     string target=Encoding.Unicode.GetString(data,start+offset,size);
     if(target.StartsWith(@"\??\UNC\",StringComparison.OrdinalIgnoreCase)) target=@"\\"+target.Substring(8);
     else if(target.StartsWith(@"\??\",StringComparison.OrdinalIgnoreCase)) target=target.Substring(4);
     else if(!Path.IsPathRooted(target)) target=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path),target));
     return target;
    }
   } catch { return path; }
  }
 }
}
