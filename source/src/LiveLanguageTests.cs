using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class LiveLanguageTests {
        static void ExternalToolsRegression(string root,MainWindow main,Action<bool,string> check) {
            var original=ArchiveSettings.Load();string oldRar=original.WinRarPath,oldArj=original.ArjPath,oldLha=original.LhaPath;
            string tool=Path.Combine(root,"archiver-fixture.exe");File.WriteAllText(tool,"archiver fixture");
            original.WinRarPath=original.ArjPath=original.LhaPath=tool;original.Save();
            ToolTrust.Approve(tool);string store=Path.Combine(AppPaths.ConfigDirectory,"trusted-tools.xml");byte[] valid=File.ReadAllBytes(store);
            try {
                File.AppendAllText(store,"\n");byte[] invalid=File.ReadAllBytes(store);bool rejected=false;
                try{ConfigurationIntegrity.Read<TrustedToolList>(store);}catch(ConfigurationIntegrityException){rejected=true;}
                check(rejected,"fixture reproduces invalid trusted-tools.xml from the reported error");
                var dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                try{dialog.LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,"en");dialog.SaveChanges();}
                finally{dialog.Close();}
                check(L.Language=="en"&&L.PreferredLanguage=="en","language saves and updates immediately with unchanged archivers and invalid tool trust store");
                check(FluentUiTests.Children<MenuItem>(main).Any(x=>Convert.ToString(x.Header)==L.Text("menu.file")),"actual main menu updates after this settings save");
                check(File.ReadAllBytes(store).SequenceEqual(invalid),"unrelated settings save leaves invalid approval bytes untouched");
                rejected=false;try{using(ToolTrust.Acquire(tool)){} }catch(IOException){rejected=true;}
                check(rejected,"invalid approvals still cannot authorize external executable launch");
                File.WriteAllBytes(store,valid);File.AppendAllText(tool," changed");
                dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                try{dialog.LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,"cs-CZ");dialog.SaveChanges();}finally{dialog.Close();}
                rejected=false;try{using(ToolTrust.Acquire(tool)){} }catch(IOException){rejected=true;}
                check(rejected,"saving the same path does not silently approve a changed executable");
                File.Delete(tool);
                dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                try{dialog.LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,"en");dialog.SaveChanges();}finally{dialog.Close();}
                check(L.Language=="en"&&L.PreferredLanguage=="en","an unavailable unchanged archiver does not block a language-only save");
                string chosen=Path.Combine(root,"explicit-new-archiver.exe");File.WriteAllText(chosen,"explicit fixture");
                dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                try{var input=(TextBox)typeof(ConfigurationDialog).GetField("arjPath",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(dialog);input.Text=chosen;dialog.SaveChanges();}finally{dialog.Close();}
                using(ToolTrust.Acquire(chosen)){check(true,"explicitly changed archiver is approved on Save");}
            } finally {File.WriteAllBytes(store,valid);original.WinRarPath=oldRar;original.ArjPath=oldArj;original.LhaPath=oldLha;original.Save();}
        }


        internal static int Run(string root) {
            Directory.CreateDirectory(root);string original=L.Language;MainWindow main=null;AboutWindow about=null;Window probe=null;int checks=0;
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);checks++;};
            try {
                L.ChangeLanguage("en");File.WriteAllText(Path.Combine(root,"File"),"user content");
                main=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};main.Show();NavigationTests.WaitPanels(main);
                var panels=FluentUiTests.Children<FilePanel>(main).ToArray();var panel=panels[0];var source=panel.List.ItemsSource;var file=panel.List.Items.Cast<Entry>().Single(x=>x.Name=="File");panel.List.SelectedItem=file;
                var label=new TextBlock{Text=L.Text("action.copy")};var userLabel=new TextBlock{Text=new string(label.Text.ToCharArray())};var editor=new TextBox{Text=L.Text("action.copy")};
                var formatted=new TextBlock{Text=L.Format("delete.chooseFileAction","File")};var choices=new ComboBox();choices.Items.Add(L.Text("action.copy"));choices.Items.Add(L.Text("action.move"));choices.SelectedIndex=1;int selectionEvents=0;choices.SelectionChanged+=delegate{selectionEvents++;};
                var stack=new StackPanel();foreach(var control in new FrameworkElement[]{label,userLabel,editor,formatted,choices})stack.Children.Add(control);
                probe=new Window{Content=stack,Width=450,Height=230,Left=-18000,Top=-18000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};probe.Show();
                about=new AboutWindow(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};about.Show();about.UpdateLayout();
                check(about.Logo.Source!=null&&about.Repository.NavigateUri.AbsoluteUri==ApplicationBrand.GitHubUrl,"About displays the application logo and exact repository URL");
                check(FluentUiTests.Children<TextBlock>(about).Any(x=>x.Text==ApplicationBrand.Developer),"About displays the developer identity");
                string canonical=L.Code("state.ready");
                foreach(string language in new[]{"cs-CZ","de","fr","en","pl","es","pt","nl","en"}) {
                    L.ChangeLanguage(language);LiveLanguage.Refresh(main);LiveLanguage.Refresh(probe);LiveLanguage.Refresh(about);
                    check(label.Text==L.Text("action.copy"),"existing static caption switches to "+language);
                    check(formatted.Text==L.Format("delete.chooseFileAction","File"),"formatted caption switches without altering its argument: "+language);
                    check(userLabel.Text=="Copy"&&editor.Text=="Copy","equal user text and editable input are preserved: "+language);
                    probe.UpdateLayout();check(choices.SelectedIndex==1&&selectionEvents==0&&AdvancedToolsTests.Visual<TextBlock>(choices).Any(x=>x.Text==L.Text("action.move")),"translated option preserves selection without firing actions: "+language+"; index="+choices.SelectedIndex+"; events="+selectionEvents+"; template="+(choices.ItemTemplate!=null)+"; value="+L.TranslateExisting(Convert.ToString(choices.SelectedItem))+"; captions="+String.Join("|",AdvancedToolsTests.Visual<TextBlock>(choices).Select(x=>x.Text)));
                    check(ReferenceEquals(panel.List.ItemsSource,source)&&ReferenceEquals(panel.List.SelectedItem,file)&&file.Name=="File"&&panel.CurrentPath==root,"language change preserves panels, file rows and selection: "+language);
                    check(FluentUiTests.Children<MenuItem>(main).Any(x=>Convert.ToString(x.Header)==L.Text("menu.file"))&&about.Title==L.Text("app.aboutMenu"),"existing menus and About title update: "+language);
                    check(L.Code("state.ready")==canonical&&main.IsVisible&&probe.IsVisible,"canonical state and open windows survive: "+language);
                }
                bool rejected=false;try{L.ChangeLanguage("unsupported");}catch(ArgumentException){rejected=true;}check(rejected&&L.Language=="en","invalid language leaves the active catalog unchanged");
                NextStageTests.Draw(about,Path.Combine(root,"about-light.png"));AppearanceTheme.Apply("Dark");NextStageTests.Draw(about,Path.Combine(root,"about-dark.png"));
                {
                    var settings=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                    settings.LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,"cs-CZ");
                    settings.Dispatcher.BeginInvoke(new Action(()=>FluentUiTests.Children<Button>(settings).First(x=>Convert.ToString(x.Content)==L.Text("action.save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                    check(settings.ShowDialog()==true&&L.Language=="cs-CZ"&&L.PreferredLanguage=="cs-CZ","saving settings changes and persists the language without restart");
                    settings=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};settings.LanguageChoice.SelectedIndex=Array.IndexOf(L.SupportedLanguages,"en");
                    settings.Dispatcher.BeginInvoke(new Action(()=>FluentUiTests.Children<Button>(settings).First(x=>Convert.ToString(x.Content)==L.Text("action.cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                    check(settings.ShowDialog()==false&&L.Language=="cs-CZ"&&L.PreferredLanguage=="cs-CZ","cancel keeps the active and saved language unchanged");
                }
                ExternalToolsRegression(root,main,check);
                File.WriteAllText(Path.Combine(root,"result.txt"),"PASS: "+checks+" checks");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL after "+checks+" checks: "+ex);return 1;}
            finally{if(about!=null)about.Close();if(probe!=null)probe.Close();if(main!=null)main.Close();L.SaveSelection(original);L.ChangeLanguage(original);}
        }
    }
}
