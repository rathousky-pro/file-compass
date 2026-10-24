using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class QueuePresentation {
        internal static TextBlock Heading(string text) { return new TextBlock { Text=text, FontSize=17, FontWeight=FontWeights.SemiBold, Margin=new Thickness(2,2,2,10) }; }
        internal static StackPanel Empty(string title, string description) {
            var panel=new StackPanel { Margin=new Thickness(20,18,20,22), HorizontalAlignment=HorizontalAlignment.Center };
            var icon=new Border { Width=48, Height=48, CornerRadius=new CornerRadius(24), Background=CompactTheme.ThemeBrush("#E4F0FF"), HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,0,0,14), Child=new TextBlock { Text="✓", FontSize=24, Foreground=FluentTheme.Accent, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center } };
            panel.Children.Add(icon);
            var heading=Heading(title); heading.TextAlignment=TextAlignment.Center;panel.Children.Add(heading);
            panel.Children.Add(new TextBlock { Text=description, Foreground=FluentTheme.Muted, TextAlignment=TextAlignment.Center, TextWrapping=TextWrapping.Wrap, LineHeight=21 });return panel;
        }
        internal static WrapPanel Actions() { return new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,10,0,0) }; }
    }
}
