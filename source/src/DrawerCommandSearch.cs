using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace FileCompass {
 public partial class MainWindow {
  internal static string NormalizeCommandSearch(string value){
   var text=new StringBuilder();foreach(char c in (value??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&c!='_')text.Append(Char.ToLowerInvariant(c));return text.ToString().Normalize(NormalizationForm.FormC);
  }
  internal static bool CommandSearchMatches(string caption,string query){
   string value=NormalizeCommandSearch(caption);
   return NormalizeCommandSearch(query).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(word=>value.Contains(word)||(word=="prejmenovani"&&value.Contains("prejmenov")));
  }
  void InstallDrawerSearch(DockPanel drawerBody,MenuItem[] roots){
   var host=new Grid{Margin=new Thickness(12,12,12,4)};
   fluentCommandSearch=new TextBox{Height=36,Padding=new Thickness(8,6,28,6),VerticalContentAlignment=VerticalAlignment.Center,Template=CleanFieldTemplates.For(typeof(TextBox)),ToolTip=L.Text("tools1.palette")};
   System.Windows.Automation.AutomationProperties.SetAutomationId(fluentCommandSearch,"DrawerCommandSearch");System.Windows.Automation.AutomationProperties.SetName(fluentCommandSearch,L.Text("tools1.palette"));
   fluentCommandSearch.Loaded+=delegate{var textHost=fluentCommandSearch.Template.FindName("PART_ContentHost",fluentCommandSearch) as ScrollViewer;if(textHost!=null)textHost.Margin=new Thickness(8,0,8,0);};
   host.Children.Add(fluentCommandSearch);var hint=new TextBlock{Text=L.Text("action.search")+"…",Foreground=FluentTheme.Muted,Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};host.Children.Add(hint);
   DockPanel.SetDock(host,Dock.Top);drawerBody.Children.Insert(0,host);
   var results=new StackPanel{Margin=new Thickness(12,4,12,8)};var scroll=new ScrollViewer{Content=results,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Visibility=Visibility.Collapsed};drawerBody.Children.Add(scroll);
   // The original scroll viewer fills the body when no query is entered.
   var normal=(ScrollViewer)drawerBody.Children.OfType<ScrollViewer>().First(x=>x!=scroll);
   drawerBody.Children.Remove(normal);drawerBody.Children.Remove(scroll);var pages=new Grid();pages.Children.Add(normal);pages.Children.Add(scroll);drawerBody.Children.Add(pages);
   Action refresh=delegate{
    CascadeNavigation.Close(fluentCascadeMenu);results.Children.Clear();string query=fluentCommandSearch.Text.Trim();bool filtering=query.Length>0;hint.Visibility=filtering?Visibility.Collapsed:Visibility.Visible;normal.Visibility=filtering?Visibility.Collapsed:Visibility.Visible;scroll.Visibility=filtering?Visibility.Visible:Visibility.Collapsed;
    if(!filtering)return;
    Action<MenuItem,string,bool> visit=null;visit=delegate(MenuItem item,string path,bool enabled){
     if(item.Visibility!=Visibility.Visible)return;string title=Convert.ToString(item.Header).Replace("_","");bool available=enabled&&item.IsEnabled;
     if(item.HasItems){item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent,item));foreach(var child in item.Items.OfType<MenuItem>().ToArray())visit(child,path.Length==0?title:path+" / "+title,available);return;}
     if(!CommandSearchMatches(title,query))return;
     var caption=new StackPanel();caption.Children.Add(new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.White});caption.Children.Add(new TextBlock{Text=path,FontSize=10,Foreground=System.Windows.Media.Brushes.LightSteelBlue,TextWrapping=TextWrapping.Wrap});
     var button=new Button{Content=caption,ToolTip=path,IsEnabled=available,Style=DrawerControls.ActionStyle,Padding=new Thickness(8,6,8,6),HorizontalContentAlignment=HorizontalAlignment.Stretch};
     button.Click+=delegate{if(busy||PanelsLoading||!item.IsEnabled)return;CloseFluentDrawer();if(item.IsCheckable)item.IsChecked=!item.IsChecked;if(item.Command!=null){if(item.Command.CanExecute(item.CommandParameter))item.Command.Execute(item.CommandParameter);}else item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent,item));};results.Children.Add(button);
    };
    foreach(var root in roots)visit(root,"",true);
    if(results.Children.Count==0)results.Children.Add(new TextBlock{Text=L.Text("help.noResults"),Foreground=System.Windows.Media.Brushes.White,TextWrapping=TextWrapping.Wrap});
   };
   fluentCommandSearch.TextChanged+=delegate{refresh();};
   fluentCommandSearch.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Down||e.Key==Key.Enter){var first=results.Children.OfType<Button>().FirstOrDefault(x=>x.IsEnabled);if(first!=null){first.Focus();e.Handled=true;}}};
  }
 }
}
