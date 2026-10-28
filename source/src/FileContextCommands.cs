using System;using System.Collections.Generic;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Input;
namespace FileCompass {
 public partial class FilePanel {
  bool contextPending;Entry contextPressedEntry;int contextPressTimestamp;
  internal static bool IsContextHold(TimeSpan elapsed,bool moved){return !moved && elapsed>=TimeSpan.FromMilliseconds(600);}
  internal bool CanShowWindowsMenu {get{return !IsRemote&&!IsArchive&&!IsVirtualBrowser && ShellFileMenu.Compatible(Selected().Where(x=>!x.IsParent).Select(x=>x.FullPath).ToArray());}}
  internal void SelectContextEntry(Entry entry){if(entry==null)return;activate(this);if(windowsSelection){if(!List.SelectedItems.Contains(entry))List.SelectedItem=entry;}else{if(!entry.IsMarked&&List.Items.Cast<Entry>().Any(x=>x.IsMarked))MarkAll(false);List.SelectedItem=entry;}}
  internal void FocusRightEntry(Entry entry){if(entry==null)return;bool previous=synchronizingSelection;synchronizingSelection=true;try{List.SelectedItem=entry;}finally{synchronizingSelection=previous;}SyncWindowsSelection();}
  internal void BeginContextEntry(Entry entry,Point point){contextPressedEntry=entry;activate(this);FocusRightEntry(entry);contextPressTimestamp=Environment.TickCount;rightStart=point;rightMoved=false;contextPending=true;StartRightHoldIndicator(point);}
  void BeginFileContextGesture(MouseButtonEventArgs e){var row=RowAt(e.GetPosition(List));if(row==null)return;BeginContextEntry((Entry)row.Content,e.GetPosition(List));contextPressTimestamp=e.Timestamp;bool previous=synchronizingSelection;synchronizingSelection=true;try{row.Focus();}finally{synchronizingSelection=previous;}SyncWindowsSelection();Mouse.Capture(List,CaptureMode.SubTree);e.Handled=true;}
  void ContinueFileContextGesture(MouseEventArgs e){if(!contextPending||e.RightButton!=MouseButtonState.Pressed)return;var delta=e.GetPosition(List)-rightStart;if(!rightMoved&&(Math.Abs(delta.X)>=SystemParameters.MinimumHorizontalDragDistance||Math.Abs(delta.Y)>=SystemParameters.MinimumVerticalDragDistance)){rightMoved=true;HideRightHoldIndicator();BeginRightMark(contextPressedEntry);}if(rightDragging){var row=RowAt(e.GetPosition(List));if(row!=null)ContinueRightMark((Entry)row.Content);}}
  internal void FinishContextEntry(TimeSpan elapsed){if(!contextPending)return;if(!rightMoved&&IsContextHold(elapsed,false)){CompleteProgramContextHold();return;}var entry=contextPressedEntry;bool moved=rightMoved;contextPending=false;HideRightHoldIndicator();EndRightMark();Mouse.Capture(null);if(!moved&&entry!=null)ToggleMark(entry);}
  void EndFileContextGesture(MouseButtonEventArgs e){if(!contextPending){if(holdMenuShown)e.Handled=true;return;}FinishContextEntry(TimeSpan.FromMilliseconds(unchecked((uint)(e.Timestamp-contextPressTimestamp))));e.Handled=true;}
  internal async void ShowFileContextMenu(bool native){if(LocalLoading||IsLoading)return;activate(this);if(native&&CanShowWindowsMenu){try{await ShellMenuWorker.Show(Window.GetWindow(this),Selected().Where(x=>!x.IsParent).Select(x=>x.FullPath).ToArray());if(IsLoaded)Refresh();}catch(OperationCanceledException){}catch(Exception ex){if(IsLoaded)MessageBox.Show(Window.GetWindow(this),ex.Message,L.Text("context.windows"));}return;}if(List.ContextMenu!=null){List.ContextMenu.PlacementTarget=List;List.ContextMenu.IsOpen=true;}}
 }
 public partial class MainWindow {
  internal void AddFileContextCommands(ContextMenu menu,FilePanel panel){
   menu.MinWidth=350;var previous=menu.Items.Cast<object>().ToArray();menu.Items.Clear();var extra=new MenuItem{Header=L.Text("context.more")};foreach(var old in previous){var item=old as MenuItem;if(item!=null&&new[]{L.Text("action.copyToClipboard"),L.Text("action.cut"),L.Text("action.paste"),L.Text("shell.propertiesShortcut")}.Contains(Convert.ToString(item.Header)))continue;extra.Items.Add(old);}
   var commands=new List<Tuple<MenuItem,Func<bool>>>();int index=0;Func<Entry[]> selected=()=>panel.Selected().Where(x=>!x.IsParent).ToArray();Func<bool> any=()=>selected().Length>0;
   Func<bool> file=()=>selected().Length==1&&!selected()[0].IsDirectory;Func<bool> ready=()=>!busy&&!PanelsLoading&&fileToolsRunning==0;
   Action<string,string,string,Action,Func<bool>> add=(key,shortcut,icon,run,allowed)=>{var item=new MenuItem{Header=L.Text(key),InputGestureText=shortcut,Tag=key};if(icon!=null)item.Icon=DesktopIcons.Toolbar(icon,16);item.Click+=delegate{SetActive(panel);if(ready()&&allowed())run();};menu.Items.Insert(index++,item);commands.Add(Tuple.Create(item,allowed));};
   add("context.open","Enter","folder",()=>panel.OpenSelected(),()=>panel.List.SelectedItem is Entry);
   add("context.preview","F3","view",()=>panel.ViewSelected(),file);
   add("action.edit","F4","edit",EditSelected,()=>file()&&!panel.IsExt);add("codeEditor.external","Shift+F4","edit",EditExternalSelected,()=>file()&&!panel.IsExt);
   Func<bool> convertible=()=>!panel.IsRemote&&!panel.IsArchive&&!panel.IsVirtualBrowser&&any()&&selected().All(x=>!x.IsDirectory&&(MediaCatalog.IsImage(x.FullPath)||MediaCatalog.IsAudio(x.FullPath)));
   add("media.contextConvert","","sync",delegate{var cursor=panel.List.SelectedItem as Entry;var first=cursor!=null&&selected().Contains(cursor)?cursor:selected()[0];OpenMediaConverter(MediaCatalog.IsImage(first.FullPath));},convertible);
   var convert=commands.Last().Item1;
   menu.Items.Insert(index++,new Separator());
   add("action.copy","F5","copy",()=>Transfer(false),any);add("action.move","F6","move",()=>Transfer(true),()=>any()&&!panel.IsArchive&&!panel.IsVirtualBrowser);
   add("action.copyToClipboard","Ctrl+C","copy",()=>CopyClipboard(false),any);add("action.cut","Ctrl+X","cut",()=>CopyClipboard(true),()=>any()&&!panel.IsArchive&&!panel.IsRemote&&!panel.IsExt);add("action.paste","Ctrl+V","paste",PasteClipboard,()=>!panel.IsVirtualBrowser&&!String.IsNullOrEmpty(panel.CurrentPath));
   add("action.rename","Shift+F6","rename",Rename,()=>selected().Length==1&&!panel.IsVirtualBrowser);add("action.delete","F8","delete",DeleteSelected,()=>any()&&!panel.IsVirtualBrowser);
   menu.Items.Insert(index++,new Separator());
   add("action.compress","Alt+F5","pack",CreateArchive,()=>any()&&!panel.IsRemote&&!panel.IsArchive&&!panel.IsVirtualBrowser);
   add("action.extract","Alt+F9","unpack",ExtractArchives,()=>panel.IsArchive||any()&&!panel.IsRemote&&selected().All(x=>!x.IsDirectory&&ArchiveEngine.Recognized(x.FullPath)));
   menu.Items.Insert(index++,new Separator());
   add("shell.propertiesShortcut","",null,ShowProperties,()=>any()&&!panel.IsRemote&&!panel.IsArchive&&!panel.IsExt);menu.Items.Add(extra);
   var system=new MenuItem{Header=L.Text("context.windows"),Tag="context.windows",ToolTip=L.Text("context.windowsHint")};system.Click+=delegate{panel.ShowFileContextMenu(true);};menu.Items.Add(new Separator());menu.Items.Add(system);
   menu.Opened+=delegate{SetActive(panel);foreach(var command in commands)command.Item1.IsEnabled=ready()&&command.Item2();convert.Visibility=convertible()?Visibility.Visible:Visibility.Collapsed;system.Visibility=panel.CanShowWindowsMenu?Visibility.Visible:Visibility.Collapsed;system.IsEnabled=ready();};
  }
 }
}
