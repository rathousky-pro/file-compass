using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 public partial class FilePanel {
  readonly Border tabNotice=new Border{Background=CompactTheme.WhiteSurface,Padding=new Thickness(24),Visibility=Visibility.Collapsed};
  readonly TextBlock tabNoticeText=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,10,0,14)};
  internal bool HasUnavailableTab {get{return tabs.Count>0&&tabs[tabIndex].Pending&&tabNotice.Visibility==Visibility.Visible;}}
  internal string UnavailableTabMessage {get{return tabNoticeText.Text;}}
  void InitializeTabNotice(Grid grid){
   var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Stretch};
   content.Children.Add(new TextBlock{Text=L.Text("tabs.unavailableTitle"),FontSize=18,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});content.Children.Add(tabNoticeText);
   var retry=CopyUi.Button(L.Text("tabs.retry"),delegate{SwitchTab(tabIndex);});retry.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(retry);tabNotice.Child=content;Grid.SetRow(tabNotice,3);Panel.SetZIndex(tabNotice,500);grid.Children.Add(tabNotice);
  }
  void ShowTabUnavailable(PanelTab tab,string reason){
   tab.Pending=true;tab.Error=reason;tab.Remote=null;CurrentPath=null;Remote=null;CloseArchiveContext();projection=null;List.ItemsSource=new Entry[0];tabNoticeText.Text=reason+"\n\n"+(String.IsNullOrEmpty(tab.Saved.Caption)?"":tab.Saved.Caption+"\n")+(tab.Saved.Locked?tab.Saved.Home:tab.Saved.Path);tabNotice.Visibility=Visibility.Visible;address.Text=tab.Saved.Locked?tab.Saved.Home:tab.Saved.Path;status.Text=reason.Split('\n')[0];status.ToolTip=reason;capacity.Text=L.Text("tabs.unavailableTitle");capacity.ToolTip=reason;ConfigureWatcher();RenderTabs();RenderCrumbs();if(PathChanged!=null)PathChanged(this,EventArgs.Empty);
  }
  static string LocalTabError(string path,Exception error){
   string title=L.Text("tabs.unavailableFolder");int code=error.HResult&65535;
   try{string root=Path.GetPathRoot(path);if(path.StartsWith(@"\\")||code==15||code==21||code==53||code==64||code==67||!DriveInfo.GetDrives().Any(x=>String.Equals(x.Name,root,StringComparison.OrdinalIgnoreCase)))title=L.Text("tabs.unavailableDrive");}catch(ArgumentException){}
   return title+"\n"+error.Message;
  }
  internal System.Threading.Tasks.Task OpenLocationTab(SavedTab saved){
   if(IsLoading||LocalLoading||saved==null)return System.Threading.Tasks.Task.FromResult(0);if(EmptyPanel){EmptyPanel=false;tabs.Clear();}CaptureTab();var copy=CloneSavedTab(saved);copy.Locked=false;copy.Home=copy.Path;tabs.Add(new PanelTab{Saved=copy,View=copy.View??new PanelView(),Pending=true});tabIndex=tabs.Count-1;switchingTabs=true;RenderTabs();NotifyTabsChanged();SwitchTab(tabIndex);return TabNavigationTask;
  }
  internal void PinCurrentFolder(){
   if(CurrentPath==null||IsLoading||LocalLoading)return;CaptureTab();var current=tabs[tabIndex].Saved;
   int existing=tabs.FindIndex(x=>x.Saved.Locked&&x.Saved.Home==CurrentPath&&x.Saved.ProfileId==current.ProfileId&&x.Saved.ArchiveFile==current.ArchiveFile);
   if(existing>=0){SwitchTab(existing);return;}
   NewTab();LockTab(tabIndex);
  }
 }
 public partial class MainWindow {
  void PersistPanelTabs(object sender,EventArgs args){
   try{var settings=ProgramSettings.Load();settings.SaveTabsOnExit=true;settings.LeftTabs=left.SaveTabs();settings.RightTabs=right.SaveTabs();settings.LeftPanelEmpty=left.EmptyPanel;settings.RightPanelEmpty=right.EmptyPanel;settings.LeftTabIndex=left.CurrentTabIndex;settings.RightTabIndex=right.CurrentTabIndex;settings.Save();}
   catch(Exception ex){MessageBox.Show(this,ex.Message,L.Text("settings.saveViewFailed"),MessageBoxButton.OK,MessageBoxImage.Warning);}
  }
 }
}
