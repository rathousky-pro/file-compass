using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace FileCompass
{
    internal static class PerformanceLog
    {
        static readonly object Gate = new object();
        static string path;
        static Timer sampler;
        internal static long Start { get { return path == null ? 0 : Stopwatch.GetTimestamp(); } }

        internal static void Begin(string[] args)
        {
            long entered = Stopwatch.GetTimestamp();
            try
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                bool diagnosticTest = Environment.GetEnvironmentVariable("FILE_COMPASS_PERFORMANCE_TEST") == "1";
                if (!diagnosticTest && (Array.Exists(args, x => x.StartsWith("--", StringComparison.Ordinal)) ||
                    !File.Exists(Path.Combine(directory, "performance-measurement.txt")))) return;
                directory = Path.Combine(directory, "measurements");
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, "performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Process.GetCurrentProcess().Id + ".tsv");
                File.WriteAllText(path, "utc\tevent\tid\tduration_ms\tdetails\n");
                Entry = entered;
                Write("startup.entry");
                sampler = new Timer(_ => Sample(), null, 5000, 5000);
            }
            catch { path = null; }
        }

        internal static long Entry { get; private set; }
        internal static string Milliseconds(long ticks) { return (ticks * 1000.0 / Stopwatch.Frequency).ToString("F2", CultureInfo.InvariantCulture); }

        internal static void Write(string name, long started = 0, string id = "", string details = "")
        {
            if (path == null) return;
            long now = Stopwatch.GetTimestamp();
            try
            {
                string line = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\t" + name + "\t" + id + "\t" +
                    (started == 0 ? "" : Milliseconds(now - started)) + "\t" + details + "\n";
                lock (Gate) File.AppendAllText(path, line);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void Sample(string name = "memory.sample")
        {
            if (path == null) return;
            try
            {
                using (var process = Process.GetCurrentProcess())
                    Write(name, details: "private_bytes=" + process.PrivateMemorySize64 + " working_set_bytes=" + process.WorkingSet64 +
                        " managed_estimate_bytes=" + GC.GetTotalMemory(false) + " cpu_ms=" + (long)process.TotalProcessorTime.TotalMilliseconds +
                        " handles=" + process.HandleCount + " gc0=" + GC.CollectionCount(0) + " gc1=" + GC.CollectionCount(1) + " gc2=" + GC.CollectionCount(2));
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        internal static void End()
        {
            if (sampler != null) sampler.Dispose();
            Sample("memory.exit");
            Write("session.end", Entry);
        }
    }
}
