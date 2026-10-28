using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FileCompass {
    internal sealed class DiskChartRow {
        internal string TargetPath;internal bool TargetFile;internal Func<DiskLargeFile,bool> Predicate;
        public string Name { get; set; }
        internal HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase); internal string Key;
        public long Bytes { get; set; }
        public long Files { get; set; }
        public double Value { get; set; }
        public string Share { get; set; }
        public string Size { get { return DiskAnalysis.FormatSize(Bytes); } }
        public Brush Color { get; set; }
        public string Marker { get { return "●"; } }
    }
    internal sealed partial class DiskAnalyzerWindow : Window {
        internal readonly ComboBox GroupChoice = new ComboBox { MinWidth=145 }, MetricChoice = new ComboBox { MinWidth=115 }, ChartChoice = new ComboBox { MinWidth=120 };
        internal readonly TreeView Folders = new TreeView { Background=CompactTheme.WhiteSurface, BorderThickness=new Thickness(0) };
        internal readonly DiskChart Chart = new DiskChart();
        readonly TextBlock status = new TextBlock { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,6,0,4) }, scope = new TextBlock { TextTrimming=TextTrimming.CharacterEllipsis, Margin=new Thickness(4,4,4,6), Foreground=FluentTheme.Muted };
        readonly DataGrid largest = Grid(), legend = Grid(); readonly ListBox issues = new ListBox { Background=CompactTheme.WhiteSurface };
        readonly Button refresh, stop; readonly ProgressBar progress = new ProgressBar { Height=3, IsIndeterminate=true, Visibility=Visibility.Collapsed };
        readonly TabControl tabs = new TabControl(); readonly string path; readonly Action<string> navigate; CancellationTokenSource cancellation; bool closed; DiskFolder selected;
        internal DiskAnalysis Result;
        internal Task ScanTask;
        internal DiskAnalyzerWindow(Window owner,string initial,Action<string> openFolder,bool autoStart=false) {
            path=initial; navigate=openFolder; CopyUi.Style(this,owner,L.Text("analyzer.title"),1160);
            SizeToContent=SizeToContent.Manual; ResizeMode=ResizeMode.CanResize;ConfigureFolderRows();ConfigureVirtualFolders();
            var work=SystemParameters.WorkArea; Width=Math.Min(1160,work.Width-32); Height=Math.Min(820,work.Height-32); MinWidth=Math.Min(780,Width); MinHeight=Math.Min(520,Height);
            StackPanel actions;var body=SettingsUx.Frame(this,L.Text("analyzer.title"),out actions);
            var header=new StackPanel{Margin=new Thickness(18,12,18,8)}; DockPanel.SetDock(header,Dock.Top); body.Children.Add(header);
            header.Children.Add(new TextBlock { Text=L.Text("analyzer.title"),Visibility=Visibility.Collapsed });

            header.Children.Add(new TextBlock { Text=L.Text("analyzer.help"), Foreground=FluentTheme.Muted, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,5,0,8) });
            var footer=(Border)actions.Parent;footer.Child=null;var footerBody=new StackPanel();footerBody.Children.Add(progress);footerBody.Children.Add(status);footerBody.Children.Add(actions);footer.Child=footerBody;
            refresh=CopyUi.Button(L.Text("analyzer.start"),delegate { if(cancellation==null) ScanTask=Scan(); });refresh.IsDefault=true;CopyUi.EnterDefault(this,refresh);FluentTheme.Primary(refresh);actions.Children.Add(refresh);
            stop=CopyUi.Button(L.Text("analyzer.stop"),delegate { if(cancellation!=null){cancellation.Cancel();status.Text=L.Text("transfer.cancellingStatus");} }); stop.IsEnabled=false; actions.Children.Add(stop);
            actions.Children.Add(CopyUi.Button(L.Text("analyzer.openPanel"),delegate { string target=SelectedPath(); if(navigate!=null&&target!=null)navigate(target); }));
            actions.Children.Add(CopyUi.Button(L.Text("action.close"),Close));
            var split=new System.Windows.Controls.Grid{Margin=new Thickness(18,0,18,14)}; body.Children.Add(split); split.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1.15,GridUnitType.Star) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(8) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            split.Children.Add(new Border{Child=tabs,Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(8)});
            tabs.Items.Add(new TabItem { Header=L.Text("analyzer.folders"), Content=Folders });
            tabs.Items.Add(new TabItem { Header=L.Text("analyzer.largest"), Content=largest });
            tabs.Items.Add(new TabItem { Header=L.Text("analyzer.skipped"), Content=issues });
            AddColumn(largest,L.Text("analyzer.path"),"RelativePath",1); AddColumn(largest,L.Text("analyzer.bytes"),"Size",.35); AddColumn(largest,L.Text("analyzer.modified"),"Modified",.5);
            largest.MouseDoubleClick+=delegate { var item=largest.SelectedItem as DiskLargeFile; if(item!=null&&navigate!=null)navigate(System.IO.Path.GetDirectoryName(item.Path)); };
            Folders.SelectedItemChanged+=delegate { if(rebuildingTree)return;var item=Folders.SelectedItem as TreeViewItem; selected=Folders.SelectedItem as DiskFolder??(item==null?null:item.Tag as DiskFolder); if(GroupChoice.SelectedIndex==0)UpdateChart(); };
            var right=new DockPanel(); var chartFrame=new Border{Child=right,Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(10)};System.Windows.Controls.Grid.SetColumn(chartFrame,2);split.Children.Add(chartFrame);
            var controls=new WrapPanel(); DockPanel.SetDock(controls,Dock.Top);right.Children.Add(controls);
            foreach(string key in new[]{"analyzer.byFolder","analyzer.byExtension","analyzer.byAge","analyzer.bySize"})GroupChoice.Items.Add(L.Text(key));
            MetricChoice.Items.Add(L.Text("analyzer.bytes")); MetricChoice.Items.Add(L.Text("analyzer.files"));
            ChartChoice.Items.Add(L.Text("analyzer.pie")); ChartChoice.Items.Add(L.Text("analyzer.bars"));ChartChoice.Items.Add(L.Text("analyzer.treemap"));
            foreach(var choice in new[]{GroupChoice,MetricChoice,ChartChoice}) { choice.Margin=new Thickness(0,0,6,4);controls.Children.Add(choice);choice.SelectionChanged+=delegate { UpdateChart(); }; }
            GroupChoice.ToolTip=L.Text("analyzer.ageHelp");
            DockPanel.SetDock(scope,Dock.Top);right.Children.Add(scope);
            var chartBody=new System.Windows.Controls.Grid(); chartBody.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });chartBody.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });right.Children.Add(chartBody);
            Chart.MinHeight=100; chartBody.Children.Add(Chart); System.Windows.Controls.Grid.SetRow(legend,1);chartBody.Children.Add(legend);
            var marker=new DataGridTextColumn { Header="",Binding=new Binding("Marker"),Width=26,ElementStyle=new Style(typeof(TextBlock)) };marker.ElementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,new Binding("Color")));legend.Columns.Add(marker);
            AddColumn(legend,L.Text("analyzer.category"),"Name",1);AddColumn(legend,L.Text("analyzer.bytes"),"Size",.55);AddColumn(legend,L.Text("analyzer.files"),"Files",.4);AddColumn(legend,"%","Share",.35); legend.Columns[legend.Columns.Count-1].MinWidth=65;
            GroupChoice.Items.Add(L.Text("analyzer.byCategory")); GroupChoice.SelectedIndex=MetricChoice.SelectedIndex=ChartChoice.SelectedIndex=0; InitializeAnalyzerExtensions(header,actions);InitializeAdvancedAnalyzer(header,actions);
            Loaded+=delegate { if(autoStart)ScanTask=Scan(); }; Closed+=delegate { closed=true;if(cancellation!=null)cancellation.Cancel(); DisposeAnalyzerExtensions(); };
            ToolDock.Attach(this,actions,delegate{return new DockStatus{Title=L.Text("analyzer.title"),Icon="◔",Busy=cancellation!=null,Fraction=cancellation==null&&Result!=null?(double?)1:null,Description=path+" · "+status.Text};});
            FooterActions.Analyzer(actions,refresh,stop);
            InitializeAnalyzerWorkspace(body,header,split,controls,chartBody,actions);
            InitializeAnalyzerStart(autoStart);
        }
        string SelectedPath() { if(tabs.SelectedIndex==3&&MatchingFiles.SelectedItem is DiskLargeFile)return System.IO.Path.GetDirectoryName(((DiskLargeFile)MatchingFiles.SelectedItem).Path);var file=largest.SelectedItem as DiskLargeFile; return tabs.SelectedIndex==1&&file!=null?System.IO.Path.GetDirectoryName(file.Path):selected!=null?(selected.Virtual?selected.Children.FirstOrDefault()?.Path:selected.Path):path; }
        static DataGrid Grid() { return new DataGrid { AutoGenerateColumns=false,CanUserAddRows=false,IsReadOnly=true,SelectionMode=DataGridSelectionMode.Single,Background=CompactTheme.WhiteSurface,RowBackground=CompactTheme.WhiteSurface,AlternatingRowBackground=CompactTheme.ThemeBrush("#F4F7FB"),BorderThickness=new Thickness(0),HeadersVisibility=DataGridHeadersVisibility.Column,EnableRowVirtualization=true }; }
        static void AddColumn(DataGrid grid,string title,string binding,double width) {
            var data=new Binding(binding);if(binding=="Modified")data.StringFormat=L.Language=="en"?"yyyy-MM-dd HH:mm":"dd.MM.yyyy HH:mm";if(binding=="Files")data.StringFormat="N0";
            var column=new DataGridTextColumn{Header=title,Binding=data,SortMemberPath=binding=="Size"?"Bytes":binding=="Share"?"Value":binding,Width=new DataGridLength(width,DataGridLengthUnitType.Star)};
            if(binding=="RelativePath"){var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Path")));column.ElementStyle=style;}grid.Columns.Add(column);
        }
        TreeViewItem FolderItem(DiskFolder folder,int depth=0) {return new TreeViewItem{Header=FolderHeader(folder),Style=folderRowStyle,Tag=folder,ToolTip=folder.Path,ItemsSource=folder.Children.OrderByDescending(x=>x.Bytes).ThenBy(x=>x.Name).ToArray(),ItemTemplate=folderTemplate,ItemContainerStyle=folderRowStyle};}

    }
    public partial class MainWindow {
        internal void AnalyzeFolder() {
            AnalyzeFolder(active);
        }
        internal void AnalyzeFolder(FilePanel panel) {
            if(panel == null || panel.IsArchive||panel.IsRemote||String.IsNullOrEmpty(panel.CurrentPath)||!Directory.Exists(panel.CurrentPath)) { MessageBox.Show(this,L.Text("analyzer.localOnly"),L.Text("analyzer.title"));return; }
            new DiskAnalyzerWindow(this,panel.CurrentPath,target=>panel.Navigate(target,true)).Show();
        }
    }
}
