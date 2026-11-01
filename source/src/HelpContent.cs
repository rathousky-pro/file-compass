using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows;
using System.Windows.Input;

namespace FileCompass {
    internal sealed class HelpTopic {
        public string Id { get; set; }
        public string Group { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public string SourceLanguage { get; set; }
        public string[] Related { get; set; }
    }
    internal static class HelpContent {
        internal static string ContentLanguage { get { return L.Language=="cs"?"cs":"en"; } }
        internal static string Root { get { return AppPaths.Resource("help"); } }
        internal static string PagePath { get { return Path.Combine(Root,ContentLanguage,"index.html"); } }
        internal static Uri BrowserUri(string topic) { return new Uri(new Uri(PagePath).AbsoluteUri+"#"+Uri.EscapeDataString(topic??"home")); }
        internal static List<HelpTopic> Load() {
            var document=new XmlDocument{XmlResolver=null};
            using(var reader=XmlReader.Create(PagePath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=8*1024*1024}))document.Load(reader);
            var topics=new List<HelpTopic>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(XmlElement article in document.SelectNodes("//article[@data-topic]")) {
                string id=article.GetAttribute("data-topic");
                if(!Regex.IsMatch(id,@"^[a-zA-Z0-9.-]{1,80}$")||!ids.Add(id))throw new InvalidDataException("Invalid help topic.");
                var body=article.SelectSingleNode("div[@class='topic-body']");
                if(body==null)throw new InvalidDataException("Missing help topic body.");
                var native=body.CloneNode(true);
                foreach(XmlElement figure in native.SelectNodes(".//figure[@data-figure]")) {
                    var replacement=document.CreateElement("figure");replacement.SetAttribute("src",figure.GetAttribute("data-figure"));
                    var caption=figure.SelectSingleNode("figcaption");
                    if(caption!=null){var p=document.CreateElement("p");p.InnerText=caption.InnerText;replacement.AppendChild(p);}
                    figure.ParentNode.ReplaceChild(replacement,figure);
                }
                topics.Add(new HelpTopic{Id=id,Title=article.GetAttribute("data-title"),Group=article.GetAttribute("data-group"),Summary=article.GetAttribute("data-summary"),SourceLanguage=article.GetAttribute("data-source-language"),Body=native.InnerXml,Related=article.SelectNodes("nav[@class='related']/a").Cast<XmlElement>().Select(x=>x.GetAttribute("href").TrimStart('#')).ToArray()});
            }
            if(!ids.Contains("home"))throw new InvalidDataException("Missing help home topic.");
            foreach(var topic in topics)if(topic.Related.Any(x=>!ids.Contains(x)))throw new InvalidDataException("Invalid related help topic.");
            return topics;
        }
        internal static string Normalize(string value) {
            var result=new StringBuilder();
            foreach(char c in (value??"").Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(Char.ToLowerInvariant(c));
            return result.ToString().Normalize(NormalizationForm.FormC);
        }
        internal static string SearchText(HelpTopic topic) { return Normalize(topic.Title+" "+topic.Summary+" "+Regex.Replace(topic.Body,"<[^>]*>"," ")); }
        internal static string ContextTopic(Type type) {
            string name=type==null?"":type.Name;
            if(name=="CodeEditorWindow")return "editor";
            if(name=="ExtBrowserWindow")return "ext";
            if(name=="MediaConverterWindow")return "media";
            if(name=="MediaNamesWindow")return "metadata";
            if(name=="UndoTransactionsWindow")return "undo";
            if(name.Contains("Conflict"))return "conflicts";
            if(name.Contains("Copy")||name.Contains("Transfer"))return "copy";
            if(name.Contains("Archive"))return "archives";
            if(name.Contains("Cloud"))return "cloud";
            if(name.Contains("Network")||name=="ConnectionsDialog"||name.Contains("Sshfs"))return "network";
            if(name.Contains("DiskAnalyzer"))return "disk";
            if(name.Contains("DiskSmart"))return "smart";
            if(name.Contains("Rename"))return "rename";
            if(name.Contains("Search")||name.Contains("Duplicate"))return "search";
            if(name.Contains("Compare")||name.Contains("Sync"))return "sync";
            if(name.Contains("Attributes"))return "attributes";
            if(name.Contains("Checksum"))return "checksums";
            if(name.Contains("Viewer")||name.Contains("Editor"))return "preview";
            if(name.Contains("Configuration")||name.Contains("Settings")||name.Contains("Appearance")||name.Contains("ToolbarTheme"))return "settings";
            return "home";
        }
    }
    internal static class HelpService {
        static bool registered;
        static readonly Dictionary<Window,HelpWindow> windows=new Dictionary<Window,HelpWindow>();
        internal static void Ensure() {
            if(registered)return;registered=true;
            EventManager.RegisterClassHandler(typeof(Window),Keyboard.PreviewKeyDownEvent,new KeyEventHandler(OnKey));
        }
        static void OnKey(object sender,KeyEventArgs e) {
            if(e.Key!=Key.F1||Keyboard.Modifiers!=ModifierKeys.None)return;
            e.Handled=true;var owner=sender as Window;
            var help=owner as HelpWindow;
            if(help!=null){help.Navigate("home");return;}
            Open(owner,HelpContent.ContextTopic(owner==null?null:owner.GetType()));
        }
        internal static HelpWindow Open(Window owner,string topic="home") {
            if(owner==null&&Application.Current!=null)owner=Application.Current.MainWindow;
            HelpWindow window;
            if(owner!=null&&windows.TryGetValue(owner,out window)) {
                window.Navigate(topic);if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Show();window.Activate();return window;
            }
            try{window=new HelpWindow(owner);window.Navigate(topic);}
            catch(Exception ex){if(!(ex is IOException)&&!(ex is XmlException)&&!(ex is UnauthorizedAccessException))throw;MessageBox.Show(owner,L.Text("help.unavailable"),L.Text("help.title"),MessageBoxButton.OK,MessageBoxImage.Information);return null;}
            if(owner!=null){windows.Add(owner,window);Window key=owner;window.Closed+=delegate{windows.Remove(key);};}
            window.Show();return window;
        }
    }
}
