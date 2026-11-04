using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
 public partial class FilePanel {
  static readonly string[] BaseColumnKeys={"Name","Extension","Size","Modified","Attributes"};
  bool? sizeColumn,modifiedColumn;
  double cleanSizeWidth=90,cleanModifiedWidth=150;
  static string ColumnKey(GridViewColumn column){return Convert.ToString(((GridViewColumnHeader)column.Header).Tag);}
  GridViewColumn BaseColumn(int index){return view.Columns.FirstOrDefault(x=>ColumnKey(x)==BaseColumnKeys[index]);}
  IEnumerable<GridViewColumn> LogicalColumns {get{return BaseColumnKeys.Select(key=>view.Columns.FirstOrDefault(x=>ColumnKey(x)==key)).Where(x=>x!=null).Concat(view.Columns.Where(x=>MetadataColumn.IsSort(ColumnKey(x))));}}
  double RememberedWidth(int index){return index==1?cleanExtensionWidth:index==2?cleanSizeWidth:index==3?cleanModifiedWidth:cleanAttributesWidth;}
  void RememberWidth(int index,double width){if(index==1)cleanExtensionWidth=width;else if(index==2)cleanSizeWidth=width;else if(index==3)cleanModifiedWidth=width;else if(index==4)cleanAttributesWidth=width;}
  void ApplyColumnOrder(IEnumerable<string> order){int target=0;foreach(string key in (order??new string[0]).Distinct()){var column=view.Columns.FirstOrDefault(x=>ColumnKey(x)==key);if(column==null)continue;int index=view.Columns.IndexOf(column);if(index!=target)view.Columns.Move(index,target);target++;}}
  void MoveVisibleColumn(string key,int direction){var visible=view.Columns.Where(x=>x.Width>0).ToList();var column=visible.FirstOrDefault(x=>ColumnKey(x)==key);int index=visible.IndexOf(column),next=index+direction;if(index<0||next<0||next>=visible.Count)return;view.Columns.Move(view.Columns.IndexOf(column),view.Columns.IndexOf(visible[next]));}
  string ColumnCaption(string key){return MetadataColumn.IsSort(key)?MetadataColumn.Caption(key.Substring(5)):L.Text(key=="Name"?"column.name":key=="Extension"?"column.extension":key=="Size"?"column.size":key=="Modified"?"details.modified":"attributes.title");}
  static GridViewColumnHeader FileColumnHeader(DependencyObject origin){while(origin!=null){var header=origin as GridViewColumnHeader;if(header!=null)return header;origin=origin is Visual?VisualTreeHelper.GetParent(origin):LogicalTreeHelper.GetParent(origin);}return null;}
 }
}
