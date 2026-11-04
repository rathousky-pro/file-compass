using System;using System.Diagnostics;using System.Globalization;using System.Windows;using System.Windows.Controls;using System.Windows.Documents;using System.Windows.Media;
namespace FileCompass {
 internal sealed class AboutWindow : Window {
  internal readonly Image Logo,DeveloperLogo;
  internal readonly Hyperlink Repository;
  internal readonly TextBlock VersionText,BuildText,DateText;
  internal AboutWindow(Window owner){
   CopyUi.Style(this,owner,L.Text("app.aboutMenu"),640);ApplicationBrand.Attach(this);
   StackPanel actions;var root=SettingsUx.Frame(this,L.Text("app.aboutMenu"),out actions);
   var body=new StackPanel{Margin=new Thickness(24,20,24,10)};root.Children.Add(body);
   var identity=new DockPanel{Margin=new Thickness(0,0,0,20)};
   Logo=new Image{Source=ApplicationBrand.Icon,Width=64,Height=64,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,18,0)};System.Windows.Automation.AutomationProperties.SetName(Logo,ApplicationBrand.Name);DockPanel.SetDock(Logo,Dock.Left);identity.Children.Add(Logo);
   var product=new StackPanel{VerticalAlignment=VerticalAlignment.Center};product.Children.Add(new TextBlock{Text=ApplicationBrand.Name,FontSize=26,FontWeight=FontWeights.SemiBold});product.Children.Add(new TextBlock{Text=L.Text("app.aboutDescription"),Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0)});identity.Children.Add(product);body.Children.Add(identity);
   VersionText=new TextBlock{Text=L.Format("about.version",BuildInfo.DisplayVersion),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,6)};
   BuildText=new TextBlock{Text=L.Format("about.build",BuildInfo.Build),Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,6)};
   DateText=new TextBlock{Text=L.Format("about.date",BuildInfo.BuiltAtUtc.ToLocalTime().ToString("d",CultureInfo.CurrentCulture)),Foreground=FluentTheme.Muted,ToolTip=BuildInfo.TimestampUtc};
   body.Children.Add(VersionText);body.Children.Add(BuildText);body.Children.Add(DateText);body.Children.Add(new Border{Height=1,Background=CompactTheme.Line,Margin=new Thickness(0,18,0,16)});
   body.Children.Add(new TextBlock{Text="MIT License | Copyright © 2026 Jan Rathouský",Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,12),TextWrapping=TextWrapping.Wrap});
   var developer=new Grid();developer.ColumnDefinitions.Add(new ColumnDefinition());developer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});DeveloperLogo=new Image{Source=ApplicationBrand.DeveloperLogo,Width=196,Height=46,Stretch=Stretch.Uniform};System.Windows.Automation.AutomationProperties.SetName(DeveloperLogo,ApplicationBrand.Developer);
   DeveloperLogo.HorizontalAlignment=HorizontalAlignment.Left;DeveloperLogo.Margin=new Thickness(0,0,32,0);developer.Children.Add(DeveloperLogo);
   var links=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(links,1);links.Children.Add(new TextBlock{Text=L.Text("app.developerLabel"),Foreground=FluentTheme.Muted,TextAlignment=TextAlignment.Right});links.Children.Add(new TextBlock{Text=ApplicationBrand.Developer,TextAlignment=TextAlignment.Right,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,3,0,3)});
   Repository=new Hyperlink(new Run("GitHub · File Compass")){NavigateUri=new Uri(ApplicationBrand.GitHubUrl),ToolTip=ApplicationBrand.GitHubUrl};Repository.SetResourceReference(TextElement.ForegroundProperty,"CompassAccent");Repository.RequestNavigate+=delegate(object sender,System.Windows.Navigation.RequestNavigateEventArgs e){e.Handled=true;try{Process.Start(new ProcessStartInfo(ApplicationBrand.GitHubUrl){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,ex.Message,L.Text("error.cannotOpenFile"));}};
   var link=new TextBlock{TextAlignment=TextAlignment.Right};link.Inlines.Add(Repository);links.Children.Add(link);developer.Children.Add(links);body.Children.Add(developer);
   var status=new TextBlock{Foreground=FluentTheme.Muted,Margin=new Thickness(0,10,0,0),MinHeight=18};body.Children.Add(status);
   actions.Children.Add(CopyUi.Button(L.Text("about.copyInformation"),delegate{try{Clipboard.SetText(Diagnostics());status.Text=L.Text("about.copied");}catch{status.Text=L.Text("about.copyFailed");}}));
   var close=CopyUi.Button(L.Text("action.close"),Close);close.IsCancel=true;close.IsDefault=true;FluentTheme.Primary(close);actions.Children.Add(close);
  }
  internal static string Diagnostics(){return ApplicationBrand.Name+" "+BuildInfo.DisplayVersion+Environment.NewLine+"Build: "+BuildInfo.Build+Environment.NewLine+"Built (UTC): "+BuildInfo.TimestampUtc+Environment.NewLine+Environment.OSVersion.VersionString+"; "+(Environment.Is64BitProcess?"x64":"x86")+Environment.NewLine+ApplicationBrand.Developer;}
 }
}
