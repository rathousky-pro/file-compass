using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
 public partial class FilePanel {
  bool batchColumnWidths;
  ScrollViewer columnViewport;
  static T ColumnVisual<T>(DependencyObject node)where T:DependencyObject{if(node is T)return (T)node;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++){var found=ColumnVisual<T>(System.Windows.Media.VisualTreeHelper.GetChild(node,i));if(found!=null)return found;}return null;}
  void ObserveColumnViewport(){var viewport=ColumnVisual<ScrollViewer>(List);if(Object.ReferenceEquals(viewport,columnViewport))return;if(columnViewport!=null)columnViewport.ScrollChanged-=ColumnViewportChanged;columnViewport=viewport;if(viewport!=null)viewport.ScrollChanged+=ColumnViewportChanged;FitColumns();}
  void ColumnViewportChanged(object sender,ScrollChangedEventArgs e){if(Math.Abs(e.ViewportWidthChange)>.1&&!resizingColumns)FitColumns();}
  double ColumnRoom {get{if(columnViewport!=null&&columnViewport.ViewportWidth>0&&!Double.IsInfinity(columnViewport.ViewportWidth))return columnViewport.ViewportWidth;return Math.Max(0,List.ActualWidth-List.BorderThickness.Left-List.BorderThickness.Right-List.Padding.Left-List.Padding.Right);}}
  double ColumnMinimum(string key){return key=="Name"?100:key=="Extension"?60:key=="Size"?70:key=="Modified"?130:key=="Attributes"?65:80;}
  bool CanShowColumn(string key){return ColumnRoom<1||view.Columns.Where(x=>x.Width>0&&ColumnKey(x)!=key).Sum(x=>ColumnMinimum(ColumnKey(x)))+ColumnMinimum(key)<=ColumnRoom;}
  void FitColumns(string preferred=null){
   if(List.ActualWidth<1||view.Columns.Count<5)return;double room=ColumnRoom;var wanted=view.Columns.Where(x=>{int index=Array.IndexOf(BaseColumnKeys,ColumnKey(x));return index<0||BaseColumnVisible(index);}).ToList();
   var priority=wanted.OrderBy(x=>ColumnKey(x)=="Name"?0:ColumnKey(x)=="Size"?1:ColumnKey(x)=="Modified"?2:ColumnKey(x)=="Extension"?3:ColumnKey(x)=="Attributes"?4:5).ToList();var shown=new System.Collections.Generic.List<GridViewColumn>();double used=0;
   foreach(var column in priority){double minimum=ColumnMinimum(ColumnKey(column));if(shown.Count==0||used+minimum<=room){shown.Add(column);used+=Math.Min(minimum,room);}}
   bool previous=resizingColumns;resizingColumns=true;try{
    var widths=shown.Select(x=>Math.Max(ColumnMinimum(ColumnKey(x)),Double.IsNaN(x.Width)?100:x.Width)).ToArray();double minimumTotal=shown.Sum(x=>ColumnMinimum(ColumnKey(x)));
    if(shown.Count==1)widths[0]=room;else{
     int focus=shown.FindIndex(x=>ColumnKey(x)==preferred);if(focus>=0){double maximum=room-minimumTotal+ColumnMinimum(preferred);widths[focus]=Math.Min(widths[focus],Math.Max(ColumnMinimum(preferred),maximum));}
     double extra=Math.Max(0,room-minimumTotal),desired=widths.Select((w,i)=>w-ColumnMinimum(ColumnKey(shown[i]))).Sum();
     if(desired>extra){double locked=focus<0?0:widths[focus]-ColumnMinimum(preferred);double rest=desired-locked;for(int i=0;i<widths.Length;i++)if(i!=focus)widths[i]=ColumnMinimum(ColumnKey(shown[i]))+(rest<=0?0:(widths[i]-ColumnMinimum(ColumnKey(shown[i])))*Math.Max(0,extra-locked)/rest);}

    }
    // A column resize transfers space to another column; it never leaves a blank tail.
    int elastic=shown.FindIndex(x=>ColumnKey(x)=="Name");
    if(preferred=="Name"&&shown.Count>1)elastic=shown.Select((x,i)=>new{Column=x,Index=i}).Where(x=>ColumnKey(x.Column)!="Name").OrderBy(x=>view.Columns.IndexOf(x.Column)).Last().Index;
    if(elastic<0)elastic=0;
    widths[elastic]+=Math.Max(0,room-widths.Sum());
    for(int i=0;i<widths.Length;i++)widths[i]=Math.Floor(widths[i]);
    widths[elastic]+=room-widths.Sum();
    foreach(var column in view.Columns){int index=shown.IndexOf(column);column.Width=index<0?0:Math.Max(0,widths[index]);}
   }finally{resizingColumns=previous;}
  }
 }
}
