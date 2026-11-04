using System.Windows;using System.Windows.Controls;using System.Windows.Markup;
namespace FileCompass {
 internal static class FluentToolbarUx {
  internal static readonly DependencyProperty ActiveProperty=DependencyProperty.RegisterAttached("Active",typeof(bool),typeof(FluentToolbarUx),new PropertyMetadata(false));
  internal static readonly ControlTemplate Template=Build();
  static ControlTemplate Build(){var template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Grid><Border x:Name='Frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='6' Margin='1' Padding='{TemplateBinding Padding}'><ContentPresenter VerticalAlignment='Center' HorizontalAlignment='Center' RecognizesAccessKey='True'/></Border><Border x:Name='ActiveMark' Width='18' Height='3' CornerRadius='1' VerticalAlignment='Bottom' Margin='0,0,0,2' Background='{StaticResource CompassAccent}' Visibility='Collapsed'/></Grid></ControlTemplate>");
   var active=new Trigger{Property=ActiveProperty,Value=true};active.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"ActiveMark"));template.Triggers.Add(active);
   var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,CompactTheme.WhiteSurface,"Frame"));hover.Setters.Add(new Setter(Border.BorderBrushProperty,FluentTheme.Accent,"Frame"));template.Triggers.Add(hover);
   var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,CompactTheme.ThemeBrush("#DDEDFC"),"Frame"));template.Triggers.Add(pressed);
   var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,FluentTheme.Accent,"Frame"));template.Triggers.Add(focus);
   var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.45));template.Triggers.Add(disabled);return template;
  }
  internal static void Refresh(Button button){bool active=(bool)button.GetValue(ActiveProperty);button.Template=Template;button.Background=active?CompactTheme.ThemeBrush("#DDEDFC"):CompactTheme.PanelSurface;button.BorderBrush=active?FluentTheme.Accent:CompactTheme.Line;button.Foreground=active?FluentTheme.Accent:FluentTheme.Text;}
 }
}
