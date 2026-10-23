using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    public partial class MainWindow
    {
        NetworkSettingsStore networkStore;
        internal NetworkSettingsStore ConnectionSettings { get { return networkStore ?? (networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath)); } }
        bool PanelsLoading { get { return (left != null && left.IsLoading) || (right != null && right.IsLoading); } }
        void UpdateNetworkActions()
        {
            actions.ForEach(b => b.IsEnabled = !busy && !PanelsLoading);
            commandLine.IsEnabled = !busy && !PanelsLoading && (active == null || !active.IsShellBrowser);
        }
        void ManageConnections(NetworkProtocol? filter = null, Window dialogOwner = null, bool cloudSelected = false)
        {
            if (busy || PanelsLoading) return;
            try
            {
                var dialog = new ConnectionsDialog(dialogOwner ?? this, this, ConnectionSettings, filter, cloudSelected);
                if (dialog.ShowDialog() == true)
                {
                    if (dialog.OpenSsh || dialog.ConnectProfile.Protocol == NetworkProtocol.SSH) OpenSshTerminal(dialog.ConnectProfile);
                    else ConnectNetwork(dialog.ConnectProfile);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message + L.Text("ssh.configurationPathPrefix") + NetworkSettingsStore.DefaultPath, L.Text("network.connectionsTitle"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        void OpenSshTerminal(NetworkProfile profile)
        {
            try { SshTerminal.Open(profile); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("ssh.terminalOpenFailed"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        void OpenActiveSshTerminal()
        {
            if (active.IsRemote && active.Remote.Profile.IsSshTransfer) OpenSshTerminal(active.Remote.Profile);
            else if (active.IsAtLocalNetwork && active.LocalNetworkProfile.CanOpenSsh) OpenSshTerminal(active.LocalNetworkProfile);
            else ManageConnections(NetworkProtocol.SSH);
        }
        string RequestPassword(NetworkProfile profile, out bool remember)
        {
            var window = new Window(); CopyUi.Style(window, this, L.Text("network.loginTitlePrefix") + profile.Name, 440);
            var panel = new StackPanel { Margin = new Thickness(12) }; window.Content = panel;
            panel.Children.Add(CopyUi.Label(L.Text("network.passwordForPrefix") + profile.UserName + "@" + profile.Host + ":"));
            var input = new PasswordBox { Margin = new Thickness(2, 4, 2, 8), MinHeight = 24 }; panel.Children.Add(input);
            var save = new CheckBox { Content = L.Text("network.rememberConnectionPassword"), Margin = new Thickness(2, 0, 2, 10) }; panel.Children.Add(save);
            var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var ok = CopyUi.Button(L.Text("action.append"), delegate { window.DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { window.DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel); panel.Children.Add(buttons);
            window.Loaded += delegate { input.Focus(); };
            bool accepted = window.ShowDialog() == true;
            remember = accepted && save.IsChecked == true;
            return accepted ? input.Password : null;
        }
        void NetworkBusy(bool value, string text)
        {
            busy = value; actions.ForEach(b => b.IsEnabled = !value && !PanelsLoading);
            left.IsEnabled = right.IsEnabled = !value; commandLine.IsEnabled = !value;
            if (text != null) { operation.Visibility = Visibility.Visible; operation.Text = text; }
            progress.IsIndeterminate = value; progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
        async void ConnectNetwork(NetworkProfile source) { await ConnectNetworkTask(source); }
        async Task ConnectNetworkTask(NetworkProfile source)
        {
            if (source.Protocol == NetworkProtocol.SSH) { OpenSshTerminal(source); return; }
            if (source.Protocol == NetworkProtocol.SSHFS) { await ConnectSshfs(source, active, false); return; }
            if (busy || PanelsLoading) { MessageBox.Show(this, L.Text("network.waitForFileOperations"), L.Text("network.connectionsTitle")); return; }
            var panel = active; var profile = source.Clone(); RemoteConnection connection = null; string nfsDrive = null;
            try
            {
                profile.Validate();
                if (panel.NfsDrive != null) throw new IOException(L.Text("nfs.disconnectBeforeConnecting"));
                string password = NetworkSettingsStore.Unprotect(profile.EncryptedPassword);
                bool needsPassword = profile.Protocol != NetworkProtocol.NFS && String.IsNullOrEmpty(profile.EncryptedPassword) &&
                    (profile.Protocol != NetworkProtocol.SMB || !String.IsNullOrEmpty(profile.UserName)) &&
                    (profile.Protocol != NetworkProtocol.FTP || !String.IsNullOrWhiteSpace(profile.UserName)) &&
                    (!profile.IsSshTransfer || (!profile.SshAgent && String.IsNullOrEmpty(profile.PrivateKeyPath)));
                bool rememberPassword = false;
                if (needsPassword) { password = RequestPassword(profile, out rememberPassword); if (password == null) return; }
                NetworkBusy(true, L.Text("network.connectingPrefix") + profile.Name);
                if (profile.Protocol == NetworkProtocol.SMB || profile.Protocol == NetworkProtocol.NFS)
                {
                    string path = await Task.Run(delegate { return profile.Protocol == NetworkProtocol.SMB ? WindowsNetwork.ConnectSmb(profile, password) : WindowsNetwork.ConnectNfs(profile); });
                    if (profile.Protocol == NetworkProtocol.NFS) nfsDrive = profile.DriveLetter;
                    await Task.Run(delegate { new DirectoryInfo(path).GetFileSystemInfos(); });
                    panel.Navigate(path, true); await panel.NavigationTask;
                    if (!String.Equals(panel.CurrentPath, path, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("network.folderOpenFailed"));
                    if (profile.Protocol == NetworkProtocol.NFS) { panel.NfsDrive = nfsDrive; nfsDrive = null; }
                    panel.SetLocalNetwork(profile, path);
                }
                else
                {
                    connection = new RemoteConnection(profile);
                    var options = connection.Options(password);
                    if (profile.IsSshTransfer && String.IsNullOrEmpty(profile.HostKeyFingerprint))
                    {
                        string fingerprint = await Task.Run(delegate { return connection.Session.ScanFingerprint(options, "SHA-256"); });
                        if (MessageBox.Show(this, L.Text("network.serverPrefix") + profile.Host + ":" + profile.Port + L.Text("ssh.hostKeyFingerprintPrefix") + fingerprint +
                            L.Text("ssh.confirmTrustHostKeySuffix"),
                            L.Text("network.firstConnectionPrefix") + profile.Protocol + L.Text("network.connectionSuffix"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                        profile.HostKeyFingerprint = fingerprint; connection.Profile.HostKeyFingerprint = fingerprint; options.SshHostKeyFingerprint = fingerprint;
                        // Trust is saved only after a successful authenticated connection and directory listing.
                    }
                    Exception authenticationFailure = null;
                    try { await Task.Run(delegate { connection.Open(options); }); }
                    catch (SessionRemoteException ex) { authenticationFailure = ex; }
                    if (authenticationFailure != null)
                    {
                        if (!profile.IsSshTransfer || needsPassword || !String.IsNullOrEmpty(password) || authenticationFailure.ToString().IndexOf(L.Text("network.authenticationRejectedMatch"), StringComparison.OrdinalIgnoreCase) < 0) throw authenticationFailure;
                        NetworkBusy(false, null); password = RequestPassword(profile, out rememberPassword); if (password == null) return;
                        connection.Dispose(); connection = new RemoteConnection(profile); options = connection.Options(password);
                        NetworkBusy(true, L.Text("network.connectingPrefix") + profile.Name); await Task.Run(delegate { connection.Open(options); });
                    }
                    await panel.AttachRemote(connection); connection = null;
                }
                if (rememberPassword || source.HostKeyFingerprint != profile.HostKeyFingerprint)
                {
                    try
                    {
                        if (rememberPassword) profile.EncryptedPassword = NetworkSettingsStore.Protect(password);
                        if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath);
                        networkStore.Put(profile);
                    }
                    catch (Exception ex) { MessageBox.Show(this, L.Text("network.connectedSettingsSaveFailedPrefix") + ex.Message, L.Text("network.connectionSettingsTitle")); }
                }
                if (!String.IsNullOrWhiteSpace(profile.LocalPath))
                {
                    var other = panel == left ? right : left;
                    if (!other.IsLoading && other.NfsDrive == null) other.Navigate(profile.LocalPath, true);
                }
                operation.Text = L.Text("network.connectedPrefix") + profile.Name;
            }
            catch (Exception ex)
            {
                operation.Text = L.Text("network.connectionFailedTitle");
                if (nfsDrive != null) { try { WindowsNetwork.UnmountNfs(nfsDrive); } catch { } }
                MessageBox.Show(this, ex.Message, L.Text("network.remoteConnectionFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (connection != null) connection.Dispose();
                NetworkBusy(false, null); panel.List.Focus();
            }
        }
        void DisconnectNetwork() { DisconnectNetworkPanel(active); }
        void DisconnectLocalNetworkPanel(FilePanel panel) { DisconnectNetworkPanel(panel, true); }
        async void DisconnectNetworkPanel(FilePanel panel, bool localOnly = false)
        {
            if (busy || PanelsLoading) return;
            if (panel.IsRemote && !localOnly) { panel.DisconnectRemote(); return; }
            if (panel.LocalNetworkProfile != null && panel.LocalNetworkProfile.Protocol == NetworkProtocol.SSHFS)
            { await DisconnectSshfs(panel.LocalNetworkProfile); return; }
            if (panel.NfsDrive != null)
            {
                string drive = panel.NfsDrive; NetworkBusy(true, L.Text("nfs.disconnectingTitle"));
                try
                {
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (panel.IsAtLocalNetwork) panel.Navigate(local, false);
                    await Task.Run(delegate { WindowsNetwork.UnmountNfs(drive); }); panel.NfsDrive = null; panel.SetLocalNetwork(null, null);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("nfs.disconnectFailedTitle")); }
                finally { NetworkBusy(false, L.Code("state.ready")); }
            }
            else if (panel.LocalNetworkProfile != null)
            {
                var profile = panel.LocalNetworkProfile; NetworkBusy(true, L.Text("smb.disconnectingTitle"));
                try
                {
                    bool returnToLocal = panel.IsAtLocalNetwork;
                    await Task.Run(delegate { WindowsNetwork.DisconnectSmb(profile); });
                    panel.SetLocalNetwork(null, null); if (returnToLocal) panel.Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), false);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("smb.disconnectFailedTitle")); }
                finally { NetworkBusy(false, L.Code("state.ready")); }
            }
            else panel.DisconnectRemote();
        }
        async void NetworkTransfer(bool move)
        { await TransferNetworkEntries(move, active, active == left ? right : left, active.Selected().ToArray()); }
        async Task TransferNetworkEntries(bool move, FilePanel source, FilePanel target, Entry[] entries)
        {
            if (busy || PanelsLoading) return;

            if (entries.Length == 0) return;
            var setup = new NetworkCopyDialog(this, entries.Length, source.DisplayPath, target.DisplayPath, source.Remote == null ? null : source.Remote.Profile, target.Remote == null ? null : target.Remote.Profile,move);
            if (move) setup.Title = L.Text("network.moveTitle");
            if (setup.ShowDialog() != true) return;
            if (source.IsRemote && target.IsRemote) { await StartNetworkRelay(entries,source.Remote,target.Remote,target.CurrentPath,target,setup.Rule,move); if (move) source.Refresh(); return; }
            await StartNetworkCopy(entries.ToArray(), source.Remote ?? target.Remote, source.IsRemote, target.CurrentPath, target, setup.Rule,move);
            if (move) source.Refresh();
        }
        internal async Task<NetworkTransferJob> StartNetworkCopy(Entry[] entries, RemoteConnection browsing, bool download, string destination, FilePanel target = null, NetworkCopyRule rule = NetworkCopyRule.Skip, bool move = false,DurableTransfer recovery = null)
        {
            entries = entries.Select(x => new Entry { FullPath = x.FullPath, Name = x.Name, IsDirectory = x.IsDirectory, Bytes = x.Bytes }).ToArray();
            var durable = recovery ?? durableQueue.Begin(download ? "Download" : "Upload",entries.Select(x => x.FullPath).ToArray(),destination,null,rule,move,download ? browsing.Profile : null,download ? null : browsing.Profile);
            var planner = browsing.TransferConnection();
            // An explicit resume must not disappear into a previous job's root
            // de-duplication set after an individual item was interrupted.
            var job = recovery == null && rule != NetworkCopyRule.Resume ? networkJobs.AsEnumerable().Reverse().FirstOrDefault(x => x.Move == move && x.Matches(browsing, download) && x.Reserve()) : null;
            if (job == null)
            {
                job = new NetworkTransferJob(browsing.Profile, download) { Move = move }; job.Reserve(); networkJobs.Add(job);
                networkCopies++;
                var captured = job;
                job.ResolveError = delegate(CopyFailure failure)
                {
                    captured.Cancellation.Token.ThrowIfCancellationRequested();
                    return Dispatcher.Invoke(new Func<CopyErrorChoice>(delegate
                    { var error = new CopyErrorDialog(this, failure); error.ShowDialog(); return error.Choice; }));
                };
                job.Activity = AddCopyActivity((move ? L.Text("network.moveTitlePrefix") : L.Text("network.copyTitlePrefix")) + job.Profile.Protocol + " · " + job.Profile.Name, job.Cancel, delegate { ShowNetworkQueue(captured); });
                job.Activity.Record.RetryInterrupted = true;
                job.Activity.Record.Retry = async delegate
                {
                    var failedItems = captured.SnapshotItems().Where(x => x.State.StartsWith(L.Code("state.error")) || x.State == L.Code("state.cancelled") || x.State == L.Code("state.interrupted")).ToArray();
                    foreach (var failed in failedItems.Where(x => !failedItems.Any(d => d.Directory && d.Source != x.Source && x.Source.StartsWith(d.Source.TrimEnd('/', '\\') + (captured.Download ? "/" : "\\"), captured.Download ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))))
                    {
                        string parent = captured.Download ? System.IO.Path.GetDirectoryName(failed.Destination) : RemoteConnection.Parent(failed.Destination);
                        await StartNetworkCopy(new[] { new Entry { FullPath = failed.Source, Name = System.IO.Path.GetFileName(failed.Source), Bytes = failed.Bytes, IsDirectory = failed.Directory } }, browsing, captured.Download, parent, null, NetworkCopyRule.Resume,captured.Move);
                    }
                };
                var updates = new Progress<NetworkCopyUpdate>(delegate(NetworkCopyUpdate update) { captured.Activity.Update(update.Text, update.File, update.Total, update.Preparing); });
                job.Changed = delegate { ((IProgress<NetworkCopyUpdate>)updates).Report(captured.Update()); };
                job.Completion = RunNetworkJob(job, browsing.TransferConnection());
            }
            AttachDurable(job,durable); ShowNetworkQueue(job);
            await job.QueuePlanning(delegate
            {
                Exception planningError = null;
                try
                {
                    using (planner)
                    {
                        var prepared = recovery != null && recovery.Files.Count > 0 ? RecoveredBatches(recovery) : NetworkRetry.Plan(planner,delegate(RemoteConnection worker) { return NetworkCopyWorker.Prepare(worker,entries,download,destination,rule,job.Cancellation.Token); },job);
                        if (recovery == null || recovery.Files.Count == 0) PersistPrepared(durable,prepared);
                        job.Cancellation.Token.ThrowIfCancellationRequested(); job.AddPrepared(prepared);
                    }
                }
                catch (Exception ex) { planningError = ex; }
                finally { job.PlanningDone(planningError, String.Join("; ", entries.Select(x => x.FullPath)), destination, entries, rule); }
            });
            await job.Completion;
            if (target != null && target.CurrentPath == destination && !target.IsLoading && (download ? !target.IsRemote : target.Remote == browsing)) target.Refresh();
            return job;
        }
        async Task RunNetworkJob(NetworkTransferJob job, RemoteConnection connection, RemoteConnection relayTarget = null)
        {
            try
            {
                var result = await Task.Run(delegate { using (connection) using (relayTarget) return relayTarget == null ? NetworkCopyWorker.Run(job, connection) : NetworkRelay.Run(job, connection, relayTarget); });
                bool errors = job.SnapshotItems().Any(x => x.State.StartsWith(L.Code("state.error")));
                job.Finish(errors ? L.Text("state.completedWithErrors") : L.Code("state.completed"));
                job.Activity.Finish((errors ? L.Text("state.completedWithErrors") : L.Code("state.completed")) + " · " + job.Caption + (job.Move ? L.Text("copy.movedCountPrefix") : L.Text("copy.copiedCountPrefix")) + result.Copied + L.Text("copy.skippedCountSeparator") + result.Skipped + " · " + CopyUi.Bytes(job.TotalBytes) + L.Text("queue.clickToOpenSuffix"), !errors);
            }
            catch (OperationCanceledException) {
                job.Cancel(); bool kept = job.RelayTarget == null || job.KeepPartialOnCancel; job.Finish(kept ? L.Code("state.interrupted") : L.Code("state.cancelled"));
                job.Activity.Finish((kept ? L.Text("transfer.interruptedPartialPreserved") : L.Text("transfer.cancelledStatus") + (job.PartialCleanupError == null ? "" : L.Text("transfer.partialCleanupFailedSuffix") + job.PartialCleanupError)) + " · " + job.Caption,false);
                if (kept) job.Activity.Record.State = L.Code("state.interrupted");
            }
            catch (Exception ex) { job.Cancel(); job.Finish(L.Code("error.messagePrefix") + ex.Message); job.Activity.Finish(L.Text("transfer.failedPrefix") + ex.Message + L.Text("queue.clickToOpenSuffix"), false); }
            try { await job.PlanningCompletion; }
            finally
            {
                FinishDurable(job);
                job.Activity.Record.Items.AddRange(job.SnapshotItems().Select(x => new OperationItem { Source = x.Source, Target = x.Destination, Directory = x.Directory, State = x.State.StartsWith(L.Code("state.error")) ? L.Code("state.error") : x.State, Error = x.State.StartsWith(L.Code("state.error")) ? x.State : "" }));
                operationJournal.Save(); networkCopies--;
            }
        }
        async void RemoteNewFolder()
        {
            var panel = active; string name = Ask(L.Text("network.newFolderTitle"), L.Text("folder.nameLabel"), ""); if (name == null) return;
            try
            {
                RemoteConnection.ValidateRemoteName(name); string path = RemoteConnection.Combine(panel.CurrentPath, name);
                NetworkBusy(true, L.Text("network.creatingFolderTitle"));
                await Task.Run(delegate { if (panel.Remote.Session.FileExists(path)) throw new IOException(L.Text("error.itemAlreadyExists")); panel.Remote.Session.CreateDirectory(path); });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("folder.createFailed")); }
            finally { NetworkBusy(false, null); panel.Refresh(); }
        }
        async void RemoteRename()
        {
            var panel = active; var entries = panel.Selected();
            if (entries.Count != 1) { MessageBox.Show(this, L.Text("selection.exactlyOneRequired"), L.Text("action.rename")); return; }
            string name = Ask(L.Text("network.renameItemTitle"), L.Text("rename.newNameLabel"), RemoteConnection.Name(entries[0].FullPath), !entries[0].IsDirectory); if (name == null) return;
            try
            {
                RemoteConnection.ValidateRemoteName(name); string destination = RemoteConnection.Combine(panel.CurrentPath, name);
                NetworkBusy(true, L.Text("rename.operationTitle"));
                await Task.Run(delegate { if (panel.Remote.Session.FileExists(destination)) throw new IOException(L.Text("error.itemAlreadyExists")); panel.Remote.Session.MoveFile(entries[0].FullPath, destination); });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("rename.failed")); }
            finally { NetworkBusy(false, null); panel.Refresh(); }
        }
    }
}



