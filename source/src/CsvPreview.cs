using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal sealed partial class QuickPreviewPane {
  readonly WrapPanel csvBar=new WrapPanel{Margin=new Thickness(0,2,0,6),Visibility=Visibility.Collapsed};
  internal readonly ComboBox CsvSeparator=new ComboBox{Width=155,SelectedIndex=0,Margin=new Thickness(4,0,10,0)};
  internal readonly CheckBox CsvHeader=new CheckBox{Content=L.Text("csv.header"),IsChecked=true,VerticalAlignment=VerticalAlignment.Center};
  internal readonly DataGrid CsvGrid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=false,CanUserReorderColumns=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,HeadersVisibility=DataGridHeadersVisibility.Column,GridLinesVisibility=DataGridGridLinesVisibility.All,Visibility=Visibility.Collapsed};
  string csvText,csvStatus,csvPath;int csvChoice=-1;bool csvHasHeader,csvWrap;CsvDocument csvData;
  void InitializeCsvPreview(DockPanel body){CsvSeparator.ItemsSource=new[]{L.Text("csv.auto"),L.Text("csv.comma"),L.Text("csv.semicolon"),"TAB",L.Text("csv.pipe"),L.Text("csv.colon")};CsvSeparator.SelectedIndex=0;csvBar.Children.Add(new TextBlock{Text=L.Text("csv.separator"),VerticalAlignment=VerticalAlignment.Center});csvBar.Children.Add(CsvSeparator);csvBar.Children.Add(CsvHeader);DockPanel.SetDock(csvBar,Dock.Top);body.Children.Add(csvBar);content.Children.Add(CsvGrid);CsvSeparator.SelectionChanged+=delegate{UpdateFormat();};CsvHeader.Checked+=delegate{UpdateFormat();};CsvHeader.Unchecked+=delegate{UpdateFormat();};}
  bool CsvAvailable{get{return current!=null&&CsvDocument.Supports(current)&&lastData!=null&&lastData.Text!=null&&!lastData.IsBinary&&currentOffset==0&&(currentMode==PreviewMode.Auto||currentMode==PreviewMode.Text);}}
  bool UpdateCsvPreview(){bool available=CsvAvailable;csvBar.Visibility=available?Visibility.Visible:Visibility.Collapsed;bool show=available&&formatted.IsChecked==true;CsvGrid.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(!show){if(!available){CsvGrid.ItemsSource=null;CsvGrid.Columns.Clear();csvText=null;csvData=null;csvPath=null;}return false;}
   if(csvPath!=current||!Object.ReferenceEquals(csvText,lastData.Text)||csvChoice!=CsvSeparator.SelectedIndex||csvHasHeader!=(CsvHeader.IsChecked==true)||csvWrap!=(wrap.IsChecked==true)){
    csvPath=current;csvText=lastData.Text;csvChoice=CsvSeparator.SelectedIndex;csvHasHeader=CsvHeader.IsChecked==true;csvWrap=wrap.IsChecked==true;char? chosen=csvChoice>0?(char?)new[]{',',';','\t','|',':'}[csvChoice-1]:null;csvData=CsvDocument.Parse(csvText,chosen,lastData.TotalLength>lastData.ReadBytes,current);CsvGrid.Columns.Clear();
    var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.TextWrappingProperty,csvWrap?TextWrapping.Wrap:TextWrapping.NoWrap));style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty,72.0));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4,2,4,2)));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Text"){RelativeSource=RelativeSource.Self}));
    for(int i=0;i<csvData.Columns;i++){int column=i;string heading=csvHasHeader&&csvData.Rows.Count>0&&i<csvData.Rows[0].Length&&!String.IsNullOrWhiteSpace(csvData.Rows[0][i])?csvData.Rows[0][i]:L.Format("csv.column",i+1);int longest=Math.Max(heading.Length,csvData.Rows.Take(50).Select(x=>column<x.Length?x[column].Split('\n').Max(y=>y.Length):0).DefaultIfEmpty(0).Max());CsvGrid.Columns.Add(new DataGridTextColumn{Header=heading,Binding=new Binding("["+i+"]"){FallbackValue="",TargetNullValue=""},Width=Math.Max(100,Math.Min(320,longest*7+24)),ElementStyle=style});}
    CsvGrid.ItemsSource=csvData.Rows.Skip(csvHasHeader?1:0).Select(x=>Enumerable.Range(0,csvData.Columns).Select(i=>i<x.Length?x[i]:"").ToArray()).ToArray();string name=csvData.Separator=='\t'?"TAB":csvData.Separator.ToString();csvStatus=L.Format("csv.status",Math.Max(0,csvData.Rows.Count-(csvHasHeader?1:0)),csvData.Columns,name)+(csvData.Limited||csvData.Incomplete||lastData.TotalLength>lastData.ReadBytes?" · "+L.Text("csv.limited"):"");
   }formatted.IsEnabled=true;status.Text=csvStatus;return true;
  }
 }
}
