using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Net;
namespace FileCompass
{
    internal static class NetworkBrowserTests
    {
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr parameter,IntPtr value);
        internal static int Live(string root)
        {
            Directory.CreateDirectory(root);
            try { var task = NetworkBrowser.Read(NetworkBrowser.Root); NavigationTests.Wait(task);var until=DateTime.UtcNow.AddSeconds(40);while(NetworkBrowser.DiscoveryRunning&&DateTime.UtcNow<until)System.Threading.Thread.Sleep(100);var snapshot=NetworkBrowser.Snapshot(); File.WriteAllText(Path.Combine(root, "network-browser-live.txt"), "Devices=" + snapshot.Count + "; Computers=" + snapshot.Count(x=>NetworkBrowser.IsServer(x.FullPath)) + "; Shell devices=" + snapshot.Count(x=>x.NetworkDeviceTarget!=null) + "; candidate addresses="+NetworkDiscovery.CandidateCount+"; permission-denied probes="+NetworkDiscovery.AccessDeniedCount);
                var window=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                try{window.Show();NavigationTests.WaitPanels(window);var panel=FluentUiTests.Children<FilePanel>(window).First();panel.Navigate(NetworkBrowser.Root,true);NavigationTests.Wait(System.Threading.Tasks.Task.Delay(600));if(!panel.IsNetworkBrowser||!panel.List.IsEnabled||panel.List.Items.Count<snapshot.Count)throw new Exception("Cached discoveries were not shown immediately");NextStageTests.Draw(window,Path.Combine(root,"live-network.png"));panel.Navigate(root,false);NavigationTests.Wait(panel.NavigationTask);File.AppendAllText(Path.Combine(root,"network-browser-live.txt"),"; immediate interactive panel verified");}finally{window.Close();}
                return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "network-browser-live.txt"), ex.ToString()); return 1; }
        }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow window = null;
            Action<bool, string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                check(NetworkBrowser.Device("Computer","\\\\nas",null).NetworkDeviceTarget==null,"UNC computers remain share browsers");
                var device=NetworkBrowser.Device("Printer",@"::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}\Provider\printer",null);check(device!=null&&device.NetworkDeviceTarget!=null&&!NetworkBrowser.CanMap(device),"Non-UNC Shell devices are retained without becoming mappable folders");
                check(NetworkBrowser.Device("Untrusted","https://example.com",null)==null,"Discovery does not expose arbitrary URLs as executable devices");
                NetworkBrowser.TestProvider = delegate(string path) { return new List<Entry> { new Entry { Name = path == NetworkBrowser.Root ? "Test server" : "Data", FullPath = path == NetworkBrowser.Root ? "\\\\test-server" : "\\\\test-server\\Data", IsDirectory = true, Icon = DesktopIcons.FileIcon("", true) } }; };
                window = new MainWindow(root); window.Show(); NavigationTests.WaitPanels(window);
                var panel = FluentUiTests.Children<FilePanel>(window).First();
                panel.Navigate(NetworkBrowser.Root, true); NavigationTests.Wait(panel.NavigationTask);
                check(panel.IsNetworkBrowser && panel.CurrentPath == NetworkBrowser.Root && panel.List.Items.Count == 1, "network button destination displays devices in the file panel");
                panel.List.SelectedIndex = 0; panel.OpenSelected(); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == "\\\\test-server" && panel.List.Items.Count == 2, "opening a device enumerates shares and a parent entry");
                var share = panel.List.Items.Cast<Entry>().Single(x => !x.IsParent); panel.List.SelectedItem = share;
                check(NetworkBrowser.CanMap(share) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = "\\\\test-server" }) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = root }), "mapping accepts UNC shares and rejects devices and local directories");
                panel.List.ContextMenu.IsOpen = true; window.UpdateLayout();
                check(panel.List.ContextMenu.Items.OfType<MenuItem>().Any(x => (string)x.Header == L.Text("drive.mapFolderAsDrive") && x.Visibility == Visibility.Visible), "UNC folder context menu offers network drive mapping"); panel.List.ContextMenu.IsOpen = false;
                panel.Up(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == NetworkBrowser.Root, "parent navigation returns from device to network");
                panel.Back(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == "\\\\test-server", "network browsing participates in back history");
                panel.Navigate(root, true); NavigationTests.Wait(panel.NavigationTask); check(!panel.IsNetworkBrowser && panel.CurrentPath == root, "local navigation leaves network browsing cleanly");
                NetworkBrowser.TestProvider = delegate(string location) { System.Threading.Thread.Sleep(100); return new List<Entry>(); };
                panel.Navigate(NetworkBrowser.Root, true); var stale = panel.NavigationTask;
                panel.Navigate(root, true); NavigationTests.Wait(panel.NavigationTask); NavigationTests.Wait(stale);
                check(panel.CurrentPath == root && !panel.LocalLoading && panel.List.IsEnabled, "late network discovery cannot replace a newer local navigation");
                string literalFolder = "C:\\folder's $data"; var info = MainWindow.PowerShellStartInfo(literalFolder);
                string decoded = Encoding.Unicode.GetString(Convert.FromBase64String(info.Arguments.Split(' ').Last()));
                check(info.WorkingDirectory == literalFolder && decoded == "Set-Location -LiteralPath 'C:\\folder''s $data'", "PowerShell preserves literal folder names and never interpolates path source");
                var start=FluentUiTests.Children<MenuItem>(window).First(x=>(string)x.Header==L.Text("menu.start"));
                check(start.Items.OfType<MenuItem>().Select(x=>(string)x.Header).SequenceEqual(new[]{L.Text("action.openExplorer"),L.Text("action.runCommandPromptHere"),L.Text("action.runPowerShellHere")}),"Start contains Explorer, Command Prompt and PowerShell in order");
                check(!FluentUiTests.Children<MenuItem>(window).Any(x=>(string)x.Header==L.Text("menuGroup.external")),"obsolete external tools group is removed");
                var cmd=MainWindow.CommandPromptStartInfo("C:\\folder & data%\\");check(cmd.Arguments=="/D /K"&&cmd.WorkingDirectory=="C:\\folder & data%\\"&&cmd.FileName.EndsWith("\\cmd.exe"),"command prompt uses literal working directory without command injection");
                IntPtr hwnd=new WindowInteropHelper(window).Handle;check(SendMessage(hwnd,0x7f,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero&&SendMessage(hwnd,0x7f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero,"running window has both small and large native taskbar icons");
                ApplicationBrand.Apply(ApplicationBrand.Current);check(SendMessage(hwnd,0x7f,new IntPtr(1),IntPtr.Zero)!=IntPtr.Zero,"taskbar icon remains valid after changing appearance");
                check(NetworkDiscovery.LocalAddresses(IPAddress.Parse("10.0.10.100"),IPAddress.Parse("255.255.255.0")).Count()==253,"local subnet includes neighbors without probing own, network or broadcast address");
                check(NetworkDiscovery.LocalAddresses(IPAddress.Parse("10.0.10.100"),IPAddress.Parse("255.0.0.0")).Count()==253,"broad VPN prefixes cannot trigger an unbounded sweep");
                check(!NetworkDiscovery.LocalAddresses(IPAddress.Parse("100.64.1.2"),IPAddress.Parse("255.255.255.255")).Any()&&!NetworkDiscovery.LocalAddresses(IPAddress.Parse("8.8.8.8"),IPAddress.Parse("255.255.255.0")).Any(),"host routes and public ranges are not swept");
                check(NetworkDiscovery.Service("10.0.10.101",445).FullPath=="\\\\10.0.10.101","SMB endpoints browse real UNC shares");
                check(NetworkDiscovery.Service("10.0.10.101",22).NetworkServiceProfile.Protocol==NetworkProtocol.SFTP&&NetworkDiscovery.Service("10.0.10.101",21).NetworkServiceProfile.Protocol==NetworkProtocol.FTP,"SSH and FTP endpoints create the corresponding connection profiles");
                check(NetworkDiscovery.Service("10.0.10.101",990).NetworkServiceProfile.FtpImplicitTls&&NetworkDiscovery.Service("10.0.10.101",2049).NetworkServiceProfile.Protocol==NetworkProtocol.NFS,"FTPS and NFS service metadata is retained");
                check(NetworkDiscovery.Service("https://attacker",22)==null&&NetworkDiscovery.Service("10.0.10.101",80)==null,"arbitrary URLs and ordinary websites are not treated as file services");
                check(System.Threading.Tasks.Task.Run(()=>NetworkDiscovery.Service("10.0.10.101",22)).GetAwaiter().GetResult().Icon.IsFrozen,"background discoveries provide immutable cross-thread icons");
                check(FluentUiTests.Children<Button>(window).Any(x => (x.ToolTip as string) == L.Text("toolbar.compressShortcut")) && FluentUiTests.Children<Button>(window).Any(x => (x.ToolTip as string) == L.Text("toolbar.extractShortcut")), "compression and extraction are visible toolbar commands");
                NetworkTests.Render(window, Path.Combine(root, "main.png"));
                File.WriteAllLines(Path.Combine(root, "network-browser-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "network-browser-result.txt"), "FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { NetworkBrowser.TestProvider = null; if (window != null) window.Close(); }
        }
    }
}

