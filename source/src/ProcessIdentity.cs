using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
namespace FileCompass {
    internal static class ProcessIdentity {
        [StructLayout(LayoutKind.Sequential)] struct Basic { internal IntPtr Reserved1,Peb,Reserved2,Reserved3,Pid,Parent; }
        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process,int information,ref Basic data,int size,out int returned);
        internal static Process Parent(){var data=new Basic();int returned;if(NtQueryInformationProcess(Process.GetCurrentProcess().Handle,0,ref data,Marshal.SizeOf(typeof(Basic)),out returned)!=0)throw new IOException("Cannot verify the calling process.");return Process.GetProcessById(data.Parent.ToInt32());}
        internal static void VerifyExecutable(uint pid,string expected){try{using(var process=Process.GetProcessById(checked((int)pid)))if(process.HasExited||process.SessionId!=Process.GetCurrentProcess().SessionId||!String.Equals(Path.GetFullPath(process.MainModule.FileName),Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase))throw new IOException("Unexpected IPC peer executable.");}catch(System.ComponentModel.Win32Exception ex){throw new IOException("IPC peer cannot be verified.",ex);}catch(ArgumentException ex){throw new IOException("IPC peer exited before verification.",ex);}catch(InvalidOperationException ex){throw new IOException("IPC peer exited before verification.",ex);}}
    }
}
