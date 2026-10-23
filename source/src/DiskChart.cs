using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace CommanderCZ {
    internal sealed class DiskChart : FrameworkElement {
        internal List<DiskChartRow> Rows=new List<DiskChartRow>();internal bool Pie=true;internal string CenterText="",Subtitle="";
        internal event Action<DiskChartRow> SliceClicked;
        readonly List<Tuple<DiskChartRow,Geometry>> hits=new List<Tuple<DiskChartRow,Geometry>>();DiskChartRow hovered;
        internal static readonly string[] Colors={"#2979D8","#008B78","#D58A0D","#8756C1","#C34F71","#278FA8","#71852E","#A76035","#5164B3","#8D8999"};
        internal static int ColorIndex(string key){uint hash=2166136261;foreach(char c in key.ToLowerInvariant())hash=(hash^c)*16777619;return (int)(hash%(Colors.Length-1));}
        internal static Brush ColorFor(string key){return CompactTheme.ColorBrush(Colors[ColorIndex(key)]);}
        protected override void OnRender(DrawingContext dc){
            base.OnRender(dc);hits.Clear();var rows=Rows.Where(x=>x.Value>0).ToArray();double total=rows.Sum(x=>x.Value);if(total<=0){Text(dc,L.Text("analyzer.empty"),new Point(12,20),FluentTheme.Muted,ActualWidth-24,12,false);return;}
            if(Pie){
                double radius=Math.Max(1,Math.Min(ActualWidth,ActualHeight)/2-12),inner=radius*.55;var center=new Point(ActualWidth/2,ActualHeight/2);double angle=-Math.PI/2;
                foreach(var row in rows){double sweep=row.Value/total*Math.PI*2,gap=rows.Length==1?0:Math.Min(.012,sweep*.12);Geometry outer;
                    if(rows.Length==1)outer=new EllipseGeometry(center,radius,radius);
                    else{var wedge=new StreamGeometry();using(var ctx=wedge.Open()){ctx.BeginFigure(center,true,true);ctx.LineTo(Polar(center,radius,angle+gap/2),true,false);ctx.ArcTo(Polar(center,radius,angle+sweep-gap/2),new Size(radius,radius),0,sweep-gap>Math.PI,SweepDirection.Clockwise,true,false);}outer=wedge;}
                    var ring=new CombinedGeometry(GeometryCombineMode.Exclude,outer,new EllipseGeometry(center,inner,inner));ring.Freeze();dc.DrawGeometry(row.Color,hovered!=null&&row.Key==hovered.Key?new Pen(FluentTheme.Text,2):null,ring);hits.Add(Tuple.Create(row,(Geometry)ring));
                    if(row.Value/total>=.065&&radius>=65){var point=Polar(center,radius*.78,angle+sweep/2);Text(dc,row.Share,new Point(point.X-27,point.Y-8),Brushes.White,54,11,true);}angle+=sweep;
                }
                Text(dc,CenterText,new Point(center.X-inner+5,center.Y-18),FluentTheme.Text,Math.Max(1,inner*2-10),Math.Min(19,Math.Max(12,inner/3)),true);
                Text(dc,Subtitle,new Point(center.X-inner+5,center.Y+8),FluentTheme.Muted,Math.Max(1,inner*2-10),11,true);
            }else{
                double maximum=rows.Max(x=>x.Value),height=Math.Min(34,Math.Max(12,(ActualHeight-10)/rows.Length)),left=Math.Min(165,ActualWidth*.34);
                for(int i=0;i<rows.Length;i++){var row=rows[i];double y=5+i*height;Text(dc,row.Name,new Point(4,y+3),FluentTheme.Text,left-10,12,false);var rect=new Rect(left,y+3,Math.Max(1,(ActualWidth-left-68)*row.Value/maximum),Math.Max(2,height-9));dc.DrawRoundedRectangle(row.Color,hovered!=null&&row.Key==hovered.Key?new Pen(FluentTheme.Text,1):null,rect,3,3);Text(dc,row.Share,new Point(ActualWidth-63,y+3),FluentTheme.Text,60,11,false);hits.Add(Tuple.Create(row,(Geometry)new RectangleGeometry(new Rect(0,y,Math.Max(1,ActualWidth),height))));}
            }
        }
        static Point Polar(Point center,double radius,double angle){return new Point(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle));}
        void Text(DrawingContext dc,string value,Point point,Brush brush,double width,double size,bool center){var text=new FormattedText(value??"",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(1,width),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=center?TextAlignment.Center:TextAlignment.Left};dc.DrawText(text,point);}
        internal DiskChartRow Hit(Point point){return hits.Where(x=>x.Item2.FillContains(point)).Select(x=>x.Item1).FirstOrDefault();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var row=Hit(e.GetPosition(this));if(row!=hovered){hovered=row;Cursor=row==null?Cursors.Arrow:Cursors.Hand;ToolTip=row==null?null:row.Name+"\n"+row.Size+" · "+row.Files.ToString("N0")+" · "+row.Share;InvalidateVisual();}}
        protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);hovered=null;InvalidateVisual();}
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);var row=Hit(e.GetPosition(this));if(row!=null&&SliceClicked!=null){SliceClicked(row);e.Handled=true;}}
    }
}
