using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal sealed class MediaNamesWindow:Window {
  internal MediaMetadataRow[] Rows;internal Task PreviewTask;internal readonly TextBox Mask=new TextBox();internal readonly CheckBox Overwrite=new CheckBox{Content=L.Text("mediaNames.overwrite")};
  internal readonly Button PreviewButton,WriteButton;readonly ListView list=new ListView();readonly TextBlock status=CopyUi.Label("");bool running,closed;CancellationTokenSource cancellation;int generation;
  internal MediaNamesWindow(Window owner,string[] paths,Action refresh,Action<bool> activity,Action<MediaMetadataRow> logged=null){
   CopyUi.Style(this,owner,L.Text("mediaNames.write"),1440);Height=760;MinHeight=470;MinWidth=760;SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanResize;
   var body=new DockPanel();CopyUi.SectionedSurface(this,body);DialogGeometry.Bound(this,1440,760,900,470);var controls=new StackPanel();DockPanel.SetDock(controls,Dock.Top);body.Children.Add(controls);
   controls.Children.Add(CopyUi.Label(L.Text("mediaNames.help")));
   var preset=new ComboBox{SelectedIndex=paths.All(MediaNames.IsImage)?4:1,ItemsSource=new[]{"[MP3.Artist] - [MP3.Title]","[MP3.Artist] - [MP3.Track] - [MP3.Title]","[MP3.Track] - [MP3.Title]","[MP3.Artist] - [MP3.Album] - [MP3.Year] - [MP3.Track] - [MP3.Title]","[EXIF.Date]_[EXIF.Time] - [Image.Title]","[Image.Author] - [Image.Title]","[Image.Title]"},Margin=new Thickness(2,4,2,4)};controls.Children.Add(preset);
   controls.Children.Add(Mask);TokenDropdown.Attach(Mask,MediaNames.WritableTokens.Concat(new[]{"[Ignore]"}));Mask.Text=paths.All(MediaNames.IsImage)?"[EXIF.Date]_[EXIF.Time] - [Image.Title]":"[MP3.Artist] - [MP3.Track] - [MP3.Title]";preset.SelectionChanged+=delegate{if(preset.SelectedItem!=null)Mask.Text=(string)preset.SelectedItem;};
   controls.Children.Add(Overwrite);
   var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);body.Children.Add(buttons);DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
   var columns=new GridView();foreach(var column in new[]{new[]{"column.name","Name","230"},new[]{"mediaNames.before","OldValue","240"},new[]{"mediaNames.after","NewValue","240"},new[]{"common.status","Status","180"},new[]{"mediaNames.backup","Backup","260"}}){var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(column[1]));text.SetBinding(FrameworkElement.ToolTipProperty,new Binding(column[1]=="Name"?"Path":column[1]));text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);columns.Columns.Add(new GridViewColumn{Header=L.Text(column[0]),Width=Int32.Parse(column[2]),CellTemplate=new DataTemplate{VisualTree=text}});}list.View=columns;body.Children.Add(list);list.SizeChanged+=delegate{if(list.ActualWidth<400)return;double space=list.ActualWidth-36;columns.Columns[0].Width=space*.24;columns.Columns[1].Width=space*.25;columns.Columns[2].Width=space*.25;columns.Columns[3].Width=space*.16;columns.Columns[4].Width=space*.10;};
   PreviewButton=CopyUi.Button(L.Text("rename.refreshPreview"),delegate{PreviewTask=Preview(paths,controls,activity);});buttons.Children.Add(PreviewButton);
   WriteButton=CopyUi.Button(L.Text("mediaNames.apply"),async delegate{
    var pending=Rows==null?new MediaMetadataRow[0]:Rows.Where(x=>x.Changes!=null&&x.Changes.Count>0).ToArray();if(running||pending.Length==0)return;
    if(MessageBox.Show(this,L.Format("mediaNames.confirm",pending.Length),L.Text("mediaNames.write"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    running=true;activity(true);controls.IsEnabled=false;PreviewButton.IsEnabled=WriteButton.IsEnabled=false;cancellation=new CancellationTokenSource();
    try{foreach(var row in pending){cancellation.Token.ThrowIfCancellationRequested();status.Text=row.Path;try{await Task.Run(()=>MediaNames.Apply(row,cancellation.Token));}catch(OperationCanceledException){throw;}catch(Exception ex){row.Status=ex.Message;row.Changes=null;}if(logged!=null)logged(row);list.Items.Refresh();}status.Text=L.Text("mediaNames.finished");}
    catch(OperationCanceledException){status.Text=L.Text("mediaNames.cancelled");}finally{refresh();running=false;activity(false);controls.IsEnabled=true;PreviewButton.IsEnabled=true;Rows=null;cancellation.Dispose();cancellation=null;}
   });WriteButton.IsEnabled=false;buttons.Children.Add(WriteButton);buttons.Children.Add(CopyUi.Button(L.Text("action.close"),delegate{if(running)cancellation.Cancel();else Close();}));
   Action invalidate=delegate{generation++;Rows=null;WriteButton.IsEnabled=false;};Mask.TextChanged+=delegate{invalidate();};Overwrite.Checked+=delegate{invalidate();};Overwrite.Unchecked+=delegate{invalidate();};
   Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(running){cancellation.Cancel();e.Cancel=true;}else{closed=true;generation++;}};
   Loaded+=delegate{PreviewTask=Preview(paths,controls,activity);};
  }
  async Task Preview(string[] paths,Panel controls,Action<bool> activity){if(running)return;int version=++generation;string mask=Mask.Text;bool overwrite=Overwrite.IsChecked==true;running=true;cancellation=new CancellationTokenSource();activity(true);controls.IsEnabled=false;PreviewButton.IsEnabled=WriteButton.IsEnabled=false;Rows=null;status.Text=L.Text("rename.preparingPreview");
   try{var rows=await Task.Run(()=>MediaNames.Preview(paths,mask,overwrite,cancellation.Token));if(closed||version!=generation)return;Rows=rows;list.ItemsSource=rows;WriteButton.IsEnabled=rows.Any(x=>x.Changes!=null&&x.Changes.Count>0);status.Text=L.Format("mediaNames.previewCount",rows.Count(x=>x.Changes!=null&&x.Changes.Count>0),rows.Length);}
   catch(OperationCanceledException){status.Text=L.Text("mediaNames.cancelled");}catch(Exception ex){status.Text=ex.Message;}finally{running=false;activity(false);controls.IsEnabled=true;PreviewButton.IsEnabled=true;cancellation.Dispose();cancellation=null;}
  }
 }
 public partial class MainWindow {
  string[] SelectedMediaPaths(){if(active.IsArchive||active.IsRemote||active.IsShellBrowser){MessageBox.Show(this,L.Text("rename.supportedLocationsHelp"),ApplicationBrand.Name);return new string[0];}return active.Selected().Where(x=>!x.IsDirectory).Select(x=>x.FullPath).ToArray();}
  void WriteMetadataFromNames(){var paths=SelectedMediaPaths();if(paths.Length==0)return;new MediaNamesWindow(this,paths,delegate{left.Refresh();right.Refresh();},delegate(bool value){fileToolsRunning+=value?1:-1;},delegate(MediaMetadataRow row){var record=new OperationRecord{Title=L.Text("mediaNames.write"),Ended=DateTime.Now,State=L.Code(row.Applied?"state.completed":"state.error"),Summary=row.Status,Items=new System.Collections.Generic.List<OperationItem>{new OperationItem{Source=row.Path,Target=row.Backup,State=row.Status}}};operationJournal.Records.Insert(0,record);operationJournal.Save();}).Show();}
  void RenameUsingMetadata(){var paths=SelectedMediaPaths();if(paths.Length==0)return;BatchRenameWindow window=null;window=new BatchRenameWindow(this,paths,delegate{left.Refresh();right.Refresh();},delegate(bool value){fileToolsRunning+=value?1:-1;},paths.All(MediaNames.IsImage)?"[EXIF.Date]_[EXIF.Time]_[C]":"[MP3.Artist] - [MP3.Track] - [MP3.Title]",delegate{RecordUndoTransaction(L.Text("mediaNames.rename"),window.LastUndoReceipts);});window.Show();}
 }
}
