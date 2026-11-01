#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Threading;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace FileCompass {
 public partial class FilePanel {
  internal void VerifyReferenceStrip(string root,Action<bool,string> check){
   var main=Window.GetWindow(this);Action pump=()=>{NavigationTests.Wait(Task.Delay(150));main.UpdateLayout();};pump();
   check(cleanBreadcrumbFrame.IsVisible&&cleanBreadcrumbFrame.Height==46,"navigation and tabs use one compact rounded row");
   check(cleanBreadcrumbDock.Children.OfType<Button>().Count()==2&&cleanRootButton.Content.Equals("\\")&&cleanParentButton.Content.Equals("↑"),"combined row retains root and parent actions");
   check(cleanBreadcrumbDock.Children.Contains(tabRow)&&((FrameworkElement)cleanBreadcrumbFrame.Parent).ActualHeight<60,"tabs share the navigation row instead of adding a second line");
   var face=activeTabFace;check(face.BorderThickness==new Thickness(0)&&face.CornerRadius.TopLeft==8&&face.CornerRadius.BottomLeft==0,"active tab has rounded upper corners and joins the file panel");
   check(FluentUiTests.Children<Button>(face).Any(x=>Convert.ToString(x.Tag)=="panel-tab-close"),"each tab has a visible close button");
   var tabClose=FluentUiTests.Children<Button>(face).Single(x=>Convert.ToString(x.Tag)=="panel-tab-close");tabClose.ApplyTemplate();check(tabClose.Template.FindName("Hit",tabClose) is Border&&tabClose.BorderThickness==new Thickness(0),"tab close uses a bare cross without the standard button frame");
   check(cleanRootButton.TranslatePoint(new Point(),this).X>face.TranslatePoint(new Point(face.ActualWidth,0),this).X,"navigation actions sit after the tabs at the right");
   Highlight(false);string inactive=Convert.ToString(cleanBreadcrumbFrame.Background);Highlight(true);check(BorderThickness.Left==3,"active file panel has a stronger three-pixel outline");check(inactive!=Convert.ToString(cleanBreadcrumbFrame.Background),"active panel navigation has a darker blue-grey background");
   check(tabStrip.Children[tabStrip.Children.Count-1]==newTabButton,"plus follows the tabs");
   string original=CurrentPath;cleanParentButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(NavigationTask);pump();check(CurrentPath==Path.GetDirectoryName(original.TrimEnd('\\')),"parent action navigates one level up");Navigate(original,false);NavigationTests.Wait(NavigationTask);pump();cleanRootButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(NavigationTask);pump();check(CurrentPath==Path.GetPathRoot(original),"root action navigates to the current drive root");Navigate(original,false);NavigationTests.Wait(NavigationTask);pump();
   var firstHeader=(GridViewColumnHeader)BaseColumn(0).Header;firstHeader.ApplyTemplate();var frame=(Border)firstHeader.Template.FindName("ColumnFrame",firstHeader);check(frame.Margin.Top==5&&frame.BorderThickness==new Thickness(0,0,1,1),"column separators start below the top and include the header baseline");
   firstHeader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));pump();check(Convert.ToString(firstHeader.Content).Contains("↓")||Convert.ToString(firstHeader.Content).Contains("↑"),"sort direction stays in the sorted header");
   NextStageTests.Draw(main,Path.Combine(root,"initial-panel.png"));FocusAddress();pump();check(address.IsVisible&&!tabRow.IsVisible&&!cleanBreadcrumbDock.IsVisible,"path editing temporarily replaces the single tab/navigation row; address="+address.Visibility+"/"+address.IsVisible+" tabs="+tabRow.Visibility+" crumbs="+cleanBreadcrumbDock.Visibility);FocusFiles();pump();
   newTabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));pump();check(TabCount==2&&tabStrip.Children.Count==3&&tabStrip.Children[2]==newTabButton,"plus creates a second tab and remains at the end");
   var first=FluentUiTests.Children<Button>((Border)tabStrip.Children[0]).First(x=>Convert.ToString(x.Tag)=="panel-tab");first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));pump();check(CurrentTabIndex==0,"clicking a tab switches the active location");
   VerifyColumnCustomization(root,check);NextStageTests.Draw(main,Path.Combine(root,"navigation-tabs.png"));
   RestoreTabs(new[]{new SavedTab{Path="/",ProfileId="offline-test",Caption="FTP · demo"}},0);pump();check(HasUnavailableTab,"disconnected FTP remains an explicit tab");
   var offlineClose=FluentUiTests.Children<Button>(activeTabFace).Single(x=>Convert.ToString(x.Tag)=="panel-tab-close");offlineClose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));pump();check(EmptyPanel,"close button closes the last disconnected FTP tab without reconnecting");newTabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(NavigationTask);pump();
   CloseTab(0);pump();check(EmptyPanel&&newTabButton.IsVisible&&tabStrip.Children.Count==1,"empty panels retain only the new-tab action");newTabButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(NavigationTask);pump();check(!EmptyPanel&&TabCount==1,"plus reopens a location from an empty panel");
   NextStageTests.Draw(main,Path.Combine(root,"reopened-panel.png"));
  }
 }
 internal static class PanelStripTests {
  internal static int Run(string root){Directory.CreateDirectory(root);MainWindow main=null;var log=new List<string>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);};try{FluentTheme.Ensure();L.ChangeLanguage("cs");SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));var folder=Directory.CreateDirectory(Path.Combine(root,"Dokumenty")).FullName;File.WriteAllText(Path.Combine(folder,"Ukázka.txt"),"test");main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1280,Height=820,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);FluentUiTests.Children<FilePanel>(main).First().VerifyReferenceStrip(root,check);main.Close();main=null;File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;}catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null)main.Close();}}
 }
}
#endif
