using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    public sealed class MetadataSearchCondition {
        public string Field="width", Operator="gt", Value="";
    }
    internal sealed class MetadataSearchField {
        internal string Key, Kind, Unit;
        internal MetadataSearchField(string key,string kind,string unit=""){Key=key;Kind=kind;Unit=unit;}
        public override string ToString(){return L.Text(Key=="width"||Key=="height"?"metaSearch."+Key:"details."+Key)+(Unit.Length==0?"":" ("+Unit+")");}
    }
    internal static class MetadataSearch {
        internal static readonly MetadataSearchField[] Fields={
            new MetadataSearchField("width","number","px"),new MetadataSearchField("height","number","px"),
            new MetadataSearchField("megapixels","number","Mpx"),new MetadataSearchField("pages","number"),new MetadataSearchField("sheets","number"),new MetadataSearchField("slides","number"),
            new MetadataSearchField("author","text"),new MetadataSearchField("title","text"),new MetadataSearchField("camera","text"),
            new MetadataSearchField("artist","text"),new MetadataSearchField("album","text"),
            new MetadataSearchField("duration","duration","hh:mm:ss"),new MetadataSearchField("taken","date","dd.MM.yyyy"),
            new MetadataSearchField("audioBitrate","number","kb/s")};
        internal static bool Enabled(FileSearchOptions o){return o.MetadataConditions!=null&&o.MetadataConditions.Length>0;}
        internal static MetadataSearchField Field(string key){return Fields.FirstOrDefault(x=>x.Key==key);}
        internal static string[] Operators(string kind){return kind=="text"?new[]{"contains","eq","starts"}:new[]{"gt","ge","eq","lt","le"};}
        internal static object Parse(string value,string kind){
            double number;TimeSpan duration;DateTime date;
            if(kind=="text"){if(String.IsNullOrWhiteSpace(value)||value.Length>1000)throw new FormatException();return value.Trim();}
            if(kind=="number"){
                if(!Double.TryParse(value,NumberStyles.Float,CultureInfo.CurrentCulture,out number)&&!Double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number))throw new FormatException();
                if(number<0||Double.IsNaN(number)||Double.IsInfinity(number))throw new FormatException();return number;
            }
            if(kind=="duration"){if(String.IsNullOrEmpty(value)||value.IndexOf(':' )<0||!TimeSpan.TryParse(value,CultureInfo.InvariantCulture,out duration)||duration<TimeSpan.Zero)throw new FormatException();return duration;}
            if(!DateTime.TryParse(value,CultureInfo.CurrentCulture,DateTimeStyles.None,out date)&&!DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out date))throw new FormatException();return date.Date;
        }
        internal static void Validate(FileSearchOptions o){
            if(!Enabled(o))return;
            if(o.MetadataConditions.Length>20)throw new IOException(L.Text("metaSearch.limit"));
            foreach(var rule in o.MetadataConditions){
                var field=rule==null?null:Field(rule.Field);
                if(field==null||!Operators(field.Kind).Contains(rule.Operator))throw new IOException(L.Text("metaSearch.invalid"));
                try{Parse(rule.Value,field.Kind);}catch{throw new IOException(L.Text("metaSearch.invalid")+": "+field);}
            }
        }
        internal static string Value(FileDetails data,string key){
            if(key=="width"||key=="height"){
                string dims=MetadataColumn.Value(data,"dimensions");
                var match=Regex.Match(dims,@"^(\d+)\s*[×x]\s*(\d+)");
                return match.Success?match.Groups[key=="width"?1:2].Value:"";
            }
            string text=MetadataColumn.Value(data,key);
            if(key=="megapixels"||key=="audioBitrate")text=text.Split(' ')[0];
            return text;
        }
        internal static bool Matches(FileDetails data,FileSearchOptions options,out string summary){
            var values=new List<string>();summary="";
            foreach(var rule in options.MetadataConditions??new MetadataSearchCondition[0]){
                var field=Field(rule.Field);if(field==null)return false;
                string value=Value(data,rule.Field);if(String.IsNullOrWhiteSpace(value))return false;
                object actual,expected;try{actual=Parse(value,field.Kind);expected=Parse(rule.Value,field.Kind);}catch{return false;}
                bool matches;
                if(field.Kind=="text"){
                    var comparison=options.CaseSensitive?StringComparison.CurrentCulture:StringComparison.CurrentCultureIgnoreCase;
                    matches=rule.Operator=="contains"?value.IndexOf((string)expected,comparison)>=0:rule.Operator=="starts"?value.StartsWith((string)expected,comparison):String.Equals(value,(string)expected,comparison);
                }else{
                    int cmp=((IComparable)actual).CompareTo(expected);
                    matches=rule.Operator=="gt"?cmp>0:rule.Operator=="ge"?cmp>=0:rule.Operator=="lt"?cmp<0:rule.Operator=="le"?cmp<=0:cmp==0;
                }
                if(!matches)return false;
                values.Add(field+": "+value);
            }
            summary=String.Join(" · ",values);return true;
        }
        internal static bool CouldMatch(string path,FileSearchOptions options){
            string ext=Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
            string[] images={"jpg","jpeg","png","bmp","gif","tif","tiff","wdp","webp","heic","avif","svg"};
            string[] media={"mp3","flac","wav","m4a","aac","ogg","wma","opus","mp4","mkv","avi","mov","wmv","webm","mpeg","mpg","m4v","aiff","ape","asf","mp2"};
            string[] documents={"pdf","doc","docx","docm","dotx","dotm","xls","xlsb","xlsx","xlsm","xltx","xltm","ppt","pptx","pptm","ppsx","ppsm","potx","potm","odt","ods","odp","rtf","msg","eml"};
            foreach(var rule in options.MetadataConditions){
                bool possible;
                switch(rule.Field){
                    case "width":case "height":possible=images.Contains(ext)||media.Contains(ext);break;
                    case "megapixels":case "camera":case "taken":possible=images.Contains(ext);break;
                    case "pages":possible=new[]{"pdf","doc","docx","docm","dotx","dotm","odt","rtf"}.Contains(ext);break;
                    case "sheets":possible=new[]{"xls","xlsb","xlsx","xlsm","xltx","xltm","ods"}.Contains(ext);break;
                    case "slides":possible=new[]{"pptx","pptm","ppsx","ppsm","potx","potm"}.Contains(ext);break;
                    case "author":possible=documents.Contains(ext);break;
                    case "duration":case "artist":case "album":case "audioBitrate":possible=media.Contains(ext);break;
                    default:possible=documents.Contains(ext)||media.Contains(ext)||new[]{"exe","dll","sys","ocx","scr"}.Contains(ext);break;
                }if(!possible)return false;
            }return true;
        }
        internal static bool Check(string path,FileSearchOptions options,CancellationToken token,Action<string> warning,out string summary){
            summary="";if(!CouldMatch(path,options))return false;token.ThrowIfCancellationRequested();
            if(options.MetadataInspecting!=null)options.MetadataInspecting(path);
            var data=FileMetadataService.Load(path,token).GetAwaiter().GetResult();token.ThrowIfCancellationRequested();
            if(data.Rows.Any(x=>x.Key=="notice"&&(x.Value==L.Text("details.partial")||x.Value==L.Text("details.passwordProtected"))))warning(L.Text("metaSearch.unreadable")+path);
            return Matches(data,options,out summary);
        }
    }
    internal sealed class MetadataSearchEditor : StackPanel {
        readonly StackPanel rules=new StackPanel();
        readonly List<Func<MetadataSearchCondition>> readers=new List<Func<MetadataSearchCondition>>();
        internal MetadataSearchEditor(bool enabled,Action<string> mask){
            Margin=new Thickness(8);
            Children.Add(new TextBlock{Text=L.Text(enabled?"metaSearch.help":"metaSearch.localOnly"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,8)});
            var presets=new ComboBox{ItemsSource=new[]{L.Text("metaSearch.presets"),L.Text("metaSearch.photoPreset"),L.Text("metaSearch.pdfPreset"),L.Text("metaSearch.authorPreset"),L.Text("metaSearch.videoPreset")},SelectedIndex=0,Margin=new Thickness(0,0,0,8),IsEnabled=enabled};
            Children.Add(presets);
            presets.SelectionChanged+=delegate{
                int choice=presets.SelectedIndex;if(choice<1)return;
                var rule=choice==1?new MetadataSearchCondition{Field="width",Operator="gt",Value="4000"}:choice==2?new MetadataSearchCondition{Field="pages",Operator="gt",Value="20"}:choice==3?new MetadataSearchCondition{Field="author",Operator="contains",Value=""}:new MetadataSearchCondition{Field="duration",Operator="gt",Value="01:00:00"};
                Restore(new[]{rule});mask(choice==1?"*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.webp;*.bmp;*.gif;*.heic;*.avif":choice==2?"*.pdf":choice==3?"*.pdf;*.doc;*.docx;*.docm;*.odt;*.xlsx;*.pptx":"*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.webm;*.mpeg;*.mpg;*.m4v");presets.SelectedIndex=0;
            };
            var section=FluentTheme.Section(this,L.Text("metaSearch.conditions"));section.Children.Add(rules);section.IsEnabled=enabled;
            var actions=new WrapPanel();section.Children.Add(actions);
            actions.Children.Add(CopyUi.Button(L.Text("metaSearch.add"),delegate{if(readers.Count<20)Add(new MetadataSearchCondition());}));
            actions.Children.Add(CopyUi.Button(L.Text("metaSearch.clear"),delegate{Restore(null);}));
        }
        internal MetadataSearchCondition[] Read(){return readers.Select(x=>x()).ToArray();}
        internal void Restore(IEnumerable<MetadataSearchCondition> items){readers.Clear();rules.Children.Clear();foreach(var item in (items??new MetadataSearchCondition[0]).Take(20))Add(item);}
        void Add(MetadataSearchCondition initial){
            var row=new Grid{Margin=new Thickness(0,0,0,8)};rules.Children.Add(row);
            foreach(double width in new[]{2.0,1.2,1.5})row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var field=new ComboBox{ItemsSource=MetadataSearch.Fields,SelectedItem=MetadataSearch.Field(initial.Field)??MetadataSearch.Fields[0],MinWidth=130,Margin=new Thickness(0,0,6,0)};
            var op=new ComboBox{MinWidth=85,Margin=new Thickness(0,0,6,0)};
            var value=new TextBox{Text=initial.Value??"",MinWidth=85};
            Action operators=delegate{var f=(MetadataSearchField)field.SelectedItem;op.ItemsSource=MetadataSearch.Operators(f.Kind).Select(x=>new Operation(x)).ToArray();op.SelectedIndex=0;value.ToolTip=f.ToString();};
            operators();op.SelectedItem=op.Items.Cast<Operation>().FirstOrDefault(x=>x.Key==initial.Operator)??op.Items[0];field.SelectionChanged+=delegate{operators();};
            row.Children.Add(field);Grid.SetColumn(op,1);row.Children.Add(op);Grid.SetColumn(value,2);row.Children.Add(value);
            Func<MetadataSearchCondition> read=()=>new MetadataSearchCondition{Field=((MetadataSearchField)field.SelectedItem).Key,Operator=((Operation)op.SelectedItem).Key,Value=value.Text};
            readers.Add(read);var remove=CopyUi.Button("×",delegate{readers.Remove(read);rules.Children.Remove(row);});remove.MinWidth=28;remove.Width=28;remove.Padding=new Thickness(0);remove.FontSize=16;remove.ToolTip=L.Text("action.remove");Grid.SetColumn(remove,3);row.Children.Add(remove);
        }
        sealed class Operation {
            internal string Key;internal Operation(string key){Key=key;}
            public override string ToString(){return L.Text("metaSearch.op."+Key);}
        }
    }
}