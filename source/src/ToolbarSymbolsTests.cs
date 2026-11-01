#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace FileCompass {
 internal static class ToolbarSymbolsTests {
  internal static string Pixels(ImageSource source){var visual=new DrawingVisual();using(var c=visual.RenderOpen())c.DrawImage(source,new Rect(0,0,32,32));var bitmap=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var bytes=new byte[4096];bitmap.CopyPixels(bytes,128,0);return Convert.ToBase64String(bytes);}
  static bool Monochrome(Drawing drawing){var group=drawing as DrawingGroup;if(group!=null)return group.Children.All(Monochrome);var geometry=drawing as GeometryDrawing;if(geometry==null)return true;return new[]{geometry.Brush,geometry.Pen==null?null:geometry.Pen.Brush}.All(brush=>{var solid=brush as SolidColorBrush;return brush==null||solid!=null&&(solid.Color.A==0||solid.Color.ToString()=="#FF234B70"||solid.Color.ToString()=="#FFFFFFFF");});}
  internal static int Run(string root){Directory.CreateDirectory(root);var log=new List<string>();MainWindow main=null;var theme=ToolbarThemeSettings.Current;string motif=AppearanceTheme.Motif;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);log.Add(label);};try{
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));FluentTheme.Ensure();L.ChangeLanguage("cs");AppearanceTheme.Apply("Light");
   foreach(string name in ToolbarThemeDialog.PreviewNames.Concat(new[]{"copy","move","delete","network","archive-test","document","lock"}).Distinct()){
    var bitmap=DesktopIcons.ToolbarDrawing(name,ToolbarTheme.Classic) as BitmapSource;
    check(bitmap!=null&&bitmap.PixelWidth>=256&&bitmap.PixelHeight>=256,"Fluent asset has resolution for 32 DIP at high DPI: "+name);
   }
   foreach(string name in ToolbarThemeDialog.PreviewNames){var versions=Enum.GetValues(typeof(ToolbarTheme)).Cast<ToolbarTheme>().Select(x=>Pixels(DesktopIcons.ToolbarDrawing(name,x))).ToArray();check(versions.Distinct().Count()==3,"Three distinct icon styles: "+name);}
   foreach(ToolbarTheme style in Enum.GetValues(typeof(ToolbarTheme)))check(new[]{"panels-SideBySide","panels-Stacked","panels-Single"}.All(name=>Pixels(DesktopIcons.ToolbarDrawing("view",style))!=Pixels(DesktopIcons.ToolbarDrawing(name,style))),"Preview is visually distinct from every panel layout: "+style);
   foreach(string name in ToolbarSymbols.Names)check(Monochrome(((DrawingImage)DesktopIcons.ToolbarDrawing(name,ToolbarTheme.Outline)).Drawing),"Outline uses only the shared monochrome ink: "+name);
   foreach(ToolbarTheme style in Enum.GetValues(typeof(ToolbarTheme)))check(new[]{"hidden-files-visible","hidden-files-hidden","hidden-files-partial"}.Select(x=>Pixels(DesktopIcons.ToolbarDrawing(x,style))).Distinct().Count()==3,"Visibility states remain distinct: "+style);
   var gallery=new StackPanel();foreach(ToolbarTheme style in Enum.GetValues(typeof(ToolbarTheme))){gallery.Children.Add(new TextBlock{Text=style.ToString(),Margin=new Thickness(8)});var row=new WrapPanel();foreach(string name in ToolbarThemeDialog.PreviewNames){var tile=new StackPanel{Width=92,Margin=new Thickness(3)};tile.Children.Add(new Image{Source=DesktopIcons.ToolbarDrawing(name,style),Width=28,Height=28});tile.Children.Add(new TextBlock{Text=name,FontSize=9,TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center});row.Children.Add(tile);}gallery.Children.Add(row);}var preview=new Window{Content=gallery,Width=1160,Height=600};NetworkTests.Render(preview,Path.Combine(root,"icon-gallery.png"));preview.Close();
   File.WriteAllText(Path.Combine(root,"visible.txt"),"visible");string hidden=Path.Combine(root,"hidden.txt"),system=Path.Combine(root,"system.txt");File.WriteAllText(hidden,"hidden");File.SetAttributes(hidden,FileAttributes.Hidden);File.WriteAllText(system,"system");File.SetAttributes(system,FileAttributes.System);
   main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Width=1740,Height=820,Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);main.VerifyToolbarSymbols(root,check);
   File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null)main.Close();ToolbarThemeSettings.Current=theme;AppearanceTheme.SetMotif(motif);}}
 }
 public partial class MainWindow {
  internal void VerifyToolbarSymbols(string root,Action<bool,string> check){
   foreach(string motif in new[]{"Classic","Clean"}){AppearanceTheme.SetMotif(motif);foreach(ToolbarTheme theme in Enum.GetValues(typeof(ToolbarTheme))){ToolbarThemeSettings.Current=theme;ApplyToolbarTheme(theme);var settings=new ProgramSettings{AutoRefresh=false,ShowHiddenFiles=true,ShowSystemFiles=true};
     foreach(double size in new[]{8.0,16.0,20.0,24.0,32.0}){settings.ButtonIconSize=size;ApplyDisplaySettings(settings);UpdateLayout();check(toolbarImages.All(x=>x.Width==size&&x.Height==size),motif+" / "+theme+": every toolbar icon follows configured size "+size);check(toolbarImages.Where(x=>x.Parent is Button||x.Parent is StackPanel&&((StackPanel)x.Parent).Parent is Button).All(x=>{var button=x.Parent as Button??((StackPanel)x.Parent).Parent as Button;return button.ActualHeight>=size+4;}),motif+" / "+theme+": enlarged icons fit their buttons at "+size);if(size==32&&theme==ToolbarTheme.Classic)NextStageTests.Draw(this,Path.Combine(root,"toolbar-"+motif+"-Fluent-32.png"));}
     settings.ButtonIconSize=20;ApplyDisplaySettings(settings);NavigationTests.Wait(Task.Delay(100));UpdateLayout();
     var buttons=toolbarImages.Where(x=>x.Parent is Button||x.Parent is StackPanel&&((StackPanel)x.Parent).Parent is Button).ToArray();check(buttons.Length>=15&&buttons.All(x=>Object.ReferenceEquals(x.Source,DesktopIcons.ToolbarDrawing(Convert.ToString(x.Tag),theme))),motif+" / "+theme+": all toolbar actions honor the selected icon set");
     var eye=toolbarAllFilesButton.Content as Image;check(eye!=null&&Convert.ToString(eye.Tag)=="hidden-files-visible"&&Convert.ToString(toolbarAllFilesButton.ToolTip).StartsWith(L.Text("toolbar.allFilesYes"))&&left.List.Items.Cast<Entry>().Any(x=>x.FullName=="hidden.txt"),motif+" / "+theme+": shown state matches icon, tooltip and visible files");
     toolbarAllFilesButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Convert.ToString(eye.Tag)=="hidden-files-hidden"&&Convert.ToString(toolbarAllFilesButton.ToolTip).StartsWith(L.Text("toolbar.allFilesNo"))&&!left.List.Items.Cast<Entry>().Any(x=>x.FullName=="hidden.txt"||x.FullName=="system.txt"),motif+" / "+theme+": clicking hides hidden/system files and updates the icon");
     toolbarAllFilesButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Convert.ToString(eye.Tag)=="hidden-files-visible"&&left.List.Items.Cast<Entry>().Any(x=>x.FullName=="hidden.txt"),motif+" / "+theme+": clicking again reveals files and restores the shown icon");
     settings.ShowSystemFiles=false;ApplyDisplaySettings(settings);check(Convert.ToString(eye.Tag)=="hidden-files-partial"&&Convert.ToString(toolbarAllFilesButton.ToolTip).StartsWith(L.Text("toolbar.allFilesPartial"))&&left.List.Items.Cast<Entry>().Any(x=>x.FullName=="hidden.txt")&&!left.List.Items.Cast<Entry>().Any(x=>x.FullName=="system.txt"),motif+" / "+theme+": mixed configuration is reported explicitly");
     settings.ShowSystemFiles=true;ApplyDisplaySettings(settings);UpdateLayout();NextStageTests.Draw(this,Path.Combine(root,"toolbar-"+motif+"-"+theme+".png"));
   }}
  }
 }
}
#endif
