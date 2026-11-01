#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Threading;
namespace FileCompass {
 internal static class CleanThemeTests {
  static void Open(Window w){SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=-20000;w.Top=-20000;w.ShowActivated=false;w.ShowInTaskbar=false;w.Show();Pump(w);}
  static void Pump(Window w){NavigationTests.Wait(Task.Delay(180));w.UpdateLayout();}
  static string ColorOf(Brush b){return ((SolidColorBrush)b).Color.ToString();}
  internal static int Run(string root){Directory.CreateDirectory(root);var log=new List<string>();var windows=new List<Window>();string motif=AppearanceTheme.Motif,mode=AppearanceTheme.Mode;Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);log.Add(text);};
   try{
    L.Initialize();L.ChangeLanguage("cs-CZ");FluentTheme.Ensure();AppearanceTheme.Apply("Light");AppearanceTheme.SetMotif("Classic");
    check(new ProgramSettings().InterfaceMotif=="Clean"&&AppearanceTheme.NormalizeMotif("Balanced")=="Clean"&&AppearanceTheme.NormalizeMotif("Original")=="Clean","all legacy preferences migrate to the single layered appearance");
    var chrome=CompactTheme.Chrome;check(ColorOf(chrome)=="#FFCFDEEC","one blue-grey base is active even for a legacy Classic request");
    string sample=Path.Combine(root,"Ukázka.txt");File.WriteAllText(sample,"Clean theme fixture");var entries=new List<Entry>{new Entry{Name="Ukázka.txt",FullPath=sample,Bytes=19}};
    var main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Width=1280,Height=820};windows.Add(main);Open(main);NavigationTests.WaitPanels(main);
    var copy=new CopySetupDialog(main,entries,root,new string[0]);windows.Add(copy);Open(copy);var originalContent=copy.Content;
    NextStageTests.Draw(main,Path.Combine(root,"classic-main.png"));NextStageTests.Draw(copy,Path.Combine(root,"classic-copy.png"));
    AppearanceTheme.SetMotif("Clean");Pump(copy);
    check(AppearanceTheme.IsClean&&Object.ReferenceEquals(chrome,CompactTheme.Chrome)&&ColorOf(chrome)=="#FFCFDEEC","Clean updates shared brushes without replacing references");
    var nav=FluentUiTests.Children<ListBox>(copy).Single();var more=More(copy);
    check(more.IsVisible&&Convert.ToString(more.Content)=="Více"&&!nav.IsVisible&&nav.SelectedIndex==0,"Clean starts on Basic with More at the left of the footer");
    Click(more);Pump(copy);check(nav.IsVisible&&nav.SelectedIndex==0,"More reveals navigation with Basic selected");
    nav.SelectedIndex=3;Pump(copy);var verify=FluentUiTests.Children<CheckBox>(copy).Single(x=>Convert.ToString(x.Content)==L.Text("copy.verifySha256Option"));verify.IsChecked=true;
    check(verify.IsVisible,"advanced controls replace the basic content in the main area");
    NextStageTests.Draw(copy,Path.Combine(root,"clean-copy-verification.png"));Click(more);Pump(copy);
    check(!nav.IsVisible&&nav.SelectedIndex==0&&copy.CreateOptions(false).Verify,"collapsing returns to Basic and preserves advanced values");
    var pack=new ArchivePackDialog(main,1,Path.Combine(root,"Dokumenty.zip"));windows.Add(pack);Open(pack);Click(More(pack));Pump(pack);var packNav=FluentUiTests.Children<ListBox>(pack).Single();packNav.SelectedIndex=3;Pump(pack);
    var protect=FluentUiTests.Children<CheckBox>(pack).Single(x=>Convert.ToString(x.Content)==L.Text("archive.setPassword"));protect.IsChecked=true;var secret=FluentUiTests.Children<PasswordBox>(pack).Single();secret.Password="fixture-only";packNav.SelectedIndex=1;packNav.SelectedIndex=3;
    check(secret.Password=="fixture-only"&&protect.IsChecked==true,"archive password survives page switching");
    NextStageTests.Draw(pack,Path.Combine(root,"clean-archive-security.png"));
    check(FluentUiTests.Children<CheckBox>(pack).Single(x=>Convert.ToString(x.Content)==L.Text("archive.selfExtracting")).IsEnabled,"SFX is available for the supported ZIP format");
    var profile=new NetworkProfileDialog(main,new NetworkProfile{Name="Fixture",Host="fixture.invalid",Protocol=NetworkProtocol.SFTP});windows.Add(profile);Open(profile);check(More(profile).IsVisible,"connection options expose More");Click(More(profile));profile.Tabs.SelectedIndex=1;Pump(profile);NextStageTests.Draw(profile,Path.Combine(root,"clean-network.png"));
    var search=new FileSearchWindow(main,root,true,true,delegate{return 0;},delegate{});windows.Add(search);Open(search);check(More(search).IsVisible,"search options expose More");NextStageTests.Draw(search,Path.Combine(root,"clean-search.png"));
    var media=new MediaConverterWindow(main,new string[0],root,true,delegate{},delegate{});windows.Add(media);Open(media);check(More(media).IsVisible,"media settings expose More");Click(More(media));media.SettingsTabs.SelectedIndex=1;Pump(media);NextStageTests.Draw(media,Path.Combine(root,"clean-media.png"));
    var smart=new DiskSmartWindow(main,false);windows.Add(smart);Open(smart);NextStageTests.Draw(smart,Path.Combine(root,"clean-smart.png"));
    var attributes=new AttributesDialog(main,new[]{sample},delegate{});windows.Add(attributes);Open(attributes);check(More(attributes).IsVisible,"additional file timestamps use the same More navigation");Click(More(attributes));FluentUiTests.Children<TabControl>(attributes).Single().SelectedIndex=1;Pump(attributes);NextStageTests.Draw(attributes,Path.Combine(root,"clean-attributes.png"));
    var viewer=new ImageViewerWindow(main,Path.Combine(root,"classic-main.png"));windows.Add(viewer);Open(viewer);NavigationTests.Wait(viewer.LoadingTask);NextStageTests.Draw(viewer,Path.Combine(root,"clean-preview.png"));
    var error=new CopyErrorDialog(main,new CopyFailure{Source=sample,Target=sample+".copy",Error=new IOException("Ukázková chyba")});windows.Add(error);Open(error);check(InterfaceTheme.IsFramed(error),"auxiliary dialogs inherit the shared frame");NextStageTests.Draw(error,Path.Combine(root,"clean-error.png"));
    foreach(var w in windows){Pump(w);check(ColorOf(w.Background)==ColorOf(WindowGlass.Background(w)),w.Title+": window uses the shared blue-grey base");}
    var cards=FluentUiTests.Children<Border>(copy).Where(x=>Convert.ToString(x.Tag)=="dialog-header"||Convert.ToString(x.Tag)=="dialog-footer").ToArray();check(cards.Length==2&&cards.All(x=>ColorOf(x.Background)=="#FFFFFFFF"&&x.CornerRadius.TopLeft==11),"operation header and footer are separate rounded white cards");
    check(FluentUiTests.Children<Border>(copy).Any(x=>Convert.ToString(x.Tag)=="operation-page-card"&&ColorOf(x.Background)=="#FFFFFFFF"),"operation form sits on its own rounded white card");
    check(AdvancedToolsTests.Visual<GroupBox>(copy).All(x=>ColorOf(x.Background)=="#FFEDF3F9"),"logical option groups use pale blue-grey wells inside the white card");
    var filePanels=FluentUiTests.Children<FilePanel>(main).ToArray();check(filePanels.Length==2&&filePanels.All(x=>x.CornerRadius.TopLeft==12&&ColorOf(x.Background)=="#FFFFFFFF"),"both file panels are rounded white cards");
    NextStageTests.Draw(main,Path.Combine(root,"clean-main.png"));
    var savedIcons=ToolbarThemeSettings.Current;foreach(ToolbarTheme icons in Enum.GetValues(typeof(ToolbarTheme))){ToolbarThemeSettings.Current=icons;main.ApplyToolbarTheme(icons);Pump(main);check(FluentUiTests.Children<Image>(main).Where(x=>Convert.ToString(x.Tag)=="pack").All(x=>Object.ReferenceEquals(x.Source,DesktopIcons.ToolbarDrawing("pack",icons))),"Clean honors configured button icon set: "+icons);}ToolbarThemeSettings.Current=savedIcons;main.ApplyToolbarTheme(savedIcons);
    check(FluentUiTests.Children<Button>(main).Where(x=>(x.Content is Image||x.Content is StackPanel)&&!Convert.ToString(x.Tag).StartsWith("drive:")).All(x=>Object.ReferenceEquals(x.Background,CompactTheme.WhiteSurface)),"toolbar button surfaces remain neutral, including enabled view toggles");
    bool safeDefault=false;var messageTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};messageTimer.Tick+=delegate{var message=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x.Title=="Clean confirmation fixture");if(message==null)return;messageTimer.Stop();var no=FluentUiTests.Children<Button>(message).Single(x=>Convert.ToString(x.Content)==L.Text("action.no"));safeDefault=no.IsDefault;NextStageTests.Draw(message,Path.Combine(root,"clean-message.png"));Click(no);};messageTimer.Start();var answer=MessageBox.Show(main,"Ukázková potvrzovací zpráva. Zachovat bezpečnou výchozí odpověď?","Clean confirmation fixture",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No);messageTimer.Stop();check(safeDefault&&answer==MessageBoxResult.No,"Clean confirmation retains the caller's safe default and exact result");
    AppearanceTheme.Apply("Dark");Pump(copy);check(ColorOf(chrome)=="#FFCFDEEC"&&!AppearanceTheme.IsDark,"legacy dark preferences cannot reintroduce another palette");NextStageTests.Draw(copy,Path.Combine(root,"clean-copy-dark.png"));AppearanceTheme.Apply("Light");
    AppearanceTheme.SetMotif("Classic");Pump(copy);check(ColorOf(chrome)=="#FFCFDEEC"&&Object.ReferenceEquals(originalContent,copy.Content)&&!nav.IsVisible&&more.IsVisible,"legacy motif requests retain the new dialog without nesting frames");
    check(More(attributes).IsVisible,"timestamp options retain the shared sidebar");
    for(int i=0;i<3;i++){AppearanceTheme.SetMotif("Clean");AppearanceTheme.SetMotif("Classic");}check(Object.ReferenceEquals(originalContent,copy.Content)&&FluentUiTests.Children<Button>(copy).Count(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="CleanMore")==1,"repeated switching does not duplicate More or dialog frames");
    var saved=new ProgramSettings{InterfaceMotif="Clean"};saved.Save();check(ProgramSettings.Load().InterfaceMotif=="Clean","Clean persists in signed settings");
    var config=new ConfigurationDialog(main);windows.Add(config);Open(config);check(!FluentUiTests.Children<TextBlock>(config).Any(x=>x.Text==L.Text("settings.interfaceMotif")||x.Text==L.Text("settings.colorTheme")||x.Text==L.Text("settings.backdrop")),"settings contains no retired motif, color or backdrop options");Pump(config);check(AppearanceTheme.IsClean,"motif selection previews immediately");NextStageTests.Draw(config,Path.Combine(root,"clean-settings.png"));config.Close();windows.Remove(config);check(AppearanceTheme.IsClean,"Cancel keeps the single layered appearance");
    saved.InterfaceMotif="Classic";saved.Save();check(ProgramSettings.Load().InterfaceMotif=="Clean","saving a legacy motif normalizes it to the permanent appearance");
    File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"PASS: "+log.Count+" checks"}));return 0;
   }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),log.Concat(new[]{"FAIL: "+ex}));return 1;}finally{foreach(var w in windows.ToArray())w.Close();AppearanceTheme.SetMotif(motif);AppearanceTheme.Apply(mode);}
  }
  static Button More(Window w){return FluentUiTests.Children<Button>(w).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="CleanMore");}
  static void Click(Button b){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 }
}
#endif
