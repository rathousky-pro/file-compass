using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CommanderCZ
{
    internal sealed class ArchiveSourceVersion
    {
        internal string Path, Member, Hash; internal bool Directory; internal SyncStamp Stamp;
    }
    internal static class ArchiveExtended
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr security, FileMode mode, uint flags, IntPtr template);
        [StructLayout(LayoutKind.Sequential)] struct Disposition { internal byte Delete; }
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref Disposition information, uint size);
        static void DeleteVerifiedSource(ArchiveSourceVersion file, CancellationToken token)
        {
            // Deny writes and renames while hashing, then delete this very file handle.
            using (var handle = CreateFile(file.Path,0x80000000u | 0x00010000u,FileShare.Read,IntPtr.Zero,FileMode.Open,0x00200000u,IntPtr.Zero)) {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                using (var input = new FileStream(handle,FileAccess.Read)) {
                    if (FileParts.Hash(input,token) != file.Hash || !file.Stamp.Matches(file.Path)) throw new IOException(L.Text("parts.sourceChanged"));
                    token.ThrowIfCancellationRequested(); var disposition = new Disposition { Delete = 1 };
                    if (!SetFileInformationByHandle(handle,4,ref disposition,1)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        internal static ArchivePackOptions PlainOptions(ArchivePackOptions options)
        {
            return new ArchivePackOptions { Paths = options.Paths, Recursive = options.Recursive, ExcludeBase = options.ExcludeBase,
                Password = options.Password, Level = options.Level, NewestFileDate = options.NewestFileDate };
        }
        static List<ArchiveSourceVersion> Snapshot(string[] sources, ArchivePackOptions options, CancellationToken token)
        {
            var result = new List<ArchiveSourceVersion>();
            foreach (string path in sources) Capture(path, options.ExcludeBase && Directory.Exists(path) ? "" : Path.GetFileName(path.TrimEnd('\\')), options, token, result);
            return result;
        }
        static void Capture(string path, string member, ArchivePackOptions options, CancellationToken token, List<ArchiveSourceVersion> result)
        {
            token.ThrowIfCancellationRequested(); var stamp = SyncStamp.Read(path); if (stamp == null) throw new IOException(L.Text("parts.sourceChanged"));
            result.Add(new ArchiveSourceVersion { Path = path, Member = stamp.Directory ? member : options.Paths ? member : Path.GetFileName(path), Directory = stamp.Directory, Stamp = stamp, Hash = stamp.Directory ? "" : FileParts.Hash(path,token) });
            if (stamp.Directory) foreach (string child in Directory.EnumerateFileSystemEntries(path)) Capture(child, options.Paths && member.Length > 0 ? member + "/" + Path.GetFileName(child) : Path.GetFileName(child), options, token, result);
        }
        static void VerifyMembers(ArchiveDocument archive, List<ArchiveSourceVersion> versions, bool preservePaths, CancellationToken token, Action<string> progress)
        {
            using (var extracted = new ArchiveTemp()) {
                ArchiveEngine.Extract(archive,archive.Items.Select(x => x.Path),extracted.Path,token,progress);
                if (preservePaths) foreach (var folder in versions.Where(x => x.Directory && x.Member.Length > 0)) if (!Directory.Exists(ArchiveEngine.DiskPath(extracted.Path,folder.Member))) throw new IOException(L.Text("archive.moveVerificationFailed"));
                foreach (var file in versions.Where(x => !x.Directory)) {
                    token.ThrowIfCancellationRequested();
                    string member = ArchiveEngine.SafeMember(file.Member), disk = ArchiveEngine.DiskPath(extracted.Path,member);
                    if (!File.Exists(disk) && versions.Count(x => !x.Directory) == 1 && archive.Items.Count(x => !x.Directory) == 1 &&
                        new[] { "gzip","gz","bzip2","bz2","xz" }.Contains(archive.Type.ToLowerInvariant())) disk = ArchiveEngine.DiskPath(extracted.Path,archive.Items.Single(x => !x.Directory).Path);
                    if (!File.Exists(disk) || FileParts.Hash(disk,token) != file.Hash) throw new IOException(L.Text("archive.moveVerificationFailed"));
                }
            }
        }
        static bool SameSources(List<ArchiveSourceVersion> before, List<ArchiveSourceVersion> after)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            if (before.Count != after.Count) return false;
            var current = after.ToDictionary(x => x.Path,comparer);
            foreach (var original in before) {
                ArchiveSourceVersion found;
                if (!current.TryGetValue(original.Path,out found) || original.Directory != found.Directory || original.Hash != found.Hash ||
                    original.Stamp.Attributes != found.Stamp.Attributes || (!original.Directory && (original.Stamp.Length != found.Stamp.Length || original.Stamp.Modified != found.Stamp.Modified))) return false;
            }
            return true;
        }
        internal static void Create(string target, IEnumerable<string> input, ArchiveSettings settings, CancellationToken token, Action<string> progress, ArchivePackOptions options)
        {
            target = Path.GetFullPath(target); string[] sources = input.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (sources.Any(x => sources.Any(y => x != y && Directory.Exists(y) && WindowsShell.IsWithin(x,y)))) throw new IOException(L.Text("archive.overlappingSources"));
            if (options.Move && !options.Recursive) throw new IOException(L.Text("archive.moveNeedsRecursive"));
            if (options.VolumeBytes > 0 && !new[] { ".zip", ".7z" }.Contains(Path.GetExtension(target).ToLowerInvariant())) throw new IOException(L.Text("archive.volumeFormats"));
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException(L.Text("parts.outputExists"));
            foreach (string source in sources) if (WindowsShell.SamePath(source,target) || (Directory.Exists(source) && WindowsShell.IsWithin(target,source)) || WindowsShell.SamePath(source,Path.GetPathRoot(source))) throw new IOException(L.Text("archive.outputInsideInputRejected"));
            var snapshot = options.Move ? Snapshot(sources,options,token) : null;
            var outputs = new List<string>(); bool verified = false;
            using (var staging = new ArchiveTemp()) {
                string complete = Path.Combine(staging.Path,Path.GetFileName(target));
                try {
                    ArchiveEngine.Create(complete,sources,settings,token,progress,PlainOptions(options));
                    var document = ArchiveEngine.Open(complete,options.Password,token);
                    ArchiveEngine.Test(document,token,progress);
                    if (options.Move) VerifyMembers(document,snapshot,options.Paths,token,progress);
                    token.ThrowIfCancellationRequested();
                    if (options.VolumeBytes > 0) outputs.AddRange(FileParts.Split(complete,target,options.VolumeBytes,token,progress));
                    else { File.Move(complete,target); outputs.Add(target); }
                    var locks = new List<FileStream>();
                    try {
                        foreach (string file in outputs) locks.Add(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read));
                        var published = ArchiveEngine.Open(options.VolumeBytes > 0 ? target + ".001" : target,options.Password,token);
                        ArchiveEngine.Test(published,token,progress);
                        verified = true;
                        if (options.Move) {
                            if (!SameSources(snapshot,Snapshot(sources,options,token))) throw new IOException(L.Text("parts.sourceChanged"));
                            foreach (var file in snapshot.Where(x => !x.Directory)) {
                                token.ThrowIfCancellationRequested();
                                DeleteVerifiedSource(file,token);
                            }
                            // Empty-only deletes preserve anything created concurrently after the snapshot.
                            foreach (var folder in snapshot.Where(x => x.Directory).OrderByDescending(x => x.Path.Length)) { token.ThrowIfCancellationRequested(); Directory.Delete(folder.Path,false); }
                        }
                    } finally { foreach (var stream in locks) stream.Dispose(); }
                } catch {
                    if (!verified) foreach (string file in outputs) try { File.Delete(file); } catch (IOException) { }
                    throw;
                }
            }
        }
        internal static string VolumeStamp(string path)
        {
            if (!path.EndsWith(".001",StringComparison.OrdinalIgnoreCase)) return "";
            return String.Join("|",FileParts.Discover(path).Select(x => { var f = new FileInfo(x); return f.Name + ":" + f.Length + ":" + f.LastWriteTimeUtc.Ticks; }));
        }
    }
}
