using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 public partial class FilePanel {
  internal bool EmptyPanel {get;private set;}
  internal void ClearEmptyPanel(bool notify=true){
   CancelNavigation();RememberRemote();Remote=null;CloseArchiveContext();projection=null;history.Clear();forwardHistory.Clear();CurrentPath=null;EmptyPanel=true;tabs.Clear();tabs.Add(new PanelTab());tabIndex=0;switchingTabs=false;
   SetQuickFilter("");List.ItemsSource=new Entry[0];tabNotice.Visibility=Visibility.Collapsed;address.Text="";capacity.Text="";capacity.ToolTip=null;selectingDrive=true;driveSelector.SelectedIndex=-1;selectingDrive=false;RenderCrumbs();RenderTabs();ConfigureWatcher();UpdateConnectionButtons();RefreshCleanDrives();UpdateStatus();if(PathChanged!=null)PathChanged(this,EventArgs.Empty);if(notify)NotifyTabsChanged();
  }
  bool ConfirmRemoteTabClose(PanelTab tab){
   var connection=tab.Remote;if(connection==null||!openConnections.ContainsKey(connection))return true;
   var dialog=new Window();CopyUi.Style(dialog,Window.GetWindow(this),L.Text("tabs.remoteCloseTitle"),540);StackPanel actions;var body=SettingsUx.Frame(dialog,L.Text("tabs.remoteCloseTitle"),out actions);body.Children.Add(new TextBlock{Text=L.Text("tabs.remoteCloseHelp")+"\n\n"+connection.Profile.DisplayLabel,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(20,18,20,18)});
   int answer=0;var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{dialog.DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);actions.Children.Add(CopyUi.Button(L.Text("action.disconnect"),delegate{answer=2;dialog.DialogResult=true;}));var closeOnly=CopyUi.Button(L.Text("tabs.closeOnly"),delegate{answer=1;dialog.DialogResult=true;});closeOnly.IsDefault=true;actions.Children.Add(closeOnly);if(dialog.ShowDialog()!=true||answer==0)return false;
   if(answer==2){openConnections.Remove(connection);connection.Dispose();foreach(var item in tabs.Where(x=>x.Remote==connection))item.Remote=null;if(Remote==connection){Remote=null;if(tabs[tabIndex]!=tab)ShowTabUnavailable(tabs[tabIndex],L.Text("tabs.disconnected"));}UpdateConnectionButtons();}return true;
  }
 }
}
