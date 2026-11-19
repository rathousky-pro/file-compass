using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal sealed class DuplicateWindow:Window {
  readonly TextBox roots=new TextBox();
  readonly TextBox excluded=new TextBox(), extensions=new TextBox(), minSize=new TextBox{Width=75}, maxSize=new TextBox{Width=75};
  readonly ComboBox minUnit=new ComboBox{Width=78},maxUnit=new ComboBox{Width=78};
  readonly CheckBox content=Option("duplicates.byContent",true), name=Option("duplicates.byName"), size=Option("duplicates.bySize"), date=Option("duplicates.byDate"), extension=Option("duplicates.byExtension"), recursive=Option("projection.includeChildren",true), hidden=Option("duplicates.hidden"), empty=Option("duplicates.empty",true), ignoreCase=Option("duplicates.ignoreCase",true), ignoreExtension=Option("duplicates.ignoreExtension");
  readonly DatePicker dateFrom=new DatePicker{Width=130},dateTo=new DatePicker{Width=130};
  readonly StackPanel settings=new StackPanel();
  Expander searchOptions;
  readonly ComboBox selection=new ComboBox{Width=155,Margin=new Thickness(4,0,4,0)};
  readonly string initialFolder;
  internal readonly DataGrid Results=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Extended,Background=CompactTheme.WhiteSurface,RowBackground=CompactTheme.WhiteSurface,AlternatingRowBackground=CompactTheme.ThemeBrush("#F4F7FB"),BorderThickness=new Thickness(0)};
  readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,5)};
  readonly QuickPreviewPane preview;
  readonly ColumnDefinition previewColumn;
  readonly Button previewToggle;
  Button backgroundButton;
  bool previewOpen;
  double previewAddedWidth,previewExpandedWidth;
  List<DuplicateFile> files=new List<DuplicateFile>();
  List<string> skipped=new List<string>();
  CancellationTokenSource cancellation;
  internal Task ScanTask;
  bool closed;
  static CheckBox Option(string key,bool value=false){return new CheckBox{Content=L.Text(key),IsChecked=value,Margin=new Thickness(0,3,14,3),VerticalAlignment=VerticalAlignment.Center};}
  internal DuplicateWindow(Window owner,string initial,string other=null) {
   initialFolder=initial??"";
   CopyUi.Style(this,owner,L.Text("duplicates.title"),1450);DialogGeometry.Bound(this,1450,860,900,540);ResizeMode=ResizeMode.CanResize;
   var body=new DockPanel{Margin=new Thickness(12)};Content=body;
   var top=new StackPanel();
   var section=FluentTheme.Section(top,L.Text("duplicates.locations"));
   searchOptions=new Expander{Header=L.Text("duplicates.options"),IsExpanded=true,Content=settings};section.Children.Add(searchOptions);
   searchOptions.Expanded+=delegate{settings.Visibility=Visibility.Visible;};searchOptions.Collapsed+=delegate{settings.Visibility=Visibility.Collapsed;};
   var row=new DockPanel{Margin=new Thickness(0,8,0,0)};settings.Children.Add(row);roots.Text=initial??"";
   var browse=CopyUi.Button(L.Text("common.ellipsis"),delegate{if(cancellation!=null)return;using(var dialog=new System.Windows.Forms.FolderBrowserDialog())if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)roots.Text=String.IsNullOrWhiteSpace(roots.Text)?dialog.SelectedPath:roots.Text+"; "+dialog.SelectedPath;});DockPanel.SetDock(browse,Dock.Right);row.Children.Add(browse);row.Children.Add(roots);
   var exrow=new DockPanel{Margin=new Thickness(0,4,0,0)};settings.Children.Add(exrow);var exbutton=CopyUi.Button(L.Text("duplicates.excludeFolder"),delegate{using(var d=new System.Windows.Forms.FolderBrowserDialog())if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)excluded.Text=Append(excluded.Text,d.SelectedPath);});DockPanel.SetDock(exbutton,Dock.Left);exrow.Children.Add(exbutton);exrow.Children.Add(excluded);
   settings.Children.Add(new TextBlock{Text=L.Text("duplicates.criteriaHeading"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,3)});
   var criteria=new WrapPanel{Margin=new Thickness(0,2,0,0)};settings.Children.Add(criteria);foreach(var option in new[]{content,name,size,date,extension})criteria.Children.Add(option);
   settings.Children.Add(new TextBlock{Text=L.Text("duplicates.filtersHeading"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,3)});
   var filterOptions=new WrapPanel();settings.Children.Add(filterOptions);foreach(var option in new[]{recursive,hidden,empty,ignoreCase,ignoreExtension})filterOptions.Children.Add(option);
   var filters=new StackPanel{Margin=new Thickness(0,6,0,0)};settings.Children.Add(filters);
   AddFilterRow(filters,"duplicates.types",extensions,null);
   foreach(var unit in new[]{minUnit,maxUnit}){unit.ItemsSource=new[]{"B","KiB","MiB","GiB","TiB"};unit.SelectedIndex=0;}
   AddFilterRow(filters,"duplicates.minSize",minSize,minUnit);AddFilterRow(filters,"duplicates.maxSize",maxSize,maxUnit);
   AddFilterRow(filters,"duplicates.from",dateFrom,null);AddFilterRow(filters,"duplicates.to",dateTo,null);
   var actionArea=new StackPanel{Margin=new Thickness(0,6,0,0)};DockPanel.SetDock(actionArea,Dock.Bottom);body.Children.Add(actionArea);
   var searchActions=new WrapPanel();actionArea.Children.Add(searchActions);
   var bottomRow=new DockPanel();actionArea.Children.Add(bottomRow);var backgroundHost=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(backgroundHost,Dock.Right);bottomRow.Children.Add(backgroundHost);
   var actions=new WrapPanel();bottomRow.Children.Add(actions);
   var search=CopyUi.Button(L.Text("action.search"),delegate{if(cancellation==null)ScanTask=Scan();});FluentTheme.Primary(search);searchActions.Children.Add(search);
   selection.ItemsSource=new[]{L.Text("duplicates.keepFirst"),L.Text("duplicates.keepOldest"),L.Text("duplicates.keepNewest"),L.Text("duplicates.keepPreferred")};selection.SelectedIndex=0;actions.Children.Add(selection);
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.markCopies"),delegate{if(cancellation!=null)return;if(files.Any(x=>x.Digest==null)){status.Text=L.Text("duplicates.requireContent");return;}foreach(var group in files.GroupBy(x=>x.Group)){var keeper=selection.SelectedIndex==1?group.OrderBy(x=>x.Modified).First():selection.SelectedIndex==2?group.OrderByDescending(x=>x.Modified).First():selection.SelectedIndex==3?group.OrderByDescending(x=>Within(x.Path,initialFolder)).First():group.First();foreach(var file in group)file.Selected=file!=keeper;}Results.Items.Refresh();Summary();}));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.clear"),delegate{if(cancellation!=null)return;foreach(var file in files)file.Selected=false;Results.Items.Refresh();Summary();}));
   actions.Children.Add(CopyUi.Button(L.Text("action.compareFiles"),delegate{var pair=Results.SelectedItems.Cast<DuplicateFile>().ToArray();if(pair.Length==2)new FileDiffWindow(this,pair[0].Path,pair[1].Path).Show();else MessageBox.Show(this,L.Text("duplicates.chooseTwo"),L.Text("duplicates.title"));}));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.recycle"),async delegate{
    if(cancellation!=null)return;Results.CommitEdit();Results.CommitEdit(DataGridEditingUnit.Row,true);
    if(!files.Any(x=>x.Selected))return;
    if(files.Any(x=>x.Selected&&x.Digest==null)){status.Text=L.Text("duplicates.requireContent");return;}
    if(MessageBox.Show(this,L.Text("duplicates.confirmRecycle")+" "+files.Count(x=>x.Selected)+"\n\n"+String.Join("\n",files.Where(x=>x.Selected).Select(x=>x.Path)),L.Text("duplicates.title"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
    preview.ShowMessage("");Results.IsEnabled=false;cancellation=new CancellationTokenSource();var token=cancellation.Token;var remaining=files.ToList();
    try{await Task.Run(()=>DuplicateEngine.Recycle(remaining,token,path=>Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException)));if(!closed){files=remaining;Results.ItemsSource=null;Results.ItemsSource=files;Summary();}}
    catch(Exception ex){if(!closed){files=remaining;Results.ItemsSource=null;Results.ItemsSource=files;status.Text=ex.Message;}}
    finally{cancellation.Dispose();cancellation=null;if(!closed)Results.IsEnabled=true;}
   }));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.export"),delegate{var d=new Microsoft.Win32.SaveFileDialog{Filter="CSV (*.csv)|*.csv",DefaultExt=".csv",FileName="duplicates.csv"};if(d.ShowDialog(this)==true){try{File.WriteAllLines(d.FileName,new[]{"Group,Path,Size,Modified,Match"}.Concat(files.Select(x=>String.Join(",",new[]{x.Group.ToString(),x.Path,x.Bytes.ToString(),x.Modified.ToString("o"),x.Match}.Select(DiskAnalysisReports.Csv)))),new System.Text.UTF8Encoding(true));}catch(Exception ex){status.Text=ex.Message;}}}));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.skipped"),delegate{if(skipped.Count==0)return;var w=new Window{Title=L.Text("duplicates.skipped"),Owner=this,Width=750,Height=450,Content=new TextBox{Text=String.Join("\n",skipped),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};w.Show();}));
   searchActions.Children.Add(CopyUi.Button(L.Text("action.cancel"),delegate{if(cancellation!=null)cancellation.Cancel();}));
   searchActions.Children.Add(CopyUi.Button(L.Text("action.close"),Close));
   previewToggle=CopyUi.Button(L.Text("duplicates.showPreview"),TogglePreview);searchActions.Children.Add(previewToggle);
   ToolDock.Attach(this,backgroundHost,delegate{return new DockStatus{Title=L.Text("duplicates.title"),Icon="≡",Busy=cancellation!=null,Description=status.Text};},null,null,delegate{if(cancellation!=null)cancellation.Cancel();else Close();});
   backgroundButton=backgroundHost.Children.OfType<Button>().FirstOrDefault();if(backgroundButton!=null)backgroundButton.Visibility=Visibility.Collapsed;
   DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
   Results.Columns.Add(new DataGridCheckBoxColumn{Header=L.Text("duplicates.mark"),Binding=new Binding("Selected"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=65});
   foreach(var column in new[]{new[]{"duplicates.group","Group","65"},new[]{"column.name","Name","150"},new[]{"column.path","DisplayPath","280"},new[]{"column.size","Size","100"},new[]{"duplicates.byDate","Date","130"},new[]{"duplicates.match","Match","150"}})Results.Columns.Add(new DataGridTextColumn{Header=L.Text(column[0]),Binding=new Binding(column[1]),IsReadOnly=true,Width=Double.Parse(column[2])});
   var workspace=new Grid();workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(400)});workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});body.Children.Add(workspace);
   workspace.Children.Add(new ScrollViewer{Content=top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,10,0)});
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});previewColumn=new ColumnDefinition{Width=new GridLength(0)};grid.ColumnDefinitions.Add(previewColumn);Grid.SetColumn(grid,1);workspace.Children.Add(grid);grid.Children.Add(new Border{Child=Results,Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(6)});
   var rowStyle=new Style(typeof(DataGridRow));rowStyle.Setters.Add(new Setter(ToolTipProperty,new Binding("Path")));Results.RowStyle=rowStyle;
   preview=new QuickPreviewPane(delegate{},true){Margin=new Thickness(10,0,0,0),Visibility=Visibility.Collapsed};Grid.SetColumn(preview,1);grid.Children.Add(preview);
   Results.SelectionChanged+=delegate{if(previewOpen){var file=Results.SelectedItem as DuplicateFile;if(file!=null)preview.ShowFile(file.Path);else preview.ShowMessage("");}};
   Results.CellEditEnding+=delegate{Dispatcher.BeginInvoke(new Action(Summary));};
   Closed+=delegate{closed=true;if(cancellation!=null)cancellation.Cancel();preview.DisposePreview();};
  }
  void TogglePreview(){
   previewOpen=!previewOpen;
   if(previewOpen&&WindowState==WindowState.Normal){var area=DialogGeometry.WorkArea(this);double before=ActualWidth;double target=Math.Min(area.Width,Math.Min(MaxWidth,before+360));previewAddedWidth=Math.Max(0,target-before);if(previewAddedWidth>0)Width=target;Left=area.Left+(area.Width-target)/2;previewExpandedWidth=target;}
   else if(!previewOpen&&WindowState==WindowState.Normal&&previewAddedWidth>0&&Math.Abs(ActualWidth-previewExpandedWidth)<2){double target=Math.Max(MinWidth,ActualWidth-previewAddedWidth);Width=target;var area=DialogGeometry.WorkArea(this);Left=area.Left+(area.Width-target)/2;previewAddedWidth=0;}
   previewColumn.Width=previewOpen?new GridLength(360):new GridLength(0);preview.Visibility=previewOpen?Visibility.Visible:Visibility.Collapsed;previewToggle.Content=L.Text(previewOpen?"duplicates.hidePreview":"duplicates.showPreview");previewToggle.ToolTip=previewToggle.Content;if(previewOpen){var selected=Results.SelectedItem as DuplicateFile;if(selected!=null)preview.ShowFile(selected.Path);}else preview.ShowMessage("");
  }
  static void AddFilterRow(Panel panel,string label,FrameworkElement field,FrameworkElement suffix){var row=new Grid{Margin=new Thickness(0,3,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(125)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});if(suffix!=null)row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(new TextBlock{Text=L.Text(label),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,6,0)});field.Width=Double.NaN;Grid.SetColumn(field,1);row.Children.Add(field);if(suffix!=null){suffix.Margin=new Thickness(6,0,0,0);Grid.SetColumn(suffix,2);row.Children.Add(suffix);}panel.Children.Add(row);}
  internal static bool SizeValue(string text,int unit,long fallback,out long value){value=fallback;if(String.IsNullOrWhiteSpace(text))return true;decimal number;if(unit<0||unit>4||(!Decimal.TryParse(text,NumberStyles.Number,CultureInfo.CurrentCulture,out number)&&!Decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out number))||number<0)return false;decimal bytes=number;for(int i=0;i<unit;i++){if(bytes>Int64.MaxValue/1024m)return false;bytes*=1024;}if(bytes>Int64.MaxValue||Decimal.Truncate(bytes)!=bytes)return false;value=(long)bytes;return true;}
  static string Append(string text,string path){return String.IsNullOrWhiteSpace(text)?path:text+"; "+path;}
  static bool Within(string path,string folder){if(String.IsNullOrWhiteSpace(folder)||folder.IndexOf(';')>=0)return false;try{return Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
  void Summary(){status.Text=L.Text("duplicates.groups")+": "+files.Select(x=>x.Group).Distinct().Count()+" · "+L.Text("duplicates.files")+": "+files.Count+" · "+L.Text("duplicates.possibleBytes")+": "+CopyUi.Bytes(files.Where(x=>x.Digest!=null).GroupBy(x=>x.Group).Sum(g=>g.Skip(1).Sum(x=>x.Bytes)))+" · "+L.Text("duplicates.selectedBytes")+": "+CopyUi.Bytes(files.Where(x=>x.Selected).Sum(x=>x.Bytes))+(skipped.Count>0?" · "+L.Text("duplicates.skipped")+": "+skipped.Count:"");}
  internal async Task Scan() {
   var paths=SearchAdvanced.Roots(roots.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(paths.Length==0||paths.Any(x=>!Directory.Exists(x)&&!File.Exists(x))){status.Text=L.Text("search.rootPathMissing");return;}
   long minimum,maximum;if(!SizeValue(minSize.Text,minUnit.SelectedIndex,0,out minimum)||!SizeValue(maxSize.Text,maxUnit.SelectedIndex,Int64.MaxValue,out maximum)||maximum<minimum){status.Text=L.Text("duplicates.invalidSize");return;}
   var options=new DuplicateOptions{Content=content.IsChecked==true,Name=name.IsChecked==true,Size=size.IsChecked==true,Date=date.IsChecked==true,Extension=extension.IsChecked==true,Recursive=recursive.IsChecked==true,IncludeHidden=hidden.IsChecked==true,IncludeEmpty=empty.IsChecked==true,IgnoreCase=ignoreCase.IsChecked==true,IgnoreExtension=ignoreExtension.IsChecked==true,Minimum=minimum,Maximum=maximum,From=dateFrom.SelectedDate,To=dateTo.SelectedDate?.AddDays(1).AddTicks(-1),Extensions=extensions.Text.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim().TrimStart('*','.')).ToArray(),Excluded=SearchAdvanced.Roots(excluded.Text)};
   if(!options.Content&&!options.Name&&!options.Size&&!options.Date&&!options.Extension){status.Text=L.Text("duplicates.selectCriterion");return;}
   if(options.From.HasValue&&options.To.HasValue&&options.From>options.To){status.Text=L.Text("duplicates.invalidDate");return;}
   cancellation=new CancellationTokenSource();var operation=cancellation;var token=operation.Token;roots.IsEnabled=false;settings.IsEnabled=false;if(backgroundButton!=null)backgroundButton.Visibility=Visibility.Visible;preview.ShowMessage("");var warnings=new List<string>();DateTime update=DateTime.MinValue;
   files=new List<DuplicateFile>();skipped=new List<string>();var live=new ObservableCollection<DuplicateFile>();Results.ItemsSource=live;
   try{
    var found=await Task.Run(()=>DuplicateEngine.Scan(paths,options,token,path=>{if(DateTime.UtcNow-update<TimeSpan.FromMilliseconds(120))return;update=DateTime.UtcNow;Dispatcher.BeginInvoke(new Action(delegate{if(!closed&&Object.ReferenceEquals(cancellation,operation))status.Text=path+" · "+L.Text("duplicates.groups")+": "+(live.Count==0?0:live[live.Count-1].Group);}));},warning=>warnings.Add(warning),group=>Dispatcher.Invoke(new Action(delegate{if(closed||!Object.ReferenceEquals(cancellation,operation))return;foreach(var file in group){file.DisplayPath=file.Path;files.Add(file);live.Add(file);}}))));
    if(closed)return;files=found;skipped=warnings;
    string prefix=files.Count==0?"":Path.GetDirectoryName(files[0].Path)+Path.DirectorySeparatorChar;foreach(var file in files){while(prefix.Length>0&&!file.Path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){prefix=prefix.TrimEnd(Path.DirectorySeparatorChar);int end=prefix.LastIndexOf(Path.DirectorySeparatorChar);prefix=end<0?"":prefix.Substring(0,end+1);}}foreach(var file in files)file.DisplayPath=file.Path.Substring(prefix.Length);
    Results.ItemsSource=files;if(files.Count>0)Results.SelectedIndex=0;Summary();
   }catch(OperationCanceledException){if(!closed){skipped=warnings;status.Text=L.Text("duplicates.cancelledPartial");}}catch(Exception ex){if(!closed)status.Text=ex.Message;}
   finally{cancellation.Dispose();cancellation=null;if(!closed){roots.IsEnabled=true;settings.IsEnabled=true;Results.IsEnabled=true;if(backgroundButton!=null)backgroundButton.Visibility=Visibility.Collapsed;}}
  }
 }
 public partial class MainWindow {
  internal void FindDuplicates(){if(active.IsArchive||active.IsRemote){MessageBox.Show(this,L.Text("duplicates.localOnly"),L.Text("duplicates.title"));return;}var other=active==left?right:left;new DuplicateWindow(this,active.CurrentPath,!other.IsRemote&&!other.IsArchive?other.CurrentPath:null).Show();}
 }
}
