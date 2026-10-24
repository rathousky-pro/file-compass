using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileCompass
{
    internal sealed class SyncStamp
    {
        internal bool Directory; internal long Length; internal DateTime Modified; internal FileAttributes Attributes;
        internal static SyncStamp Read(string path)
        {
            if (!File.Exists(path) && !System.IO.Directory.Exists(path)) return null;
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("compare.linkUnsupportedPrefix") + path);
            bool folder = (attributes & FileAttributes.Directory) != 0;
            return new SyncStamp { Directory = folder, Attributes = attributes, Length = folder ? 0 : new FileInfo(path).Length, Modified = File.GetLastWriteTimeUtc(path) };
        }
        internal bool Matches(string path)
        {
            var now = Read(path); return now != null && now.Directory == Directory && now.Attributes == Attributes && (Directory || (now.Length == Length && now.Modified == Modified));
        }
    }
    internal sealed partial class SyncRow : System.ComponentModel.INotifyPropertyChanged
    {
        public string Relative { get; set; } public string Difference { get; set; } public string Action { get { return action; } set { action = value; NotifyAction(); } }
        string action;
        public string SourceInfo { get { return Info(Source); } } public string TargetInfo { get { return Info(Target); } }
        internal SyncStamp Source, Target; internal string SourcePath, TargetPath;
        static string Info(SyncStamp stamp) { return stamp == null ? L.Text("common.unknownValue") : stamp.Directory ? "<DIR>" : CopyUi.Bytes(stamp.Length) + " · " + stamp.Modified.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"); }
    }
    internal sealed class SyncPlan
    {
        internal string SourceRoot, TargetRoot; internal List<SyncRow> Rows;
        internal bool Mirror, Bidirectional;
        internal SyncLocation SourceLocation,TargetLocation;
    }
    internal static class DirectorySync
    {
        internal static string Root(string path) { return Path.GetFullPath(path).TrimEnd('\\') + "\\"; }
        static void SafePath(string path)
        {
            for (string current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("compare.pathContainsLinkPrefix") + current);
        }
        static Dictionary<string, SyncStamp> Scan(string root, Action check)
        {
            SafePath(root); if (!Directory.Exists(root)) throw new IOException(L.Text("compare.folderUnavailablePrefix") + root);
            var found = new Dictionary<string, SyncStamp>(StringComparer.OrdinalIgnoreCase); var folders = new Stack<string>(); folders.Push(root);
            while (folders.Count > 0) {
                check(); string folder = folders.Pop(); SafePath(folder);
                foreach (string path in Directory.EnumerateFileSystemEntries(folder)) {
                    check(); var stamp = SyncStamp.Read(path); if (stamp == null) throw new IOException(L.Text("compare.itemDisappearedPrefix") + path);
                    found.Add(path.Substring(root.Length), stamp); if (stamp.Directory) folders.Push(path);
                }
            }
            return found;
        }
        static bool SameContent(string left, string right, Action check)
        {
            using (var a = new FileStream(left, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var b = new FileStream(right, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var x = new byte[65536]; var y = new byte[65536];
                while (true) { check(); int count = a.Read(x, 0, x.Length), other = 0;
                    while (other < count) { int n = b.Read(y, other, count - other); if (n == 0) return false; other += n; }
                    if (count == 0) return b.ReadByte() < 0;
                    for (int i = 0; i < count; i++) if (x[i] != y[i]) return false;
                }
            }
        }
        internal static SyncPlan Compare(string source, string target, bool contents, bool mirror, Action check, bool bidirectional = false)
        {
            source = Root(source); target = Root(target);
            if (source.StartsWith(target, StringComparison.OrdinalIgnoreCase) || target.StartsWith(source, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("compare.sameOrNestedFoldersRejected"));
            var a = Scan(source, check); var b = Scan(target, check); var rows = new List<SyncRow>();
            foreach (string name in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)) {
                check(); SyncStamp first, second; a.TryGetValue(name, out first); b.TryGetValue(name, out second);
                var row = new SyncRow { Relative = name, Source = first, Target = second, SourcePath = Path.Combine(source, name), TargetPath = Path.Combine(target, name) };
                if (first == null) { row.Difference = L.Text("compare.onlyInTarget"); row.Action = mirror ? L.Text("action.remove") : L.Text("action.keep"); }
                else if (second == null) { row.Difference = "Pouze ve zdroji"; row.Action = first.Directory ? L.Text("compare.createFolderPlan") : L.Text("action.copy"); }
                else if (first.Directory != second.Directory) { row.Difference = L.Text("compare.fileFolderConflict"); row.Action = L.Text("state.conflict"); }
                else if (first.Directory) { row.Difference = L.Text("compare.equalFolders"); row.Action = L.Text("action.keep"); }
                else {
                    bool same = first.Length == second.Length && (contents ? SameContent(row.SourcePath, row.TargetPath, check) : first.Modified == second.Modified);
                    if (!first.Matches(row.SourcePath) || !second.Matches(row.TargetPath)) throw new IOException(L.Text("compare.fileChangedPrefix") + name);
                    row.Difference = same ? L.Text("compare.equalFiles") : first.Modified > second.Modified ? L.Text("compare.sourceNewerOrDifferent") : second.Modified > first.Modified ? L.Text("compare.targetNewerOrDifferent") : L.Text("compare.differentContentOrSize");
                    row.Action = same ? L.Text("action.keep") : L.Code("action.overwrite");
                }
                rows.Add(row);
            }
            var plan = new SyncPlan { SourceRoot = source, TargetRoot = target, Rows = rows, Mirror = mirror };
            if (bidirectional) BidirectionalSync.Configure(plan);
            return plan;
        }
        static void Validate(SyncRow row)
        {
            SafePath(row.SourcePath); SafePath(row.TargetPath);
            if ((row.Source == null ? SyncStamp.Read(row.SourcePath) != null : !row.Source.Matches(row.SourcePath)) || (row.Target == null ? SyncStamp.Read(row.TargetPath) != null : !row.Target.Matches(row.TargetPath)))
                throw new IOException(L.Text("compare.itemChangedSinceScanPrefix") + row.Relative);
        }
        internal static List<OperationItem> Apply(SyncPlan plan, CopyControl control, Action<string, double> progress, List<OperationItem> result = null)
        {
            if (plan.Rows.Any(x => x.Reverse)) return BidirectionalSync.Apply(plan, control, progress, result);
            if (plan.SourceLocation != null) return RemoteDirectorySync.Apply(plan,control,progress,result);
            result = result ?? new List<OperationItem>();
            if (plan.Rows.Any(x => x.Action == L.Text("state.conflict"))) throw new IOException(L.Text("compare.resolveConflictsFirst"));
            SafePath(plan.SourceRoot); SafePath(plan.TargetRoot);
            if (!Directory.Exists(plan.SourceRoot) || !Directory.Exists(plan.TargetRoot)) throw new IOException(L.Text("sync.rootUnavailable"));
            // Fail closed if anything in the preview changed before the first write.
            foreach (var row in plan.Rows) { control.Check(); Validate(row); }
            var writes = plan.Rows.Where(x => x.Action == L.Text("compare.createFolderPlan")).OrderBy(x => x.Relative.Length)
                .Concat(plan.Rows.Where(x => x.Action == L.Text("action.copy") || x.Action == L.Code("action.overwrite")))
                .Concat(plan.Rows.Where(x => x.Action == L.Text("action.remove")).OrderBy(x => x.Target.Directory ? 1 : 0).ThenByDescending(x => x.Relative.Length)).ToArray();
            int done = 0; bool writeFailed = false;
            foreach (var row in writes) {
                control.Check(); progress(row.Action + " · " + row.Relative, writes.Length == 0 ? 1 : (double)done / writes.Length);
                var item = new OperationItem { Source = row.Action == L.Text("action.remove") ? row.TargetPath : row.SourcePath, Target = row.TargetPath, Directory = (row.Source ?? row.Target).Directory, State = L.Code("state.completed") };
                try {
                    if (row.Action == L.Text("action.remove") && writeFailed) { item.State = L.Code("state.skipped"); item.Error = L.Text("sync.deleteSkippedAfterCopyError"); }
                    else {
                        Validate(row);
                        if (row.Action == L.Text("compare.createFolderPlan")) Directory.CreateDirectory(row.TargetPath);
                        else if (row.Action == L.Text("action.remove")) {
                            if (row.Target.Directory) Directory.Delete(row.TargetPath, false); // Never recursively delete unreviewed new files.
                            else DeleteOperations.Local(row.TargetPath, DeleteOperations.IsNetwork(row.TargetPath));
                        } else {
                            var options = new CopyOptions { Target = row.TargetPath, TargetIsFile = true, Rule = CopyRule.OverwriteAll, Times = true, Attributes = true };
                            var copied = new CopyEngine(options, control, delegate { return new CopyDecision(CopyChoice.Cancel); }, delegate(CopyUpdate update) { progress(update.Phase + " · " + row.Relative, (done + (update.FileTotal > 0 ? (double)update.FileDone / update.FileTotal : 0)) / Math.Max(1, writes.Length)); }).Run(new[] { row.SourcePath });
                            if (copied.Cancelled) throw new OperationCanceledException();
                            if (copied.Errors.Count > 0 || copied.Copied != 1) throw new IOException(String.Join("\n", copied.Errors));
                        }
                    }
                } catch (OperationCanceledException) { throw; }
                catch (Exception ex) { item.State = L.Code("state.error"); item.Error = ex.Message; writeFailed = true; }
                result.Add(item); done++;
            }
            return result;
        }
    }
}

