using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace CommanderCZ {
    internal static class ConnectionsDialogTests {
        static Button Button(Window window,string key){return AdvancedToolsTests.Visual<Button>(window).First(x=>x.IsVisible&&Convert.ToString(x.Content)==L.Text(key));}
        static void CheckEditor(ConnectionsDialog host,string button,string file,Action<bool,string> check,bool cloud){
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
            timer.Tick+=delegate{
                var child=host.OwnedWindows.Cast<Window>().FirstOrDefault(x=>x.IsVisible);if(child==null)return;
                timer.Stop();check(child.Owner==host,"Child editor is owned by the unified window: "+button);
                child.UpdateLayout();
                if(cloud)check(AdvancedToolsTests.Visual<ComboBox>(child).Single().Items.Cast<string>().SequenceEqual(new[]{"Google Drive","OneDrive","Dropbox"}),"Cloud wizard retains all three providers inside the shared window");
                else check(child is NetworkProfileDialog,"Network profile editor opens from the network tab");
                NextStageTests.Draw(child,file);child.DialogResult=false;
            };
            timer.Start();try{Button(host,button).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));}finally{timer.Stop();}
            check(!host.OwnedWindows.Cast<Window>().Any(x=>x.IsVisible),"Cancelling the child editor returns to the same shared window");
        }
        internal static int Run(string root){
            Directory.CreateDirectory(root);var results=new List<string>();MainWindow main=null;ConnectionsDialog host=null;string previous=CloudStore.TestRoot;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);results.Add(label);};
            try{
                System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                CloudStore.TestRoot=Path.Combine(root,"cloud");
                CloudStore.Save(new CloudAccounts{Accounts=new List<CloudAccount>{new CloudAccount{Name="Osobní Google Drive",Provider="drive"}}});
                var store=new NetworkSettingsStore(Path.Combine(root,"connections.xml"));
                var ftp=new NetworkProfile{Name="FTP server",Host="example.invalid",Protocol=NetworkProtocol.FTP,Port=21};
                var sftp=new NetworkProfile{Name="SFTP server",Host="example.invalid",Protocol=NetworkProtocol.SFTP,UserName="tester"};
                store.Settings.Connections.Add(ftp);store.Settings.Connections.Add(sftp);
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                host=new ConnectionsDialog(main,main,store){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};host.Show();host.UpdateLayout();
                check(host.Title==L.Text("connections.title")&&host.Tabs.Items.Count==2,"There is one connection window with exactly two tabs");
                check(host.Tabs.Items.Cast<TabItem>().Select(x=>Convert.ToString(x.Header)).SequenceEqual(new[]{L.Text("network.connectionsTitle"),L.Text("connections.cloudTab")}),"The two tabs are named Network and Cloud connections");
                check(host.Tabs.SelectedIndex==0,"The common entry point selects network connections by default");
                check(!host.Network.IsVisible&&!host.Cloud.IsVisible,"Separate network and cloud windows are not displayed");
                host.Network.SelectFilter(NetworkProtocol.SFTP);host.UpdateLayout();
                check(AdvancedToolsTests.Visual<ListBox>(host).Single().Items.Cast<NetworkProfile>().Single().Id==sftp.Id,"Protocol filtering works in the embedded network tab");
                NextStageTests.Draw(host,Path.Combine(root,"network-tab.png"));
                CheckEditor(host,"network.newConnection",Path.Combine(root,"network-editor.png"),check,false);
                host.Tabs.SelectedIndex=1;host.UpdateLayout();
                check(Button(host,"cloud.connect").Content.ToString()==L.Text("action.append"),"Both tabs use the same Connect label");
                check(AdvancedToolsTests.Visual<ListBox>(host).Single().Items.Cast<CloudAccount>().Single().Name=="Osobní Google Drive","Saved cloud accounts appear in the cloud tab");
                NextStageTests.Draw(host,Path.Combine(root,"cloud-tab.png"));
                CheckEditor(host,"cloud.add",Path.Combine(root,"cloud-editor.png"),check,true);
                host.Tabs.SelectedIndex=0;host.UpdateLayout();
                check(host.Network.ProtocolFilter==NetworkProtocol.SFTP,"Switching tabs preserves the selected network protocol");
                var menus=AdvancedToolsTests.Visual<Menu>(main).SelectMany(x=>x.Items.OfType<MenuItem>()).ToArray();
                var menu=menus.Single(x=>Convert.ToString(x.Header)==L.Text("connections.title"));
                check(menu.Items.OfType<MenuItem>().Count(x=>Convert.ToString(x.Header)==L.Text("network.manageConnectionsMenu"))==1&&!menu.Items.OfType<MenuItem>().Any(x=>Convert.ToString(x.Header)==L.Text("cloud.title")),"The main menu has a single entry for connection management");
                Button(host,"action.cancel").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));check(!host.IsVisible,"Network Cancel closes the shared window");host=null;
                host=new ConnectionsDialog(main,main,store,null,true){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};host.Show();host.UpdateLayout();
                check(host.Tabs.SelectedIndex==1,"A cloud-specific request selects the cloud tab in the same window");
                Button(host,"action.close").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));check(!host.IsVisible,"Cloud Close closes the shared window");host=null;
                host=new ConnectionsDialog(main,main,store,NetworkProtocol.SFTP){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};
                var choose=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};choose.Tick+=delegate{choose.Stop();Button(host,"action.append").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));};
                choose.Start();bool? accepted;try{accepted=host.ShowDialog();}finally{choose.Stop();}
                check(accepted==true&&host.ConnectProfile.Id==sftp.Id&&!host.OpenSsh,"Network Connect returns the selected profile through the shared dialog");
                host=null;check(!main.OwnedWindows.Cast<Window>().Any(x=>x.IsVisible),"Closing connection management leaves no separate visible manager window");
                File.WriteAllLines(Path.Combine(root,"result.txt"),results.Concat(new[]{"PASS: "+results.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,results)+Environment.NewLine+"FAIL: "+ex);return 1;}
            finally{if(host!=null)host.Close();if(main!=null)main.Close();CloudStore.TestRoot=previous;}
        }
    }
}