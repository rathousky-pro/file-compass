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
  internal void VerifyPanelInteractions(string root, Action<bool,string> check)
  {
   var original = new PanelView();
   SavePanelView(original);
   try
   {
    var header = (GridViewColumnHeader)BaseColumn(0).Header;
    SetBaseColumnVisible(1,true);
    SetBaseColumnVisible(2,false);
    var widths = view.Columns.Select(column => column.Width).ToArray();
    var neighbor = NextVisibleColumn(BaseColumn(0));
    check(neighbor == BaseColumn(1), "the Name divider targets the next visible column");
    ResizeColumnPair(BaseColumn(0), neighbor, widths[0], widths[1], -20);
    check(BaseColumn(0).Width < widths[0] && Math.Abs(BaseColumn(0).Width + neighbor.Width - widths[0] - widths[1]) < .001
        && view.Columns.Skip(2).Select(column => column.Width).SequenceEqual(widths.Skip(2)), "moving the Name divider redistributes only the adjacent widths");
    ResizeColumnPair(BaseColumn(0), neighbor, widths[0], widths[1], 0);
    header.ContextMenu.IsOpen = true;
    var groupMenu = header.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, L.Text("columns.groupBy")));
    ((MenuItem)groupMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    header.ContextMenu.IsOpen = false;
    var groups = List.Items.Groups;
    check(groups != null && groups.Cast<System.Windows.Data.CollectionViewGroup>().Any(group => Equals(group.Name, ".txt"))
        && ((Entry)List.Items[0]).IsParent, "type grouping keeps the parent first and groups visible files by extension");
    SetDisplay("Short");
    check(List.Items.Groups == null, "tile mode suspends grouping");
    SetDisplay("Details");
    check(List.Items.Groups != null, "returning to Details restores grouping");
    var popupSize = new Size(520, 340);
    var workAtTop = new Rect(-140, -30, 850, 710);
    var workAtBottom = new Rect(-690, -675, 850, 710);
    var nearTop = HoverPreviewBounds(workAtTop, 20, popupSize);
    var nearBottom = HoverPreviewBounds(workAtBottom, 20, popupSize);
    check(nearTop.Top > 20 && nearTop.Size == popupSize && workAtTop.Contains(nearTop),
        "hover preview opens below a top row within the monitor work area");
    check(nearBottom.Bottom < 0 && nearBottom.Size == popupSize && workAtBottom.Contains(nearBottom),
        "hover preview opens above a bottom row and stays clear of the taskbar and right edge");
    var smallWorkArea = new Rect(-110, -150, 460, 310);
    var limited = HoverPreviewBounds(smallWorkArea, 20, popupSize);
    check(!limited.IsEmpty && limited.Width < popupSize.Width && limited.Height < popupSize.Height && smallWorkArea.Contains(limited),
        "hover preview shrinks to fit a small monitor without covering the hovered name");
    var state = new PanelView();
    SavePanelView(state);
    var file = Path.Combine(root, "panel-interactions.xml");
    new ProgramSettings { LeftView = state, HoverFilePreview = false, HoverImagePreview = true, HoverVideoPreview = false }.Save(file);
    var restored = ProgramSettings.Load(file);
    check(!restored.HoverFilePreview && restored.ShowHoverImages && !restored.ShowHoverVideos
        && new ProgramSettings { HoverFilePreview = true }.ShowHoverVideos
        && !new ProgramSettings().ShowHoverImages, "hover options persist independently and preserve legacy preferences");
    RestorePanelView(original);
    RestorePanelView(restored.LeftView);
    check(restored.LeftView.GroupBy == "Extension" && List.Items.Groups != null, "grouping survives the panel settings round trip");
   }
   finally { RestorePanelView(original); }
  }
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
