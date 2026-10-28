using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FileCompass
{
    public static partial class DesktopIcons
    {
        public static ImageSource ToolbarDrawing(string name, ToolbarTheme theme) {
            if(name=="tools"){
                var drawing=new DrawingGroup();using(var c=drawing.Open()){
                    c.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));
                    var pen=new Pen(Brush("#267ED0"),1.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                    Shape(c,"M21,3 C17,3 14,6 14,10 L15,14 L4,25 C1,28 5,32 8,29 L19,18 C25,21 32,15 29,8 L24,13 L19,8 L24,3 Z",Brush("#ECF4FF"),pen);
                    c.DrawEllipse(null,pen,new Point(6,27),1,1);
                }drawing.Freeze();return new DrawingImage(drawing);
            }
            if(name.StartsWith("panels-",StringComparison.Ordinal)||name=="hidden-files"){
                var drawing=new DrawingGroup();using(var c=drawing.Open()){
                    c.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));var pen=new Pen(Brush("#267ED0"),1.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                    if(name=="hidden-files"){
                        Shape(c,"M2,16 C8,5 24,5 30,16 C24,27 8,27 2,16 Z",Brush("#ECF4FF"),pen);c.DrawEllipse(null,pen,new Point(16,16),4,4);Shape(c,"M4,28 L28,4",null,new Pen(Brush("#267ED0"),2.5));
                    }else{
                        c.DrawRoundedRectangle(Brush("#ECF4FF"),pen,new Rect(3,4,26,24),2,2);c.DrawLine(pen,new Point(3,10),new Point(29,10));
                        if(name=="panels-SideBySide")c.DrawLine(pen,new Point(16,10),new Point(16,28));
                        else if(name=="panels-Stacked")c.DrawLine(pen,new Point(3,19),new Point(29,19));
                    }
                }drawing.Freeze();return new DrawingImage(drawing);
            }
            if(name=="undo"){var undo=new DrawingGroup();using(var c=undo.Open()){c.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));var pen=new Pen(Brush("#267ED0"),2.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};Shape(c,"M25,25 C32,14 22,5 12,9 L5,14 M5,6 L5,14 L13,14",null,pen);}undo.Freeze();return new DrawingImage(undo);}
            if (name == "invert") {
                var drawing = new DrawingGroup();
                using (var c = drawing.Open()) {
                    c.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
                    var pen = new Pen(Brush("#267ED0"), 2);
                    c.DrawRectangle(Brush("#267ED0"), pen, new Rect(4, 5, 9, 9));
                    c.DrawRectangle(null, pen, new Rect(19, 18, 9, 9));
                    Shape(c, "M19,6 L27,6 L27,13 M23,9 L27,13 L31,9 M13,26 L5,26 L5,19 M1,23 L5,19 L9,23", null, pen);
                }
                drawing.Freeze(); return new DrawingImage(drawing);
            }
            return IconLibrary.Action(name, theme);
        }
        public static ImageSource BuildToolbarDrawing(string name, ToolbarTheme theme)
        {
            if(name=="new-file"){
                var drawing=new DrawingGroup();using(var c=drawing.Open()){var pen=new Pen(Brush("#267ED0"),1.7);c.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));Shape(c,"M6,3 L20,3 L26,9 L26,29 L6,29 Z",Brush("#ECF4FF"),pen);Shape(c,"M20,3 L20,9 L26,9",null,pen);var plus=new Pen(Brush("#168A76"),2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};c.DrawLine(plus,new Point(11,19),new Point(21,19));c.DrawLine(plus,new Point(16,14),new Point(16,24));}drawing.Freeze();return new DrawingImage(drawing);
            }
            if (theme == ToolbarTheme.Classic && name == "network") return FluentIcons.Network();
            if (theme == ToolbarTheme.Classic && (name == "pack" || name == "unpack")) return FluentIcons.Archive(name == "unpack");
            if (theme == ToolbarTheme.Classic) return FluentIcons.Load(name) ?? BuildLegacyDrawing(name);
            if (name == "copy" || name == "move" || name == "delete" || name == "settings") return FluentIcons.Load(name) ?? BuildLegacyDrawing(name);
            bool outline = theme == ToolbarTheme.Outline;
            var group = new DrawingGroup();
            using (var c = group.Open())
            {
                c.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
                var ink = Brush("#234B70"); var blue = Brush("#267ED0"); var green = Brush("#27966B"); var amber = Brush("#E4A640");
                var pen = new Pen(outline ? ink : blue, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                Action<string, Brush> shape = delegate(string path, Brush fill) { Shape(c, path, outline ? null : fill, pen); };
                Action folder = delegate { shape("M3,9 L3,6 L12,6 L15,9 L29,9 L29,27 L3,27 Z", amber); };
                if (name == "folder" || name == "favorite")
                { folder(); if (name == "favorite") Shape(c, "M16,11 L18,16 L24,16 L20,20 L21,25 L16,22 L11,25 L12,20 L8,16 L14,16 Z", outline ? null : green, outline ? pen : new Pen(Brushes.White, 1)); }
                else if (name == "refresh")
                {
                    Shape(c, "M27,13 C25,4 11,2 6,10 M5,19 C8,29 22,30 27,22", null, pen);
                    shape("M6,4 L6,11 L13,11 M26,28 L26,21 L19,21", outline ? null : blue);
                }
                else if (name == "back" || name == "forward")
                {
                    if (!outline) c.DrawRoundedRectangle(Brush("#E4EFFB"), null, new Rect(2, 2, 28, 28), 7, 7);
                    c.PushTransform(new RotateTransform(name == "forward" ? 180 : 0, 16, 16));
                    Shape(c, "M18,7 L9,16 L18,25 M9,16 L26,16", null, pen); c.Pop();
                }
                else if (name == "details" || name == "tree" || name == "select")
                {
                    c.DrawRoundedRectangle(outline ? null : Brush("#EDF4FB"), pen, new Rect(3, 3, 26, 26), 3, 3);
                    if (name == "select") { Shape(c, "M8,16 L14,22 L24,10", null, new Pen(outline ? ink : green, 2.4)); }
                    else for (int y = 9; y <= 24; y += 7)
                    { if (name == "tree") Shape(c, "M8,7 L8," + y + " L15," + y, null, pen); else c.DrawEllipse(outline ? ink : amber, null, new Point(8, y), 1.6, 1.6); c.DrawLine(pen, new Point(name == "tree" ? 17 : 13, y), new Point(24, y)); }
                }
                else if (name == "picture")
                { c.DrawRoundedRectangle(outline ? null : Brush("#E6F5FA"), pen, new Rect(3, 5, 26, 23), 3, 3); c.DrawEllipse(outline ? null : amber, pen, new Point(10, 12), 3, 3); shape("M4,25 L13,16 L19,22 L24,16 L28,21", green); }
                else if (name == "pack" || name == "unpack" || name == "archive-test")
                {
                    shape("M4,15 L28,15 L28,28 L4,28 Z", amber);
                    c.DrawLine(pen, new Point(4, 20), new Point(28, 20));
                    if (name == "archive-test") Shape(c, "M10,9 L15,13 L24,4", null, new Pen(outline ? ink : green, 2.4));
                    else { bool down = name == "pack"; Shape(c, down ? "M16,3 L16,16 M10,10 L16,16 L22,10" : "M16,16 L16,3 M10,9 L16,3 L22,9", null, new Pen(outline ? ink : down ? green : blue, 2.4)); }
                }
                else if (name == "ftp" || name == "url" || name == "network")
                {
                    c.DrawLine(pen, new Point(7, 24), new Point(25, 24)); c.DrawLine(pen, new Point(16, 14), new Point(16, 24));
                    c.DrawRoundedRectangle(outline ? null : Brush("#DDEFFA"), pen, new Rect(3, 20, 9, 8), 1, 1); c.DrawRoundedRectangle(outline ? null : Brush("#DDEFFA"), pen, new Rect(20, 20, 9, 8), 1, 1);
                    c.DrawText(new FormattedText(name == "url" ? "URL" : name == "ftp" ? "FTP" : "NET", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 10, ink, 1), new Point(6, 2));
                }
                else if (name == "search")
                { c.DrawEllipse(outline ? null : Brush("#DDEFFB"), pen, new Point(14, 13), 9, 9); c.DrawLine(new Pen(outline ? ink : blue, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(21, 20), new Point(28, 28)); }
                else if (name == "rename")
                { c.DrawRoundedRectangle(outline ? null : Brush("#EDF4FB"), pen, new Rect(3, 3, 26, 26), 3, 3); c.DrawText(new FormattedText("A B", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 11, ink, 1), new Point(5, 6)); Shape(c, "M6,23 L25,23 L20,18 M25,23 L20,28", null, pen); }
                else if (name == "sync")
                { Shape(c, "M4,10 L27,10 L21,4 M27,10 L21,16", null, new Pen(outline ? ink : green, 2.2)); Shape(c, "M28,23 L5,23 L11,17 M5,23 L11,29", null, pen); }
                else
                {
                    shape("M6,3 L22,3 L27,8 L27,29 L6,29 Z", Brush("#EEF5FA"));
                    Shape(c, "M22,3 L22,9 L27,9 M10,12 L22,12 M10,17 L22,17 M10,22 L19,22", null, pen);
                    if (name == "edit") { Shape(c, "M12,25 L26,11 L30,15 L16,29 L11,30 Z", outline ? null : amber, pen); }
                    if (name == "notepad") { for (int x = 10; x <= 22; x += 6) c.DrawLine(pen, new Point(x, 1), new Point(x, 6)); }
                }
            }
            group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;
        }
    }
}
