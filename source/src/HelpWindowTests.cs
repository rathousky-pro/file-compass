#if APP_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
namespace FileCompass {
    internal static class HelpWindowTests {
        static IEnumerable<Hyperlink> Links(BlockCollection blocks) {
            foreach(Block block in blocks) {
                var paragraph=block as Paragraph;if(paragraph!=null)foreach(var link in Links(paragraph.Inlines))yield return link;
                var section=block as Section;if(section!=null)foreach(var link in Links(section.Blocks))yield return link;
                var list=block as System.Windows.Documents.List;if(list!=null)foreach(ListItem item in list.ListItems)foreach(var link in Links(item.Blocks))yield return link;
            }
        }
        static IEnumerable<Hyperlink> Links(InlineCollection inlines) {
            foreach(Inline inline in inlines){var link=inline as Hyperlink;if(link!=null)yield return link;var span=inline as Span;if(span!=null)foreach(var child in Links(span.Inlines))yield return child;}
        }
        internal static int Run(string root) {
            Directory.CreateDirectory(root);int checks=0;string original=L.Language;Window owner=null;HelpWindow help=null;AboutWindow about=null;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
            try {
                FluentTheme.Ensure();L.ChangeLanguage("cs-CZ");
                owner=new MainWindow(root,new ProgramSettings{AutoRefresh=false}){Left=-18000,Top=-18000,Width=1000,Height=700,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};owner.Show();NavigationTests.WaitPanels((MainWindow)owner);
                help=HelpService.Open(owner,"copy");help.ShowActivated=false;
                check(help.IsVisible&&help.Owner==owner&&help.Article.Document!=null,"Help opens as a native owned window");
                check(Object.ReferenceEquals(help,HelpService.Open(owner,"copy")),"Repeated help command reuses its window");
                var cs=HelpContent.Load();check(cs.Count>=30&&cs.Select(t=>t.Id).Distinct().Count()==cs.Count,"Complete catalog and unique IDs");
                check(!typeof(HelpContent).Assembly.GetManifestResourceNames().Any(x=>x.StartsWith("FileCompass.Help.")),"Help content and screenshots are not embedded in the executable");
                check(File.Exists(Path.Combine(HelpContent.Root,"index.html"))&&File.Exists(Path.Combine(HelpContent.Root,"help.css"))&&File.Exists(Path.Combine(HelpContent.Root,"help.js")),"Standalone offline browser package exists");
                check(HelpContent.BrowserUri("copy").IsFile&&HelpContent.BrowserUri("copy").Fragment=="#copy"&&help.BrowserButton.Content.ToString()==L.Text("help.openBrowser"),"Browser action points to the current external topic");
                byte[] originalPage=File.ReadAllBytes(HelpContent.PagePath);
                try{string page=File.ReadAllText(HelpContent.PagePath);File.WriteAllText(HelpContent.PagePath,page.Replace("data-title=\""+System.Security.SecurityElement.Escape(cs[0].Title)+"\"","data-title=\"External edit test\""));check(HelpContent.Load()[0].Title=="External edit test","HTML changes load without rebuilding the executable");}
                finally{File.WriteAllBytes(HelpContent.PagePath,originalPage);}
                string hiddenPage=HelpContent.PagePath+".test-backup";
                try{File.Move(HelpContent.PagePath,hiddenPage);bool missing=false;try{HelpContent.Load();}catch(FileNotFoundException){missing=true;}check(missing,"Missing external help has no hidden embedded fallback");}
                finally{if(File.Exists(hiddenPage))File.Move(hiddenPage,HelpContent.PagePath);}
                var ids=new HashSet<string>(cs.Select(t=>t.Id));
                foreach(var topic in cs) {
                    help.Navigate(topic.Id);
                    check(help.CurrentTopicId==topic.Id&&help.Article.Document.Blocks.Count>1,"Render native topic "+topic.Id);
                    check(Links(help.Article.Document.Blocks).All(link=>ids.Contains((string)link.Tag)),"Internal hyperlinks are valid: "+topic.Id);
                }
                check(cs.Count>=41,"Detailed workflows are included");check(cs.Where(t=>t.Id!="guide.third-party-notices").All(t=>t.SourceLanguage=="cs-CZ"),"Czech user guides no longer fall back to English");
                foreach(string figure in new[]{"panels","copy","csv","audio","audio-advanced","images","metadata","settings"}){var picture=HelpIllustrations.Load(figure);check(picture.IsFrozen&&picture.PixelWidth>=600&&picture.PixelWidth<=1800,"Embedded Czech illustration "+figure);}
                bool rejected=false;try{HelpIllustrations.Load("../private-file");}catch(ArgumentException){rejected=true;}check(rejected,"Illustrations reject paths and external resources");
                help.Navigate("media-audio");help.UpdateLayout();var pictureButton=(Button)help.Article.Document.Blocks.OfType<BlockUIContainer>().First().Child;pictureButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var enlargement=Application.Current.Windows.Cast<Window>().Single(x=>x.Owner==help&&x.Title==L.Text("help.zoom"));check(enlargement.IsVisible&&FluentUiTests.Children<Slider>(enlargement).Any(),"Illustration opens a native zoom window");enlargement.Close();help.UpdateLayout();check(((Image)pictureButton.Content).ActualWidth<=help.Article.ActualWidth-40,"Illustrations fit the article width");NextStageTests.Draw(help,Path.Combine(root,"help-illustrated.png"));
                check(HelpContent.ContextTopic(typeof(MediaConverterWindow))=="media"&&HelpContent.ContextTopic(typeof(MediaNamesWindow))=="metadata"&&HelpContent.ContextTopic(typeof(UndoTransactionsWindow))=="undo","New workflows have contextual F1 topics");
                help.Navigate("copy");var conflict=Links(help.Article.Document.Blocks).First(x=>(string)x.Tag=="conflicts");
                check(conflict.Parent is Paragraph&&((Paragraph)conflict.Parent).Inlines.OfType<Bold>().Any(),"Note text and hyperlink share one paragraph");
                conflict.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));check(help.CurrentTopicId=="conflicts","Article hyperlink navigates");
                help.Back();check(help.CurrentTopicId=="copy","Back restores topic");help.Forward();check(help.CurrentTopicId=="conflicts","Forward restores topic");
                help.Back();help.Navigate("network");check(!help.ForwardButton.IsEnabled,"New navigation discards forward history");
                help.Search.Text="kopirovani souboru";check(help.SearchResultCount>0,"Search ignores accents and supports multiple terms");
                help.Search.Text="xyzzzzzz";check(help.SearchResultCount==0,"Empty search result is handled");
                help.Back();check(help.Search.Text==""&&help.CurrentTopicId=="network","Back exits search without losing current topic");
                help.Navigate("not-a-topic");check(help.CurrentTopicId=="home","Missing topic falls back to Home");
                check(HelpContent.ContextTopic(typeof(CopySetupDialog))=="copy"&&HelpContent.ContextTopic(typeof(CopyConflictDialog))=="conflicts","Copy dialog context");
                check(HelpContent.ContextTopic(typeof(NetworkProfileDialog))=="network"&&HelpContent.ContextTopic(typeof(ConfigurationDialog))=="settings","Network and settings context");
                check(HelpContent.ContextTopic(typeof(DiskAnalyzerWindow))=="disk"&&HelpContent.ContextTopic(typeof(FileSearchWindow))=="search","Tool context");
                var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(owner),0,Key.F1){RoutedEvent=Keyboard.PreviewKeyDownEvent};owner.RaiseEvent(key);
                check(key.Handled&&help.CurrentTopicId=="home","Actual F1 routed event opens context help");
                var modal=new ConfigurationDialog(owner){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
                modal.Loaded+=delegate{try{var modalKey=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(modal),0,Key.F1){RoutedEvent=Keyboard.PreviewKeyDownEvent};modal.RaiseEvent(modalKey);var child=Application.Current.Windows.Cast<Window>().OfType<HelpWindow>().Single(x=>x.Owner==modal);check(modalKey.Handled&&child.CurrentTopicId=="settings"&&child.IsEnabled,"F1 help is usable from a modal settings dialog");child.Close();}finally{modal.Close();}};modal.ShowDialog();
                help.Navigate("copy");L.ChangeLanguage("en");LiveLanguage.Refresh();
                check(help.Title==L.Text("help.title")&&help.CurrentTopicId=="copy"&&HelpContent.ContentLanguage=="en","Open help changes language and retains topic");
                foreach(var topic in HelpContent.Load()){help.Navigate(topic.Id);check(help.Article.Document.Blocks.Count>1,"Render English topic "+topic.Id);}
                foreach(string figure in new[]{"panels","copy","csv","audio","audio-advanced","images","metadata","settings"})check(HelpIllustrations.Load(figure).PixelWidth>=600,"Embedded English illustration "+figure);
                L.ChangeLanguage("de");LiveLanguage.Refresh();check(HelpContent.ContentLanguage=="en"&&help.Title==L.Text("help.title"),"Other UI languages show English help with an explicit content language");
                L.ChangeLanguage("cs-CZ");LiveLanguage.Refresh();help.Navigate("copy");
                about=new AboutWindow(owner){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false};about.Show();
                var bitmap=about.DeveloperLogo.Source as BitmapSource;check(bitmap!=null&&bitmap.PixelWidth==685&&bitmap.PixelHeight==161,"Exact attached developer logo is embedded");
                check(about.Logo.Source!=null&&about.Repository.NavigateUri.AbsoluteUri==ApplicationBrand.GitHubUrl,"Application logo and repository remain present");
                check(about.VersionText.Text.Contains(BuildInfo.DisplayVersion)&&about.BuildText.Text.Contains(BuildInfo.Build),"About displays generated version and build");
                check(BuildInfo.BuiltAtUtc.Kind==DateTimeKind.Utc&&BuildInfo.Build.StartsWith(BuildInfo.BuiltAtUtc.ToString("yyyyMMdd")+"."),"Build ID matches actual UTC build date");
                var info=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(BuildInfo).Assembly,typeof(AssemblyInformationalVersionAttribute));
                check(info.InformationalVersion.Contains(BuildInfo.Build),"Executable metadata matches About");
                check(AboutWindow.Diagnostics().Contains(BuildInfo.TimestampUtc)&&!AboutWindow.Diagnostics().Contains(AppPaths.ProfileDirectory),"Copyable diagnostics include timestamp and omit profile paths");
                NextStageTests.Draw(help,Path.Combine(root,"help-light.png"));NextStageTests.Draw(about,Path.Combine(root,"about-light.png"));
                AppearanceTheme.Apply("Dark");NextStageTests.Draw(help,Path.Combine(root,"help-dark.png"));NextStageTests.Draw(about,Path.Combine(root,"about-dark.png"));
                help.Width=700;help.Height=480;help.UpdateLayout();NextStageTests.Draw(help,Path.Combine(root,"help-small.png"));
                File.WriteAllText(Path.Combine(root,"result.txt"),"PASS "+checks+" checks");Console.WriteLine("PASS "+checks+" help/about checks");return 0;
            } catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),ex.ToString());Console.Error.WriteLine(ex);return 1;}
            finally{if(about!=null)about.Close();if(help!=null)help.Close();if(owner!=null)owner.Close();L.ChangeLanguage(original);}
        }
    }
}
#endif
