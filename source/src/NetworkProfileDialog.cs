using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    public sealed partial class NetworkProfileDialog : Window
    {
        public NetworkProfile Profile;
        readonly TextBox name = new TextBox(), host = new TextBox(), path = new TextBox(), local = new TextBox(), user = new TextBox(),
            key = new TextBox(), fingerprint = new TextBox(), timeout = new TextBox(), commands = new TextBox(), certificate = new TextBox(),
            proxyHost = new TextBox(), proxyPort = new TextBox(), proxyUser = new TextBox(), keepalive = new TextBox();
        readonly PasswordBox password = new PasswordBox(), proxyPassword = new PasswordBox();
        readonly CheckBox remember = Check(L.Text("network.encryptSavedPassword")),
            tls = Check("SSL/TLS"), passive = Check(L.Text("ftp.passiveModeOption")),
            anon = Check(L.Text("nfs.anonymousOption")), compression = Check(L.Text("ssh.compressionOption")),
            agent = Check(L.Text("ssh.useAgentOption")), terminal = Check(L.Text("ssh.useWindowsTerminalOption"));
        readonly CheckBox sshfsStartup = Check(L.Text("sshfs.connectOnStartupOption")), sshfsKeep = Check(L.Text("sshfs.keepMountedOption"));
        readonly ComboBox protocol = new ComboBox { ItemsSource = Enum.GetValues(typeof(NetworkProtocol)) },
            tlsMode = Choice(L.Text("ftp.explicitTlsChoice"), L.Text("ftp.implicitTlsChoice")), encoding = Choice(L.Text("network.ansiEncodingChoice"), "UTF-8", L.Text("network.detectUtf8EncodingChoice")),
            mlsd = Choice(L.Text("ftp.disableMlsd"), L.Text("ftp.enableMlsd"), L.Text("network.autoDetectChoice")),
            hidden = Choice(L.Text("ftp.disableListHidden"), L.Text("ftp.enableListHidden"), L.Text("network.autoDetectChoice")),
            proxy = Choice(L.Text("proxy.noneChoice"), "SOCKS4", "SOCKS5", "HTTP CONNECT"), drive = new ComboBox(),
            shell = Choice(L.Text("shell.windowsPowerShellChoice"), L.Text("shell.powerShell7Choice"));
        readonly StackPanel general = new StackPanel(), advanced = new StackPanel();
        internal readonly TabControl Tabs = new TabControl();
        internal void SelectProtocol(NetworkProtocol type) { protocol.SelectedItem = type; }
        public NetworkProfileDialog(Window owner, NetworkProfile source, bool newProfile = false)
        {
            Profile = source.Clone(); CopyUi.Style(this, owner, L.Text("network.connectionDetailsTitlePrefix") + Profile.Protocol, 510);
            DialogGeometry.Bound(this, 960, 630, 820, 350); ResizeMode = ResizeMode.CanResize;
            StackPanel buttons;var root=SettingsUx.Frame(this,L.Text("network.connectionDetailsTitlePrefix")+Profile.Protocol,out buttons);
            var ok = CopyUi.Button("OK", Accept); ok.IsDefault = true; ok.Width = 110; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; cancel.Width = 110; buttons.Children.Add(cancel);
            var help = CopyUi.Button(L.Text("action.help"), Help); help.Width = 110; buttons.Children.Add(help);
            buttons.Children.Remove(ok);buttons.Children.Add(ok);FluentTheme.Primary(ok);Tabs.Margin=new Thickness(16);
            Tabs.Items.Add(new TabItem { Header = L.Text("tab.general"), Content = Page(general) });
            Tabs.Items.Add(new TabItem { Header = L.Text("tab.details"), Content = Page(advanced) }); root.Children.Add(Tabs);
            name.Text = Profile.Name; host.Text = HostText(Profile);
            path.Text = Profile.Path; local.Text = Profile.LocalPath; user.Text = Profile.UserName; key.Text = Profile.PrivateKeyPath; fingerprint.Text = Profile.HostKeyFingerprint;
            remember.IsChecked = !String.IsNullOrEmpty(Profile.EncryptedPassword);
            password.ToolTip = L.Text("network.savedPasswordFieldHelp");
            tls.IsChecked = Profile.FtpTls; tlsMode.SelectedIndex = Profile.FtpImplicitTls ? 1 : 0; passive.IsChecked = Profile.FtpPassive;
            anon.IsChecked = Profile.NfsAnonymous; compression.IsChecked = Profile.SshCompression; agent.IsChecked = Profile.SshAgent;
            terminal.IsChecked = Profile.UseWindowsTerminal; shell.SelectedIndex = Profile.UsePowerShell7 ? 1 : 0;
            sshfsStartup.IsChecked = Profile.SshfsConnectOnStartup; sshfsKeep.IsChecked = Profile.SshfsKeepMounted;
            encoding.SelectedIndex = Profile.FilenameEncoding; mlsd.SelectedIndex = Profile.FtpMlsd; hidden.SelectedIndex = Profile.FtpHidden;
            timeout.Text = Profile.TimeoutSeconds.ToString(); commands.Text = Profile.FtpCommands; certificate.Text = Profile.ClientCertificate;
            commands.AcceptsReturn = true; commands.Height = 46; commands.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            proxy.SelectedIndex = Profile.ProxyMethod; proxyHost.Text = Profile.ProxyHost; proxyPort.Text = Profile.ProxyPort.ToString(); proxyUser.Text = Profile.ProxyUser;
            proxyPassword.ToolTip = L.Text("proxy.savedPasswordFieldHelp");
            keepalive.Text = Profile.SshKeepAliveSeconds.ToString();
            var letters = WindowsNetwork.FreeDriveLetters();
            if (!newProfile && !String.IsNullOrEmpty(Profile.DriveLetter) && !System.Linq.Enumerable.Contains(letters, Profile.DriveLetter))
                letters = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderByDescending(System.Linq.Enumerable.Concat(letters, new[] { Profile.DriveLetter }), x => x, StringComparer.OrdinalIgnoreCase));
            drive.ItemsSource = letters;
            drive.SelectedItem = newProfile ? System.Linq.Enumerable.FirstOrDefault(letters) : Profile.DriveLetter;
            protocol.SelectedItem = Profile.Protocol; protocol.SelectionChanged += delegate { Rebuild(true); };
            tls.Checked += delegate { tlsMode.IsEnabled = certificate.IsEnabled = true; };
            tls.Unchecked += delegate { tlsMode.IsEnabled = certificate.IsEnabled = false; };
            proxy.SelectionChanged += delegate { ProxyState(); };
            Rebuild(false);
            Loaded += delegate { QueueProfileFit(); };
            Tabs.SelectionChanged += delegate { QueueProfileFit(); };
            SizeChanged += delegate(object sender, SizeChangedEventArgs e) { if (e.WidthChanged) QueueProfileFit(); };
            LocationChanged += delegate { if (IsLoaded && !fittingProfile && (LayoutWorkArea ?? DialogGeometry.WorkArea(this)) != lastProfileWorkArea) QueueProfileFit(); };
            Loaded += delegate { name.Focus(); name.SelectAll(); };
        }
        static ScrollViewer Page(StackPanel panel)
        {
            panel.Margin = new Thickness(5); return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }
        static CheckBox Check(string text) { return new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(2, 5, 2, 4) }; }
        static ComboBox Choice(params string[] items) { return new ComboBox { ItemsSource = items, SelectedIndex = 0 }; }
        internal static void Row(StackPanel panel, string text, FrameworkElement field)
        {
            var grid = new Grid { Margin = new Thickness(2, 3, 2, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            grid.Children.Add(label); field.MinHeight = 28; Grid.SetColumn(field, 1); grid.Children.Add(field); panel.Children.Add(grid);
        }
        static void Note(StackPanel panel, string text) { panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 2, 5) }); }
        FrameworkElement Browse(TextBox field, bool folder)
        {
            var row = new DockPanel();
            var button = CopyUi.Button("»", delegate
            {
                if (folder)
                {
                    string chosen = FolderPickerWindow.Choose(this, field.Text);
                    if (chosen != null) field.Text = chosen;
                }
                else
                {
                    var chooser = new Microsoft.Win32.OpenFileDialog { CheckFileExists = true, Title = field == key ? L.Text("ssh.privateKeyPickerTitle") : L.Text("network.clientCertificatePickerTitle"), Filter = field == certificate ? L.Text("network.certificateFileFilter") : L.Text("common.allFilesFilter") };
                    if (chooser.ShowDialog(this) == true) field.Text = chooser.FileName;
                }
            });
            button.Width = 30; button.MinWidth = 0; button.Height = 28; button.Padding = new Thickness(4); button.Margin = new Thickness(3, 0, 0, 0);
            DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(field); return row;
        }
        void Rebuild(bool reset)
        {
            var type = (NetworkProtocol)protocol.SelectedItem; bool ftp = type == NetworkProtocol.FTP, ssh = type == NetworkProtocol.SSH,
                transfer = type == NetworkProtocol.SFTP || type == NetworkProtocol.SCP, network = type == NetworkProtocol.SMB || type == NetworkProtocol.NFS;
            bool sshfs = type == NetworkProtocol.SSHFS;
            // Detach controls from their old row containers before rebuilding protocol-specific pages.
            Detach(general); Detach(advanced); general.Children.Clear(); advanced.Children.Clear();
            Title = L.Text("network.connectionDetailsTitlePrefix") + type;
            if (reset) { host.Text = Profile.Host + (ftp ? ":21" : ssh || transfer || sshfs ? ":22" : ""); path.Text = type == NetworkProtocol.SMB ? "" : "/"; }
            var identity = FluentTheme.Section(general, L.Text("toolbar.connections"));
            Row(identity, L.Text("network.sessionNameLabel"), name); Row(identity, L.Text("network.profileProtocolLabel"), protocol); Row(identity, network ? "Hostitel:" : L.Text("network.hostAndPortLabel"), host);
            var credentials = type == NetworkProtocol.NFS ? null : FluentTheme.Section(general, L.Text("network.credentialsGroup"));
            if (ftp)
            {
                var tlsRow = new DockPanel(); var anonymous = CopyUi.Button(L.Text("ftp.anonymousLogin"), delegate { user.Text = ""; password.Password = ""; remember.IsChecked = false; });
                anonymous.Margin = new Thickness(0); DockPanel.SetDock(anonymous, Dock.Right); tlsRow.Children.Add(anonymous); tlsRow.Children.Add(tls); credentials.Children.Add(tlsRow);
            }
            if (type != NetworkProtocol.NFS)
            {
                Row(credentials, L.Text("network.usernameLabel"), user);
                if (!ssh) { Row(credentials, String.IsNullOrEmpty(Profile.EncryptedPassword) ? L.Text("network.passwordLabel") : L.Text("network.savedPasswordLabel"), password); credentials.Children.Add(remember); }
            }
            var locations = !ssh ? FluentTheme.Section(general, L.Text("network.foldersAndTransferGroup")) : null;
            if (!ssh) Row(locations, type == NetworkProtocol.SMB ? L.Text("smb.shareLabel") : type == NetworkProtocol.NFS ? L.Text("nfs.exportLabel") : L.Text("network.remoteFolderLabel"), path);
            if (!ssh) Row(locations, L.Text("network.localFolderLabel"), Browse(local, true));
            if (ftp) locations.Children.Add(passive);
            if (type == NetworkProtocol.NFS) { Row(locations, L.Text("network.driveLetterLabel"), drive); locations.Children.Add(anon); }
            if (sshfs) { Row(locations, L.Text("network.driveLetterLabel"), drive); locations.Children.Add(sshfsStartup); locations.Children.Add(sshfsKeep); }
            if (transfer || ssh || sshfs) Row(credentials, L.Text("ssh.privateKeyLabel"), Browse(key, false));
            if (ftp || transfer)
            {
                Row(locations, "Firewall / proxy:", proxy);
            }
            if (!network) Row(advanced, L.Text("network.timeoutSecondsLabel"), timeout);
            if (ftp)
            {
                Row(advanced, L.Text("ftp.commandsLabel"), commands);
                Row(advanced, L.Text("ftp.tlsModeLabel"), tlsMode); Row(advanced, L.Text("network.filenameEncodingLabel"), encoding);
                Row(advanced, L.Text("ftp.directoryListingLabel"), mlsd); Row(advanced, L.Text("ftp.hiddenFilesLabel"), hidden); Row(advanced, L.Text("network.clientCertificateLabel"), Browse(certificate, false));
                tlsMode.IsEnabled = certificate.IsEnabled = tls.IsChecked == true;
                Note(advanced, L.Text("ftp.serverDetectionHelp"));
            }
            if (transfer || ssh || sshfs)
            {
                Row(advanced, L.Text("ssh.hostKeyFingerprintLabel"), fingerprint);
                if (transfer) Row(advanced, L.Text("network.filenameEncodingLabel"), encoding);
                advanced.Children.Add(compression); if (!sshfs) advanced.Children.Add(agent);
                Row(advanced, L.Text("ssh.terminalShellLabel"), shell); advanced.Children.Add(terminal);
                Row(advanced, L.Text("ssh.keepAliveSecondsLabel"), keepalive);
                Note(advanced, L.Text("ssh.agentAndKeepAliveHelp"));
            }
            if (sshfs)
            {
                Note(advanced, SshfsMount.DependencyStatus() + L.Text("sshfs.credentialsAndPathsHelp"));
                advanced.Children.Add(CopyUi.Button(L.Text("sshfs.installSupportMenu"), delegate { SshfsSetupWindow.Open(this); }));
            }
            if (type == NetworkProtocol.SMB) Note(advanced, L.Text("smb.windowsCredentialsHelp"));
            if (type == NetworkProtocol.NFS) Note(advanced, L.Text("nfs.permissionsHelp"));
            if (!ssh) Note(advanced, L.Text("network.savedPasswordSecurityHelp"));
            if (type == NetworkProtocol.SCP) Note(advanced, L.Text("scp.serverCommandRequirement"));
            if (ftp || transfer)
            {
                var proxyFields = new StackPanel(); Row(proxyFields, L.Text("proxy.hostLabel"), proxyHost); Row(proxyFields, L.Text("proxy.portLabel"), proxyPort);
                Row(proxyFields, L.Text("proxy.usernameLabel"), proxyUser); Row(proxyFields, L.Text("proxy.passwordLabel"), proxyPassword);
                advanced.Children.Add(new Expander { Header = L.Text("proxy.settingsGroup"), Content = proxyFields, IsExpanded = proxy.SelectedIndex > 0, Margin = new Thickness(2, 5, 2, 3) });
            }
            var more = CopyUi.Button(L.Text("network.openAdvancedTab"), delegate { Tabs.SelectedIndex = 1; }); more.HorizontalAlignment = HorizontalAlignment.Right; general.Children.Add(more);
            ProxyState();
            var details = new StackPanel();
            foreach (UIElement child in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<UIElement>(advanced.Children))) { advanced.Children.Remove(child); details.Children.Add(child); }
            advanced.Children.Add(new GroupBox { Header = L.Text("network.protocolOptionsPrefix") + type, Content = details, Margin = new Thickness(0, 0, 0, 8) });
            ArrangeProfilePages(); QueueProfileFit();
        }
        static void Detach(DependencyObject node)
        {
            var group = node as GroupBox;
            if (group != null) { Detach(group.Content as DependencyObject); group.Content = null; return; }
            var expander = node as Expander;
            if (expander != null) { Detach(expander.Content as DependencyObject); expander.Content = null; return; }
            var panel = node as Panel;
            if (panel == null) return;
            foreach (UIElement child in new System.Collections.Generic.List<UIElement>(System.Linq.Enumerable.Cast<UIElement>(panel.Children))) Detach(child);
            panel.Children.Clear();
        }
        void ProxyState()
        {
            bool enabled = proxy.SelectedIndex > 0; proxyHost.IsEnabled = proxyPort.IsEnabled = proxyUser.IsEnabled = proxyPassword.IsEnabled = enabled;
        }
        internal static void ParseHost(string text, int defaultPort, out string hostname, out int port)
        {
            hostname = text.Trim(); port = defaultPort;
            if (hostname.StartsWith("["))
            {
                int end = hostname.IndexOf(']'); if (end < 0) throw new ArgumentException(L.Text("network.invalidIpv6Address"));
                string suffix = hostname.Substring(end + 1); hostname = hostname.Substring(1, end - 1);
                if (suffix.Length > 0 && (!suffix.StartsWith(":") || !Int32.TryParse(suffix.Substring(1), out port))) throw new ArgumentException(L.Text("network.invalidPort"));
            }
            else if (hostname.IndexOf(':') == hostname.LastIndexOf(':') && hostname.IndexOf(':') >= 0)
            {
                int split = hostname.LastIndexOf(':');
                if (!Int32.TryParse(hostname.Substring(split + 1), out port)) throw new ArgumentException(L.Text("network.invalidPort"));
                hostname = hostname.Substring(0, split);
            }
        }
        static string HostText(NetworkProfile source)
        {
            if (source.Protocol == NetworkProtocol.SMB || source.Protocol == NetworkProtocol.NFS) return source.Host;
            return (source.Host.IndexOf(':') >= 0 ? "[" + source.Host + "]" : source.Host) + ":" + source.Port;
        }
        internal NetworkProfile ReadProfile()
        {
            var result = Profile.Clone(); result.Name = name.Text.Trim(); result.Protocol = (NetworkProtocol)protocol.SelectedItem;
            if (result.Protocol == NetworkProtocol.SMB || result.Protocol == NetworkProtocol.NFS) result.Host = host.Text.Trim();
            else ParseHost(host.Text, result.Protocol == NetworkProtocol.FTP ? (tls.IsChecked == true && tlsMode.SelectedIndex == 1 ? 990 : 21) : 22, out result.Host, out result.Port);
            result.Path = path.Text.Trim(); result.LocalPath = local.Text.Trim(); result.UserName = user.Text.Trim();
            result.PrivateKeyPath = key.Text.Trim(); result.HostKeyFingerprint = fingerprint.Text.Trim();
            result.FtpTls = tls.IsChecked == true; result.FtpImplicitTls = tlsMode.SelectedIndex == 1; result.FtpPassive = passive.IsChecked == true;
            result.NfsAnonymous = anon.IsChecked == true; result.DriveLetter = drive.SelectedItem as string;
            result.TimeoutSeconds = Number(timeout.Text, L.Text("network.timeoutFieldName")); result.FilenameEncoding = encoding.SelectedIndex;
            result.FtpMlsd = mlsd.SelectedIndex; result.FtpHidden = hidden.SelectedIndex; result.FtpCommands = commands.Text; result.ClientCertificate = certificate.Text.Trim();
            result.ProxyMethod = proxy.SelectedIndex; result.ProxyHost = proxyHost.Text.Trim(); result.ProxyPort = Number(proxyPort.Text, L.Text("proxy.portFieldName")); result.ProxyUser = proxyUser.Text.Trim();
            result.EncryptedProxyPassword = result.ProxyMethod == 0 ? "" : proxyPassword.Password.Length == 0 ? Profile.EncryptedProxyPassword : NetworkSettingsStore.Protect(proxyPassword.Password);
            result.SshCompression = compression.IsChecked == true; result.SshAgent = agent.IsChecked == true; result.SshKeepAliveSeconds = Number(keepalive.Text, L.Text("ssh.keepAliveFieldName"));
            result.UseWindowsTerminal = terminal.IsChecked == true; result.UsePowerShell7 = shell.SelectedIndex == 1;
            result.SshfsConnectOnStartup = sshfsStartup.IsChecked == true; result.SshfsKeepMounted = sshfsKeep.IsChecked == true;
            result.EncryptedPassword = result.Protocol != NetworkProtocol.SSH && remember.IsChecked == true ? (password.Password.Length == 0 ? Profile.EncryptedPassword : NetworkSettingsStore.Protect(password.Password)) : "";
            result.Validate(); return result;
        }
        static int Number(string text, string label) { int value; if (!Int32.TryParse(text, out value)) throw new ArgumentException(label + L.Text("validation.integerRequiredSuffix")); return value; }
        void Accept()
        {
            try { Profile = ReadProfile(); DialogResult = true; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("validation.checkSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        void Help()
        {
            MessageBox.Show(this, L.Text("network.profileDialogHelp"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    internal sealed class FolderPickerWindow : Window
    {
        readonly TextBox path = new TextBox(); readonly ListBox list = new ListBox();
        FolderPickerWindow(Window owner, string initial)
        {
            CopyUi.Style(this, owner, L.Text("folderPicker.localFolderTitle"), 470);
            var panel = new StackPanel { Margin = new Thickness(10) }; Content = panel;
            var row = new DockPanel(); var go = CopyUi.Button(L.Text("action.open"), delegate { Load(path.Text); }); DockPanel.SetDock(go, Dock.Right); row.Children.Add(go); row.Children.Add(path); panel.Children.Add(row);
            list.Height = 235; panel.Children.Add(list);
            list.MouseDoubleClick += delegate { var selected = list.SelectedItem as string; if (selected != null) Load(selected); };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(CopyUi.Button(L.Text("action.upOneLevel"), delegate { var parent = Directory.GetParent(path.Text); if (parent != null) Load(parent.FullName); }));
            var ok = CopyUi.Button(L.Text("menu.selection"), delegate { if (Directory.Exists(path.Text)) DialogResult = true; }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel); panel.Children.Add(buttons);
            Load(Directory.Exists(initial) ? initial : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        void Load(string folder)
        {
            try { var dirs = Directory.GetDirectories(folder); path.Text = Path.GetFullPath(folder); list.ItemsSource = dirs; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("folderPicker.openFailedTitle")); }
        }
        internal static string Choose(Window owner, string initial) { var dialog = new FolderPickerWindow(owner, initial); return dialog.ShowDialog() == true ? dialog.path.Text : null; }
    }
}
