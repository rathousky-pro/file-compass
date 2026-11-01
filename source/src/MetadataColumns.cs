using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml;
using System.Xml.Serialization;
namespace FileCompass {
    internal static class MetadataColumn {
        internal static readonly string[] Keys = { "dimensions", "megapixels", "taken", "camera", "pages", "sheets", "slides", "author", "title", "duration", "artist", "album", "audioBitrate", "fontFamily", "productVersion", "architecture" };
        internal static string Caption(string key) { return L.Text("details."+key); }
        internal static bool IsSort(string key) { return key != null && key.StartsWith("Meta:") && Keys.Contains(key.Substring(5)); }
        internal static string Value(FileDetails data, string key) {
            var row=data.Rows.FirstOrDefault(x=>x.Key==key);
            if(row==null&&key=="dimensions"){var width=data.Rows.FirstOrDefault(x=>x.Key=="videoWidth");var height=data.Rows.FirstOrDefault(x=>x.Key=="videoHeight");if(width!=null&&height!=null)return width.Value+" × "+height.Value+" px";} if(row==null&&key=="pages")row=data.Rows.FirstOrDefault(x=>x.Key=="cachedPages");
            return row==null ? "" : row.Value;
        }
        internal static object SortValue(string key,string value) {
            if(String.IsNullOrEmpty(value))return null;
            double number;DateTime date;TimeSpan duration;
            if(key=="taken"&&DateTime.TryParse(value,CultureInfo.CurrentCulture,DateTimeStyles.None,out date))return date;
            if(key=="duration"&&TimeSpan.TryParse(value,CultureInfo.CurrentCulture,out duration))return duration;
            if(new[]{"pages","sheets","slides","megapixels","audioBitrate"}.Contains(key)){
                var text=value.Split(' ')[0];
                if(Double.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out number))return number;
            }
            if(key=="dimensions"){
                var match=System.Text.RegularExpressions.Regex.Match(value,@"^(\d+)\s*[×x]\s*(\d+)");
                if(match.Success)return Double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture)*Double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
            }
            return value;
        }
        internal static int Compare(Entry a,Entry b,string key,int direction,bool natural) {
            object av=SortValue(key,a.MetadataValues.ContainsKey(key)?a.MetadataValues[key]:""),bv=SortValue(key,b.MetadataValues.ContainsKey(key)?b.MetadataValues[key]:"");
            if(av==null||bv==null){if(av!=bv)return av==null?1:-1;}
            int result=av==null?0:av.GetType()==bv.GetType()&&av is IComparable?((IComparable)av).CompareTo(bv):StringComparer.CurrentCultureIgnoreCase.Compare(Convert.ToString(av),Convert.ToString(bv));
            return (result==0?FileBehavior.CompareNames(a.FullName,b.FullName,natural):result)*direction;
        }
    }
    public partial class Entry {
        Dictionary<string,string> metadataValues=new Dictionary<string,string>();
        public Dictionary<string,string> MetadataValues {get{return metadataValues;}}
        internal bool ColumnMetadataLoaded;
        internal void SetColumnMetadata(FileDetails data) {
            metadataValues=MetadataColumn.Keys.ToDictionary(x=>x,x=>MetadataColumn.Value(data,x));
            ColumnMetadataLoaded=true;
            if(PropertyChanged!=null)PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("MetadataValues"));
        }
    }
    public sealed class FolderViewRule {
        public string Path;public bool IncludeChildren;public PanelView View=new PanelView();
    }
    public sealed class FolderViewRules {
        public List<FolderViewRule> Rules=new List<FolderViewRule>();
        internal static string FilePath {get{return System.IO.Path.Combine(AppPaths.ConfigDirectory,"folder-views.xml");}}
        internal static FolderViewRules Load() {
            try{using(var r=XmlReader.Create(FilePath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))return (FolderViewRules)new XmlSerializer(typeof(FolderViewRules)).Deserialize(r);}catch{return new FolderViewRules();}
        }
        internal void Save() {
            string temporary=FilePath+".tmp";Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
            try{using(var s=File.Create(temporary))new XmlSerializer(typeof(FolderViewRules)).Serialize(s,this);if(File.Exists(FilePath))File.Replace(temporary,FilePath,null);else File.Move(temporary,FilePath);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal FolderViewRule Match(string path) {
            if(String.IsNullOrEmpty(path))return null;
            string target=path.TrimEnd('\\','/');
            return Rules.Where(x=>!String.IsNullOrEmpty(x.Path)&&(String.Equals(target,x.Path.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)||x.IncludeChildren&&target.StartsWith(x.Path.TrimEnd('\\','/')+"\\",StringComparison.OrdinalIgnoreCase))).OrderByDescending(x=>x.Path.Length).FirstOrDefault();
        }
    }
    public partial class FilePanel {
        CancellationTokenSource columnCancellation=new CancellationTokenSource();
        readonly HashSet<Entry> columnPending=new HashSet<Entry>();
        readonly Dictionary<string,FileDetails> columnCache=new Dictionary<string,FileDetails>();
        bool automaticFolderViews, applyingFolderView, columnScanQueued;
        string lastColumnPath;
        PanelView normalFolderView;
        internal string[] MetadataColumnKeys {get{return view.Columns.Cast<GridViewColumn>().Select(ColumnKey).Where(MetadataColumn.IsSort).Select(x=>x.Substring(5)).ToArray();}}
        internal string ColumnIdentity(int index) {var columns=LogicalColumns.ToArray();return index<0||index>=columns.Length?null:ColumnKey(columns[index]);}
        internal void InitializeMetadataColumns() {
            PathChanged+=delegate {
                columnCancellation.Cancel();columnCancellation.Dispose();columnCancellation=new CancellationTokenSource();columnPending.Clear();
                if(!String.Equals(lastColumnPath,DisplayPath,StringComparison.OrdinalIgnoreCase)){
                    lastColumnPath=DisplayPath;ApplyAutomaticFolderView();
                }
                ScheduleColumnMetadata();
                if(MetadataColumn.IsSort(sortProperty))LoadAllColumnMetadata();
            };
            Unloaded+=delegate {columnCancellation.Cancel();};
            List.ItemContainerGenerator.StatusChanged+=delegate {ScheduleColumnMetadata();};
            List.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate {ScheduleColumnMetadata();}));
        }
        internal void SetMetadataColumns(IEnumerable<string> keys) {
            var wanted=(keys??new string[0]).Where(MetadataColumn.Keys.Contains).Distinct().ToArray();
            if(MetadataColumnKeys.SequenceEqual(wanted))return;
            var previousOrder=view.Columns.Select(ColumnKey).ToArray();
            foreach(var c in view.Columns.Cast<GridViewColumn>().Where(x=>MetadataColumn.IsSort(Convert.ToString(((GridViewColumnHeader)x.Header).Tag))).ToArray())view.Columns.Remove(c);
            foreach(string key in wanted)AddColumn(MetadataColumn.Caption(key),"Meta:"+key,150);
            if(List.ActualWidth>100) {
                resizingColumns=true;
                try {
                    double available=Math.Max(480,List.ActualWidth-24);
                    double extra=Math.Min(wanted.Length*125,Math.Max(125,available-420));
                    double[] minimum=AppearanceTheme.IsClean?new double[]{140,BaseColumnVisible(1)?80:0,BaseColumnVisible(2)?70:0,BaseColumnVisible(3)?110:0,BaseColumnVisible(4)?90:0}:new double[]{140,BaseColumnVisible(1)?50:0,BaseColumnVisible(2)?70:0,BaseColumnVisible(3)?110:0,BaseColumnVisible(4)?55:0};double total=minimum.Sum();
                    for(int i=0;i<5;i++)BaseColumn(i).Width=minimum[i]*Math.Max(1,(available-extra)/total);
                    foreach(var column in view.Columns.Where(x=>MetadataColumn.IsSort(ColumnKey(x))))column.Width=Math.Max(85,extra/Math.Max(1,wanted.Length));
                    if(wanted.Length==0){preserveColumnWidths=false;ResizeColumns();}
                }finally{resizingColumns=false;}
            }
            if(MetadataColumn.IsSort(sortProperty)&&!wanted.Contains(sortProperty.Substring(5)))sortProperty="Name";
            ApplyColumnOrder(previousOrder);FitColumns();ScheduleColumnMetadata();
        }
        ContextMenu MetadataColumnMenu(string selectedColumn) {
            var menu=new ContextMenu();
            menu.Opened+=delegate {
                menu.Items.Clear();
                menu.Items.Add(new MenuItem{Header=L.Text("columns.choose"),IsEnabled=false});
                foreach(int index in new[]{0,1,2,3,4}){
                    int captured=index;var baseItem=new MenuItem{Header=ColumnCaption(BaseColumnKeys[index]),Tag=BaseColumnKeys[index],IsCheckable=true,IsChecked=BaseColumn(index).Width>0,IsEnabled=index!=0&&(BaseColumn(index).Width>0||CanShowColumn(BaseColumnKeys[index]))};
                    baseItem.Click+=delegate{SetBaseColumnVisible(captured,baseItem.IsChecked);};menu.Items.Add(baseItem);
                }
                menu.Items.Add(new Separator());
                foreach(string key in MetadataColumn.Keys){
                    string captured=key;var item=new MenuItem{Header=MetadataColumn.Caption(key),IsCheckable=true,IsChecked=view.Columns.Any(x=>ColumnKey(x)=="Meta:"+key&&x.Width>0),IsEnabled=view.Columns.Any(x=>ColumnKey(x)=="Meta:"+key&&x.Width>0)||CanShowColumn("Meta:"+key)};
                    item.Click+=delegate {var keys=MetadataColumnKeys.ToList();if(item.IsChecked)keys.Add(captured);else keys.Remove(captured);SetMetadataColumns(keys);FitColumns();Sort();};
                    menu.Items.Add(item);
                }
                menu.Items.Add(new Separator());
                var visible=view.Columns.Where(x=>x.Width>0).Select(ColumnKey).ToList();int position=visible.IndexOf(selectedColumn);
                var moveLeft=new MenuItem{Header=L.Text("columns.moveLeft"),IsEnabled=position>0};moveLeft.Click+=delegate{MoveVisibleColumn(selectedColumn,-1);};menu.Items.Add(moveLeft);
                var moveRight=new MenuItem{Header=L.Text("columns.moveRight"),IsEnabled=position>=0&&position<visible.Count-1};moveRight.Click+=delegate{MoveVisibleColumn(selectedColumn,1);};menu.Items.Add(moveRight);
                var reset=new MenuItem{Header=L.Text("columns.reset")};reset.Click+=delegate {extensionColumn=null;attributesColumn=null;sizeColumn=null;modifiedColumn=null;ApplyBaseColumns();SetMetadataColumns(new string[0]);ApplyColumnOrder(BaseColumnKeys);preserveColumnWidths=false;ResizeColumns();Sort();};menu.Items.Add(reset);
                menu.Items.Add(new Separator());
                AddFolderRuleItem(menu,false);AddFolderRuleItem(menu,true);
                var remove=new MenuItem{Header=L.Text("columns.removeRule"),IsEnabled=CanUseFolderView};
                remove.Click+=delegate {try{var store=FolderViewRules.Load();store.Rules.RemoveAll(x=>String.Equals(x.Path,CurrentPath,StringComparison.OrdinalIgnoreCase));store.Save();if(automaticFolderViews){lastColumnPath=null;ApplyAutomaticFolderView();}}catch(Exception ex){MessageBox.Show(Window.GetWindow(this),ex.Message);}};
                menu.Items.Add(remove);
            };
            return menu;
        }
        bool CanUseFolderView {get{return !IsRemote&&!IsArchive&&!IsShellBrowser&&!IsNetworkBrowser&&projection==null&&!String.IsNullOrEmpty(CurrentPath);}}
        void AddFolderRuleItem(ContextMenu menu,bool children) {
            var item=new MenuItem{Header=L.Text(children?"columns.saveChildren":"columns.saveFolder"),IsEnabled=CanUseFolderView};
            item.Click+=delegate {
                try{
                    var state=new PanelView();SavePanelView(state);
                    var store=FolderViewRules.Load();store.Rules.RemoveAll(x=>String.Equals(x.Path,CurrentPath,StringComparison.OrdinalIgnoreCase));
                    store.Rules.Add(new FolderViewRule{Path=CurrentPath,IncludeChildren=children,View=state});store.Save();
                }catch(Exception ex){MessageBox.Show(Window.GetWindow(this),ex.Message);}
            };menu.Items.Add(item);
        }
        internal void SetAutomaticFolderViews(bool enabled) {
            if(automaticFolderViews==enabled)return;
            automaticFolderViews=enabled;
            if(!enabled&&normalFolderView!=null){applyingFolderView=true;try{RestorePanelView(normalFolderView);}finally{applyingFolderView=false;normalFolderView=null;}}
            if(enabled)ApplyAutomaticFolderView();
        }
        internal static string DetectFolderKind(IEnumerable<Entry> entries) {
            var files=entries.Where(x=>!x.IsDirectory&&!x.IsParent&&!x.IsHidden&&!x.IsSystem).ToArray();
            if(files.Length==0)return "";
            foreach(string kind in new[]{"images","documents","audio","video"}){
                int count=files.Count(x=>FolderKind(x.Extension)==kind);
                if(count>0&&count*100>=files.Length*60)return kind;
            }return "";
        }
        static string FolderKind(string extension) {
            string e=extension.ToLowerInvariant().TrimStart('.');
            if(new[]{"jpg","jpeg","png","gif","bmp","tif","tiff","webp","heic","avif","ico","cur"}.Contains(e))return "images";
            if(new[]{"pdf","doc","docx","docm","xls","xlsx","xlsm","ppt","pptx","odt","ods","odp","rtf"}.Contains(e))return "documents";
            if(new[]{"mp3","flac","wav","m4a","aac","ogg","wma","opus"}.Contains(e))return "audio";
            if(new[]{"mp4","mkv","avi","mov","wmv","webm","mpeg","mpg","m4v"}.Contains(e))return "video";
            return "";
        }
        internal void ApplyAutomaticFolderView() {
            if(!automaticFolderViews||applyingFolderView||List.ItemsSource==null)return;
            applyingFolderView=true;
            try {
                if(normalFolderView!=null)RestorePanelView(normalFolderView);
                if(!CanUseFolderView)return;
                if(normalFolderView==null){normalFolderView=new PanelView();SavePanelView(normalFolderView);}
                var rule=FolderViewRules.Load().Match(CurrentPath);
                if(rule!=null){RestorePanelView(rule.View);return;}
                string kind=DetectFolderKind(List.ItemsSource.Cast<Entry>());
                if(kind=="images"){SetMetadataColumns(new[]{"dimensions","taken","camera"});SetDisplay("Thumbnails");}
                else if(kind=="documents"){SetMetadataColumns(new[]{"author","pages","sheets"});SetDisplay("Details");}
                else if(kind=="audio"){SetMetadataColumns(new[]{"artist","album","title","duration"});SetDisplay("Details");}
                else if(kind=="video"){SetMetadataColumns(new[]{"duration","dimensions"});SetDisplay("Details");}
            }finally{applyingFolderView=false;}
        }
        void ScheduleColumnMetadata() {
            if(columnScanQueued||MetadataColumnKeys.Length==0)return;
            columnScanQueued=true;
            Dispatcher.BeginInvoke(new Action(delegate {
                columnScanQueued=false;
                if(!List.IsVisible||columnCancellation.IsCancellationRequested||!CanUseFolderView)return;
                foreach(var row in RealizedRows(List).Take(100)){
                    var entry=row.Content as Entry;if(entry!=null){var pending=RequestColumnMetadata(entry);}
                }
            }),System.Windows.Threading.DispatcherPriority.Background);
        }
        internal Task LoadAllColumnMetadata() {
            if(!CanUseFolderView||List.ItemsSource==null)return Task.FromResult(0);
            var entries=List.ItemsSource.Cast<Entry>().Where(x=>!x.IsDirectory&&!x.IsLink).ToArray();
            var token=columnCancellation.Token;
            return LoadColumnBatch(entries,token);
        }
        async Task LoadColumnBatch(Entry[] entries,CancellationToken token) {
            try{
                // A bounded producer avoids one task/process per file queued in memory.
                foreach(var entry in entries){token.ThrowIfCancellationRequested();while(columnPending.Contains(entry)||columnPending.Count>=100)await Task.Delay(50,token);await RequestColumnMetadata(entry); }
                if(!token.IsCancellationRequested)Sort();
            }catch(OperationCanceledException){}
        }
        internal async Task RequestColumnMetadata(Entry entry) {
            if(entry.IsDirectory||entry.IsLink||entry.ColumnMetadataLoaded||columnPending.Contains(entry)||columnCancellation.IsCancellationRequested)return;
            var token=columnCancellation.Token;
            if(columnPending.Count>=100)return;
            columnPending.Add(entry);
            try {
                string key=entry.FullPath+"|"+entry.Bytes+"|"+entry.ModifiedTime.Ticks;
                FileDetails data;
                if(!columnCache.TryGetValue(key,out data)){
                    data=await Task.Run(()=>FileMetadataService.Load(entry.FullPath,token),token);
                    if(token.IsCancellationRequested)return;
                    if(columnCache.Count>=512)columnCache.Clear();columnCache[key]=data;
                }
                if(token.IsCancellationRequested)return;
                entry.SetColumnMetadata(data);
                if(MetadataColumn.IsSort(sortProperty))QueueMetadataSort();
            }catch(OperationCanceledException){}catch(Exception){}finally{columnPending.Remove(entry);}
        }
        bool metadataSortQueued;
        void QueueMetadataSort() {
            if(metadataSortQueued)return;metadataSortQueued=true;
            var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
            timer.Tick+=delegate {timer.Stop();metadataSortQueued=false;Sort();};timer.Start();
        }
    }
}