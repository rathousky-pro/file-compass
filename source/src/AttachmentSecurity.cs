using System;
using System.IO;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileCompass
{
    internal static class AttachmentSecurity
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint attributes, IntPtr template);
        static FileStream OpenZone(string path, bool write)
        {
            // .NET Framework rejects ADS syntax in its managed path checks.
            var handle = CreateFile(Path.GetFullPath(path) + ":Zone.Identifier", write ? 0x40000000u : 0x80000000u, 1, IntPtr.Zero, write ? 2u : 3u, 0x80, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error(); handle.Dispose();
                if (!write && (error == 2 || error == 3)) return null;
                throw new IOException(L.Text("security.zoneAccess"), new Win32Exception(error));
            }
            try { return new FileStream(handle, write ? FileAccess.Write : FileAccess.Read); }
            catch { handle.Dispose(); throw; }
        }
        internal static bool RequiresLaunchConfirmation(string path)
        {
            return new[] { ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".scr", ".msi", ".msp", ".mst", ".lnk", ".url", ".pif", ".chm", ".reg", ".cpl", ".msc", ".application", ".appref-ms", ".scf", ".library-ms", ".search-ms", ".settingcontent-ms", ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".ppam" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }
        internal static string ReadZone(string path)
        {
            try
            {
                using (var input = OpenZone(path, false))
                {
                    if (input == null) return null;
                    if (input.Length > 65536) throw new IOException(L.Text("security.zoneInvalid"));
                    using (var reader = new StreamReader(input, Encoding.UTF8, true)) return reader.ReadToEnd();
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        internal static void WriteZone(string path, string zone)
        {
            if (zone == null) return;
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("archive.extractedFileIsLink"));
            // Fail closed: an untrusted attachment must never silently become a trusted file.
            var attributes = File.GetAttributes(path);
            DateTime modified = File.GetLastWriteTimeUtc(path);
            try
            {
                if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                using (var output = OpenZone(path, true))
                using (var writer = new StreamWriter(output, new UTF8Encoding(false))) writer.Write(zone);
            }
            finally
            {
                File.SetLastWriteTimeUtc(path, modified);
                if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, attributes);
            }
        }
        internal static void MarkInternet(string path) { WriteZone(path, "[ZoneTransfer]\r\nZoneId=3\r\n"); }
    }
}
