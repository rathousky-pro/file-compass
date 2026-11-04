using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace FileCompass {
 internal static class TokenDropdown {
  internal static readonly DependencyProperty MenuProperty=DependencyProperty.RegisterAttached("Menu",typeof(ContextMenu),typeof(TokenDropdown));
  static readonly DependencyProperty OpenProperty=DependencyProperty.RegisterAttached("Open",typeof(Action),typeof(TokenDropdown));
  internal static void Show(TextBox input){var open=input.GetValue(OpenProperty) as Action;if(open!=null)open();}
  internal static ContextMenu Attach(TextBox input,IEnumerable<string> tokens){
   input.ToolTip=L.Text("rename.metadataTokenHelp");var menu=new ContextMenu{PlacementTarget=input,Placement=PlacementMode.Bottom,MaxHeight=380,MinWidth=270};input.SetValue(MenuProperty,menu);int start=0,length=0;
   foreach(string token in tokens.Distinct()){string value=token;var item=new MenuItem{Header=token,Tag=token};item.Click+=delegate{int position=Math.Min(start,input.Text.Length);input.Select(position,Math.Min(length,input.Text.Length-position));input.SelectedText=value;input.CaretIndex=position+value.Length;input.Select(input.CaretIndex,0);input.Focus();};menu.Items.Add(item);}
   Action open=delegate{if(!input.IsEnabled||input.IsReadOnly)return;start=input.SelectionStart;length=input.SelectionLength;menu.IsOpen=true;};
   input.SetValue(OpenProperty,open);
   bool altOnly=false;
   input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){var key=e.Key==Key.System?e.SystemKey:e.Key;if(key==Key.LeftAlt||key==Key.RightAlt){altOnly=!e.IsRepeat;e.Handled=true;return;}if(key==Key.Down&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0){altOnly=false;open();e.Handled=true;return;}altOnly=false;};
   input.PreviewKeyUp+=delegate(object sender,KeyEventArgs e){var key=e.Key==Key.System?e.SystemKey:e.Key;if(key==Key.LeftAlt||key==Key.RightAlt){if(altOnly)open();altOnly=false;e.Handled=true;}};input.LostKeyboardFocus+=delegate{altOnly=false;};input.Unloaded+=delegate{menu.IsOpen=false;};return menu;
  }
 }
}
