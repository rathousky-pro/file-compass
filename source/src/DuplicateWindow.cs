using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal sealed class DuplicateWindow:Window {
  readonly TextBox roots=new TextBox();
  internal readonly DataGrid Results=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,SelectionMode=DataGridSelectionMode.Extended,Background=CompactTheme.WhiteSurface,RowBackground=CompactTheme.WhiteSurface,AlternatingRowBackground=CompactTheme.ThemeBrush("#F4F7FB"),BorderThickness=new Thickness(0)};
  readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,5)};
  readonly QuickPreviewPane preview;
  List<DuplicateFile> files=new List<DuplicateFile>();
  CancellationTokenSource cancellation;
  internal Task ScanTask;
  bool closed;
  internal DuplicateWindow(Window owner,string initial) {
   CopyUi.Style(this,owner,L.Text("duplicates.title"),1220);Height=750;MinWidth=900;MinHeight=540;SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanResize;
   var body=new DockPanel{Margin=new Thickness(12)};Content=body;
   var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
   var section=FluentTheme.Section(top,L.Text("duplicates.title"));section.Children.Add(new TextBlock{Text=L.Text("duplicates.help"),TextWrapping=TextWrapping.Wrap});
   var row=new DockPanel{Margin=new Thickness(0,8,0,0)};section.Children.Add(row);roots.Text=initial??"";
   var browse=CopyUi.Button(L.Text("common.ellipsis"),delegate{if(cancellation!=null)return;using(var dialog=new System.Windows.Forms.FolderBrowserDialog())if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)roots.Text=String.IsNullOrWhiteSpace(roots.Text)?dialog.SelectedPath:roots.Text+"; "+dialog.SelectedPath;});DockPanel.SetDock(browse,Dock.Right);row.Children.Add(browse);row.Children.Add(roots);
   var actions=FluentTheme.Actions();DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);
   var search=CopyUi.Button(L.Text("action.search"),delegate{if(cancellation==null)ScanTask=Scan();});actions.Children.Add(search);
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.selectCopies"),delegate{if(cancellation!=null)return;DuplicateEngine.SelectCopies(files);Results.Items.Refresh();Summary();}));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.clear"),delegate{if(cancellation!=null)return;foreach(var file in files)file.Selected=false;Results.Items.Refresh();Summary();}));
   actions.Children.Add(CopyUi.Button(L.Text("action.compareFiles"),delegate{var pair=Results.SelectedItems.Cast<DuplicateFile>().ToArray();if(pair.Length==2)new FileDiffWindow(this,pair[0].Path,pair[1].Path).Show();else MessageBox.Show(this,L.Text("duplicates.chooseTwo"),L.Text("duplicates.title"));}));
   actions.Children.Add(CopyUi.Button(L.Text("duplicates.recycle"),async delegate{
    if(cancellation!=null)return;Results.CommitEdit();Results.CommitEdit(DataGridEditingUnit.Row,true);
    if(!files.Any(x=>x.Selected))return;
    if(MessageBox.Show(this,L.Text("duplicates.confirmRecycle")+" "+files.Count(x=>x.Selected),L.Text("duplicates.title"),MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
    preview.ShowMessage("");Results.IsEnabled=false;cancellation=new CancellationTokenSource();var token=cancellation.Token;var remaining=files.ToList();
    try{await Task.Run(()=>DuplicateEngine.Recycle(remaining,token,path=>Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException)));if(!closed){files=remaining;Results.ItemsSource=null;Results.ItemsSource=files;Summary();}}
    catch(Exception ex){if(!closed){files=remaining;Results.ItemsSource=null;Results.ItemsSource=files;status.Text=ex.Message;}}
    finally{cancellation.Dispose();cancellation=null;if(!closed)Results.IsEnabled=true;}
   }));
   actions.Children.Add(CopyUi.Button(L.Text("action.cancel"),delegate{if(cancellation!=null)cancellation.Cancel();}));
   actions.Children.Add(CopyUi.Button(L.Text("action.close"),Close));
   DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
   Results.Columns.Add(new DataGridCheckBoxColumn{Header=L.Text("duplicates.mark"),Binding=new Binding("Selected"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=65});
   foreach(var column in new[]{new[]{"duplicates.group","Group","65"},new[]{"column.name","Name","150"},new[]{"column.path","DisplayPath","280"},new[]{"column.size","Size","100"}})Results.Columns.Add(new DataGridTextColumn{Header=L.Text(column[0]),Binding=new Binding(column[1]),IsReadOnly=true,Width=Double.Parse(column[2])});
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});body.Children.Add(grid);grid.Children.Add(new Border{Child=Results,Background=CompactTheme.WhiteSurface,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(6)});
   var rowStyle=new Style(typeof(DataGridRow));rowStyle.Setters.Add(new Setter(ToolTipProperty,new Binding("Path")));Results.RowStyle=rowStyle;
   preview=new QuickPreviewPane(delegate{},true){Margin=new Thickness(10,0,0,0)};Grid.SetColumn(preview,1);grid.Children.Add(preview);
   Results.SelectionChanged+=delegate{var file=Results.SelectedItem as DuplicateFile;if(file!=null)preview.ShowFile(file.Path);else preview.ShowMessage("");};
   Results.CellEditEnding+=delegate{Dispatcher.BeginInvoke(new Action(Summary));};
   Closed+=delegate{closed=true;if(cancellation!=null)cancellation.Cancel();preview.DisposePreview();};
  }
  void Summary(){status.Text=L.Text("duplicates.groups")+": "+files.Select(x=>x.Group).Distinct().Count()+" · "+L.Text("duplicates.files")+": "+files.Count+" · "+L.Text("duplicates.selectedBytes")+": "+CopyUi.Bytes(files.Where(x=>x.Selected).Sum(x=>x.Bytes));}
  internal async Task Scan() {
   var paths=SearchAdvanced.Roots(roots.Text);if(paths.Length==0||paths.Any(x=>!Directory.Exists(x)&&!File.Exists(x))){status.Text=L.Text("search.rootPathMissing");return;}
   cancellation=new CancellationTokenSource();var token=cancellation.Token;roots.IsEnabled=false;Results.IsEnabled=false;preview.ShowMessage("");int warnings=0;string lastWarning="";DateTime update=DateTime.MinValue;
   try{
    var found=await Task.Run(()=>DuplicateEngine.Scan(paths,token,path=>{if(DateTime.UtcNow-update<TimeSpan.FromMilliseconds(120))return;update=DateTime.UtcNow;Dispatcher.BeginInvoke(new Action(delegate{if(!closed&&cancellation!=null)status.Text=L.Text("duplicates.scanning")+" "+path;}));},warning=>{warnings++;lastWarning=warning;}));
    if(closed)return;files=found;
    string prefix=files.Count==0?"":Path.GetDirectoryName(files[0].Path)+Path.DirectorySeparatorChar;foreach(var file in files){while(prefix.Length>0&&!file.Path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){prefix=prefix.TrimEnd(Path.DirectorySeparatorChar);int end=prefix.LastIndexOf(Path.DirectorySeparatorChar);prefix=end<0?"":prefix.Substring(0,end+1);}}foreach(var file in files)file.DisplayPath=file.Path.Substring(prefix.Length);
    Results.ItemsSource=files;if(files.Count>0)Results.SelectedIndex=0;Summary();if(warnings>0)status.Text+=" · "+L.Text("duplicates.skipped")+": "+warnings+" · "+lastWarning;
   }catch(OperationCanceledException){if(!closed)status.Text=L.Text("duplicates.cancelled");}catch(Exception ex){if(!closed)status.Text=ex.Message;}
   finally{cancellation.Dispose();cancellation=null;if(!closed){roots.IsEnabled=true;Results.IsEnabled=true;}}
  }
 }
 public partial class MainWindow {
  internal void FindDuplicates(){if(active.IsArchive||active.IsRemote){MessageBox.Show(this,L.Text("duplicates.localOnly"),L.Text("duplicates.title"));return;}new DuplicateWindow(this,active.CurrentPath).Show();}
 }
}
