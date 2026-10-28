using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic.FileIO;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public static class DeleteOperations
    {
        public static void Validate(string path, bool remote)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new IOException(L.Text("delete.invalidPath"));
            if (remote)
            {
                if (RemoteConnection.Normalize(path) == "/") throw new IOException(L.Text("delete.connectionRootRejected"));
            }
            else
            {
                string full = Path.GetFullPath(path).TrimEnd('\\', '/');
                if (String.Equals(full, Path.GetPathRoot(full).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    throw new IOException(L.Text("delete.driveRootRejected"));
            }
        }
        public static bool IsNetwork(string path)
        {
            if (path.StartsWith("\\\\")) return true;
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).DriveType == DriveType.Network;
        }
        public static void Local(string path, bool permanent)
        {
            Validate(path, false);
            if(permanent){PathSafety.DeleteTree(path);return;}
            using(PathSafety.LockAncestors(path)) {
            // Shell recycling also handles directory links without walking their targets.
            var recycle = permanent ? RecycleOption.DeletePermanently : RecycleOption.SendToRecycleBin;
            if (Directory.Exists(path)) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, recycle, UICancelOption.ThrowException);
            else FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, recycle, UICancelOption.ThrowException);
            }
        }
        public static void Remote(Session session, string path)
        {
            Validate(path, true);
            // Escape literal names, so brackets and wildcard characters cannot select other files.
            session.RemoveFiles(RemotePath.EscapeFileMask(path.TrimEnd('/'))).Check();
        }
    }
    public partial class MainWindow
    {
        int deletions;
        async void DeleteSelected()
        {
            if(RejectExtWrite())return;
            if (busy || PanelsLoading || deletions > 0) return;
            if (active.IsArchive) { ArchiveDelete(); return; }
            if (active.Projection != null && active.Projection.Kind == "Basket") { RemoveFromBasket(); return; }
            var panel = active;
            var selected = panel.Selected().Where(x => !x.IsParent).Select(x => new Entry { FullPath = x.FullPath, Name = x.Name, IsDirectory = x.IsDirectory }).ToList();
            if (selected.Count == 0) return;
            bool remote = panel.IsRemote;
            bool permanent = remote || panel.IsAtLocalNetwork || selected.Any(x => DeleteOperations.IsNetwork(x.FullPath));
            string directory = panel.CurrentPath;
            var connection = panel.Remote;
            string location = remote ? connection.Display(directory) : directory;
            var confirmation = new DeleteConfirmationDialog(this, selected, !permanent, permanent || !ProgramSettings.Load().DeleteToRecycleBin || System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift, location);
            if (confirmation.ShowDialog() != true) return; permanent = confirmation.Permanent;
            await DeleteBatch(selected, connection, remote, permanent, directory);
        }
        async Task DeleteBatch(List<Entry> selected, RemoteConnection connection, bool remote, bool permanent, string directory)
        {
            RemoteConnection worker = null;
            bool cancelled = false, skipErrors = false; int done = 0, skipped = 0;
            deletions++;
            var activity = AddCopyActivity(L.Text("delete.operationTitlePrefix") + directory, delegate { cancelled = true; if (worker != null) worker.Session.Abort(); });
            activity.Record.Retry = delegate { var failed = activity.Record.Items.Where(x => x.State == L.Code("state.error")).Select(x => new Entry { FullPath = x.Source, Name = Path.GetFileName(x.Source) }).ToList(); if (failed.Count > 0) { var retryTask = DeleteBatch(failed, connection, remote, permanent, directory); } };
            try
            {
                foreach (var entry in selected) DeleteOperations.Validate(entry.FullPath, remote);
                if (remote) { worker = connection.TransferConnection(); await Task.Run(delegate { worker.OpenTransfer(); }); }
                foreach (var entry in selected)
                {
                    if (cancelled) break;
                    activity.Update(L.Text("delete.statusPrefix") + entry.FullPath + " · " + done + " / " + selected.Count, 0, (double)(done + skipped) / selected.Count);
                    bool retry;
                    do
                    {
                        retry = false;
                        try
                        {
                            await Task.Run(delegate { if (remote) DeleteOperations.Remote(worker.Session, entry.FullPath); else DeleteOperations.Local(entry.FullPath, permanent); });
                            done++; activity.Record.Items.Add(new OperationItem { Source = entry.FullPath, State = L.Code("state.completed") });
                        }
                        catch (OperationCanceledException) { cancelled = true; }
                        catch (Exception ex)
                        {
                            CopyErrorChoice answer = CopyErrorChoice.Skip;
                            if (!skipErrors) { var error = new CopyErrorDialog(this, new CopyFailure { Source = entry.FullPath, Error = ex }, true); error.ShowDialog(); answer = error.Choice; }
                            if (answer == CopyErrorChoice.Retry) retry = true;
                            else
                            {
                                activity.Record.Items.Add(new OperationItem { Source = entry.FullPath, State = L.Code("state.error"), Error = ex.Message });
                                if (answer == CopyErrorChoice.Cancel) cancelled = true;
                                else { skipped++; if (answer == CopyErrorChoice.SkipAll) skipErrors = true; }
                            }
                        }
                    } while (retry && !cancelled);
                }
                activity.Finish((cancelled ? L.Text("delete.interrupted") : L.Text("delete.completed")) + L.Text("delete.deletedCountPrefix") + done + " / " + selected.Count + (skipped > 0 ? L.Text("copy.skippedSummaryPrefix") + skipped : ""), !cancelled && skipped == 0);
            }
            catch (Exception ex) { activity.Finish(L.Text("delete.failedPrefix") + ex.Message, false); MessageBox.Show(this, ex.Message, L.Text("delete.failedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally
            {
                if (worker != null) worker.Dispose();
                deletions--;
                // Refresh only the original location; the user may have navigated elsewhere.
                foreach (var current in new[] { left, right })
                    if (!current.IsLoading && current.IsRemote == remote && current.CurrentPath == directory && (!remote || current.Remote == connection)) current.Refresh();
            }
        }
    }
}
