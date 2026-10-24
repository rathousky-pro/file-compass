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
            string installed = AppPaths.Resource("languages"), originalLanguage = L.Language, originalMode = AppearanceTheme.Mode, originalBackdrop = AppearanceTheme.Backdrop;
            Action<bool,string> check = (valid,label) => { if (!valid) throw new Exception(label); checks.Add(label); };
            try {
                FluentTheme.Ensure(); AppearanceTheme.Apply("Light");
                var surface = (SolidColorBrush)CompactTheme.WhiteSurface; var text = (SolidColorBrush)FluentTheme.Text;
                var oldSurface = surface.Color; var oldText = text.Color;
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Left=-18000, Top=-18000, Width=1200, Height=760, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main); main.UpdateLayout();
                AppearanceTheme.Apply("Dark"); main.UpdateLayout();
                check(AppearanceTheme.IsDark && surface.Color != oldSurface && text.Color != oldText, "Dark theme updates existing shared surfaces and text after styles are sealed");
                check(text.Color.R > surface.Color.R + 100, "Dark theme maintains readable text contrast");
                check(!surface.IsFrozen && !surface.CanFreeze, "Palette bindings prevent styles from freezing live theme brushes");
                NextStageTests.Draw(main, Path.Combine(root,"main-dark.png"));
                AppearanceTheme.Apply("Light"); check(surface.Color == oldSurface && text.Color == oldText, "Light theme restores original palette without rebuilding windows");
                AppearanceTheme.SystemDarkOverride = () => true; AppearanceTheme.Apply("System");
                check(AppearanceTheme.IsDark, "System mode follows the Windows dark application preference");
                AppearanceTheme.SystemDarkOverride = () => false; AppearanceTheme.RefreshSystem();
                check(!AppearanceTheme.IsDark, "System preference changes update the palette while running");
                AppearanceTheme.Apply("Dark"); AppearanceTheme.RefreshSystem(); check(AppearanceTheme.IsDark, "Explicit dark choice is independent of Windows preference");
                AppearanceTheme.Apply("Light");
                dialog = new ConfigurationDialog(main) { Left=-18000, Top=-18000, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=WindowStartupLocation.Manual }; dialog.Show();
                check(dialog.ColorThemeChoice.Items.Count == 3 && dialog.LanguageChoice.Items.Count == 8, "Appearance settings expose three themes and all eight languages");
                check(dialog.BackdropChoice.Items.Count == 3, "Backdrop settings expose Solid, Mica and Mica Alt");
                dialog.BackdropChoice.SelectedIndex = 2; check(AppearanceTheme.Backdrop == "MicaAlt", "Mica Alt previews immediately");
                int nativeBackdrop; int nativeResult = DwmGetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(main).Handle, 38, out nativeBackdrop, sizeof(int));
                check(nativeResult != 0 || nativeBackdrop == 4 || main.Background != Brushes.Transparent, "Native Mica Alt is applied, or opaque fallback is retained");
                check(((SolidColorBrush)CompactTheme.WhiteSurface).Color.A == 255, "File content surfaces remain opaque with Mica Alt");
                AppearanceTheme.Apply("Dark"); check(AppearanceTheme.IsDark && AppearanceTheme.Backdrop == "MicaAlt", "Mica Alt works independently of light and dark mode"); AppearanceTheme.Apply("Light");
                dialog.UpdateLayout(); dialog.FitSettingsPage(); NextStageTests.Draw(dialog,Path.Combine(root,"settings-mica-alt.png"));
                dialog.BackdropChoice.SelectedIndex = 1; check(AppearanceTheme.Backdrop == "Mica", "Mica can be selected independently");
                dialog.ColorThemeChoice.SelectedIndex = 1; dialog.UpdateLayout(); dialog.FitSettingsPage(); check(AppearanceTheme.IsDark, "Theme choice previews immediately");
                NextStageTests.Draw(dialog,Path.Combine(root,"settings-dark.png")); dialog.Close(); dialog=null;
                check(AppearanceTheme.Backdrop == originalBackdrop, "Cancel restores the original backdrop"); check(AppearanceTheme.Mode == "Light", "Cancel restores the theme that was active before opening settings");
                var preferences = new ProgramSettings { ColorTheme="Dark", WindowBackdrop="MicaAlt" }; string config=Path.Combine(root,"settings.xml"); preferences.Save(config);
                check(ProgramSettings.Load(config).ColorTheme == "Dark" && AppearanceTheme.Normalize("invalid") == "System", "Theme choice persists and invalid values fall back to System");
                check(ProgramSettings.Load(config).WindowBackdrop == "MicaAlt" && AppearanceTheme.NormalizeBackdrop("invalid") == "None", "Backdrop persists and unknown backdrop falls back to solid"); var json=new JavaScriptSerializer();
                var cs=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed,"cs-CZ.lng")));
                var en=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed,"en.lng")));
                check(cs.Keys.OrderBy(x=>x).SequenceEqual(en.Keys.OrderBy(x=>x)), "English catalog covers every Czech key");
                check(cs.All(item => Regex.Matches(item.Value,@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x).SequenceEqual(Regex.Matches(en[item.Key],@"\{\d+[^}]*\}").Cast<Match>().Select(x=>x.Value).OrderBy(x=>x))), "English translations preserve all format arguments");
                check(en.All(x=>!x.Value.Contains("[[FC")&&!x.Value.Contains("â€")&&!x.Value.Contains("Ã")), "English catalog contains no translation markers or corrupted UTF-8");
                foreach(var code in L.SupportedLanguages.Where(x=>x!="cs-CZ"&&x!="en")) {
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
                File.Copy(Path.Combine(installed,"cs-CZ.lng"),Path.Combine(root,"cs-CZ.lng"),true); File.Copy(Path.Combine(installed,"en.lng"),Path.Combine(root,"en.lng"),true);
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
