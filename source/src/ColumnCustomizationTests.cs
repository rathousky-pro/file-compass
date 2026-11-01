#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
namespace FileCompass {
 public partial class FilePanel {
  internal void VerifyColumnCustomization(string root,Action<bool,string> check){
   var original=new PanelView();SavePanelView(original);var main=Window.GetWindow(this);
   try{
    var face=FluentUiTests.Children<Border>(this).First(x=>Convert.ToString(x.Tag)=="panel-tab-face");
    check(Math.Abs(face.TranslatePoint(new Point(),this).X-(cleanBreadcrumbFrame.TranslatePoint(new Point(),this).X+cleanBreadcrumbFrame.Padding.Left))<1,"first tab begins at the inset of the shared navigation row");
    check(FluentUiTests.Children<TextBlock>(face).Any(x=>x.FontSize>=14),"tab caption uses a readable minimum font size");
    var header=(GridViewColumnHeader)BaseColumn(0).Header;
    header.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Right){RoutedEvent=UIElement.PreviewMouseRightButtonDownEvent});
    NavigationTests.Wait(Task.Delay(50));check(header.ContextMenu.IsOpen,"actual right-click route opens the column menu instead of file actions");
    var items=header.ContextMenu.Items.OfType<MenuItem>().Where(x=>BaseColumnKeys.Contains(Convert.ToString(x.Tag))).ToArray();
    check(items.Length==5&&items.Single(x=>Convert.ToString(x.Tag)=="Name").IsChecked&&items.Single(x=>Convert.ToString(x.Tag)=="Size").IsChecked,"column menu lists all standard columns and their current visibility");
    foreach(string key in new[]{"Extension","Attributes","Size"}){var item=items.Single(x=>Convert.ToString(x.Tag)==key);item.IsChecked=key!="Size";item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));}header.ContextMenu.IsOpen=false;
    check(BaseColumn(1).Width>0&&BaseColumn(4).Width>0&&BaseColumn(2).Width==0,"column menu independently shows extension and attributes and hides size");
    header.ContextMenu.IsOpen=true;var move=header.ContextMenu.Items.OfType<MenuItem>().Single(x=>Convert.ToString(x.Header)==L.Text("columns.moveRight"));move.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));header.ContextMenu.IsOpen=false;
    check(ColumnKey(view.Columns[0])=="Extension"&&ColumnKey(view.Columns[1])=="Name","column menu changes the visible column order");
    SetMetadataColumns(new[]{"dimensions"});check(ColumnKey(view.Columns[0])=="Extension"&&BaseColumn(2).Width==0,"adding metadata preserves reordered base columns and hidden columns");
    SetColumnWidth(0,233);check(BaseColumn(0).Width==233,"shared width changes address a column identity after reordering");
    var state=new PanelView();SavePanelView(state);var settings=new ProgramSettings{LeftView=state};string file=Path.Combine(root,"column-order.xml");settings.Save(file);var loaded=ProgramSettings.Load(file).LeftView;
    RestorePanelView(new PanelView());RestorePanelView(loaded);
    check(ColumnKey(view.Columns[0])=="Extension"&&BaseColumn(0).Width==233&&BaseColumn(2).Width==0&&MetadataColumnKeys.Contains("dimensions"),"column order, widths and visibility survive settings round trip");
    var metadata=view.Columns.Single(x=>ColumnKey(x)=="Meta:dimensions");view.Columns.Move(view.Columns.IndexOf(metadata),0);var moved=new PanelView();SavePanelView(moved);
    check(view.AllowsColumnReorder&&moved.ColumnOrder[0]=="Meta:dimensions","native column drag order is captured with panel settings");
    check(SaveTabs()[CurrentTabIndex].View.ColumnOrder.SequenceEqual(moved.ColumnOrder),"tab snapshots preserve the customized column order");
    main.UpdateLayout();NextStageTests.Draw(main,Path.Combine(root,"custom-column-order.png"));
   }finally{RestorePanelView(original);}
  }
 }
}
#endif
