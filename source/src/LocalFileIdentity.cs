using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileCompass
{
    internal static class LocalFileIdentity
    {
        [StructLayout(LayoutKind.Sequential)] struct Info { internal uint Attributes, CreatedLow, CreatedHigh, AccessLow, AccessHigh, WrittenLow, WrittenHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
        internal static string Read(string path)
        {
            using (var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                Info info; if (handle.IsInvalid || !GetFileInformationByHandle(handle, out info) || (info.IndexHigh == 0 && info.IndexLow == 0)) return null;
                return info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8");
            }
        }
    }
}

