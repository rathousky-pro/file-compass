using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
namespace FileCompass {
    // Common hierarchy for local copies and archive operations in both interface motifs.
    internal sealed class TransferProgressLayout {
        internal readonly TextBlock Percent = Text("0 %"), Remaining = Text(""), Totals = Text(""), Count = Text(""), FilePercent = Text("—");
        internal readonly PathTextBlock CurrentFile = new PathTextBlock();
        internal readonly LocalCopyProgressBar Overall = new LocalCopyProgressBar(true) { Height = 8 }, File = new LocalCopyProgressBar(false) { Height = 3 };
        internal readonly Expander Details = new Expander { Header = L.Text("transfer.progressDetails"), IsExpanded = false, Margin = new Thickness(0,16,0,0) };
        internal readonly StackPanel Supplement = new StackPanel();
        internal readonly StackPanel DetailBody = new StackPanel { Margin = new Thickness(0,10,0,0) };
        internal readonly StackPanel Actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        internal readonly TextBlock Heading;
        internal readonly PathTextBlock Route = new PathTextBlock { Visibility=Visibility.Collapsed, Margin=new Thickness(0,5,0,0), Foreground=FluentTheme.Muted };
        internal TransferProgressLayout(Window window, string title) {
            Details.Template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Expander'>
              <StackPanel><ToggleButton Content='{TemplateBinding Header}' IsChecked='{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}' HorizontalContentAlignment='Left'>
              <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent' Padding='0,8'><StackPanel Orientation='Horizontal'><TextBlock x:Name='Arrow' Text='▸' Foreground='{StaticResource CompassMuted}' Margin='0,0,8,0'/><ContentPresenter ContentSource='Content' TextElement.Foreground='{StaticResource CompassMuted}'/></StackPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='Text' Value='▾'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton>
              <ContentPresenter x:Name='DetailsContent' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' ContentSource='Content' Visibility='Collapsed'/></StackPanel>
              <ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='DetailsContent' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            var root = new DockPanel(); CopyUi.Surface(window,root);
            var outer = (Border)((Grid)window.Content).Children[0]; outer.Padding = new Thickness(0);
            Heading = new TextBlock { Text=title, FontSize=20, FontWeight=FontWeights.SemiBold, TextWrapping=TextWrapping.Wrap };
            var headingBody=new StackPanel();headingBody.Children.Add(Heading);headingBody.Children.Add(Route);
            var header = new Border { Background=CompactTheme.PanelSurface, BorderBrush=CompactTheme.Line, BorderThickness=new Thickness(0,0,0,1), Padding=new Thickness(20,16,20,16), CornerRadius=new CornerRadius(8,8,0,0), Child=headingBody };
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            var footer = new Border { Background=CompactTheme.PanelSurface, BorderBrush=CompactTheme.Line, BorderThickness=new Thickness(0,1,0,0), Padding=new Thickness(14,12,14,12), CornerRadius=new CornerRadius(0,0,8,8), Child=Actions };
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var body = new StackPanel { Margin=new Thickness(20,18,20,18) };root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=System.Math.Max(160,window.MaxHeight-190)});
            var top=new DockPanel{Margin=new Thickness(0,0,0,14)};Remaining.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(Remaining,Dock.Right);top.Children.Add(Remaining);
            Percent.FontSize=32;Percent.FontWeight=FontWeights.SemiBold;Percent.Foreground=FluentTheme.Text;top.Children.Add(Percent);body.Children.Add(top);body.Children.Add(Overall);
            var counts=new DockPanel{Margin=new Thickness(0,10,0,22)};Count.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(Count,Dock.Right);counts.Children.Add(Count);counts.Children.Add(Totals);body.Children.Add(counts);
            var current=new DockPanel{Margin=new Thickness(0,0,0,7)};FilePercent.Width=58;FilePercent.TextAlignment=TextAlignment.Right;DockPanel.SetDock(FilePercent,Dock.Right);current.Children.Add(FilePercent);current.Children.Add(CurrentFile);body.Children.Add(current);body.Children.Add(File);
            Details.Content=DetailBody;body.Children.Add(Details);body.Children.Add(Supplement);
            // Expanding is user-driven. Metrics updates never resize an already expanded path field.
            Details.Expanded += delegate { window.UpdateLayout(); };Details.Collapsed += delegate { window.UpdateLayout(); };
        }
        static TextBlock Text(string value){return new TextBlock{Text=value,Foreground=FluentTheme.Muted,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};}
        internal void Current(string path,double? fraction){
            string display=path??"";int split=System.Math.Max(display.LastIndexOf('/'),display.LastIndexOf('\\'));
            CurrentFile.SetPath("",split<0?display:display.Substring(split+1));
            CurrentFile.ToolTip=path;File.Preparing=!fraction.HasValue;File.Update(fraction??0);FilePercent.Text=fraction.HasValue?(System.Math.Max(0,System.Math.Min(1,fraction.Value))*100).ToString("F0")+" %":"—";
        }
        internal void ShowProgress(bool known){
            Overall.Visibility=File.Visibility=Percent.Visibility=FilePercent.Visibility=known?Visibility.Visible:Visibility.Collapsed;
            var summaryRow=Totals.Parent as FrameworkElement;if(summaryRow!=null)summaryRow.Visibility=known?Visibility.Visible:Visibility.Collapsed;
            Remaining.HorizontalAlignment=known?HorizontalAlignment.Right:HorizontalAlignment.Left;
            DockPanel.SetDock(Remaining,known?Dock.Right:Dock.Left);
        }
        internal static string ParentPath(string path){path=path??"";int split=System.Math.Max(path.LastIndexOf('/'),path.LastIndexOf('\\'));return split<0?path:path.Substring(0,split);}
    }
}
