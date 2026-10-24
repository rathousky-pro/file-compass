using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace FileCompass {
    internal sealed class AboutWindow : Window {
        internal readonly Image Logo,DeveloperLogo;
        internal readonly Hyperlink Repository;
        internal readonly TextBlock VersionText,BuildText,DateText;
        internal AboutWindow(Window owner) {
            Owner=owner;Title=L.Text("app.aboutMenu");Width=520;SizeToContent=SizeToContent.Height;
            MaxHeight=Math.Max(360,SystemParameters.WorkArea.Height-40);
            ResizeMode=ResizeMode.NoResize;WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
            ShowInTaskbar=false;FluentTheme.Dialog(this);ApplicationBrand.Attach(this);
            var body=new StackPanel{Margin=new Thickness(28)};
            Content=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            Logo=new Image{Source=ApplicationBrand.Icon,Width=90,Height=90,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,12)};
            System.Windows.Automation.AutomationProperties.SetName(Logo,ApplicationBrand.Name);body.Children.Add(Logo);
            body.Children.Add(new TextBlock{Text=ApplicationBrand.Name,FontSize=28,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center});
            body.Children.Add(new TextBlock{Text=L.Text("app.aboutDescription"),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,8,0,14)});
            VersionText=new TextBlock{Text=L.Format("about.version",BuildInfo.DisplayVersion),TextAlignment=TextAlignment.Center,FontWeight=FontWeights.SemiBold};
            BuildText=new TextBlock{Text=L.Format("about.build",BuildInfo.Build),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,4,0,0)};
            DateText=new TextBlock{Text=L.Format("about.date",BuildInfo.BuiltAtUtc.ToLocalTime().ToString("d",CultureInfo.CurrentCulture)),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,4,0,18),ToolTip=BuildInfo.TimestampUtc};
            body.Children.Add(VersionText);body.Children.Add(BuildText);body.Children.Add(DateText);
            var details=new StackPanel{Margin=new Thickness(16)};
            details.Children.Add(new TextBlock{Text=L.Text("app.developerLabel"),Foreground=FluentTheme.Muted,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,0,0,10)});
            DeveloperLogo=new Image{Source=ApplicationBrand.DeveloperLogo,Height=78,MaxWidth=370,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center};
            System.Windows.Automation.AutomationProperties.SetName(DeveloperLogo,ApplicationBrand.Developer);
            details.Children.Add(new Border{Child=DeveloperLogo,Background=Brushes.White,Padding=new Thickness(7),CornerRadius=new CornerRadius(5)});
            details.Children.Add(new TextBlock{Text=ApplicationBrand.Developer,FontSize=15,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,9,0,12)});
            Repository=new Hyperlink(new Run(ApplicationBrand.GitHubUrl)){NavigateUri=new Uri(ApplicationBrand.GitHubUrl)};
            Repository.SetResourceReference(TextElement.ForegroundProperty,"CompassAccent");
            Repository.RequestNavigate+=delegate(object sender,System.Windows.Navigation.RequestNavigateEventArgs e){
                e.Handled=true;try{Process.Start(new ProcessStartInfo(ApplicationBrand.GitHubUrl){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message,L.Text("error.cannotOpenFile"));}
            };
            var link=new TextBlock{TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap};link.Inlines.Add(Repository);details.Children.Add(link);
            body.Children.Add(new Border{Child=details,Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});
            var status=new TextBlock{TextAlignment=TextAlignment.Center,Foreground=FluentTheme.Muted,Margin=new Thickness(0,8,0,0),MinHeight=16,TextWrapping=TextWrapping.Wrap};body.Children.Add(status);
            var buttons=new DockPanel{Margin=new Thickness(0,12,0,0)};body.Children.Add(buttons);
            var close=CopyUi.Button(L.Text("action.close"),delegate{Close();});close.IsCancel=true;close.IsDefault=true;close.MinWidth=90;DockPanel.SetDock(close,Dock.Right);buttons.Children.Add(close);
            var copy=CopyUi.Button(L.Text("about.copyInformation"),delegate{try{Clipboard.SetText(Diagnostics());status.Text=L.Text("about.copied");}catch{status.Text=L.Text("about.copyFailed");}});
            copy.HorizontalAlignment=HorizontalAlignment.Left;buttons.Children.Add(copy);
        }
        internal static string Diagnostics() {
            return ApplicationBrand.Name+" "+BuildInfo.DisplayVersion+Environment.NewLine+"Build: "+BuildInfo.Build+Environment.NewLine+"Built (UTC): "+BuildInfo.TimestampUtc+Environment.NewLine+Environment.OSVersion.VersionString+"; "+(Environment.Is64BitProcess?"x64":"x86")+Environment.NewLine+ApplicationBrand.Developer;
        }
    }
}

