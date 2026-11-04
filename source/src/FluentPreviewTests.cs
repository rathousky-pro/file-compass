#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
 internal static class FluentPreviewTests {
  internal static int Run(string root){
   Directory.CreateDirectory(root);var log=new List<string>();MainWindow main=null;
   Action<bool,string> check=delegate(bool ok,string text){if(!ok)throw new Exception(text);log.Add(text);};
   try{
    L.ChangeLanguage("cs-CZ");
    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
    var sample=Directory.CreateDirectory(Path.Combine(root,"Ukázkové soubory")).FullName;
    File.WriteAllText(Path.Combine(sample,"Návrh rozhraní.txt"),"Ukázková data pro Fluent Preview.");
    main=new MainWindow(sample,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);foreach(var panel in FluentUiTests.Children<FilePanel>(main)){panel.RefreshDriveBar();NavigationTests.Wait(panel.DriveDiscovery);}
    var shared=(SolidColorBrush)CompactTheme.Chrome;
    foreach(var p in FluentColors.All){FluentColors.Select(p.Id);check(shared.Color==(Color)ColorConverter.ConvertFromString(p.Frame),p.Name+": existing chrome brush changes live");var prefs=new ProgramSettings{FluentColor=p.Id};var path=Path.Combine(root,p.Id+".xml");prefs.Save(path);check(ProgramSettings.Load(path).FluentColor==p.Id,p.Name+": settings roundtrip");NextStageTests.Draw(main,Path.Combine(root,"main-"+p.Id+".png"));}
    FluentColors.Select("azure");
    var settings=new ConfigurationDialog(main);settings.Show();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));
    FluentUiTests.Children<SettingsPager>(settings).Single().Select("Barevný");
    var ocean=FluentUiTests.Children<RadioButton>(settings).Single(x=>Convert.ToString(x.ToolTip)=="Petrolejová a ledová");ocean.IsChecked=true;check(FluentColors.Current.Id=="ocean","settings previews chosen palette");NextStageTests.Draw(settings,Path.Combine(root,"settings.png"));check(settings.WindowStyle==WindowStyle.None,"settings uses a single custom window header");for(int section=0;section<settings.Sections.Items.Count;section++){settings.Sections.SelectedIndex=section;NavigationTests.Wait(System.Threading.Tasks.Task.Delay(80));NextStageTests.Draw(settings,Path.Combine(root,"settings-page-"+section+".png"));check(FluentUiTests.Children<SettingsPager>(settings).Any(x=>x.IsVisible),"settings category "+section+" has bounded subpages");}settings.Close();check(FluentColors.Current.Id=="azure","closing settings rolls preview back");
    settings=new ConfigurationDialog(main);settings.Show();FluentUiTests.Children<SettingsPager>(settings).Single().Select("Barevný");FluentUiTests.Children<RadioButton>(settings).Single(x=>Convert.ToString(x.ToolTip)=="Modrofialový charakter").IsChecked=true;
    FluentUiTests.Children<Button>(settings).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="ConfigurationApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));settings.Close();check(ProgramSettings.Load().FluentColor=="indigo"&&FluentColors.Current.Id=="indigo","Apply persists palette and advances cancellation baseline");
    FluentColors.Select("azure");main.ToggleFluentDrawer();NextStageTests.Draw(main,Path.Combine(root,"menu.png"));main.ToggleFluentDrawer();
    var entries=new List<Entry>{new Entry{FullPath=Path.Combine(sample,"Návrh rozhraní.txt"),Name="Návrh rozhraní.txt"}};
    var searchBox=FluentUiTests.Children<TextBox>(main).Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Hledat soubor");searchBox.Text="Návrh";check(FluentUiTests.Children<FilePanel>(main).First().List.Items.Cast<Entry>().All(x=>x.IsParent||x.Name.Contains("Návrh")),"toolbar search filters the active file panel");searchBox.Clear();
    Action<Window,string> capture=delegate(Window w,string name){w.Left=-18000;w.Top=-18000;w.WindowStartupLocation=WindowStartupLocation.Manual;w.Show();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(80));check(w.AllowsTransparency&&((FrameworkElement)w.Content).Margin.Left==30,name+": real transparent shadow margin survives loaded styling");NextStageTests.Draw(w,Path.Combine(root,name+".png"));w.Close();};
    capture(new CopySetupDialog(main,entries,sample,new string[0]),"copy");
    var expandedCopy=new CopySetupDialog(main,entries,sample,new string[0]);expandedCopy.Left=-18000;expandedCopy.Show();CleanOptions.Reveal(expandedCopy);NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));NextStageTests.Draw(expandedCopy,Path.Combine(root,"copy-sidebar.png"));expandedCopy.Close();
    var modal=new CopySetupDialog(main,entries,sample,new string[0]);Exception modalError=null;
    modal.Dispatcher.BeginInvoke(new Action(async delegate{try{await System.Threading.Tasks.Task.Delay(120);check(modal.WindowStyle==WindowStyle.None,"copy dialog removes native duplicate title bar");check(main.Content is FrameworkElement&&((FrameworkElement)main.Content).Effect is System.Windows.Media.Effects.BlurEffect,"modal dialog blurs its disabled owner");check(FluentUiTests.Children<CheckBox>(modal).Any(x=>x.IsVisible&&Convert.ToString(x.Content).Contains("SHA")),"verification is directly visible on the basic copy form");modal.Hide();check(((FrameworkElement)main.Content).Effect==null,"hiding a modal releases the owner blur before Close");}catch(Exception ex){modalError=ex;}finally{modal.Close();}}));modal.ShowDialog();if(modalError!=null)throw modalError;check(((FrameworkElement)main.Content).Effect==null,"closing modal restores the owner rendering");
    capture(new ArchivePackDialog(main,1,Path.Combine(sample,"Archiv.zip")),"archive");
    capture(new DeleteConfirmationDialog(main,entries,true,false,sample),"delete");
    using(var deleteControl=new CopyControl()){var w=new FluentDeleteProgress(main,deleteControl,deleteControl.Cancel);w.Update(entries[0].FullPath,2,5,true);w.Left=-18000;w.Show();NextStageTests.Draw(w,Path.Combine(root,"delete-progress.png"));w.Finish();}
    capture(new CopyConflictDialog(main,new CopyConflict{Source=entries[0].FullPath,Target=Path.Combine(sample,"Záloha.txt"),SourceSize=35,TargetSize=40,SourceTime=DateTime.Today,TargetTime=DateTime.Today.AddDays(-1)}),"conflict");
    using(var control=new CopyControl()){var w=new CopyProgressDialog(main,control);w.Update(new CopyUpdate{Phase="Kopírování",Source=entries[0].FullPath,Target=Path.Combine(sample,"Záloha.txt"),FileDone=64,FileTotal=100,TotalDone=64,TotalBytes=100,Completed=1,TotalFiles=3});w.Left=-18000;w.Show();NextStageTests.Draw(w,Path.Combine(root,"copy-progress.png"));w.Finish();w.Close();}
    using(var cancel=new CancellationTokenSource()){var w=new ArchiveProgressDialog(main,"Komprese · Archiv.zip",cancel);w.Update("Zpracovávám 2 ze 3 souborů",.64,true);w.UpdateCurrentFile(entries[0].FullPath);w.Left=-18000;w.Show();NextStageTests.Draw(w,Path.Combine(root,"archive-progress.png"));w.Finish();w.Close();}
    var about=new AboutWindow(main){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false};about.Show();NavigationTests.Wait(System.Threading.Tasks.Task.Delay(100));check(about.VersionText.Text.Contains(BuildInfo.DisplayVersion)&&about.BuildText.Text.Contains(BuildInfo.Build),"about shows actual version and build");NextStageTests.Draw(about,Path.Combine(root,"about.png"));foreach(string light in new[]{"sky","mint","silver"}){FluentColors.Select(light);NextStageTests.Draw(about,Path.Combine(root,"about-"+light+".png"));}about.Close();
    var disposable=Path.Combine(sample,"Odstranit-test.txt");File.WriteAllText(disposable,"Only test data");NavigationTests.Wait(main.VerifyFluentDeletion(disposable,sample));check(!File.Exists(disposable),"real deletion completes through the new progress window");
    main.Close();main=null;File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(main!=null)main.Close();}
  }
 }
}
#endif
