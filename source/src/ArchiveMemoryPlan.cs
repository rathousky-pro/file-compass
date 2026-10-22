using System;
using System.IO;
using System.Runtime.InteropServices;
namespace CommanderCZ
{
    internal sealed class ArchiveMemoryException : IOException { internal ArchiveMemoryException() : base(L.Text("archive.memoryFailed")) { } }
    internal sealed class ArchiveMemoryPlan
    {
        internal int DictionaryMiB, Threads;
        [StructLayout(LayoutKind.Sequential)] sealed class MemoryStatus { public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)), Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtended; }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);
        internal static long AvailableBytes() { var status = new MemoryStatus(); if (!GlobalMemoryStatusEx(status)) return 256L * 1048576; return (long)Math.Min(status.AvailablePhysical, status.AvailablePageFile); }
        internal static ArchiveMemoryPlan Choose(int limitMiB, long availableBytes, int items, int level, bool fallback)
        {
            // Conservative estimate, including managed/native overhead and file metadata.
            // The OS job remains the hard limit; this is a planning budget, not a guarantee.
            long budget = Math.Min(limitMiB - Math.Max(64, limitMiB / 5), availableBytes / 2097152);
            long reserve = 96 + ((long)items * 2048 + 1048575) / 1048576;
            if (budget < reserve + 16) throw new ArchiveMemoryException();
            int desired = fallback ? 2 : level <= 3 ? 4 : level <= 7 ? 16 : 32;
            int dictionary = desired; while (dictionary > 1 && reserve + 16 * dictionary > budget) dictionary /= 2;
            if (reserve + 16 * dictionary > budget) throw new ArchiveMemoryException();
            return new ArchiveMemoryPlan { DictionaryMiB = dictionary, Threads = fallback || dictionary < 4 ? 1 : Math.Min(2, Environment.ProcessorCount) };
        }
        internal static bool IsFailure(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (current is OutOfMemoryException || current is ArchiveMemoryException || current.HResult == unchecked((int)0x8007000E) || current.Message.IndexOf("E_OUTOFMEMORY", StringComparison.OrdinalIgnoreCase) >= 0 || current.Message.IndexOf("not enough memory", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}

