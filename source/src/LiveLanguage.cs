using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
namespace FileCompass {
    internal static class LiveLanguage {
        static readonly DependencyProperty LocalizedTemplateProperty=DependencyProperty.RegisterAttached("LocalizedTemplate",typeof(bool),typeof(LiveLanguage),new PropertyMetadata(false));
        sealed class CatalogTextConverter : IValueConverter {
            public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return value is string?L.TranslateExisting((string)value):value;}
            public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
        }
        static DataTemplate TextTemplate() {
            var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding{Converter=new CatalogTextConverter()});return new DataTemplate{VisualTree=text};
        }
        static void RefreshSelection(DependencyObject node,string selected,DataTemplate template) {
            var presenter=node as ContentPresenter;
            if(presenter!=null&&String.Equals(presenter.Content as string,selected,StringComparison.Ordinal))presenter.SetCurrentValue(ContentPresenter.ContentTemplateProperty,template);
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)RefreshSelection(VisualTreeHelper.GetChild(node,i),selected,template);
        }
        internal static void Refresh(Window root=null) {
            var windows=Application.Current==null?new List<Window>():Application.Current.Windows.Cast<Window>().ToList();
            if(root!=null&&!windows.Contains(root))windows.Add(root);
            var visited=new HashSet<DependencyObject>();
            foreach(var window in windows){Visit(window,visited);var help=window as HelpWindow;if(help!=null)help.RefreshLanguage();var smart=window as DiskSmartWindow;if(smart!=null)smart.RefreshLanguage();var main=window as MainWindow;if(main!=null)main.RefreshLanguageLabels();}
        }
        static void Visit(DependencyObject node,HashSet<DependencyObject> visited) {
            if(node==null||!visited.Add(node))return;
            var properties=new List<DependencyProperty>();var values=node.GetLocalValueEnumerator();while(values.MoveNext())properties.Add(values.Current.Property);
            foreach(var property in properties) {
                if(property.ReadOnly||property==TextBox.TextProperty||property==ComboBox.TextProperty||property==Selector.SelectedItemProperty||property==Selector.SelectedValueProperty||property==FrameworkElement.DataContextProperty||property==FrameworkElement.TagProperty||property==FrameworkElement.NameProperty||property==UIElement.UidProperty)continue;
                if((node is ContentPresenter&&property==ContentPresenter.ContentProperty)||(node is ListBoxItem&&property==ContentControl.ContentProperty))continue;
                string before=node.GetValue(property) as string;if(before==null)continue;
                string after=L.TranslateExisting(before);if(!String.Equals(before,after,StringComparison.Ordinal))node.SetCurrentValue(property,after);
            }
            var element=node as FrameworkElement;if(element!=null)Visit(element.ContextMenu,visited);
            if(node is TextBoxBase||node is PasswordBox)return;
            var list=node as ListView;var grid=list==null?null:list.View as GridView;
            if(grid!=null)foreach(var column in grid.Columns)Visit(column,visited);
            var data=node as DataGrid;if(data!=null)foreach(var column in data.Columns)Visit(column,visited);
            var items=node as ItemsControl;
            if(items!=null) {
                // Translate presentation instead of replacing items: replacement would fire
                // SelectionChanged and could restart a preview or overwrite dialog input.
                if(items.Items.Count>0&&items.Items[0] is string&&(items.ItemTemplate==null||items.ReadLocalValue(ItemsControl.ItemTemplateProperty)==DependencyProperty.UnsetValue||(bool)items.GetValue(LocalizedTemplateProperty))&&String.IsNullOrEmpty(items.DisplayMemberPath)&&String.IsNullOrEmpty(items.ItemStringFormat)&&items.Items.Cast<object>().OfType<string>().Any(L.IsCatalogText)) {
                    items.SetCurrentValue(ItemsControl.ItemTemplateProperty,TextTemplate());items.SetValue(LocalizedTemplateProperty,true);
                }
                // ComboBox caches its selected-item template independently from the dropdown.
                var combo=items as ComboBox;string selected=combo==null?null:combo.SelectedItem as string;
                if(combo!=null&&!combo.IsEditable&&L.IsCatalogText(selected)){combo.ApplyTemplate();RefreshSelection(combo,selected,TextTemplate());}
                // Only catalog-owned string instances are eligible; equal user text is untouched.
                if(items.ItemsSource==null)for(int i=0;i<items.Items.Count;i++) {
                    Visit(items.Items[i] as DependencyObject,visited);
                }
            }
            var logical=LogicalTreeHelper.GetChildren(node).Cast<object>().OfType<DependencyObject>().ToArray();foreach(var child in logical)Visit(child,visited);
            if(node is Visual||node is System.Windows.Media.Media3D.Visual3D)for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Visit(VisualTreeHelper.GetChild(node,i),visited);
        }
    }
    public partial class MainWindow {
        internal void RefreshLanguageLabels(){left.RefreshLanguageLabels();right.RefreshLanguageLabels();UpdatePrompt();UpdateViewToolbarControls();}
    }
    public partial class FilePanel {
        internal void RefreshLanguageLabels(){UpdateStatus();UpdateConnectionButtons();}
    }
}
