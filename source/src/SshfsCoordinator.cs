using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FileCompass.NetworkTransport;

namespace FileCompass
{
    public partial class MainWindow
    {
        readonly HashSet<string> sshfsConnecting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal ContextMenu SshfsDriveMenu(string root, string remote)
        {
            if (remote == null || !remote.StartsWith("\\\\sshfs", StringComparison.OrdinalIgnoreCase)) return null;
            var menu = new ContextMenu();
            NetworkProfile profile = null;
            try { if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath); profile = networkStore.Settings.Connections.FirstOrDefault(x => x.Protocol == NetworkProtocol.SSHFS && x.DriveLetter + "\\" == root && SshfsMount.SameRemote(SshfsMount.Remote(x), remote)); } catch (IOException) { } catch (InvalidOperationException) { }
            var disconnect = new MenuItem { Header = L.Text("action.disconnect") }; menu.Items.Add(disconnect);
            disconnect.Click += async delegate {
                if (profile != null) { await DisconnectSshfs(profile); return; }
                if (copyRunning || networkCopies > 0 || fileToolsRunning > 0 || archiveOperations > 0 || deletions > 0) { MessageBox.Show(this, L.Text("sshfs.waitBeforeDisconnecting"), "SSHFS"); return; }
                fileToolsRunning++;
                try {
                    foreach (var view in new[] { left, right }) if (view.CurrentPath != null && view.CurrentPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { view.Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), false); await view.NavigationTask; }
                    await Task.Run(delegate { string current; if (!FilePanel.RememberedDrives().TryGetValue(root, out current) || !String.Equals(current, remote, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("sshfs.driveMappingChanged")); WindowsNetwork.DisconnectMappedDrive(root.Substring(0, 2)); });
                    left.RefreshDriveBar(); right.RefreshDriveBar();
                } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("sshfs.disconnectFailedTitle")); } finally { fileToolsRunning--; }
            };
            var reconnect = new MenuItem { Header = L.Text("action.reconnect"), IsEnabled = profile != null }; menu.Items.Add(reconnect);
            reconnect.Click += async delegate { await DisconnectSshfs(profile); if (!await Task.Run(delegate { return SshfsMount.Connected(profile); })) await ConnectSshfs(profile, active, false); };
            var edit = new MenuItem { Header = L.Text("network.editConnectionMenu"), IsEnabled = profile != null }; menu.Items.Add(edit);
            edit.Click += delegate { var editor = new NetworkProfileDialog(this, profile); if (editor.ShowDialog() == true) try { networkStore.Put(editor.Profile); } catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("sshfs.configurationTitle")); } };
            var terminal = new MenuItem { Header = L.Text("ssh.terminalTitle"), IsEnabled = profile != null }; menu.Items.Add(terminal); terminal.Click += delegate { OpenSshTerminal(profile); };
            return menu;
        }
        async Task ConnectSshfs(NetworkProfile source, FilePanel panel, bool startup)
        {
            var profile = source.Clone();
            if (!sshfsConnecting.Add(profile.DriveLetter ?? "")) return;
            fileToolsRunning++;
            try
            {
                operation.Visibility = Visibility.Visible;
                profile.Validate();
                if (busy || panel.IsLoading || panel.NfsDrive != null) throw new IOException(L.Text("sshfs.waitBeforeConnecting"));
                string root;
                if (await Task.Run(delegate { return SshfsMount.Connected(profile); })) root = profile.DriveLetter + "\\";
                else
                {
                    if (SshfsSetup.MissingPackages() != 0)
                    {
                        if (startup) throw new IOException(L.Text("sshfs.installBeforeStartupMount"));
                        if (!await SshfsSetupWindow.Open(this)) return;
                        if (SshfsSetup.MissingPackages() != 0) throw new IOException(SshfsMount.DependencyStatus());
                    }
                    if (startup && (String.IsNullOrEmpty(profile.HostKeyFingerprint) || (String.IsNullOrEmpty(profile.PrivateKeyPath) && String.IsNullOrEmpty(profile.EncryptedPassword)))) throw new IOException(L.Text("sshfs.startupCredentialsRequired"));
                    operation.Text = L.Text("sshfs.verifyingServerPrefix") + profile.Host;
                    Tuple<string, string> key;
                    using (var probe = new Session())
                    {
                        var options = new SessionOptions { Protocol = Protocol.Sftp, HostName = profile.Host, PortNumber = profile.Port, UserName = profile.UserName, Timeout = TimeSpan.FromSeconds(profile.TimeoutSeconds), SshHostKeyFingerprint = profile.HostKeyFingerprint };
                        key = await Task.Run(delegate { return probe.ScanSshfsKey(options); });
                    }
                    if (String.IsNullOrEmpty(profile.HostKeyFingerprint))
                    {
                        if (MessageBox.Show(this, L.Text("network.serverPrefix") + profile.Host + ":" + profile.Port + L.Text("ssh.hostKeyFingerprintPrefix") + key.Item1 + L.Text("sshfs.confirmTrustFingerprintSuffix"), L.Text("sshfs.firstConnectionTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                        profile.HostKeyFingerprint = key.Item1;
                    }
                    if (!NativeSsh.Trusted(profile.HostKeyFingerprint, Convert.FromBase64String(key.Item2.Split(' ')[1]))) throw new IOException(L.Text("sshfs.hostKeyMismatchRejected"));
                    string password = NetworkSettingsStore.Unprotect(profile.EncryptedPassword); bool remember = false;
                    if (String.IsNullOrEmpty(profile.PrivateKeyPath) && String.IsNullOrEmpty(profile.EncryptedPassword)) { password = RequestPassword(profile, out remember); if (password == null) return; }
                    operation.Text = L.Text("sshfs.connectingPrefix") + profile.Name + L.Text("transfer.destinationNameSeparator") + profile.DriveLetter;
                    try{using(ToolTrust.AcquireSshfs(SshfsMount.Executable())){}}
                    catch(IOException){if(startup)throw;if(MessageBox.Show(this,L.Text("security.sshfsTrustWarning")+"\n\n"+SshfsMount.Executable(),"SSHFS",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;ToolTrust.ApproveSshfs(SshfsMount.Executable());}
                    root = await Task.Run(delegate { return SshfsMount.Mount(profile, password, key.Item2); });
                    if (remember || source.HostKeyFingerprint != profile.HostKeyFingerprint)
                    {
                        if (remember) profile.EncryptedPassword = NetworkSettingsStore.Protect(password);
                        if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath);
                        try { networkStore.Put(profile); } catch (Exception ex) { if (!startup) MessageBox.Show(this, L.Text("sshfs.mountedProfileSaveFailedPrefix") + ex.Message, L.Text("sshfs.configurationTitle")); }
                    }
                }
                if (windowClosed) return;
                foreach (var view in new[] { left, right }) view.RefreshDriveBar();
                // Startup connects all saved drives without replacing the saved panel folders.
                if (!startup) { panel.Navigate(root, true); await panel.NavigationTask; panel.SetLocalNetwork(profile, root); }
                else foreach (var view in new[] { left, right }) if (view.CurrentPath != null && view.CurrentPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) view.SetLocalNetwork(profile, root);
                operation.Text = L.Text("network.connectedPrefix") + profile.Name + " · " + profile.DriveLetter;
                if (!startup && !String.IsNullOrWhiteSpace(profile.LocalPath)) { var other = panel == left ? right : left; if (!other.IsLoading && other.NfsDrive == null) other.Navigate(profile.LocalPath, true); }
            }
            catch (Exception ex) { operation.Text = "SSHFS " + profile.Name + ": " + ex.Message; if (!startup && !windowClosed) MessageBox.Show(this, ex.Message, L.Text("sshfs.connectionFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { fileToolsRunning--; sshfsConnecting.Remove(profile.DriveLetter ?? ""); }
        }
        async Task RestoreSshfsConnections(ProgramSettings preferences)
        {
            try
            {
                if (networkStore == null) networkStore = new NetworkSettingsStore(NetworkSettingsStore.DefaultPath);
                foreach (var p in networkStore.Settings.Connections.Where(x => x.Protocol == NetworkProtocol.SSHFS && x.SshfsConnectOnStartup).ToArray())
                {
                    if (windowClosed) break;
                    string beforeLeft = left.CurrentPath, beforeRight = right.CurrentPath;
                    await ConnectSshfs(p, left, true);
                    if (!await Task.Run(delegate { return SshfsMount.Connected(p); })) continue;
                    foreach (var view in new[] { left, right })
                    {
                        string desired = view == left ? preferences.LeftFolder : preferences.RightFolder, before = view == left ? beforeLeft : beforeRight;
                        if (view.CurrentPath == before && desired != null && desired.StartsWith(p.DriveLetter + "\\", StringComparison.OrdinalIgnoreCase)) { view.Navigate(desired, false); await view.NavigationTask; view.SetLocalNetwork(p, p.DriveLetter + "\\"); }
                    }
                }
            }
            catch (Exception ex) { operation.Text = L.Text("sshfs.restoreStatusPrefix") + ex.Message; }
        }
        async Task DisconnectSshfs(NetworkProfile profile)
        {
            if (copyRunning || networkCopies > 0 || fileToolsRunning > 0 || archiveOperations > 0 || deletions > 0) { MessageBox.Show(this, L.Text("sshfs.waitForTransfersBeforeDisconnect"), "SSHFS"); return; }
            fileToolsRunning++;
            try
            {
                foreach (var view in new[] { left, right }) if (view.CurrentPath != null && view.CurrentPath.StartsWith(profile.DriveLetter + "\\", StringComparison.OrdinalIgnoreCase)) { view.Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), false); await view.NavigationTask; }
                await Task.Run(delegate { SshfsMount.Disconnect(profile); });
                foreach (var view in new[] { left, right }) { if (view.LocalNetworkProfile != null && view.LocalNetworkProfile.Id == profile.Id) view.SetLocalNetwork(null, null); view.RefreshDriveBar(); }
                operation.Text = L.Text("network.disconnectedPrefix") + profile.Name;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("sshfs.disconnectFailedTitle")); }
            finally { fileToolsRunning--; }
        }
    }
}

