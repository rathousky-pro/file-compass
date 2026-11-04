using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FileCompass
{
    public partial class FilePanel : Border
    {
        CancellationTokenSource navigationCancellation;
        int navigationVersion;
        public bool LocalLoading { get; private set; }
#if APP_TESTS
        internal Func<string,CancellationToken,List<Entry>> LocalRowsForTests;
#endif
        internal event Action LocalNavigationCompleted;
        public Task NavigationTask { get; private set; }
        internal void CancelNavigation() { navigationVersion++; var previous = navigationCancellation; navigationCancellation = null; if (previous != null) previous.Cancel(); LocalLoading = false; List.IsEnabled = true; }
        internal void SavePanelView(PanelView saved) { saved.ExtensionColumn=extensionColumn;saved.AttributesColumn=attributesColumn;saved.SizeColumn=sizeColumn;saved.ModifiedColumn=modifiedColumn;saved.ColumnOrder=view.Columns.Select(ColumnKey).ToArray();saved.MetadataColumns = MetadataColumnKeys; saved.Sort = sortProperty; saved.Descending = descending; saved.Widths = LogicalColumns.Select((x,i) => i>0&&i<5&&!BaseColumnVisible(i)?RememberedWidth(i):x.Width).ToArray(); saved.Display = displayMode; saved.Tree = treeRequested; saved.ThumbnailSize = thumbnailSize; SaveDetailView(saved); }
        internal bool SharedColumnWidths;
        internal event Action<int, double> ColumnWidthChanged;
        internal void SetColumnWidth(int index, double width)
        {
            var columns=LogicalColumns.ToArray();if (index < 0 || index >= columns.Length || Double.IsNaN(width) || Double.IsInfinity(width)) return;
            if(index>0&&index<5&&!BaseColumnVisible(index)){RememberWidth(index,width);preserveColumnWidths=true;return;}
            resizingColumns = true;
            try { columns[index].Width = Math.Max(25, Math.Min(4000, width)); preserveColumnWidths = true; }
            finally { resizingColumns = false; }
            if(!batchColumnWidths)FitColumns(ColumnIdentity(index));
        }
        internal void RestorePanelView(PanelView saved)
        {
            if (saved == null) return;
            extensionColumn=saved.ExtensionColumn;attributesColumn=saved.AttributesColumn;sizeColumn=saved.SizeColumn;modifiedColumn=saved.ModifiedColumn;ApplyBaseColumns();
            SetMetadataColumns(saved.MetadataColumns); if (new[] { "Name", "Extension", "Size", "Modified", "Attributes" }.Contains(saved.Sort) || MetadataColumn.IsSort(saved.Sort)) sortProperty = saved.Sort;
            descending = saved.Descending;
            thumbnailSize = Math.Max(0, Math.Min(2, saved.ThumbnailSize)); SetDisplay(saved.Display ?? "Details"); SetTree(saved.Tree);
            RestoreDetailView(saved);
            batchColumnWidths=true;try{if (saved.Widths != null) for (int i = 0; i < Math.Min(saved.Widths.Length, view.Columns.Count); i++) if ((!SharedColumnWidths || i >= 5 || MetadataColumnKeys.Length > 0 || applyingFolderView) && saved.Widths[i] >= 25 && saved.Widths[i] <= 4000) SetColumnWidth(i, saved.Widths[i]);}finally{batchColumnWidths=false;}
            ApplyColumnOrder(saved.ColumnOrder!=null&&saved.ColumnOrder.Length>0?saved.ColumnOrder:BaseColumnKeys.Concat(MetadataColumnKeys.Select(x=>"Meta:"+x)));FitColumns();Sort();
        }
        public string CurrentPath { get; private set; }
        public readonly ListView List = new ListView();
        readonly TextBox address = new TextBox();
        readonly TextBlock status = new TextBlock(); readonly TextBlock panelRole=new TextBlock{Foreground=FluentTheme.Muted,FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,24,0),Visibility=Visibility.Collapsed}; Border cleanFileFrame,cleanDriveFrame,cleanBreadcrumbFrame; DockPanel cleanBreadcrumbDock;
        readonly TextBlock capacity = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly ComboBox driveSelector = new ComboBox { Width = 58, Height = 24, MinHeight = 0, Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(0, 0, 6, 0) };
        readonly Dictionary<string, Button> driveButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        internal readonly StackPanel connectionButtons = new StackPanel { Orientation = Orientation.Horizontal };
        readonly Stack<string> history = new Stack<string>();
        readonly Stack<string> forwardHistory = new Stack<string>();
        readonly GridView view = new GridView();
        readonly Action<FilePanel> activate;
        string sortProperty = "Name";
        bool descending, selectingDrive, preserveColumnWidths, resizingColumns;
        bool rightDragging, dragMark;Entry rightDragLast;
        readonly HashSet<Entry> dragVisited = new HashSet<Entry>();
        public static readonly DependencyProperty IsActivePanelProperty = DependencyProperty.Register("IsActivePanel", typeof(bool), typeof(FilePanel), new PropertyMetadata(false));
        public bool IsActivePanel { get { return (bool)GetValue(IsActivePanelProperty); } set { SetValue(IsActivePanelProperty, value); } }
        public event EventHandler PathChanged;

        public FilePanel(Action<FilePanel> activatePanel)
        {
            FluentTheme.Ensure();
            activate = activatePanel;
            BorderThickness = new Thickness(2); BorderBrush = CompactTheme.ChromeLine; CornerRadius = new CornerRadius(6); Padding = new Thickness(2);
            CornerRadius = new CornerRadius(6); Padding = new Thickness(0); Margin = new Thickness(0); Background = Brushes.Transparent;
            var grid = new Grid();
            cleanPaneGrid=grid;
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(31) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(39) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            InitializeNavigation(grid);
            var fileFrame = cleanFileFrame = new Border { Tag="panel-surface", Background = CompactTheme.PanelSurface, BorderBrush = CompactTheme.ChromeLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), IsHitTestVisible = false };
            Grid.SetRow(fileFrame, 2); Grid.SetRowSpan(fileFrame, 3); grid.Children.Add(fileFrame);
            var driveGrid = new Grid();
            cleanDriveGrid=driveGrid;
            driveGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) });
            driveGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
            var driveFrame = cleanDriveFrame = new Border { Tag="panel-surface", Child = driveGrid, Background = CompactTheme.PanelSurface, BorderBrush = CompactTheme.ChromeLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(2), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRowSpan(driveFrame, 2); grid.Children.Add(driveFrame);

            var drives = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            InitializeDrives(drives);
            var networkLabel = new StackPanel { Orientation = Orientation.Horizontal };
            networkLabel.Children.Add(DesktopIcons.Toolbar("network", 16));
            networkLabel.Children.Add(new TextBlock { Text = L.Text("network.shortTitle"), VerticalAlignment = VerticalAlignment.Center });
            networkDriveButton=CompactTheme.Button(networkLabel, L.Text("network.browseDevices"), delegate { activate(this); Navigate(NetworkBrowser.Root, true); });drives.Children.Add(networkDriveButton);
            drives.Children.Add(connectionButtons);
            var driveScroll = new ScrollViewer { Content = drives, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var driveLine=new DockPanel();DockPanel.SetDock(driveSelector,Dock.Right);driveLine.Children.Add(driveSelector);driveLine.Children.Add(driveScroll);driveGrid.Children.Add(driveLine);

            driveSelector.SelectionChanged += delegate { if(!selectingDrive && driveSelector.SelectedItem is DriveChoice){var choice=(DriveChoice)driveSelector.SelectedItem;if(choice.IsLocation){activate(this);FocusFiles();Navigate(choice.Root,true);}} };
            DriveSelector.Configure(driveSelector);

            var pathRow = panelPathRow = new DockPanel { Margin = new Thickness(8,0,8,0) };
            address.GotKeyboardFocus += delegate { address.Text = CurrentPath ?? ""; address.SelectAll(); };
            address.LostKeyboardFocus += delegate { ShowPath(); };
            address.BorderThickness = new Thickness(0); address.Padding = new Thickness(6, 3, 6, 3);
            address.VerticalContentAlignment = VerticalAlignment.Center;
            address.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { Navigate(address.Text, true); List.Focus(); e.Handled = true; }
                else if (e.Key == Key.Escape) { address.Text = CurrentPath ?? ""; List.Focus(); e.Handled = true; }
            };
            InitializeAddress(pathRow);pathRow.Height=40;tabRow.Height=40;tabRow.VerticalAlignment=VerticalAlignment.Bottom;
            var pathStack=new StackPanel();cleanBreadcrumbDock=new DockPanel{Tag="panel-breadcrumb-actions",LastChildFill=true};
            cleanRootButton=CompactTheme.Button("\\",L.Text("navigation.driveRootTooltip"),delegate{if(CurrentPath!=null)Navigate(IsShellBrowser?WindowsShell.Computer:IsNetworkBrowser?NetworkBrowser.Root:IsExt?ExtLocations.Root(CurrentPath):IsRemote||IsArchive?"/":Path.GetPathRoot(CurrentPath),true);});
            cleanParentButton=CompactTheme.Button("↑",L.Text("navigation.parentShortcutTooltip"),delegate{Up();});
            System.Windows.Automation.AutomationProperties.SetAutomationId(cleanRootButton,"PanelRoot");System.Windows.Automation.AutomationProperties.SetAutomationId(cleanParentButton,"PanelUp");
            foreach(var action in new[]{cleanRootButton,cleanParentButton}){action.Tag="panel-breadcrumb";action.Width=30;action.Height=30;action.MinWidth=0;action.Margin=new Thickness(1,0,3,0);action.Padding=new Thickness(0);action.Background=CompactTheme.WhiteSurface;action.BorderBrush=CompactTheme.Line;action.BorderThickness=new Thickness(1);action.Foreground=FluentTheme.Accent;DockPanel.SetDock(action,Dock.Right);cleanBreadcrumbDock.Children.Add(action);}
            pathRow.Children.Clear();cleanBreadcrumbDock.Children.Add(tabRow);
            var pathEditor=new Grid();pathEditor.Children.Add(cleanBreadcrumbDock);pathEditor.Children.Add(address);
            cleanBreadcrumbFrame=new Border{Tag="panel-path",Child=pathEditor,Background=CompactTheme.PanelSurface,BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(10),Margin=new Thickness(8,5,8,0),Padding=new Thickness(7,1,7,0),Height=46};
            System.Windows.Automation.AutomationProperties.SetAutomationId(cleanBreadcrumbFrame,"PanelBreadcrumb");cleanBreadcrumbFrame.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){activate(this);if(e.ClickCount==2){FocusAddress();e.Handled=true;}};
            pathStack.Children.Add(cleanBreadcrumbFrame);Grid.SetRow(pathStack,2);grid.Children.Add(pathStack);

            AddColumn(L.Text("column.name"), "Name", 230); AddColumn(L.Text("column.extension"), "Extension", 80);
            AddColumn(L.Text("column.size"), "Size", 90); AddColumn(L.Text("column.date"), "Modified", 130); AddColumn(L.Text("attributes.title"), "Attributes", 70);
            var fillerFrame=new FrameworkElementFactory(typeof(Border));fillerFrame.SetValue(Border.BackgroundProperty,CompactTheme.WhiteSurface);fillerFrame.SetValue(Border.BorderBrushProperty,CompactTheme.Line);fillerFrame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,1));
            var fillerStyle=new Style(typeof(GridViewColumnHeader),Application.Current.Resources[typeof(GridViewColumnHeader)] as Style);fillerStyle.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(GridViewColumnHeader)){VisualTree=fillerFrame}));fillerStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty,34.0));fillerStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));List.Resources[typeof(GridViewColumnHeader)]=fillerStyle;view.ColumnHeaderContainerStyle=fillerStyle;
            view.AllowsColumnReorder=true;List.View = view; List.SelectionMode = SelectionMode.Single;
            List.Margin = new Thickness(1, 0, 1, 0); List.BorderThickness = new Thickness(0, 1, 0, 1); List.BorderBrush = CompactTheme.Line;
            List.Background = CompactTheme.WhiteSurface; List.Foreground = FluentTheme.Text;
            List.FontFamily = new FontFamily("Segoe UI"); List.FontSize = 12;
            ScrollViewer.SetHorizontalScrollBarVisibility(List, ScrollBarVisibility.Disabled);
            List.ItemContainerStyle = RowStyle();InitializeSelectionBrackets();
            List.SizeChanged += delegate { ResizeColumns(); };List.Loaded+=delegate{ObserveColumnViewport();};
            List.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (windowsSelection) return;
                var row = RowAt(e.GetPosition(List)); if (row == null) return;
                var entry = (Entry)row.Content;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) MarkRange(List.SelectedItem as Entry, entry);
                else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) ToggleMark(entry);
                List.SelectedItem = entry; row.Focus(); e.Handled = true;
                if (e.ClickCount == 2) OpenSelected();
            };
            List.PreviewMouseRightButtonDown+=delegate(object sender,MouseButtonEventArgs e){var header=FileColumnHeader(e.OriginalSource as DependencyObject);if(header!=null){CancelFileContextGesture();if(header.ContextMenu==null)header.ContextMenu=MetadataColumnMenu("Name");header.ContextMenu.PlacementTarget=header;header.ContextMenu.IsOpen=true;e.Handled=true;return;}BeginFileContextGesture(e);};
            List.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){ContinueFileContextGesture(e);};
            List.PreviewMouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(FileColumnHeader(e.OriginalSource as DependencyObject)!=null){e.Handled=true;return;}EndFileContextGesture(e);};
            List.LostMouseCapture+=delegate{CancelFileContextGesture();};
            // ListViewItem handles Space before a bubbling KeyDown reaches the list.
            // Handle file-manager commands on the tunnelling route instead.
            List.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if(HandleQuickSearchKey(e))return;
                if (e.Key == Key.Insert || e.Key == Key.Space)
                {
                    var item = List.SelectedItem as Entry; ToggleMark(item);
                    if (e.Key == Key.Space && item != null && item.IsDirectory && !item.IsParent) BeginFolderSize(item);
                    if (e.Key == Key.Insert && List.SelectedIndex < List.Items.Count - 1) List.SelectedIndex++;
                    if (List.SelectedItem != null) List.ScrollIntoView(List.SelectedItem);
                    e.Handled = true;
                }
                if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control) { MarkAll(true); e.Handled = true; }
                if (e.Key == Key.Multiply) { InvertMarks(); e.Handled = true; }
                if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; }
                if (e.Key == Key.Back) { Up(); e.Handled = true; }
                if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) { Back(); e.Handled = true; }
                if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt) { Forward(); e.Handled = true; }
            };
            List.SelectionChanged += delegate { SyncWindowsMarks(); UpdateStatus(); };
            List.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e) { if (windowsSelection && e.ChangedButton == MouseButton.Left && RowAt(e.GetPosition(List)) != null) { OpenSelected(); e.Handled = true; } };
            Grid.SetRow(List, 3); grid.Children.Add(List);
            status.Margin = new Thickness(9, 3, 5, 0); status.Foreground = FluentTheme.Muted; status.FontSize = 11;
            System.Windows.Automation.AutomationProperties.SetAutomationId(status,"PanelStatus");System.Windows.Automation.AutomationProperties.SetAutomationId(panelRole,"PanelRole");var statusRow=new Grid();statusRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});statusRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});capacity.Margin=new Thickness(8,0,12,0);capacity.VerticalAlignment=VerticalAlignment.Center;capacity.HorizontalAlignment=HorizontalAlignment.Right;capacity.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(capacity,1);status.TextTrimming=TextTrimming.CharacterEllipsis;statusRow.Children.Add(capacity);statusRow.Children.Add(status);Grid.SetRow(statusRow,4);grid.Children.Add(statusRow);
            PreviewGotKeyboardFocus += delegate { activate(this); };
            PreviewMouseDown += delegate { activate(this); };
            InitializePanelTools();
            InitializeDisplay(grid); InitializeMetadataColumns();
            InitializeFileDetail(grid);
        }

        Style RowStyle()
        {
            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0)));style.Setters.Add(new Setter(FrameworkElement.UseLayoutRoundingProperty,true));style.Setters.Add(new Setter(UIElement.SnapsToDevicePixelsProperty,true));
            style.Setters.Add(new Setter(BracketOwnerProperty,this));style.Setters.Add(new Setter(BracketMarkedProperty,new Binding("IsMarked")));style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent,new RoutedEventHandler(delegate{QueueSelectionBrackets();})));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, Math.Max(27, List.FontSize * 1.65 + 6)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, FluentTheme.Text));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            var border = new FrameworkElementFactory(typeof(Border),"RowFrame");border.SetValue(UIElement.SnapsToDevicePixelsProperty,true);
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderThicknessProperty, new Thickness(3, 0, 0, 0));border.SetBinding(Border.CornerRadiusProperty,new Binding{Path=new PropertyPath(BracketCornersProperty),RelativeSource=RelativeSource.TemplatedParent});
            var presenter = new FrameworkElementFactory(typeof(GridViewRowPresenter));
            presenter.SetBinding(GridViewRowPresenter.ContentProperty, new Binding());
            presenter.SetBinding(GridViewRowPresenter.ColumnsProperty, new Binding("View.Columns") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1) });
            border.AppendChild(presenter);
            var rowTemplate=new ControlTemplate(typeof(ListViewItem)){VisualTree=border};style.Setters.Add(new Setter(Control.TemplateProperty,rowTemplate));
            var cursor = new MultiDataTrigger();
            cursor.Conditions.Add(new Condition(new Binding("IsSelected") { RelativeSource = RelativeSource.Self }, true));
            cursor.Conditions.Add(new Condition(new Binding("IsActivePanel") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FilePanel), 1) }, true));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#F0F3F8"))); style.Triggers.Add(hover);
            cursor.Setters.Add(new Setter(Control.BackgroundProperty, CompactTheme.ThemeBrush("#E4F0FF")));
            cursor.Setters.Add(new Setter(Control.BorderBrushProperty, FluentTheme.Accent)); style.Triggers.Add(cursor);
            var marked = new DataTrigger { Binding = new Binding("IsMarked"), Value = true };
            marked.Setters.Add(new Setter(Control.BorderBrushProperty,FluentTheme.Accent));
            marked.Setters.Add(new Setter(Control.ForegroundProperty, CompactTheme.ThemeBrush("#B92331")));
            marked.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold)); style.Triggers.Add(marked);
            return style;
        }

        ListViewItem RowAt(Point point)
        {
            var hit = List.InputHitTest(point) as DependencyObject;
            return hit == null ? null : ItemsControl.ContainerFromElement(List, hit) as ListViewItem;
        }
        void MarkDragged(Entry entry)
        {
            if (dragVisited.Add(entry) && !entry.IsParent) entry.IsMarked = dragMark;
            rightDragLast=entry;FocusRightEntry(entry);SyncWindowsSelection(); UpdateStatus();
        }
        internal void BeginRightMark(Entry entry)
        {
            RememberSelection();rightDragging = true; dragMark = !entry.IsMarked; dragVisited.Clear(); MarkDragged(entry);
        }
        internal void ContinueRightMark(Entry entry)
        {
            if (!rightDragging) return;
            int from = List.Items.IndexOf(rightDragLast), to = List.Items.IndexOf(entry);
            if (from >= 0 && to >= 0)
                for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++) MarkDragged((Entry)List.Items[i]);
            MarkDragged(entry);
        }
        internal void EndRightMark() { rightDragging = false; rightDragLast=null;dragVisited.Clear(); }
        public void ToggleMark(Entry entry) { RememberSelection(); if (entry != null && !entry.IsParent) entry.IsMarked = !entry.IsMarked; SyncWindowsSelection(); UpdateStatus(); }
        public void MarkAll(bool marked) { RememberSelection(); foreach (Entry entry in List.Items) entry.IsMarked = marked; SyncWindowsSelection(); UpdateStatus(); }
        public void InvertMarks() { RememberSelection(); foreach (Entry entry in List.Items) if (!entry.IsParent) entry.IsMarked = !entry.IsMarked; SyncWindowsSelection(); UpdateStatus(); }
        void MarkRange(Entry start, Entry end)
        {
            RememberSelection();
            int a = List.Items.IndexOf(start), b = List.Items.IndexOf(end); if (a < 0 || b < 0) return;
            for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) ((Entry)List.Items[i]).IsMarked = true;
            UpdateStatus();
        }

        void AddColumn(string caption, string property, double width)
        {
            var header = new GridViewColumnHeader { Content = caption, Tag = property, HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0, 0, 0), Height = 34, Background = CompactTheme.WhiteSurface, Foreground = FluentTheme.Muted,
                FontSize = 12, FontWeight = FontWeights.Normal,
                BorderBrush = CompactTheme.ChromeLine, BorderThickness = new Thickness(0, 0, 1, 1) };
            var border = new FrameworkElementFactory(typeof(Border),"ColumnFrame");
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            border.SetValue(Border.BorderBrushProperty, CompactTheme.Line); border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 1));border.SetValue(FrameworkElement.MarginProperty,new Thickness(0,5,0,0));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(FrameworkElement.MarginProperty, new Thickness(8,0,0,0));presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(presenter);
            var headerGrid = new FrameworkElementFactory(typeof(Grid)); headerGrid.AppendChild(border);
            var grip = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Thumb), "PART_HeaderGripper");
            grip.SetValue(FrameworkElement.WidthProperty, 7.0);
            grip.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            grip.SetValue(FrameworkElement.CursorProperty, Cursors.SizeWE);
            var gripBorder = new FrameworkElementFactory(typeof(Border)); gripBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            grip.SetValue(Control.TemplateProperty, new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb)) { VisualTree = gripBorder });
            headerGrid.AppendChild(grip);
                        var headerTemplate=new ControlTemplate(typeof(GridViewColumnHeader)){VisualTree=headerGrid};var cleanHeader=new DataTrigger{Binding=new Binding("Clean"){Source=InterfaceTheme.State},Value=true};cleanHeader.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(0,0,1,1),"ColumnFrame"));cleanHeader.Setters.Add(new Setter(Border.BorderBrushProperty,CompactTheme.ThemeBrush("#C6D8F5"),"ColumnFrame"));headerTemplate.Triggers.Add(cleanHeader);header.Template=headerTemplate;
            header.ContextMenu = MetadataColumnMenu(property);header.PreviewMouseRightButtonDown+=delegate(object sender,MouseButtonEventArgs e){CancelFileContextGesture();header.ContextMenu.PlacementTarget=header;header.ContextMenu.IsOpen=true;e.Handled=true;};header.PreviewMouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;}; header.Click += delegate { if (sortProperty == property) descending = !descending; else { sortProperty = property; descending = false; } Sort(); if (MetadataColumn.IsSort(property)) LoadAllColumnMetadata(); };
            var column = new GridViewColumn { Header = header, Width = width };
            var text = new FrameworkElementFactory(typeof(TextBlock));
                        if(property=="Name"){var nameBinding=new MultiBinding{Converter=new PanelNameConverter()};nameBinding.Bindings.Add(new Binding("ListName"));nameBinding.Bindings.Add(new Binding("FullName"));nameBinding.Bindings.Add(new Binding("IsDirectory"));nameBinding.Bindings.Add(new Binding("LocationName"));nameBinding.Bindings.Add(new Binding("Clean"){Source=InterfaceTheme.State});nameBinding.Bindings.Add(new Binding("SeparateExtension"){Source=this});text.SetBinding(TextBlock.TextProperty,nameBinding);}
                        else if(property=="Size"||property=="Modified"){var valueBinding=new MultiBinding{Converter=new PanelValueConverter(),ConverterParameter=property=="Size"?"size":"date"};valueBinding.Bindings.Add(new Binding(property));valueBinding.Bindings.Add(new Binding("ModifiedTime"));valueBinding.Bindings.Add(new Binding("Bytes"));valueBinding.Bindings.Add(new Binding("IsDirectory"));valueBinding.Bindings.Add(new Binding("Clean"){Source=InterfaceTheme.State});text.SetBinding(TextBlock.TextProperty,valueBinding);text.SetBinding(FrameworkElement.ToolTipProperty,new Binding(property));}
            else text.SetBinding(TextBlock.TextProperty,new Binding(property.StartsWith("Meta:")?"MetadataValues["+property.Substring(5)+"]":property));
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            text.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 3, 0));
            if (property == "Size" || property == "Modified") {var alignment=new Style(typeof(TextBlock));alignment.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,TextAlignment.Right));var cleanAlign=new DataTrigger{Binding=new Binding("Clean"){Source=InterfaceTheme.State},Value=true};cleanAlign.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,TextAlignment.Right));alignment.Triggers.Add(cleanAlign);text.SetValue(FrameworkElement.StyleProperty,alignment);}
            if (property == "Name")
            {
                text.SetBinding(FrameworkElement.ToolTipProperty, new Binding("NameToolTip"));
                var dock = new FrameworkElementFactory(typeof(DockPanel));
                var icon = new FrameworkElementFactory(typeof(Image));
                icon.SetBinding(Image.SourceProperty, new Binding("Icon")); icon.SetValue(FrameworkElement.WidthProperty, 16.0); icon.SetValue(FrameworkElement.HeightProperty, 16.0);
                icon.SetValue(FrameworkElement.MarginProperty, new Thickness(5, 0, 7, 0));
                icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                icon.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);
                icon.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
                icon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
                dock.AppendChild(icon);
                var tags = new FrameworkElementFactory(typeof(TextBlock)); tags.SetBinding(TextBlock.TextProperty, new Binding("TagsLabel")); tags.SetValue(DockPanel.DockProperty, Dock.Right); tags.SetValue(TextBlock.ForegroundProperty, FluentTheme.Accent); tags.SetValue(TextBlock.FontSizeProperty, 11.0); tags.SetValue(FrameworkElement.MaxWidthProperty, 130.0); tags.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); tags.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 0, 6, 0)); tags.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); dock.AppendChild(tags);
                var percent = new FrameworkElementFactory(typeof(TextBlock)); percent.SetBinding(TextBlock.TextProperty, new Binding("DiskPercentText")); percent.SetBinding(UIElement.VisibilityProperty, new Binding("DiskUsageVisibility")); percent.SetValue(DockPanel.DockProperty, Dock.Right); percent.SetValue(FrameworkElement.MarginProperty, new Thickness(5,0,6,0)); percent.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); percent.SetBinding(FrameworkElement.ToolTipProperty, new Binding("DiskUsageNote")); dock.AppendChild(percent);
                dock.AppendChild(text);
                var usage = new FrameworkElementFactory(typeof(ProgressBar)); usage.SetValue(System.Windows.Controls.Primitives.RangeBase.MaximumProperty, 100.0); usage.SetBinding(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new Binding("DiskPercent") { Mode = BindingMode.OneWay }); usage.SetBinding(UIElement.VisibilityProperty, new Binding("DiskUsageVisibility")); usage.SetValue(FrameworkElement.HeightProperty, 21.0); usage.SetValue(Control.ForegroundProperty, CompactTheme.ThemeBrush("#DCEBFF")); usage.SetValue(Control.BackgroundProperty, Brushes.Transparent); usage.SetValue(Control.BorderThicknessProperty, new Thickness(0)); usage.SetValue(UIElement.IsHitTestVisibleProperty, false);
                var nameGrid = new FrameworkElementFactory(typeof(Grid)); nameGrid.AppendChild(usage); nameGrid.AppendChild(dock);
                column.CellTemplate = new DataTemplate { VisualTree = nameGrid };
            }
            else column.CellTemplate = new DataTemplate { VisualTree = text };
            view.Columns.Add(column);
            DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn)).AddValueChanged(column, delegate {
                if (resizingColumns) return;
                preserveColumnWidths = true;FitColumns(ColumnKey(column));
                if (ColumnWidthChanged != null) ColumnWidthChanged(LogicalColumns.ToList().IndexOf(column), column.Width);
            });
        }

        void ResizeColumns()
        {
            if (preserveColumnWidths || view.Columns.Count != 5){FitColumns();return;}
            double available = Math.Max(AppearanceTheme.IsClean?200:480, List.ActualWidth - 24);
            double[] ratios = { .50, .15, .08, .13, .14 };
            if (available < 850) ratios = new double[] { .39, .10, .14, .24, .13 };
            if(AppearanceTheme.IsClean)ratios=new double[]{.66,BaseColumnVisible(1)?.10:0,.16,.18,BaseColumnVisible(4)?.10:0};
            for(int i=1;i<5;i++)if(!BaseColumnVisible(i))ratios[i]=0;
            double total=ratios.Sum();ratios=ratios.Select(x=>x/total).ToArray();
            resizingColumns = true;
            try {
                var widths=ratios.Select(x=>Math.Floor(available*x)).ToArray();
                if(AppearanceTheme.IsClean)foreach(int index in new[]{1,4})if(BaseColumnVisible(index)){double extra=Math.Max(0,(index==1?80:90)-widths[index]);widths[index]+=extra;widths[0]=Math.Max(140,widths[0]-extra);}
                for(int i=0;i<5;i++)BaseColumn(i).Width=widths[i];
            }
            finally { resizingColumns = false; }
            FitColumns();
        }

        void Sort()
        {
            if (List.ItemsSource != null) foreach (Entry entry in List.ItemsSource) entry.ApplyNameSettings(folderBrackets, fullNameTooltips);
            var collection = CollectionViewSource.GetDefaultView(List.ItemsSource) as ListCollectionView;
            if (collection != null) { collection.CustomSort = new EntryComparer(sortProperty, descending, naturalSorting, foldersAlwaysByName); collection.Filter = MatchesFilter; }
            foreach (GridViewColumn column in view.Columns)
            {
                var header = (GridViewColumnHeader)column.Header;
                string label = MetadataColumn.IsSort((string)header.Tag) ? MetadataColumn.Caption(((string)header.Tag).Substring(5)) : (string)header.Tag == "Name" ? L.Text("column.name") : (string)header.Tag == "Extension" ? L.Text("column.extension") : (string)header.Tag == "Size" ? L.Text("column.size") : (string)header.Tag == "Modified" ? L.Text("column.date") : L.Text("attributes.title");
                header.Content = AppearanceTheme.IsClean ? label.ToUpper(System.Globalization.CultureInfo.CurrentCulture)+((string)header.Tag==sortProperty?(descending?" ↓":" ↑"):"") : ((string)header.Tag == sortProperty ? (descending ? "▼ " : "▲ ") : "") + label;
                header.ToolTip=label;
                header.Background = !AppearanceTheme.IsClean && IsActivePanel && (string)header.Tag == sortProperty ? CompactTheme.SortedHeader : CompactTheme.WhiteSurface;
            }
        }

        public void Highlight(bool active) { bool changed=IsActivePanel!=active;IsActivePanel = active;panelRole.Text=L.Text(active?"panel.activeRole":"panel.targetRole"); BorderThickness=new Thickness(AppearanceTheme.IsClean?(active?3:2):AppearanceTheme.IsBalanced?1:2);BorderBrush = AppearanceTheme.IsClean ? active?CompactTheme.ThemeBrush("#146DCC"):CompactTheme.ThemeBrush("#C6D8F5") : active ? (AppearanceTheme.IsBalanced ? CompactTheme.ThemeBrush("#79A9DF") : FluentTheme.Accent) : CompactTheme.ChromeLine; address.Background = active ? CompactTheme.ActivePath : CompactTheme.InactivePath; crumbs.Background = address.Background; RefreshCleanNavigation(); foreach (GridViewColumn column in view.Columns) { var header = column.Header as GridViewColumnHeader; if (header != null) header.Background = !AppearanceTheme.IsClean && active && (string)header.Tag == sortProperty ? CompactTheme.SortedHeader : CompactTheme.WhiteSurface; } address.Foreground = active ? FluentTheme.Accent : FluentTheme.Text; crumbs.SetValue(TextBlock.ForegroundProperty, active ? FluentTheme.Accent : FluentTheme.Text);if(changed)UpdateStatus(); }
        void ShowPath() { address.Text = CurrentPath == null ? "" : IsRemote || IsArchive || IsExt ? DisplayPath : CurrentPath.TrimEnd('\\') + "\\*.*"; }
        public void Navigate(string path, bool remember)
        {
            if (IsLoading) return;
            EmptyPanel=false;projection = null;
            if(ExtLocations.IsPath(path)){NavigationTask=NavigateExt(path,remember);return;}
            if (WindowsShell.IsLocation(path)) { NavigationTask = NavigateShell(path, remember); return; }
            if (NetworkBrowser.IsLocation(path)) { NavigationTask = NavigateNetwork(path, remember); return; }
            if (IsLoading) return;
            if (remember && tabs.Count > 0 && tabs[tabIndex].Pending && !switchingTabs) tabs[tabIndex].Pending = false;
            if (IsArchive && (path.StartsWith("/") || path == "archive-exit:")) { NavigateArchive(path, remember); return; }
            if (IsRemote && !System.IO.Path.IsPathRooted(path.Replace('/', '\\')) && path.IndexOf(':') < 0)
            { NavigateRemote(RemoteConnection.Combine(CurrentPath, path), remember); return; }
            if (IsRemote && (path.StartsWith("/") || path.StartsWith(Remote.Display("/"))))
            { NavigateRemote(path.StartsWith("/") ? path : path.Substring(Remote.Display("/").Length - 1), remember); return; }
            NavigationTask = NavigateLocal(path, remember);
        }
        async Task NavigateLocal(string path, bool remember)
        {
            CancelNavigation(); var cancellation = new CancellationTokenSource(); navigationCancellation = cancellation;
            int version = navigationVersion; var token = cancellation.Token;
            bool restoreListFocus = List.IsKeyboardFocusWithin;
            LocalLoading = true; bool backgroundRefresh = !IsRemote && !IsArchive && String.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase);
            if (!backgroundRefresh) { List.IsEnabled = false; status.Text = L.Text("panel.loadingFolder"); }
            var focusAfterDisable = Keyboard.FocusedElement;
            Exception loadError = null; Task pendingLoad = null;
            try
            {
                if (path.EndsWith("\\*.*")) path = path.Substring(0, path.Length - 3);
                path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                var keepMarks = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string keepCursor = null; double keepScroll = CaptureScroll(); bool refreshingSameFolder = String.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase);
                if (!refreshingSameFolder && CurrentPath != null && !IsRemote && !IsArchive && !IsVirtualBrowser) {
                    var parent = new DirectoryInfo(CurrentPath).Parent;
                    if (parent != null && String.Equals(parent.FullName.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) keepCursor = CurrentPath.TrimEnd('\\');
                }
                if (String.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Entry old in List.Items) if (old.IsMarked) keepMarks.Add(old.FullPath);
                    var cursor = List.SelectedItem as Entry; if (cursor != null) keepCursor = cursor.FullPath;
                }
                var load = Task.Factory.StartNew(delegate
                {
#if APP_TESTS
                if(LocalRowsForTests!=null)return LocalRowsForTests(path,token);
#endif
                var folder = new DirectoryInfo(path);
                var rows = new List<Entry>();
                if (folder.Parent != null) rows.Add(new Entry { Name = "[..]", FullPath = folder.Parent.FullName, IsDirectory = true, IsParent = true, Icon = DesktopIcons.FileIcon("", true) });
                foreach (var child in folder.EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();
                    bool dir = (child.Attributes & FileAttributes.Directory) != 0;
                    string extension = dir || child.Name.LastIndexOf('.') <= 0 ? "" : Path.GetExtension(child.Name);
                    rows.Add(new Entry { Name = dir ? "[" + child.Name + "]" : extension.Length == 0 ? child.Name : Path.GetFileNameWithoutExtension(child.Name),
                        Extension = extension.TrimStart('.'), FullPath = child.FullName, IsDirectory = dir,
                        IsHidden = (child.Attributes & FileAttributes.Hidden) != 0, IsSystem = (child.Attributes & FileAttributes.System) != 0,
                        Icon = DesktopIcons.FileIcon(child.FullName, dir), IsLink = (child.Attributes & FileAttributes.ReparsePoint) != 0,
                        IsMarked = keepMarks.Contains(child.FullName),
                        Bytes = dir ? 0 : ((FileInfo)child).Length, ModifiedTime = child.LastWriteTime,
                        Modified = child.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), Attributes = AttributeText(child.Attributes) });
                }
                token.ThrowIfCancellationRequested(); FileTagStore.Apply(rows); return rows;
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                pendingLoad = load;
                if (await Task.WhenAny(load, Task.Delay(TimeSpan.FromSeconds(15), token)).ConfigureAwait(false) != load)
                { cancellation.Cancel(); throw new IOException(L.Text("panel.folderNotResponding")); }
                var loadedRows = await load.ConfigureAwait(false);
                await Dispatcher.InvokeAsync(delegate
                {
                    if (version != navigationVersion || token.IsCancellationRequested) return;
                if (IsArchive) { CloseArchiveContext(); history.Clear(); forwardHistory.Clear(); }
                if (IsRemote) { RememberRemote(); Remote = null; history.Clear(); forwardHistory.Clear(); }
                if (remember && CurrentPath != null && !String.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase) && !CurrentPath.StartsWith("/")) { history.Push(CurrentPath); forwardHistory.Clear(); }
                CurrentPath = path; ShowPath();
                var stableRows = refreshingSameFolder ? List.ItemsSource as System.Collections.ObjectModel.ObservableCollection<Entry> : null;
                if (stableRows != null) { int oldIndex = List.SelectedIndex; MergeLocalRows(stableRows, loadedRows); if (List.SelectedItem == null && List.Items.Count > 0) List.SelectedIndex = Math.Max(0, Math.Min(oldIndex, List.Items.Count - 1)); }
                else { List.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Entry>(loadedRows); Sort(); }
                if (stableRows == null && loadedRows.Count > 0) List.SelectedIndex = 0;
                if (keepCursor != null) { var cursor = List.Items.Cast<Entry>().FirstOrDefault(x => String.Equals(x.FullPath.TrimEnd('\\'), keepCursor.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)); if (cursor != null && !Object.ReferenceEquals(List.SelectedItem, cursor)) { List.SelectedItem = cursor; List.ScrollIntoView(cursor); } }
                UpdateDiskInfo(); UpdateStatus(); if (refreshingSameFolder) RestoreScroll(keepScroll, version);
                UpdateConnectionButtons();
                if (PathChanged != null) PathChanged(this, EventArgs.Empty);
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { loadError = ex; }
            await Dispatcher.InvokeAsync(delegate
            {
                if (version != navigationVersion) return;
                if (loadError != null) { address.Text = CurrentPath ?? ""; status.Text = L.Text("panel.openFolderFailedPrefix") + loadError.Message; status.ToolTip = status.Text; if(tabs.Count>0&&(switchingTabs||tabs[tabIndex].Pending||String.Equals(path,CurrentPath,StringComparison.OrdinalIgnoreCase)))ShowTabUnavailable(tabs[tabIndex],LocalTabError(path,loadError)); }
                LocalLoading = false; List.IsEnabled = true; navigationCancellation = null;
                if (loadError == null && LocalNavigationCompleted != null) LocalNavigationCompleted();
                var owner = Window.GetWindow(this);
                // Removing the disabled old row can leave keyboard focus on the owner window.
                // Restore the selected new row unless focus moved to another control/panel.
                var focused = Keyboard.FocusedElement;
                if (restoreListFocus && IsActivePanel && (owner == null || owner.IsActive) && (focused == focusAfterDisable || focused == null || focused == owner || List.IsKeyboardFocusWithin)) FocusFiles();
            });
            if (pendingLoad == null || pendingLoad.IsCompleted) cancellation.Dispose();
            else { var observe = pendingLoad.ContinueWith(delegate(Task completed) { var observed = completed.Exception; cancellation.Dispose(); }, TaskScheduler.Default); }
        }

        static bool SameLocalRow(Entry a, Entry b)
        {
            return a.Name == b.Name && a.Extension == b.Extension && a.IsDirectory == b.IsDirectory && a.IsParent == b.IsParent && a.IsLink == b.IsLink && a.IsHidden == b.IsHidden && a.IsSystem == b.IsSystem && (a.IsDirectory || a.Bytes == b.Bytes) && a.ModifiedTime == b.ModifiedTime && a.Attributes == b.Attributes && a.TagNames.SequenceEqual(b.TagNames);
        }
        void MergeLocalRows(System.Collections.ObjectModel.ObservableCollection<Entry> rows, List<Entry> loaded)
        {
            var incoming = loaded.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);
            for (int i = rows.Count - 1; i >= 0; i--) {
                Entry next;
                if (!incoming.TryGetValue(rows[i].FullPath, out next)) rows.RemoveAt(i);
                else { incoming.Remove(rows[i].FullPath); if (!SameLocalRow(rows[i], next)) { next.IsMarked = rows[i].IsMarked; next.ApplyNameSettings(folderBrackets, fullNameTooltips); rows[i] = next; } }
            }
            foreach (var entry in loaded) if (incoming.ContainsKey(entry.FullPath)) { entry.ApplyNameSettings(folderBrackets, fullNameTooltips); rows.Add(entry); }
        }
        internal void FocusFiles()
        {
            List.Focus();
            if (List.SelectedItem == null) return;
            List.ScrollIntoView(List.SelectedItem); List.UpdateLayout();
            var row = List.ItemContainerGenerator.ContainerFromItem(List.SelectedItem) as ListBoxItem;
            if (row != null) row.Focus();
        }
        void UpdateDriveSelection()
        {
            if(IsExt){selectingDrive=true;DriveChoice ext;driveSelector.SelectedItem=extChoices.TryGetValue(ExtLocations.Root(CurrentPath),out ext)?ext:null;selectingDrive=false;return;}
            string root = Path.GetPathRoot(CurrentPath);
            selectingDrive = true; driveSelector.SelectedItem = CommonLocations.Match(driveSelector.Items.Cast<DriveChoice>(),CurrentPath); selectingDrive = false;
            foreach (var pair in driveButtons)
            {
                bool selected = String.Equals(pair.Key, root, StringComparison.OrdinalIgnoreCase);
                pair.Value.Background = selected ? CompactTheme.ThemeBrush("#D0E7FA") : CompactTheme.WhiteSurface;
                pair.Value.BorderBrush = selected ? CompactTheme.ThemeBrush("#89BFE5") : CompactTheme.Line;
            }
        }
        void UpdateDiskInfo()
        {
            UpdateDriveSelection();
            if(IsExt){selectingDrive=true;DriveChoice ext;driveSelector.SelectedItem=extChoices.TryGetValue(ExtLocations.Root(CurrentPath),out ext)?ext:null;selectingDrive=false;return;}
            string root = Path.GetPathRoot(CurrentPath);
            capacity.Text = root + L.Text("drive.loadingCapacity"); string location = CurrentPath;
            Task.Run(delegate
            {
                string text;
                try { var drive = new DriveInfo(root); text = "[" + drive.VolumeLabel + "]  " + ByteSize.Format(drive.AvailableFreeSpace) + L.Text("drive.freeAndTotalCapacitySeparator") + ByteSize.Format(drive.TotalSize); }
                catch { text = root + L.Text("drive.capacityUnavailable"); }
                Dispatcher.BeginInvoke(new Action(delegate { if (CurrentPath == location) { capacity.Text = text; capacity.ToolTip = text; } }));
            });
        }

        static string AttributeText(FileAttributes attributes)
        {
            return ((attributes & FileAttributes.ReadOnly) != 0 ? "r" : "-") + ((attributes & FileAttributes.Archive) != 0 ? "a" : "-") +
                ((attributes & FileAttributes.Hidden) != 0 ? "h" : "-") + ((attributes & FileAttributes.System) != 0 ? "s" : "-");
        }
        public void Refresh() { if (projection != null) { NavigationTask = LoadProjection(projection); return; } if (IsArchive) { RefreshArchive(); return; } if (CurrentPath != null) Navigate(CurrentPath, false); }
        public async void Back() { if (IsArchive && history.Count == 0 && ArchivePrefix.Length == 0) { ExitArchive(); return; } if (IsRemote) { await RemoteHistory(false); return; } if (history.Count > 0) { string previous = CurrentPath; string expected = history.Peek(); Navigate(expected, false); NavigationTask = CompleteHistory(NavigationTask, previous, expected, history, forwardHistory); await NavigationTask; } }
        public async void Forward() { if (IsRemote) { await RemoteHistory(true); return; } if (forwardHistory.Count > 0) { string previous = CurrentPath; string expected = forwardHistory.Peek(); Navigate(expected, false); NavigationTask = CompleteHistory(NavigationTask, previous, expected, forwardHistory, history); await NavigationTask; } }
        async Task CompleteHistory(Task navigation, string previous, string expected, Stack<string> from, Stack<string> to)
        {
            if (navigation != null) await navigation.ConfigureAwait(false);
            await Dispatcher.InvokeAsync(delegate { if (CurrentPath == expected && CurrentPath != previous && from.Count > 0 && from.Peek() == expected) { from.Pop(); to.Push(previous); } });
        }
        public void Up() { if(IsExt){string relative=ExtLocations.Relative(CurrentPath);if(relative!="/")Navigate(ExtLocations.Root(CurrentPath)+relative.Substring(0,relative.LastIndexOf('/')).TrimStart('/'),true);return;} if (IsShellBrowser) { if (CurrentPath != WindowsShell.Computer) Navigate(WindowsShell.Computer, true); return; } if (IsNetworkBrowser) { if (CurrentPath != NetworkBrowser.Root) Navigate(NetworkBrowser.Root, true); return; } if (IsArchive) { Navigate(ArchivePrefix.Length == 0 ? "archive-exit:" : "/" + (ArchivePrefix.Contains("/") ? ArchivePrefix.Substring(0, ArchivePrefix.LastIndexOf('/')) : ""), true); return; } if (IsRemote) { Navigate(RemoteConnection.Parent(CurrentPath), true); return; } if (CurrentPath != null) { var parent = Directory.GetParent(CurrentPath); if (parent != null) Navigate(parent.FullName, true); else if (CurrentPath.StartsWith("\\\\")) Navigate("\\\\" + CurrentPath.Trim('\\').Split('\\')[0], true); } }
        public List<Entry> Selected()
        {
            if (LocalLoading || IsShellBrowser) return new List<Entry>();
            var marked = List.Items.Cast<Entry>().Where(x => x.IsMarked && !x.IsParent).ToList();
            if (marked.Count > 0) return marked;
            return List.SelectedItems.Cast<Entry>().Where(x => !x.IsParent).ToList();
        }
        void UpdateStatus()
        {
            QueueSelectionBrackets();SyncWindowsSelection();
            var all = List.Items.Cast<Entry>().Where(x => !x.IsParent).ToList();
            var marked = all.Where(x => x.IsMarked).ToList();
            string detailed = L.Text("panel.selectionSizePrefix") + ByteSize.Format(marked.Sum(x => (decimal)x.Bytes)) + " / " + ByteSize.Format(all.Sum(x => (decimal)x.Bytes)) +
                L.Text("panel.fileCountSeparator") + marked.Count(x => !x.IsDirectory) + " / " + all.Count(x => !x.IsDirectory) + L.Text("panel.folderCountSeparator") + marked.Count(x => x.IsDirectory) + " / " + all.Count(x => x.IsDirectory);
            int selected=marked.Count>0?marked.Count:IsActivePanel&&List.SelectedItem is Entry&&!((Entry)List.SelectedItem).IsParent?1:0;status.Text=AppearanceTheme.IsClean?L.Format("panel.cleanSummary",selected,all.Count):detailed;status.ToolTip=detailed;
            if(PreviewSelectionChanged!=null)PreviewSelectionChanged();
        }
        public void OpenSelected()
        {
            var entry = List.SelectedItem as Entry; if (entry == null) return;
            if(entry.NetworkServiceProfile!=null){var main=Window.GetWindow(this) as MainWindow;if(main!=null)main.OpenDiscoveredService(this,entry);return;}
            if(entry.NetworkDeviceTarget!=null){try{Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),SshTerminal.QuoteWindowsArgument("shell:"+entry.NetworkDeviceTarget)){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,L.Text("error.cannotOpenFile"));}return;}
            if (entry.IsDirectory) Navigate(entry.FullPath, true);
            else if (IsArchive) { if (ArchiveFileRequested != null) ArchiveFileRequested(this, entry, false, false); }
            else if(IsExt)ViewSelected();
            else if (IsRemote) OpenRemoteFile(entry);
            else if (ArchiveEngine.Recognized(entry.FullPath)) { var opening = OpenArchive(entry.FullPath); }
            else if (FileChecksums.IsChecksumFile(entry.FullPath)) { var main = Window.GetWindow(this) as MainWindow; if (main != null) main.VerifyChecksumsDialog(entry.FullPath); }
            else { try { Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, L.Text("error.cannotOpenFile")); } }
        }
        public void ViewSelected()
        {

            var entry = List.SelectedItem as Entry; if (entry == null) return;
            if (entry.IsDirectory) { OpenSelected(); return; }
            var host=Window.GetWindow(this) as MainWindow;if(host!=null){host.PreviewInOtherPanel(this);return;}
            if (IsArchive) { if (ArchiveFileRequested != null) ArchiveFileRequested(this, entry, false, true); return; }
            if (IsRemote) { OpenRemoteFile(entry, true); return; }
            try { MainWindow.ShowRichPreviewWindow(Window.GetWindow(this), entry.FullPath); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, L.Text("viewer.displayFailedTitle")); }
        }

        class EntryComparer : IComparer
        {
            readonly string property; readonly int direction; readonly bool natural, foldersByName;
            public EntryComparer(string property, bool descending, bool natural, bool foldersByName) { this.property = property; this.natural = natural; this.foldersByName = foldersByName; direction = descending ? -1 : 1; }
            public int Compare(object x, object y)
            {
                var a = (Entry)x; var b = (Entry)y;
                if (a.IsParent != b.IsParent) return a.IsParent ? -1 : 1;
                if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
                if (foldersByName && a.IsDirectory) return FileBehavior.CompareNames(a.FullName, b.FullName, natural) * direction;
                if (MetadataColumn.IsSort(property)) return MetadataColumn.Compare(a, b, property.Substring(5), direction, natural); int result = property == "Size" ? a.Bytes.CompareTo(b.Bytes) : property == "Modified" ? a.ModifiedTime.CompareTo(b.ModifiedTime) :
                    FileBehavior.CompareNames(property == "Extension" ? a.Extension : property == "Attributes" ? a.Attributes : a.FullName,
                        property == "Extension" ? b.Extension : property == "Attributes" ? b.Attributes : b.FullName, natural);
                return (result == 0 ? FileBehavior.CompareNames(a.FullName, b.FullName, natural) : result) * direction;
            }
        }
    }
}
