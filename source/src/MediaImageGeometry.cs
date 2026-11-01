using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 internal sealed partial class MediaConverterWindow {
  internal readonly TabItem GeometryTab=new TabItem();
  string pixelAmount="1920",percentAmount="50";int previousResize;
  void InitializeImageGeometry(){var body=new StackPanel();body.Children.Add(Form("imageTools.resize",resize,sizeLabel,width,"imageBatch.height",height,"imageTools.rotate",rotation));body.Children.Add(new TextBlock{Text=L.Text("imageBatch.resizeHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,4,0,6)});GeometryTab.Header=L.Text("media.imageGeometry");GeometryTab.Content=body;SettingsTabs.Items.Add(GeometryTab);resize.SelectionChanged+=delegate{int selected=resize.SelectedIndex;if(previousResize==2&&selected!=2){percentAmount=width.Text;width.Text=pixelAmount;}else if(previousResize!=2&&selected==2){pixelAmount=width.Text;width.Text=percentAmount;}previousResize=selected;};}
  void UpdateImageGeometry(bool image){GeometryTab.Visibility=image?Visibility.Visible:Visibility.Collapsed;if(!image&&SettingsTabs.SelectedItem==GeometryTab)SettingsTabs.SelectedIndex=0;}
 }
}
