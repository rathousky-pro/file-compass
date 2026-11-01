#if APP_TESTS
using System;using System.Threading;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
namespace FileCompass {
 internal sealed partial class QuickPreviewPane {
  internal void VerifyCard(Window owner,string root,Action<bool,string> check){
   var close=AdvancedToolsTests.Visual<Button>(this).Single(x=>Convert.ToString(x.Tag)=="preview-close");close.Focus();owner.UpdateLayout();var frame=close.Template.FindName("Frame",close) as Border;check(close.FocusVisualStyle==null&&frame!=null&&frame.BorderThickness==new Thickness(1),"focused Close button has one clean border without an extra focus outline");
   check(Object.ReferenceEquals(carouselToggle.Parent,basicInfo.Parent)&&DockPanel.GetDock(carouselToggle)==Dock.Left,"More sits at the beginning of the file information row");
   check(((System.Windows.Shapes.Path)((StackPanel)carouselToggle.Content).Children[0]).Data.GetFlattenedPathGeometry().Figures[0].StartPoint.Y==0,"collapsed More chevron points down");
   check(!selectionFrame.IsVisible,"folder carousel is hidden by default");SetCarouselExpanded(true);owner.UpdateLayout();check(selectionFrame.IsVisible,"More reveals the folder carousel");check(((System.Windows.Shapes.Path)((StackPanel)carouselToggle.Content).Children[0]).Data.GetFlattenedPathGeometry().Figures[0].StartPoint.Y==5,"expanded Less chevron points up");
   check(!String.IsNullOrEmpty(basicInfo.Text),"basic file information is present without expanding details");
   check(selectionFrame.TranslatePoint(new Point(),this).Y>picture.TranslatePoint(new Point(),this).Y+picture.ActualHeight-1,"folder carousel is below the enlarged preview");
   check(Metadata!=null&&basicInfo.Text.Contains("48"),"image dimensions appear in the immediate summary");
   ShowDetailsPopup();var popup=detailsPopup;check(popup!=null&&popup.IsVisible&&popup.Owner==Window.GetWindow(this),"Details opens an owned detail popup without shrinking the preview");NextStageTests.Draw(popup,Path.Combine(root,"details-popup.png"));
   PreviewCardTests.Escape(popup,true);check(!popup.IsVisible,"Escape closes the detail popup even when the key event was previously handled");
   var original=selectionFiles;var callback=openSelection;var context=selectionContext;var many=Enumerable.Range(0,24).Select(i=>new Entry{Name="item"+i,FullPath=Path.Combine(root,"item"+i+".txt")}).ToArray();SetSelection(many,"wheel",delegate{},false);owner.UpdateLayout();var scroll=CarouselScroll(selectionStrip);double before=scroll.HorizontalOffset;selectionStrip.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});owner.UpdateLayout();check(scroll.HorizontalOffset>before&&scroll.ComputedHorizontalScrollBarVisibility!=Visibility.Visible,"mouse wheel scrolls the carousel without a scrollbar over thumbnails");var icons=AdvancedToolsTests.Visual<Image>(selectionStrip).Where(x=>x.IsVisible).ToArray();check(icons.Length>0&&icons.All(x=>x.ActualHeight<=23),"document icons remain small rather than stretching to photograph height");SetSelection(original,context,callback,true);SetCarouselExpanded(false);owner.UpdateLayout();check(!selectionFrame.IsVisible,"Less hides the carousel again");

  }
 }
 public partial class FilePanel {
  internal void VerifyLocations(Action<bool,string> check){
   check(driveButtons.Values.All(x=>x.Margin.Left<=1.1&&x.Margin.Right<=1.1),"drive buttons retain tight spacing after the Clean theme is applied");check(FluentUiTests.Children<Button>(this).Any(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="PanelRoot"),"panel header includes the root-folder shortcut");
   check(driveSelector.Items.Cast<DriveChoice>().All(x=>x.IsLocation)&&driveSelector.Items.Count>=5,"locations dropdown contains common folders and no duplicate drives");
   check(cleanPaneGrid.RowDefinitions[1].ActualHeight==0,"former disk-information row is released for file content");
   check(capacity.TranslatePoint(new Point(),this).Y>List.TranslatePoint(new Point(),this).Y+List.ActualHeight-2,"disk capacity is in the bottom status row");
   check(driveSelector.TranslatePoint(new Point(),this).X>ActualWidth*.6,"folder dropdown is at the right of the drive-button row");
  }
 }
 public partial class MainWindow {
  internal async Task VerifyRecycleSkipAll(string root,Action<bool,string> check){
   string a=Path.Combine(root,"skipall-a.txt"),b=Path.Combine(root,"skipall-b.txt"),good=Path.Combine(root,"skipall-good.txt");foreach(string path in new[]{a,b,good})File.WriteAllText(path,"fixture");int prompts=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
   timer.Tick+=delegate{foreach(var dialog in OwnedWindows.OfType<CopyErrorDialog>().Where(x=>x.IsVisible).ToArray()){prompts++;NextStageTests.Draw(dialog,Path.Combine(root,"locked-file-skip-all.png"));FluentUiTests.Children<Button>(dialog).Single(x=>Convert.ToString(x.Content)==L.Text("conflict.skipAll")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
   using(var first=new FileStream(a,FileMode.Open,FileAccess.Read,FileShare.None))using(var second=new FileStream(b,FileMode.Open,FileAccess.Read,FileShare.None))try{timer.Start();await DeleteBatch(new[]{a,b,good}.Select(x=>new Entry{FullPath=x,Name=Path.GetFileName(x)}).ToList(),null,false,false,root);}finally{timer.Stop();}
   check(prompts==1&&File.Exists(a)&&File.Exists(b)&&!File.Exists(good),"Skip all prompts once for two locked recycle items and continues with unlocked files");
  }
 }
 internal static class PreviewCardTests {
  internal static void Escape(UIElement element,bool handled=false){element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(element),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent,Handled=handled});}
  internal static int Run(string root){Directory.CreateDirectory(root);MainWindow main=null;var log=new List<string>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);};try{
   FluentTheme.Ensure();L.ChangeLanguage("cs-CZ");SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));AppearanceTheme.Apply("Light");AppearanceTheme.SetMotif("Clean");
   string files=Directory.CreateDirectory(Path.Combine(root,"files")).FullName;for(int i=0;i<3;i++){var bitmap=BitmapSource.Create(48,32,96,96,PixelFormats.Bgra32,null,Enumerable.Range(0,48*32).SelectMany(x=>new byte[]{80,150,220,255}).ToArray(),48*4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(files,"image"+i+".png")))encoder.Save(file);}File.WriteAllText(Path.Combine(files,"notes.txt"),"Preview example");File.WriteAllText(Path.Combine(files,"unsupported.fixture"),"no thumbnail");
   main=new MainWindow(files,new ProgramSettings{AutoRefresh=false}){Width=1480,Height=920,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);var panel=FluentUiTests.Children<FilePanel>(main).First();NavigationTests.Wait(panel.DriveDiscovery);main.UpdateLayout();panel.VerifyLocations(check);panel.List.SelectedItem=panel.List.Items.Cast<Entry>().First(x=>x.FullPath.EndsWith("image1.png"));var preview=main.PreviewInOtherPanel(panel);NavigationTests.Wait(preview.LoadingTask);NavigationTests.Wait(preview.MetadataLoadingTask);main.UpdateLayout();
   check(preview.SelectionCount==4&&preview.SelectedPreviewPath.EndsWith("image1.png"),"unmarked folder supplies all previewable files and initially follows the cursor");preview.VerifyCard(main,root,check);NavigationTests.Wait(preview.LoadingTask);NavigationTests.Wait(preview.MetadataLoadingTask);main.UpdateLayout();NextStageTests.Draw(main,Path.Combine(root,"preview-card.png"));
   Escape(preview);check(!main.DockToolsForTests.Any()&&preview.Parent==null,"Escape closes the embedded preview while keeping the main window open");
   string locked=Path.Combine(root,"locked-recycle.txt");File.WriteAllText(locked,"fixture");using(var file=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.None)){bool failed=false;try{DeleteOperations.Local(locked,false);}catch(IOException){failed=true;}check(failed&&File.Exists(locked),"locked recycling reports an application error without opening the Windows error dialog");}
   string recyclable=Path.Combine(root,"recycle-test.txt");File.WriteAllText(recyclable,"fixture");DeleteOperations.Local(recyclable,false);check(!File.Exists(recyclable),"normal recycling still uses the Windows Recycle Bin");
   NavigationTests.Wait(main.VerifyRecycleSkipAll(root,check));
   main.Close();main=null;File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null)main.Close();}}
 }
}
#endif
