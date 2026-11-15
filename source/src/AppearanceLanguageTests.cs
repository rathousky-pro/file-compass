using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
    internal static class AppearanceLanguageTests {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); ConfigurationDialog dialog = null; MainWindow main = null;
            string installed = AppPaths.Resource("languages"), originalLanguage = String.IsNullOrEmpty(L.Language)?L.DefaultLanguage:L.Language, originalMode = AppearanceTheme.Mode, originalBackdrop = AppearanceTheme.Backdrop;
            Action<bool,string> check = (valid,label) => { if (!valid) throw new Exception(label); checks.Add(label); };
            try {
                FluentTheme.Ensure(); AppearanceTheme.Apply("Light");
                var surface = (SolidColorBrush)CompactTheme.WhiteSurface; var text = (SolidColorBrush)FluentTheme.Text;
                var oldSurface = surface.Color; var oldText = text.Color;
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Left=-18000, Top=-18000, Width=1200, Height=760, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main); main.UpdateLayout();
                AppearanceTheme.Apply("Dark");main.UpdateLayout();check(AppearanceTheme.IsDark&&surface.Color!=oldSurface&&text.Color!=oldText,"dark theme updates existing surface and text brushes");NextStageTests.Draw(main,Path.Combine(root,"main-dark.png"));string example=Path.Combine(root,"dark-example.json");File.WriteAllText(example,"{\n  \"name\": \"File Compass\",\n  \"enabled\": true\n}");var darkEditor=new CodeEditorWindow(main){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};darkEditor.Show();darkEditor.Add(EditorTextFile.Load(example));darkEditor.UpdateLayout();NextStageTests.Draw(darkEditor,Path.Combine(root,"editor-dark.png"));darkEditor.Close();var copyDialog=new CopySetupDialog(main,new[]{new Entry{FullPath=example,Name="dark-example.json",Bytes=32}},root,new string[0],false,new ProgramSettings()){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};copyDialog.Show();copyDialog.UpdateLayout();NextStageTests.Draw(copyDialog,Path.Combine(root,"copy-dark.png"));copyDialog.Close();dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,Width=1080,Height=760,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();dialog.FitSettingsPage();var paletteChoices=FluentUiTests.Children<RadioButton>(dialog).Where(x=>x.GroupName=="FluentColor").ToArray();check(paletteChoices.Length==15 && paletteChoices.All(x=>x.IsVisible && x.TranslatePoint(new Point(0,x.ActualHeight),dialog).Y < dialog.ActualHeight-60),"all fifteen color choices fit in dark appearance settings");NextStageTests.Draw(dialog,Path.Combine(root,"settings-dark.png"));dialog.Close();dialog=null; var darkBefore=surface.Color;var frameBefore=((SolidColorBrush)CompactTheme.Chrome).Color;FluentColors.Select("ruby");check(AppearanceTheme.IsDark&&((SolidColorBrush)CompactTheme.Chrome).Color!=frameBefore,"palette remains adjustable in dark mode");FluentColors.Select("azure");check(surface.Color==darkBefore,"restoring palette restores dark surface");AppearanceTheme.Apply("Light");main.UpdateLayout();check(!AppearanceTheme.IsDark&&surface.Color==oldSurface&&text.Color==oldText,"light theme restores existing brushes");
                check(text.Color.R+100<surface.Color.R,"layered appearance maintains readable text contrast");
                NextStageTests.Draw(main,Path.Combine(root,"main-layered.png"));
                dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();
                check(dialog.LanguageChoice.Items.Count==9,"appearance settings retain all nine languages");var modeChoice=FluentUiTests.Children<ComboBox>(dialog).Single(x=>System.Windows.Automation.AutomationProperties.GetAutomationId(x)=="AppearanceMode");modeChoice.SelectedIndex=1;check(AppearanceTheme.IsDark,"appearance selector previews dark mode");dialog.SaveChanges();check(ProgramSettings.Load().ColorTheme=="Dark","dark mode is saved from settings and survives reload");modeChoice.SelectedIndex=0;check(!AppearanceTheme.IsDark,"appearance selector restores light mode");dialog.SaveChanges();check(ProgramSettings.Load().ColorTheme=="Light","light mode is saved from settings and survives reload");
                check(!FluentUiTests.Children<TextBlock>(dialog).Any(x=>x.Text==L.Text("settings.interfaceMotif")||x.Text==L.Text("settings.backdrop")),"retired theme selectors are absent");
                dialog.UpdateLayout();dialog.FitSettingsPage();NextStageTests.Draw(dialog,Path.Combine(root,"settings-layered.png"));dialog.Close();dialog=null;
                var preferences=new ProgramSettings{InterfaceMotif="Classic",ColorTheme="Dark",WindowBackdrop="MicaAlt"};string config=Path.Combine(root,"settings.xml");preferences.Save(config);
                var migrated=ProgramSettings.Load(config);check(migrated.InterfaceMotif=="Clean"&&migrated.ColorTheme=="Dark"&&migrated.WindowBackdrop=="None","saving settings preserves dark theme while migrating retired options");
                check(AppearanceTheme.Normalize("invalid")=="Light"&&AppearanceTheme.NormalizeMotif("invalid")=="Clean","unknown legacy values resolve to the new appearance"); var json=new JavaScriptSerializer();
                var cs=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed,"cs.lng")));
                var en=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed,"en.lng")));
                check(cs.Keys.OrderBy(x=>x).SequenceEqual(en.Keys.OrderBy(x=>x)), "English catalog covers every Czech key");
                check(cs.All(item => Regex.Matches(item.Value,@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x).SequenceEqual(Regex.Matches(en[item.Key],@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x))), "English translations preserve all format arguments");
                check(en.All(x=>!x.Value.Contains("[[FC")&&!x.Value.Contains("â€")&&!x.Value.Contains("Ã")), "English catalog contains no translation markers or corrupted UTF-8");
                foreach(var code in L.SupportedLanguages.Where(x=>x!="cs"&&x!="en")) {
                    var catalog=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed,code+".lng")));
                    check(catalog["_language"]==code&&cs.Keys.OrderBy(x=>x).SequenceEqual(catalog.Keys.OrderBy(x=>x)),code+": catalog metadata and all keys are present");
                    check(cs.All(item=>Regex.Matches(item.Value,@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x).SequenceEqual(Regex.Matches(catalog[item.Key],@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x))),code+": format arguments are preserved");
                    foreach(var key in new[]{"internal.sshExitStatusScript","proxy.basicAuthorizationHeader","clipboard.panelDataFormat","ssh.passwordPromptMatch"})check(catalog[key]==cs[key],code+": technical value stays unchanged: "+key);
                    File.Copy(Path.Combine(installed,code+".lng"),Path.Combine(root,code+".lng"),true);L.SaveSelection(code,root);check(File.ReadAllText(Path.Combine(root,"language.txt"))==code,code+": language choice persists");
                    L.LoadForTests(installed,code);check(L.Text("action.copy")==catalog["action.copy"]&&L.Text("preview.modeAuto")=="Auto",code+": translated interface loads and automatic preview caption is correct");
                    dialog=new ConfigurationDialog(main){Left=-18000,Top=-18000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();dialog.FitSettingsPage();
                    check(dialog.LanguageChoice.Items.Cast<string>().SequenceEqual(L.LanguageNames)&&dialog.LanguageChoice.SelectedIndex>=0,code+": settings offer native language names and retain a valid choice");
                    NextStageTests.Draw(dialog,Path.Combine(root,"settings-"+code+".png"));dialog.Close();dialog=null;
                }
                foreach(var key in new[]{"internal.sshExitStatusScript","proxy.basicAuthorizationHeader","clipboard.panelDataFormat","ssh.passwordPromptMatch"}) check(en[key]==cs[key], "Technical protocol value stays unchanged: "+key);
                File.Copy(Path.Combine(installed,"cs.lng"),Path.Combine(root,"cs.lng"),true); File.Copy(Path.Combine(installed,"en.lng"),Path.Combine(root,"en.lng"),true);
                L.SaveSelection("en",root); check(File.ReadAllText(Path.Combine(root,"language.txt"))=="en", "Language selection is saved without changing current UI mid-dialog");
                L.LoadForTests(installed,"en"); check(L.Text("action.copy")=="Copy"&&L.Text("action.cancel")=="Cancel"&&L.Text("menu.file")=="File", "English language is loaded with standard file-manager captions");
                main.Close(); main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Left=-18000, Top=-18000, Width=1200, Height=760, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=WindowStartupLocation.Manual }; main.Show(); NavigationTests.WaitPanels(main);
                NextStageTests.Draw(main,Path.Combine(root,"main-english.png"));
                dialog=new ConfigurationDialog(main){ Left=-18000, Top=-18000, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=WindowStartupLocation.Manual }; dialog.Show(); dialog.UpdateLayout(); dialog.FitSettingsPage();
                check((string)dialog.Sections.Items[0]=="Appearance", "Settings categories are translated into English");
                NextStageTests.Draw(dialog,Path.Combine(root,"settings-english.png"));
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"})); return 0;
            } catch(Exception ex) { File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL: "+ex); return 1; }
            finally { if(dialog!=null)dialog.Close(); if(main!=null)main.Close(); AppearanceTheme.SystemDarkOverride=null; AppearanceTheme.Backdrop = originalBackdrop; AppearanceTheme.Apply(originalMode); L.LoadForTests(installed,originalLanguage); }
        }
    }
}
