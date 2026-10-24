using System;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal sealed class ConnectionsDialog : Window {
        internal readonly TabControl Tabs=new TabControl();
        internal readonly NetworkManagerDialog Network;
        internal readonly CloudManagerWindow Cloud;
        readonly ConnectionsView view;
        internal NetworkProfile ConnectProfile {get{return Network.ConnectProfile;}}
        internal bool OpenSsh {get{return Network.OpenSsh;}}
        internal ConnectionsDialog(Window owner,MainWindow main,NetworkSettingsStore store,NetworkProtocol? filter=null,bool cloudSelected=false){
            CopyUi.Style(this,owner,L.Text("connections.title"),760);
            DialogGeometry.Bound(this,820,590,680,460);ResizeMode=ResizeMode.CanResize;
            view=new ConnectionsView(this,main,store,false,filter,cloudSelected);
            Tabs=view.Tabs;Network=view.Network;Cloud=view.Cloud;
            Content=new Border{Child=view,Margin=new Thickness(12),Padding=new Thickness(6),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(!view.AllowHostClose())e.Cancel=true;};
            Closed+=delegate{view.DisposeManagers();};
        }
    }
    internal sealed class ConnectionsView : Border {
        internal readonly TabControl Tabs=new TabControl();
        internal readonly NetworkManagerDialog Network;
        internal readonly CloudManagerWindow Cloud;
        internal event Action<bool> BusyChanged;
        internal ConnectionsView(Window host,MainWindow main,NetworkSettingsStore store,bool managementOnly,NetworkProtocol? filter=null,bool cloudSelected=false){
            Child=Tabs;
            Network=new NetworkManagerDialog(main,store,filter,managementOnly){HostWindow=host};
            var networkContent=Network.Content;Network.Content=null;
            var networkTab=new TabItem{Header=L.Text("network.connectionsTitle"),Content=networkContent};Tabs.Items.Add(networkTab);
            try{
                Cloud=new CloudManagerWindow(main,managementOnly){HostWindow=host};
                var cloudContent=Cloud.Content;Cloud.Content=null;
                var frame=cloudContent as Border;if(frame!=null){frame.Margin=new Thickness(0);frame.Padding=new Thickness(6);frame.BorderThickness=new Thickness(0);frame.Background=System.Windows.Media.Brushes.Transparent;}
                Tabs.Items.Add(new TabItem{Header=L.Text("connections.cloudTab"),Content=cloudContent});
                Cloud.BusyChanged+=delegate(bool busy){networkTab.IsEnabled=!busy;if(BusyChanged!=null)BusyChanged(busy);};
            }catch(Exception ex){
                Tabs.Items.Add(new TabItem{Header=L.Text("connections.cloudTab"),Content=new TextBlock{Text=ex.Message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(16)}});
            }
            Tabs.SelectedIndex=cloudSelected?1:0;
        }
        internal bool AllowHostClose(){return Cloud==null||Cloud.AllowHostClose();}
        internal void DisposeManagers(){Network.HostWindow=null;Network.Close();if(Cloud!=null){Cloud.HostWindow=null;Cloud.Close();}}
    }
}
