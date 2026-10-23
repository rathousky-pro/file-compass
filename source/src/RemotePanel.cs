using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    public partial class FilePanel
    {
        public RemoteConnection Remote { get; private set; }
        public bool IsRemote { get { return Remote != null; } }
        public bool IsLoading { get; private set; }
        public string NfsDrive;
        readonly Dictionary<RemoteConnection, string> openConnections = new Dictionary<RemoteConnection, string>();
        NetworkProfile localNetworkProfile;
        string localNetworkPath;
        public event Action<FilePanel> DisconnectLocalNetwork;
        string localReturnPath;
        public string DisplayPath { get { return IsArchive ? Archive.Display + "\\" + (ArchivePrefix ?? "").Replace('/', '\\') : IsRemote ? Remote.Display(CurrentPath) : IsShellBrowser ? ShellLocationName : CurrentPath; } }
        public event EventHandler LoadingChanged;
        void Loading(bool value)
        {
            IsLoading = value; IsEnabled = !value;
            if (LoadingChanged != null) LoadingChanged(this, EventArgs.Empty);
        }
        List<Entry> RemoteRows(RemoteDirectoryInfo directory, string path)
        {
            var rows = new List<Entry>();
            if (path != "/") rows.Add(new Entry { Name = "[..]", FullPath = RemoteConnection.Parent(path), IsDirectory = true, IsParent = true, Icon = DesktopIcons.FileIcon("", true) });
            foreach (var file in directory.Files)
            {
                if (file.Name == "." || file.Name == "..") continue;
                int dot = file.Name.LastIndexOf('.');
                string extension = file.IsDirectory || dot <= 0 ? "" : file.Name.Substring(dot);
                string iconPath = extension.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 ? "file" + extension : "file";
                rows.Add(new Entry { Name = file.IsDirectory ? "[" + file.Name + "]" : extension.Length == 0 ? file.Name : file.Name.Substring(0, dot),
                    FullPath = RemoteConnection.Combine(path, file.Name), Extension = extension.TrimStart('.'), IsDirectory = file.IsDirectory,
                    IsHidden = file.Name.StartsWith(".", StringComparison.Ordinal),
                    Bytes = file.IsDirectory ? 0 : file.Length, ModifiedTime = file.LastWriteTime, Modified = file.LastWriteTime.ToString("dd.MM.yyyy HH:mm"),
                    Attributes = file.FilePermissions == null ? "" : file.FilePermissions.ToString(), Icon = DesktopIcons.FileIcon(iconPath, file.IsDirectory) });
            }
            return rows;
        }
        public async Task AttachRemote(RemoteConnection connection)
        {
            CancelNavigation();
            string path = RemoteConnection.Normalize(connection.Profile.Path);
            var directory = await Task.Run(delegate { return connection.Session.ListDirectory(path); });
            var rows = RemoteRows(directory, path);
            if (!IsRemote) localReturnPath = IsArchive ? archiveReturnPath : CurrentPath;
            RememberRemote();
            var previous = openConnections.Keys.FirstOrDefault(x => x.Profile.Id == connection.Profile.Id && x != connection);
            if (previous != null) { openConnections.Remove(previous); previous.Dispose(); }
            openConnections[connection] = path;
            Remote = connection; history.Clear(); forwardHistory.Clear();
            CloseArchiveContext();
            CurrentPath = path; ApplyRemoteRows(rows, null);
        }
        void ApplyRemoteRows(List<Entry> rows, string cursor)
        {
            CloseArchiveContext();
            synchronizingSelection = true;
            try {
                ShowPath(); List.ItemsSource = rows; Sort(); List.SelectedIndex = rows.Count > 0 ? 0 : -1;
                if (cursor != null) { List.SelectedItem = rows.FirstOrDefault(x => x.FullPath == cursor) ?? List.SelectedItem; if (List.SelectedItem != null) List.ScrollIntoView(List.SelectedItem); }
            } finally { synchronizingSelection = false; }
            SyncWindowsSelection();
            selectingDrive = true; driveSelector.SelectedIndex = -1; selectingDrive = false;
            foreach (var button in driveButtons.Values) button.Background = CompactTheme.WhiteSurface;
            capacity.Text = String.IsNullOrEmpty(Remote.Profile.CloudAccountId)?Remote.Profile.Name+" · "+Remote.Profile.DisplayLabel:Remote.Profile.DisplayLabel;
            capacity.ToolTip = capacity.Text; UpdateStatus();
            RememberRemote(); UpdateConnectionButtons();
            if (PathChanged != null) PathChanged(this, EventArgs.Empty);
        }
        async void NavigateRemote(string path, bool remember)
        {
            if (IsLoading) return;
            Loading(true);
            try
            {
                path = RemoteConnection.Normalize(path);
                string cursor = null; var marks = new HashSet<string>(StringComparer.Ordinal);
                if (CurrentPath != null && path != CurrentPath && RemoteConnection.Parent(CurrentPath) == path) cursor = CurrentPath;
                if (path == CurrentPath)
                {
                    var selected = List.SelectedItem as Entry; cursor = selected == null ? null : selected.FullPath;
                    foreach (Entry item in List.Items) if (item.IsMarked) marks.Add(item.FullPath);
                }
                var info = await Task.Run(delegate { return Remote.Session.ListDirectory(path); });
                var rows = RemoteRows(info, path); foreach (var item in rows) item.IsMarked = marks.Contains(item.FullPath);
                if (remember && path != CurrentPath) { history.Push(CurrentPath); forwardHistory.Clear(); }
                CurrentPath = path; ApplyRemoteRows(rows, cursor);
            }
            catch (Exception ex) { ShowPath(); MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("network.remoteFolderOpenFailedTitle")); }
            finally { Loading(false); }
        }
        public async Task RemoteHistory(bool forward)
        {
            if (IsLoading) return;
            var stack = forward ? forwardHistory : history; if (stack.Count == 0) return;
            Loading(true);
            try
            {
                string path = stack.Peek(), previous = CurrentPath;
                var info = await Task.Run(delegate { return Remote.Session.ListDirectory(path); });
                var rows = RemoteRows(info, path);
                stack.Pop(); (forward ? history : forwardHistory).Push(previous);
                CurrentPath = path; ApplyRemoteRows(rows, RemoteConnection.Parent(previous) == path ? previous : null);
            }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("network.connectionHistoryTitle")); }
            finally { Loading(false); }
        }
        public void DisconnectRemote()
        {
            if (IsLoading) return;
            if (Remote != null) { openConnections.Remove(Remote); Remote.Dispose(); Remote = null; }
            history.Clear(); forwardHistory.Clear();
            Navigate(localReturnPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), false);
        }
        void RememberRemote() { if (Remote != null) openConnections[Remote] = CurrentPath; }
        async void SwitchConnection(RemoteConnection connection)
        {
            try { await SwitchConnectionTask(connection); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("network.openConnectionFailedTitle")); }
        }
        internal async Task SwitchConnectionTask(RemoteConnection connection, Func<string, bool> offerRoot = null)
        {
            if (IsLoading || !openConnections.ContainsKey(connection)) return;
            if (Remote == connection) { activate(this); return; }
            Loading(true);
            try
            {
                string path = openConnections[connection];
                RemoteDirectoryInfo directory = null; IOException failure = null;
                try { directory = await Task.Run(delegate { return connection.Session.ListDirectory(path); }); }
                catch (IOException error) { failure = error; }
                if (failure != null) {
                    try { directory = await Task.Run(delegate { return connection.ReconnectDirectory(path); }); failure = null; }
                    catch (IOException error) { failure = error; }
                    if (failure != null) {
                        var retry = failure;
                        string detail = ConnectionFailure(connection, path, retry);
                        bool root = path != "/" && (offerRoot != null ? offerRoot(detail) : MessageBox.Show(Window.GetWindow(this), detail + L.Text("network.confirmOpenRootInsteadSuffix"), L.Text("network.folderUnavailableTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
                        if (!root) { if (path != "/") return; throw new IOException(detail, retry); }
                        path = "/";
                        try { directory = await Task.Run(delegate { return connection.ReconnectDirectory(path); }); }
                        catch (IOException rootError) { throw new IOException(ConnectionFailure(connection, path, rootError), rootError); }
                    }
                }
                var rows = RemoteRows(directory, path);
                if (!IsRemote) localReturnPath = IsArchive ? archiveReturnPath : CurrentPath;
                RememberRemote(); Remote = connection; history.Clear(); forwardHistory.Clear();
                CurrentPath = path; ApplyRemoteRows(rows, null); activate(this);
            }
            finally { Loading(false); }
        }
        static string ConnectionFailure(RemoteConnection connection, string path, Exception error)
        { return L.Text("network.connectionPrefix") + connection.Profile.Name + " · " + connection.Profile.Protocol + " · " + connection.Profile.Host + ":" + connection.Profile.Port + L.Text("network.folderSummaryPrefix") + path + "\n\n" + error.Message; }
        internal void UpdateConnectionButtons()
        {
            connectionButtons.Children.Clear();
            foreach (var connection in openConnections.Keys.ToArray())
            {
                var captured = connection;
                var button = ConnectionButton(connection.Profile, delegate { SwitchConnection(captured); });
                button.Tag = connection;
                if (Remote == connection) button.Background = CompactTheme.ThemeBrush("#CDE8FF");
                var menu = new System.Windows.Controls.ContextMenu();
                var disconnect = new System.Windows.Controls.MenuItem { Header = L.Text("action.disconnect") };
                disconnect.Click += delegate
                {
                    if (IsLoading) return;
                    if (Remote == captured) DisconnectRemote();
                    else { openConnections.Remove(captured); captured.Dispose(); UpdateConnectionButtons(); }
                };
                menu.Items.Add(disconnect); button.ContextMenu = menu; connectionButtons.Children.Add(button);
            }
            if (localNetworkProfile != null)
            {
                var button = ConnectionButton(localNetworkProfile, delegate { Navigate(localNetworkPath, true); activate(this); });
                if (IsAtLocalNetwork) button.Background = CompactTheme.ThemeBrush("#CDE8FF");
                var menu = new System.Windows.Controls.ContextMenu(); var disconnect = new System.Windows.Controls.MenuItem { Header = L.Text("action.disconnect") };
                disconnect.Click += delegate { if (!IsLoading && DisconnectLocalNetwork != null) DisconnectLocalNetwork(this); };
                menu.Items.Add(disconnect); button.ContextMenu = menu; connectionButtons.Children.Add(button);
            }
        }
        static System.Windows.Controls.Button ConnectionButton(NetworkProfile profile, Action action)
        {
            var label = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            label.Children.Add(CompactTheme.DriveIcon(true));
            label.Children.Add(new System.Windows.Controls.TextBlock { Text = String.IsNullOrEmpty(profile.CloudAccountId)?profile.Protocol.ToString():profile.CloudProvider, MaxWidth = 140,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 2, 0) });
            return CompactTheme.Button(label, !String.IsNullOrEmpty(profile.CloudAccountId)?profile.Name+" · "+profile.CloudProvider: profile.Name + " · " + profile.Protocol + " · " + profile.Host + (profile.Protocol == NetworkProtocol.SMB || profile.Protocol == NetworkProtocol.NFS ? "" : ":" + profile.Port), delegate { action(); });
        }
        public void SetLocalNetwork(NetworkProfile profile, string path) { localNetworkProfile = profile == null ? null : profile.Clone(); localNetworkPath = path; UpdateConnectionButtons(); }
        public NetworkProfile LocalNetworkProfile { get { return localNetworkProfile; } }
        public bool IsAtLocalNetwork { get { return !IsRemote && localNetworkPath != null && CurrentPath != null &&
            (String.Equals(CurrentPath.TrimEnd('\\'), localNetworkPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
            CurrentPath.StartsWith(localNetworkPath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)); } }
        public void DisposeConnections()
        {
            foreach (var connection in openConnections.Keys.ToArray()) connection.Dispose();
            openConnections.Clear(); Remote = null;
        }
        async void OpenRemoteFile(Entry entry, bool view = false)
        {
            if (IsLoading) return;
            Loading(true);
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "FileCompass", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                string name = RemoteConnection.Name(entry.FullPath);
                FileOperations.ValidateName(name);
                string file = Path.Combine(folder, name);
                await Task.Run(delegate { Remote.Session.GetFiles(RemotePath.EscapeFileMask(entry.FullPath), file).Check(); });
                if (view) FileViewers.Open(Window.GetWindow(this), file); else Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("network.remoteFileOpenFailedTitle")); }
            finally { Loading(false); }
        }
    }
}

