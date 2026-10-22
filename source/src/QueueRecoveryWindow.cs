using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        DurableQueue durableQueue; Window recoveryWindow; bool preserveQueueOnExit;
        void AttachDurable(NetworkTransferJob job,DurableTransfer transfer)
        {
            lock (job.DurableRequests) job.DurableRequests.Add(transfer);
            job.PersistDone = delegate(NetworkQueueItem item) { DurableTransfer[] values; lock (job.DurableRequests) values = job.DurableRequests.ToArray(); foreach (var value in values) durableQueue.Done(value,item.Source,item.Destination); };
        }
        void PersistPrepared(DurableTransfer transfer,IEnumerable<NetworkCopyBatch> batches)
        { durableQueue.Plan(transfer,batches.SelectMany(x => x.Items).Select(x => new DurableFile { Source = x.Source,Target = x.Destination,Directory = x.Directory,Bytes = x.Bytes / (x.Relay ? 2 : 1),Modified = x.Modified.ToUniversalTime() })); }
        List<NetworkCopyBatch> RecoveredBatches(DurableTransfer transfer)
        {
            var batch = new NetworkCopyBatch { Entry = new Entry { FullPath = transfer.Sources.FirstOrDefault() ?? "",IsDirectory = true },Destination = transfer.Destination,Rule = transfer.Rule };
            foreach (var file in transfer.Files.Where(x => !x.Done).OrderByDescending(x => x.Directory).ThenBy(x => x.Source.Length)) {
                long bytes = checked(file.Bytes * (transfer.Kind == "Relay" ? 2 : 1)); batch.Bytes = checked(batch.Bytes + bytes);
                batch.Items.Add(new NetworkQueueItem { Source = file.Source,Destination = file.Target,Directory = file.Directory,Bytes = bytes,Modified = file.Modified.ToLocalTime(),Relay = transfer.Kind == "Relay",State = L.Code("state.queued"),Policy = L.Text("action.refresh"),ResumeIfAvailable = true });
            } return new List<NetworkCopyBatch> { batch };
        }
        void FinishDurable(NetworkTransferJob job)
        {
            DurableTransfer[] values; lock (job.DurableRequests) values = job.DurableRequests.ToArray();
            bool success = job.SnapshotItems().All(x => x.State == L.Code("state.completed") || x.State == L.Code("state.skipped"));
            foreach (var value in values) durableQueue.Finish(value,success || (job.Cancellation.IsCancellationRequested && !preserveQueueOnExit && !job.KeepPartialOnCancel));
        }
        internal void ShowDurableQueue()
        {
            if (recoveryWindow != null) { recoveryWindow.Activate(); return; }
            var window = recoveryWindow = new Window(); CopyUi.Style(window,this,L.Text("queue.restoreTitle"),920); window.Height = 570; window.ResizeMode = ResizeMode.CanResize; window.SizeToContent = SizeToContent.Manual;
            var body = new DockPanel { Margin = new Thickness(12) }; window.Content = body;
            var explanation = CopyUi.Label(L.Text("queue.restoreSafetyHelp")); DockPanel.SetDock(explanation,Dock.Top); body.Children.Add(explanation);
            var status = CopyUi.Label(durableQueue.LastError ?? L.Text("queue.restoreCredentialsHelp")); DockPanel.SetDock(status,Dock.Bottom); body.Children.Add(status);
            var buttons = FluentTheme.Actions(); DockPanel.SetDock(buttons,Dock.Bottom); body.Children.Add(buttons);
            var details = new TextBox { Height = 140,IsReadOnly = true,AcceptsReturn = true,TextWrapping = TextWrapping.Wrap,VerticalScrollBarVisibility = ScrollBarVisibility.Auto,Margin = new Thickness(0,8,0,8) }; DockPanel.SetDock(details,Dock.Bottom); body.Children.Add(details);
            var list = new ListBox { Background = System.Windows.Media.Brushes.White }; body.Children.Add(new Border { Background = System.Windows.Media.Brushes.White,BorderBrush = CompactTheme.Line,BorderThickness = new Thickness(1),Child = list });
            Action refresh = delegate { list.ItemsSource = durableQueue.Pending(); if (list.Items.Count > 0) list.SelectedIndex = 0; };
            list.SelectionChanged += delegate { var item = list.SelectedItem as DurableTransfer; details.Text = item == null ? L.Text("queue.noSavedJobs") : L.Text("queue.jobTypePrefix") + item.Kind + L.Text("queue.targetPrefix") + item.Destination + L.Text("queue.conflictRulePrefix") + (item.Copy == null ? item.Rule.ToString() : L.Text("conflict.ask")) + "\nZdroje:\n" + String.Join("\n",item.Sources ?? new string[0]) + L.Text("queue.completedFilesPrefix") + item.Files.Count(x => !x.Directory && x.Done) + " / " + item.Files.Count(x => !x.Directory); };
            buttons.Children.Add(CopyUi.Button(L.Text("queue.resumeSelected"),async delegate { var selected = list.SelectedItem as DurableTransfer; if (selected == null) return; buttons.IsEnabled = false; try { await RestoreTransfer(selected); status.Text = L.Text("queue.jobSubmitted"); } catch (Exception ex) { selected.Running = false; status.Text = ex.Message; status.ToolTip = ex.ToString(); } finally { buttons.IsEnabled = true; refresh(); } }));
            buttons.Children.Add(CopyUi.Button(L.Text("queue.removeItem"),delegate { var selected = list.SelectedItem as DurableTransfer; if (selected != null) { durableQueue.Remove(selected); refresh(); } }));
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"),window.Close)); window.Closed += delegate { recoveryWindow = null; }; refresh(); window.Show();
        }
        internal async Task RestoreTransfer(DurableTransfer transfer)
        {
            if (transfer.Running) throw new IOException(L.Text("queue.jobAlreadyRunning"));
            if (transfer.Files.Count > 0 && transfer.Files.All(x => x.Done)) { durableQueue.Remove(transfer); return; }
            if (transfer.Kind == "Local") {
                var options = transfer.Copy.Clone(); options.Rule = CopyRule.Ask;
                var files = transfer.Files.Where(x => !x.Done).ToArray(); Dictionary<string,string> targets = null; string[] sources = transfer.Sources;
                if (transfer.Files.Count > 0) {
                    await Task.Run(delegate { foreach (var file in files.Where(x => !x.Directory)) if (!File.Exists(file.Source) || (File.GetAttributes(file.Source) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file.Source).Length != file.Bytes || File.GetLastWriteTimeUtc(file.Source) != file.Modified) throw new IOException(L.Text("queue.sourceChangedOrMissingPrefix") + file.Source); });
                    foreach (var directory in files.Where(x => x.Directory && !Directory.Exists(x.Source))) durableQueue.Done(transfer,directory.Source,directory.Target);
                    targets = files.Where(x => !x.Done).OrderByDescending(x => x.Directory).ThenBy(x => x.Source.Length).ToDictionary(x => x.Source,x => x.Target,StringComparer.OrdinalIgnoreCase); sources = targets.Keys.ToArray();
                    if (sources.Length == 0) { durableQueue.Remove(transfer); return; }
                }
                transfer.Running = true; EnqueueCopy(new CopyJob { Sources = sources,Options = options,Durable = transfer,RecoveryTargets = targets }); if (!copyRunning) RunCopyQueue(); return;
            }
            RemoteConnection source = null,target = null;
            try {
                if (transfer.SourceProfileId != null) source = await RecoveryConnection(transfer.SourceProfileId,transfer.SourceIdentity);
                if (transfer.TargetProfileId != null) target = await RecoveryConnection(transfer.TargetProfileId,transfer.TargetIdentity);
                var entries = transfer.Sources.Select(x => new Entry { FullPath = x }).ToArray(); transfer.Running = true;
                if (transfer.Kind == "Relay") await StartNetworkRelay(entries,source,target,transfer.Destination,null,transfer.Rule,transfer.Move,transfer);
                else await StartNetworkCopy(entries,source ?? target,transfer.Kind == "Download",transfer.Destination,null,transfer.Rule,transfer.Move,transfer);
            } finally { if (source != null) source.Dispose(); if (target != null) target.Dispose(); }
        }
        async Task<RemoteConnection> RecoveryConnection(string id,string identity)
        {
            if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath);
            var profile = id!=null&&id.StartsWith("cloud-")?CloudRuntime.SavedProfile(id.Substring(6)):networkStore.Settings.Connections.FirstOrDefault(x => x.Id == id);
            if (profile == null || DurableQueue.Identity(profile) != identity) throw new IOException(L.Text("queue.connectionProfileChanged"));
            var browsing = new[] { left.Remote,right.Remote }.FirstOrDefault(x => x != null && DurableQueue.Identity(x.Profile) == identity); if (browsing != null) return browsing.TransferConnection();
            string password = NetworkSettingsStore.Unprotect(profile.EncryptedPassword); bool remember = false;
            if (String.IsNullOrEmpty(password) && !(profile.IsSshTransfer && (profile.SshAgent || !String.IsNullOrEmpty(profile.PrivateKeyPath))) && !(profile.Protocol == NetworkProtocol.FTP && String.IsNullOrWhiteSpace(profile.UserName))) { password = RequestPassword(profile,out remember); if (password == null) throw new OperationCanceledException(L.Text("queue.restoreCancelled")); }
            var connection = new RemoteConnection(profile); try {
                bool retryPassword = false;
                try { await Task.Run(delegate { connection.Open(connection.Options(password)); }); }
                catch (CommanderCZ.NetworkTransport.SessionRemoteException ex) {
                    if (!profile.IsSshTransfer || !String.IsNullOrEmpty(password) || ex.ToString().IndexOf(L.Text("network.authenticationRejectedMatch"),StringComparison.OrdinalIgnoreCase) < 0) throw;
                    retryPassword = true;
                }
                if (retryPassword) { connection.Dispose(); password = RequestPassword(profile,out remember); if (password == null) throw new OperationCanceledException(L.Text("queue.restoreCancelled")); connection = new RemoteConnection(profile); await Task.Run(delegate { connection.Open(connection.Options(password)); }); }
                if (remember) { profile.EncryptedPassword = NetworkSettingsStore.Protect(password); networkStore.Put(profile); } return connection;
            } catch { connection.Dispose(); throw; }
        }
        async void SaveQueueAndExit()
        {
            if (fileToolsRunning > 0 || archiveOperations > 0 || deletions > 0 || busy) { MessageBox.Show(this,L.Text("queue.waitForOtherOperations"),L.Text("queue.saveTitle")); return; }
            preserveQueueOnExit = true; IsEnabled = false;
            try {
                foreach (var job in copyJobs) if (job.Durable != null) durableQueue.Finish(job.Durable,false); copyJobs.Clear(); if (copyControl != null) copyControl.Cancel(); foreach (var job in networkJobs.Where(x => !x.Finished)) { job.KeepPartialOnCancel = true; job.Cancel(); }
                while (copyRunning || networkCopies > 0 || networkJobs.Any(x => x.Planning > 0)) await Task.Delay(100);
                Close();
            } finally { preserveQueueOnExit = false; IsEnabled = true; }
        }
    }
}

