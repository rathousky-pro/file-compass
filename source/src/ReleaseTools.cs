using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace FileCompass {
 public sealed class ShortcutSetting { public string Id, Gesture; }
 public sealed class ShortcutSettings { public List<ShortcutSetting> Items = new List<ShortcutSetting>(); }
 internal sealed class ReleaseCommand { internal string Id, Default; internal MenuItem Item; internal readonly List<MenuItem> Mirrors=new List<MenuItem>(); internal Action Run; }
 public partial class MainWindow {
  readonly List<ReleaseCommand> releaseCommands=new List<ReleaseCommand>();
  ShortcutSettings customShortcuts=new ShortcutSettings();
  bool linkedBrowsing; string linkedLeft,linkedRight; long linkedRequest;
  readonly Dictionary<FilePanel,string> linkedTargets=new Dictionary<FilePanel,string>();
  MenuItem linkedMenu;
  void RegisterReleaseCommand(MenuItem item,string title,Action action,string shortcut) {
   if(action==null)return;string id=L.CatalogKey(title)??title;
   var existing=releaseCommands.FirstOrDefault(x=>x.Id==id);if(existing!=null){existing.Mirrors.Add(item);return;}
   releaseCommands.Add(new ReleaseCommand{Id=id,Default=shortcut,Item=item,Run=action});
  }
  void AddReleaseTools(MenuItem edit,MenuItem commands,MenuItem config) {
   AddMenu(commands,L.Text("mediaNames.write"),WriteMetadataFromNames);
   AddMenu(commands,L.Text("mediaNames.rename"),RenameUsingMetadata);
   AddMenu(edit,L.Text("tools1.undo"),ShowSafeUndo,"Ctrl+Z");
   AddMenu(commands,L.Text("tools1.locks"),delegate{FileLocks.Show(this,active.Selected().Select(x=>x.FullPath).ToArray());});
   AddMenu(commands,L.Text("tools1.linked"),ToggleLinkedBrowsing);linkedMenu=(MenuItem)commands.Items[commands.Items.Count-1];
   AddMenu(commands,L.Text("tools1.palette"),ShowCommandPalette,"Ctrl+Shift+P");
   AddMenu(config,L.Text("tools1.shortcuts"),ShowShortcutEditor);
  }
  void InitializeReleaseTools() {
   try{customShortcuts=WorkflowStore.Load<ShortcutSettings>("shortcuts.xml");if(customShortcuts==null||customShortcuts.Items==null||customShortcuts.Items.Count>1000||customShortcuts.Items.Any(x=>x==null||String.IsNullOrEmpty(x.Id)||x.Id.Length>250||(x.Gesture??"").Length>100))throw new IOException(L.Text("tools1.invalidShortcuts"));}catch(Exception ex){customShortcuts=new ShortcutSettings();operation.Text=ex.Message;}
   RefreshCommandGestures();left.PathChanged+=LinkedPathChanged;right.PathChanged+=LinkedPathChanged;
  }
  string Gesture(ReleaseCommand c){var item=customShortcuts.Items.FirstOrDefault(x=>x.Id==c.Id);return item==null?c.Default:item.Gesture;}
  void RefreshCommandGestures(){foreach(var c in releaseCommands){c.Item.InputGestureText=Gesture(c);foreach(var item in c.Mirrors)item.InputGestureText=Gesture(c);}}
  internal static KeyGesture ParseGesture(string text){return String.IsNullOrWhiteSpace(text)?null:(KeyGesture)new KeyGestureConverter().ConvertFromInvariantString(text);}
  bool HandleReleaseKeys(KeyEventArgs e) {
   if(busy||PanelsLoading||Keyboard.FocusedElement is TextBox||Keyboard.FocusedElement is PasswordBox)return false;
   Key key=e.Key==Key.System?e.SystemKey:e.Key;var modifiers=Keyboard.Modifiers;
   foreach(var c in releaseCommands){if(MatchesCommand(c,key,modifiers)){e.Handled=true;if(!e.IsRepeat)c.Run();return true;}}
   foreach(var c in releaseCommands.Where(x=>customShortcuts.Items.Any(y=>y.Id==x.Id))){if(DefaultMatches(c,key,modifiers)){e.Handled=true;return true;}}
   return false;
  }
  internal static bool ShortcutMatches(Key key,ModifierKeys modifiers,string text){
   foreach(string part in (text??"").Split('/')){string value=part.Trim();if(value.Length==0)continue;
    if(modifiers==ModifierKeys.None&&((value=="Num *"&&key==Key.Multiply)||(value=="Num +"&&key==Key.Add)||(value=="Num -"&&key==Key.Subtract)||(value=="Backspace"&&key==Key.Back)||(value=="Delete"&&key==Key.Delete)))return true;
    try{var g=ParseGesture(value);if(g!=null&&g.Key==key&&g.Modifiers==modifiers)return true;}catch{}
   }return false;
  }
  bool DefaultMatches(ReleaseCommand c,Key key,ModifierKeys modifiers){return (c.Id=="action.delete"&&(key==Key.F8||key==Key.Delete)&&modifiers==ModifierKeys.Shift)||ShortcutMatches(key,modifiers,c.Default)||(c.Id=="action.rename"&&key==Key.F2&&modifiers==ModifierKeys.None);}
  bool MatchesCommand(ReleaseCommand c,Key key,ModifierKeys modifiers){var custom=customShortcuts.Items.FirstOrDefault(x=>x.Id==c.Id);return custom==null?DefaultMatches(c,key,modifiers):ShortcutMatches(key,modifiers,custom.Gesture);}
  internal static bool CommandMatches(string title,string query){return System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(title,query,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0;}
  void ShowCommandPalette() {
   var w=new Window();CopyUi.Style(w,this,L.Text("tools1.palette"),660);var panel=new StackPanel();CopyUi.SectionedSurface(w,panel);
   var search=new TextBox{Margin=new Thickness(2,4,2,8)};panel.Children.Add(search);var list=new ListBox{Height=330};panel.Children.Add(list);
   Action fill=delegate{list.ItemsSource=releaseCommands.Where(x=>CommandMatches(Convert.ToString(x.Item.Header).Replace("_",""),search.Text)).Select(x=>new ListBoxItem{Content=Convert.ToString(x.Item.Header).Replace("_","")+"    "+Gesture(x),Tag=x}).ToList();list.SelectedIndex=0;};
   Action run=delegate{var item=list.SelectedItem as ListBoxItem;if(item==null)return;w.Close();((ReleaseCommand)item.Tag).Run();};
   search.TextChanged+=delegate{fill();};list.MouseDoubleClick+=delegate{run();};w.PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;w.Close();}else if(e.Key==Key.Enter){e.Handled=true;run();}else if(e.Key==Key.Down&&search.IsKeyboardFocusWithin&&list.Items.Count>0){list.Focus();((ListBoxItem)list.Items[0]).Focus();e.Handled=true;}};
   fill();w.Loaded+=delegate{search.Focus();};w.ShowDialog();
  }
  void ShowShortcutEditor() {
   var w=new Window();CopyUi.Style(w,this,L.Text("tools1.shortcuts"),680);var p=new StackPanel();CopyUi.SectionedSurface(w,p);
   var list=new ComboBox{ItemsSource=releaseCommands,DisplayMemberPath="Item.Header",Margin=new Thickness(2,5,2,8)};
   // Bind public display items rather than internal command members.
   list.ItemsSource=releaseCommands.Select(c=>new ComboBoxItem{Content=c.Item.Header,Tag=c}).ToArray();list.DisplayMemberPath="";p.Children.Add(list);
   p.Children.Add(CopyUi.Label(L.Text("tools1.shortcutHelp")));var input=new TextBox();p.Children.Add(input);var status=CopyUi.Label("");p.Children.Add(status);
   list.SelectionChanged+=delegate{var item=list.SelectedItem as ComboBoxItem;if(item!=null)input.Text=Gesture((ReleaseCommand)item.Tag);};list.SelectedIndex=0;
   p.Children.Add(CopyUi.Button(L.Text("action.save"),delegate{try{var c=(ReleaseCommand)((ComboBoxItem)list.SelectedItem).Tag;var gesture=ParseGesture(input.Text);string normalized=gesture==null?"":new KeyGestureConverter().ConvertToInvariantString(gesture);if(gesture!=null&&gesture.Modifiers==ModifierKeys.None&&gesture.Key<Key.F1)throw new IOException(L.Text("tools1.shortcutReserved"));
    if(gesture!=null&&releaseCommands.Any(other=>other!=c&&MatchesCommand(other,gesture.Key,gesture.Modifiers)))throw new IOException(L.Text("tools1.shortcutConflict"));
    var updated=WorkflowStore.Edit<ShortcutSettings>("shortcuts.xml",data=>{data.Items.RemoveAll(x=>x.Id==c.Id);data.Items.Add(new ShortcutSetting{Id=c.Id,Gesture=normalized});});customShortcuts=updated;RefreshCommandGestures();status.Text=L.Text("tools1.saved");
   }catch(Exception ex){status.Text=ex.Message;}}));
   p.Children.Add(CopyUi.Button(L.Text("tools1.reset"),delegate{try{customShortcuts=WorkflowStore.Edit<ShortcutSettings>("shortcuts.xml",data=>data.Items.Clear());RefreshCommandGestures();var item=list.SelectedItem as ComboBoxItem;if(item!=null)input.Text=Gesture((ReleaseCommand)item.Tag);}catch(Exception ex){status.Text=ex.Message;}}));w.ShowDialog();
  }
  static bool SameGesture(KeyGesture gesture,string other){return ShortcutMatches(gesture.Key,gesture.Modifiers,other);}
  static bool Linkable(FilePanel p){return !p.IsRemote&&!p.IsArchive&&!p.IsProjection&&!p.IsShellBrowser&&!String.IsNullOrEmpty(p.CurrentPath)&&Path.IsPathRooted(p.CurrentPath);}
  internal static string LinkedRelative(string root,string path){root=Path.GetFullPath(root).TrimEnd('\\')+"\\";path=Path.GetFullPath(path).TrimEnd('\\');if(String.Equals(root.TrimEnd('\\'),path,StringComparison.OrdinalIgnoreCase))return "";return path.StartsWith(root,StringComparison.OrdinalIgnoreCase)?path.Substring(root.Length):null;}
  void ToggleLinkedBrowsing(){if(linkedBrowsing){linkedBrowsing=false;linkedRequest++;}else if(Linkable(left)&&Linkable(right)){linkedLeft=left.CurrentPath;linkedRight=right.CurrentPath;linkedBrowsing=true;}else{MessageBox.Show(this,L.Text("tools1.localFolders"),ApplicationBrand.Name);return;}linkedMenu.IsChecked=linkedBrowsing;operation.Text=L.Text(linkedBrowsing?"tools1.linkedOn":"tools1.linkedOff");}
  async void LinkedPathChanged(object sender,EventArgs args){
   if(!linkedBrowsing)return;var source=(FilePanel)sender;string expected;
   if(linkedTargets.TryGetValue(source,out expected)){linkedTargets.Remove(source);if(String.Equals(expected,source.CurrentPath,StringComparison.OrdinalIgnoreCase))return;}
   var target=source==left?right:left;string relative=Linkable(source)?LinkedRelative(source==left?linkedLeft:linkedRight,source.CurrentPath):null;
   if(relative==null){ToggleLinkedBrowsing();return;}
   long request=++linkedRequest;string sourcePath=source.CurrentPath;string path=Path.Combine(source==left?linkedRight:linkedLeft,relative);
   try {bool exists=await Task.Run(()=>Directory.Exists(path));if(request!=linkedRequest||!linkedBrowsing)return;if(!exists){operation.Text=L.Text("tools1.counterpartMissing")+" "+path;return;}
    await target.NavigationTask;if(request!=linkedRequest||!linkedBrowsing||source.CurrentPath!=sourcePath)return;
    if(String.Equals(path,target.CurrentPath,StringComparison.OrdinalIgnoreCase))return;linkedTargets[target]=path;target.Navigate(path,true);
   }catch(Exception ex){if(request==linkedRequest)operation.Text=ex.Message;}
  }
 }
}
