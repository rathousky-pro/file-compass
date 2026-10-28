using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FileCompass {
    internal sealed class DiskReportChart {internal string Title,Scope;internal List<DiskChartRow> Rows;internal byte[] Image;}
    public sealed class DiskSnapshotFolder {public string Path;public long Bytes,Files;}
    public sealed class DiskSnapshot {public int Version=1;public string Root,Filters;public DateTime Created;public bool Partial;public List<DiskSnapshotFolder> Folders=new List<DiskSnapshotFolder>();}
    internal sealed class DiskComparisonRow {
        public string Path {get;set;}public long BeforeBytes{get;set;}public long AfterBytes{get;set;}public long Delta{get{return AfterBytes-BeforeBytes;}}
        public string Before{get{return DiskAnalysis.FormatSize(BeforeBytes);}}public string After{get{return DiskAnalysis.FormatSize(AfterBytes);}}
        public string Change{get{return (Delta>0?"+":Delta<0?"−":"")+DiskAnalysis.FormatSize(Math.Abs(Delta));}}
    }
    internal static class DiskAnalysisReports {
        internal static DiskSnapshot Capture(DiskAnalysis result){
            var snapshot=new DiskSnapshot{Root=result.Root.Path,Filters=result.FilterSignature,Created=result.ScannedUtc,Partial=result.IsPartial||result.Cancelled};var stack=new Stack<DiskFolder>();stack.Push(result.Root);
            while(stack.Count>0){var folder=stack.Pop();if(!folder.Virtual)snapshot.Folders.Add(new DiskSnapshotFolder{Path=result.Root.Virtual?folder.Path:folder==result.Root?".":folder.Path.Substring(result.Root.Path.TrimEnd('\\').Length).TrimStart('\\'),Bytes=folder.Bytes,Files=folder.Files});foreach(var child in folder.Children)stack.Push(child);}return snapshot;
        }
        internal static void SaveSnapshot(string path,DiskAnalysis result){File.WriteAllText(path,new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Serialize(Capture(result)),new System.Text.UTF8Encoding(true));}
        internal static DiskSnapshot LoadSnapshot(string path){
            if(new FileInfo(path).Length>64*1024*1024)throw new IOException(L.Text("analyzer.snapshotInvalid"));
            var snapshot=new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=30}.Deserialize<DiskSnapshot>(File.ReadAllText(path));
            if(snapshot==null||snapshot.Version!=1||String.IsNullOrEmpty(snapshot.Root)||snapshot.Folders==null||snapshot.Folders.Any(x=>x==null||x.Path==null||x.Bytes<0||x.Files<0)||snapshot.Folders.Select(x=>x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=snapshot.Folders.Count)throw new IOException(L.Text("analyzer.snapshotInvalid"));return snapshot;
        }
        internal static List<DiskComparisonRow> Compare(DiskSnapshot before,DiskSnapshot after){
            if(before.Partial||after.Partial||!String.Equals(before.Root.TrimEnd('\\'),after.Root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||before.Filters!=after.Filters)throw new IOException(L.Text("analyzer.snapshotMismatch"));
            var old=before.Folders.ToDictionary(x=>x.Path,StringComparer.OrdinalIgnoreCase);var current=after.Folders.ToDictionary(x=>x.Path,StringComparer.OrdinalIgnoreCase);var rows=new List<DiskComparisonRow>();
            foreach(string key in old.Keys.Union(current.Keys,StringComparer.OrdinalIgnoreCase)){DiskSnapshotFolder a,b;old.TryGetValue(key,out a);current.TryGetValue(key,out b);long previous=a==null?0:a.Bytes,now=b==null?0:b.Bytes;if(previous!=now)rows.Add(new DiskComparisonRow{Path=key,BeforeBytes=previous,AfterBytes=now});}return rows.OrderByDescending(x=>Math.Abs(x.Delta)).ToList();
        }
        internal static byte[] Png(FrameworkElement element){
            int width=Math.Max(1,(int)Math.Ceiling(element.ActualWidth)),height=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(CompactTheme.WhiteSurface,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,width,height));}
            var bitmap=new RenderTargetBitmap(width*2,height*2,192,192,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=new MemoryStream()){encoder.Save(output);return output.ToArray();}
        }
        internal static string Csv(string value){value=value??"";if(value.Length>0&&"=+-@\t\r\n".IndexOf(value[0])>=0)value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
        internal static void CsvReport(string target,DiskAnalysis result,CancellationToken token){
            using(var writer=new StreamWriter(target,false,new System.Text.UTF8Encoding(true))){writer.WriteLine("Path,Bytes,ModifiedUtc,Category");foreach(var file in DiskFileIndex.Read(result.IndexPath,token))writer.WriteLine(Csv(file.Path)+","+file.Bytes.ToString(CultureInfo.InvariantCulture)+","+Csv(file.Modified.ToUniversalTime().ToString("o"))+","+Csv(DiskScanOptions.FileCategory(file.Path)));}
        }
        static string H(string value){return System.Net.WebUtility.HtmlEncode(value??"");}
        internal static void HtmlReport(string target,DiskAnalysis result,string title,string chartScope,IEnumerable<DiskChartRow> rows,byte[] chart){
            var text=new System.Text.StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>").Append(H(title)).Append("</title><style>body{font:15px Segoe UI,sans-serif;max-width:1050px;margin:32px auto;color:#20242a}table{border-collapse:collapse;width:100%}td,th{padding:8px;text-align:left;border-bottom:1px solid #ddd}img{max-width:600px;width:100%;height:auto}small{color:#596572}</style></head><body><h1>").Append(H(title)).Append("</h1><p>").Append(H(result.Root.Path)).Append("</p><p>").Append(H(result.Root.Size)).Append(" · ").Append(result.Root.Files.ToString("N0")).Append(" ").Append(H(L.Text("analyzer.files"))).Append("</p><small>").Append(H(result.ScannedUtc.ToLocalTime().ToString("g"))).Append(" · ").Append(H(result.FilterDescription)).Append("</small><h2>").Append(H(chartScope)).Append("</h2><img alt=\"chart\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(chart)).Append("\"><table><tr><th>").Append(H(L.Text("analyzer.category"))).Append("</th><th>").Append(H(L.Text("analyzer.bytes"))).Append("</th><th>").Append(H(L.Text("analyzer.files"))).Append("</th><th>%</th></tr>");
            foreach(var row in rows)text.Append("<tr><td>").Append(H(row.Name)).Append("</td><td>").Append(H(row.Size)).Append("</td><td>").Append(row.Files.ToString("N0")).Append("</td><td>").Append(H(row.Share)).Append("</td></tr>");
            text.Append("</table><h2>").Append(H(L.Text("analyzer.largest"))).Append("</h2><table>");foreach(var row in result.Largest)text.Append("<tr><td>").Append(H(row.Path)).Append("</td><td>").Append(H(row.Size)).Append("</td></tr>");text.Append("</table><p>").Append(H(L.Text("analyzer.skipped"))).Append(": ").Append(result.Skipped).Append("</p></body></html>");File.WriteAllText(target,text.ToString(),new System.Text.UTF8Encoding(true));
        }
        internal static void HtmlReportSet(string target,DiskAnalysis result,IList<DiskReportChart> charts){
            var first=charts[0];HtmlReport(target,result,L.Text("analyzer.title"),first.Title+" · "+first.Scope,first.Rows,first.Image);var additional=new System.Text.StringBuilder();
            foreach(var chart in charts.Skip(1)){additional.Append("<h2>").Append(H(chart.Title)).Append("</h2><p>").Append(H(chart.Scope)).Append("</p><img alt=\"chart\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(chart.Image)).Append("\"><table>");foreach(var row in chart.Rows)additional.Append("<tr><td>").Append(H(row.Name)).Append("</td><td>").Append(H(row.Size)).Append("</td><td>").Append(row.Files.ToString("N0")).Append("</td><td>").Append(H(row.Share)).Append("</td></tr>");additional.Append("</table>");}
            string html=File.ReadAllText(target),marker="<h2>"+H(L.Text("analyzer.largest"))+"</h2>";File.WriteAllText(target,html.Replace(marker,additional+marker),new System.Text.UTF8Encoding(true));
        }
    }
}
