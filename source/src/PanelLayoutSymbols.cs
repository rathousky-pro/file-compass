using System;using System.Collections.Generic;using System.Windows;using System.Windows.Media;using System.Windows.Media.Imaging;
namespace FileCompass {
 internal static class PanelLayoutSymbols {
  static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
  internal static ImageSource Get(string name,ToolbarTheme theme){string key=name+theme;ImageSource found;if(cache.TryGetValue(key,out found))return found;bool outline=theme==ToolbarTheme.Outline;var ink=CompactTheme.ColorBrush("#234B70");var drawing=new DrawingGroup();using(var dc=drawing.Open()){
   dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));
   Action<Rect,bool> panel=delegate(Rect rect,bool teal){Brush fill=outline?null:theme==ToolbarTheme.Classic?(Brush)new LinearGradientBrush((Color)ColorConverter.ConvertFromString(teal?"#48D9C3":"#5DC5FF"),(Color)ColorConverter.ConvertFromString(teal?"#008B7B":"#1569CD"),90):CompactTheme.ColorBrush(teal?"#189F8A":"#287ED1");var pen=new Pen(outline?ink:CompactTheme.ColorBrush(teal?"#008574":"#145EA9"),outline?1.8:1.2);dc.DrawRoundedRectangle(fill,pen,rect,1.4,1.4);var line=new Pen(outline?ink:Brushes.White,1.3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};double left=rect.Left+2.8,right=rect.Right-2.8;dc.DrawLine(line,new Point(left,rect.Top+3.7),new Point(right,rect.Top+3.7));if(rect.Height>14){dc.DrawLine(line,new Point(left,rect.Top+9),new Point(right,rect.Top+9));dc.DrawLine(line,new Point(left,rect.Top+14),new Point(right,rect.Top+14));}};
   if(name=="panels-SideBySide"){panel(new Rect(2,3,11,26),false);panel(new Rect(19,3,11,26),true);}
   else if(name=="panels-Stacked"){panel(new Rect(2,3,28,10),false);panel(new Rect(2,19,28,10),true);}
   else panel(new Rect(4,3,24,26),false);
  }drawing.Freeze();ImageSource result;if(theme==ToolbarTheme.Classic){var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new ScaleTransform(8,8));dc.DrawDrawing(drawing);dc.Pop();}var bitmap=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();result=bitmap;}else{var image=new DrawingImage(drawing);image.Freeze();result=image;}cache[key]=result;return result;}
 }
}
