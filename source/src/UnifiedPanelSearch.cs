using System;using System.Windows;using System.Windows.Controls;using System.Windows.Input;
namespace FileCompass {
 public partial class MainWindow {
  TextBox fluentSearchInput;bool synchronizingFluentSearch;
  void InstallUnifiedSearch(){foreach(var panel in new[]{left,right}){panel.UnifiedQuickSearch=true;panel.QuickFilterChanged=delegate(FilePanel sender){if(sender==active)SyncFluentSearch();};panel.QuickFilterFocusRequested=delegate(FilePanel sender){SetActive(sender);SyncFluentSearch();fluentSearchInput.Focus();fluentSearchInput.SelectAll();};}SyncFluentSearch();}
  void SyncFluentSearch(){if(fluentSearchInput==null||active==null)return;string text=active.QuickFilterText;if(fluentSearchInput.Text==text)return;synchronizingFluentSearch=true;try{fluentSearchInput.Text=text;fluentSearchInput.CaretIndex=text.Length;}finally{synchronizingFluentSearch=false;}}
 }
 public partial class FilePanel {
  internal bool UnifiedQuickSearch;internal Action<FilePanel> QuickFilterChanged,QuickFilterFocusRequested;
  internal string QuickFilterText{get{return quickText.Text;}}
  internal bool HandleQuickSearchKey(KeyEventArgs e){if(Keyboard.Modifiers!=ModifierKeys.None||String.IsNullOrEmpty(quickText.Text))return false;if(e.Key==Key.Escape){SetQuickFilter("");e.Handled=true;return true;}if(e.Key==Key.Back){SetQuickFilter(quickText.Text.Substring(0,quickText.Text.Length-1));e.Handled=true;return true;}if(UnifiedQuickSearch&&e.Key==Key.Space){SetQuickFilter(quickText.Text+" ");e.Handled=true;return true;}return false;}
 }
}
