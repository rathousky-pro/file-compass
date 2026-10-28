using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal static class FooterActions {
    static IEnumerable<Button> Buttons(Panel panel){foreach(UIElement child in panel.Children){var button=child as Button;if(button!=null)yield return button;var nested=child as Panel;if(nested!=null)foreach(var item in Buttons(nested))yield return item;}}
    internal static void Analyzer(StackPanel actions,Button refresh,Button stop){
        var all=Buttons(actions).ToArray();
        var keys=new[]{L.Text("analyzer.openPanel"),L.Text("copy.backgroundAction"),L.Text("action.close")};
        var core=all.Where(x=>x==refresh||x==stop||keys.Contains(Convert.ToString(x.Content))).ToArray();
        var extras=all.Except(core).ToArray();
        foreach(var button in all){var parent=button.Parent as Panel;if(parent!=null)parent.Children.Remove(button);}
        actions.Children.Clear();actions.Orientation=Orientation.Horizontal;actions.HorizontalAlignment=HorizontalAlignment.Right;
        var more=CopyUi.Button(L.Text("action.moreOptions")+" ▾",delegate{});var menu=new ContextMenu();
        foreach(var source in extras){var captured=source;var item=new MenuItem{Header=source.Content};item.SetBinding(UIElement.IsEnabledProperty,new Binding("IsEnabled"){Source=source});item.Click+=delegate{captured.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};menu.Items.Add(item);}
        more.Click+=delegate{menu.PlacementTarget=more;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;menu.IsOpen=true;};more.ContextMenu=menu;actions.Children.Add(more);
        foreach(var button in core.Where(x=>Convert.ToString(x.Content)!=L.Text("action.close")))actions.Children.Add(button);
        foreach(var button in core.Where(x=>Convert.ToString(x.Content)==L.Text("action.close")))actions.Children.Add(button);
        foreach(var button in actions.Children.OfType<Button>()){button.MinWidth=0;button.Padding=new Thickness(10,3,10,3);}
    }
 }
}
