using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommanderCZ
{
    internal static class EverythingSearch
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void Reset();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] delegate void SetText(string value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void SetNumber(uint value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate bool Query([MarshalAs(UnmanagedType.Bool)] bool wait);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Number();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] delegate uint Result(uint index, StringBuilder path, uint count);
        static T Function<T>(IntPtr module, string name) where T : class { IntPtr ptr = GetProcAddress(module, name); if (ptr == IntPtr.Zero) throw new IOException(L.Text("everything.invalidSdk")); return (T)(object)Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)); }
        internal static string Validate(string query) { if (String.IsNullOrWhiteSpace(query) || query.Length > 4096 || query.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException(L.Text("everything.invalidQuery")); return query; }
        internal static async Task<string[]> Search(string query, CancellationToken token)
        {
            Validate(query); token.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--everything-worker") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (var worker = Process.Start(info))
            {
                var read = Task.Run(delegate
                {
                    // Query is passed over stdin, never interpreted as executable arguments.
                    byte[] request = new UTF8Encoding(false).GetBytes(query + "\n");
                    worker.StandardInput.BaseStream.Write(request, 0, request.Length); worker.StandardInput.BaseStream.Flush(); worker.StandardInput.Close();
                    var paths = new List<string>(); string header = worker.StandardOutput.ReadLine(); int count;
                    if (!Int32.TryParse(header, out count) || count < 0 || count > 20000) throw new IOException(L.Text("everything.notRunning"));
                    int total = 0;
                    for (int i = 0; i < count; i++) { string path = worker.StandardOutput.ReadLine(); if (path == null || path.Length > 32767 || (total += path.Length) > 4 * 1048576 || !Path.IsPathRooted(path)) throw new IOException(L.Text("everything.invalidResult")); paths.Add(path); }
                    worker.WaitForExit(); if (worker.ExitCode != 0) throw new IOException(L.Text("everything.notRunning")); return paths.ToArray();
                });
                try
                {
                    if (await Task.WhenAny(read, Task.Delay(15000, token)) != read) { token.ThrowIfCancellationRequested(); throw new IOException(L.Text("everything.timeout")); }
                    return await read;
                }
                finally
                {
                    // Only this owned helper is terminated; the user's Everything stays running.
                    if (!worker.HasExited) { try { worker.Kill(); } catch (InvalidOperationException) { } }
                    if (!read.IsCompleted) { var observe = read.ContinueWith(delegate(Task<string[]> completed) { var observed = completed.Exception; }, TaskScheduler.Default); }
                }
            }
        }
        internal static int Worker()
        {
            Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                var query = new StringBuilder(); int value; while ((value = Console.In.Read()) >= 0 && value != '\n') { if (query.Length >= 4096) throw new ArgumentException(); if (value != '\r') query.Append((char)value); }
                Validate(query.ToString());
                using (PluginIntegrity.OpenVerified(@"everything\Everything64.dll"))
                {
                    IntPtr module = LoadLibraryEx(Path.Combine(PluginIntegrity.PluginRoot, "everything", "Everything64.dll"), IntPtr.Zero, 8);
                    if (module == IntPtr.Zero) throw new IOException(L.Text("everything.invalidSdk"));
                    try
                    {
                        Function<Reset>(module, "Everything_Reset")(); Function<SetText>(module, "Everything_SetSearchW")(query.ToString());
                        Function<SetNumber>(module, "Everything_SetMax")(20000); Function<SetNumber>(module, "Everything_SetRequestFlags")(4);
                        if (!Function<Query>(module, "Everything_QueryW")(true)) throw new IOException(L.Text("everything.notRunning"));
                        uint count = Math.Min(20000, Function<Number>(module, "Everything_GetNumResults")()); var getPath = Function<Result>(module, "Everything_GetResultFullPathNameW");
                        var paths = new List<string>(); var buffer = new StringBuilder(32768); int chars = 0;
                        for (uint i = 0; i < count; i++) { buffer.Clear(); uint length = getPath(i, buffer, 32768); if (length == 0 || length >= 32768) continue; string path = buffer.ToString(); if ((chars += path.Length) > 4 * 1048576) break; if (Path.IsPathRooted(path) && path.IndexOfAny(new[] { '\r', '\n' }) < 0) paths.Add(path); }
                        Console.Out.WriteLine(paths.Count); foreach (string path in paths) Console.Out.WriteLine(path);
                    }
                    finally { Function<Reset>(module, "Everything_CleanUp")(); FreeLibrary(module); }
                }
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
    }
}

