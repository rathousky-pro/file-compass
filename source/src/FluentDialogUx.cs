using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shell;
namespace FileCompass {
 internal static class FluentDialogUx {
  static readonly DependencyProperty Installed=DependencyProperty.RegisterAttached("Installed",typeof(bool),typeof(FluentDialogUx));
  static readonly Brush contentSurface=CreateContentSurface();
  static Brush CreateContentSurface(){var last=new GradientStop{Offset=1};System.Windows.Data.BindingOperations.SetBinding(last,GradientStop.ColorProperty,new System.Windows.Data.Binding("Color"){Source=CompactTheme.PanelSurface});var brush=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};brush.GradientStops.Add(new GradientStop(Colors.White,0));brush.GradientStops.Add(last);return brush;}
  static readonly ControlTemplate groupTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='GroupBox'><StackPanel><ContentPresenter ContentSource='Header' Margin='0,0,0,10' TextElement.FontWeight='SemiBold' TextElement.Foreground='{StaticResource CompassText}'/><ContentPresenter ContentSource='Content'/></StackPanel></ControlTemplate>");
  internal static void Refresh(Window w){
   if(w is MainWindow)return;
   if(!(bool)w.GetValue(Installed)){
    w.SetValue(Installed,true);w.WindowStyle=WindowStyle.None;w.Padding=new Thickness(0);w.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Window'><AdornerDecorator><ContentPresenter/></AdornerDecorator></ControlTemplate>".Replace("'","\x27"));
    WindowChrome.SetWindowChrome(w,null);
    var content=w.Content as FrameworkElement;if(content==null)return;
    string title=w.Title;var oldHeading=FluentUiHeading(content);if(oldHeading!=null)title=oldHeading.Text;
    w.Content=null;var shell=new DockPanel{LastChildFill=true};
    var heading=new DockPanel();var close=new Button{Content="×",FontSize=22,Width=42,Height=34,MinWidth=0,Margin=new Thickness(20,0,0,0),Padding=new Thickness(0),Background=Brushes.Transparent,BorderBrush=FluentTheme.Accent,BorderThickness=new Thickness(1),Foreground=FluentTheme.Text,ToolTip=L.Text("action.close")};close.Click+=delegate{w.Close();};DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
    var caption=new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=FluentTheme.Text,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap};
    if(w is NetworkProfileDialog)caption.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Title"){Source=w});
    heading.Children.Add(caption);
    var header=new Border{Tag="fluent-title",Background=CompactTheme.PanelSurface,Padding=new Thickness(24,14,22,14),Child=heading,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,0,0,1)};
    header.MouseLeftButtonDown+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(e.ClickCount==1&&e.OriginalSource is TextBlock)w.DragMove();};DockPanel.SetDock(header,Dock.Top);shell.Children.Add(header);shell.Children.Add(content);
    shell.SizeChanged+=delegate{shell.Clip=new RectangleGeometry(new Rect(0,0,shell.ActualWidth,shell.ActualHeight),15,15);};
    var card=new Border{Tag="fluent-shell",BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(15),Background=CompactTheme.WhiteSurface,Child=shell};var stage=new Grid{Tag="fluent-stage",Margin=new Thickness(30)};stage.Children.Add(new Border{Tag="fluent-shadow",CornerRadius=new CornerRadius(15),Background=CompactTheme.WhiteSurface,Effect=new DropShadowEffect{BlurRadius=44,ShadowDepth=12,Opacity=.48,Color=Colors.Black}});stage.Children.Add(card);w.Content=stage;
    w.FontSize=14;w.Foreground=FluentTheme.Text;
    // Modal dialogs blur only their disabled owner; modeless transfer windows leave it usable.
    FrameworkElement ownerContent=null;Effect prior=null;AdornerLayer shadeLayer=null;FluentModalShade shade=null;
    w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(delegate{if(w.IsVisible&&w.Owner!=null&&!IsWindowEnabled(new WindowInteropHelper(w.Owner).Handle)){ownerContent=w.Owner.Content as FrameworkElement;if(ownerContent!=null){prior=ownerContent.Effect;ownerContent.Effect=new BlurEffect{Radius=7};shadeLayer=AdornerLayer.GetAdornerLayer(ownerContent);if(shadeLayer!=null){shade=new FluentModalShade(ownerContent);shadeLayer.Add(shade);}}}}));
    Action releaseShade=delegate{if(ownerContent!=null)ownerContent.Effect=prior;if(shadeLayer!=null&&shade!=null)shadeLayer.Remove(shade);ownerContent=null;shadeLayer=null;shade=null;};w.Closed+=delegate{releaseShade();};w.IsVisibleChanged+=delegate{if(!w.IsVisible)releaseShade();};w.StateChanged+=delegate{if(w.WindowState==WindowState.Minimized)releaseShade();};
   }
   Walk(w,w);
  }
  static TextBlock FluentUiHeading(DependencyObject node){foreach(object child in LogicalTreeHelper.GetChildren(node)){var t=child as TextBlock;if(t!=null&&t.FontSize==20&&t.FontWeight==FontWeights.SemiBold)return t;var d=child as DependencyObject;if(d!=null){var result=FluentUiHeading(d);if(result!=null)return result;}}return null;}
  static void Walk(DependencyObject node,Window w){
   var e=node as FrameworkElement;if(e!=null&&Convert.ToString(e.Tag)=="fluent-stage")((Panel)e).Background=Brushes.Transparent;
   if(e!=null){string tag=Convert.ToString(e.Tag);if(tag=="fluent-title"||tag=="editor-workspace")return;
    var b=e as Border;if(b!=null){
     if(tag=="fluent-shell"){b.Padding=new Thickness(0);b.Margin=new Thickness(0);b.Effect=null;b.Background=CompactTheme.WhiteSurface;var panel=b.Child as Panel;if(panel!=null)panel.Margin=new Thickness(0);}
     if(tag=="dialog-split-card"){b.Margin=new Thickness(0);b.Padding=new Thickness(0);b.Background=contentSurface;}
     if(tag=="dialog-header")b.Visibility=Visibility.Collapsed;
     if(tag=="window-shell"){b.Padding=new Thickness(0);b.Background=CompactTheme.WhiteSurface;}
     if(tag=="dialog-content-card"||tag=="settings-page-card"||tag=="operation-page-card"){b.Background=contentSurface;b.CornerRadius=new CornerRadius(0);b.Margin=new Thickness(0);}
     if(tag=="dialog-footer"){b.Background=CompactTheme.Chrome;b.Padding=w is CodeEditorWindow?new Thickness(12,8,12,8):new Thickness(20,12,20,12);b.CornerRadius=new CornerRadius(0);b.BorderBrush=FluentColors.SectionLine;b.BorderThickness=new Thickness(0,1,0,0);}
     if(b.CornerRadius.TopLeft>0&&b.BorderThickness.Left>0&&b.BorderThickness.Bottom>0&&tag!="fluent-shell"){b.BorderBrush=FluentColors.SectionLine;b.BorderThickness=new Thickness(2);b.SnapsToDevicePixels=true;b.UseLayoutRounding=true;}
     if(tag=="dialog-navigation"){b.Background=CompactTheme.Chrome;b.CornerRadius=new CornerRadius(0);b.Margin=new Thickness(0);b.Padding=new Thickness(10,18,10,18);b.Width=210;}
    }
    var spinner=e as NumberSpinner;if(spinner!=null){spinner.Width=104;spinner.Input.Padding=new Thickness(5);spinner.Input.MinWidth=70;}
    var text=e as TextBlock;if(text!=null&&text.FontSize<18&&text.FontFamily.Source!="Segoe MDL2 Assets"&&Convert.ToString(text.Tag)!="font-preview")text.FontSize=14;
    var group=e as GroupBox;if(group!=null){group.Template=groupTemplate;group.Background=Brushes.Transparent;group.Padding=new Thickness(0);group.Margin=new Thickness(0,0,0,16);}
    var input=e as Control;if(input is TextBox||input is ComboBox||input is PasswordBox){input.MinHeight=32;if(!Double.IsNaN(input.Height))input.Height=32;input.VerticalContentAlignment=VerticalAlignment.Center;input.Padding=new Thickness(10,4,10,4);input.FontSize=14;input.Background=CompactTheme.WhiteSurface;input.BorderBrush=CompactTheme.FieldLine;if(input.Parent is NumberSpinner||!Double.IsNaN(input.Width)&&input.Width<80){input.Padding=new Thickness(4);input.MinWidth=60;}if(Convert.ToString(input.Tag)=="archive-limit-value"){input.Height=input.MinHeight=28;input.Padding=new Thickness(8,2,8,2);input.Margin=new Thickness(1);}}
    var button=e as Button;if(button!=null){button.MinHeight=32;if(!Double.IsNaN(button.Height))button.Height=32;button.Padding=new Thickness(14,4,14,4);button.FontSize=14;if(!Double.IsNaN(button.Width)&&button.Width<=48)button.Padding=new Thickness(2);}
    if(e is PasswordBox)((PasswordBox)e).FocusVisualStyle=null;
    var check=e as CheckBox;if(check!=null){check.Margin=new Thickness(3,6,3,6);check.FontSize=14;}
    var nav=e as ListBox;if(nav!=null&&nav.Parent is Border&&Convert.ToString(((Border)nav.Parent).Tag)=="dialog-navigation"){nav.Background=CompactTheme.Chrome;nav.Padding=new Thickness(0);}
   }
   foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)Walk(d,w);}
  }
  [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool IsWindowEnabled(IntPtr h);
 }
}
