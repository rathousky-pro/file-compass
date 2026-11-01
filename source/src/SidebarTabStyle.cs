using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
namespace FileCompass {
 internal static partial class SidebarTabStyle {
  internal static Style Create(){
   var style=new Style(typeof(TabItem));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderBrushProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9,8,9,8)));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,2,0,2)));style.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.Normal));style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Border x:Name='NavFrame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter x:Name='HeaderCaption' ContentSource='Header' TextElement.Foreground='{TemplateBinding Foreground}' HorizontalAlignment='Stretch' VerticalAlignment='Center' RecognizesAccessKey='True'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='HeaderCaption' Property='TextElement.Foreground' Value='{StaticResource CompassAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
   var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ActivePath));style.Triggers.Add(hover);
   var selected=new Trigger{Property=TabItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.WhiteSurface));selected.Setters.Add(new Setter(Control.BorderBrushProperty,CompactTheme.FieldLine));style.Triggers.Add(selected);return style;
  }
 }
}
