using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
namespace FileCompass {
 public partial class FilePanel {
  static ControlTemplate cleanTabTemplate, tabCloseTemplate;
  void RenderCleanTabs(){
   if(cleanTabTemplate==null)cleanTabTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='TabFrame' CornerRadius='6,6,0,0' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='TabFrame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='TabFrame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
   if(tabCloseTemplate==null)tabCloseTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Hit' Background='Transparent' CornerRadius='4'><TextBlock Text='×' FontSize='17' Foreground='{TemplateBinding Foreground}' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hit' Property='Background' Value='#E4EDF6'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Hit' Property='Background' Value='#D2E4F6'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
   if(activeTabFace!=null){activeTabFace.Child=null;activeTabFace=null;}
   tabStrip.Children.Clear();tabRow.Background=Brushes.Transparent;RefreshCleanNavigation();
   newTabButton.Template=cleanTabTemplate;
   if(EmptyPanel)return;
   var widths=TabWidths();
   for(int i=0;i<tabs.Count;i++){
    int index=i;bool selected=i==tabIndex;
    var menu=BuildTabMenu(index);
    string caption=TabCaption(i);
    TextBlock text;
    if(selected)
    {
     var path=TabDisplayPath(i);
     if(String.IsNullOrEmpty(path))path=caption;
     text=new PathTextBlock { FullPath=path, Prefix=caption.EndsWith(path,StringComparison.Ordinal)?caption.Substring(0,caption.Length-path.Length):"" };
    }
    else text=new TextBlock { Text=caption,TextTrimming=TextTrimming.CharacterEllipsis };
    text.FontSize=TabFontSize;
    text.FontWeight=selected?FontWeights.Medium:FontWeights.Normal;
    text.VerticalAlignment=VerticalAlignment.Center;
    text.Foreground=selected?FluentTheme.Accent:FluentTheme.Text;
    string tooltip=TabToolTip(i);
    var button=new Button{Tag="panel-tab",Template=cleanTabTemplate,Content=text,MinWidth=0,Height=28,Padding=new Thickness(12,0,8,0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=text.Foreground,HorizontalContentAlignment=HorizontalAlignment.Stretch,ContextMenu=menu,ToolTip=tooltip,Focusable=true};button.Click+=delegate{if(index!=tabIndex)SwitchTab(index);else{activate(this);ShowNetworkRange();FocusFiles();}};button.PreviewMouseLeftButtonDown+=async delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){e.Handled=true;if(index!=tabIndex)SwitchTab(index);if(TabNavigationTask!=null)await TabNavigationTask;FocusAddress();}};
    var face=new Border{Tag="panel-tab-face",Child=button,Width=widths[i],Height=40,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0),CornerRadius=new CornerRadius(8,8,0,0),Background=CompactTheme.WhiteSurface,BorderBrush=FluentTheme.Accent,BorderThickness=new Thickness(0),ContextMenu=menu,ToolTip=tooltip};
    face.PreviewMouseDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Middle){CloseTab(index);e.Handled=true;}};
    System.Windows.Automation.AutomationProperties.SetAutomationId(face,"PanelTab");System.Windows.Automation.AutomationProperties.SetName(button,caption);System.Windows.Automation.AutomationProperties.SetItemStatus(button,L.Text(selected?"panel.activeRole":"panel.targetRole"));
    face.Child=null;var tabContent=new DockPanel{LastChildFill=true};
    var close=CompactTheme.Button("×",L.Text("action.close"),delegate{CloseTab(index);});close.Tag="panel-tab-close";close.Template=tabCloseTemplate;close.Width=24;close.Height=24;close.MinWidth=0;close.Padding=new Thickness(0);close.Margin=new Thickness(1,0,3,0);close.VerticalAlignment=VerticalAlignment.Center;close.Background=Brushes.Transparent;close.BorderThickness=new Thickness(0);System.Windows.Automation.AutomationProperties.SetAutomationId(close,"PanelTabClose");DockPanel.SetDock(close,Dock.Right);tabContent.Children.Add(close);tabContent.Children.Add(button);face.Child=tabContent;face.Background=selected?CompactTheme.ActivePath:Brushes.Transparent;
    if(selected)
    {
     face.BorderBrush=FluentTheme.Accent;
     face.BorderThickness=new Thickness(0,0,0,2);
    }
    else
    {
     face.BorderBrush=CompactTheme.FieldLine;
     face.BorderThickness=new Thickness(1,1,1,0);
     face.Margin=new Thickness(1,0,1,0);
    }
    if(selected)activeTabFace=face;tabStrip.Children.Add(face);
   }
   RevealActiveTab();
  }
 }
}
