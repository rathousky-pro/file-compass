using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
namespace FileCompass {
 internal static class FunctionKeyIcons {
    internal static DataTemplate Caption(string command){
        string lines;
        switch(command){
            case "newFile": lines="M3,1.5 H10 L13,4.5 V14.5 H3 Z M10,1.5 V4.5 H13 M8,7 V12 M5.5,9.5 H10.5";break;
            case "view": lines="M3,1.5 H13 V14.5 H3 Z M5.5,5 H10.5 M5.5,8 H10.5 M5.5,11 H9";break;
            case "edit": lines="M3,10.5 L10.5,3 L13,5.5 L5.5,13 L2,14 Z M9,4.5 L11.5,7";break;
            case "copy": lines="M3,13 L12.5,3.5 M5.5,3.5 H12.5 V10.5";break;
            case "move": lines="M2,5 H14 M10.5,1.5 L14,5 L10.5,8.5 M14,11 H2 M5.5,7.5 L2,11 L5.5,14.5";break;
            case "newFolder": lines="M8,2 V14 M2,8 H14";break;
            case "delete": lines="M2,4 H14 M5.5,4 V2 H10.5 V4 M3.5,4 L4.5,14 H11.5 L12.5,4 M6.5,6.5 V11.5 M9.5,6.5 V11.5";break;
            default: lines="M7,2 H2 V14 H7 M6,8 H14 M10.5,4.5 L14,8 L10.5,11.5";break;
        }
        var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);row.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        var icon=new FrameworkElementFactory(typeof(Path));icon.SetValue(Path.DataProperty,Geometry.Parse(lines));icon.SetValue(Path.StrokeProperty,FluentTheme.Accent);icon.SetValue(Path.StrokeThicknessProperty,1.15);icon.SetValue(Path.StrokeStartLineCapProperty,PenLineCap.Round);icon.SetValue(Path.StrokeEndLineCapProperty,PenLineCap.Round);icon.SetValue(Path.StrokeLineJoinProperty,PenLineJoin.Round);icon.SetValue(Path.StretchProperty,Stretch.Uniform);icon.SetValue(FrameworkElement.WidthProperty,12.0);icon.SetValue(FrameworkElement.HeightProperty,12.0);icon.SetValue(UIElement.OpacityProperty,.78);icon.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,6,0));icon.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);icon.SetValue(UIElement.IsHitTestVisibleProperty,false);row.AppendChild(icon);
        var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetBinding(TextBlock.TextProperty,new Binding());caption.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(caption);
        return new DataTemplate{VisualTree=row};
    }
 }
}
