using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace FileCompass {
 internal static class SettingsUx {
  static readonly DependencyProperty HeadingProperty=DependencyProperty.RegisterAttached("Heading",typeof(DockPanel),typeof(SettingsUx));
  static readonly DependencyProperty HeaderProperty=DependencyProperty.RegisterAttached("Header",typeof(Border),typeof(SettingsUx));
  static readonly DependencyProperty TitleProperty=DependencyProperty.RegisterAttached("Title",typeof(TextBlock),typeof(SettingsUx));
  internal static TextBlock HeaderIcon(string title){
   string glyph="\uE946";
   foreach(var item in new[]{new[]{"menu.configuration","\uE713"},new[]{"settings.title","\uE713"},new[]{"codeEditor.title","\uE70F"},new[]{"archive.packDialogTitle","\uE7B8"},new[]{"copy.designCopy","\uE8C8"},new[]{"copy.designMove","\uE8DE"},new[]{"analyzer.title","\uE9D2"},new[]{"smart.title","\uE9D9"},new[]{"media.title","\uE8B9"}})if(title==L.Text(item[0])){glyph=item[1];break;}
   var icon=new TextBlock{Text=glyph,FontFamily=new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),FontSize=20,Foreground=FluentTheme.Accent,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(icon,Dock.Left);return icon;
  }
  internal static Border HeaderFrame(Window window){return (Border)window.GetValue(HeaderProperty);}
  internal static TextBlock HeaderTitle(Window window){return (TextBlock)window.GetValue(TitleProperty);}
  internal static void HeaderDetail(Window window,FrameworkElement detail){var heading=window.GetValue(HeadingProperty) as DockPanel;if(heading==null)return;DockPanel.SetDock(detail,Dock.Right);heading.Children.Insert(0,detail);}
  internal static DockPanel Frame(Window window,string title,out StackPanel actions){
   Install(window);var shell=new DockPanel();CopyUi.Surface(window,shell);((Border)((Grid)window.Content).Children[0]).Padding=new Thickness(0);
   var heading=new DockPanel{LastChildFill=true};heading.Children.Add(HeaderIcon(title));var titleLabel=new TextBlock{Text=title,FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};heading.Children.Add(titleLabel);window.SetValue(TitleProperty,titleLabel);window.SetValue(HeadingProperty,heading);
   var header=new Border{Tag="dialog-header",Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,0,0,1),CornerRadius=new CornerRadius(8,8,0,0),Padding=new Thickness(20,15,20,15),Child=heading};window.SetValue(HeaderProperty,header);DockPanel.SetDock(header,Dock.Top);shell.Children.Add(header);
   actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var footer=new Border{Tag="dialog-footer",Child=actions,Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(14,12,14,12),CornerRadius=new CornerRadius(0,0,8,8)};DockPanel.SetDock(footer,Dock.Bottom);shell.Children.Add(footer);footer.Loaded+=delegate{footer.Visibility=((Panel)footer.Child).Children.Count==0?Visibility.Collapsed:Visibility.Visible;};var body=new DockPanel();shell.Children.Add(new Border{Tag="dialog-content-card",Child=body,Background=CompactTheme.WhiteSurface});return body;
  }
  internal static string Prompt(Window owner,string title,string value,string description=null,string acceptKey="action.save"){var window=new Window();CopyUi.Style(window,owner,title,460);StackPanel actions;var root=Frame(window,title,out actions);if(!String.IsNullOrEmpty(description)){var help=new TextBlock{Text=description,TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(20,16,20,0)};DockPanel.SetDock(help,Dock.Top);root.Children.Add(help);}var input=new TextBox{Text=value??"",MinHeight=32,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(20,18,20,18)};root.Children.Add(input);var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{window.DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);var save=CopyUi.Button(L.Text(acceptKey),delegate{window.DialogResult=true;});save.IsDefault=true;FluentTheme.Primary(save);actions.Children.Add(save);window.Loaded+=delegate{input.Focus();input.SelectAll();};return window.ShowDialog()==true?input.Text:null;}
  internal static void Install(Window window){FluentTheme.Ensure();}
  internal static void Navigation(ListBox list){
   list.Background=CompactTheme.ToolbarSurface;list.BorderThickness=new Thickness(0);list.HorizontalContentAlignment=HorizontalAlignment.Stretch;list.ItemContainerStyle=NavigationIcons.ItemStyle();
   list.ItemTemplate=NavigationIcons.Template();
  }
 }
}
