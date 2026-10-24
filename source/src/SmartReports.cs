using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
namespace FileCompass {
    internal static class SmartReports {
        internal const int ReportWidth=1400,RowsPerPdfPage=32;
        static Brush Brush(string color){var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(color);brush.Freeze();return brush;}
        static readonly Brush Ink=Brush("#202A36"),Muted=Brush("#526173"),Line=Brush("#DEE5ED"),Alternate=Brush("#F3F6FA");
        static TextBlock Text(string value,double size=14,bool bold=false){return new TextBlock{Text=value??"",FontFamily=new FontFamily("Segoe UI"),FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Foreground=Ink,TextWrapping=TextWrapping.Wrap};}
        internal static FrameworkElement Build(SmartSnapshot snapshot,IList<SmartAttribute> rows=null,int first=0) {
            rows=rows??snapshot.Attributes;
            var body=new StackPanel();var root=new Border{Width=ReportWidth,Padding=new Thickness(28),Background=Brushes.White,Child=body};
            body.Children.Add(Text(ApplicationBrand.Name+" · "+L.Text("smart.title"),25,true));
            var caption=Text(snapshot.Model+" · "+snapshot.Device,20,true);caption.Margin=new Thickness(0,6,0,4);body.Children.Add(caption);
            var time=Text(L.Format("smart.collectedAt",snapshot.CollectedUtc.ToLocalTime().ToString("g")),13);time.Foreground=Muted;time.Margin=new Thickness(0,0,0,18);body.Children.Add(time);
            var main=new Grid();main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(345)});main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});main.ColumnDefinitions.Add(new ColumnDefinition());body.Children.Add(main);
            var information=new StackPanel();main.Children.Add(information);
            var badges=new Grid{Margin=new Thickness(0,0,0,18)};badges.ColumnDefinitions.Add(new ColumnDefinition());badges.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});badges.ColumnDefinitions.Add(new ColumnDefinition());
            var health=Badge(L.Text("smart.health"),snapshot.HealthText+(snapshot.RemainingText.Length>0?"\n"+snapshot.RemainingText:""),snapshot.HealthKey);
            var temperature=Badge(L.Text("smart.temperature"),snapshot.TemperatureText,"temperature");Grid.SetColumn(temperature,2);badges.Children.Add(health);badges.Children.Add(temperature);information.Children.Add(badges);
            foreach(var item in snapshot.Information) {
                var row=new Grid{Margin=new Thickness(0,0,0,8)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(125)});row.ColumnDefinitions.Add(new ColumnDefinition());
                var label=Text(L.Text("smart."+item.Key),13);label.Foreground=Muted;label.Margin=new Thickness(0,0,10,0);row.Children.Add(label);
                var value=Text(item.Value,14,true);Grid.SetColumn(value,1);row.Children.Add(value);information.Children.Add(row);
            }
            var metrics=new StackPanel();Grid.SetColumn(metrics,2);main.Children.Add(metrics);
            metrics.Children.Add(Text(L.Text("smart.attributesTitle"),18,true));
            var count=Text(L.Format("smart.reportRows",rows.Count==0?0:first+1,first+rows.Count,snapshot.Attributes.Count),13);count.Foreground=Muted;count.Margin=new Thickness(0,4,0,10);metrics.Children.Add(count);
            if(!snapshot.Nvme){var legend=Text(L.Text("smart.normalizedHelp"),12);legend.Foreground=Muted;legend.Margin=new Thickness(0,0,0,8);metrics.Children.Add(legend);}
            if(rows.Count==0)metrics.Children.Add(Text(L.Text("smart.unavailable")));
            else {
                var columns=new Grid();columns.ColumnDefinitions.Add(new ColumnDefinition());columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});columns.ColumnDefinitions.Add(new ColumnDefinition());metrics.Children.Add(columns);
                int middle=(rows.Count+1)/2;FrameworkElement firstTable=Table(rows.Take(middle).ToList(),snapshot.Nvme),secondTable=Table(rows.Skip(middle).ToList(),snapshot.Nvme);
                columns.Children.Add(firstTable);Grid.SetColumn(secondTable,2);columns.Children.Add(secondTable);
            }
            if(!String.IsNullOrWhiteSpace(snapshot.Error)){var error=Text(L.Text("smart.readProblem")+" "+snapshot.Error,13);body.Children.Add(new Border{Child=error,Background=Brush("#FFF1D2"),Padding=new Thickness(12),Margin=new Thickness(0,16,0,0)});}
            var footer=Text(ApplicationBrand.Name+" "+BuildInfo.DisplayVersion+" · "+ApplicationBrand.Developer+" · "+L.Format("smart.attributeCount",snapshot.Attributes.Count),12);footer.Foreground=Muted;footer.Margin=new Thickness(0,20,0,0);body.Children.Add(footer);
            root.Measure(new Size(ReportWidth,Double.PositiveInfinity));root.Arrange(new Rect(new Point(),root.DesiredSize));root.UpdateLayout();return root;
        }
        static Border Badge(string title,string value,string state) {
            string background=state=="good"?"#DCF5E8":state=="caution"?"#FFF0C2":state=="bad"?"#FFE0E0":state=="temperature"?"#DFEEFF":"#E4E7EB";
            string foreground=state=="good"?"#12623C":state=="caution"?"#704600":state=="bad"?"#9E2031":state=="temperature"?"#135C9E":"#526173";
            var stack=new StackPanel();var label=Text(title,13,true);label.Foreground=Brush(foreground);label.TextAlignment=TextAlignment.Center;stack.Children.Add(label);
            var number=Text(value,25,true);number.Foreground=Brush(foreground);number.TextAlignment=TextAlignment.Center;number.Margin=new Thickness(0,4,0,0);stack.Children.Add(number);
            return new Border{Child=stack,Background=Brush(background),Padding=new Thickness(10),CornerRadius=new CornerRadius(7),MinHeight=95};
        }
        static FrameworkElement Table(IList<SmartAttribute> rows,bool nvme) {
            var stack=new StackPanel();bool state=rows.Any(x=>!String.IsNullOrEmpty(x.State));
            string[] titles={"ID",L.Text("smart.attribute"),L.Text("smart.normalizedCompact"),L.Text("smart.raw"),L.Text("smart.state")};
            stack.Children.Add(TableRow(titles,nvme,state,true,Brush("#E9EFF7")));
            for(int i=0;i<rows.Count;i++){var row=rows[i];var background=!String.IsNullOrEmpty(row.State)?Brush(row.State==L.Text("smart.bad")?"#FFE0E0":"#FFF0C2"):i%2==1?Alternate:Brushes.White;
                stack.Children.Add(TableRow(new[]{row.Id,row.Name,row.Normalized,row.Raw,row.State},nvme,state,false,background));}
            return new Border{Child=stack,BorderBrush=Line,BorderThickness=new Thickness(1)};
        }
        static FrameworkElement TableRow(string[] values,bool nvme,bool state,bool header,Brush background) {
            var row=new Grid{Background=background,MinHeight=29};double[] widths={29,0,nvme?0:106,112,state?70:0};
            for(int i=0;i<values.Length;i++){row.ColumnDefinitions.Add(new ColumnDefinition{Width=i==1?new GridLength(1,GridUnitType.Star):new GridLength(widths[i])});if(i!=1&&widths[i]==0)continue;var text=Text(values[i],i==2?12:13,header);text.Margin=new Thickness(5,5,5,5);text.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(text,i);row.Children.Add(text);}
            return row;
        }
        internal static BitmapSource Render(FrameworkElement report,double scale=2) {
            int width=(int)Math.Ceiling(report.ActualWidth*scale),height=(int)Math.Ceiling(report.ActualHeight*scale);
            if(width<1||height<1||(long)width*height>100000000)throw new IOException(L.Text("smart.reportTooLarge"));
            var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){draw.DrawRectangle(Brushes.White,null,new Rect(0,0,report.ActualWidth,report.ActualHeight));draw.DrawRectangle(new VisualBrush(report),null,new Rect(0,0,report.ActualWidth,report.ActualHeight));}
            var bitmap=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
        }
        internal static List<List<SmartAttribute>> PdfRows(SmartSnapshot snapshot) {
            var pages=new List<List<SmartAttribute>>();for(int i=0;i<snapshot.Attributes.Count;i+=RowsPerPdfPage)pages.Add(snapshot.Attributes.Skip(i).Take(RowsPerPdfPage).ToList());
            if(pages.Count==0)pages.Add(new List<SmartAttribute>());return pages;
        }
        internal static void Png(string path,SmartSnapshot snapshot) {
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(Build(snapshot))));
            using(var output=new MemoryStream()){encoder.Save(output);Write(path,output.ToArray());}
        }
        internal static void Pdf(string path,SmartSnapshot snapshot) {
            var builder=new PdfDocumentBuilder();builder.DocumentInformation.Title=ApplicationBrand.Name+" SMART - "+snapshot.Model;
            int offset=0;foreach(var rows in PdfRows(snapshot)) {
                var report=Build(snapshot,rows,offset);var encoder=new JpegBitmapEncoder{QualityLevel=96};encoder.Frames.Add(BitmapFrame.Create(Render(report,3)));
                byte[] image;using(var output=new MemoryStream()){encoder.Save(output);image=output.ToArray();}
                var page=builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4,false);double width=page.PageSize.Width,height=page.PageSize.Height,scale=Math.Min((width-36)/report.ActualWidth,(height-36)/report.ActualHeight),imageWidth=report.ActualWidth*scale,imageHeight=report.ActualHeight*scale;
                page.AddJpeg(image,new PdfRectangle(18,height-18-imageHeight,18+imageWidth,height-18));offset+=rows.Count;
            }
            Write(path,builder.Build());
        }
        static void Write(string path,byte[] bytes) {
            path=Path.GetFullPath(path);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}

