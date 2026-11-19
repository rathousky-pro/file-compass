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
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
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
                var ranges=FluentUiTests.Children<TextBox>(panel).Where(x=>System.Windows.Automation.AutomationProperties.GetName(x)==L.Text("networkScan.range")).ToArray();
                check(ranges.Length==1&&ranges[0].IsVisible,"network panel exposes custom range controls");
                panel.RefreshNetworkTree(new[]{NetworkDiscovery.Service("10.0.10.5",445),NetworkDiscovery.Service("10.0.20.5",22)});
                var subnetTree=FluentUiTests.Children<TreeView>(panel).Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)==L.Text("networkScan.tree"));
                check(subnetTree.Items.OfType<TreeViewItem>().Any(x=>Equals(x.Header,"10.0.10.x"))&&subnetTree.Items.OfType<TreeViewItem>().Any(x=>Equals(x.Header,"10.0.20.x")),"network tree groups discovered endpoints by IPv4 subnet");
                var bareHost=subnetTree.Items.OfType<TreeViewItem>().SelectMany(x=>x.Items.OfType<TreeViewItem>()).Single(x=>(x.Tag as Entry)?.FullPath=="\\\\10.0.10.5");
                check(!bareHost.HasItems, "Unidentified addresses have no protocol children");
                bareHost.IsSelected=true;bool openedDialog=false;
                var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
                timer.Tick+=delegate
                {
                    var dialog=window.OwnedWindows.OfType<NetworkProfileDialog>().FirstOrDefault();
                    if(dialog==null)return;
                    var selector=FluentUiTests.Children<ComboBox>(dialog).FirstOrDefault(combo=>combo.Items.Contains(NetworkProtocol.SFTP)&&combo.Items.Contains(NetworkProtocol.SSH)&&combo.Items.Contains(NetworkProtocol.SMB));
                    openedDialog=dialog.Profile.Host=="10.0.10.5"&&selector!=null;
                    timer.Stop();dialog.Close();
                };
                timer.Start();
                var enter=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Enter){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};subnetTree.RaiseEvent(enter);timer.Stop();
                check(enter.Handled&&openedDialog,"Opening an unidentified address shows the connection protocol selector");
                check(subnetTree.IsVisible && panel.List.Visibility == Visibility.Collapsed && subnetTree.Parent == panel.List.Parent,"network tree replaces the device list in the panel");
                var scanButton=FluentUiTests.Children<Button>(panel).Single(x=>Equals(x.Content,L.Text("networkScan.start")));
                var closeScan=FluentUiTests.Children<Button>(panel).Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)==L.Text("tabs.close"));
                check(FluentUiTests.Children<Button>(panel).Where(x=>new[]{"PanelRoot","PanelUp"}.Contains(System.Windows.Automation.AutomationProperties.GetAutomationId(x))).All(x=>!x.IsVisible),"network root hides parent and root buttons");
                ranges[0].Text="10.0.10.0/24";panel.NetworkRangeProbeForTests=async (host,port,timeout)=>{await System.Threading.Tasks.Task.Delay(150);return false;};
                var running=panel.StartNetworkRangeScan();check(Equals(scanButton.Content,L.Text("networkScan.stop")),"the search button becomes the stop button while scanning");
                var escape=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,System.Windows.Input.Key.Escape){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};window.RaiseEvent(escape);NavigationTests.Wait(running);
                check(escape.Handled&&Equals(scanButton.Content,L.Text("networkScan.start")),"Escape cancels the scan and restores the search button");
                running=panel.StartNetworkRangeScan();scanButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(running);
                check(Equals(scanButton.Content,L.Text("networkScan.start")),"stop button cancels scanning and restores search");
                panel.NetworkRangeProbeForTests=null;
                var otherPanel=FluentUiTests.Children<FilePanel>(window).First(x=>x!=panel);
                window.PreviewInOtherPanel(otherPanel); window.CloseQuickPreview();
                check(panel.List.Visibility==Visibility.Collapsed && subnetTree.IsVisible,"closing a preview restores the network tree instead of the hidden device list");
                window.UpdateLayout();NetworkTests.Render(window,Path.Combine(root,"network-subnets.png"));
                panel.RefreshNetworkTree(panel.List.Items.Cast<Entry>());
                var deviceNode=subnetTree.Items.OfType<TreeViewItem>().SelectMany(x=>x.Items.OfType<TreeViewItem>()).Single(x=>x.Tag is Entry);
                deviceNode.IsSelected=true;
                check(panel.List.SelectedItem is Entry && ((Entry)panel.List.SelectedItem).FullPath=="\\\\test-server","selecting a tree device updates the panel selection");
                panel.OpenSelected(); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == "\\\\test-server" && panel.List.Items.Count == 2, "opening a device enumerates shares and a parent entry");
                check(panel.List.Visibility==Visibility.Visible && subnetTree.Visibility==Visibility.Collapsed,"opening a device restores the share list");
                var share = panel.List.Items.Cast<Entry>().Single(x => !x.IsParent); panel.List.SelectedItem = share;
                check(NetworkBrowser.CanMap(share) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = "\\\\test-server" }) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = root }), "mapping accepts UNC shares and rejects devices and local directories");
                panel.List.ContextMenu.IsOpen = true; window.UpdateLayout(); NavigationTests.Wait(System.Threading.Tasks.Task.Delay(150));
                var more=panel.List.ContextMenu.Items.OfType<MenuItem>().Single(x=>Equals(x.Header,L.Text("context.more")));
                check(more.Items.OfType<MenuItem>().Any(x => Equals(x.Header,L.Text("drive.mapFolderAsDrive")) && x.Visibility == Visibility.Visible), "UNC folder context menu offers network drive mapping"); panel.List.ContextMenu.IsOpen = false;
                panel.Up(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == NetworkBrowser.Root, "parent navigation returns from device to network");
                panel.Back(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == "\\\\test-server", "network browsing participates in back history");
                panel.Navigate(root, true); NavigationTests.Wait(panel.NavigationTask); check(!panel.IsNetworkBrowser && panel.CurrentPath == root, "local navigation leaves network browsing cleanly");
                panel.NewTab();panel.Navigate(NetworkBrowser.Root,true);NavigationTests.Wait(panel.NavigationTask);closeScan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(panel.TabNavigationTask);
                check(panel.TabCount==1&&panel.CurrentPath==root,"network scan close button closes the network tab");
                panel.Navigate(NetworkBrowser.Root,true);NavigationTests.Wait(panel.NavigationTask);closeScan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(panel.NavigationTask);
                var firstDisk=DriveInfo.GetDrives().Where(d=>d.DriveType!=DriveType.Network&&d.IsReady).Select(d=>d.Name).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).First();
                check(panel.TabCount==1&&!panel.EmptyPanel&&panel.CurrentPath==firstDisk,"closing the last network tab opens the first available disk");
                panel.Navigate(root,false);NavigationTests.Wait(panel.NavigationTask);
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
                check(NetworkDiscovery.ParseRange("10.x.x.x").Count==16777214&&NetworkDiscovery.ParseRange("192.168.0.0/16").Count==65534,"explicit private wildcard and CIDR ranges retain their full requested size");
                var candidates=new[]{Tuple.Create(IPAddress.Parse("192.168.1.4"),IPAddress.Parse("255.255.255.0"),false,2),Tuple.Create(IPAddress.Parse("10.0.10.42"),IPAddress.Parse("255.255.255.0"),true,3)};
                check(NetworkDiscovery.DefaultRange(candidates)=="10.0.10.*","default range uses an active private interface with a gateway");
                check(NetworkDiscovery.DefaultRange(new[]{Tuple.Create(IPAddress.Parse("10.0.23.5"),IPAddress.Parse("255.255.240.0"),true,1)})=="10.0.16.0/20","non-octet subnet masks preserve their exact network prefix");
                check(NetworkDiscovery.DefaultRange(new[]{Tuple.Create(IPAddress.Parse("8.8.8.8"),IPAddress.Parse("255.255.255.0"),true,1)})=="","no private interface leaves the range blank");
                bool invalid=false;try{NetworkDiscovery.ParseRange("8.8.8.0/24");}catch(FormatException){invalid=true;}check(invalid,"public ranges are rejected");
                var found=new List<Entry>();long completed=0;var gate=new object();
                var scan=NetworkDiscovery.ScanRangeAsync(NetworkDiscovery.ParseRange("10.0.10.0/30"),System.Threading.CancellationToken.None,e=>{lock(gate)found.Add(e);},(done,total,subnet)=>{System.Threading.Interlocked.Exchange(ref completed,done);},(host,port,timeout)=>System.Threading.Tasks.Task.FromResult(port==445&&host.EndsWith(".1")));
                NavigationTests.Wait(scan);check(found.Count==1&&found[0].FullPath=="\\\\10.0.10.1"&&completed==2,"custom scan streams findings and progress through bounded workers");
                var progressRows=new List<string>();var blocks=NetworkDiscovery.ScanRangeAsync(NetworkDiscovery.ParseRange("10.0.10.0/23"),System.Threading.CancellationToken.None,e=>{},(done,total,subnet)=>{lock(gate){if(progressRows.Count==0||progressRows[progressRows.Count-1]!=subnet)progressRows.Add(subnet);}},(host,port,timeout)=>System.Threading.Tasks.Task.FromResult(false));
                NavigationTests.Wait(blocks);check(progressRows.SequenceEqual(new[]{"10.0.10.*","10.0.11.*"})&&FilePanel.NetworkProgress(254,510,"10.0.11.*").Contains("10.0.11.*")&&!FilePanel.NetworkProgress(254,510,"10.0.11.*").Contains("254"),"scan advances through subnets and shows percentage rather than address counts");
                using(var stopped=new System.Threading.CancellationTokenSource()){
                    stopped.Cancel();var canceled=NetworkDiscovery.ScanRangeAsync(NetworkDiscovery.ParseRange("10.0.10.0/24"),stopped.Token,e=>{},(done,total,subnet)=>{},(host,port,timeout)=>System.Threading.Tasks.Task.FromResult(false));
                    try{NavigationTests.Wait(canceled);}catch(OperationCanceledException){}check(canceled.IsCanceled,"custom network scan honors cancellation before probing");
                }
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

