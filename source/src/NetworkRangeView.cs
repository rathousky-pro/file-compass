using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FileCompass
{
    public partial class FilePanel
    {
        StackPanel networkRangeBar;
        TextBox networkRangeInput;
        TextBlock networkRangeStatus;
        TreeView networkRangeTree;
        Button networkRangeStart;
        bool networkRangeVisible = true;
        CancellationTokenSource networkRangeCancellation;
#if APP_TESTS
        internal Func<string,int,int,Task<bool>> NetworkRangeProbeForTests;
#endif
        readonly Dictionary<string, TreeViewItem> networkSubnets = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, TreeViewItem> networkTreeEntries = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Entry> customNetworkEntries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        internal Task NetworkRangeTask { get; private set; }

        void InitializeNetworkRangeBar(StackPanel host)
        {
            networkRangeBar = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(9, 3, 9, 3) };
            host.Children.Add(networkRangeBar);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); networkRangeBar.Children.Add(row);
            networkRangeStart = CompactTheme.Button(L.Text("networkScan.start"), L.Text("networkScan.help"), delegate { if (networkRangeCancellation != null) CancelNetworkRangeScan(); else NetworkRangeTask = StartNetworkRangeScan(); });
            FluentTheme.Primary(networkRangeStart); Grid.SetColumn(networkRangeStart, 1); row.Children.Add(networkRangeStart);
            var close = CompactTheme.Button("×", L.Text("tabs.close"), delegate { CancelNetworkRangeScan(); CloseTab(CurrentTabIndex); });
            close.Width = 26; close.Padding = new Thickness(0); System.Windows.Automation.AutomationProperties.SetName(close, L.Text("tabs.close")); Grid.SetColumn(close, 3); row.Children.Add(close);
            networkRangeInput = new TextBox { Text = NetworkDiscovery.DefaultLocalRange(), ToolTip = L.Text("networkScan.help"), Width = 200, MinWidth = 80, VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(networkRangeInput, L.Text("networkScan.range"));
            networkRangeInput.GotKeyboardFocus += delegate { activate(this); };
            networkRangeInput.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && networkRangeCancellation == null) { NetworkRangeTask = StartNetworkRangeScan(); e.Handled = true; } };
            row.Children.Add(networkRangeInput);
            networkRangeStatus = new TextBlock { Text = L.Text("networkScan.help"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 3), Foreground = FluentTheme.Muted };
            networkRangeBar.Children.Add(networkRangeStatus);
            networkRangeTree = new TreeView { Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0,1,0,1), BorderBrush = CompactTheme.Line, Background = CompactTheme.WhiteSurface };
            System.Windows.Automation.AutomationProperties.SetName(networkRangeTree, L.Text("networkScan.tree"));
            networkRangeTree.SelectedItemChanged += delegate { var node = networkRangeTree.SelectedItem as TreeViewItem; var entry = node?.Tag as Entry; if (entry == null) { List.SelectedItem = null; return; } var match = List.Items.Cast<Entry>().FirstOrDefault(x => String.Equals(x.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase)); if (match != null) { activate(this); List.SelectedItem = match; } };
            networkRangeTree.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                var node = networkRangeTree.SelectedItem as TreeViewItem;
                if (e.Key == Key.Enter && node?.Tag is Entry entry) { OpenNetworkTreeEntry(entry); e.Handled = true; }
                else if (e.Key == Key.Back) { Up(); e.Handled = true; }
            };
            PathChanged += delegate {
                bool show = CurrentPath == NetworkBrowser.Root;
                if (show) networkRangeVisible = true;
                UpdateNetworkRangeVisibility();
                networkRangeTree.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                List.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                if (!show && networkRangeCancellation != null) networkRangeCancellation.Cancel();
            };
            Unloaded += delegate { if (networkRangeCancellation != null) networkRangeCancellation.Cancel(); };
        }

        void UpdateNetworkRangeVisibility() { bool show = CurrentPath == NetworkBrowser.Root; networkRangeBar.Visibility = show && networkRangeVisible ? Visibility.Visible : Visibility.Collapsed; cleanRootButton.Visibility = cleanParentButton.Visibility = show ? Visibility.Collapsed : Visibility.Visible; }
        void ShowNetworkRange() { if (CurrentPath == NetworkBrowser.Root && !networkRangeVisible) { networkRangeVisible = true; UpdateNetworkRangeVisibility(); } }
        internal bool CancelNetworkRangeScan() { if (networkRangeCancellation == null) return false; networkRangeCancellation.Cancel(); return true; }
        void SetNetworkRangeBusy(bool busy) { networkRangeInput.IsEnabled = !busy; networkRangeStart.Content = L.Text(busy ? "networkScan.stop" : "networkScan.start"); networkRangeStart.ToolTip = L.Text(busy ? "networkScan.stop" : "networkScan.help"); }

        bool OpenUnidentifiedNetworkAddress(Entry entry)
        {
            if (CurrentPath != NetworkBrowser.Root || entry.NetworkServiceProfile != null ||
                entry.NetworkDeviceTarget != null || !NetworkBrowser.IsServer(entry.FullPath)) return false;
            string host = entry.FullPath.Trim('\\');
            if (!IPAddress.TryParse(host, out var address)) return false;
            var main = Window.GetWindow(this) as MainWindow;
            if (main == null) return false;
            var profile = new NetworkProfile { Name = host, Host = host, Protocol = NetworkProtocol.SFTP, Port = 22, Path = "/" };
            main.OpenDiscoveredService(this, new Entry { NetworkServiceProfile = profile });
            return true;
        }

        void OpenNetworkTreeEntry(Entry entry)
        {
            if (OpenUnidentifiedNetworkAddress(entry)) return;
            var match = List.Items.Cast<Entry>().FirstOrDefault(item =>
                String.Equals(item.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase));
            if (match == null) return;
            List.SelectedItem = match;
            OpenSelected();
        }

        static string NetworkSubnet(Entry entry)
        {
            string host = entry.NetworkServiceProfile != null ? entry.NetworkServiceProfile.Host : (entry.FullPath ?? "").TrimStart('\\').Split('\\')[0];
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip) && ip.AddressFamily == AddressFamily.InterNetwork) { var b = ip.GetAddressBytes(); return b[0] + "." + b[1] + "." + b[2] + ".x"; }
            return L.Text("networkScan.unknownSubnet");
        }

        internal void RefreshNetworkTree(IEnumerable<Entry> rows)
        {
            if (networkRangeTree == null || CurrentPath != NetworkBrowser.Root) return;
            var entries = rows.Where(x => !x.IsParent && !String.IsNullOrEmpty(x.FullPath)).ToList();
            var present = new HashSet<string>(entries.Select(x => x.FullPath), StringComparer.OrdinalIgnoreCase);
            foreach (string key in networkTreeEntries.Keys.Where(x => !present.Contains(x)).ToArray()) { var node = networkTreeEntries[key]; var parent = node.Parent as TreeViewItem; if (parent != null) parent.Items.Remove(node); networkTreeEntries.Remove(key); }
            foreach (var entry in entries)
            {
                var group = NetworkSubnet(entry); TreeViewItem parent;
                if (!networkSubnets.TryGetValue(group, out parent)) { parent = new TreeViewItem { Header = group, IsExpanded = true }; networkSubnets[group] = parent; networkRangeTree.Items.Add(parent); }
                TreeViewItem node;
                if (!networkTreeEntries.TryGetValue(entry.FullPath, out node)) {
                    node = new TreeViewItem(); networkTreeEntries[entry.FullPath] = node; parent.Items.Add(node);
                    node.PreviewMouseRightButtonDown += delegate(object sender, MouseButtonEventArgs e) { ((TreeViewItem)sender).IsSelected = true; };
                    node.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
                    {
                        if (e.ChangedButton != MouseButton.Left || ((TreeViewItem)sender).Tag is not Entry selected) return;
                        OpenNetworkTreeEntry(selected);
                        e.Handled = true;
                    };
                }
                string host = entry.NetworkServiceProfile != null ? entry.NetworkServiceProfile.Host : (entry.FullPath ?? "").TrimStart('\\').Split('\\')[0];
                IPAddress address; string caption = IPAddress.TryParse(host, out address) && !entry.Name.Contains(host) ? entry.Name + " · " + host : entry.Name;
                var title = new StackPanel { Orientation = Orientation.Horizontal }; if (entry.Icon != null) title.Children.Add(new Image { Source = entry.Icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) }); title.Children.Add(new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center }); node.Header = title; node.Tag = entry;
            }
            foreach (string key in networkSubnets.Keys.Where(x => networkSubnets[x].Items.Count == 0).ToArray()) { networkRangeTree.Items.Remove(networkSubnets[key]); networkSubnets.Remove(key); }
        }

        internal async Task StartNetworkRangeScan()
        {
            if (networkRangeCancellation != null || CurrentPath != NetworkBrowser.Root) return;
            if (LocalLoading) { networkRangeStatus.Text = L.Text("networkScan.wait"); return; }
            NetworkDiscovery.ScanRange range;
            try { range = NetworkDiscovery.ParseRange(networkRangeInput.Text); }
            catch (FormatException ex) { networkRangeStatus.Text = ex.Message; return; }
            var operation = new CancellationTokenSource(); networkRangeCancellation = operation;
            SetNetworkRangeBusy(true);
            networkRangeStatus.Text = NetworkProgress(0, range.Count, NetworkDiscovery.SubnetLabel(range.First));
            long shownProgress = -1;
            try
            {
                Func<string,int,int,Task<bool>> probe = null;
#if APP_TESTS
                probe = NetworkRangeProbeForTests;
#endif
                await Task.Run(() => NetworkDiscovery.ScanRangeAsync(range, operation.Token, entry => Dispatcher.Invoke(delegate {
                    if (networkRangeCancellation != operation || CurrentPath != NetworkBrowser.Root || entry == null) return;
                    customNetworkEntries[entry.FullPath] = entry;
                    var rows = List.ItemsSource.Cast<Entry>().ToList(); int i = rows.FindIndex(x => String.Equals(x.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (i < 0) rows.Add(entry); else rows[i] = entry; ShowNetworkRows(NetworkBrowser.Root, false, rows);
                }), (done, total, subnet) => Dispatcher.BeginInvoke(new Action(delegate {
                    if (networkRangeCancellation == operation && CurrentPath == NetworkBrowser.Root && done >= shownProgress) { shownProgress = done; networkRangeStatus.Text = NetworkProgress(done, total, subnet); }
                })), probe));
                await Dispatcher.InvokeAsync(new Action(delegate { if (networkRangeCancellation == operation) networkRangeStatus.Text = L.Text("networkScan.complete"); }));
            }
            catch (OperationCanceledException) { await Dispatcher.InvokeAsync(new Action(delegate { if (networkRangeCancellation == operation) networkRangeStatus.Text = L.Text("networkScan.cancelled"); })); }
            catch (Exception ex) { await Dispatcher.InvokeAsync(new Action(delegate { if (networkRangeCancellation == operation) networkRangeStatus.Text = ex.Message; })); }
            finally { await Dispatcher.InvokeAsync(new Action(delegate { if (networkRangeCancellation == operation) { networkRangeCancellation = null; SetNetworkRangeBusy(false); } })); operation.Dispose(); }
        }
        internal static string NetworkProgress(long done,long total,string subnet) { double percent = total <= 0 ? 0 : 100.0 * done / total; if (done < total) percent = Math.Min(99.99, percent); return L.Text("networkScan.scanning") + " " + subnet + " · " + percent.ToString("0.00") + " %"; }
    }
}
