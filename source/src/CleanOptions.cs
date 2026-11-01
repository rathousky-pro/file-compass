using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Data;

namespace FileCompass {
    // A single content area: More only reveals navigation, never a second form.
    internal static class CleanOptions {
        sealed class Session {
            internal Window Window;
            internal Panel Actions;
            internal Button More;
            internal Action<bool> ShowNavigation;
            internal Func<bool> Available;
            internal bool Expanded, WasClean; internal double ExtraWidth;
        }
        static readonly DependencyProperty SessionsProperty=DependencyProperty.RegisterAttached("Sessions",typeof(List<Session>),typeof(CleanOptions));
        internal static void Register(Window window,Panel actions,Action<bool> showNavigation,Func<bool> available) {
            var sessions=window.GetValue(SessionsProperty) as List<Session>;
            if(sessions==null){sessions=new List<Session>();window.SetValue(SessionsProperty,sessions);}
            sessions.Add(new Session{Window=window,Actions=actions,ShowNavigation=showNavigation,Available=available});
            window.Loaded+=delegate{Refresh(window);};
        }
        internal static void Tabs(Window window,TabControl tabs,Panel actions,Action<bool> mount=null,UIElement sharedContent=null) {
            object originalTemplate=tabs.ReadLocalValue(Control.TemplateProperty), originalPlacement=tabs.ReadLocalValue(TabControl.TabStripPlacementProperty), originalTag=tabs.ReadLocalValue(FrameworkElement.TagProperty);
            bool styled=false;var originalHeaders=new Dictionary<TabItem,object>();var originalStyles=new Dictionary<TabItem,object>();var navigationStyle=SidebarTabStyle.Create();
            Register(window,actions,delegate(bool visible){
                if(mount!=null)mount(AppearanceTheme.IsClean);
                if(AppearanceTheme.IsClean){
                    if(!styled){
                        string selected="<ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent' HorizontalAlignment='Stretch' VerticalAlignment='Stretch' Margin='12,0,0,0'/>";
                        if(sharedContent!=null){tabs.Resources["CleanSharedContent"]=sharedContent;selected="<DockPanel x:Name='RightBody' Margin='12,0,0,0'><ContentPresenter x:Name='PART_SelectedContentHost' DockPanel.Dock='Top' ContentSource='SelectedContent' HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/><ContentPresenter Content='{DynamicResource CleanSharedContent}'/></DockPanel>";}
                        string collapsed=sharedContent==null?"<Setter TargetName='PART_SelectedContentHost' Property='Margin' Value='0'/>":"<Setter TargetName='RightBody' Property='Margin' Value='0'/>";
                        tabs.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabControl'><DockPanel KeyboardNavigation.TabNavigation='Local'><Border x:Name='Navigation' DockPanel.Dock='Left' Width='174' VerticalAlignment='Stretch' Margin='0' CornerRadius='10' Background='{StaticResource CompassNavigation}' BorderThickness='0' Padding='7'><StackPanel IsItemsHost='True'/></Border>"+selected+"</DockPanel><ControlTemplate.Triggers><Trigger Property='Tag' Value='clean-options-collapsed'><Setter TargetName='Navigation' Property='Visibility' Value='Collapsed'/>"+collapsed+"</Trigger></ControlTemplate.Triggers></ControlTemplate>");styled=true;
                    }
                    foreach(var tab in tabs.Items.OfType<TabItem>()){if(!originalStyles.ContainsKey(tab))originalStyles[tab]=tab.ReadLocalValue(FrameworkElement.StyleProperty);tab.Style=navigationStyle;if(tab.Header is string){if(!originalHeaders.ContainsKey(tab))originalHeaders[tab]=tab.ReadLocalValue(HeaderedContentControl.HeaderTemplateProperty);tab.HeaderTemplate=NavigationIcons.Template();}}
                    tabs.Tag=visible?"clean-options-expanded":"clean-options-collapsed";
                    if(!visible)tabs.SelectedIndex=0;
                }else if(styled){foreach(var pair in originalStyles)Restore(pair.Key,FrameworkElement.StyleProperty,pair.Value);originalStyles.Clear();foreach(var pair in originalHeaders)Restore(pair.Key,HeaderedContentControl.HeaderTemplateProperty,pair.Value);originalHeaders.Clear();Restore(tabs,Control.TemplateProperty,originalTemplate);Restore(tabs,TabControl.TabStripPlacementProperty,originalPlacement);Restore(tabs,FrameworkElement.TagProperty,originalTag);styled=false;}
            },delegate{return tabs.Items.Count>1;});
        }
        internal static void Sections(Window window,StackPanel body,Panel actions){
            var expanders=body.Children.OfType<Expander>().ToArray();if(expanders.Length==0)return;
            var original=body.Children.Cast<UIElement>().ToArray();var contents=expanders.Select(x=>x.Content).ToArray();
            var tabs=new TabControl();var basic=new StackPanel();tabs.Items.Add(new TabItem{Header=L.Text("operation.basic")});
            foreach(var expander in expanders)tabs.Items.Add(new TabItem{Header=expander.Header});tabs.SelectedIndex=0;
            bool mounted=false;
            Tabs(window,tabs,actions,delegate(bool clean){
                if(clean==mounted)return;mounted=clean;
                if(clean){
                    foreach(var child in original.Where(x=>x!=actions))body.Children.Remove(child);
                    foreach(var child in original.Where(x=>x!=actions&&!(x is Expander)))basic.Children.Add(child);
                    ((TabItem)tabs.Items[0]).Content=basic;
                    for(int i=0;i<expanders.Length;i++){expanders[i].Content=null;((TabItem)tabs.Items[i+1]).Content=contents[i];}
                    body.Children.Insert(0,tabs);
                }else{
                    body.Children.Remove(tabs);basic.Children.Clear();foreach(TabItem item in tabs.Items)item.Content=null;
                    for(int i=0;i<expanders.Length;i++)expanders[i].Content=contents[i];
                    // The footer may already live in the shared left/right action wrapper.
                    int index=0;foreach(var child in original.Where(x=>x!=actions))body.Children.Insert(index++,child);
                }
            });
        }
        internal static void Refresh(Window window) {
            var sessions=window.GetValue(SessionsProperty) as List<Session>;if(sessions==null)return;
            foreach(var s in sessions){
                if(s.More==null){
                    // Delay wrapping until constructors finish (several tools compose their own footers).
                    var parent=s.Actions.Parent as DependencyObject;if(parent==null)continue;
                    var slot=new Grid();slot.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});slot.ColumnDefinitions.Add(new ColumnDefinition());
                    if(!Replace(parent,s.Actions,slot))continue;
                    slot.Children.Add(s.Actions);Grid.SetColumn(s.Actions,1);
                    s.More=CopyUi.Button(L.Text("action.more"),delegate{});s.More.HorizontalAlignment=HorizontalAlignment.Left;s.More.MinWidth=86;
                    s.More.ContentTemplate=MoreCaption();
                    s.More.Click+=delegate{s.Expanded=!s.Expanded;Update(s);};
                    System.Windows.Automation.AutomationProperties.SetAutomationId(s.More,"CleanMore");
                    slot.Children.Add(s.More);
                }
                if(s.WasClean!=AppearanceTheme.IsClean){s.Expanded=false;s.WasClean=AppearanceTheme.IsClean;}
                Update(s);
            }
        }
        static void Resize(Session s,bool expanded){
            double target=expanded?174:0, delta=target-s.ExtraWidth;if(Math.Abs(delta)<.1)return;
            var w=s.Window;double width=w.ActualWidth>0?w.ActualWidth:w.Width;if(Double.IsNaN(width))return;
            if(w.WindowState!=WindowState.Normal)return;
            if(w.SizeToContent==SizeToContent.WidthAndHeight)w.SizeToContent=SizeToContent.Height;else if(w.SizeToContent==SizeToContent.Width)w.SizeToContent=SizeToContent.Manual;
            double next=Math.Max(w.MinWidth,Math.Min(w.MaxWidth,width+delta));double change=next-width;
            w.Width=next;if(!Double.IsNaN(w.Left))w.Left-=change;s.ExtraWidth+=change;
        }
        static void Update(Session s){Resize(s,AppearanceTheme.IsClean&&s.Expanded&&s.Available());bool clean=AppearanceTheme.IsClean,available=s.Available();s.More.Visibility=clean&&available?Visibility.Visible:Visibility.Collapsed;s.More.Tag=s.Expanded;s.More.Content=L.Text(s.Expanded?"action.less":"action.more");s.ShowNavigation(!clean||s.Expanded);}
        internal static void Reveal(Window window){var sessions=window.GetValue(SessionsProperty) as List<Session>;if(sessions==null)return;Refresh(window);foreach(var s in sessions){s.Expanded=true;Update(s);}}
        static DataTemplate MoreCaption(){return (DataTemplate)XamlReader.Parse(@"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><StackPanel Orientation='Horizontal'><Path x:Name='Arrow' Data='M5,1 L1,6 L5,11 M12,1 L8,6 L12,11' Stroke='{Binding Foreground,RelativeSource={RelativeSource AncestorType=Button}}' StrokeThickness='1.2' Width='14' Height='12' VerticalAlignment='Center' Margin='0,1,8,0'/><TextBlock Text='{Binding}' VerticalAlignment='Center'/></StackPanel><DataTemplate.Triggers><DataTrigger Binding='{Binding Tag, RelativeSource={RelativeSource AncestorType=Button}}' Value='True'><Setter TargetName='Arrow' Property='Data' Value='M2,1 L6,6 L2,11 M9,1 L13,6 L9,11'/></DataTrigger></DataTemplate.Triggers></DataTemplate>");}
        static bool Replace(DependencyObject parent,FrameworkElement child,FrameworkElement next){
            Grid.SetRow(next,Grid.GetRow(child));Grid.SetColumn(next,Grid.GetColumn(child));Grid.SetRowSpan(next,Grid.GetRowSpan(child));Grid.SetColumnSpan(next,Grid.GetColumnSpan(child));DockPanel.SetDock(next,DockPanel.GetDock(child));
            var panel=parent as Panel;if(panel!=null){int i=panel.Children.IndexOf(child);if(i<0)return false;panel.Children.RemoveAt(i);panel.Children.Insert(i,next);return true;}
            var border=parent as Decorator;if(border!=null&&border.Child==child){border.Child=null;border.Child=next;return true;}
            var content=parent as ContentControl;if(content!=null&&content.Content==child){content.Content=null;content.Content=next;return true;}return false;
        }
        static void Restore(DependencyObject target,DependencyProperty property,object value){if(value==DependencyProperty.UnsetValue)target.ClearValue(property);else target.SetValue(property,value);}
    }
}
