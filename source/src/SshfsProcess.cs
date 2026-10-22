using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CommanderCZ
{
    internal static class SshfsProcess
    {
        [StructLayout(LayoutKind.Sequential)] struct Security { public int Size; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Startup
        { public int Size; public string Reserved, Desktop, Title; public int X, Y, Width, Height, XChars, YChars, Fill, Flags; public short Show, ReservedSize; public IntPtr ReservedData, Input, Output, Error; }
        [StructLayout(LayoutKind.Sequential)] struct ProcessData { public IntPtr Process, Thread; public int Id, ThreadId; }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref Security security, int size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetHandleInformation(IntPtr handle, int mask, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateFile(string path, uint access, uint share, ref Security security, uint creation, uint attributes, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref Startup startup, out ProcessData process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        static void Close(IntPtr handle) { if (handle != IntPtr.Zero && handle != new IntPtr(-1)) CloseHandle(handle); }
        // Valid native handles are necessary for Cygwin. NUL and a log file also
        // stay valid when the GUI exits, so a persistent mount has no dead pipes.
        internal static Process Start(ProcessStartInfo info, string password, string log)
        {
            var security = new Security { Size = Marshal.SizeOf(typeof(Security)), Inherit = true };
            IntPtr read = IntPtr.Zero, write = IntPtr.Zero, output = IntPtr.Zero, error = IntPtr.Zero, environment = IntPtr.Zero;
            Process child = null;
            try
            {
                if (!CreatePipe(out read, out write, ref security, 0) || !SetHandleInformation(write, 1, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
                output = CreateFile("NUL", 0x40000000, 7, ref security, 3, 0x80, IntPtr.Zero);
                error = CreateFile(log, 0x40000000, 7, ref security, 2, 0x80, IntPtr.Zero);
                if (output == new IntPtr(-1) || error == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
                string block = String.Join("\0", info.EnvironmentVariables.Keys.Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(x => x + "=" + info.EnvironmentVariables[x])) + "\0\0";
                environment = Marshal.StringToHGlobalUni(block);
                var startup = new Startup { Size = Marshal.SizeOf(typeof(Startup)), Flags = 0x100, Input = read, Output = output, Error = error };
                ProcessData data;
                if (!CreateProcess(info.FileName, new StringBuilder(SshTerminal.QuoteWindowsArgument(info.FileName) + " " + info.Arguments), IntPtr.Zero, IntPtr.Zero, true, 0x08000400, environment, info.WorkingDirectory, ref startup, out data)) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { child = Process.GetProcessById(data.Id); } finally { Close(data.Thread); Close(data.Process); }
                var writer = new SafeFileHandle(write, true); write = IntPtr.Zero;
                using (writer) using (var stream = new FileStream(writer, FileAccess.Write))
                { if (password != null) { byte[] bytes = Encoding.UTF8.GetBytes(password + "\n"); stream.Write(bytes, 0, bytes.Length); stream.Flush(); Array.Clear(bytes, 0, bytes.Length); } }
                var result = child; child = null; return result;
            }
            finally
            {
                if (child != null) { if (!child.HasExited) child.Kill(); child.Dispose(); }
                Close(read); Close(write); Close(output); Close(error); if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            }
        }
    }
}

