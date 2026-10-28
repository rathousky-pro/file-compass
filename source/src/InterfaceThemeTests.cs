#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;
namespace FileCompass {
 internal static class InterfaceThemeTests {
  static void Open(Window window){SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-20000;window.Top=-20000;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();NavigationTests.Wait(Task.Delay(150));window.UpdateLayout();}
  static string ColorOf(Brush brush){return ((SolidColorBrush)brush).Color.ToString();}
  internal static int Run(string folder){Directory.CreateDirectory(folder);var checks=new List<string>();var windows=new List<Window>();string oldMotif=AppearanceTheme.Motif,oldMode=AppearanceTheme.Mode;
   Action<bool,string> check=(yes,name)=>{if(!yes)throw new Exception(name);checks.Add(name);};
   try{
    L.Initialize();L.ChangeLanguage("cs-CZ");FluentTheme.Ensure();AppearanceTheme.Apply("Light");AppearanceTheme.SetMotif("Original");
    check(new ProgramSettings().InterfaceMotif=="Balanced"&&AppearanceTheme.NormalizeMotif("Original")=="Balanced","legacy and invalid settings migrate to the single appearance");
    var chrome=CompactTheme.Chrome;var original=ColorOf(chrome);var main=new MainWindow(folder,new ProgramSettings{AutoRefresh=false}){Width=1200,Height=780};windows.Add(main);Open(main);NavigationTests.WaitPanels(main);
    string sample=Path.Combine(folder,"Nabídka.txt");File.WriteAllText(sample,"Demonstration");var entries=new List<Entry>{new Entry{FullPath=sample,Name="Nabídka.txt",Bytes=13}};
    var setup=new CopySetupDialog(main,entries,folder,new string[0]);windows.Add(setup);Open(setup);var originalContent=setup.Content;var margin=((FrameworkElement)originalContent).Margin;NextStageTests.Draw(main,Path.Combine(folder,"original-main.png"));
    var config=new ConfigurationDialog(main);windows.Add(config);Open(config);AppearanceTheme.SetMotif("Original");NavigationTests.Wait(Task.Delay(80));
    check(AppearanceTheme.IsBalanced&&Object.ReferenceEquals(chrome,CompactTheme.Chrome)&&ColorOf(chrome)=="#FFECEEF1","settings preview updates shared brushes immediately without replacing references");
    check(InterfaceTheme.IsFramed(setup),"an already-open dialog receives balanced framing");
    check(FluentUiTests.Children<FilePanel>(main).All(x=>x.BorderThickness.Left==1),"both file panels use thin outlines");
    NextStageTests.Draw(main,Path.Combine(folder,"balanced-main.png"));NextStageTests.Draw(setup,Path.Combine(folder,"balanced-copy.png"));NextStageTests.Draw(config,Path.Combine(folder,"balanced-settings.png"));
    config.Close();windows.Remove(config);check(AppearanceTheme.IsBalanced&&ColorOf(chrome)==original,"closing settings retains the sole appearance");
    check(Object.ReferenceEquals(setup.Content,originalContent)&&((FrameworkElement)setup.Content).Margin==margin,"cancel restores original dialog content and spacing");
    check(FluentUiTests.Children<FilePanel>(main).All(x=>x.BorderThickness.Left==1),"panels retain the clean thin outline");
    for(int i=0;i<5;i++){AppearanceTheme.SetMotif("Balanced");AppearanceTheme.SetMotif("Original");}check(Object.ReferenceEquals(setup.Content,originalContent),"repeated switching does not accumulate nested frames");
    AppearanceTheme.SetMotif("Balanced");
    using(var control=new CopyControl()){var progress=new CopyProgressDialog(main,control);Open(progress);progress.Update(new CopyUpdate{Source=sample,Target=sample+".copy",FileDone=58,FileTotal=100,TotalDone=54,TotalBytes=100,Completed=18,TotalFiles=42,BytesPerSecond=120000000,RemainingSeconds=12});NextStageTests.Draw(progress,Path.Combine(folder,"balanced-progress.png"));progress.Finish();}
    var fresh=new CopyErrorDialog(main,new CopyFailure{Source=sample,Target=sample+".copy",Error=new IOException("Ukázková chyba")});windows.Add(fresh);Open(fresh);check(FluentUiTests.Children<Border>(fresh).Any(x=>x.BorderThickness.Left==1&&x.CornerRadius.TopLeft==8),"newly opened auxiliary dialogs inherit balanced framing");NextStageTests.Draw(fresh,Path.Combine(folder,"balanced-error.png"));
    var slider=new Slider();var bar=new ProgressBar{Value=50};var controls=new Window{Content=new StackPanel{Children={slider,bar}},Width=400,Height=180};windows.Add(controls);Open(controls);check(ColorOf(bar.Foreground)==ColorOf(FluentTheme.Accent)&&ColorOf(slider.Foreground)==ColorOf(FluentTheme.Accent),"progress bars and sliders use the shared blue accent: "+ColorOf(bar.Foreground)+" / "+ColorOf(slider.Foreground)+" expected "+ColorOf(FluentTheme.Accent));
    AppearanceTheme.Apply("Dark");check(AppearanceTheme.IsBalanced&&ColorOf(chrome)=="#FF25292F","balanced motif supports dark mode independently");NextStageTests.Draw(setup,Path.Combine(folder,"balanced-copy-dark.png"));
    AppearanceTheme.Apply("Light");var saved=new ProgramSettings{InterfaceMotif="Balanced",ColorTheme="Light"};saved.Save();check(ProgramSettings.Load().InterfaceMotif=="Balanced","motif persists in the signed settings file");
    config=new ConfigurationDialog(main);windows.Add(config);Open(config);check(!FluentUiTests.Children<TextBlock>(config).Any(x=>x.Text==L.Text("settings.interfaceMotif")&&x.IsVisible),"settings contains no motif selector");config.SaveChanges();check(ProgramSettings.Load().InterfaceMotif=="Balanced","settings saves only the single appearance");config.Close();windows.Remove(config);
    AppearanceTheme.SetMotif("Original");saved.InterfaceMotif="Original";saved.Save();check(ProgramSettings.Load().InterfaceMotif=="Balanced","saving a legacy motif migrates to the clean appearance");
    File.WriteAllLines(Path.Combine(folder,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(folder,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{foreach(var w in windows.ToArray())w.Close();AppearanceTheme.SetMotif(oldMotif);AppearanceTheme.Apply(oldMode);}
  }
 }
}
#endif
