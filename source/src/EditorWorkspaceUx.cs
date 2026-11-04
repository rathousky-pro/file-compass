using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
namespace FileCompass {
 internal static class EditorWorkspaceUx {
  internal static void Install(TabControl tabs){
   tabs.Tag="editor-workspace";tabs.Margin=new Thickness(0);tabs.Padding=new Thickness(0);tabs.Background=CompactTheme.WhiteSurface;tabs.BorderThickness=new Thickness(0);
   tabs.Resources["EditorTabSurface"]=CompactTheme.PanelSurface;tabs.Resources["EditorPaper"]=CompactTheme.WhiteSurface;tabs.Resources["EditorLine"]=CompactTheme.Line;tabs.Resources["EditorAccent"]=FluentTheme.Accent;
   tabs.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='TabControl'><Grid KeyboardNavigation.TabNavigation='Local'><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/></Grid.RowDefinitions><Border Background='{DynamicResource EditorTabSurface}' BorderBrush='{DynamicResource EditorLine}' BorderThickness='0,1,0,1'><TabPanel IsItemsHost='True' Margin='12,4,12,0'/></Border><ContentPresenter Grid.Row='1' ContentSource='SelectedContent' HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Grid></ControlTemplate>");
   var itemStyle=new Style(typeof(TabItem));itemStyle.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Muted));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,7,12,7)));itemStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,110.0));itemStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty,320.0));itemStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Border x:Name='Tab' Background='Transparent' CornerRadius='5,5,0,0' Margin='0,0,4,0'><Border x:Name='Accent' Padding='{TemplateBinding Padding}' BorderThickness='0,0,0,2' BorderBrush='Transparent'><ContentPresenter ContentSource='Header' RecognizesAccessKey='True' TextElement.Foreground='{TemplateBinding Foreground}'/></Border></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource EditorPaper}'/><Setter TargetName='Accent' Property='BorderBrush' Value='{DynamicResource EditorAccent}'/><Setter Property='Foreground' Value='{DynamicResource EditorAccent}'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Tab' Property='Background' Value='{DynamicResource EditorPaper}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));tabs.ItemContainerStyle=itemStyle;
  }
 }
}
