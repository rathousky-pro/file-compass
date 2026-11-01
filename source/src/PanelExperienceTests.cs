#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;
namespace FileCompass {
 internal static class PanelExperienceTests {
  static void Pump(Window window){NavigationTests.Wait(Task.Delay(100));window.UpdateLayout();}
  internal static int Run(string root){Directory.CreateDirectory(root);var log=new List<string>();MainWindow main=null;string oldMotif=AppearanceTheme.Motif,oldUnits=ByteSize.Mode;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();L.ChangeLanguage("cs-CZ");AppearanceTheme.Apply("Light");AppearanceTheme.SetMotif("Clean");var folder=Directory.CreateDirectory(Path.Combine(root,"files")).FullName;File.WriteAllText(Path.Combine(folder,"normal.txt"),"fixture");
   check(ByteSize.Format(1000000,"SI").EndsWith(" MB")&&ByteSize.Format(1048576,"IEC").EndsWith(" MiB")&&ByteSize.Format(1048576,"Bytes").EndsWith(" B"),"decimal, binary and exact-byte displays use the correct units");
   var prefs=new ProgramSettings{SizeUnits="Bytes",AutoRefresh=false};prefs.Save(Path.Combine(root,"unit-prefs.xml"));check(ProgramSettings.Load(Path.Combine(root,"unit-prefs.xml")).SizeUnits=="Bytes","unit preference survives persistence");
   main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1320,Height=820,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);var panels=FluentUiTests.Children<FilePanel>(main).ToArray();panels[0].VerifyEmptyWorkflow(root,folder,check);
   panels[0].VerifyColumnLimits(main,root,check);
   var surface=(FrameworkElement)main.Content;var initial=surface.Effect;var smart=new DiskSmartWindow(main,false);smart.Show();Pump(smart);check(ToolBackdrop.OwnerBlocked(main)&&!ToolBackdrop.NativeEnabled(main)&&Object.ReferenceEquals(surface.Effect,initial),"SMART blocks owner input without blurring the workspace");var analyzer=new DiskAnalyzerWindow(main,folder,null,false);analyzer.Show();Pump(analyzer);smart.Close();check(ToolBackdrop.OwnerBlocked(main)&&!ToolBackdrop.NativeEnabled(main),"closing one of two tools retains owner input blocking");analyzer.Hide();check(!ToolBackdrop.OwnerBlocked(main)&&ToolBackdrop.NativeEnabled(main)&&Object.ReferenceEquals(surface.Effect,initial),"backgrounding the remaining tool enables owner input");analyzer.Show();Pump(analyzer);check(ToolBackdrop.OwnerBlocked(main)&&!ToolBackdrop.NativeEnabled(main),"reopening the tool blocks owner input again");analyzer.Close();check(Object.ReferenceEquals(surface.Effect,initial),"closing all tools restores the exact previous effect");
   var archive=new ArchivePackDialog(main,1,Path.Combine(folder,"fixture.zip"));archive.Show();Pump(archive);check(Object.ReferenceEquals(surface.Effect,initial),"archive dialog leaves the main workspace unblurred");archive.Close();var copy=new CopySetupDialog(main,new List<Entry>{new Entry{Name="normal",Extension="txt",FullPath=Path.Combine(folder,"normal.txt"),Bytes=7}},folder,new string[0]);copy.Show();Pump(copy);check(Object.ReferenceEquals(surface.Effect,initial),"copy setup leaves the main workspace unblurred");copy.Close();
   var media=new MediaConverterWindow(main,new string[0],folder,true,delegate{},delegate{});media.Show();NavigationTests.Wait(media.AnalysisTask);Pump(media);check(ToolBackdrop.OwnerBlocked(main)&&!ToolBackdrop.NativeEnabled(main)&&Object.ReferenceEquals(surface.Effect,initial),"media conversion blocks owner input without blur");media.Close();check(Object.ReferenceEquals(surface.Effect,initial),"closing conversion restores the main workspace");
   var config=new ConfigurationDialog(main);try{config.Left=-18000;config.Top=-18000;config.Show();Pump(config);var units=FluentUiTests.Children<ComboBox>(config).Single(x=>x.Items.Contains(L.Text("settings.unitsBytes")));units.SelectedIndex=2;var apply=FluentUiTests.Children<Button>(config).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="ConfigurationApply");apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(ProgramSettings.Load().SizeUnits=="Bytes"&&ByteSize.Mode=="Bytes","size display can be changed and applied from Appearance settings");}finally{config.Close();}
   File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null){foreach(Window child in main.OwnedWindows.Cast<Window>().ToArray())child.Close();main.Close();}ByteSize.Mode=oldUnits;AppearanceTheme.SetMotif(oldMotif);}}
 }
 public partial class FilePanel {
  internal void VerifyEmptyWorkflow(string root,string folder,Action<bool,string> check){
   CloseTab(CurrentTabIndex);check(EmptyPanel&&TabCount==0&&CurrentPath==null&&List.Items.Count==0&&SaveTabs().Length==0,"closing the last local tab leaves a truly empty panel");var snapshot=new ProgramSettings{LeftFolder=folder,RightFolder=folder,SaveTabsOnExit=true,SaveFoldersOnExit=true};((MainWindow)Window.GetWindow(this)).CaptureView(snapshot);snapshot.LeftPanelEmpty=true;var reopened=new MainWindow(null,snapshot){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};try{reopened.Show();NavigationTests.WaitPanels(reopened);var empty=FluentUiTests.Children<FilePanel>(reopened).First();check(empty.EmptyPanel&&empty.TabCount==0&&empty.CurrentPath==null,"startup restoration preserves the empty-panel state");}finally{reopened.Close();}
   NewTab();NavigationTests.Wait(NavigationTask);check(!EmptyPanel&&TabCount==1&&CurrentPath!=null,"new tab reopens a usable location from an empty panel");Navigate(folder,false);NavigationTests.Wait(NavigationTask);
   foreach(string answer in new[]{"action.cancel","tabs.closeOnly","action.disconnect"}){
    var connection=new RemoteConnection(new NetworkProfile{Id=Guid.NewGuid().ToString("N"),Name="Fixture",Host="fixture.invalid",Protocol=NetworkProtocol.SFTP,Path="/"});openConnections[connection]="/";Remote=connection;CurrentPath="/";EmptyPanel=false;tabs[tabIndex].Remote=connection;CaptureTab();
    var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};timer.Tick+=delegate{var question=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x.Title==L.Text("tabs.remoteCloseTitle"));if(question==null)return;timer.Stop();var button=FluentUiTests.Children<Button>(question).Single(x=>Convert.ToString(x.Content)==L.Text(answer));button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};timer.Start();CloseTab(tabIndex);timer.Stop();
    if(answer=="action.cancel")check(TabCount==1&&openConnections.ContainsKey(connection),"remote-tab cancel leaves both tab and connection intact");else if(answer=="tabs.closeOnly")check(EmptyPanel&&openConnections.ContainsKey(connection),"closing only the remote tab preserves the pooled connection");else check(EmptyPanel&&!openConnections.ContainsKey(connection),"disconnect choice removes the remote connection before closing its tab");
    openConnections.Remove(connection);connection.Dispose();Remote=null;if(EmptyPanel)NewTab();Navigate(folder,false);NavigationTests.Wait(NavigationTask);
   }
  }
  internal void VerifyColumnLimits(Window window,string root,Action<bool,string> check){
   var original=new PanelView();SavePanelView(original);try{extensionColumn=attributesColumn=true;ApplyBaseColumns();SetMetadataColumns(MetadataColumn.Keys);FitColumns();window.UpdateLayout();check(view.Columns.Sum(x=>x.Width)<=ColumnRoom+1,"many requested columns are limited to the actual viewport width");
    var name=BaseColumn(0);SetColumnWidth(0,4000);check(name.Width<=ColumnRoom&&view.Columns.Sum(x=>x.Width)<=ColumnRoom+1,"oversized manual widths are clamped without horizontal overflow");
    window.Width=800;NavigationTests.Wait(Task.Delay(120));window.UpdateLayout();FitColumns();check(view.Columns.Sum(x=>x.Width)<=ColumnRoom+1&&ScrollViewer.GetHorizontalScrollBarVisibility(List)==ScrollBarVisibility.Disabled,"shrinking the window refits columns without a horizontal scrollbar");
    var header=(GridViewColumnHeader)name.Header;header.ContextMenu.IsOpen=true;var choices=header.ContextMenu.Items.OfType<MenuItem>().Where(x=>x.IsCheckable&&!x.IsChecked).ToArray();check(choices.Any(x=>!x.IsEnabled),"column menu prevents adding columns that cannot fit");header.ContextMenu.IsOpen=false;
    NextStageTests.Draw(window,Path.Combine(root,"bounded-columns.png"));window.Width=1320;
   }finally{RestorePanelView(original);}
  }
 }
}
#endif
