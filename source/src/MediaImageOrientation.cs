using System;
namespace FileCompass {
 internal static class MediaImageOrientation {
  // Evaluate the displayed dimensions after EXIF orientation, before resizing.
  internal static bool ShouldRotate(int width,int height,int orientation){return orientation==1&&height>width||orientation==2&&width>height;}
  internal static int[] Dimensions(int width,int height,MediaImageOptions options){
   int w=options.Width,h=options.Height;
   // Exact output dimensions follow the chosen orientation too.
   if(options.Resize==3&&ShouldRotate(w,h,options.Orientation)){int swap=w;w=h;h=swap;}
   return ImageTools.Dimensions(width,height,new ImageToolOptions{ResizeMode=options.Resize,Width=w,Height=h,MaxEdge=options.Width,Percent=options.Percent});
  }
 }
}
