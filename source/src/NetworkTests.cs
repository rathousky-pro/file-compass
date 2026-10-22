using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CommanderCZ
{
    public static class NetworkTests
    {
        static void Check(bool value, string name) { if (!value) throw new Exception(name); }
        static void Reject(Action action, string name)
        {
            try { action(); } catch { return; } throw new Exception("Expected rejection: " + name);
        }
        internal static void Render(Window window, string path)
        {
            FluentTheme.Prepare(window);
            var content = (FrameworkElement)window.Content;
            var panel = content as Panel; if (panel != null) panel.Background = window.Background;
            var scroll = content as ScrollViewer; if (scroll != null) scroll.Background = window.Background;
            content.Measure(new Size(window.Width, 950));
            double height = Math.Ceiling(content.DesiredSize.Height);
            content.Arrange(new Rect(0, 0, window.Width, height)); content.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.Width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
        public static int Run(string workspace)
        {
            string root = Path.Combine(Path.GetFullPath(workspace), "network-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); var passed = new List<string>();
            Action<bool, string> assert = delegate(bool value, string text) { Check(value, text); passed.Add(text); };
            try
            {
                var store = new NetworkSettingsStore(Path.Combine(root, "connections.xml"));
                foreach (NetworkProtocol type in Enum.GetValues(typeof(NetworkProtocol)))
                {
                    var profile = new NetworkProfile { Name = "České připojení " + type, Protocol = type, Host = "server.example",
                        Port = type == NetworkProtocol.FTP ? 21 : 22, UserName = "tester", Path = type == NetworkProtocol.SMB ? "data\\fotky" : "/data",
                        EncryptedPassword = NetworkSettingsStore.Protect("tajné-heslo-123") };
                    store.Put(profile);
                }
                var restored = new NetworkSettingsStore(store.FilePath);
                assert(restored.Settings.Connections.Count == 7, "all seven protocols survive restart");
                assert(restored.Settings.Connections.All(x => x.Name.StartsWith("České")), "Czech text survives XML");
                assert(!File.ReadAllText(store.FilePath).Contains("tajné-heslo"), "no plaintext password in config");
                assert(restored.Settings.Connections.Where(x => x.Protocol != NetworkProtocol.SSH).All(x => NetworkSettingsStore.Unprotect(x.EncryptedPassword) == "tajné-heslo-123"), "DPAPI passwords round trip");
                assert(restored.Settings.Connections.Single(x => x.Protocol == NetworkProtocol.SSH).EncryptedPassword == "", "SSH password is not persisted");
                string legacy = Path.Combine(root, "legacy.xml");
                File.WriteAllText(legacy, File.ReadAllText(store.FilePath).Replace("<UseWindowsTerminal>true</UseWindowsTerminal>", "").Replace("<UsePowerShell7>false</UsePowerShell7>", ""));
                assert(new NetworkSettingsStore(legacy).Settings.Connections.All(x => x.UseWindowsTerminal && !x.UsePowerShell7), "legacy config loads default PowerShell and Terminal settings");
                var changed = restored.Settings.Connections[0].Clone(); changed.Name = "Upravené SMB"; restored.Put(changed);
                var detailed = restored.Settings.Connections.Single(x => x.Protocol == NetworkProtocol.FTP).Clone();
                detailed.FtpPassive = false; detailed.FtpTls = true; detailed.FtpImplicitTls = true; detailed.TimeoutSeconds = 45;
                detailed.FilenameEncoding = 1; detailed.FtpMlsd = 1; detailed.FtpHidden = 0; detailed.FtpCommands = "NOOP\nSYST";
                detailed.ProxyMethod = 2; detailed.ProxyHost = "proxy.example"; detailed.ProxyPort = 1080; detailed.ProxyUser = "proxy-user";
                detailed.EncryptedProxyPassword = NetworkSettingsStore.Protect("proxy-secret"); detailed.LocalPath = root;
                restored.Put(detailed);
                var detailedRestored = new NetworkSettingsStore(store.FilePath).Settings.Connections.Single(x => x.Protocol == NetworkProtocol.FTP);
                assert(!detailedRestored.FtpPassive && detailedRestored.FtpImplicitTls && detailedRestored.TimeoutSeconds == 45 && detailedRestored.LocalPath == root && detailedRestored.FtpCommands == "NOOP\nSYST", "FTP advanced options and local directory survive XML restart");
                assert(!File.ReadAllText(store.FilePath).Contains("proxy-secret") && NetworkSettingsStore.Unprotect(detailedRestored.EncryptedProxyPassword) == "proxy-secret", "proxy password stored using DPAPI");
                using (var connection = new RemoteConnection(detailedRestored))
                {
                    var options = connection.Options("test");
                    assert(options.FtpMode == CommanderCZ.NetworkTransport.FtpMode.Active && options.FtpSecure == CommanderCZ.NetworkTransport.FtpSecure.Implicit && options.Timeout == TimeSpan.FromSeconds(45), "FTP mode TLS and timeout forwarded to transport");
                    var raw = (Dictionary<string, string>)typeof(CommanderCZ.NetworkTransport.SessionOptions).GetProperty("RawSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(options, null);
                    assert(raw["FtpUseMlsd"] == "on" && raw["Utf"] == "1" && raw["PostLoginCommands"] == "NOOP\nSYST" && raw["ProxyHost"] == "proxy.example", "FTP commands encoding MLSD and proxy forwarded to transport");
                }
                string parsedHost; int parsedPort;
                NetworkProfileDialog.ParseHost("192.168.1.10:65000", 21, out parsedHost, out parsedPort);
                assert(parsedHost == "192.168.1.10" && parsedPort == 65000, "host colon port parsed correctly");
                NetworkProfileDialog.ParseHost("[2001:db8::1]:2200", 22, out parsedHost, out parsedPort);
                assert(parsedHost == "2001:db8::1" && parsedPort == 2200, "IPv6 host colon port parsed correctly");
                assert(WindowsNetwork.ValidateMapping("Z:", "\\\\server\\data") == "\\\\server\\data", "drive mapping UNC validation");
                Reject(delegate { WindowsNetwork.ValidateMapping("C:", "\\\\server\\data"); }, "system drive mapping");
                Reject(delegate { WindowsNetwork.ValidateMapping("Z:", "\\\\server"); }, "mapping without share");
                Reject(delegate { WindowsNetwork.ValidateMapping("Z:", "\\\\server\\data\\.."); }, "mapping traversal");
                passed.Add("invalid drive mappings rejected without changing Windows connections");
                assert(new NetworkSettingsStore(store.FilePath).Settings.Connections.Count == 7, "editing replaces profile by ID");
                restored.Remove(changed); assert(new NetworkSettingsStore(store.FilePath).Settings.Connections.Count == 6, "deletion persists");
                var scp = restored.Settings.Connections.Single(x => x.Protocol == NetworkProtocol.SCP);
                scp.HostKeyFingerprint = "ssh-rsa 2048 AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
                using (var connection = new RemoteConnection(scp))
                {
                    var options = connection.Options("test-secret");
                    assert(options.Protocol == CommanderCZ.NetworkTransport.Protocol.Scp, "SCP selects genuine SCP protocol without SFTP fallback");
                    assert(options.PortNumber == scp.Port && options.UserName == scp.UserName && options.Password == "test-secret" &&
                        options.SshHostKeyFingerprint == scp.HostKeyFingerprint, "SCP port, username, password and SSH host key passed to transport");
                    assert(connection.Display("/data") == "scp://server.example:22/data", "SCP panel address uses scp scheme");
                }
                scp.HostKeyFingerprint = "";
                using (var first = new RemoteConnection(scp)) assert(String.IsNullOrEmpty(first.Options("").SshHostKeyFingerprint), "first SCP connection leaves fingerprint for trust prompt");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.SCP, UserName = "tester", Port = 0 }.Validate(); }, "invalid SCP port");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.SCP, UserName = "" }.Validate(); }, "missing SCP user");
                passed.Add("SCP validates port and user");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.SFTP, Port = 0 }.Validate(); }, "invalid port");
                Reject(delegate { new NetworkProfile { Host = "server\" & x", Protocol = NetworkProtocol.NFS, Path = "/data" }.Validate(); }, "unsafe host");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.SMB, Path = "" }.Validate(); }, "missing share");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.NFS, Path = "/data", DriveLetter = "C:" }.Validate(); }, "system disk");
                Reject(delegate { new NetworkProfile { Host = "server", Protocol = NetworkProtocol.SFTP, HostKeyFingerprint = "*" }.Validate(); }, "wildcard trust");
                Reject(delegate { RemoteConnection.ValidateRemoteName("../bad"); }, "remote traversal");
                passed.Add("invalid ports, hosts, shares, disk letters, SSH trust and traversal rejected");
                assert(RemoteConnection.Normalize("/a/../b//./česká/") == "/b/česká", "remote POSIX path normalization");
                assert(RemoteConnection.Parent("/") == "/" && RemoteConnection.Parent("/a/b") == "/a", "remote root and parent navigation");
                string malformed = Path.Combine(root, "malformed.xml"); File.WriteAllText(malformed, "<broken>");
                Reject(delegate { new NetworkSettingsStore(malformed); }, "malformed config");
                assert(File.ReadAllText(malformed) == "<broken>", "malformed config remains intact");
                string dtd = Path.Combine(root, "dtd.xml"); File.WriteAllText(dtd, "<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///missing'>]><x>&x;</x>");
                Reject(delegate { new NetworkSettingsStore(dtd); }, "XML external entity");
                passed.Add("DTD/external entities rejected");
                var main = new MainWindow(root);
                assert(main.FontFamily.Source == "Segoe UI" && main.FontSize == 12, "main font Segoe UI 9pt");
                var panels = ((DockPanel)main.Content).Children.OfType<Grid>().Single().Children.OfType<FilePanel>().ToArray();
                assert(panels.All(x => x.List.FontFamily.Source == "Segoe UI" && x.List.FontSize == 12), "both file lists Segoe UI 9pt");
                var menus = FindMenus((DependencyObject)main.Content).SelectMany(x => x.Items.OfType<MenuItem>()).ToArray();
                var network = menus.First(x => (string)x.Header == L.Text("connections.title"));
                assert(network.Items.OfType<MenuItem>().Count() == 5 && network.Items.OfType<MenuItem>().All(x => x.IsEnabled) && !network.Items.OfType<MenuItem>().Any(x => Enum.GetValues(typeof(NetworkProtocol)).Cast<NetworkProtocol>().Any(p => x.Header.ToString() == p + " připojení…")), "network menu groups connection management without individual protocols");
                main.Show(); main.Hide();
                var manager = new NetworkManagerDialog(main, restored, null);
                manager.SelectFilter(NetworkProtocol.FTP); assert(FluentUiTests.Children<ListBox>(manager).Single().Items.Cast<NetworkProfile>().All(x => x.Protocol == NetworkProtocol.FTP), "FTP filter excludes every other protocol");
                manager.SelectFilter(NetworkProtocol.SSHFS); assert(FluentUiTests.Children<ListBox>(manager).Single().Items.Count == 1, "SSHFS filter shows saved SSHFS profile");
                manager.SelectFilter(NetworkProtocol.SMB); assert(FluentUiTests.Children<ListBox>(manager).Single().Items.Count == 0, "empty protocol filter shows no unrelated connection");
                manager.SelectFilter(null); assert(FluentUiTests.Children<ListBox>(manager).Single().Items.Count == restored.Settings.Connections.Count, "all filter restores complete list");
                assert(manager.FontFamily.Source == "Segoe UI" && manager.FontSize == 12, "unified dialog font Segoe UI 9pt");
                Render(manager, Path.Combine(workspace, "network-manager.png")); manager.Close();
                foreach (NetworkProtocol type in Enum.GetValues(typeof(NetworkProtocol)))
                {
                    var editor = new NetworkProfileDialog(main, new NetworkProfile { Name = "Domácí server", Host = "192.168.1.10", Protocol = type, Port = type == NetworkProtocol.FTP ? 21 : 22, UserName = "tester", Path = type == NetworkProtocol.SMB ? "data" : "/data" });
                    assert(editor.ReadProfile().Protocol == type, "editor reads valid " + type + " profile");
                    Render(editor, Path.Combine(workspace, "network-" + type + ".png"));
                    editor.Tabs.SelectedIndex = 1;
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
                    Render(editor, Path.Combine(workspace, "network-" + type + "-advanced.png")); editor.Close();
                }
                var switchEditor = new NetworkProfileDialog(main, new NetworkProfile { Protocol = NetworkProtocol.FTP, Host = "server", UserName = "tester" });
                foreach (NetworkProtocol type in Enum.GetValues(typeof(NetworkProtocol))) switchEditor.SelectProtocol(type);
                assert(switchEditor.ReadProfile().Protocol == NetworkProtocol.SSHFS, "protocol switching rebuilds tabs without retaining parent containers"); switchEditor.Close();
                var mapping = new MapNetworkDriveDialog(main, "\\\\server\\data"); Render(mapping, Path.Combine(workspace, "network-map-drive.png")); mapping.Close();
                passed.Add("network manager and six editors rendered");
                var copyDialog = new NetworkCopyDialog(main, 4, "sftp://server:22/data", "C:\\Cílová složka");
                Render(copyDialog, Path.Combine(workspace, "network-copy-options.png")); copyDialog.Close();
                passed.Add("network copy skip and overwrite dialog rendered");
                var progressPreview = new Window(); CopyUi.Style(progressPreview, main, "Průběh kopírování", 700);
                var activity = new CopyActivity("SFTP · video.mkv · Soubor 65% · Celkem 30% · 12,40 MB/s", delegate { });
                activity.Update("SFTP · video.mkv · Soubor 65% · Celkem 30% · 12,40 MB/s", .65, .30);
                progressPreview.Content = activity; Render(progressPreview, Path.Combine(workspace, "dual-progress.png")); progressPreview.Close();
                var diskRow = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
                foreach (string drive in new[] { "q", "x", "y", "z" })
                    diskRow.Children.Add(CompactTheme.Button(FilePanel.DriveLabel(drive + ":\\", true, drive != "q"), drive, delegate { }));
                var places = new StackPanel { Orientation = Orientation.Horizontal }; places.Children.Add(DesktopIcons.Toolbar("network", 16)); places.Children.Add(new TextBlock { Text = "\\" });
                diskRow.Children.Add(CompactTheme.Button(places, "Síť", delegate { }));
                foreach (string protocol in new[] { "FTP", "SFTP", "SMB", "NFS", "SCP" })
                { var label = new StackPanel { Orientation = Orientation.Horizontal }; label.Children.Add(CompactTheme.DriveIcon(true)); label.Children.Add(new TextBlock { Text = protocol }); diskRow.Children.Add(CompactTheme.Button(label, protocol, delegate { })); }
                var diskPreview = new Window { Width = 500, Content = diskRow, Background = Brushes.White };
                Render(diskPreview, Path.Combine(workspace, "drive-icons.png")); diskPreview.Close();
                passed.Add("connected and disconnected network disk icons rendered");
                main.Close();
                File.WriteAllLines(Path.Combine(workspace, "network-test-result.txt"), passed.Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "network-test-result.txt"), "FAIL: " + ex); return 1; }
            finally
            {
                if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(workspace).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Test cleanup outside workspace.");
                Directory.Delete(root, true);
            }
        }
        static IEnumerable<Menu> FindMenus(DependencyObject obj)
        {
            var menu = obj as Menu; if (menu != null) yield return menu;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                foreach (var child in FindMenus(VisualTreeHelper.GetChild(obj, i))) yield return child;
            var panel = obj as Panel;
            if (VisualTreeHelper.GetChildrenCount(obj) == 0 && panel != null)
                foreach (UIElement child in panel.Children) foreach (var item in FindMenus(child)) yield return item;
        }
        static void Wait(Task task)
        {
            while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
            task.GetAwaiter().GetResult();
        }
        static void WaitPanel(FilePanel panel)
        {
            var limit = DateTime.UtcNow.AddSeconds(30);
            while (panel.IsLoading && DateTime.UtcNow < limit) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
            Check(!panel.IsLoading, "panel navigation timeout");
        }
        public static int Ftp(string workspace, int port, bool sftp = false, int ftpPort = 0)
        {
            Directory.CreateDirectory(workspace);
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var passed = new List<string>();
            Action<bool, string> assert = delegate(bool value, string name) { Check(value, name); passed.Add(name); };
            RemoteConnection connection = null; FilePanel panel = null;
            try
            {
                connection = new RemoteConnection(new NetworkProfile { Protocol = sftp ? NetworkProtocol.SFTP : NetworkProtocol.FTP, Host = "127.0.0.1", Port = port, UserName = "tester", Path = "/" });
                var options = connection.Options("test-password");
                if (sftp)
                {
                    string fingerprint = connection.Session.ScanFingerprint(options, "SHA-256");
                    assert(!String.IsNullOrWhiteSpace(fingerprint), "SFTP server fingerprint obtained");
                    options.SshHostKeyFingerprint = fingerprint;
                    using (var wrong = new RemoteConnection(connection.Profile))
                    {
                        var wrongOptions = wrong.Options("test-password");
                        wrongOptions.SshHostKeyFingerprint = "ssh-rsa 2048 AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
                        bool rejected = false;
                        try { wrong.Session.Open(wrongOptions); } catch (CommanderCZ.NetworkTransport.SessionRemoteException) { rejected = true; }
                        assert(rejected, "SFTP host key mismatch rejected during connection");
                    }
                    passed.Add("SFTP changed server key rejected");
                }
                connection.Open(options);
                if (!sftp)
                {
                    var activeFtp = connection.Profile.Clone(); activeFtp.FtpPassive = false; activeFtp.FtpMlsd = 1; activeFtp.FilenameEncoding = 1; activeFtp.FtpCommands = "NOOP\nSYST";
                    using (var probe = new RemoteConnection(activeFtp))
                    {
                        probe.Open(probe.Options("test-password"));
                        assert(probe.Session.ListDirectory("/").Files.Any(x => x.Name == "český soubor.txt"), "real active FTP with UTF-8 MLSD and post-login commands");
                    }
                }
                panel = new FilePanel(delegate { }); panel.Navigate(workspace, false); NavigationTests.Wait(panel.NavigationTask);
                Wait(panel.AttachRemote(connection)); connection = null;
                assert(panel.IsRemote && panel.CurrentPath == "/", "real FTP connection and remote panel");
                var entry = panel.List.Items.Cast<Entry>().Single(x => x.FullPath == "/český soubor.txt");
                assert(entry.Bytes > 0 && entry.Extension == "txt", "FTP Czech file listing and metadata");
                entry.IsMarked = true; panel.Refresh(); WaitPanel(panel);
                assert(panel.Selected().Single().FullPath == entry.FullPath, "FTP refresh preserves marks");
                panel.Navigate("/sub", true); WaitPanel(panel); assert(panel.CurrentPath == "/sub", "FTP subfolder navigation");
                panel.Back(); WaitPanel(panel); assert(panel.CurrentPath == "/", "FTP back history");
                panel.Forward(); WaitPanel(panel); assert(panel.CurrentPath == "/sub", "FTP forward history");
                panel.Up(); WaitPanel(panel); assert(panel.CurrentPath == "/", "FTP parent navigation");
                var browsing = panel.Remote;
                assert(panel.connectionButtons.Children.Count == 1, "connected server appears after disk icons");
                panel.Navigate(workspace, true); NavigationTests.Wait(panel.NavigationTask);
                assert(!panel.IsRemote && panel.connectionButtons.Children.Count == 1, "server icon remains when browsing a local disk");
                var connectionButton = panel.connectionButtons.Children.Cast<Button>().Single(x => x.Tag == browsing);
                connectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); WaitPanel(panel);
                assert(panel.IsRemote && panel.Remote == browsing && panel.CurrentPath == "/", "server icon reopens the existing connection and folder");
                if (!sftp) {
                    panel.Navigate("/sub", true); WaitPanel(panel);
                    using (var iconUpload = browsing.TransferConnection()) {
                        iconUpload.OpenTransfer();
                        string source = Path.Combine(workspace, "icon-upload.bin"); File.WriteAllBytes(source, new byte[8388608]);
                        long uploaded = 0; iconUpload.Session.FileTransferProgress += delegate(object sender, CommanderCZ.NetworkTransport.FileTransferProgressEventArgs e) { System.Threading.Interlocked.Exchange(ref uploaded, (long)(e.FileProgress * 8388608)); };
                        var uploading = System.Threading.Tasks.Task.Run(delegate { iconUpload.Session.PutFiles(source, "/icon-upload.bin", false, new CommanderCZ.NetworkTransport.TransferOptions()).Check(); });
                        var deadline = DateTime.UtcNow.AddSeconds(15);
                        while (System.Threading.Interlocked.Read(ref uploaded) == 0 && !uploading.IsCompleted && DateTime.UtcNow < deadline) { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { })); System.Threading.Thread.Sleep(10); }
                        assert(!uploading.IsCompleted && uploaded > 0, "FTP upload is running before browser reconnect test");
                        var previous = browsing.Session;
                        try { browsing.Session.CreateDirectory("/__deny_cwd_for_test__"); } catch (IOException) { }
                        panel.Navigate(workspace, false); NavigationTests.Wait(panel.NavigationTask);
                        Wait(panel.SwitchConnectionTask(browsing));
                        assert(panel.IsRemote && panel.Remote == browsing && panel.CurrentPath == "/sub" && browsing.Session != previous && !uploading.IsCompleted, "FTP icon returns from local disk through a fresh browser session while upload continues");
                        Wait(uploading);
                        assert(browsing.Session.GetFileInfo("/icon-upload.bin").Length == 8388608, "browser recovery leaves FTP upload complete and intact");
                        browsing.Session.RemoveFiles("/icon-upload.bin");
                    }
                    browsing.Session.CreateDirectory("/icon-removed"); panel.Navigate("/icon-removed", true); WaitPanel(panel);
                    panel.Navigate(workspace, false); NavigationTests.Wait(panel.NavigationTask);
                    using (var removing = browsing.TransferConnection()) { removing.OpenTransfer(); removing.Session.RemoveFiles("/icon-removed"); }
                    string rootPrompt = null;
                    Wait(panel.SwitchConnectionTask(browsing, delegate(string detail) { rootPrompt = detail; return false; }));
                    assert(!panel.IsRemote && panel.CurrentPath == workspace && rootPrompt.Contains("/icon-removed"), "declining FTP root fallback preserves local panel and connection icon");
                    Wait(panel.SwitchConnectionTask(browsing, delegate(string detail) { rootPrompt = detail; return true; }));
                    assert(panel.IsRemote && panel.Remote == browsing && panel.CurrentPath == "/" && rootPrompt.Contains("/icon-removed") && rootPrompt.Contains("550"), "unavailable FTP folder offers root with exact path and server reply");
                }
                panel.NewTab(); panel.Navigate("/sub", true); WaitPanel(panel);
                panel.SwitchTab(0); Wait(panel.TabNavigationTask);
                assert(panel.Remote == browsing && panel.CurrentPath == "/" && panel.TabCount == 2, "remote tabs restore original connection and root");
                panel.SwitchTab(1); Wait(panel.TabNavigationTask);
                assert(panel.Remote == browsing && panel.CurrentPath == "/sub", "second remote tab retains its folder");
                var remoteTabs = panel.SaveTabs(); assert(remoteTabs[1].ProfileId == browsing.Profile.Id && remoteTabs[1].Path == "/sub", "saved remote tabs reference profile without credentials");
                panel.CloseTab(1); Wait(panel.TabNavigationTask);
                assert(panel.TabCount == 1 && panel.Remote == browsing && panel.CurrentPath == "/" && browsing.Session.FileExists("/sub"), "closing remote tab keeps shared connection usable");
                var secondConnection = browsing.TransferConnection(); secondConnection.Profile.Id = Guid.NewGuid().ToString("N"); secondConnection.Profile.Name = "Druhý server"; secondConnection.OpenTransfer();
                Wait(panel.AttachRemote(secondConnection));
                assert(panel.connectionButtons.Children.Count == 2, "multiple connected servers have separate disk-bar icons");
                panel.connectionButtons.Children.Cast<Button>().Single(x => x.Tag == browsing).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); WaitPanel(panel);
                var otherIcon = panel.connectionButtons.Children.Cast<Button>().Single(x => x.Tag == secondConnection);
                otherIcon.ContextMenu.Items.Cast<MenuItem>().Single().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                assert(panel.Remote == browsing && panel.connectionButtons.Children.Count == 1 && browsing.Session.FileExists("/sub"), "disconnect inactive server preserves active connection");
                panel.Remote.Session.CreateDirectory("/created"); panel.Remote.Session.MoveFile("/created", "/renamed");
                assert(panel.Remote.Session.FileExists("/renamed"), "FTP mkdir and rename");
                string local = Path.Combine(workspace, "upload.txt"); File.WriteAllText(local, "Příliš žluťoučký kůň");
                string lockedUpload = Path.Combine(workspace, "locked-upload.txt"); File.WriteAllText(lockedUpload, "LOCKED UPLOAD");
                using (var lockedFile = new FileStream(lockedUpload, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    int prompts = 0; var lockedResult = new NetworkCopyResult();
                    NetworkTransfers.Copy(new Entry { FullPath = lockedUpload }, null, panel.Remote, "/", NetworkCopyRule.Overwrite, lockedResult, null, null, null,
                        failure => { prompts++; return CopyErrorChoice.Skip; });
                    assert(prompts == 1 && lockedResult.Skipped == 1 && panel.Remote.Session.FileExists("/sub"), "network upload locked file offers skip and preserves browsing session");
                    NetworkTransfers.Copy(new Entry { FullPath = lockedUpload }, null, panel.Remote, "/", NetworkCopyRule.Overwrite, lockedResult, null, null, null,
                        failure => { lockedFile.Dispose(); return CopyErrorChoice.Retry; });
                    assert(lockedResult.Copied == 1 && panel.Remote.Session.GetFileInfo("/locked-upload.txt").Length == new FileInfo(lockedUpload).Length, "network upload retries after source lock release");
                }
                var upload = new Entry { FullPath = local };
                NetworkTransfers.Copy(upload, null, panel.Remote, "/");
                File.WriteAllText(local, "Changed local file");
                Reject(delegate { NetworkTransfers.Copy(upload, null, panel.Remote, "/"); }, "remote collision");
                string download = Path.Combine(workspace, "download.txt");
                panel.Remote.Session.GetFiles("/upload.txt", download).Check();
                assert(File.ReadAllText(download) == "Příliš žluťoučký kůň", "real FTP upload exact content and collision preserves original");
                var downloadEntry = new Entry { FullPath = "/upload.txt" };
                string downloadTarget = Directory.CreateDirectory(Path.Combine(workspace, "f5-download")).FullName;
                NetworkTransfers.Copy(downloadEntry, panel.Remote, null, downloadTarget);
                string downloaded = Path.Combine(downloadTarget, "upload.txt");
                assert(File.ReadAllText(downloaded) == "Příliš žluťoučký kůň", "real F5 FTP download exact content");
                File.WriteAllText(downloaded, "Preserve existing");
                Reject(delegate { NetworkTransfers.Copy(downloadEntry, panel.Remote, null, downloadTarget); }, "local collision");
                assert(File.ReadAllText(downloaded) == "Preserve existing", "FTP download collision preserves existing file");
                string folder = Directory.CreateDirectory(Path.Combine(workspace, "directory upload")).FullName;
                File.WriteAllText(Path.Combine(folder, "file.txt"), "nested");
                NetworkTransfers.Copy(new Entry { FullPath = folder, IsDirectory = true }, null, panel.Remote, "/");
                assert(panel.Remote.Session.FileExists("/directory upload/file.txt"), "real recursive FTP directory upload");
                var measuredFolder = new Entry { FullPath = "/directory upload", IsDirectory = true };
                Wait(panel.CalculateFolderSize(measuredFolder));
                assert(measuredFolder.SizeKnown && measuredFolder.Bytes == new FileInfo(Path.Combine(folder, "file.txt")).Length, "FTP directory size uses independent session and exact recursive byte count");
                using (var searchConnection = panel.Remote.TransferConnection())
                {
                    searchConnection.OpenTransfer(); var found = new List<SearchHit>();
                    FileSearchEngine.Remote(new FileSearchOptions { Root = "/directory upload", Mask = "*.txt" }, new string[0], searchConnection, System.Threading.CancellationToken.None, found.Add);
                    assert(found.Count == 1 && found[0].Path == "/directory upload/file.txt", "FTP filename search finds exact recursive path on independent session");
                }
                string folderTarget = Directory.CreateDirectory(Path.Combine(workspace, "download-folder")).FullName;
                NetworkTransfers.Copy(new Entry { FullPath = "/directory upload", IsDirectory = true }, panel.Remote, null, folderTarget);
                assert(File.ReadAllText(Path.Combine(workspace, "download-folder", "directory upload", "file.txt")) == "nested", "real recursive FTP directory download");
                File.WriteAllText(Path.Combine(folder, "file.txt"), "replacement");
                File.WriteAllText(Path.Combine(folder, "new.txt"), "new file");
                var skipResult = new NetworkCopyResult();
                NetworkTransfers.Copy(new Entry { FullPath = folder, IsDirectory = true }, null, panel.Remote, "/", NetworkCopyRule.Skip, skipResult);
                panel.Remote.Session.GetFiles("/directory upload/file.txt", download).Check();
                assert(File.ReadAllText(download) == "nested" && panel.Remote.Session.FileExists("/directory upload/new.txt") && skipResult.Skipped == 1 && skipResult.Copied == 1, "upload skip merges folder, preserves existing and copies new file");
                NetworkTransfers.Copy(new Entry { FullPath = folder, IsDirectory = true }, null, panel.Remote, "/", NetworkCopyRule.Overwrite);
                panel.Remote.Session.GetFiles("/directory upload/file.txt", download).Check();
                assert(File.ReadAllText(download) == "replacement", "upload overwrite replaces existing nested content");
                var downloadSkip = new NetworkCopyResult();
                NetworkTransfers.Copy(new Entry { FullPath = "/directory upload", IsDirectory = true }, panel.Remote, null, folderTarget, NetworkCopyRule.Skip, downloadSkip);
                assert(File.ReadAllText(Path.Combine(folderTarget, "directory upload", "file.txt")) == "nested" && File.ReadAllText(Path.Combine(folderTarget, "directory upload", "new.txt")) == "new file" && downloadSkip.Skipped == 1, "download skip merges folder, preserves existing and copies new file");
                NetworkTransfers.Copy(new Entry { FullPath = "/directory upload", IsDirectory = true }, panel.Remote, null, folderTarget, NetworkCopyRule.Overwrite);
                assert(File.ReadAllText(Path.Combine(folderTarget, "directory upload", "file.txt")) == "replacement", "download overwrite replaces existing nested content");
                if (sftp)
                {
                    using (var cancellation = new System.Threading.CancellationTokenSource())
                    using (var transfer = panel.Remote.TransferConnection())
                    {
                        string cancelledTarget = Directory.CreateDirectory(Path.Combine(workspace, "cancelled-target")).FullName;
                        bool cancelled = false;
                        var updates = new Progress<NetworkCopyUpdate>(delegate(NetworkCopyUpdate u) { if (u.Total > 0 && u.Total < 1) cancellation.Cancel(); });
                        var task = System.Threading.Tasks.Task.Run(delegate
                        {
                            try { NetworkCopyWorker.Run(transfer, new[] { new Entry { FullPath = "/slow.bin" } }, true, cancelledTarget, cancellation.Token, updates); }
                            catch (OperationCanceledException) { cancelled = true; }
                        });
                        Wait(task); assert(cancelled, "SFTP cancellation interrupts transfer");
                        assert(panel.Remote.Session.FileExists("/slow.bin"), "SFTP cancellation preserves browsing connection and source");
                    }
                    var independent = panel.Remote.TransferConnection(); independent.OpenTransfer();
                    RemoteConnection concurrentFtp = null;
                    if (ftpPort > 0)
                    {
                        concurrentFtp = new RemoteConnection(new NetworkProfile { Name = "FTP souběžně", Protocol = NetworkProtocol.FTP, Host = "127.0.0.1", Port = ftpPort, UserName = "tester", Path = "/" });
                        concurrentFtp.Open(concurrentFtp.Options("test-password"));
                    }
                    var window = new MainWindow(workspace) { Left = -20000, Top = -20000, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual }; window.Show(); NavigationTests.WaitPanels(window);
                    try { Wait(window.VerifyBackgroundCopy(independent, workspace, concurrentFtp)); }
                    finally { if (concurrentFtp != null) concurrentFtp.Dispose(); }
                    window.Close();
                    passed.Add("SFTP live file and total progress, responsive UI, navigation, disconnect, swap, fixed destination");
                    passed.Add("SFTP running queue expands by a folder with nested files, grows total bytes, preserves append overwrite rule and ignores duplicate roots");
                    if (ftpPort > 0) { passed.Add("FTP and SFTP transfers run concurrently with separate jobs in one queue window"); passed.Add("FTP upload queue expands during transfer and new queue cancellation preserves browsing connection"); }
                }
                using (var deleting = panel.Remote.TransferConnection())
                {
                    deleting.OpenTransfer();
                    string deleteSource = Directory.CreateDirectory(Path.Combine(workspace, "delete-source")).FullName;
                    string literal = Path.Combine(deleteSource, "literal[1].txt"), neighbour = Path.Combine(deleteSource, "literal1.txt");
                    File.WriteAllText(literal, "remove only this"); File.WriteAllText(neighbour, "keep neighbour");
                    NetworkTransfers.Copy(new Entry { FullPath = literal }, null, deleting, "/", NetworkCopyRule.Overwrite);
                    assert(deleting.Session.FileExists("/literal[1].txt"), "FTP uploads local literal brackets in filename");
                    deleting.Session.PutFiles(CommanderCZ.NetworkTransport.RemotePath.EscapeFileMask(neighbour), "/literal1.txt").Check();
                    DeleteOperations.Remote(deleting.Session, "/literal[1].txt");
                    assert(!deleting.Session.FileExists("/literal[1].txt") && deleting.Session.FileExists("/literal1.txt"), "FTP delete escapes literal filename and preserves neighbour");
                    deleting.Session.CreateDirectory("/delete-folder");
                    deleting.Session.CreateDirectory("/delete-folder/nested");
                    deleting.Session.PutFiles(CommanderCZ.NetworkTransport.RemotePath.EscapeFileMask(neighbour), "/delete-folder/nested/item.txt").Check();
                    DeleteOperations.Remote(deleting.Session, "/delete-folder");
                    assert(!deleting.Session.FileExists("/delete-folder") && panel.Remote.Session.FileExists("/literal1.txt"), "FTP deletes nonempty directory on independent session and browsing stays usable");
                    Reject(delegate { DeleteOperations.Remote(deleting.Session, "/"); }, "remote delete refuses root");
                    passed.Add("FTP delete refuses connection root");
                }
                var preview = new Window { Width = 800, Height = 420, Content = panel }; Render(preview, Path.Combine(workspace, "connected-panel.png")); preview.Content = null; preview.Close();
                var connectedButton = panel.connectionButtons.Children.Cast<Button>().Single(x => x.Tag == panel.Remote);
                assert(((StackPanel)connectedButton.Content).Children.OfType<TextBlock>().Single().Text == panel.Remote.Profile.Protocol.ToString(), "server button shows protocol only");
                var disconnectItem = connectedButton.ContextMenu.Items.Cast<MenuItem>().Single();
                assert((string)disconnectItem.Header == "Odpojit", "server icon context menu offers disconnect");
                disconnectItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                NavigationTests.Wait(panel.NavigationTask); assert(!panel.IsRemote && panel.CurrentPath == Path.GetFullPath(workspace) && panel.connectionButtons.Children.Count == 0, "FTP context-menu disconnect removes icon and returns to local folder");
                File.WriteAllLines(Path.Combine(workspace, sftp ? "sftp-test-result.txt" : "ftp-test-result.txt"), passed.Select(x => sftp && !x.StartsWith("FTP and SFTP") && !x.StartsWith("FTP upload queue") ? x.Replace("SFTP", "FTP").Replace("FTP", "SFTP") : x).Concat(new[] { "PASS: " + passed.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, sftp ? "sftp-test-result.txt" : "ftp-test-result.txt"), "FAIL: " + ex); return 1; }
            finally { if (connection != null) connection.Dispose(); if (panel != null && panel.Remote != null) panel.Remote.Dispose(); }
        }
    }
}



