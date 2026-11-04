#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace FileCompass {
 public partial class MainWindow {
    internal double CopyDockWidthForTests{get{return copySummary.ActualWidth;}}
    internal ToolDockItem[] DockToolsForTests {get{return dockTools.ToArray();}}
    internal int DockTileCountForTests {get{return dockTiles.Count;}}
    internal void VerifyStableDock(Action<bool,string> check){
     double height=copySummary.ActualHeight;int flashes=0;DependencyPropertyChangedEventHandler changed=delegate{flashes++;};copySummary.IsVisibleChanged+=changed;var existing=dockTiles.Values.ToArray();
     var archive=AddDockTransferForTests("Compression",delegate{});RefreshTaskDock();UpdateLayout();for(int i=0;i<10;i++){archive.Update("Progress "+i,.1,.2);RefreshCopySummary();UpdateLayout();}
     check(flashes==0&&Math.Abs(copySummary.ActualHeight-height)<1,"copy/archive progress cannot blink or increase the fixed dock height");check(existing.All(x=>dockTiles.Values.Contains(x)),"updating transfers retains the same tool tile instances");archive.Finish("done",true);copySummary.IsVisibleChanged-=changed;
    }
    internal CopyActivity AddDockTransferForTests(string title,Action open){var activity=AddCopyActivity(title,delegate{},open);activity.Kind=DockActivityKind.LocalTransfer;activity.TransferTitle=title;activity.TransferRoute="Dokumenty → Záloha";activity.Update("6 / 10",.6,.54);return activity;}
 }
 internal static class TaskDockTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr first,IntPtr second);
    internal static int Run(string root) {
        Directory.CreateDirectory(root);var checks=new List<string>();MainWindow main=null;var windows=new List<Window>();
        Action<bool,string> check=(yes,label)=>{if(!yes)throw new Exception(label);checks.Add(label);};
        try {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs");FluentTheme.Ensure();AppearanceTheme.Apply("Light");
            string folder=Directory.CreateDirectory(Path.Combine(root,"data")).FullName;File.WriteAllText(Path.Combine(folder,"one.txt"),"fixture");
            main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1400,Height=860};Open(main);NavigationTests.WaitPanels(main);
            var smart=new DiskSmartWindow(main,false);windows.Add(smart);Open(smart);
            var analyzer=new DiskAnalyzerWindow(main,folder,null,true);windows.Add(analyzer);Open(analyzer);NavigationTests.Wait(analyzer.ScanTask);
            string input=Path.Combine(folder,"test.png");var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(16,16,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[16*16*4],16*4)));using(var file=File.Create(input))encoder.Save(file);
            var converter=new MediaConverterWindow(main,new[]{input},folder,true,delegate{},delegate{});windows.Add(converter);Open(converter);NavigationTests.Wait(converter.AnalysisTask);
            foreach(var size in new[]{new Size(780,540),new Size(1180,820)}){converter.Width=size.Width;converter.Height=size.Height;converter.UpdateLayout();NavigationTests.Wait(Task.Delay(100));var background=FluentUiTests.Children<Button>(converter).Single(x=>Convert.ToString(x.Content)==L.Text("copy.backgroundAction"));check(Math.Abs(background.TranslatePoint(new Point(),converter).Y-converter.Start.TranslatePoint(new Point(),converter).Y)<2,"converter footer actions remain on one row at "+size.Width);check(converter.Start.TranslatePoint(new Point(converter.Start.ActualWidth,0),converter).X<converter.ActualWidth-5,"converter start action fits within the right edge");NextStageTests.Draw(converter,Path.Combine(root,"converter-"+size.Width+".png"));}
            foreach(var size in new[]{new Size(780,560),new Size(1160,820)}){analyzer.Width=size.Width;analyzer.Height=size.Height;analyzer.UpdateLayout();NavigationTests.Wait(Task.Delay(100));var footer=FluentUiTests.Children<Button>(analyzer).Single(x=>Convert.ToString(x.Content)==L.Text("copy.backgroundAction")).Parent as StackPanel;var buttons=footer.Children.OfType<Button>().ToArray();check(buttons.Length==6&&buttons.Max(x=>x.TranslatePoint(new Point(),analyzer).Y)-buttons.Min(x=>x.TranslatePoint(new Point(),analyzer).Y)<2,"analyzer has six actions on one row at "+size.Width);check(buttons.All(x=>x.TranslatePoint(new Point(x.ActualWidth,0),analyzer).X<=analyzer.ActualWidth-5),"analyzer actions fit inside the window");var more=buttons.Single(x=>Convert.ToString(x.Content).StartsWith(L.Text("action.moreOptions")));check(more.ContextMenu.Items.Count==5,"analyzer overflow retains export, snapshots, duplicates and repeat scan");NextStageTests.Draw(analyzer,Path.Combine(root,"analyzer-"+size.Width+".png"));}
            check(main.DockTileCountForTests==0,"visible tool windows do not create dock tiles");
            foreach(var window in windows.ToArray()) {
                double width=window.Width,height=window.Height;var registration=main.DockToolsForTests.Single(x=>x.Window==window);
                SendMessage(new System.Windows.Interop.WindowInteropHelper(window).Handle,0x0112,new IntPtr(0xf020),IntPtr.Zero);NavigationTests.Wait(Task.Delay(160));main.UpdateLayout();
                check(!window.IsVisible&&window.WindowState!=WindowState.Minimized&&registration.Background,window.GetType().Name+": native minimize hides window into the application dock");
                check(main.IsVisible&&main.WindowState!=WindowState.Minimized,"tool minimization leaves the main window usable");
                registration.Placement.Restore();NavigationTests.Wait(Task.Delay(80));
                check(window.IsVisible&&!registration.Background&&Math.Abs(width-window.Width)<2&&Math.Abs(height-window.Height)<2,window.GetType().Name+": restore preserves window size and removes its tile");
                var background=FluentUiTests.Children<Button>(window).Single(x=>Convert.ToString(x.Content)==L.Text("copy.backgroundAction"));background.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));NavigationTests.Wait(Task.Delay(80));
                check(!window.IsVisible&&registration.Background,window.GetType().Name+": background button uses the same dock lifecycle");
            }
            check(main.DockTileCountForTests==3,"SMART, analyzer and converter coexist as three independent tiles");
            NavigationTests.Wait(converter.ConvertRows());main.RefreshTaskDock();
            check(!converter.IsVisible&&converter.Rows.Single().Status==L.Text("media.done")&&File.Exists(converter.Rows.Single().Target),"real image conversion completes while its tool stays hidden in the dock");
            main.UpdateLayout();main.VerifyStableDock(check);
            bool restored=false;var copy=main.AddDockTransferForTests("Kopírování",delegate{restored=true;});var ftp=main.AddDockTransferForTests("FTP · testovací přenos",delegate{});main.RefreshTaskDock();main.UpdateLayout();
            var tiles=FluentUiTests.Children<ActivityTile>(main).ToArray();check(tiles.Length==5,"two transfer activities coexist with three background tools");
            check(tiles.Select(x=>x.TranslatePoint(new Point(),main).Y).Distinct().Count()==1&&tiles.Count(x=>x.ActualWidth>250)==3,"transfers share the same row with compact SMART/analyzer tiles and use remaining width");
            check(tiles.All(x=>x.ActualWidth>=230&&x.ActualHeight>=76&&x.ActualHeight<=88),"dock tiles retain usable width and enough height for larger text");
            check(tiles.All(x=>Convert.ToString(x.Background)=="#FFFFFFFF"),"background task tiles retain white card surfaces on the blue-grey base");var first=tiles[0];var scroll=FluentUiTests.Children<ScrollViewer>(main).First(x=>FluentUiTests.Children<ActivityTile>(x).Any());check(scroll.ComputedHorizontalScrollBarVisibility!=Visibility.Visible,"dock rows and compact tool tiles do not require horizontal scrolling");
            NextStageTests.Draw(main,Path.Combine(root,"five-activity-tiles.png"));
            var local=tiles.Single(x=>FluentUiTests.Children<TextBlock>(x).Any(t=>t.Text=="Kopírování"));FluentUiTests.Children<Button>(local).Single(x=>Convert.ToString(x.Content).StartsWith("↗")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(restored,"each transfer tile opens its own details action");
            copy.Finish("done",true);main.RefreshTaskDock();check(main.DockTileCountForTests==4,"completed transfer disappears without removing unrelated tools");ftp.Finish("done",true);
            main.DockToolsForTests.Single(x=>x.Window==smart).Placement.Restore();check(main.DockTileCountForTests==2,"restoring SMART leaves analyzer and converter in the dock");smart.Close();windows.Remove(smart);
            foreach(var window in windows.ToArray()){window.Close();windows.Remove(window);}main.RefreshTaskDock();check(main.DockTileCountForTests==0,"closing hidden tools removes their registrations and tiles");
            var wide=main.AddDockTransferForTests("Kopírování",delegate{});main.RefreshTaskDock();main.UpdateLayout();main.RefreshTaskDock();main.UpdateLayout();var single=FluentUiTests.Children<ActivityTile>(main).Single();check(Math.Abs(single.ActualWidth-main.CopyDockWidthForTests)<=3,"single copy uses the entire available dock width");NextStageTests.Draw(main,Path.Combine(root,"full-width-copy.png"));wide.Finish("done",true);
            var login=new SshLoginDialog(main,new NetworkProfile{Protocol=NetworkProtocol.SSH,Host="fixture.invalid",UserName="",Path="/srv/data"});windows.Add(login);Open(login);check(!FluentUiTests.Children<Button>(login).Single(x=>Convert.ToString(x.Content)==L.Text("ssh.loginContinue")).IsEnabled,"SSH cannot continue with an empty user");login.User.Text="chosen-user";var profile=new NetworkProfile{UserName="windows-fallback",PrivateKeyPath="fixture",SshAgent=true};login.Apply(profile);check(profile.UserName=="chosen-user"&&profile.SshPasswordOnly&&!profile.SshAgent&&profile.PrivateKeyPath=="","SSH defaults to chosen identity and password authentication");NextStageTests.Draw(login,Path.Combine(root,"ssh-login.png"));login.Close();windows.Remove(login);
            main.Close();main=null;File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
        }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
        finally{foreach(var w in windows.ToArray())w.Close();if(main!=null)main.Close();}
    }
    static void Open(Window window){window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-18000;window.Top=-18000;window.ShowActivated=false;window.Show();NavigationTests.Wait(Task.Delay(90));window.UpdateLayout();}
 }
}
#endif
