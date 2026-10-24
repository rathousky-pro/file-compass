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
  internal static ContextMenu Attach(TextBox input,IEnumerable<string> tokens){
   input.ToolTip=L.Text("rename.metadataTokenHelp")+" (Alt+↓)";var menu=new ContextMenu{PlacementTarget=input,Placement=PlacementMode.Bottom,MaxHeight=380,MinWidth=270};input.SetValue(MenuProperty,menu);int start=0,length=0;
   foreach(string token in tokens.Distinct()){string value=token;var item=new MenuItem{Header=token,Tag=token};item.Click+=delegate{int position=Math.Min(start,input.Text.Length);input.Select(position,Math.Min(length,input.Text.Length-position));input.SelectedText=value;input.CaretIndex=position+value.Length;input.Select(input.CaretIndex,0);input.Focus();};menu.Items.Add(item);}
   Action open=delegate{if(!input.IsEnabled||input.IsReadOnly)return;start=input.SelectionStart;length=input.SelectionLength;menu.IsOpen=true;};
   input.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler(delegate(object sender,MouseButtonEventArgs e){open();}),true);
   input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Down&&Keyboard.Modifiers==ModifierKeys.Alt){open();e.Handled=true;}};input.Unloaded+=delegate{menu.IsOpen=false;};return menu;
  }
 }
}
