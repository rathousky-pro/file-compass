using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace FileCompass {
    // Vector counterparts for actions which are not part of the packaged icon sets.
    internal static class ToolbarSymbols {
        static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
        internal static readonly string[] Names={"panels-SideBySide","panels-Stacked","panels-Single","tools","undo","invert","settings","view","new-file","hidden-files-visible","hidden-files-hidden","hidden-files-partial"};
        internal static ImageSource Get(string name,ToolbarTheme theme){
            if(name=="hidden-files")name="hidden-files-hidden";
            if(Array.IndexOf(Names,name)<0)return null;
            if(theme==ToolbarTheme.Classic)return FluentIcons.Load(name);
            string key=theme+":"+name;lock(cache){ImageSource result;if(cache.TryGetValue(key,out result))return result;result=Draw(name,theme);cache[key]=result;return result;}
        }
        static Brush Ink(string color){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));}
        static ImageSource Draw(string name,ToolbarTheme theme){
            bool outline=theme==ToolbarTheme.Outline,fluent=theme==ToolbarTheme.Classic;
            string color=name=="undo"?"#BD7515":name=="invert"?"#8054C7":name=="tools"?"#347C99":name=="settings"?"#69758C":name=="hidden-files-visible"?"#168369":name=="hidden-files-hidden"?"#A6505B":name=="hidden-files-partial"?"#B47A14":"#267ED0";
            if(outline)color="#234B70";else if(fluent&&name=="undo")color="#267ED0";
            var ink=Ink(color);var pen=new Pen(ink,outline?1.6:fluent?1.7:2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
            Brush fill=outline?null:fluent?(Brush)new LinearGradientBrush(Color.FromRgb(246,250,255),Color.FromRgb(210,228,247),90):Ink("#E8F1FA");
            var group=new DrawingGroup();using(var c=group.Open()){
                c.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));
                Action<string,Brush> shape=(path,paint)=>c.DrawGeometry(paint,pen,Geometry.Parse(path));
                if(name=="view"){
                    // A document under a magnifier distinguishes preview from panel layout.
                    shape("M5,3 L18,3 L24,9 L24,26 L5,26 Z",fill);
                    shape("M18,3 L18,9 L24,9 M9,12 L16,12 M9,17 L13,17",null);
                    var lensInk=outline?ink:Ink(fluent?"#5568AD":"#168B99");
                    var lensPen=new Pen(lensInk,outline?1.6:1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                    c.DrawEllipse(outline?null:fluent?(Brush)new LinearGradientBrush(Color.FromRgb(237,250,255),Color.FromRgb(174,222,244),45):Ink("#DDF4F6"),lensPen,new Point(21,21),6,6);
                    c.DrawLine(new Pen(lensInk,2.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(25.5,25.5),new Point(30,30));
                }else if(name.StartsWith("panels-",StringComparison.Ordinal)){
                    c.DrawRoundedRectangle(outline?null:Ink("#EDF5FE"),pen,new Rect(3,4,26,24),2,2);
                    if(!outline)c.DrawRectangle(fill,null,new Rect(4,5,24,5));
                    c.DrawLine(pen,new Point(3,10),new Point(29,10));
                    if(name=="panels-SideBySide"||name=="view"){if(!outline)c.DrawRectangle(fill,null,new Rect(name=="view"?19:4,11,name=="view"?9:11,16));c.DrawLine(pen,new Point(name=="view"?18:16,10),new Point(name=="view"?18:16,28));}
                    else if(name=="panels-Stacked"){if(!outline)c.DrawRectangle(fill,null,new Rect(4,11,24,7));c.DrawLine(pen,new Point(3,19),new Point(29,19));}
                    else if(!outline)c.DrawRectangle(fill,null,new Rect(4,11,24,16));
                }else if(name=="tools"){
                    shape("M21,3 C17,3 14,6 14,10 L15,14 L4,25 C1,28 5,32 8,29 L19,18 C25,21 32,15 29,8 L24,13 L19,8 L24,3 Z",fill);
                    c.DrawEllipse(outline?null:Brushes.White,pen,new Point(6,27),1,1);
                }else if(name=="undo"){
                    shape("M25,25 C32,14 22,5 12,9 L5,14 M5,6 L5,14 L13,14",null);
                }else if(name=="invert"){
                    c.DrawRoundedRectangle(outline?null:fluent?Ink("#EEE8FA"):ink,pen,new Rect(4,5,9,9),1,1);c.DrawRoundedRectangle(fill,pen,new Rect(19,18,9,9),1,1);
                    shape("M19,6 L27,6 L27,13 M23,9 L27,13 L31,9 M13,26 L5,26 L5,19 M1,23 L5,19 L9,23",null);
                    if(outline)shape("M6,9 L8,11 L11,7",null);
                }else if(name=="settings"){
                    shape("M12,2 L20,2 L21,7 L25,9 L29,8 L32,15 L28,18 L27,22 L29,26 L23,30 L19,27 L14,27 L10,30 L4,26 L6,22 L5,18 L1,15 L4,8 L8,9 L11,7 Z",fill);
                    c.DrawEllipse(outline?null:Brushes.White,pen,new Point(16,16),5,5);
                }else if(name=="new-file"){
                    shape("M6,3 L20,3 L26,9 L26,29 L6,29 Z",fill);shape("M20,3 L20,9 L26,9",null);var plus=new Pen(outline?ink:Ink("#168369"),outline?1.8:2.2);c.DrawLine(plus,new Point(11,19),new Point(21,19));c.DrawLine(plus,new Point(16,14),new Point(16,24));
                }else{
                    // A file plus an eye describes file visibility; the badge shows the current state.
                    shape("M5,2 L15,2 L20,7 L20,13 L5,13 Z",outline?null:Ink("#EDF3FA"));shape("M15,2 L15,7 L20,7",null);
                    shape("M2,21 C8,12 22,12 28,21 C22,30 8,30 2,21 Z",outline?null:Ink("#F4F8FC"));
                    c.DrawEllipse(outline||fluent?null:ink,pen,new Point(15,21),3.5,3.5);
                    if(name=="hidden-files-hidden"){
                        c.DrawLine(new Pen(outline?Brushes.White:Ink("#F4F8FC"),4),new Point(4,29),new Point(27,10));
                        c.DrawLine(new Pen(ink,2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(4,29),new Point(27,10));
                    }else{
                        c.DrawEllipse(outline||fluent?Brushes.White:ink,pen,new Point(25,26),6,6);
                        var badge=new Pen(outline||fluent?ink:Brushes.White,1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                        if(name=="hidden-files-visible"){c.DrawLine(badge,new Point(22,26),new Point(24,28));c.DrawLine(badge,new Point(24,28),new Point(28,24));}
                        else c.DrawLine(badge,new Point(22,26),new Point(28,26));
                    }
                }
            }
            group.Freeze();var image=new DrawingImage(group);image.Freeze();return image;
        }
    }
}
