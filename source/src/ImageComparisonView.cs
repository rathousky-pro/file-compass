using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
 internal sealed class ComparedImage {
  internal BitmapSource Source;internal int Width,Height;
  internal static ComparedImage Load(string path,CancellationToken token){
   token.ThrowIfCancellationRequested();
   if(new FileInfo(path).Length>128L*1024*1024)throw new IOException(L.Text("imageCompare.limit"));
   using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
    var decoder=BitmapDecoder.Create(input,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);int w=decoder.Frames[0].PixelWidth,h=decoder.Frames[0].PixelHeight;
    if((long)w*h>100000000)throw new IOException(L.Text("imageCompare.pixelLimit"));
    token.ThrowIfCancellationRequested();input.Position=0;
    var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=Math.Max(1,Math.Min(w,(int)(4096.0*w/Math.Max(w,h))));image.StreamSource=input;image.EndInit();image.Freeze();
    return new ComparedImage{Source=image,Width=w,Height=h};
   }
  }
 }
 internal sealed class ImageComparisonView:DockPanel {
  readonly Grid side=new Grid();readonly Grid overlay=new Grid();readonly Grid canvas=new Grid{Background=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
  readonly Image first=new Image(),second=new Image(),bottom=new Image{Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top},top=new Image{Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
  readonly Slider wipe=new Slider{Minimum=0,Maximum=1,Value=.5,Width=260,Margin=new Thickness(10,0,10,0)};
  readonly ComboBox mode=new ComboBox{Width=200,SelectedIndex=0};
  readonly TextBlock information=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,6,4,8)};
  double commonWidth,commonHeight;
  internal ImageComparisonView() {
   var tools=new WrapPanel{Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(tools,Dock.Top);Children.Add(tools);
   mode.Items.Add(L.Text("imageCompare.side"));mode.Items.Add(L.Text("imageCompare.overlay"));tools.Children.Add(mode);tools.Children.Add(wipe);
   tools.Children.Add(new TextBlock{Text=L.Text("imageCompare.wipeHelp"),VerticalAlignment=VerticalAlignment.Center});
   DockPanel.SetDock(information,Dock.Top);Children.Add(information);
   var host=new Grid();Children.Add(host);host.Children.Add(side);host.Children.Add(overlay);
   side.ColumnDefinitions.Add(new ColumnDefinition());side.ColumnDefinitions.Add(new ColumnDefinition());
   first.Stretch=second.Stretch=Stretch.Uniform;
   side.Children.Add(new Border{Child=first,Background=Brushes.White,Margin=new Thickness(0,0,5,0)});
   var other=new Border{Child=second,Background=Brushes.White,Margin=new Thickness(5,0,0,0)};Grid.SetColumn(other,1);side.Children.Add(other);
   canvas.Children.Add(bottom);canvas.Children.Add(top);overlay.Children.Add(new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
   mode.SelectionChanged+=delegate{side.Visibility=mode.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;overlay.Visibility=mode.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;wipe.IsEnabled=mode.SelectedIndex==1;};
   wipe.ValueChanged+=delegate{UpdateClip();};overlay.Visibility=Visibility.Collapsed;wipe.IsEnabled=false;
  }
  void UpdateClip(){if(commonWidth>0)top.Clip=new RectangleGeometry(new Rect(0,0,commonWidth*wipe.Value,commonHeight));}
  internal async Task Load(string left,string right,CancellationToken token) {
   var pair=await Task.Run(()=>new[]{ComparedImage.Load(left,token),ComparedImage.Load(right,token)},token);token.ThrowIfCancellationRequested();
   first.Source=bottom.Source=pair[0].Source;second.Source=top.Source=pair[1].Source;
   bottom.Width=pair[0].Width;bottom.Height=pair[0].Height;top.Width=pair[1].Width;top.Height=pair[1].Height;
   canvas.Width=commonWidth=Math.Max(pair[0].Width,pair[1].Width);canvas.Height=commonHeight=Math.Max(pair[0].Height,pair[1].Height);UpdateClip();
   information.Text=Path.GetFileName(left)+" · "+pair[0].Width+" × "+pair[0].Height+" px     |     "+Path.GetFileName(right)+" · "+pair[1].Width+" × "+pair[1].Height+" px";
  }
 }
}
