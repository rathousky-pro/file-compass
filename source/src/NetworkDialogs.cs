using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public sealed class NetworkManagerDialog : Window
    {
        internal Window HostWindow; Window UiOwner {get{return HostWindow??this;}} void CloseManager(){if(HostWindow!=null)HostWindow.Close();else Close();} public NetworkProfile ConnectProfile;
        public bool OpenSsh;
        readonly NetworkSettingsStore store;
        readonly ListBox list = new ListBox { BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        readonly TextBlock details = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 6, 2, 0) };
        readonly Button connect, terminal;
        readonly bool managementOnly;
        readonly System.Collections.Generic.List<Button> selectionButtons = new System.Collections.Generic.List<Button>();
        readonly ComboBox protocolFilter = new ComboBox { MinWidth = 100 };
        internal NetworkProtocol? ProtocolFilter { get { return protocolFilter.SelectedItem is NetworkProtocol ? (NetworkProtocol?)protocolFilter.SelectedItem : null; } }
        internal void SelectFilter(NetworkProtocol? protocol) { protocolFilter.SelectedItem = protocol.HasValue ? (object)protocol.Value : L.Text("network.allProtocols"); }
        public NetworkManagerDialog(Window owner, NetworkSettingsStore settings, NetworkProtocol? filter, bool managementOnly = false)
        {
            this.managementOnly = managementOnly; store = settings; CopyUi.Style(this, owner, L.Text("network.serverConnectionTitle"), 580); Height = 530; MinWidth = 540; MinHeight = 500; SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            DialogGeometry.Bound(this,760,550,600,460);
            var panel = new Grid { Margin = new Thickness(6) }; Content = panel;
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition()); panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var filters = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(filters, Dock.Right); filterRow.Children.Add(filters);
            filters.Children.Add(new TextBlock { Text = L.Text("network.protocolLabel"), VerticalAlignment = VerticalAlignment.Center }); filters.Children.Add(protocolFilter);
            filterRow.Children.Add(new TextBlock { Text = L.Text(managementOnly ? "connections.savedProfiles" : "network.connectToServerLabel"), VerticalAlignment = VerticalAlignment.Center }); panel.Children.Add(filterRow);
            protocolFilter.Items.Add(L.Text("network.allProtocols")); foreach (NetworkProtocol type in Enum.GetValues(typeof(NetworkProtocol))) protocolFilter.Items.Add(type);
            protocolFilter.SelectedItem = filter.HasValue ? (object)filter.Value : L.Text("network.allProtocols");
            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); Grid.SetRow(body, 1); panel.Children.Add(body);
            list.BorderThickness = new Thickness(0);
            body.Children.Add(new Border { Child = list, Background = CompactTheme.WhiteSurface,
                BorderBrush = CompactTheme.ThemeBrush("#AEB8C4"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(2), Margin = new Thickness(0, 0, 12, 0) });
            var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var icon = new FrameworkElementFactory(typeof(Image)); icon.SetValue(Image.SourceProperty, DesktopIcons.Drawing("network")); icon.SetValue(Image.WidthProperty, 14.0); icon.SetValue(Image.HeightProperty, 14.0); icon.SetValue(Image.MarginProperty, new Thickness(0, 0, 3, 0)); row.AppendChild(icon);
            var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding()); row.AppendChild(name);
            list.ItemTemplate = new DataTemplate { VisualTree = row };
            var buttons = new StackPanel(); Grid.SetColumn(buttons, 1); body.Children.Add(buttons);
            connect = CopyUi.Button(L.Text("action.append"), Connect); connect.IsDefault = !managementOnly; connect.BorderBrush = System.Windows.Media.Brushes.DodgerBlue; connect.Margin = new Thickness(2, 0, 0, 10); if (!managementOnly) buttons.Children.Add(connect);
            buttons.Children.Add(CopyUi.Button(L.Text("network.newConnection"), delegate { Edit(null, ProtocolFilter); }));
            var duplicate = CopyUi.Button(L.Text("action.duplicate"), Duplicate); buttons.Children.Add(duplicate); selectionButtons.Add(duplicate);
            buttons.Children.Add(new Border { Height = 8 });
            var edit = CopyUi.Button(L.Text("action.editDialog"), delegate { var item = list.SelectedItem as NetworkProfile; if (item != null) Edit(item, null); }); buttons.Children.Add(edit); selectionButtons.Add(edit);
            var remove = CopyUi.Button(L.Text("action.remove"), Remove); buttons.Children.Add(remove); selectionButtons.Add(remove);
            terminal = CopyUi.Button(L.Text("ssh.terminalTitle"), delegate { Choose(true); }); if (!managementOnly) buttons.Children.Add(terminal);
            var close = CopyUi.Button(L.Text("action.cancel"), CloseManager); close.IsCancel = true; if (!managementOnly) buttons.Children.Add(close);
            var help = CopyUi.Button(L.Text("action.help"), delegate { MessageBox.Show(UiOwner, L.Text("network.savedConnectionListHelp"), L.Text("network.connectionsTitle")); }); help.Margin = new Thickness(3, 10, 3, 3); buttons.Children.Add(help);
            var footer = new DockPanel(); Grid.SetRow(footer, 2); panel.Children.Add(footer);
            var support = CopyUi.Button(L.Text("sshfs.setupMenu"), delegate { SshfsSetupWindow.Open(UiOwner); }); support.Width = 164; support.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(support, Dock.Right); footer.Children.Add(support); footer.Children.Add(details);
            list.MouseDoubleClick += delegate { var selected = list.SelectedItem as NetworkProfile; if (selected != null) { if (managementOnly) Edit(selected, null); else Connect(); } };
            protocolFilter.SelectionChanged += delegate { var selected = list.SelectedItem as NetworkProfile; Reload(selected == null ? null : selected.Id); };
            Reload(null);
            list.SelectionChanged += delegate { UpdateButtons(); }; UpdateButtons();
        }
        void Duplicate()
        {
            var profile = list.SelectedItem as NetworkProfile; if (profile == null) return;
            var copy = profile.Clone(); copy.Id = Guid.NewGuid().ToString("N"); copy.Name += L.Text("network.profileCopySuffix"); Edit(copy, null);
        }
        void Reload(string id)
        {
            var visible = store.Settings.Connections.Where(x => !ProtocolFilter.HasValue || x.Protocol == ProtocolFilter.Value).OrderBy(x => x.Name).ToArray();
            list.ItemsSource = visible;
            list.SelectedItem = visible.FirstOrDefault(x => x.Id == id);
            if (list.SelectedItem == null && list.Items.Count > 0) list.SelectedIndex = 0;
        }
        void Edit(NetworkProfile profile, NetworkProtocol? protocol)
        {
            var editor = new NetworkProfileDialog(UiOwner, profile ?? new NetworkProfile { Protocol = protocol ?? NetworkProtocol.SMB,
                Port = protocol == NetworkProtocol.FTP ? 21 : 22, Path = protocol == NetworkProtocol.SMB || !protocol.HasValue ? "" : "/" }, profile == null);
            if (editor.ShowDialog() != true) return;
            try { store.Put(editor.Profile); if (ProtocolFilter.HasValue && ProtocolFilter.Value != editor.Profile.Protocol) SelectFilter(editor.Profile.Protocol); Reload(editor.Profile.Id); }
            catch (Exception ex) { MessageBox.Show(UiOwner, ex.Message, L.Text("network.saveConnectionFailed"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        void Remove()
        {
            var profile = list.SelectedItem as NetworkProfile; if (profile == null) return;
            if (MessageBox.Show(UiOwner, L.Text("network.confirmDeleteProfilePrefix") + profile.Name + "“?", L.Text("network.connectionsTitle"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try { store.Remove(profile); Reload(null); } catch (Exception ex) { MessageBox.Show(UiOwner, ex.Message, L.Text("settings.saveFailed")); }
        }
        void UpdateButtons()
        {
            var profile = list.SelectedItem as NetworkProfile;
            foreach (var button in selectionButtons) button.IsEnabled = profile != null;
            connect.IsEnabled = profile != null; connect.Content = profile != null && profile.Protocol == NetworkProtocol.SSH ? L.Text("ssh.openTerminal") : L.Text("action.append");
            terminal.IsEnabled = profile != null && profile.CanOpenSsh;
            details.Text = profile == null ? "" : profile.Protocol + " · " + profile.Host + ((profile.Protocol == NetworkProtocol.SMB || profile.Protocol == NetworkProtocol.NFS) ? "" : ":" + profile.Port) + "\n" + profile.Path;
            details.ToolTip = details.Text;
            if (profile != null && profile.Protocol == NetworkProtocol.SSHFS) { details.Text += L.Text("common.routeArrow") + profile.DriveLetter; details.ToolTip = details.Text; }
        }
        void Choose(bool ssh)
        {
            if (managementOnly) return;
            ConnectProfile = list.SelectedItem as NetworkProfile;
            if (ConnectProfile == null || (ssh && !ConnectProfile.CanOpenSsh)) return;
            OpenSsh = ssh; UiOwner.DialogResult = true;
        }
        void Connect() { Choose(false); }
    }

}

