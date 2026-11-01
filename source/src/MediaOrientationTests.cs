#if APP_TESTS
using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
 internal static class MediaOrientationTests {
  internal static void Verify(string root,string landscape,Action<bool,string> check){
   string portrait=Path.Combine(root,"orientation-portrait.png");
   MediaImages.Call(landscape,portrait,new MediaImageOptions{Rotation=90},false,CancellationToken.None);
   foreach(int orientation in new[]{1,2})foreach(string source in new[]{landscape,portrait}){
    string target=Path.Combine(root,"oriented-"+orientation+"-"+Path.GetFileName(source));
    var result=MediaImages.Call(source,target,new MediaImageOptions{KeepFormat=true,Orientation=orientation,Resize=1,Width=12},false,CancellationToken.None);
    check(result.Width==(orientation==1?12:6)&&result.Height==(orientation==1?6:12),"mixed orientations use the actual longest edge after automatic rotation: "+orientation+" "+Path.GetFileName(source));
   }
   foreach(int orientation in new[]{1,2}){
    var result=MediaImages.Call(landscape,Path.Combine(root,"exact-orientation-"+orientation+".png"),new MediaImageOptions{Orientation=orientation,Resize=3,Width=18,Height=10},false,CancellationToken.None);
    check(result.Width==(orientation==1?18:10)&&result.Height==(orientation==1?10:18),"exact dimensions follow forced orientation: "+orientation);
   }
   check(!MediaImageOrientation.ShouldRotate(20,20,1)&&!MediaImageOrientation.ShouldRotate(20,20,2),"square images do not receive an unnecessary rotation");
   var bitmap=BitmapSource.Create(2400,1200,96,96,PixelFormats.Gray8,null,new byte[2400*1200],2400);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));string large=Path.Combine(root,"large-landscape.png");using(var file=File.Create(large))encoder.Save(file);
   var resized=MediaImages.Call(large,Path.Combine(root,"portrait-1920.png"),new MediaImageOptions{Orientation=2,Resize=1,Width=1920},false,CancellationToken.None);
   check(resized.Width==960&&resized.Height==1920,"1920 px longest-edge limit becomes height when all images are portrait");
  }
 }
}
#endif
