using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace FileCompass {
 // Keep the actual bound controls; split categories into named, bounded subpages.
 internal sealed class SettingsPager : DockPanel {
  readonly TabControl sections=CreateTabs();
  static TabControl CreateTabs(){var tabs=new TabControl{Margin=new Thickness(0,0,0,14),BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent};tabs.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='TabControl'><WrapPanel IsItemsHost='True'/></ControlTemplate>");var style=new Style(typeof(TabItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(11,6,11,6)));style.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Muted));style.Setters.Add(new Setter(Control.BackgroundProperty,System.Windows.Media.Brushes.Transparent));style.Setters.Add(new Setter(Control.FontSizeProperty,13.0));style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Border x:Name='Frame' Padding='{TemplateBinding Padding}' Background='{TemplateBinding Background}' BorderBrush='{StaticResource CompassLine}' BorderThickness='1' CornerRadius='6' Margin='0,0,6,6'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/><Setter TargetName='Frame' Property='Background' Value='{StaticResource CompassHover}'/><Setter Property='Foreground' Value='{StaticResource CompassAccent}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));tabs.ItemContainerStyle=style;return tabs;}

  readonly Grid content=new Grid();readonly List<FrameworkElement> units=new List<FrameworkElement>();
  internal void Select(string label){for(int i=0;i<sections.Items.Count;i++)if(Convert.ToString(((TabItem)sections.Items[i]).Header).Contains(label)){sections.SelectedIndex=i;return;}}
  internal int PageCount{get{return units.Count;}}
  internal void Select(int index){sections.SelectedIndex=index;}
  internal bool ContentFits(){content.Measure(new Size(Math.Max(1,content.ActualWidth),Double.PositiveInfinity));return content.DesiredSize.Height<=content.ActualHeight+1;}
  internal SettingsPager(StackPanel source){
   var children=source.Children.Cast<FrameworkElement>().ToArray();source.Children.Clear();
   var heading=new StackPanel();foreach(var item in children.Take(2))heading.Children.Add(item);DockPanel.SetDock(heading,Dock.Top);Children.Add(heading);
   foreach(var item in children.Skip(2)){if(item is TextBlock&&children.Length>1&&children[1] is TextBlock&&((TextBlock)item).Text==((TextBlock)children[1]).Text)continue;Extract(item);}
   var palette=units.OfType<GroupBox>().FirstOrDefault(x=>Convert.ToString(x.Header).Contains("Barevný motiv"));if(palette!=null&&units.IndexOf(palette)>0){var language=units[0];units.Remove(language);units.Remove(palette);var combined=new StackPanel();combined.Children.Add(language);combined.Children.Add(palette);units.Insert(0,new GroupBox{Header="Barevný motiv a jazyk",Content=combined});}
   if(children.Length>0&&children[0] is TextBlock&&((TextBlock)children[0]).Text==L.Text("network.connectionsTitle")){var fields=units.ToArray();units.Clear();AddGroup("Správa připojení",fields);}
   DockPanel.SetDock(sections,Dock.Top);Children.Add(sections);Children.Add(content);
   foreach(var item in units){string label=item is GroupBox?Convert.ToString(((GroupBox)item).Header):item is FontChoiceControl?"Písmo":item is UpdatesView?"Aktualizace komponent":item is ConnectionsView?"Připojení":item is Button?Convert.ToString(((Button)item).Content):"Informace";sections.Items.Add(new TabItem{Header=label});}
   sections.SelectionChanged+=delegate{content.Children.Clear();if(sections.SelectedIndex>=0){var unit=units[sections.SelectedIndex];unit.Margin=new Thickness(0);content.Children.Add(unit);}Resize();Dispatcher.BeginInvoke(new Action(delegate{var window=Window.GetWindow(this);if(window!=null)CleanTheme.Refresh(window);}));};
   sections.Visibility=units.Count>1?Visibility.Visible:Visibility.Collapsed;sections.SelectedIndex=units.Count>0?0:-1;
   SizeChanged+=delegate{Resize();};
  }
  void Extract(FrameworkElement item){if(Convert.ToString(item.Tag)=="settings-section-page"){units.Add(item);return;}
   if(item is UpdatesView){units.Add(item);return;}
   var group=item as GroupBox;var stack=group==null?null:group.Content as StackPanel;
   if(stack!=null){var fieldsGrid=stack.Children.Count==1?stack.Children[0] as Grid:null;if(fieldsGrid!=null&&fieldsGrid.Children.Count>5){var gridFields=fieldsGrid.Children.Cast<FrameworkElement>().ToArray();fieldsGrid.Children.Clear();for(int offset=0;offset<gridFields.Length;offset+=3)AddGroup(gridFields.Length==9?new[]{"Názvy a řazení","Výběr a navigace","Zobrazení a archivy"}[offset/3]:new[]{"Ověření a metadata","Oprávnění a rozsah"}[Math.Min(1,offset/3)],gridFields.Skip(offset).Take(3));return;}var startup=stack.Children.OfType<StartupPreferencesEditor>().FirstOrDefault();if(startup!=null){var fields=startup.Children.Cast<FrameworkElement>().ToArray();startup.Children.Clear();AddGroup("Spuštění aplikace",fields.Take(2));AddGroup("Ukládání pracovního prostoru",fields.Skip(2));return;}if(stack.Children.Count>5){var fields=stack.Children.Cast<FrameworkElement>().ToArray();stack.Children.Clear();for(int offset=0;offset<fields.Length;offset+=3)AddGroup(Convert.ToString(group.Header)+" · "+(offset/3+1),fields.Skip(offset).Take(3));return;}}
   var panel=item as Panel;
   if(panel!=null&&!(item is UpdatesView)&&!(item is ConnectionsView)&&!(item is FontChoiceControl)){
    var nested=panel.Children.Cast<FrameworkElement>().ToArray();if(nested.Any(x=>x is GroupBox)||item is FileBehaviorEditor||item is StartupPreferencesEditor){panel.Children.Clear();foreach(var child in nested)Extract(child);return;}
   }
   units.Add(item);
  }
  void AddGroup(string title,IEnumerable<FrameworkElement> fields){var body=new StackPanel();foreach(var field in fields)body.Children.Add(field is DataGrid?(UIElement)new PagedSettingsGrid((DataGrid)field):field);units.Add(new GroupBox{Header=title,Content=body});}
  void Resize(){foreach(var scroll in FluentUiTestsSafe.Children<ScrollViewer>(content)){scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;} }
 }
 internal static class FluentUiTestsSafe {
  internal static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject{if(root is T)yield return (T)root;foreach(object child in LogicalTreeHelper.GetChildren(root)){var d=child as DependencyObject;if(d!=null)foreach(var item in Children<T>(d))yield return item;}}
 }
}
