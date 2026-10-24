using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public static partial class WindowsNetwork
    {
        internal static string ValidateMapping(string drive, string remote)
        {
            if (drive == null || drive.Length != 2 || drive[1] != ':' || drive[0] < 'D' || drive[0] > 'Z')
                throw new ArgumentException(L.Text("drive.validLetterRequired"));
            string value = (remote ?? "").Trim().Replace('/', '\\').TrimEnd('\\');
            if (!value.StartsWith("\\\\") || value.IndexOfAny(new[] { '"', '\r', '\n', '\0', '*', '?', '<', '>', '|', ':' }) >= 0)
                throw new ArgumentException(L.Text("drive.uncPathRequired"));
            string[] parts = value.Substring(2).Split('\\');
            if (parts.Length < 2 || parts.Any(x => String.IsNullOrWhiteSpace(x) || x == "." || x == ".."))
                throw new ArgumentException(L.Text("drive.serverAndShareRequired"));
            return value;
        }
        internal static string[] FreeDriveLetters()
        {
            var used = DriveInfo.GetDrives().Select(x => x.Name.Substring(0, 2)).Concat(FilePanel.RememberedDrives().Keys.Select(x => x.Substring(0, 2))).ToArray();
            return FreeDriveLetters(used);
        }
        internal static string[] FreeDriveLetters(System.Collections.Generic.IEnumerable<string> used)
        {
            var occupied = new System.Collections.Generic.HashSet<string>(used.Select(x => x.TrimEnd('\\', '/')), StringComparer.OrdinalIgnoreCase);
            return Enumerable.Range('D', 'Z' - 'D' + 1).Reverse().Select(x => ((char)x) + ":").Where(x => !occupied.Contains(x)).ToArray();
        }
        internal static void MapDrive(string drive, string remote, string user, string password, bool reconnect)
        {
            remote = ValidateMapping(drive, remote);
            if (!FreeDriveLetters().Contains(drive, StringComparer.OrdinalIgnoreCase)) throw new IOException(L.Text("drive.namePrefix") + drive + L.Text("drive.mappingLetterOccupiedSuffix"));
            var resource = new NetResource { Type = 1, LocalName = drive, RemoteName = remote };
            int result = WNetAddConnection2(ref resource, String.IsNullOrEmpty(user) ? null : password, String.IsNullOrEmpty(user) ? null : user, reconnect ? 1 : 0);
            if (result == 1219) throw new IOException(L.Text("drive.existingCredentialsConflict"));
            if (result != 0) throw new Win32Exception(result);
        }
    }
    internal sealed class MapNetworkDriveDialog : Window
    {
        readonly ComboBox drive = new ComboBox();
        readonly TextBox remote = new TextBox(), user = new TextBox();
        readonly PasswordBox password = new PasswordBox();
        readonly CheckBox reconnect = new CheckBox { Content = L.Text("drive.reconnectAtLogin"), IsChecked = true, Margin = new Thickness(2, 7, 2, 5) },
            credentials = new CheckBox { Content = L.Text("drive.useOtherCredentials"), Margin = new Thickness(2, 5, 2, 5) };
        readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 2, 5) };
        readonly StackPanel fields = new StackPanel(); readonly Button connect, cancel;
        bool connecting;
        internal string MappedRoot;
        internal MapNetworkDriveDialog(Window owner, string initial)
        {
            CopyUi.Style(this, owner, L.Text("drive.mapTitle"), 490);
            var panel = new StackPanel { Margin = new Thickness(12) }; Content = panel;
            panel.Children.Add(new TextBlock { Text = L.Text("drive.mapShareDescription"), FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 2, 9) });
            NetworkProfileDialog.Row(fields, L.Text("drive.letterLabel"), drive); drive.ItemsSource = WindowsNetwork.FreeDriveLetters(); drive.SelectedIndex = 0;
            remote.Text = initial != null && initial.StartsWith("\\\\") ? initial : "";
            NetworkProfileDialog.Row(fields, L.Text("drive.networkFolderLabel"), remote);
            fields.Children.Add(new TextBlock { Text = L.Text("drive.uncPathExample"), Margin = new Thickness(136, 0, 2, 5) });
            fields.Children.Add(reconnect); fields.Children.Add(credentials); NetworkProfileDialog.Row(fields, L.Text("network.usernameLabel"), user); NetworkProfileDialog.Row(fields, L.Text("network.passwordLabel"), password);
            user.IsEnabled = password.IsEnabled = false;
            credentials.Checked += delegate { user.IsEnabled = password.IsEnabled = true; user.Focus(); };
            credentials.Unchecked += delegate { user.IsEnabled = password.IsEnabled = false; };
            panel.Children.Add(fields); panel.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            connect = CopyUi.Button(L.Text("action.append"), Connect); connect.Width = 110; connect.IsDefault = true; buttons.Children.Add(connect);
            cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.Width = 110; cancel.IsCancel = true; buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (connecting) e.Cancel = true; };
            Loaded += delegate { remote.Focus(); };
        }
        async void Connect()
        {
            string letter = drive.SelectedItem as string, target;
            try { target = WindowsNetwork.ValidateMapping(letter, remote.Text); }
            catch (Exception ex) { status.Text = ex.Message; return; }
            string login = credentials.IsChecked == true ? user.Text.Trim() : "", secret = credentials.IsChecked == true ? password.Password : "";
            if (credentials.IsChecked == true && login.Length == 0) { status.Text = L.Text("drive.usernameRequired"); return; }
            bool persistent = reconnect.IsChecked == true;
            connecting = true; fields.IsEnabled = connect.IsEnabled = cancel.IsEnabled = false; status.Text = L.Text("drive.connectingStatus") + letter + L.Text("common.routeArrow") + target;
            try
            {
                await Task.Run(delegate { WindowsNetwork.MapDrive(letter, target, login, secret, persistent); });
                MappedRoot = letter + "\\"; connecting = false; DialogResult = true;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message; drive.ItemsSource = WindowsNetwork.FreeDriveLetters(); drive.SelectedItem = letter;
                if (drive.SelectedItem == null) drive.SelectedIndex = 0;
            }
            finally { connecting = false; fields.IsEnabled = connect.IsEnabled = cancel.IsEnabled = true; password.Clear(); }
        }
    }
    public partial class MainWindow
    {
        internal void MapNetworkDrive()
        { MapNetworkFolder(active.IsRemote || active.IsArchive || active.IsVirtualBrowser ? null : active.CurrentPath); }
        internal void MapNetworkFolder(string path)
        {
            if (busy || PanelsLoading) return;
            var panel = active; var dialog = new MapNetworkDriveDialog(this, path);
            if (dialog.ShowDialog() == true)
            {
                left.RefreshDriveBar(); right.RefreshDriveBar();
                try { panel.Navigate(dialog.MappedRoot, true); }
                catch (Exception ex) { MessageBox.Show(this, L.Text("drive.connectedFolderOpenFailedPrefix") + ex.Message, L.Text("drive.networkDriveTitle")); }
            }
        }
    }
}


