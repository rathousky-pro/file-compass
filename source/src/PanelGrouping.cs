using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FileCompass
{
    public partial class FilePanel
    {
        string panelGrouping = "";

        void InitializePanelGrouping()
        {
            ScrollViewer.SetCanContentScroll(List, true);
            VirtualizingPanel.SetIsVirtualizing(List, true);
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(List, true);
            VirtualizingPanel.SetVirtualizationMode(List, VirtualizationMode.Recycling);

            var label = new FrameworkElementFactory(typeof(TextBlock));
            var caption = new MultiBinding { StringFormat = "{0} ({1})" };
            caption.Bindings.Add(new Binding("Name"));
            caption.Bindings.Add(new Binding("ItemCount"));
            label.SetBinding(TextBlock.TextProperty, caption);
            label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            label.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 4, 8, 4));

            var header = new FrameworkElementFactory(typeof(Border), "GroupHeading");
            header.SetValue(Border.BackgroundProperty, CompactTheme.PanelSurface);
            header.AppendChild(label);
            var template = new DataTemplate { VisualTree = header };
            var parentGroup = new DataTrigger { Binding = new Binding("Name"), Value = "" };
            parentGroup.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, "GroupHeading"));
            template.Triggers.Add(parentGroup);
            List.GroupStyle.Add(new GroupStyle { HeaderTemplate = template });
        }

        void AddGroupingMenu(ContextMenu menu)
        {
            var group = new MenuItem { Header = L.Text("columns.groupBy") };
            foreach (string key in new[] { "", "Extension", "Modified", "Size" })
            {
                var item = new MenuItem
                {
                    Header = L.Text(key == "" ? "columns.noGrouping" : key == "Extension" ? "columns.groupType" : key == "Modified" ? "column.date" : "column.size"),
                    IsCheckable = true,
                    IsChecked = panelGrouping == key
                };
                item.Click += delegate { panelGrouping = key; RefreshPanelGrouping(); };
                group.Items.Add(item);
            }
            menu.Items.Add(group);
        }

        void RefreshPanelGrouping()
        {
            var collection = CollectionViewSource.GetDefaultView(List.ItemsSource) as ListCollectionView;
            if (collection == null) return;
            var selected = List.SelectedItem;
            bool wasSynchronizing = synchronizingSelection;
            synchronizingSelection = true;
            try
            {
                using (collection.DeferRefresh()) ApplyPanelGrouping(collection);
                if (selected != null && List.Items.Contains(selected))
                {
                    List.SelectedItem = selected;
                    List.ScrollIntoView(selected);
                }
            }
            finally { synchronizingSelection = wasSynchronizing; }
            SyncWindowsSelection();
            UpdateStatus();
        }

        void ApplyPanelGrouping(ListCollectionView collection)
        {
            string mode = displayMode == "Details" ? panelGrouping : "";
            var current = collection.GroupDescriptions.Count == 0 ? null : collection.GroupDescriptions[0] as FileGroup;
            if (current != null ? current.Mode == mode && current.Language == L.Language : String.IsNullOrEmpty(mode)) return;
            collection.GroupDescriptions.Clear();
            if (!String.IsNullOrEmpty(mode)) collection.GroupDescriptions.Add(new FileGroup(mode));
        }

        sealed class FileGroup : GroupDescription
        {
            internal readonly string Mode;
            internal readonly string Language = L.Language;
            readonly string folders = L.Text("columns.groupFolders");
            readonly string noExtension = L.Text("columns.groupNoExtension");
            readonly string unknownDate = L.Text("columns.groupUnknownDate");
            readonly string[] sizes = {
                L.Text("analyzer.size0"), L.Text("analyzer.size1"), L.Text("analyzer.size2"),
                L.Text("analyzer.size3"), L.Text("analyzer.size4"), L.Text("analyzer.size5")
            };

            internal FileGroup(string mode) { Mode = mode; }

            public override object GroupNameFromItem(object item, int level, CultureInfo culture)
            {
                var entry = (Entry)item;
                if (entry.IsParent) return "";
                if (entry.IsDirectory) return folders;
                if (Mode == "Extension") return String.IsNullOrEmpty(entry.Extension) ? noExtension : "." + entry.Extension.ToLowerInvariant();
                if (Mode == "Modified") return entry.ModifiedTime == DateTime.MinValue ? unknownDate : entry.ModifiedTime.ToString("d", CultureInfo.GetCultureInfo(Language));
                long bytes = entry.Bytes;
                return sizes[bytes < 1024 ? 0 : bytes < 1048576 ? 1 : bytes < 104857600 ? 2 : bytes < 1073741824 ? 3 : bytes < 10737418240L ? 4 : 5];
            }
        }
    }
}
