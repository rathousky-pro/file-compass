using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal sealed class SyncLocation : IDisposable
    {
        internal readonly string Root; internal readonly RemoteConnection Template;
        internal SyncLocation(string path,RemoteConnection remote = null) { Root = remote == null ? DirectorySync.Root(path) : RemoteConnection.Normalize(path); Template = remote == null ? null : remote.TransferConnection(); }
        internal string Caption { get { return Template == null ? Root : Template.Display(Root); } }
        internal SyncAccess Open(CancellationToken token) { return new SyncAccess(this,token); }
        public void Dispose() { if (Template != null) Template.Dispose(); }
    }
    internal sealed class SyncAccess : IDisposable
    {
        internal readonly SyncLocation Location; internal RemoteConnection Connection; readonly CancellationToken token; CancellationTokenRegistration abort;
        internal SyncAccess(SyncLocation location,CancellationToken cancellation)
        {
            Location = location; token = cancellation;
            if (location.Template != null) { Connection = location.Template.TransferConnection(); abort = token.Register(delegate { Connection.Session.Abort(); }); try { token.ThrowIfCancellationRequested(); Connection.OpenTransfer(); } catch { Dispose(); throw; } }
        }
        internal string PathFor(string relative)
        {
            if (relative.Split('/','\\').Any(x => x == "..") || relative.StartsWith("/") || relative.IndexOf(':') >= 0) throw new IOException(L.Text("sync.invalidRelativePath"));
            return Connection == null ? Path.Combine(Location.Root,relative.Replace('/', '\\')) : RemoteConnection.Combine(Location.Root,relative.Replace('\\','/'));
        }
        internal SyncStamp Read(string relative)
        {
            token.ThrowIfCancellationRequested(); string path = PathFor(relative);
            if (Connection == null) {
                for (string parent = Path.GetFullPath(path); !String.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent)) if ((File.Exists(parent) || Directory.Exists(parent)) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("compare.pathContainsLinkPrefix") + parent);
                return SyncStamp.Read(path);
            }
            for (string parent = RemoteConnection.Parent(path); parent != "/"; parent = RemoteConnection.Parent(parent)) if (Connection.Session.FileExists(parent) && Connection.Session.GetFileInfo(parent).FileType == 'L') throw new IOException(L.Text("compare.pathContainsLinkPrefix") + parent);
            if (!Connection.Session.FileExists(path)) return null;
            var info = Connection.Session.GetFileInfo(path); if (info.FileType == 'L') throw new IOException(L.Text("sync.linksUnsupportedPrefix") + path);
            return new SyncStamp { Directory = info.IsDirectory,Length = info.IsDirectory ? 0 : info.Length,Modified = info.LastWriteTime.ToUniversalTime(),Attributes = info.IsDirectory ? FileAttributes.Directory : 0 };
        }
        internal Dictionary<string,SyncStamp> Scan(StringComparer comparer,Action check)
        {
            var root = Read(""); if (root == null || !root.Directory) throw new IOException(L.Text("compare.folderUnavailablePrefix") + Location.Caption);
            if (Connection != null) for (string path = Location.Root; path != "/"; path = RemoteConnection.Parent(path)) if (Connection.Session.GetFileInfo(path).FileType == 'L') throw new IOException(L.Text("sync.rootContainsLink"));
            var result = new Dictionary<string,SyncStamp>(comparer); var folders = new Stack<string>(); folders.Push("");
            while (folders.Count > 0) {
                check(); string relative = folders.Pop(), path = PathFor(relative);
                var names = Connection == null ? Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).ToArray() : Connection.Session.ListDirectory(path).Files.Where(x => !x.IsParentDirectory && !x.IsThisDirectory).Select(x => x.Name).ToArray();
                foreach (var name in names) { check(); RemoteConnection.ValidateRemoteName(name); string child = relative.Length == 0 ? name : relative + "/" + name; var stamp = Read(child); if (stamp == null) throw new IOException(L.Text("sync.itemMissingPrefix") + child); result.Add(child,stamp); if (stamp.Directory) folders.Push(child); }
            } return result;
        }
        internal string ContentHash(string relative,SyncStamp stamp,Action check)
        {
            using (var hash = new HashWriteStream()) {
                if (Connection != null) Connection.Session.DownloadRelay(PathFor(relative),hash,stamp.Length,delegate { check(); });
                else using (var stream = new FileStream(PathFor(relative),FileMode.Open,FileAccess.Read,FileShare.Read)) { byte[] bytes = new byte[65536]; int read; while ((read = stream.Read(bytes,0,bytes.Length)) > 0) { check(); hash.Write(bytes,0,read); } }
                return hash.Finish();
            }
        }
        internal static bool Equal(SyncStamp expected,SyncStamp current) { return expected == null ? current == null : current != null && current.Directory == expected.Directory && current.Attributes == expected.Attributes && (expected.Directory || (current.Length == expected.Length && current.Modified == expected.Modified)); }
        internal void Validate(string relative,SyncStamp expected) { if (!Equal(expected,Read(relative))) throw new IOException(L.Text("sync.itemChangedSinceComparePrefix") + relative); }
        public void Dispose() { abort.Dispose(); if (Connection != null) Connection.Dispose(); }
    }
    internal static class RemoteDirectorySync
    {
        internal static SyncPlan Compare(SyncLocation source,SyncLocation target,bool contents,bool mirror,CopyControl control,bool bidirectional = false)
        {
            bool same = source.Template == null && target.Template == null;
            if (source.Template != null && target.Template != null) { var a = source.Template.Profile; var b = target.Template.Profile; same = a.Protocol == b.Protocol && String.Equals(a.Host,b.Host,StringComparison.OrdinalIgnoreCase) && a.Port == b.Port && a.UserName == b.UserName; }
            var comparison = source.Template == null ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string first = source.Root.TrimEnd('/','\\') + "/",second = target.Root.TrimEnd('/','\\') + "/";
            if (same && (first.StartsWith(second,comparison) || second.StartsWith(first,comparison))) throw new IOException(L.Text("sync.sameOrNestedRootsRejected"));
            using (var a = source.Open(control.Token)) using (var b = target.Open(control.Token)) {
                var comparer = source.Template == null || target.Template == null ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var left = a.Scan(comparer,control.Check); var right = b.Scan(comparer,control.Check); var rows = new List<SyncRow>();
                foreach (string name in left.Keys.Union(right.Keys,comparer).OrderBy(x => x,comparer)) {
                    control.Check(); if (target.Template == null || (bidirectional && source.Template == null)) foreach (string part in name.Split('/')) FileOperations.ValidateName(part);
                    SyncStamp x,y; left.TryGetValue(name,out x); right.TryGetValue(name,out y); var row = new SyncRow { Relative = name,Source = x,Target = y,SourcePath = a.PathFor(name),TargetPath = b.PathFor(name) };
                    if (x == null) { row.Difference = L.Text("compare.onlyInTarget"); row.Action = mirror ? L.Text("action.remove") : L.Text("action.keep"); }
                    else if (y == null) { row.Difference = "Pouze ve zdroji"; row.Action = x.Directory ? L.Text("compare.createFolderPlan") : L.Text("action.copy"); }
                    else if (x.Directory != y.Directory) { row.Difference = L.Text("compare.fileFolderConflict"); row.Action = L.Text("state.conflict"); }
                    else if (x.Directory) { row.Difference = L.Text("compare.equalFolders"); row.Action = L.Text("action.keep"); }
                    else { bool equal = x.Length == y.Length && (contents ? a.ContentHash(name,x,control.Check) == b.ContentHash(name,y,control.Check) : x.Modified == y.Modified); a.Validate(name,x); b.Validate(name,y); row.Difference = equal ? L.Text("compare.equalFiles") : L.Text("compare.differentFiles"); row.Action = equal ? L.Text("action.keep") : L.Code("action.overwrite"); }
                    rows.Add(row);
                }
                var plan = new SyncPlan { SourceRoot = source.Caption,TargetRoot = target.Caption,SourceLocation = source,TargetLocation = target,Rows = rows,Mirror = mirror };
                if (bidirectional) BidirectionalSync.Configure(plan);
                return plan;
            }
        }
        internal static List<OperationItem> Apply(SyncPlan plan,CopyControl control,Action<string,double> progress,List<OperationItem> result)
        {
            result = result ?? new List<OperationItem>(); if (plan.Rows.Any(x => x.Action == L.Text("state.conflict"))) throw new IOException(L.Text("sync.resolveConflictsFirst"));
            using (var a = plan.SourceLocation.Open(control.Token)) using (var b = plan.TargetLocation.Open(control.Token)) {
                if (a.Read("") == null || b.Read("") == null) throw new IOException(L.Text("sync.rootNotAvailable"));
                foreach (var row in plan.Rows) { control.Check(); a.Validate(row.Relative,row.Source); b.Validate(row.Relative,row.Target); }
                var writes = plan.Rows.Where(x => x.Action == L.Text("compare.createFolderPlan")).OrderBy(x => x.Relative.Length).Concat(plan.Rows.Where(x => x.Action == L.Text("action.copy") || x.Action == L.Code("action.overwrite"))).Concat(plan.Rows.Where(x => x.Action == L.Text("action.remove")).OrderBy(x => x.Target.Directory ? 1 : 0).ThenByDescending(x => x.Relative.Length)).ToArray(); int done = 0; bool failed = false;
                foreach (var row in writes) {
                    control.Check(); var item = new OperationItem { Source = row.SourcePath,Target = row.TargetPath,Directory = (row.Source ?? row.Target).Directory,State = L.Code("state.completed") }; progress(row.Action + " · " + row.Relative,(double)done / Math.Max(1,writes.Length));
                    try {
                        if (row.Action == L.Text("action.remove") && failed) { item.State = L.Code("state.skipped"); item.Error = L.Text("sync.deleteSkippedAfterTransferError"); }
                        else {
                            a.Validate(row.Relative,row.Source); b.Validate(row.Relative,row.Target);
                            if (row.Action == L.Text("compare.createFolderPlan")) { if (b.Connection != null) b.Connection.Session.CreateDirectory(row.TargetPath); else Directory.CreateDirectory(row.TargetPath); }
                            else if (row.Action == L.Text("action.remove")) { if (b.Connection != null) { var current = b.Connection.Session.GetFileInfo(row.TargetPath); if (current.IsDirectory) b.Connection.Session.RemoveEmptyDirectory(row.TargetPath); else b.Connection.Session.RemoveVerifiedFile(row.TargetPath,current.Length,current.LastWriteTime); } else if (row.Target.Directory) Directory.Delete(row.TargetPath,false); else DeleteOperations.Local(row.TargetPath,DeleteOperations.IsNetwork(row.TargetPath)); }
                            else if (a.Connection != null && b.Connection != null) {
                                var job = new NetworkTransferJob(a.Connection.Profile,false,b.Connection.Profile); var queue = new NetworkQueueItem { Source = row.SourcePath,Destination = row.TargetPath,Bytes = row.Source.Length * 2,Modified = row.Source.Modified.ToLocalTime(),Relay = true }; job.BeginItem(queue);
                                using (control.Token.Register(job.Cancel)) try { NetworkRelay.StreamFile(job,a.Connection,b.Connection,queue,a.Connection.Session.GetFileInfo(row.SourcePath),b.Connection.Session.FileExists(row.TargetPath) ? b.Connection.Session.GetFileInfo(row.TargetPath) : null,false); } finally { job.EndItem(); job.Cancellation.Dispose(); }
                            }
                            else if (b.Connection != null) {
                                using (var input = new FileStream(row.SourcePath,FileMode.Open,FileAccess.Read,FileShare.Read)) b.Connection.Session.UploadRelay(input,row.Source.Length,row.Source.Modified.ToLocalTime(),row.TargetPath,row.Target == null ? null : b.Connection.Session.GetFileInfo(row.TargetPath),delegate { control.Check(); },delegate { a.Validate(row.Relative,row.Source); },delegate { });
                            }
                            else if (a.Connection != null) a.Connection.Session.GetFiles(RemotePath.EscapeFileMask(row.SourcePath),row.TargetPath,false,new TransferOptions { PreserveTimestamp = true }).Check();
                            else { var copied = new CopyEngine(new CopyOptions { Target = row.TargetPath,TargetIsFile = true,Rule = CopyRule.OverwriteAll },control,c => new CopyDecision(CopyChoice.Cancel),null).Run(new[] { row.SourcePath }); if (copied.Cancelled) throw new OperationCanceledException(); if (copied.Errors.Count > 0 || copied.Copied != 1) throw new IOException(String.Join("\n",copied.Errors)); }
                        }
                    } catch (OperationCanceledException) { throw; } catch (Exception ex) { item.State = L.Code("state.error"); item.Error = ex.Message; failed = true; }
                    result.Add(item); done++;
                }
            } return result;
        }
    }
}

