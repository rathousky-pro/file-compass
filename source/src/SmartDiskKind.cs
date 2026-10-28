using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace FileCompass {
    internal sealed partial class SmartDisk {
        internal string KindCode{get{
            string protocol=SmartData.Text(SmartData.Get(Data,"device","protocol"),"").ToLowerInvariant(),type=(Type??"").Split(',')[0].ToLowerInvariant();
            if(protocol=="nvme"||type=="nvme")return "nvme";
            decimal? rpm=SmartData.Number(SmartData.Get(Data,"rotation_rate"));
            if(rpm>1024&&rpm<65535)return "hdd";
            // smartctl's SCSI path can emit zero for both unreported rotation and solid-state media.
            if(rpm==0&&(protocol=="ata"||(protocol.Length==0&&(type=="ata"||type=="sat"))))return "ssd";
            if(protocol=="usb"||type.StartsWith("usb",StringComparison.Ordinal))return "usb";
            if(protocol=="scsi"||type=="scsi")return "scsi";
            return "unknown";
        }}
        public string KindLabel{get{return KindCode=="unknown"?L.Text("smart.kindUnknown"):KindCode=="nvme"?"NVMe":KindCode.ToUpperInvariant();}}
        public string KindHint{get{return KindCode=="hdd"?L.Format("smart.kind.hdd",SmartData.Number(SmartData.Get(Data,"rotation_rate")).Value.ToString("N0")):L.Text("smart.kind."+KindCode);}}
        public Brush KindBrush{get{return SmartDiskKind.Brush(KindCode);}}
        public ImageSource KindIcon{get{return SmartDiskKind.Icon(KindCode);}}
    }
    internal static class SmartDiskKind {
        static readonly Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>();
        internal static Brush Brush(string kind){return CompactTheme.ThemeBrush(kind=="hdd"?"#216EA8":kind=="ssd"?"#167C78":kind=="nvme"?"#6553B0":"#657080");}
        internal static ImageSource Icon(string kind){
            ImageSource image;if(icons.TryGetValue(kind,out image))return image;
            var drawing=new DrawingGroup();using(var dc=drawing.Open()){
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,22));var pen=new Pen(Brush(kind),1.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                if(kind=="nvme"){
                    dc.DrawRoundedRectangle(null,pen,new Rect(1,5,30,12),2,2);dc.DrawRectangle(null,pen,new Rect(7,8,7,6));dc.DrawRectangle(null,pen,new Rect(18,8,5,6));dc.DrawEllipse(null,pen,new Point(3.5,11),1,1);dc.DrawLine(pen,new Point(27,6),new Point(27,16));
                }else if(kind=="usb"){
                    dc.DrawRoundedRectangle(null,pen,new Rect(6,7,20,14),3,3);dc.DrawRectangle(null,pen,new Rect(10,1,12,6));dc.DrawLine(pen,new Point(13,2),new Point(13,5));dc.DrawLine(pen,new Point(19,2),new Point(19,5));
                }else{
                    dc.DrawRoundedRectangle(null,pen,new Rect(3,1,26,20),2,2);
                    if(kind=="hdd"){dc.DrawEllipse(null,pen,new Point(15,11),7,7);dc.DrawEllipse(null,pen,new Point(15,11),1.4,1.4);dc.DrawLine(pen,new Point(25,4),new Point(17,13));}
                    else if(kind=="ssd"){dc.DrawRectangle(null,pen,new Rect(10,6,12,10));foreach(int y in new[]{7,11,15}){dc.DrawLine(pen,new Point(6,y),new Point(10,y));dc.DrawLine(pen,new Point(22,y),new Point(26,y));}}
                    else{dc.DrawLine(pen,new Point(7,7),new Point(25,7));dc.DrawLine(pen,new Point(7,14),new Point(20,14));dc.DrawEllipse(null,pen,new Point(24,17),1,1);}
                }
            }
            // Theme brushes remain mutable so the icon follows light/dark changes with its label.
            image=new DrawingImage(drawing);icons[kind]=image;return image;
        }
        internal static FrameworkElementFactory Badge(bool compact=false){
            var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));frame.SetValue(Border.BorderThicknessProperty,new Thickness(1));frame.SetBinding(Border.BorderBrushProperty,new Binding("KindBrush"));frame.SetValue(Border.BackgroundProperty,CompactTheme.PanelSurface);frame.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);frame.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,0,5));frame.SetValue(Border.PaddingProperty,new Thickness(6,2,6,2));frame.SetBinding(FrameworkElement.ToolTipProperty,new Binding("KindHint"));
            var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);frame.AppendChild(row);
            var image=new FrameworkElementFactory(typeof(Image));image.SetBinding(Image.SourceProperty,new Binding("KindIcon"));image.SetValue(FrameworkElement.WidthProperty,24.0);image.SetValue(FrameworkElement.HeightProperty,16.0);row.AppendChild(image);
            var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding("KindLabel"));text.SetBinding(TextBlock.ForegroundProperty,new Binding("KindBrush"));text.SetValue(TextBlock.FontSizeProperty,11.0);text.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);text.SetValue(FrameworkElement.MarginProperty,new Thickness(5,0,0,0));text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(text);
            if(compact){frame.SetValue(FrameworkElement.MarginProperty,new Thickness(6,0,0,0));frame.SetValue(Border.PaddingProperty,new Thickness(4,1,4,1));image.SetValue(FrameworkElement.WidthProperty,16.0);text.SetValue(TextBlock.FontSizeProperty,10.0);text.SetValue(FrameworkElement.MarginProperty,new Thickness(3,0,0,0));}
            return frame;
        }
    }
}
