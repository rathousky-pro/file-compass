using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;

namespace FileCompass {
    internal sealed class HelpWindow : Window {
        internal readonly TextBox Search = new TextBox { MinWidth=120, VerticalContentAlignment=VerticalAlignment.Center };
        internal readonly TreeView Topics = new TreeView { BorderThickness=new Thickness(0), Padding=new Thickness(8) };
        internal readonly FlowDocumentScrollViewer Article = new FlowDocumentScrollViewer { IsToolBarVisible=false };
        internal readonly Button BackButton,ForwardButton,HomeButton,BrowserButton;
        readonly TextBlock breadcrumb=new TextBlock { Margin=new Thickness(24,12,16,2), Foreground=FluentTheme.Muted };
        readonly TextBlock language=new TextBlock { VerticalAlignment=VerticalAlignment.Center, Foreground=FluentTheme.Muted };
        readonly TextBlock searchLabel=new TextBlock { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(12,0,8,0) };
        readonly List<string> history=new List<string>();
        readonly Dictionary<string,TreeViewItem> treeItems=new Dictionary<string,TreeViewItem>(StringComparer.Ordinal);
        Dictionary<string,HelpTopic> topics;
        Dictionary<string,string> index;
        int position=-1;
        bool updating;
        internal string CurrentTopicId { get { return position<0?"home":history[position]; } }
        internal int SearchResultCount { get; private set; }
        internal HelpWindow(Window owner) {
            Owner=owner;Width=1080;Height=760;MinWidth=680;MinHeight=450;
            WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
            ShowInTaskbar=false;FluentTheme.Dialog(this);ApplicationBrand.Attach(this);
            var root=new DockPanel{Margin=new Thickness(12)};Content=root;
            var toolbar=new DockPanel{Margin=new Thickness(0,0,0,10)};
            DockPanel.SetDock(toolbar,Dock.Top);root.Children.Add(toolbar);
            var navigation=new StackPanel{Orientation=Orientation.Horizontal};
            DockPanel.SetDock(navigation,Dock.Left);toolbar.Children.Add(navigation);
            BackButton=CopyUi.Button(L.Text("help.back"),delegate{Back();});
            ForwardButton=CopyUi.Button(L.Text("help.forward"),delegate{Forward();});
            HomeButton=CopyUi.Button(L.Text("help.home"),delegate{Navigate("home");});
            foreach(var button in new[]{BackButton,ForwardButton,HomeButton}){button.Margin=new Thickness(0,0,6,0);navigation.Children.Add(button);}
            navigation.Children.Add(searchLabel);toolbar.Children.Add(Search);
            var bottom=new DockPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
            var close=CopyUi.Button(L.Text("action.close"),delegate{Close();});close.IsCancel=true;
            close.MinWidth=90;DockPanel.SetDock(close,Dock.Right);bottom.Children.Add(close);
            BrowserButton=CopyUi.Button(L.Text("help.openBrowser"),delegate{try{Process.Start(new ProcessStartInfo(HelpContent.BrowserUri(CurrentTopicId).AbsoluteUri){UseShellExecute=true});}catch(Exception ex){if(!(ex is System.ComponentModel.Win32Exception)&&!(ex is InvalidOperationException))throw;MessageBox.Show(this,L.Text("help.browserFailed"),Title,MessageBoxButton.OK,MessageBoxImage.Information);}});
            BrowserButton.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(BrowserButton,Dock.Right);bottom.Children.Add(BrowserButton);bottom.Children.Add(language);
            var split=new Grid();split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(250),MinWidth=180,MaxWidth=400});
            split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(5)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=280});
            root.Children.Add(split);
            Topics.Background=CompactTheme.PanelSurface;
            ScrollViewer.SetHorizontalScrollBarVisibility(Topics,ScrollBarVisibility.Disabled);
            Topics.SizeChanged+=delegate{UpdateTopicWidths();};
            split.Children.Add(new Border{Child=Topics,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5)});
            var divider=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent};Grid.SetColumn(divider,1);split.Children.Add(divider);
            var page=new DockPanel{Background=CompactTheme.WhiteSurface};DockPanel.SetDock(breadcrumb,Dock.Top);page.Children.Add(breadcrumb);page.Children.Add(Article);
            var pageBorder=new Border{Child=page,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5)};Grid.SetColumn(pageBorder,2);split.Children.Add(pageBorder);
            Article.Background=CompactTheme.WhiteSurface;
            Search.TextChanged+=delegate{if(!updating)Render();};
            Topics.SelectedItemChanged+=delegate{if(!updating){var item=Topics.SelectedItem as TreeViewItem;if(item!=null&&item.Tag is string)Navigate((string)item.Tag);}};
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Close();}else if(e.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;Search.Focus();Search.SelectAll();}else if(e.Key==Key.Left&&Keyboard.Modifiers==ModifierKeys.Alt){e.Handled=true;Back();}else if(e.Key==Key.Right&&Keyboard.Modifiers==ModifierKeys.Alt){e.Handled=true;Forward();}};
            RefreshLanguage();Navigate("home");
        }
        internal void RefreshLanguage() {
            updating=true;
            try {
                Title=L.Text("help.title");BackButton.Content=L.Text("help.back");ForwardButton.Content=L.Text("help.forward");HomeButton.Content=L.Text("help.home");BrowserButton.Content=L.Text("help.openBrowser");
                searchLabel.Text=L.Text("help.search");Search.ToolTip=L.Text("help.search");Topics.ToolTip=L.Text("help.contents");
                language.Text=L.Format("help.contentLanguage",L.Text(HelpContent.ContentLanguage=="cs-CZ"?"help.languageCzech":"help.languageEnglish"));
                var catalog=HelpContent.Load();topics=catalog.ToDictionary(t=>t.Id,StringComparer.Ordinal);
                index=catalog.ToDictionary(t=>t.Id,HelpContent.SearchText,StringComparer.Ordinal);
                Topics.Items.Clear();treeItems.Clear();
                foreach(var group in catalog.GroupBy(t=>t.Group)) {
                    var category=new TreeViewItem{Header=group.Key,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,3,0,5)};
                    foreach(var topic in group){var item=new TreeViewItem{Header=new TextBlock{Text=topic.Title,TextWrapping=TextWrapping.Wrap,MaxWidth=180},Tag=topic.Id,FontWeight=FontWeights.Normal,Margin=new Thickness(0,2,0,2)};category.Items.Add(item);treeItems.Add(topic.Id,item);}
                    Topics.Items.Add(category);
                }
            } finally { updating=false; }
            UpdateTopicWidths();Render();
        }
        void UpdateTopicWidths(){foreach(var item in treeItems.Values){var text=item.Header as TextBlock;if(text!=null)text.MaxWidth=Math.Max(100,Topics.ActualWidth-70);}}
        void ContentLanguage(string code){language.Text=L.Format("help.contentLanguage",L.Text(code=="cs-CZ"?"help.languageCzech":"help.languageEnglish"));}
        internal void Navigate(string id) {
            if(!topics.ContainsKey(id))id="home";
            updating=true;try{Search.Text="";}finally{updating=false;}
            if(position<0||history[position]!=id){if(position<history.Count-1)history.RemoveRange(position+1,history.Count-position-1);history.Add(id);position=history.Count-1;}
            Render();
        }
        internal void Back() {
            if(!String.IsNullOrWhiteSpace(Search.Text)){Search.Clear();return;}
            if(position>0){position--;Render();}
        }
        internal void Forward() { if(position<history.Count-1){position++;updating=true;Search.Clear();updating=false;Render();} }
        FlowDocument Document(string title) {
            var document=new FlowDocument { FontFamily=new FontFamily("Segoe UI"),FontSize=14,Foreground=FluentTheme.Text,Background=CompactTheme.WhiteSurface,PagePadding=new Thickness(25,12,25,25) };
            document.Blocks.Add(new Paragraph(new Run(title)){FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
            return document;
        }
        void Render() {
            if(topics==null)return;
            string query=HelpContent.Normalize(Search.Text.Trim());
            BackButton.IsEnabled=position>0||query.Length>0;ForwardButton.IsEnabled=query.Length==0&&position<history.Count-1;
            if(query.Length>0) {
                foreach(var item in treeItems.Values)item.IsSelected=false;
                var words=query.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                var matches=topics.Values.Where(t=>words.All(word=>index[t.Id].Contains(word))).ToArray();SearchResultCount=matches.Length;
                ContentLanguage(HelpContent.ContentLanguage);breadcrumb.Text=L.Text("help.search");var results=Document(L.Format("help.searchResults",matches.Length));
                if(matches.Length==0)results.Blocks.Add(new Paragraph(new Run(L.Text("help.noResults"))));
                foreach(var topic in matches){var p=new Paragraph{Margin=new Thickness(0,12,0,5)};p.Inlines.Add(Link(topic.Id,topic.Title));results.Blocks.Add(p);results.Blocks.Add(new Paragraph(new Run(topic.Summary)){Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,12)});}
                Article.Document=results;return;
            }
            SearchResultCount=0;
            string id=CurrentTopicId;if(!topics.ContainsKey(id))id="home";var current=topics[id];ContentLanguage(current.SourceLanguage??HelpContent.ContentLanguage);
            updating=true;try{treeItems[id].IsSelected=true;var category=treeItems[id].Parent as TreeViewItem;if(category!=null)category.IsExpanded=true;}finally{updating=false;}
            breadcrumb.Text=L.Text("action.help")+" / "+current.Group;
            var document=Document(current.Title);
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024};
            using(var input=new StringReader("<article>"+current.Body+"</article>"))
            using(var reader=XmlReader.Create(input,settings)){var xml=new XmlDocument{XmlResolver=null};xml.Load(reader);Blocks(xml.DocumentElement,document.Blocks);}
            document.Blocks.Add(new Paragraph(new Run(L.Text("help.related"))){FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,24,0,8)});
            foreach(string related in current.Related){var p=new Paragraph{Margin=new Thickness(0,3,0,3)};p.Inlines.Add(Link(related,topics[related].Title));document.Blocks.Add(p);}
            Article.Document=document;
        }
        Hyperlink Link(string id,string label) {
            var link=new Hyperlink(new Run(label)){Foreground=FluentTheme.Accent,Tag=id};
            link.Click+=delegate(object sender,RoutedEventArgs e){e.Handled=true;Navigate(id);};return link;
        }
        void Blocks(XmlNode parent,BlockCollection blocks) {
            foreach(XmlNode node in parent.ChildNodes) {
                if(node.NodeType==XmlNodeType.Whitespace||node.NodeType==XmlNodeType.Text&&String.IsNullOrWhiteSpace(node.Value))continue;
                if(node.Name=="figure"){blocks.Add(HelpIllustrations.Figure(node.Attributes["src"].Value,Article,this));Blocks(node,blocks);}
                else if(node.Name=="ul"||node.Name=="ol"){
                    var list=new System.Windows.Documents.List{MarkerStyle=node.Name=="ol"?TextMarkerStyle.Decimal:TextMarkerStyle.Disc,Margin=new Thickness(0,4,0,12),Padding=new Thickness(22,0,0,0)};
                    foreach(XmlNode li in node.ChildNodes){if(li.Name!="li")continue;var p=new Paragraph{Margin=new Thickness(0,3,0,5)};Inlines(li,p.Inlines);list.ListItems.Add(new ListItem(p));}blocks.Add(list);
                }else if(node.Name=="div"||node.Name=="section"){var section=new Section{Margin=new Thickness(0,10,0,10)};if(node.Attributes!=null&&node.Attributes["class"]!=null&&node.Attributes["class"].Value.Contains("note")){section.Background=CompactTheme.PanelSurface;section.Padding=new Thickness(12);section.BorderBrush=FluentTheme.Accent;section.BorderThickness=new Thickness(3,0,0,0);}if(node.ChildNodes.Cast<XmlNode>().Any(child=>new[]{"p","h1","h2","h3","ul","ol","div","section"}.Contains(child.Name)))Blocks(node,section.Blocks);else{var p=new Paragraph{Margin=new Thickness(0)};Inlines(node,p.Inlines);section.Blocks.Add(p);}blocks.Add(section);
                }else{
                    var p=new Paragraph{Margin=new Thickness(0,0,0,12)};
                    if(node.Name=="h2"||node.Name=="h3"){p.FontSize=node.Name=="h2"?18:16;p.FontWeight=FontWeights.SemiBold;p.Margin=new Thickness(0,18,0,8);}
                    if(node.NodeType==XmlNodeType.Text)p.Inlines.Add(new Run(node.Value));
                    else if(node.Name=="a")Anchor(node,p.Inlines);
                    else Inlines(node,p.Inlines);
                    blocks.Add(p);
                }
            }
        }
        void Anchor(XmlNode node,InlineCollection output) {
            string href=node.Attributes==null||node.Attributes["href"]==null?"":node.Attributes["href"].Value;
            string id=href.StartsWith("#",StringComparison.Ordinal)?href.Substring(1):"";
            if(topics.ContainsKey(id))output.Add(Link(id,node.InnerText));else output.Add(new Run(node.InnerText));
        }
        void Inlines(XmlNode parent,InlineCollection output) {
            foreach(XmlNode node in parent.ChildNodes) {
                if(node.NodeType==XmlNodeType.Text||node.NodeType==XmlNodeType.SignificantWhitespace)output.Add(new Run(node.Value));
                else if(node.Name=="br")output.Add(new LineBreak());
                else if(node.Name=="a")Anchor(node,output);
                else if(node.Name=="strong"||node.Name=="b"){var bold=new Bold();Inlines(node,bold.Inlines);output.Add(bold);}
                else if(node.Name=="kbd"||node.Name=="code")output.Add(new Run(node.InnerText){FontFamily=new FontFamily("Consolas"),Background=CompactTheme.PanelSurface});
                else Inlines(node,output);
            }
        }
    }
}
