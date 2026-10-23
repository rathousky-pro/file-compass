using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
namespace CommanderCZ {
    internal sealed class CompressionRatioTrend : FrameworkElement {
        readonly List<Tuple<double,double>> samples=new List<Tuple<double,double>>();
        internal bool SpeedMode;
        internal void Clear() {samples.Clear();Current=0;ScaleMaximum=0;InvalidateVisual();}
        internal double Current {get;private set;}
        internal double ScaleMaximum {get;private set;}
        internal int SampleCount {get{return samples.Count;}}
        internal double Completion { get { return samples.Count==0?0:samples[samples.Count-1].Item1; } }
        internal void Add(double ratio, double completion) {
            if(Double.IsNaN(ratio)||Double.IsInfinity(ratio)||ratio<0||Double.IsNaN(completion)||Double.IsInfinity(completion))return;
            completion=Math.Max(0,Math.Min(1,completion));Current=ratio;
            if(samples.Count>0 && completion<samples[samples.Count-1].Item1-.01)samples.Clear();
            if(samples.Count>0 && (int)(completion*1000)==(int)(samples[samples.Count-1].Item1*1000))
                samples[samples.Count-1]=Tuple.Create(completion,ratio);
            else samples.Add(Tuple.Create(completion,ratio));
            if(samples.Count>600)for(int i=samples.Count-2;i>0;i-=2)samples.RemoveAt(i);
            ScaleMaximum=SpeedMode?Math.Max(1,Math.Ceiling(samples.Max(x=>x.Item2)*1.1)):Math.Max(125,Math.Ceiling(samples.Max(x=>x.Item2)/25)*25);InvalidateVisual();
        }
        void Label(DrawingContext c,string text,double x,double y) {
            c.DrawText(new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,FluentTheme.Muted,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        }
        protected override void OnRender(DrawingContext c) {
            base.OnRender(c);double width=ActualWidth-83,height=ActualHeight-28;if(width<20||height<20)return;
            double max=ScaleMaximum>0?ScaleMaximum:125;
            foreach(double value in (SpeedMode?new[]{0.0,max/2,max}:max>=175?new[]{0.0,100,max}:new[]{0.0,100}).Distinct()) {
                double y=8+height*(1-value/max);var pen=new Pen(!SpeedMode&&value==100?FluentTheme.Accent:CompactTheme.Line,!SpeedMode&&value==100?1.3:1);if(!SpeedMode&&value==100)pen.DashStyle=DashStyles.Dash;
                c.DrawLine(pen,new Point(70,y),new Point(70+width,y));Label(c,value.ToString(SpeedMode?"0.#":"0")+(SpeedMode?" MB/s":" %"),0,y-7);
            }
            foreach(double fraction in new[]{0.0,.25,.5,.75,1.0}) Label(c,(fraction*100).ToString("0")+" %",70+width*fraction-(fraction==1?28:0),height+13);
            if(samples.Count==0)return;
            var geometry=new StreamGeometry();using(var g=geometry.Open())for(int i=0;i<samples.Count;i++){
                var point=new Point(70+width*samples[i].Item1,8+height*(1-samples[i].Item2/max));if(i==0)g.BeginFigure(point,false,false);else g.LineTo(point,true,false);
            }
            c.DrawGeometry(null,new Pen(FluentTheme.Accent,2),geometry);
            var last=samples[samples.Count-1];c.DrawEllipse(FluentTheme.Accent,null,new Point(70+width*last.Item1,8+height*(1-last.Item2/max)),3,3);
        }
    }
}
