using System;
using System.IO;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public enum NetworkCopyRule { Reject, Skip, Overwrite, Resume }
    public sealed class NetworkCopyResult { public int Copied, Skipped; }
    public static class NetworkTransfers
    {
        public static void Copy(Entry entry, RemoteConnection source, RemoteConnection target, string targetPath,
            NetworkCopyRule rule = NetworkCopyRule.Reject, NetworkCopyResult result = null, Action<long> skipped = null, Action check = null, Action<string, bool> fileComplete = null, Func<CopyFailure, CopyErrorChoice> resolveError = null)
        {
            while (true)
            {
                try { CopyCore(entry, source, target, targetPath, rule, result, skipped, check, fileComplete, resolveError); return; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    if (check != null) check();
                    if (entry.IsDirectory || resolveError == null || !(ex is IOException || ex is UnauthorizedAccessException || ex is TimeoutException)) throw;
                    var choice = resolveError(new CopyFailure { Source = entry.FullPath, Target = source != null ? Path.Combine(targetPath, RemoteConnection.Name(entry.FullPath)) : RemoteConnection.Combine(targetPath, Path.GetFileName(entry.FullPath)), Error = ex });
                    if (check != null) check();
                    if (choice == CopyErrorChoice.Retry) continue;
                    if (choice == CopyErrorChoice.Cancel) throw new OperationCanceledException();
                    if (result != null) result.Skipped++;
                    if (fileComplete != null) fileComplete(entry.FullPath, true);
                    return;
                }
            }
        }
        static void CopyCore(Entry entry, RemoteConnection source, RemoteConnection target, string targetPath,
            NetworkCopyRule rule, NetworkCopyResult result, Action<long> skipped, Action check, Action<string, bool> fileComplete, Func<CopyFailure, CopyErrorChoice> resolveError)
        {
            if (check != null) check();
            if ((source == null) == (target == null)) throw new IOException(L.Text("transfer.exactlyOneRemotePanelRequired"));
            var options = new TransferOptions { TransferMode = TransferMode.Binary, PreserveTimestamp = true, OverwriteMode = OverwriteMode.Overwrite, Resume = rule == NetworkCopyRule.Resume && (target == null || String.IsNullOrEmpty(target.Profile.CloudAccountId)) };
            if (source != null)
            {
                string name = RemoteConnection.Name(entry.FullPath); FileOperations.ValidateName(name);
                string destination = Path.Combine(targetPath, name);
                bool exists = File.Exists(destination) || Directory.Exists(destination);
                if (exists && rule == NetworkCopyRule.Reject) throw new IOException(L.Text("fileOperation.existingDestinationPrefix") + destination);
                bool resumePartial = rule == NetworkCopyRule.Resume && !entry.IsDirectory && source.Profile.Protocol != NetworkProtocol.SCP && source.Session.HasCheckpoint(false, entry.FullPath, destination);
                bool completeTarget = exists && !entry.IsDirectory && File.Exists(destination) && new FileInfo(destination).Length == source.Session.GetFileInfo(entry.FullPath).Length;
                if (exists && (rule == NetworkCopyRule.Skip || (rule == NetworkCopyRule.Resume && !resumePartial && completeTarget)) && (!entry.IsDirectory || !Directory.Exists(destination)))
                { if (result != null) result.Skipped++; if (skipped != null && !entry.IsDirectory) skipped(source.Session.GetFileInfo(entry.FullPath).Length); if (fileComplete != null && !entry.IsDirectory) fileComplete(entry.FullPath, true); return; }
                if (exists && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.targetIsLinkPrefix") + destination);
                if (exists && entry.IsDirectory != Directory.Exists(destination)) throw new IOException(L.Text("transfer.targetFileFolderCollisionPrefix") + destination);
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(destination);
                    foreach (var child in source.Session.ListDirectory(entry.FullPath).Files)
                    {
                        if (child.Name == "." || child.Name == "..") continue;
                        RemoteConnection.ValidateRemoteName(child.Name); FileOperations.ValidateName(child.Name);
                        if (child.FileType == 'L') throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + child.FullName);
                        Copy(new Entry { FullPath = child.FullName, IsDirectory = child.IsDirectory }, source, null, destination, rule, result, skipped, check, fileComplete, resolveError);
                    }
                    return;
                }
                source.Session.GetFiles(RemotePath.EscapeFileMask(entry.FullPath), targetPath.TrimEnd('\\') + "\\", false, options).Check();
            }
            else
            {
                string destination = RemoteConnection.Combine(targetPath, Path.GetFileName(entry.FullPath));
                bool exists = target.Session.FileExists(destination);
                if (exists && rule == NetworkCopyRule.Reject) throw new IOException(L.Text("fileOperation.existingDestinationPrefix") + destination);
                bool resumePartial = rule == NetworkCopyRule.Resume && !entry.IsDirectory && target.Profile.Protocol != NetworkProtocol.SCP && String.IsNullOrEmpty(target.Profile.CloudAccountId) && target.Session.HasCheckpoint(true, entry.FullPath, destination);
                bool completeTarget = exists && !entry.IsDirectory && !target.Session.GetFileInfo(destination).IsDirectory && target.Session.GetFileInfo(destination).Length == new FileInfo(entry.FullPath).Length;
                if (exists && (rule == NetworkCopyRule.Skip || (rule == NetworkCopyRule.Resume && !resumePartial && completeTarget)) && (!entry.IsDirectory || !target.Session.GetFileInfo(destination).IsDirectory))
                { if (result != null) result.Skipped++; if (skipped != null && !entry.IsDirectory) skipped(new FileInfo(entry.FullPath).Length); if (fileComplete != null && !entry.IsDirectory) fileComplete(entry.FullPath, true); return; }
                if (exists)
                {
                    var info = target.Session.GetFileInfo(destination);
                    if (info.FileType == 'L') throw new IOException(L.Text("transfer.targetIsLinkPrefix") + destination);
                    if (entry.IsDirectory != info.IsDirectory) throw new IOException(L.Text("transfer.targetFileFolderCollisionPrefix") + destination);
                }
                RejectLinks(entry.FullPath);
                if (entry.IsDirectory)
                {
                    if (!exists) target.Session.CreateDirectory(destination);
                    foreach (var child in Directory.GetFileSystemEntries(entry.FullPath))
                        Copy(new Entry { FullPath = child, IsDirectory = Directory.Exists(child) }, null, target, destination, rule, result, skipped, check, fileComplete, resolveError);
                    return;
                }
                target.Session.PutFiles(entry.FullPath, RemotePath.EscapeOperationMask(targetPath.TrimEnd('/') + "/"), false, options).Check();
            }
            if (result != null) result.Copied++;
            if (fileComplete != null) fileComplete(entry.FullPath, false);
        }
        static void RejectLinks(string path)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("transfer.linkUnsupportedPrefix") + path);
            // CopyCore checks each child before processing it. Recursively scanning
            // here would revisit whole subtrees before the first byte is sent.
        }
    }
}

