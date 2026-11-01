using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
 internal static class MessageBox {
  internal static MessageBoxResult Show(string text,string caption="",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage icon=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.None,MessageBoxOptions options=MessageBoxOptions.None){return Show(null,text,caption,buttons,icon,defaultResult,options);}
  internal static MessageBoxResult Show(Window owner,string text,string caption="",MessageBoxButton buttons=MessageBoxButton.OK,MessageBoxImage icon=MessageBoxImage.None,MessageBoxResult defaultResult=MessageBoxResult.None,MessageBoxOptions options=MessageBoxOptions.None){return ShowCore(owner,text,caption,buttons,icon,defaultResult,options,null);}
  internal static MessageBoxResult SaveChanges(Window owner,string file){return ShowCore(owner,L.Format("codeEditor.unsavedBody",file),L.Text("codeEditor.unsavedTitle"),MessageBoxButton.YesNoCancel,MessageBoxImage.Question,MessageBoxResult.Cancel,MessageBoxOptions.None,answer=>L.Text(answer==MessageBoxResult.Yes?"codeEditor.save":answer==MessageBoxResult.No?"codeEditor.discardChanges":"codeEditor.keepEditing"));}
  static MessageBoxResult ShowCore(Window owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage icon,MessageBoxResult defaultResult,MessageBoxOptions options,Func<MessageBoxResult,string> labels){
   var app=Application.Current;
   if(app==null||app.Dispatcher.HasShutdownStarted||options!=MessageBoxOptions.None||!AppearanceTheme.IsClean&&labels==null)return owner==null?System.Windows.MessageBox.Show(text,caption,buttons,icon,defaultResult,options):System.Windows.MessageBox.Show(owner,text,caption,buttons,icon,defaultResult,options);
   if(!app.Dispatcher.CheckAccess())return (MessageBoxResult)app.Dispatcher.Invoke(new Func<MessageBoxResult>(()=>ShowCore(owner,text,caption,buttons,icon,defaultResult,options,labels)));
   if(owner==null)owner=app.Windows.Cast<Window>().FirstOrDefault(x=>x.IsActive);
   var window=new Window();CopyUi.Style(window,owner,caption,labels==null?560:660);StackPanel actions;var root=SettingsUx.Frame(window,caption,out actions);
   var body=new DockPanel{Margin=new Thickness(20,18,20,18)};
   if(icon!=MessageBoxImage.None){var symbol=new TextBlock{Text=icon==MessageBoxImage.Error?"\uEA39":icon==MessageBoxImage.Warning?"\uE7BA":icon==MessageBoxImage.Question?"\uE9CE":"\uE946",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=25,Foreground=icon==MessageBoxImage.Error?CompactTheme.ThemeBrush("#C42B1C"):icon==MessageBoxImage.Warning?CompactTheme.ThemeBrush("#A86B0A"):FluentTheme.Accent,Margin=new Thickness(0,0,15,0),VerticalAlignment=VerticalAlignment.Top};DockPanel.SetDock(symbol,Dock.Left);body.Children.Add(symbol);}
   var content=new TextBlock{Text=text??"",TextWrapping=TextWrapping.Wrap,FontSize=14,LineHeight=22,Foreground=FluentTheme.Text};System.Windows.Automation.AutomationProperties.SetAutomationId(content,"DialogMessage");
   var menu=new ContextMenu();var copy=new MenuItem{Header=L.Text("details.copy")};copy.Click+=delegate{try{Clipboard.SetText(text??"");}catch(System.Runtime.InteropServices.COMException){}};menu.Items.Add(copy);content.ContextMenu=menu;
   body.Children.Add(new ScrollViewer{Content=content,MaxHeight=340,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});root.Children.Add(body);
   MessageBoxResult result=buttons==MessageBoxButton.OK?MessageBoxResult.OK:buttons==MessageBoxButton.YesNo?MessageBoxResult.No:MessageBoxResult.Cancel;
   MessageBoxResult[] answers=buttons==MessageBoxButton.YesNo?new[]{MessageBoxResult.Yes,MessageBoxResult.No}:buttons==MessageBoxButton.YesNoCancel?new[]{MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel}:buttons==MessageBoxButton.OKCancel?new[]{MessageBoxResult.OK,MessageBoxResult.Cancel}:new[]{MessageBoxResult.OK};
   MessageBoxResult preferred=defaultResult==MessageBoxResult.None?answers[0]:defaultResult;
   foreach(var answer in answers){var choice=answer;string label=labels!=null?labels(answer):answer==MessageBoxResult.Yes?L.Text("action.yes"):answer==MessageBoxResult.No?L.Text("action.no"):answer==MessageBoxResult.Cancel?L.Text("action.cancel"):"OK";var button=CopyUi.Button(label,delegate{result=choice;window.DialogResult=true;});button.IsDefault=answer==preferred;button.IsCancel=answer==MessageBoxResult.Cancel||buttons==MessageBoxButton.OK;actions.Children.Add(button);}
   window.ShowDialog();return result;
  }
 }
}
