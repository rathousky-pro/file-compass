#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Threading.Tasks;using System.Threading;using System.Windows;using System.Windows.Controls;using System.Windows.Interop;using System.Windows.Media;using System.Windows.Threading;
namespace FileCompass {
 internal static class GlassDialogTests {
  static void Pump(Window w){NavigationTests.Wait(Task.Delay(180));w.UpdateLayout();}
  static void Open(Window w){w.Left=-18000;w.Top=-18000;w.WindowStartupLocation=WindowStartupLocation.Manual;w.ShowActivated=false;w.ShowInTaskbar=false;w.Show();Pump(w);}
  static double Alpha(Brush brush){var solid=brush as SolidColorBrush;return solid==null?255:solid.Color.A;}
  internal static int Run(string root){Directory.CreateDirectory(root);var log=new List<string>();var windows=new List<Window>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);};try{
   FluentTheme.Ensure();L.ChangeLanguage("cs");SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
   string sample=Path.Combine(root,"sample.txt");File.WriteAllText(sample,"fixture");var entries=new List<Entry>{new Entry{Name="sample.txt",FullPath=sample,Bytes=7}};
   foreach(double font in new[]{12.0,15.0})foreach(bool archive in new[]{false,true}){
    Window w=archive?(Window)new ArchivePackDialog(null,1,Path.Combine(root,"Documents.zip")):new CopySetupDialog(null,entries,root,new string[0]);windows.Add(w);w.FontSize=font;w.MaxHeight=900;Open(w);
    var card=FluentUiTests.Children<Border>(w).Single(x=>Convert.ToString(x.Tag)=="operation-page-card");var header=FluentUiTests.Children<Border>(w).Single(x=>Convert.ToString(x.Tag)=="dialog-header");var workspace=(FrameworkElement)card.Parent;var client=(FrameworkElement)w.Content;
    Action geometry=delegate{var left=card.TranslatePoint(new Point(),client).X;var right=client.ActualWidth-card.TranslatePoint(new Point(card.ActualWidth,0),client).X;var hleft=header.TranslatePoint(new Point(),client).X;check(Math.Abs(left-right)<3&&left>=hleft+6,(archive?"archive":"copy")+font+": collapsed middle card has equal insets and sits inside the header edges; "+left+" / "+right);};geometry();
    var page=FluentUiTests.Children<ScrollViewer>(card).Single(x=>x.IsVisible);check(page.ExtentHeight<=page.ViewportHeight+1,(archive?"archive":"copy")+font+": all basic options fit without scrolling; "+page.ExtentHeight+" / "+page.ViewportHeight);
    double originalWidth=card.ActualWidth;var more=FluentUiTests.Children<Button>(w).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="CleanMore");more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(w);
    check(Math.Abs(card.ActualWidth-originalWidth)<2,"opening navigation preserves the main content width at font "+font);var lastRight=client.ActualWidth-card.TranslatePoint(new Point(card.ActualWidth,0),client).X;check(lastRight>=14,"expanded card retains its outer right inset");
    check(w.Opacity==1&&Alpha(card.Background)==255&&Alpha(header.Background)==255,"cards and text stay fully opaque");
    int kind;int result=DwmGetWindowAttribute(new WindowInteropHelper(w).Handle,38,out kind,sizeof(int));check(!WindowGlass.Enabled(w)||(result==0&&kind==3&&Alpha(w.Background)<255),"active backdrop is native Desktop Acrylic with one translucent window tint");
    log.Add("Native backdrop: enabled="+WindowGlass.Enabled(w)+", kind="+kind+", tint alpha="+Alpha(w.Background));
    if(WindowGlass.Enabled(w)){check(Alpha(((Panel)w.Content).Background)==0,"outer content layer does not cover the native material");var shell=FluentUiTests.Children<Border>(w).Single(x=>Convert.ToString(x.Tag)=="window-shell");check(Alpha(shell.Background)==0,"outer frame does not stack another opaque or tinted layer");}
    NextStageTests.Draw(w,Path.Combine(root,(archive?"archive":"copy")+font+".png"));w.Close();windows.Remove(w);
   }
   var small=new ArchivePackDialog(null,1,Path.Combine(root,"Small.zip")){FontSize=15,MaxHeight=540};windows.Add(small);Open(small);var clientSmall=(FrameworkElement)small.Content;var primary=FluentUiTests.Children<Button>(small).Single(x=>x.IsDefault);check(small.ActualHeight<=541&&primary.TranslatePoint(new Point(0,primary.ActualHeight),clientSmall).Y<=clientSmall.ActualHeight,"small screens keep the archive footer reachable within the available height");small.Close();windows.Remove(small);
   var owner=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Width=1200,Height=800};windows.Add(owner);Open(owner);NavigationTests.WaitPanels(owner);
   check(FluentUiTests.Children<FilePanel>(owner).All(x=>Alpha(x.Background)==255),"main file panels remain opaque on the glass frame");
   if(WindowGlass.Enabled(owner)){check(FluentUiTests.Children<Menu>(owner).Where(x=>Convert.ToString(x.Tag)=="main-menu-area").All(x=>Alpha(x.Background)==0),"main menus leave the outer material visible");AppearanceTheme.Apply("Light");int current;check(DwmGetWindowAttribute(new WindowInteropHelper(owner).Handle,38,out current,sizeof(int))==0&&current==3,"refreshing appearance retains the native Acrylic backdrop");}
   WindowGlass.ForceOpaqueForTests=true;CleanTheme.Refresh(owner);check(!WindowGlass.Enabled(owner)&&Alpha(owner.Background)==255,"disabled or unsupported transparency falls back to the opaque blue-grey base");WindowGlass.ForceOpaqueForTests=false;CleanTheme.Refresh(owner);
   NextStageTests.Draw(owner,Path.Combine(root,"main.png"));owner.Close();windows.Remove(owner);File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{WindowGlass.ForceOpaqueForTests=false;foreach(var w in windows)w.Close();}}
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr handle,int attribute,out int value,int size);
 }
}
#endif
