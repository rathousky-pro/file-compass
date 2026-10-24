using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileCompass
{
    internal sealed class LocalMoveGuard : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Information { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written; public uint Volume,SizeHigh,SizeLow,Links,IdHigh,IdLow; }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle h,out Information info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetFileInformationByHandle(SafeFileHandle handle,int kind,ref int value,uint size);
        readonly string path; readonly Information identity; FileStream guard;
        internal LocalMoveGuard(string source,long expectedLength,DateTime expectedTime) { path = source; if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.sourceIsLink")); guard = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read); if (!GetFileInformationByHandle(guard.SafeFileHandle,out identity) || guard.Length != expectedLength || File.GetLastWriteTime(path) != expectedTime || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) { guard.Dispose(); throw new IOException(L.Text("transfer.sourceIdentityUnverified")); } }
        internal void Delete()
        {
            if (guard != null) { guard.Dispose(); guard = null; }
            using (var handle = CreateFile(path,0x80010000,1,IntPtr.Zero,3,0x00200000,IntPtr.Zero)) {
                Information current; if (handle.IsInvalid || !GetFileInformationByHandle(handle,out current) || (current.Attributes & 0x400) != 0 || current.Volume != identity.Volume || current.IdHigh != identity.IdHigh || current.IdLow != identity.IdLow || current.SizeHigh != identity.SizeHigh || current.SizeLow != identity.SizeLow || current.Written.dwHighDateTime != identity.Written.dwHighDateTime || current.Written.dwLowDateTime != identity.Written.dwLowDateTime) throw new IOException(L.Text("transfer.changedSourcePreserved"));
                int remove = 1; if (!SetFileInformationByHandle(handle,4,ref remove,4)) throw new IOException(L.Text("transfer.copyCompleteSourceDeleteFailedPrefix") + new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
        }
        public void Dispose() { if (guard != null) guard.Dispose(); guard = null; }
    }
}
namespace FileCompass.NetworkTransport
{
    public sealed partial class Session
    {
        internal void RemoveVerifiedFile(string path,long length,DateTime modified)
        {
            var info = GetFileInfo(path); if (info.FileType == 'L' || info.IsDirectory || info.Length != length || info.LastWriteTime != modified) throw new IOException(L.Text("transfer.sourceChangedAfterTransfer")); CheckAbort();
            if (options.Protocol == Protocol.Ftp) Curl.Commands("DELE " + path); else if (options.Protocol == Protocol.Sftp) ssh.Remove(path,false); else Command("rm -- " + Quote(path));
        }
        internal void RemoveEmptyDirectory(string path)
        {
            DeleteOperations.Validate(path,true); var info = GetFileInfo(path); if (!info.IsDirectory || info.FileType == 'L') throw new IOException(L.Text("transfer.sourceFolderChanged")); CheckAbort();
            if (options.Protocol == Protocol.Ftp) Curl.Commands("RMD " + path); else if (options.Protocol == Protocol.Sftp) ssh.Remove(path,true); else Command("rmdir -- " + Quote(path));
        }
    }
}

