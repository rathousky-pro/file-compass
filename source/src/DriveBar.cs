using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CommanderCZ
{
    public partial class FilePanel
    {
        internal Func<string, string> DriveFolderResolver;
        internal void SelectDrive(string root)
        {
            if (IsLoading) return;
            string path = driveAlwaysRoot || DriveFolderResolver == null ? root : DriveFolderResolver(root);
            activate(this); FocusFiles(); Navigate(path ?? root, true);
        }
        readonly DispatcherTimer driveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        Action refreshDrives;
        readonly TaskCompletionSource<bool> driveDiscovery = new TaskCompletionSource<bool>();
        internal Task DriveDiscovery { get { return driveDiscovery.Task; } }
        internal void RefreshDriveBar() { if (refreshDrives != null) refreshDrives(); }
        readonly HashSet<string> checkingDrives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, bool> driveNetworks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> driveAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DriveChoice> driveChoices = new Dictionary<string, DriveChoice>(StringComparer.OrdinalIgnoreCase);
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        static extern int WNetGetConnection(string name, StringBuilder remote, ref int length);
        internal static Dictionary<string, string> RememberedDrives()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
            using (var key = Registry.CurrentUser.OpenSubKey("Network"))
                if (key != null)
                    foreach (string letter in key.GetSubKeyNames())
                        if (letter.Length == 1 && Char.IsLetter(letter[0]))
                            using (var mapping = key.OpenSubKey(letter))
                                if (mapping != null) result[letter.ToUpperInvariant() + ":\\"] = mapping.GetValue("RemotePath", "").ToString();
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            // Remembered mappings can also be returned as ERROR_CONNECTION_UNAVAIL.
            foreach (var mapped in SshfsMount.MappedDrives()) result[mapped.Key] = mapped.Value;
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                string device = letter + ":";
                var remote = new StringBuilder(1024); int length = remote.Capacity;
                int status = WNetGetConnection(device, remote, ref length);
                if ((status == 0 || status == 1201) && remote.Length > 0) result[device + "\\"] = remote.ToString();
            }
            return result;
        }
        sealed class DriveSnapshot
        {
            internal Dictionary<string, string> Remembered, Mounted;
            internal Dictionary<string, DriveInfo> Available;
            internal Dictionary<string, System.Windows.Media.ImageSource> Icons = new Dictionary<string, System.Windows.Media.ImageSource>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string, bool> Networks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string, bool> Connected = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            internal static DriveSnapshot Read()
            {
                var data = new DriveSnapshot { Remembered = RememberedDrives(), Mounted = SshfsMount.MappedDrives(), Available = DriveInfo.GetDrives().ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase) };
                foreach (string root in data.Available.Keys.Union(data.Remembered.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    DriveInfo drive; data.Available.TryGetValue(root, out drive);
                    bool network = data.Remembered.ContainsKey(root) || (drive != null && drive.DriveType == DriveType.Network);
                    bool connected = true;
                    if (network)
                    {
                        var remote = new StringBuilder(1024); int length = remote.Capacity;
                        connected = WNetGetConnection(root.Substring(0, 2), remote, ref length) == 0 || data.Mounted.ContainsKey(root);
                    }
                    data.Networks[root] = network; data.Connected[root] = connected;
                    data.Icons[root] = DesktopIcons.WindowsDriveIcon(root, network, connected);
                }
                return data;
            }
        }
        static readonly object driveSnapshotGate = new object();
        static Task<DriveSnapshot> driveSnapshotTask;
        static DateTime driveSnapshotStarted;
        static Task<DriveSnapshot> ReadDriveSnapshot()
        {
            lock (driveSnapshotGate)
            {
                // Both panels share one probe; a slow network provider must not accumulate probes.
                if (driveSnapshotTask == null || (driveSnapshotTask.IsCompleted && DateTime.UtcNow - driveSnapshotStarted > TimeSpan.FromSeconds(1)))
                { driveSnapshotStarted = DateTime.UtcNow; driveSnapshotTask = Task.Run(() => DriveSnapshot.Read()); }
                return driveSnapshotTask;
            }
        }
        void InitializeDrives(StackPanel drives)
        {
            Action<DriveSnapshot> apply = delegate(DriveSnapshot snapshot)
            {
                var remembered = snapshot.Remembered;
                var available = snapshot.Available;
                foreach (string root in available.Keys.Union(remembered.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray())
                {
                    bool network = snapshot.Networks[root];
                    driveNetworks[root] = network;
                    driveAddresses[root] = remembered.ContainsKey(root) ? remembered[root] : "";
                    bool connected = snapshot.Connected[root];
                    Button button;
                    if (!driveButtons.TryGetValue(root, out button))
                    {
                        string path = root;
                        button = CompactTheme.Button(DriveLabel(root, network, connected, snapshot.Icons[root]), root, delegate { SelectDrive(path); });
                        ToolTipService.SetInitialShowDelay(button, 200); ToolTipService.SetBetweenShowDelay(button, 50); ToolTipService.SetShowDuration(button, 15000);
                        int position = DriveInsertionIndex(driveButtons.Keys, root);
                        drives.Children.Insert(position, button); driveButtons[root] = button;
                        var choice = new DriveChoice(root); choice.Update("", driveAddresses[root], network, connected, snapshot.Icons[root]);
                        driveChoices[root] = choice; driveSelector.Items.Insert(position, choice);
                    }
                    button.Content = DriveLabel(root, network, connected, snapshot.Icons[root]);
                    var owner = System.Windows.Window.GetWindow(this) as MainWindow;
                    button.ContextMenu = owner == null ? null : owner.SshfsDriveMenu(root, driveAddresses[root]);
                    if (network && !connected) { button.ToolTip = DriveTooltip(root, "", driveAddresses[root], true, false); driveChoices[root].Update("", driveAddresses[root], true, false, snapshot.Icons[root]); }
                    else CheckDrive(root, button);
                }
                foreach (var pair in driveButtons.Where(x => !available.ContainsKey(x.Key) && !remembered.ContainsKey(x.Key)).ToArray())
                { drives.Children.Remove(pair.Value); driveButtons.Remove(pair.Key); driveSelector.Items.Remove(driveChoices[pair.Key]); driveChoices.Remove(pair.Key); }
            };
            int refreshing = 0;
            Action refresh = async delegate
            {
                if (System.Threading.Interlocked.CompareExchange(ref refreshing, 1, 0) != 0) return;

                try
                {
                    var snapshot = await ReadDriveSnapshot().ConfigureAwait(false);
                    if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                        await Dispatcher.InvokeAsync(new Action(delegate { apply(snapshot); if (!IsRemote && !IsArchive && CurrentPath != null) UpdateDriveSelection(); driveDiscovery.TrySetResult(true); })).Task.ConfigureAwait(false);
                }
                catch (IOException) { driveDiscovery.TrySetResult(false); }
                catch (UnauthorizedAccessException) { driveDiscovery.TrySetResult(false); }
                catch (System.Security.SecurityException) { }
                catch (OperationCanceledException) { }
                finally { driveDiscovery.TrySetResult(false); System.Threading.Interlocked.Exchange(ref refreshing, 0); }
            };
            refreshDrives = refresh; driveTimer.Tick += delegate { refresh(); };
            Loaded += delegate { Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, refresh); driveTimer.Start(); };
            Unloaded += delegate { driveTimer.Stop(); };
        }
        internal static int DriveInsertionIndex(IEnumerable<string> roots, string root)
        {
            return roots.Count(x => StringComparer.OrdinalIgnoreCase.Compare(x, root) < 0);
        }
        internal static StackPanel DriveLabel(string root, bool network, bool connected, System.Windows.Media.ImageSource icon = null)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new Image { Source = icon ?? DesktopIcons.WindowsDriveIcon(root, network, connected), Width = 16, Height = 16, Margin = new System.Windows.Thickness(0, 0, 3, 0) });
            label.Children.Add(new TextBlock { Text = root.Substring(0, 1).ToLowerInvariant(), VerticalAlignment = System.Windows.VerticalAlignment.Center });
            return label;
        }
        internal static string DriveTooltip(string root, string name, string address, bool network, bool ready)
        {
            return root + (String.IsNullOrWhiteSpace(name) ? "" : "  [" + name + "]")
                + (String.IsNullOrWhiteSpace(address) ? "" : "\n" + address)
                + (!ready ? network ? L.Text("drive.disconnectedSuffix") : L.Text("drive.notReadySuffix") : "");
        }
        async void CheckDrive(string root, Button button)
        {
            if (!checkingDrives.Add(root)) return;
            // During the window constructor WPF has not installed its synchronization
            // context yet. Always dispatch the result explicitly to the panel thread.
            bool network; driveNetworks.TryGetValue(root, out network);
            string address; driveAddresses.TryGetValue(root, out address);
            bool ready = false; string name = ""; System.Windows.Media.ImageSource icon = null;
            await Task.Run(delegate
            {
                try
                {
                    var drive = new DriveInfo(root); ready = drive.IsReady;
                    if (ready) name = drive.VolumeLabel;
                    icon = DesktopIcons.WindowsDriveIcon(root, network, ready, ready && !network);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
            });
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            await Dispatcher.InvokeAsync(new Action(delegate
            {
                try
                {
                    Button current;
                    if (driveButtons.TryGetValue(root, out current) && current == button)
                    {
                        button.Content = DriveLabel(root, network, ready, icon);
                        button.ToolTip = DriveTooltip(root, name, address, network, ready);
                        driveChoices[root].Update(name, address, network, ready, icon);
                    }
                }
                finally { checkingDrives.Remove(root); }
            })).Task;
        }
    }
}

