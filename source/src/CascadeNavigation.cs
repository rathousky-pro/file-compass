using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
namespace FileCompass {
 // The original MenuItems are retained: commands, checks, dynamic submenus and enablement
 // continue to be owned by the application. Only their presentation changes.
 internal static class CascadeNavigation {
  internal static readonly Style RowStyle=MakeStyle();
  static readonly DependencyProperty CloseActionProperty=DependencyProperty.RegisterAttached("CloseAction",typeof(Action),typeof(CascadeNavigation));
  static CascadeNavigation(){EventManager.RegisterClassHandler(typeof(MenuItem),MenuItem.ClickEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs e){var item=sender as MenuItem;if(item==null||item.Style!=RowStyle||item.HasItems||!Object.ReferenceEquals(sender,e.OriginalSource))return;ItemsControl owner=ItemsControl.ItemsControlFromItemContainer(item);while(owner is MenuItem)owner=ItemsControl.ItemsControlFromItemContainer(owner);var menu=owner as Menu;var close=menu==null?null:menu.GetValue(CloseActionProperty) as Action;if(close!=null)close();}));}
  static Style MakeStyle(){
   var style=new Style(typeof(MenuItem));
   style.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));
   style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));
   style.Setters.Add(new Setter(Control.FontSizeProperty,14.0));
   style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,6,8,6)));
   style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,32.0));
   style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
   style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='MenuItem'>
 <Grid><Border x:Name='Frame' Background='{TemplateBinding Background}' CornerRadius='5' Padding='{TemplateBinding Padding}' BorderThickness='2,0,0,0' BorderBrush='Transparent'>
  <Grid VerticalAlignment='Center'><Grid.ColumnDefinitions><ColumnDefinition Width='23'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='16'/></Grid.ColumnDefinitions>
   <ContentPresenter x:Name='Icon' ContentSource='Icon' VerticalAlignment='Center' HorizontalAlignment='Center' Width='16' Height='16'/>
   <TextBlock x:Name='Check' Text='✓' Visibility='Collapsed' VerticalAlignment='Center'/>
   <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' Margin='0,0,10,0' TextElement.FontSize='{TemplateBinding FontSize}' TextElement.Foreground='{TemplateBinding Foreground}'/>
   <TextBlock Grid.Column='2' x:Name='Gesture' Text='{TemplateBinding InputGestureText}' Opacity='.75' FontSize='12' Margin='10,0,6,0' VerticalAlignment='Center'/>
   <TextBlock Grid.Column='3' x:Name='Arrow' Text='›' Visibility='Collapsed' VerticalAlignment='Center' HorizontalAlignment='Right'/>
  </Grid>
 </Border>
 <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='5' VerticalOffset='-6' AllowsTransparency='True' Focusable='False' IsOpen='{Binding IsSubmenuOpen,RelativeSource={RelativeSource TemplatedParent}}' PopupAnimation='None'>
  <Border Margin='0,8,14,18' Padding='5' CornerRadius='8' Background='{DynamicResource CascadePaper}' BorderBrush='{DynamicResource CascadeLine}' BorderThickness='1' TextElement.Foreground='{DynamicResource CascadeInk}'>
   <Border.Effect><DropShadowEffect BlurRadius='14' ShadowDepth='4' Opacity='.24'/></Border.Effect>
   <ScrollViewer MaxHeight='{DynamicResource CascadeMenuHeight}' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' CanContentScroll='True'>
    <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle' KeyboardNavigation.TabNavigation='Cycle' Grid.IsSharedSizeScope='True'/>
   </ScrollViewer>
  </Border>
 </Popup></Grid>
 <ControlTemplate.Triggers>
  <Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
  <Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/><Setter TargetName='Icon' Property='Visibility' Value='Hidden'/></Trigger>
  <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource CascadeSelection}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource CascadeAccent}'/></Trigger>
  <Trigger Property='IsSubmenuOpen' Value='True'><Setter TargetName='Frame' Property='Background' Value='{DynamicResource CascadeSelection}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{DynamicResource CascadeAccent}'/></Trigger>
  <Trigger Property='Tag' Value='cascade-root'>
   <Setter Property='Foreground' Value='White'/><Setter TargetName='Gesture' Property='Visibility' Value='Collapsed'/>
  </Trigger>
  <MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='cascade-root'/><Condition Property='IsHighlighted' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Frame' Property='Background' Value='#24689C'/><Setter TargetName='Frame' Property='BorderBrush' Value='#A8DAFF'/></MultiTrigger>
  <MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='cascade-root'/><Condition Property='IsSubmenuOpen' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Frame' Property='Background' Value='#176BAA'/><Setter TargetName='Frame' Property='BorderBrush' Value='#A8DAFF'/></MultiTrigger>
  <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>")));
   return style;
  }
  internal static Menu Build(MenuItem[] roots,Action close){
   var menu=new Menu{Background=Brushes.Transparent,Padding=new Thickness(0),BorderThickness=new Thickness(0),Tag="cascade-menu"};
   menu.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Menu'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></ControlTemplate>");
   menu.Height=Double.NaN;menu.MinHeight=0;menu.HorizontalAlignment=HorizontalAlignment.Stretch;
   menu.ItemsPanel=(ItemsPanelTemplate)XamlReader.Parse("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Vertical'/></ItemsPanelTemplate>");
   menu.SetValue(CloseActionProperty,close);
   menu.Resources["CascadePaper"]=CompactTheme.WhiteSurface;menu.Resources["CascadeLine"]=CompactTheme.Line;menu.Resources["CascadeInk"]=FluentTheme.Text;menu.Resources["CascadeSelection"]=CompactTheme.ThemeBrush("#85C5F4");menu.Resources["CascadeAccent"]=FluentTheme.Accent;
   menu.Resources["CascadeMenuHeight"]=Math.Max(200,SystemParameters.WorkArea.Height-100);
   foreach(var root in roots){Prepare(root);root.Tag="cascade-root";root.Height=Double.NaN;root.Margin=new Thickness(0,1,0,1);root.MouseEnter+=delegate{if(menu.IsVisible&&root.IsEnabled&&root.HasItems){foreach(var sibling in roots)if(sibling!=root)CloseItem(sibling);root.IsSubmenuOpen=true;}};menu.Items.Add(root);}
   menu.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){var item=Keyboard.FocusedElement as MenuItem;if(item==null)return;
    if((e.Key==Key.Down||e.Key==Key.Up)&&ItemsControl.ItemsControlFromItemContainer(item)==menu){var available=menu.Items.OfType<MenuItem>().Where(x=>x.IsEnabled&&x.Visibility==Visibility.Visible).ToArray();int index=Array.IndexOf(available,item);if(index>=0&&available.Length>0){bool open=item.IsSubmenuOpen;item.IsSubmenuOpen=false;var next=available[(index+(e.Key==Key.Down?1:available.Length-1))%available.Length];next.Focus();if(open)next.IsSubmenuOpen=true;e.Handled=true;}}
    else if(e.Key==Key.Right&&item.HasItems){item.IsSubmenuOpen=true;item.Dispatcher.BeginInvoke(new Action(delegate{var first=item.Items.OfType<MenuItem>().FirstOrDefault(x=>x.IsEnabled&&x.Visibility==Visibility.Visible);if(first!=null)first.Focus();}));e.Handled=true;}else if(e.Key==Key.Left){var parent=ItemsControl.ItemsControlFromItemContainer(item) as MenuItem;if(parent!=null){parent.IsSubmenuOpen=false;parent.Focus();e.Handled=true;}}};
   return menu;
  }
  static void Prepare(MenuItem item){item.Style=RowStyle;item.Height=Double.NaN;item.Padding=new Thickness(8,6,8,6);item.Resources[typeof(MenuItem)]=RowStyle;item.MouseEnter+=delegate{if(item.IsVisible&&item.IsEnabled&&item.HasItems){var parent=ItemsControl.ItemsControlFromItemContainer(item);if(parent!=null)foreach(var sibling in parent.Items.OfType<MenuItem>())if(sibling!=item)CloseItem(sibling);item.IsSubmenuOpen=true;}};foreach(var child in item.Items.OfType<MenuItem>())Prepare(child);item.SubmenuOpened+=delegate(object sender,RoutedEventArgs e){if(Object.ReferenceEquals(e.OriginalSource,item))foreach(var child in item.Items.OfType<MenuItem>())PrepareDynamic(child);};}
  static void PrepareDynamic(MenuItem item){if(item.Style!=RowStyle)Prepare(item);}
  internal static void Close(Menu menu){foreach(var root in menu.Items.OfType<MenuItem>())CloseItem(root);}
  static void CloseItem(MenuItem item){item.IsSubmenuOpen=false;foreach(var child in item.Items.OfType<MenuItem>())CloseItem(child);}
 }
}
