using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass {
    // The permanent, layered presentation shared by every application window.
    internal static class CleanTheme {
        sealed class Edit { internal DependencyObject Target; internal DependencyProperty Property; internal object Original, Applied; }
        sealed class WindowState { internal readonly List<Edit> Edits=new List<Edit>();internal bool Pending; }
        static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("CleanState",typeof(WindowState),typeof(CleanTheme));
        internal static readonly DependencyProperty PrimaryProperty=DependencyProperty.RegisterAttached("Primary",typeof(bool),typeof(CleanTheme),new PropertyMetadata(false));
        internal static readonly DependencyProperty DestructiveProperty=DependencyProperty.RegisterAttached("Destructive",typeof(bool),typeof(CleanTheme),new PropertyMetadata(false));
        internal static bool DestructiveCaption(string text){return new[]{"delete.permanentTitle"}.Any(key=>text==L.Text(key));}
        static ControlTemplate buttonTemplate;
        static ControlTemplate BuildButtonTemplate(){var template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
          <Border x:Name='Frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Margin='1' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter x:Name='Caption' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True'/></Border>
          <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/><Setter TargetName='Frame' Property='Background' Value='{StaticResource CompassHover}'/></Trigger>
          <Trigger Property='IsDefault' Value='True'><Setter TargetName='Frame' Property='Background' Value='{StaticResource CompassAccent}'/><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/><Setter Property='Foreground' Value='White'/></Trigger>
          <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Frame' Property='BorderBrush' Value='{StaticResource CompassAccent}'/><Setter TargetName='Frame' Property='BorderThickness' Value='1'/></Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.55'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            var primary=new Trigger{Property=PrimaryProperty,Value=true};primary.Setters.Add(new Setter(Border.BackgroundProperty,FluentTheme.Accent,"Frame"));primary.Setters.Add(new Setter(Border.BorderBrushProperty,FluentTheme.Accent,"Frame"));primary.Setters.Add(new Setter(TextElement.ForegroundProperty,Brushes.White,"Caption"));template.Triggers.Add(primary);
            var danger=new Trigger{Property=DestructiveProperty,Value=true};danger.Setters.Add(new Setter(Border.BackgroundProperty,CompactTheme.ThemeBrush("#C6323E"),"Frame"));danger.Setters.Add(new Setter(Border.BorderBrushProperty,CompactTheme.ThemeBrush("#AE2633"),"Frame"));danger.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));danger.Setters.Add(new Setter(TextElement.ForegroundProperty,Brushes.White,"Caption"));template.Triggers.Add(danger);return template;
        }
        internal static void Install(ResourceDictionary resources) {
            if(buttonTemplate==null)buttonTemplate=BuildButtonTemplate();
            foreach(Type type in new[]{typeof(Button),typeof(Wpf.Ui.Controls.Button)}){
                var style=new Style(type,resources[type] as Style);var trigger=Trigger();
                trigger.Setters.Add(new Setter(Control.TemplateProperty,buttonTemplate));
                trigger.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.WhiteSurface));
                trigger.Setters.Add(new Setter(Control.BorderBrushProperty,CompactTheme.Line));
                trigger.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));trigger.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));
                style.Triggers.Add(trigger);resources[type]=style;
            }
            foreach(Type type in new[]{typeof(TextBox),typeof(PasswordBox),typeof(ComboBox)}){var fieldStyle=new Style(type,resources[type] as Style);var fieldTrigger=Trigger();fieldTrigger.Setters.Add(new Setter(Control.TemplateProperty,CleanFieldTemplates.For(type)));fieldStyle.Triggers.Add(fieldTrigger);resources[type]=fieldStyle;}
            var group=new Style(typeof(GroupBox),resources[typeof(GroupBox)] as Style);var clean=Trigger();
            clean.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.PanelSurface));
            clean.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(14)));
            clean.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
            clean.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='GroupBox'><Border Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'><DockPanel><ContentPresenter DockPanel.Dock='Top' ContentSource='Header' Margin='0,0,0,10' TextElement.Foreground='{StaticResource CompassText}' TextElement.FontWeight='Medium'/><ContentPresenter ContentSource='Content'/></DockPanel></Border></ControlTemplate>")));
            group.Triggers.Add(clean);resources[typeof(GroupBox)]=group;
            // New/dynamically loaded pages inherit the same presentation without rebuilding controls.
            EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs args){
                if(!AppearanceTheme.IsClean||!Object.ReferenceEquals(sender,args.OriginalSource))return;
                var element=sender as FrameworkElement;if(element==null||element.TemplatedParent!=null)return;
                var window=Window.GetWindow(element);if(window==null)return;Queue(window);
            }));
            EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler(delegate(object sender,RoutedEventArgs args){if(Object.ReferenceEquals(sender,args.OriginalSource)){var w=(Window)sender;Refresh(w);CleanOptions.Refresh(w);}}));
        }
        static DataTrigger Trigger(){return new DataTrigger{Binding=new Binding("Clean"){Source=InterfaceTheme.State},Value=true};}
        static WindowState State(Window w){var state=w.GetValue(StateProperty) as WindowState;if(state==null){state=new WindowState();w.SetValue(StateProperty,state);}return state;}
        static void Queue(Window window){var state=State(window);if(state.Pending)return;state.Pending=true;window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{state.Pending=false;if(window.IsLoaded)Refresh(window);}));}
        internal static void Refresh(Window window){
            WindowGlass.Apply(window);var state=State(window);Set(state,window,Control.BackgroundProperty,WindowGlass.Background(window));RefreshPanels(window);
            var seen=new HashSet<DependencyObject>();Walk(window,state,window,seen);
            var main=window as MainWindow;if(main!=null){main.RefreshCleanToolbar();main.RefreshCleanPanelAppearance();}else FluentDialogUx.Refresh(window);
        }
        static void RefreshPanels(DependencyObject root){var panel=root as FilePanel;if(panel!=null){panel.RefreshCleanChrome();return;}foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)RefreshPanels(node);}}
        static void Set(WindowState state,DependencyObject target,DependencyProperty property,object value){
            if(BindingOperations.IsDataBound(target,property))return;
            var edit=state.Edits.FirstOrDefault(x=>Object.ReferenceEquals(x.Target,target)&&x.Property==property);
            if(edit==null){if(Equals(target.GetValue(property),value))return;edit=new Edit{Target=target,Property=property,Original=target.ReadLocalValue(property)};state.Edits.Add(edit);}
            edit.Applied=value;if(!Equals(target.GetValue(property),value))target.SetValue(property,value);
        }
        static bool Surface(Brush brush){return Object.ReferenceEquals(brush,CompactTheme.Chrome)||Object.ReferenceEquals(brush,CompactTheme.PanelSurface)||Object.ReferenceEquals(brush,CompactTheme.ToolbarSurface)||Object.ReferenceEquals(brush,CompactTheme.Surface)||Object.ReferenceEquals(brush,CompactTheme.WhiteSurface);}
        static void Walk(DependencyObject node,WindowState state,Window window,HashSet<DependencyObject> seen){
            if(!seen.Add(node))return;
            if(node is FrameworkElement && (Convert.ToString(((FrameworkElement)node).Tag)=="editor-workspace"||Convert.ToString(((FrameworkElement)node).Tag)=="fluent-drawer"||Convert.ToString(((FrameworkElement)node).Tag)=="fluent-title"||Convert.ToString(((FrameworkElement)node).Tag)=="fluent-shadow"))return;
            var element=node as FrameworkElement;
            if(element!=null&&element.TemplatedParent==null){
                if((node is TextBox||node is PasswordBox||node is ComboBox)&&Convert.ToString(element.Tag)!="drive-selector"){var input=(Control)node;Set(state,input,Control.TemplateProperty,CleanFieldTemplates.For(node is ComboBox?typeof(ComboBox):node is PasswordBox?typeof(PasswordBox):typeof(TextBox)));Set(state,input,FrameworkElement.UseLayoutRoundingProperty,true);Set(state,input,UIElement.SnapsToDevicePixelsProperty,true);}
                var button=node as Button;
                if(button!=null&&Convert.ToString(button.Tag)!="panel-tab"&&Convert.ToString(button.Tag)!="panel-tab-close"){
                    bool primary=button.IsDefault||(button is Wpf.Ui.Controls.Button&&((Wpf.Ui.Controls.Button)button).Appearance==Wpf.Ui.Controls.ControlAppearance.Primary);
                    bool toolbar=window is MainWindow;
                    Set(state,button,Control.TemplateProperty,buttonTemplate);
                    Set(state,button,PrimaryProperty,primary);Set(state,button,DestructiveProperty,DestructiveCaption(Convert.ToString(button.Content))||DestructiveCaption(Convert.ToString(button.ToolTip)));
                    Set(state,button,FrameworkElement.UseLayoutRoundingProperty,true);
                    if(Convert.ToString(button.Tag).StartsWith("drive:",StringComparison.Ordinal)||Convert.ToString(button.Tag)=="panel-breadcrumb"){} else if(toolbar||!primary){Set(state,button,Control.BackgroundProperty,toolbar&&Convert.ToString(button.Tag)!="function-key"?CompactTheme.PanelSurface:CompactTheme.WhiteSurface);Set(state,button,Control.ForegroundProperty,FluentTheme.Text);Set(state,button,Control.BorderBrushProperty,CompactTheme.Line);}
                    else{Set(state,button,Control.BackgroundProperty,FluentTheme.Accent);Set(state,button,Control.ForegroundProperty,Brushes.White);}
                    if((bool)button.GetValue(DestructiveProperty))Set(state,button,Control.ForegroundProperty,Brushes.White);
                    Set(state,button,Control.BorderThicknessProperty,new Thickness(1));Set(state,button,Control.FocusVisualStyleProperty,null);Set(state,button,UIElement.SnapsToDevicePixelsProperty,true);Set(state,button,FrameworkElement.UseLayoutRoundingProperty,true);
                    if(Convert.ToString(button.Tag)=="function-key")Set(state,button,ContentControl.ContentTemplateProperty,FunctionKeyIcons.KeyCaption);
                                        if(Convert.ToString(button.Tag)=="archive-summary"){
                        Set(state,button,Control.BackgroundProperty,CompactTheme.ThemeBrush("#ECF3FA"));Set(state,button,Control.ForegroundProperty,FluentTheme.Muted);Set(state,button,Control.BorderBrushProperty,CompactTheme.Line);Set(state,button,FrameworkElement.HeightProperty,35.0);Set(state,button,Control.PaddingProperty,new Thickness(10,4,10,4));Set(state,button,FrameworkElement.MarginProperty,new Thickness(0,0,6,0));
                    }
                    if(System.Windows.Automation.AutomationProperties.GetAutomationId(button)=="CleanMore"){
                        Set(state,button,Control.BackgroundProperty,CompactTheme.ThemeBrush("#ECF3FA"));Set(state,button,Control.ForegroundProperty,FluentTheme.Accent);Set(state,button,Control.BorderBrushProperty,CompactTheme.Line);Set(state,button,FrameworkElement.HeightProperty,30.0);Set(state,button,Control.PaddingProperty,new Thickness(12,3,12,3));
                    }
                    if(Convert.ToString(button.Tag)=="toolbar-action")FluentToolbarUx.Refresh(button);
                    var ui=button as Wpf.Ui.Controls.Button;if(ui!=null)Set(state,ui,Wpf.Ui.Controls.Button.CornerRadiusProperty,new CornerRadius(6));
                    if(toolbar&&Convert.ToString(button.Tag)!="panel-breadcrumb"&&!Convert.ToString(button.Tag).StartsWith("drive:",StringComparison.Ordinal)&&Convert.ToString(button.Tag)!="preview-close"){Set(state,button,FrameworkElement.MarginProperty,new Thickness(3,3,3,3));}
                }
                var border=node as Border;
                if(border!=null&&window is ImageViewerWindow&&border.Child is ScrollViewer)Set(state,border,Border.BackgroundProperty,CompactTheme.WhiteSurface);
                if(border!=null&&!(border is QuickPreviewPane)&&!(border is FilePanel)&&!Convert.ToString(border.Tag).StartsWith("panel-",StringComparison.Ordinal)&&Surface(border.Background)){
                    string role=Convert.ToString(border.Tag);
                    bool frame=role=="window-shell"||Object.ReferenceEquals(border.Parent,window)||border.Parent is Grid&&Object.ReferenceEquals(((FrameworkElement)border.Parent).Parent,window);
                    Set(state,border,Border.BackgroundProperty,frame?WindowGlass.Layer(window):CompactTheme.PanelSurface);
                    if(border.CornerRadius.TopLeft>0)Set(state,border,Border.CornerRadiusProperty,new CornerRadius(frame?16:9));
                    if(frame&&border.Child!=null){
                        Set(state,border,Border.BorderBrushProperty,CompactTheme.ChromeLine);Set(state,border,Border.BorderThicknessProperty,new Thickness(0));Set(state,border,Border.PaddingProperty,new Thickness(0));Set(state,border,FrameworkElement.MarginProperty,new Thickness(0));
                        var frameBody=border.Child as Panel;
                        if(frameBody!=null){bool composed=frameBody.Children.OfType<FrameworkElement>().Any(x=>Convert.ToString(x.Tag).StartsWith("dialog-",StringComparison.Ordinal)||Convert.ToString(x.Tag)=="dialog-workspace");Set(state,frameBody,Panel.BackgroundProperty,composed?WindowGlass.Layer(window):CompactTheme.WhiteSurface);if(!composed){Set(state,border,Border.PaddingProperty,new Thickness(8));Set(state,frameBody,FrameworkElement.MarginProperty,new Thickness(6));}}
                    }
                    else if(border.BorderThickness.Left>0&&border.BorderThickness.Top>0)Set(state,border,Border.BorderBrushProperty,window is MainWindow?CompactTheme.Line:FluentColors.SectionLine);
                }
                if(border!=null){string role=Convert.ToString(border.Tag);
                    if(role=="main-toolbar-frame"){Set(state,border,Border.BackgroundProperty,CompactTheme.PanelSurface);Set(state,border,Border.BorderThicknessProperty,new Thickness(0,1,0,1));Set(state,border,Border.CornerRadiusProperty,new CornerRadius(0));Set(state,border,FrameworkElement.MarginProperty,new Thickness(0));Set(state,border,Border.PaddingProperty,new Thickness(12,10,12,10));}
                    if(role=="dialog-header"||role=="dialog-footer"||role=="dialog-content-card"||role=="operation-page-card"||role=="settings-page-card"){
                        Set(state,border,Border.BackgroundProperty,role=="dialog-header"||role=="dialog-footer"?CompactTheme.PanelSurface:CompactTheme.WhiteSurface);Set(state,border,Border.BorderThicknessProperty,new Thickness(0));Set(state,border,Border.CornerRadiusProperty,new CornerRadius(6));Set(state,border,FrameworkElement.MarginProperty,new Thickness(0));
                    }
                    if(role=="preview-header-card"||role=="preview-content-card"||border is ActivityTile){Set(state,border,Border.BackgroundProperty,CompactTheme.WhiteSurface);Set(state,border,Border.CornerRadiusProperty,new CornerRadius(10));}
                    if(role=="dialog-split-card"){Set(state,border,Border.BackgroundProperty,WindowGlass.Layer(window));Set(state,border,Border.BorderThicknessProperty,new Thickness(0));Set(state,border,FrameworkElement.MarginProperty,new Thickness(8,0,8,0));}
                    if(role=="dialog-navigation"){Set(state,border,Border.BackgroundProperty,CompactTheme.ToolbarSurface);Set(state,border,Border.BorderThicknessProperty,new Thickness(0));Set(state,border,Border.CornerRadiusProperty,new CornerRadius(10));}
                }
                if(border!=null&&Convert.ToString(border.Tag)=="window-shell")Set(state,border,Border.BackgroundProperty,WindowGlass.Layer(window));
                if(node is Menu&&Convert.ToString(element.Tag)=="main-menu-area")Set(state,node,Control.BackgroundProperty,WindowGlass.Layer(window));
                var panel=node as Panel;
                if(panel!=null){string role=Convert.ToString(panel.Tag);
                    if(Object.ReferenceEquals(panel.Parent,window)||role=="main-menu-area"||role=="main-toolbar-area"||role=="main-bottom-area"||role=="dialog-workspace")Set(state,panel,Panel.BackgroundProperty,WindowGlass.Layer(window));
                }
                var group=node as GroupBox;if(group!=null){var style=Application.Current.Resources[typeof(GroupBox)] as Style;if(style!=null)Set(state,group,FrameworkElement.StyleProperty,style);}
            }
            foreach(object child in LogicalTreeHelper.GetChildren(node)){var d=child as DependencyObject;if(d!=null)Walk(d,state,window,seen);}
        }
    }
}
