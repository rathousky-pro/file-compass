using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Data;
namespace FileCompass {
    internal sealed class OperationDialogLayout {
        internal readonly StackPanel Body = new StackPanel();
        internal readonly StackPanel Actions = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right };
        readonly DockPanel root;
        readonly ScrollViewer plain;
        readonly Window window;
        Grid pageHost;
        readonly List<ScrollViewer> pages=new List<ScrollViewer>();
        internal readonly ListBox Navigation=new ListBox { Background=CompactTheme.PanelSurface, BorderThickness=new Thickness(0), Padding=new Thickness(7) };
        internal OperationDialogLayout(Window window,string title,string summary=null) {
            this.window=window;root=new DockPanel();CopyUi.Surface(window,root);var outer=(Border)((Grid)window.Content).Children[0];outer.Padding=new Thickness(0);
            var heading=new StackPanel();heading.Children.Add(new TextBlock{Text=title,FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
            if(!String.IsNullOrWhiteSpace(summary))heading.Children.Add(new TextBlock{Text=summary,Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});
            var header=new Border{Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(20,16,20,16),CornerRadius=new CornerRadius(8,8,0,0),Child=heading};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer=new Border{Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(14,12,14,12),CornerRadius=new CornerRadius(0,0,8,8),Child=Actions};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            Body.Margin=new Thickness(20,18,20,18);
            plain=new ScrollViewer{Content=Body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=Math.Max(160,window.MaxHeight-190)};root.Children.Add(plain);
        }
        internal StackPanel AddPage(string title){
            if(pageHost==null){
                root.Children.Remove(plain);
                var workspace=new Grid {Height=Math.Min(390,Math.Max(160,window.MaxHeight-220))};workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(155)});workspace.ColumnDefinitions.Add(new ColumnDefinition());
                var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetBinding(TextBlock.TextProperty,new Binding { Converter=new LanguageValueConverter() });caption.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);Navigation.ItemTemplate=new DataTemplate{VisualTree=caption};
                var navigationFrame=new Border{Child=Navigation,Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(0,0,1,0)};workspace.Children.Add(navigationFrame);
                pageHost=new Grid();Grid.SetColumn(pageHost,1);workspace.Children.Add(pageHost);root.Children.Add(workspace);
                Navigation.SelectionChanged+=delegate{for(int i=0;i<pages.Count;i++)pages[i].Visibility=i==Navigation.SelectedIndex?Visibility.Visible:Visibility.Collapsed;};
            }
            var page=new StackPanel{Margin=new Thickness(18,14,18,14)};
            var scroll=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Visibility=Visibility.Collapsed};pages.Add(scroll);pageHost.Children.Add(scroll);Navigation.Items.Add(title);if(Navigation.SelectedIndex<0)Navigation.SelectedIndex=0;return page;
        }
        internal static StackPanel Group(Panel parent,string title){var content=new StackPanel();parent.Children.Add(new GroupBox{Header=title,Content=content,Margin=new Thickness(0,0,0,12),Padding=new Thickness(12)});return content;}
        internal static void Label(Panel parent,string label){parent.Children.Add(new TextBlock{Text=label,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,6),TextWrapping=TextWrapping.Wrap});}
        internal static Expander Advanced(Panel parent,string label,UIElement content){var expander=new Expander{Header=label,Content=content,Margin=new Thickness(0,14,0,0)};parent.Children.Add(expander);return expander;}
    }
}
