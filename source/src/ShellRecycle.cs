using System;
using System.IO;
using System.Runtime.InteropServices;
namespace FileCompass {
 internal static class ShellRecycle {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
  struct Operation {internal IntPtr Owner;internal uint Function;[MarshalAs(UnmanagedType.LPWStr)]internal string From;[MarshalAs(UnmanagedType.LPWStr)]internal string To;internal ushort Flags;[MarshalAs(UnmanagedType.Bool)]internal bool Aborted;internal IntPtr Mappings;[MarshalAs(UnmanagedType.LPWStr)]internal string Title;}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHFileOperation(ref Operation operation);
  internal static void Delete(string path){
   // Keep Windows recycling semantics while reporting errors in the application's per-task dialog.
   var operation=new Operation{Function=3,From=Path.GetFullPath(path)+"\0\0",Flags=0x40|0x4|0x10|0x400};
   int result=SHFileOperation(ref operation);if(result!=0)throw new IOException(new System.ComponentModel.Win32Exception(result).Message,unchecked((int)0x80070000)|result);if(operation.Aborted)throw new OperationCanceledException();
  }
 }
}
