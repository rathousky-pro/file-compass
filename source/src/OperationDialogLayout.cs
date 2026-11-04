using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
namespace FileCompass {
    internal sealed class OperationDialogLayout {
        internal readonly StackPanel Body = new StackPanel();
        internal readonly StackPanel Actions = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
        readonly DockPanel root;
        readonly ScrollViewer plain;
        readonly Border plainCard;
        readonly Window window;
        Grid pageHost;
        Grid workspace;
        Border navigationFrame;
        StackPanel navigationActions;
        internal void AddNavigationAction(string label,Action action){
            if(navigationFrame==null)return;
            if(navigationActions==null){navigationFrame.Child=null;var dock=new DockPanel();navigationActions=new StackPanel{Margin=new Thickness(8,18,8,8)};DockPanel.SetDock(navigationActions,Dock.Bottom);dock.Children.Add(navigationActions);dock.Children.Add(Navigation);navigationFrame.Child=dock;}
            var button=CopyUi.Button(label,action);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,3,0,3);navigationActions.Children.Add(button);
        }
        bool fitQueued,compact;
        readonly List<ScrollViewer> pages=new List<ScrollViewer>();
        internal readonly ListBox Navigation=new ListBox { Background=CompactTheme.PanelSurface, BorderThickness=new Thickness(0), Padding=new Thickness(7) };
        internal OperationDialogLayout(Window window,string title,string summary=null) {
            this.window=window;window.Loaded+=delegate{QueueFit();};window.SizeChanged+=delegate(object sender,SizeChangedEventArgs args){if(args.WidthChanged)QueueFit();};window.IsVisibleChanged+=delegate{if(window.IsVisible)QueueFit();};root=new DockPanel();CopyUi.Surface(window,root);var outer=(Border)((Grid)window.Content).Children[0];outer.Padding=new Thickness(0);
            var heading=new StackPanel();heading.Children.Add(new TextBlock{Text=title,FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
            if(!String.IsNullOrWhiteSpace(summary))heading.Children.Add(new TextBlock{Text=summary,Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});
            var headerRow=new DockPanel();headerRow.Children.Add(SettingsUx.HeaderIcon(title));headerRow.Children.Add(heading);
            var header=new Border{Tag="dialog-header",Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(20,16,20,16),CornerRadius=new CornerRadius(8,8,0,0),Child=headerRow};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new Border{Tag="dialog-footer",Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(14,12,14,12),CornerRadius=new CornerRadius(0,0,8,8),Child=Actions};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            Body.Margin=new Thickness(20,18,20,18);
            plain=new ScrollViewer{Content=Body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=Math.Max(160,window.MaxHeight-190)};plainCard=new Border{Tag="dialog-content-card",Background=CompactTheme.WhiteSurface,CornerRadius=new CornerRadius(11),Child=plain};root.Children.Add(plainCard);
        }
        internal StackPanel AddPage(string title){
            if(pageHost==null){
                root.Children.Remove(plainCard);
                workspace=new Grid {Tag="dialog-workspace",Margin=new Thickness(0),Height=Math.Min(390,Math.Max(160,window.MaxHeight-220))};workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});workspace.ColumnDefinitions.Add(new ColumnDefinition());
                Navigation.ItemTemplate=NavigationIcons.Template();
                navigationFrame=new Border{Tag="dialog-navigation",Child=Navigation,Background=CompactTheme.ToolbarSurface,CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,8,0)};workspace.Children.Add(navigationFrame);
                pageHost=new Grid();var pageCard=new Border{Tag="operation-page-card",Child=pageHost,Background=CompactTheme.WhiteSurface,CornerRadius=new CornerRadius(11),Margin=new Thickness(0)};Grid.SetColumn(pageCard,1);workspace.Children.Add(pageCard);root.Children.Add(workspace);
                Navigation.SelectionChanged+=delegate{for(int i=0;i<pages.Count;i++)pages[i].Visibility=i==Navigation.SelectedIndex?Visibility.Visible:Visibility.Collapsed;};
                CleanOptions.Register(window, Actions, delegate(bool visible) { navigationFrame.Visibility=visible?Visibility.Visible:Visibility.Collapsed;Navigation.ItemContainerStyle=AppearanceTheme.IsClean?NavigationIcons.ItemStyle():null;workspace.ColumnDefinitions[0].Width=new GridLength(visible?210:0);navigationFrame.Background=Navigation.Background=CompactTheme.ToolbarSurface;navigationFrame.VerticalAlignment=VerticalAlignment.Stretch;navigationFrame.Margin=new Thickness(0,0,8,0);navigationFrame.CornerRadius=new CornerRadius(10);if(!visible)Navigation.SelectedIndex=0;QueueFit(); }, delegate { return Navigation.Items.Count>1; });
            }
            var page=new StackPanel{Margin=new Thickness(30,30,30,24)};
            var scroll=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Visibility=Visibility.Collapsed};pages.Add(scroll);pageHost.Children.Add(scroll);Navigation.Items.Add(title);if(Navigation.SelectedIndex<0)Navigation.SelectedIndex=0;return page;
        }
        internal void SelectPage(int index){CleanOptions.Reveal(window);Navigation.SelectedIndex=index;}
        internal void Compact(double height){compact=true;if(workspace!=null)workspace.Height=height;}
        internal void Refit(){QueueFit();}
        void QueueFit(){
            if(fitQueued||compact||!window.IsLoaded)return;fitQueued=true;
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(delegate{fitQueued=false;FitPages();}));
        }
        void FitPages(){
            if(workspace==null||compact||!window.IsLoaded||pageHost.ActualWidth<=0)return;
            double required=0;
            foreach(var scroll in pages){var page=scroll.Content as FrameworkElement;if(page==null)continue;page.Measure(new Size(pageHost.ActualWidth,Double.PositiveInfinity));required=Math.Max(required,page.DesiredSize.Height);}
            double chrome=Math.Max(0,window.ActualHeight-((FrameworkElement)window.Content).ActualHeight);
            double fixedHeight=root.Children.OfType<FrameworkElement>().Where(x=>x!=workspace).Sum(x=>x.ActualHeight+x.Margin.Top+x.Margin.Bottom);
            double maximum=Math.Max(140,window.MaxHeight-chrome-fixedHeight-8);
            double target=Math.Min(maximum,Math.Max(230,Math.Ceiling(required)+2));
            if(Math.Abs(workspace.Height-target)>.5)workspace.Height=target;
        }
        internal static StackPanel Group(Panel parent,string title){var content=new StackPanel();parent.Children.Add(new GroupBox{Header=title,Content=content,Margin=new Thickness(0,0,0,12),Padding=new Thickness(12)});return content;}
        internal static void Label(Panel parent,string label){parent.Children.Add(new TextBlock{Text=label,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,6),TextWrapping=TextWrapping.Wrap});}
        internal static Expander Advanced(Panel parent,string label,UIElement content){var expander=new Expander{Header=label,Content=content,Margin=new Thickness(0,14,0,0)};parent.Children.Add(expander);return expander;}
    }
}
