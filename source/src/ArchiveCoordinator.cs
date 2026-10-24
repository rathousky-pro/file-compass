using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FileCompass
{
    public partial class MainWindow
    {
        int archiveOperations;
        readonly Dictionary<Window, Func<bool>> archiveEditors = new Dictionary<Window, Func<bool>>();
        bool ConfirmArchiveEditors()
        {
            if (!archiveEditors.Values.Any(x => x())) return true;
            return MessageBox.Show(this, L.Text("archive.confirmDiscardEditorChanges"), L.Text("archive.unsavedChangesTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        async Task<bool> ArchiveOperation(string title, Action<CancellationToken, Action<string>> work, Action retryAction = null, bool startBackground = false)
        {
            archiveOperations++;
            using (var cancel = new CancellationTokenSource())
            {
                var dialog = new ArchiveProgressDialog(this, title, cancel);
                var activity = AddCopyActivity(title, cancel.Cancel, dialog.RestoreProgress);
                activity.TransferTitle = title; activity.Backgrounded = startBackground;
                dialog.Backgrounded = delegate { activity.Backgrounded = true; RefreshCopySummary(); };
                dialog.Foregrounded = delegate { activity.Backgrounded = false; RefreshCopySummary(); };
                if (!startBackground) dialog.Show(); RefreshCopySummary();
                double fraction = 0; bool known = false;
                string latestCompression = null;
                var updates = new Progress<string>(delegate(string text)
                {
                    ArchivePreparationProgress preparation;
                    if (ArchivePreparationProgress.TryParse(text, out preparation)) {
                        known = false; fraction = 0;
                        activity.TransferRoute = preparation.Path;
                        activity.Update(title + " · " + preparation.Summary, 0, 0, true);
                        dialog.UpdatePreparation(preparation); return;
                    }
                    ArchiveCompressionProgress compression;
                    if (ArchiveCompressionProgress.TryParse(text, out compression)) {
                        fraction = compression.Percent / 100; known = true;
                        activity.TransferRoute = compression.Summary();
                        activity.Update(title + " · " + compression.Summary(), fraction, fraction, false);
                        dialog.Update(L.Text("archive.compressingStatus"), fraction, true); dialog.UpdateCompression(compression); dialog.UpdateCurrentFile(compression.CurrentFile); return;
                    }
                    var percent = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*%");
                    if (percent.Success) { fraction = Math.Min(.99, Double.Parse(percent.Groups[1].Value) / 100); known = true; }
                    else if (!text.StartsWith(L.Code("state.verifying")) && !text.Contains("%") && text != L.Text("archive.verifying")) return;
                    activity.Update(title + " · " + text.Trim(), fraction, fraction, !known);
                    dialog.Update(text.Trim(), fraction, known);
                });
                try
                {
                    await Task.Run(delegate { work(cancel.Token, delegate(string text) { if (text.StartsWith("@archive-compression|", StringComparison.Ordinal)) latestCompression = text; ((IProgress<string>)updates).Report(text); }); });
                    ArchiveCompressionProgress completed; string result = ArchiveCompressionProgress.TryParse(latestCompression ?? "", out completed) && completed.Complete ? " · " + completed.Summary() : "";
                    activity.Finish(title + L.Text("state.completedSuffix") + result, true); return true;
                }
                catch (OperationCanceledException) { activity.Finish(title + L.Text("state.cancelledSuffix"), false); return false; }
                catch (Exception ex) { activity.Record.Items.Add(new OperationItem { Source = title, State = L.Code("state.error"), Error = ex.Message }); activity.Record.Retry = retryAction ?? delegate { var retry = ArchiveOperation(title, work); }; activity.Finish(title + L.Text("state.errorSuffix"), false); MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
                finally { dialog.Finish(); archiveOperations--; }
            }
        }
        bool ArchivePassword(ArchiveDocument document, IEnumerable<string> members)
        {
            if (!String.IsNullOrEmpty(document.Password) || !ArchiveEngine.Select(document, members).Any(x => x.Encrypted)) return true;
            document.Password = ArchivePasswordDialog.Ask(this, document.FilePath); return document.Password != null;
        }
        void RefreshArchivePanels(ArchiveDocument document)
        {
            foreach (var panel in new[] { left, right })
                if (panel.IsArchive && panel.Archive.Root.FilePath.Equals(document.Root.FilePath, StringComparison.OrdinalIgnoreCase)) panel.Refresh();
        }
        async void ArchiveTransfer()
        { await TransferArchiveEntries(active, active == left ? right : left, active.Selected().ToArray()); }
        async Task TransferArchiveEntries(FilePanel source, FilePanel target, Entry[] entries)
        {
            if (busy || PanelsLoading) return;
            if (entries.Length == 0) return;
            var sourceArchive = source.Archive; var targetArchive = target.Archive;
            string sourcePrefix = source.ArchivePrefix, targetPrefix = target.ArchivePrefix, destination = target.CurrentPath;
            if (targetArchive != null && !ArchiveEngine.Writable(targetArchive)) { MessageBox.Show(this, L.Text("archive.addUnsupportedFormat"), L.Text("archive.shortTitle")); return; }
            if (sourceArchive != null && !ArchivePassword(sourceArchive, entries.Select(x => x.FullPath.TrimStart('/')))) return;
            if (targetArchive != null && !ArchivePassword(targetArchive, targetArchive.Items.Select(x => x.Path))) return;
            var copyOptions = new CopyOptions();
            NetworkCopyRule rule;
            if (source.IsRemote || target.IsRemote)
            {
                var dialog = new NetworkCopyDialog(this, entries.Length, source.DisplayPath, target.DisplayPath,
                    source.Remote == null ? null : source.Remote.Profile, target.Remote == null ? null : target.Remote.Profile);
                if (dialog.ShowDialog() != true) return; rule = dialog.Rule;
            }
            else
            {
                var dialog = new ArchiveCopyDialog(this, entries.Length, source.DisplayPath, target.DisplayPath, targetArchive != null);
                if (dialog.ShowDialog() != true) return; copyOptions = dialog.Options;
                rule = copyOptions.Rule == CopyRule.OverwriteAll ? NetworkCopyRule.Overwrite : NetworkCopyRule.Skip;
            }
            await RunArchiveTransfer(entries, sourceArchive, targetArchive, targetPrefix, destination, copyOptions, rule, ArchiveSettings.Load(), source.Remote, target.Remote, target);
        }
        async Task RunArchiveTransfer(Entry[] entries, ArchiveDocument sourceArchive, ArchiveDocument targetArchive, string targetPrefix, string destination, CopyOptions copyOptions, NetworkCopyRule rule, ArchiveSettings settings, RemoteConnection sourceTemplate, RemoteConnection targetTemplate, FilePanel target)
        {
            RemoteConnection networkSource = sourceTemplate == null ? null : sourceTemplate.TransferConnection();
            RemoteConnection networkTarget = targetTemplate == null ? null : targetTemplate.TransferConnection();
            try
            {
                using (var temp = new ArchiveTemp())
                {
                string[] files = null;
                if (networkSource != null)
                {
                    var download = await StartNetworkCopy(entries, networkSource, true, temp.Path, null, NetworkCopyRule.Overwrite);
                    if (download.Cancellation.IsCancellationRequested || download.SnapshotItems().Any(x => x.State.StartsWith(L.Code("state.error")))) return;
                    files = entries.Select(x => Path.Combine(temp.Path, RemoteConnection.Name(x.FullPath))).Where(x => File.Exists(x) || Directory.Exists(x)).ToArray();
                    if (files.Length == 0) return;
                }
                bool success = await ArchiveOperation(targetArchive != null ? L.Text("archive.addingTitle") : L.Text("archive.extractingTitle"), delegate(CancellationToken token, Action<string> progress)
                {
                        if (sourceArchive != null)
                        {
                            var members = entries.Select(x => x.FullPath.TrimStart('/')).ToArray();
                            ArchiveEngine.Extract(sourceArchive, members, temp.Path, token, progress);
                            files = members.Select(x => ArchiveEngine.DiskPath(temp.Path, x)).ToArray();
                        }
                        else if (networkSource == null) files = entries.Select(x => x.FullPath).ToArray();
                        if (targetArchive != null)
                        {
                            using (var filtered = new ArchiveTemp())
                            {
                                CopyArchiveInputs(files, filtered.Path, copyOptions, token, progress);
                                var selectedFiles = Directory.GetFileSystemEntries(filtered.Path);
                                if (selectedFiles.Length > 0) ArchiveEngine.Add(targetArchive, targetPrefix, selectedFiles, rule == NetworkCopyRule.Overwrite, settings, token, progress);
                            }
                        }
                        else if (networkTarget == null)
                        {
                            using (var control = new CopyControl())
                            using (token.Register(control.Cancel))
                            {
                                copyOptions.Target = destination; copyOptions.Rule = rule == NetworkCopyRule.Overwrite ? CopyRule.OverwriteAll : CopyRule.SkipAll;
                                var result = new CopyEngine(copyOptions, control,
                                    delegate { return new CopyDecision(CopyChoice.Cancel); }, delegate(CopyUpdate update) { progress(update.Phase + " · " + Path.GetFileName(update.Source) + " · " + (update.TotalBytes > 0 ? 100.0 * update.TotalDone / update.TotalBytes : 0).ToString("F0") + "%"); },
                                    delegate(CopyFailure failure) { return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate { var error = new CopyErrorDialog(this, failure); error.ShowDialog(); return error.Choice; })); }).Run(files);
                                if (result.Cancelled) throw new OperationCanceledException();
                                if (result.Errors.Count > 0) throw new IOException(String.Join("\n", result.Errors));
                            }
                        }
                }, delegate { var again = RunArchiveTransfer(entries, sourceArchive, targetArchive, targetPrefix, destination, copyOptions.Clone(), rule, settings, sourceTemplate, targetTemplate, target); }, networkSource != null || networkTarget != null);
                if (success)
                {
                    if (networkTarget != null) await StartNetworkCopy(files.Select(x => new Entry { FullPath = x, IsDirectory = Directory.Exists(x) }).ToArray(), networkTarget, false, destination, null, rule);
                    if (targetArchive != null) RefreshArchivePanels(targetArchive);
                    else if (target.CurrentPath == destination && target.IsRemote == (networkTarget != null)) target.Refresh();
                }
                }
            }
            finally { if (networkSource != null) networkSource.Dispose(); if (networkTarget != null) networkTarget.Dispose(); }
        }
        void CopyArchiveInputs(string[] files, string destination, CopyOptions options, CancellationToken token, Action<string> progress)
        {
            using (var control = new CopyControl())
            using (token.Register(control.Cancel))
            {
                options.Target = destination;
                // The staging copy must remain removable even for read-only inputs.
                options.Attributes = false;
                var result = new CopyEngine(options, control, delegate { return new CopyDecision(CopyChoice.Cancel); },
                    delegate(CopyUpdate update) { progress(update.Phase + " · " + (update.TotalBytes > 0 ? 100.0 * update.TotalDone / update.TotalBytes : 0).ToString("F0") + "%"); },
                    delegate(CopyFailure failure) { return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate { var error = new CopyErrorDialog(this, failure); error.ShowDialog(); return error.Choice; })); }).Run(files);
                if (result.Cancelled) throw new OperationCanceledException();
                if (result.Errors.Count > 0) throw new IOException(String.Join("\n", result.Errors));
            }
        }
        async void OpenArchiveFile(FilePanel panel, Entry entry, bool edit, bool viewOnly = true)
        {
            var document = panel.Archive; if (document == null || entry.IsDirectory) return;
            await OpenArchiveFileCore(panel, entry, edit, document, viewOnly);
        }
        async Task OpenArchiveFileCore(FilePanel panel, Entry entry, bool edit, ArchiveDocument document, bool viewOnly)
        {
            string member = entry.FullPath.TrimStart('/'); if (!ArchivePassword(document, new[] { member })) return;
            var temp = new ArchiveTemp(); bool transferred = false;
            try
            {
                if (!await ArchiveOperation(L.Text("archive.extractFileTitle"), delegate(CancellationToken token, Action<string> progress) { ArchiveEngine.Extract(document, new[] { member }, temp.Path, token, progress); }, delegate { var again = OpenArchiveFileCore(panel, entry, edit, document, viewOnly); })) return;
                string file = ArchiveEngine.DiskPath(temp.Path, member);
                if (!edit && ArchiveEngine.Recognized(file))
                { transferred = true; await panel.OpenArchive(file, document, member, temp); return; }
                var viewer = new Window(); CopyUi.Style(viewer, this, edit ? L.Text("archive.editFileTitle") : L.Text("archive.extractedFileTitle"), 530);
                var body = new StackPanel { Margin = new Thickness(10) }; viewer.Content = body;
                body.Children.Add(CopyUi.Label(document.Display + "\\" + member.Replace('/', '\\')));
                body.Children.Add(CopyUi.Label(L.Text("archive.editTemporaryFileHelp")));
                var buttons = new UniformGrid { Columns = 3 }; body.Children.Add(buttons);
                Action launch = delegate { try {
                    if (!edit && !viewOnly && AttachmentSecurity.RequiresLaunchConfirmation(file) &&
                        MessageBox.Show(viewer, L.Text("security.archiveLaunchWarning") + "\n\n" + member, L.Text("security.archiveLaunchTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                    if (edit || viewOnly) FileViewers.Open(viewer, file, edit); else Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                } catch (Exception ex) { MessageBox.Show(viewer, ex.Message, L.Text("error.cannotOpenFile")); } };
                buttons.Children.Add(CopyUi.Button(edit ? L.Text("action.openEditor") : viewOnly ? L.Text("action.viewFile") : L.Text("action.openFile"), launch));
                bool saving = false; DateTime saved = File.GetLastWriteTimeUtc(file);
                var save = CopyUi.Button(L.Text("archive.saveEditedFile"), async delegate
                {
                    if (saving) return; saving = true; buttons.IsEnabled = false;
                    try
                    {
                        using (var input = new ArchiveTemp())
                        {
                            string staged = ArchiveEngine.DiskPath(input.Path, member); Directory.CreateDirectory(Path.GetDirectoryName(staged)); File.Copy(file, staged);
                            DateTime savedVersion = File.GetLastWriteTimeUtc(staged);
                            if (await ArchiveOperation(L.Text("archive.savingEditedFile"), delegate(CancellationToken token, Action<string> progress)
                            { using (var retryInput = new ArchiveTemp()) { string retryFile = ArchiveEngine.DiskPath(retryInput.Path, member); Directory.CreateDirectory(Path.GetDirectoryName(retryFile)); File.Copy(file, retryFile); ArchiveEngine.ReplaceMember(document, member, retryFile, ArchiveSettings.Load(), token, progress); } }))
                            { saved = savedVersion; RefreshArchivePanels(document); }
                        }
                    }
                    finally { saving = false; buttons.IsEnabled = true; }
                }); save.IsEnabled = ArchiveEngine.Writable(document); buttons.Children.Add(save);
                buttons.Children.Add(CopyUi.Button(L.Text("action.close"), viewer.Close));
                viewer.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
                {
                    if (saving) { e.Cancel = true; return; }
                    if (File.Exists(file) && File.GetLastWriteTimeUtc(file) != saved && MessageBox.Show(viewer, L.Text("archive.confirmDiscardChanges"), L.Text("file.changedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) e.Cancel = true;
                };
                archiveEditors.Add(viewer, delegate { return File.Exists(file) && File.GetLastWriteTimeUtc(file) != saved; });
                viewer.Closed += delegate { archiveEditors.Remove(viewer); try { temp.Dispose(); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
                viewer.Show(); transferred = true; launch();
            }
            finally { if (!transferred) temp.Dispose(); }
        }
        async void ArchiveDelete()
        {
            var document = active.Archive; var members = active.Selected().Select(x => x.FullPath.TrimStart('/')).ToArray();
            if (members.Length == 0) return;
            if (!ArchivePassword(document, document.Items.Select(x => x.Path))) return;
            var confirmation = new DeleteConfirmationDialog(this, active.Selected().Where(x => !x.IsParent).ToList(), false, true, active.DisplayPath, L.Text("delete.archiveRecycleUnavailable"));
            if (confirmation.ShowDialog() != true) return;
            if (await ArchiveOperation(L.Text("archive.deletingTitle"), delegate(CancellationToken token, Action<string> progress) { ArchiveEngine.Remove(document, members, ArchiveSettings.Load(), token, progress); })) RefreshArchivePanels(document);
        }
        async void ArchiveNewFolder()
        {
            var document = active.Archive; string prefix = active.ArchivePrefix;
            if (!ArchivePassword(document, document.Items.Select(x => x.Path))) return;
            string name = Ask(L.Text("archive.newFolderTitle"), L.Text("folder.nameLabel"), ""); if (name == null) return;
            try
            {
                FileOperations.ValidateName(name);
                using (var temp = new ArchiveTemp())
                {
                    string folder = Directory.CreateDirectory(Path.Combine(temp.Path, name)).FullName;
                    if (await ArchiveOperation(L.Text("archive.newFolderTitle"), delegate(CancellationToken token, Action<string> progress) { using (var retryInput = new ArchiveTemp()) { string retryFolder = Directory.CreateDirectory(Path.Combine(retryInput.Path, name)).FullName; ArchiveEngine.Add(document, prefix, new[] { retryFolder }, false, ArchiveSettings.Load(), token, progress); } })) RefreshArchivePanels(document);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("archive.newFolderTitle")); }
        }
        async void ArchiveRename()
        {
            var document = active.Archive; var item = active.List.SelectedItem as Entry; if (item == null || item.IsParent) return;
            string member = item.FullPath.TrimStart('/'); string name = Ask(L.Text("archive.renamePrompt"), L.Text("rename.newNameLabel"), Path.GetFileName(member), !item.IsDirectory); if (name == null) return;
            if (!ArchivePassword(document, new[] { member })) return;
            if (await ArchiveOperation(L.Text("archive.renamingTitle"), delegate(CancellationToken token, Action<string> progress) { ArchiveEngine.Rename(document, member, name, ArchiveSettings.Load(), token, progress); })) RefreshArchivePanels(document);
        }
        async void CreateArchive()
        {
            if (active.IsArchive) { ArchiveTransfer(); return; }
            if (active.IsRemote) { MessageBox.Show(this, L.Text("archive.downloadBeforeCreating"), L.Text("archive.packFilesTitle")); return; }
            var source = active; var files = source.Selected().Select(x => x.FullPath).ToArray(); if (files.Length == 0) return;
            var destination = source == left ? right : left;
            if (destination.IsProjection || destination.IsArchive || destination.IsRemote) { MessageBox.Show(this, L.Text("archive.localDestinationRequired"), L.Text("action.compressArchive")); return; }
            var dialog = new ArchivePackDialog(this, files.Length, DefaultArchiveTarget(files, source.CurrentPath, destination.CurrentPath));
            if (dialog.ShowDialog() != true) return;
            var options = dialog.Options;
            string[] targets = options.Separate ? files.Select(x => Path.Combine(Path.GetDirectoryName(options.Target), Path.GetFileName(x.TrimEnd('\\', '/')) + ArchiveEngine.OutputSuffix(options.Target))).ToArray() : new[] { options.Target };
            if (targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length) { MessageBox.Show(this, L.Text("archive.duplicateOutputNames"), L.Text("action.compressArchive")); return; }
            if (targets.Any(target => files.Any(x => String.Equals(x, target, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(x) && target.StartsWith(x.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))))) { MessageBox.Show(this, L.Text("archive.outputInsideInputRejected"), L.Text("action.compressArchive")); return; }
            var existing = targets.Where(File.Exists).ToArray();
            if (existing.Length > 0 && MessageBox.Show(this, L.Text("archive.confirmReplacePrefix") + String.Join("\n", existing.Take(5)), L.Text("action.compressArchive"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var settings = ArchiveSettings.Load();
            for (int i = 0; i < targets.Length; i++)
            {
                string archiveTarget = targets[i]; string[] archiveSources = options.Separate ? new[] { files[i] } : files;
                if (!await ArchiveOperation(L.Text("archive.creatingPrefix") + archiveTarget, delegate(CancellationToken token, Action<string> progress) { ArchiveEngine.Create(archiveTarget, archiveSources, settings, token, progress, options); })) break;
                foreach (var panel in new[] { left, right }) if (!panel.IsArchive && !panel.IsRemote && (panel.CurrentPath == Path.GetDirectoryName(archiveTarget) || options.Move)) panel.Refresh();
            }
        }
        internal static string DefaultArchiveTarget(string[] files, string source, string destination)
        {
            string name = files.Length == 1 ? (Directory.Exists(files[0]) ? Path.GetFileName(files[0].TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(files[0])) : Path.GetFileName(source.TrimEnd('\\', '/'));
            if (String.IsNullOrWhiteSpace(name)) name = L.Text("archive.shortTitle");
            return Path.Combine(destination, name + ".zip");
        }
    }
}



