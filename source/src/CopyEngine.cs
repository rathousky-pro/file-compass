using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace CommanderCZ
{
    public enum CopyRule { Ask, OverwriteAll, Older, SkipAll, Different, Larger, Smaller, RenameAll }
    public enum CopyChoice { Overwrite, OverwriteAll, Older, Skip, SkipAll, Rename, RenameAll, Append, Different, Larger, Smaller, Cancel }
    public class CopyOptions
    {
        public string Target, Filter = "*.*";
        public bool TargetIsFile, Verify, Permissions, ReadOnly, SkipHidden, Queue, Move;
        public CopyOptions Clone() { return (CopyOptions)MemberwiseClone(); }
        public bool Times = true, Attributes = true, EmptyFolders = true;
        public CopyRule Rule;
    }
    public class CopyConflict
    {
        public string Source, Target;
        public bool SourceDirectory, TargetDirectory, IsReadOnly;
        public long SourceSize, TargetSize;
        public DateTime SourceTime, TargetTime;
    }
    public class CopyDecision
    {
        public CopyChoice Choice;
        public string NewName;
        public bool AllowReadOnly;
        public CopyDecision(CopyChoice choice, string name = null) { Choice = choice; NewName = name; }
    }
    public class CopyUpdate
    {
        public string Source, Target, Phase;
        public long FileDone, FileTotal, TotalDone, TotalBytes;
        public long BytesPerSecond;
        public double? RemainingSeconds;
        public int Completed, TotalFiles;
    }
    public class CopyResult
    {
        public int Copied, Skipped;
        public bool Cancelled;
        public readonly List<string> Errors = new List<string>();
        public readonly List<OperationItem> Items = new List<OperationItem>();
    }
    public class CopyControl : IDisposable
    {
        readonly ManualResetEventSlim gate = new ManualResetEventSlim(true);
        readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        public bool Paused { get; private set; }
        internal bool Cancelled { get { return cancellation.IsCancellationRequested; } }
        internal CancellationToken Token { get { return cancellation.Token; } }
        public void Pause(bool pause) { Paused = pause; if (pause) gate.Reset(); else gate.Set(); }
        public void Cancel() { cancellation.Cancel(); gate.Set(); }
        public void Check() { cancellation.Token.ThrowIfCancellationRequested(); gate.Wait(cancellation.Token); }
        public void Dispose() { gate.Dispose(); cancellation.Dispose(); }
    }

    public sealed class CopyEngine
    {
        internal static bool NetworkPath(string path)
        {
            if (path.StartsWith("\\\\", StringComparison.Ordinal)) return true;
            string root = Path.GetPathRoot(path);
            if (String.IsNullOrEmpty(root)) return false;
            return new DriveInfo(root).DriveType == DriveType.Network || SshfsMount.MappedDrives().ContainsKey(root);
        }
        bool FullVerification(Node node, string target)
        { return options.Verify || (options.Move && !NetworkPath(node.Source) && !NetworkPath(target)); }
        sealed class Node
        {
            public string Source; public bool Directory; public long Bytes; public DateTime SourceTime;
            public readonly List<Node> Children = new List<Node>();
        }
        sealed class Stamp
        {
            public bool Exists, Directory; public long Length; public DateTime Written, Created;
            public static Stamp Read(string path)
            {
                if (System.IO.Directory.Exists(path)) return new Stamp { Exists = true, Directory = true, Written = System.IO.Directory.GetLastWriteTimeUtc(path), Created = System.IO.Directory.GetCreationTimeUtc(path) };
                if (!File.Exists(path)) return new Stamp();
                var info = new FileInfo(path); return new Stamp { Exists = true, Length = info.Length, Written = info.LastWriteTimeUtc, Created = info.CreationTimeUtc };
            }
            public bool Matches(string path)
            {
                var other = Read(path);
                return Exists == other.Exists && Directory == other.Directory && Length == other.Length && Written == other.Written && Created == other.Created;
            }
        }
        readonly CopyOptions options;
        readonly CopyControl control;
        readonly Func<CopyConflict, CopyDecision> resolve;
        readonly Action<CopyUpdate> report;
        readonly Func<CopyFailure, CopyErrorChoice> resolveError;
        bool skipErrors;
        readonly CopyResult result = new CopyResult();
        CopyRule rule;
        long totalBytes, doneBytes;
        long transferredBytes, transferTicks;
        int totalFiles, finishedFiles;
        DateTime lastReport = DateTime.MinValue;
        internal Action<List<DurableFile>> PlanReady;
        internal Action<string> PersistDone;
        internal Dictionary<string,string> RecoveryTargets;

        public CopyEngine(CopyOptions options, CopyControl control, Func<CopyConflict, CopyDecision> resolve, Action<CopyUpdate> report, Func<CopyFailure, CopyErrorChoice> resolveError = null)
        { this.options = options; this.control = control; this.resolve = resolve; this.report = report; this.resolveError = resolveError; rule = options.Rule; }

        public CopyResult Run(IEnumerable<string> sourcePaths)
        {
            string currentSource = "", currentTarget = "";
            try
            {
                string target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.Target));
                string[] sources = sourcePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (sources.Length == 0) throw new IOException(L.Text("copy.noSourceSelected"));
                if (options.TargetIsFile && sources.Length != 1) throw new IOException(L.Text("copy.singleSourceRenameRequired"));
                string destinationFolder = options.TargetIsFile ? Path.GetDirectoryName(target) : target;
                RejectLinks(destinationFolder);
                var nodes = new List<Node>();
                foreach (string source in sources)
                {
                    control.Check();
                    string destination = RecoveryTargets != null ? RecoveryTargets[source] : options.TargetIsFile ? target : Path.Combine(target, Path.GetFileName(source.TrimEnd('\\')));
                    currentSource = source; currentTarget = destination; ValidateRelation(source, destination); RejectLinks(source);
                    Publish(source, destination, L.Code("copy.loadingFiles"), 0, 0, true);
                    var node = Scan(source); if (node != null) nodes.Add(node);
                }
                Publish(currentSource, currentTarget, L.Code("copy.savingPlan"), 0, 0, true);
                if (PlanReady != null) { var plan = new List<DurableFile>(); foreach (var node in nodes) AddPlan(node,RecoveryTargets != null ? RecoveryTargets[node.Source] : options.TargetIsFile ? target : Path.Combine(target,Path.GetFileName(node.Source.TrimEnd('\\'))),plan); PlanReady(plan); }
                control.Check(); Directory.CreateDirectory(destinationFolder);
                foreach (Node node in nodes)
                {
                    string destination = RecoveryTargets != null ? RecoveryTargets[node.Source] : options.TargetIsFile ? target : Path.Combine(target, Path.GetFileName(node.Source.TrimEnd('\\')));
                    Process(node, destination);
                }
            }
            catch (OperationCanceledException) { result.Cancelled = true; }
            catch (Exception ex) { result.Errors.Add(ex.Message); result.Items.Add(new OperationItem { Source = currentSource, Target = currentTarget, Directory = Directory.Exists(currentSource), State = L.Code("state.error"), Error = ex.Message }); }
            return result;
        }

        void AddPlan(Node node,string target,List<DurableFile> result)
        {
            control.Check();
            result.Add(new DurableFile { Source = node.Source,Target = target,Directory = node.Directory,Bytes = node.Bytes,Modified = node.SourceTime });
            foreach (var child in node.Children) AddPlan(child,Path.Combine(target,Path.GetFileName(child.Source)),result);
        }
        Node Scan(string source)
        {
            control.Check(); RejectLinks(source);
            return Scan(new FileInfo(source), null);
        }
        Node Scan(FileSystemInfo info, string destination)
        {
            control.Check();
            string source = info.FullName;
            FileAttributes attributes = info.Attributes;
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("copy.reparsePointRejectedPrefix") + source);
            if (options.SkipHidden && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return null;
            bool dir = (attributes & FileAttributes.Directory) != 0;
            if (!dir && !MatchesFilter(Path.GetFileName(source), options.Filter)) return null;
            var node = new Node { Source = source, Directory = dir, SourceTime = info.LastWriteTimeUtc };
            if (destination == null) destination = RecoveryTargets != null ? RecoveryTargets[source] : options.TargetIsFile ? Path.GetFullPath(options.Target) : Path.Combine(Path.GetFullPath(options.Target), info.Name);
            if (dir)
            {
                if (RecoveryTargets == null) foreach (var child in new DirectoryInfo(source).EnumerateFileSystemInfos()) { var next = Scan(child, Path.Combine(destination, child.Name)); if (next != null) node.Children.Add(next); }
                if (RecoveryTargets == null && !options.EmptyFolders && node.Children.Count == 0) return null;
            }
            else { node.Bytes = ((FileInfo)info).Length; totalBytes = checked(totalBytes + node.Bytes); totalFiles++; }
            Publish(source, destination, L.Code("copy.loadingFiles"), 0, 0, false);
            return node;
        }

        void Process(Node node, string target)
        {
            bool refreshSource = false;
            while (true)
            {
            control.Check();
            try
            {
                if (refreshSource && !node.Directory)
                {
                    long bytes = new FileInfo(node.Source).Length; totalBytes += bytes - node.Bytes; node.Bytes = bytes; refreshSource = false;
                }
                ValidateRelation(node.Source, target); RejectLinks(target);
                var stamp = Stamp.Read(target);
                bool append = false;
                if (stamp.Exists && !(node.Directory && stamp.Directory))
                {
                    var decision = Choose(node, target, stamp);
                    if (decision.Choice == CopyChoice.Cancel) throw new OperationCanceledException();
                    if (decision.Choice == CopyChoice.Skip) { Skip(node); return; }
                    if (decision.Choice == CopyChoice.Rename)
                    {
                        FileOperations.ValidateName(decision.NewName);
                        string renamed = Path.Combine(Path.GetDirectoryName(target), decision.NewName);
                        if (String.Equals(target, renamed, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("copy.newNameMustDiffer"));
                        Process(node, renamed); return;
                    }
                    if (node.Directory != stamp.Directory) throw new IOException(L.Text("copy.fileFolderOverwriteRejected"));
                    append = decision.Choice == CopyChoice.Append;
                    if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0 && !options.ReadOnly)
                        throw new IOException(L.Text("copy.readOnlyOverwriteNotAllowed"));
                }
                if (options.Move && RecoveryTargets == null && !stamp.Exists && options.Times && options.Attributes && !options.Permissions &&
                    String.Equals(Path.GetPathRoot(node.Source), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase) &&
                    (!node.Directory || (!options.SkipHidden && options.EmptyFolders && (String.IsNullOrWhiteSpace(options.Filter) || options.Filter.Trim() == "*.*"))))
                {
                    RejectLinks(node.Source);
                    try
                    {
                        Publish(node.Source, target, L.Code("state.moving"), 0, node.Bytes, true);
                        if (node.Directory) Directory.Move(node.Source, target); else File.Move(node.Source, target);
                        FileAssociations.Moved(node.Source, target); RecordRenamed(node, target); Publish(node.Source, target, L.Code("state.completed"), node.Bytes, node.Bytes, true); return;
                    }
                    catch (IOException ex) { if ((ex.HResult & 65535) != 17) throw; } // Cross-device mount: fall back to verified copy.
                }
                if (node.Directory)
                {
                    Directory.CreateDirectory(target);
                    foreach (Node child in node.Children) Process(child, Path.Combine(target, Path.GetFileName(child.Source)));
                    // Do not change metadata of existing merged folders.
                    if (!stamp.Exists)
                    {
                        if (options.Times) Directory.SetLastWriteTimeUtc(target, Directory.GetLastWriteTimeUtc(node.Source));
                        if (options.Permissions) Directory.SetAccessControl(target, Directory.GetAccessControl(node.Source, AccessControlSections.Access));
                    }
                    if (options.Move && !Directory.EnumerateFileSystemEntries(node.Source).Any()) { Directory.Delete(node.Source); FileAssociations.Moved(node.Source, target); }
                    result.Items.Add(new OperationItem { Source = node.Source, Target = target, Directory = true, State = options.Move && Directory.Exists(node.Source) ? L.Text("state.partiallyMoved") : L.Code("state.completed") });
                }
                else
                {
                    if (options.Move && append) throw new IOException(L.Text("copy.appendDuringMoveRejected"));
                    node.SourceTime = File.GetLastWriteTimeUtc(node.Source);
                    CopyFile(node, target, stamp, append);
                    if (options.Move)
                    {
                        RemoveVerifiedSource(node, target); FileAssociations.Moved(node.Source, target);
                    }
                    result.Items.Add(new OperationItem { Source = node.Source, Target = target, State = L.Code("state.completed") });
                    result.Copied++; doneBytes += node.Bytes; finishedFiles++;
                    if (PersistDone != null) PersistDone(node.Source);
                    Publish(node.Source, target, L.Code("state.completed"), node.Bytes, node.Bytes, true);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (!node.Directory && resolveError != null && (ex is IOException || ex is UnauthorizedAccessException))
                {
                    var choice = skipErrors ? CopyErrorChoice.Skip : resolveError(new CopyFailure { Source = node.Source, Target = target, Error = ex });
                    control.Check();
                    if (choice == CopyErrorChoice.Retry) { refreshSource = true; continue; }
                    if (choice == CopyErrorChoice.Cancel) throw new OperationCanceledException();
                    if (choice == CopyErrorChoice.SkipAll) skipErrors = true;
                    result.Items.Add(new OperationItem { Source = node.Source, Target = target, State = L.Code("state.error"), Error = ex.Message, Directory = node.Directory });
                    Skip(node, false); return;
                }
                result.Items.Add(new OperationItem { Source = node.Source, Target = target, State = L.Code("state.error"), Error = ex.Message, Directory = node.Directory }); result.Errors.Add(node.Source + L.Text("common.routeArrow") + target + "\n" + ex.Message); if (!node.Directory) { doneBytes += node.Bytes; finishedFiles++; }
            }
            return;
            }
        }

        CopyDecision Choose(Node node, string target, Stamp stamp)
        {
            bool compatible = !node.Directory && !stamp.Directory;
            var choice = CopyChoice.Skip;
            if (rule == CopyRule.RenameAll) return new CopyDecision(CopyChoice.Rename, UniqueName(target));
            if (rule == CopyRule.SkipAll) return new CopyDecision(CopyChoice.Skip);
            if (compatible && rule != CopyRule.Ask) return new CopyDecision(ShouldOverwrite(node, stamp, rule) ? CopyChoice.Overwrite : CopyChoice.Skip);
            FileAttributes attributes = File.GetAttributes(target);
            var response = resolve(new CopyConflict { Source = node.Source, Target = target, SourceDirectory = node.Directory, TargetDirectory = stamp.Directory,
                SourceSize = node.Bytes, TargetSize = stamp.Length, SourceTime = File.GetLastWriteTime(node.Source), TargetTime = stamp.Written.ToLocalTime(), IsReadOnly = (attributes & FileAttributes.ReadOnly) != 0 });
            choice = response.Choice;
            if (response.AllowReadOnly) options.ReadOnly = true;
            switch (choice)
            {
                case CopyChoice.OverwriteAll: rule = CopyRule.OverwriteAll; break;
                case CopyChoice.Older: rule = CopyRule.Older; break;
                case CopyChoice.SkipAll: rule = CopyRule.SkipAll; break;
                case CopyChoice.Different: rule = CopyRule.Different; break;
                case CopyChoice.Larger: rule = CopyRule.Larger; break;
                case CopyChoice.Smaller: rule = CopyRule.Smaller; break;
                case CopyChoice.RenameAll: rule = CopyRule.RenameAll; break;
                default: return response;
            }
            if (rule == CopyRule.RenameAll) return new CopyDecision(CopyChoice.Rename, UniqueName(target));
            return new CopyDecision(compatible && ShouldOverwrite(node, stamp, rule) ? CopyChoice.Overwrite : CopyChoice.Skip);
        }
        bool ShouldOverwrite(Node node, Stamp stamp, CopyRule policy)
        {
            DateTime sourceTime = File.GetLastWriteTimeUtc(node.Source);
            return policy == CopyRule.OverwriteAll || (policy == CopyRule.Older && sourceTime > stamp.Written) ||
                (policy == CopyRule.Different && (node.Bytes != stamp.Length || sourceTime != stamp.Written)) ||
                (policy == CopyRule.Larger && node.Bytes > stamp.Length) || (policy == CopyRule.Smaller && node.Bytes < stamp.Length);
        }
        void Skip(Node node, bool record = true)
        {
            if (node.Directory) { foreach (Node child in node.Children) Skip(child, record); }
            else { if (record) result.Items.Add(new OperationItem { Source = node.Source, State = L.Code("state.skipped") }); result.Skipped++; finishedFiles++; doneBytes += node.Bytes; if (record && PersistDone != null) PersistDone(node.Source); }
        }

        void RecordRenamed(Node node, string target)
        {
            result.Items.Add(new OperationItem { Source = node.Source, Target = target, Directory = node.Directory, State = L.Code("state.completed") });
            if (node.Directory) foreach (var child in node.Children) RecordRenamed(child, Path.Combine(target, Path.GetFileName(child.Source)));
            else { result.Copied++; finishedFiles++; doneBytes += node.Bytes; if (PersistDone != null) PersistDone(node.Source); }
        }
        void CopyFile(Node node, string target, Stamp expected, bool append)
        {
            lastReport = DateTime.MinValue;
            string temp = Path.Combine(Path.GetDirectoryName(target), ".commandercz-" + Guid.NewGuid().ToString("N") + ".tmp");
            string backup = null;
            bool committed = false, ownedTemp = false, changedReadOnly = false;
            FileAttributes oldAttributes = expected.Exists ? File.GetAttributes(target) : FileAttributes.Normal;
            try
            {
                DateTime sourceTime;
                FileAttributes sourceAttributes;
                FileSecurity security;
                byte[] expectedHash;
                using (var hash = SHA256.Create())
                using (var input = new FileStream(node.Source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
                {
                    ownedTemp = true;
                    sourceTime = File.GetLastWriteTimeUtc(node.Source); sourceAttributes = File.GetAttributes(node.Source);
                    security = options.Permissions ? File.GetAccessControl(node.Source, AccessControlSections.Access) : null;
                    if (input.Length != node.Bytes) throw new IOException(L.Text("copy.sourceChangedSinceListing"));
                    if (append)
                    {
                        using (var prefix = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                            CopyStream(prefix, output, hash, node, target, true);
                    }
                    CopyStream(input, output, hash, node, target, false);
                    hash.TransformFinalBlock(new byte[0], 0, 0); expectedHash = hash.Hash;
                    output.Flush(true);
                }
                if (FullVerification(node, target))
                {
                    Publish(node.Source, target, L.Text("copy.verifyingSha256Status"), node.Bytes, node.Bytes, true);
                    using (var check = File.OpenRead(temp))
                    using (var hash = SHA256.Create())
                    {
                        byte[] buffer = new byte[1024 * 1024]; int read;
                        while ((read = check.Read(buffer, 0, buffer.Length)) > 0) { control.Check(); hash.TransformBlock(buffer, 0, read, null, 0); }
                        hash.TransformFinalBlock(new byte[0], 0, 0);
                        if (!expectedHash.SequenceEqual(hash.Hash)) throw new IOException(L.Text("copy.verificationFailedTargetPreserved"));
                    }
                }
                control.Check(); RejectLinks(target);
                if (options.Times) File.SetLastWriteTimeUtc(temp, sourceTime);
                if (security != null) File.SetAccessControl(temp, security);
                if (!expected.Matches(target)) throw new IOException(L.Text("copy.targetChangedOverwriteStopped"));
                if (expected.Exists)
                {
                    if ((oldAttributes & FileAttributes.ReadOnly) != 0)
                    { File.SetAttributes(target, oldAttributes & ~FileAttributes.ReadOnly); changedReadOnly = true; }
                    backup = Path.Combine(Path.GetDirectoryName(target), ".commandercz-" + Guid.NewGuid().ToString("N") + ".bak");
                    File.Replace(temp, target, backup, false);
                }
                else File.Move(temp, target); // Fails if another process created the target.
                committed = true;
                // No cancellation between commit and metadata/rollback.
                if (security != null) File.SetAccessControl(target, security);
                if (options.Attributes) File.SetAttributes(target, sourceAttributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive));
                else if (changedReadOnly) File.SetAttributes(target, oldAttributes);
                if (backup != null) { File.SetAttributes(backup, FileAttributes.Normal); File.Delete(backup); backup = null; }
            }
            catch
            {
                if (committed)
                {
                    if (backup != null && File.Exists(backup))
                    {
                        try { File.SetAttributes(target, FileAttributes.Normal); File.Replace(backup, target, null); File.SetAttributes(target, oldAttributes); backup = null; }
                        catch (Exception restoreError) { throw new IOException(L.Text("copy.restoreFailedBackupPrefix") + backup + "\n" + restoreError.Message); }
                    }
                    else if (!expected.Exists) { try { File.SetAttributes(target, FileAttributes.Normal); File.Delete(target); } catch { } }
                }
                else if (changedReadOnly && File.Exists(target)) File.SetAttributes(target, oldAttributes);
                throw;
            }
            finally
            {
                if (ownedTemp && File.Exists(temp)) { try { File.SetAttributes(temp, FileAttributes.Normal); File.Delete(temp); } catch { } }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, int kind, ref int information, uint size);
        byte[] CheckedHash(Stream stream)
        {
            using (var hash = SHA256.Create()) { byte[] buffer = new byte[1024 * 1024]; int count; while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) { control.Check(); hash.TransformBlock(buffer, 0, count, null, 0); } hash.TransformFinalBlock(new byte[0], 0, 0); return hash.Hash; }
        }
        void RemoveVerifiedSource(Node node, string target)
        {
            control.Check(); RejectLinks(node.Source);
            // Hold READ+DELETE access without WRITE/DELETE sharing until deletion is scheduled.
            // This prevents another process replacing the source between hash verification and removal.
            using (var handle = CreateFile(node.Source, 0x80010000, 1, IntPtr.Zero, 3, 0x08000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException(L.Text("copy.sourceCannotBeDeletedPrefix") + new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message);
                using (var source = new FileStream(handle, FileAccess.Read))
                using (var destination = File.OpenRead(target))
                {
                    if (source.Length != node.Bytes || destination.Length != node.Bytes || File.GetLastWriteTimeUtc(node.Source) != node.SourceTime ||
                        (FullVerification(node, target) && !CheckedHash(source).SequenceEqual(CheckedHash(destination))))
                        throw new IOException(L.Text("copy.changedSourceNotDeleted"));
                    control.Check(); int delete = 1; var attributes = File.GetAttributes(node.Source); bool readOnly = (attributes & FileAttributes.ReadOnly) != 0;
                    if (readOnly) File.SetAttributes(node.Source, attributes & ~FileAttributes.ReadOnly);
                    if (!SetFileInformationByHandle(handle, 4, ref delete, 4))
                    {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        if (readOnly) File.SetAttributes(node.Source, attributes);
                        throw new IOException(L.Text("copy.verifiedSourceDeleteFailedPrefix") + new System.ComponentModel.Win32Exception(error).Message);
                    }
                }
            }
        }
        void CopyStream(Stream input, Stream output, HashAlgorithm hash, Node node, string target, bool prefix)
        {
            byte[] buffer = new byte[1024 * 1024]; int count; long written = 0;
            while (true)
            {
                control.Check(); long started = System.Diagnostics.Stopwatch.GetTimestamp();
                count = input.Read(buffer, 0, buffer.Length); if (count == 0) break;
                output.Write(buffer, 0, count); hash.TransformBlock(buffer, 0, count, null, 0); written += count;
                if (!prefix) { transferredBytes += count; transferTicks += Math.Max(1, System.Diagnostics.Stopwatch.GetTimestamp() - started); }
                Publish(node.Source, target, prefix ? L.Text("copy.preparingAppend") : L.Code("copy.title"), prefix ? 0 : written, node.Bytes, false);
            }
        }
        void Publish(string source, string target, string phase, long fileDone, long fileTotal, bool force)
        {
            if (!force && (DateTime.UtcNow - lastReport).TotalMilliseconds < 80) return;
            lastReport = DateTime.UtcNow;
            long totalDone = Math.Min(totalBytes, doneBytes + (phase == L.Code("state.completed") ? 0 : fileDone));
            bool copying = !control.Paused && (phase == L.Code("copy.title") || phase == L.Code("state.completed"));
            long rate = copying && transferTicks > 0 ? (long)Math.Min(Int64.MaxValue, (double)transferredBytes * System.Diagnostics.Stopwatch.Frequency / transferTicks) : 0;
            if (report != null) report(new CopyUpdate { Source = source, Target = target, Phase = phase, FileDone = fileDone, FileTotal = fileTotal,
                TotalDone = totalDone, TotalBytes = totalBytes, Completed = finishedFiles, TotalFiles = totalFiles,
                BytesPerSecond = rate, RemainingSeconds = rate > 0 && totalBytes > 0 ? (double?)((double)Math.Max(0,totalBytes - totalDone) / rate) : null });
        }
        internal static bool MatchesFilter(string name, string filter)
        {
            string[] sides = (String.IsNullOrWhiteSpace(filter) ? "*.*" : filter).Split('|');
            Func<string, bool> matches = delegate(string masks)
            {
                return masks.Split(';').Any(mask =>
                {
                    string m = mask.Trim(); if (m == "*.*") m = "*";
                    return m.Length > 0 && Regex.IsMatch(name, "^" + Regex.Escape(m).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                });
            };
            return matches(String.IsNullOrWhiteSpace(sides[0]) ? "*" : sides[0]) && (sides.Length < 2 || !matches(sides[1]));
        }
        internal static void ValidateRelation(string source, string target)
        {
            string a = Path.GetFullPath(source).TrimEnd('\\'), b = Path.GetFullPath(target).TrimEnd('\\');
            if (String.Equals(a, b, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(source) && b.StartsWith(a + "\\", StringComparison.OrdinalIgnoreCase)))
                throw new IOException(L.Text("copy.sameOrNestedDestination"));
        }
        static void RejectLinks(string path)
        {
            for (string p = Path.GetFullPath(path); !String.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(L.Text("copy.reparsePointRejectedPrefix") + p);
        }
        static string UniqueName(string path)
        {
            bool dir = Directory.Exists(path); string ext = dir ? "" : Path.GetExtension(path), stem = dir ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
            for (int i = 2; ; i++) { string name = stem + " (" + i + ")" + ext; string candidate = Path.Combine(Path.GetDirectoryName(path), name); if (!File.Exists(candidate) && !Directory.Exists(candidate)) return name; }
        }
    }
}

