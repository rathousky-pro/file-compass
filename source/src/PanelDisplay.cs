using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace FileCompass
{
    public partial class FilePanel
    {
        internal event Action DisplayModeChanged;
        string displayMode = "Details",treeRootKey;
        bool treeVisible, treeRequested, treeAllowedByLayout = true;
        double treeWidth = 210;
        bool revealingTree;
        int treeRevealVersion;
        readonly Dictionary<TreeViewItem, Task> treeLoads = new Dictionary<TreeViewItem, Task>();
        readonly HashSet<TreeViewItem> loadedTreeNodes = new HashSet<TreeViewItem>();
        internal Task TreeLoadingTask { get; private set; }
        int thumbnailSize = 1;
        double TileWidth { get { return new[] { 118.0, 150.0, 218.0 }[thumbnailSize]; } }
        double TileHeight { get { return new[] { 114.0, 146.0, 196.0 }[thumbnailSize] + Math.Max(0, List.FontSize - 12) * 2.4; } }
        Grid displayGrid; readonly TreeView folderTree = new TreeView { Background = CompactTheme.WhiteSurface,BorderBrush = CompactTheme.Line,BorderThickness = new Thickness(1) };
        Thumb treeGrip;
        readonly Dictionary<string,ImageSource> thumbnailCache = new Dictionary<string,ImageSource>();
        readonly Dictionary<string,string> thumbnailDetails = new Dictionary<string,string>();
        readonly HashSet<Entry> thumbnailPending = new HashSet<Entry>();
        readonly HashSet<Entry> thumbnailVisible = new HashSet<Entry>();
        static System.Collections.Generic.IEnumerable<ListViewItem> RealizedRows(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); var row = child as ListViewItem; if (row != null) yield return row; else foreach (var item in RealizedRows(child)) yield return item; }
        }
        readonly SemaphoreSlim thumbnailSlots = new SemaphoreSlim(2); int displayVersion;
        static readonly ImageSource folderThumbnail = FluentIcons.TileFolder(false);
        static readonly ImageSource parentThumbnail = FluentIcons.TileFolder(true);
        static readonly ImageSource documentThumbnail = FluentIcons.Load("document") ?? DesktopIcons.Drawing("document");
        CancellationTokenSource displayCancellation = new CancellationTokenSource();
        internal string DisplayMode { get { return displayMode; } }
        void InitializeDisplay(Grid grid)
        {
            InitializeDiskUsage();
            displayGrid = grid; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (UIElement child in grid.Children) Grid.SetColumnSpan(child,2);
            Grid.SetColumn(List,1); Grid.SetColumnSpan(List,1); Grid.SetRow(folderTree,3); grid.Children.Add(folderTree); folderTree.Visibility = Visibility.Collapsed;
            folderTree.Margin = new Thickness(0, 0, 6, 0);
            treeGrip = new Thumb { Width = 6, HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.SizeWE, Background = CompactTheme.Chrome };
            Grid.SetRow(treeGrip, 3); grid.Children.Add(treeGrip);
            treeGrip.DragDelta += delegate(object sender, DragDeltaEventArgs e) { treeWidth = Math.Max(120, Math.Min(Math.Min(800, Math.Max(120, ActualWidth - 220)), displayGrid.ColumnDefinitions[0].ActualWidth + e.HorizontalChange)); if (treeVisible) displayGrid.ColumnDefinitions[0].Width = new GridLength(treeWidth); };
            folderTree.IsVisibleChanged += delegate { treeGrip.Visibility = treeVisible ? Visibility.Visible : Visibility.Collapsed; };
            treeGrip.Visibility = Visibility.Collapsed;
            folderTree.SelectedItemChanged += delegate { if (revealingTree) return; var node = folderTree.SelectedItem as TreeViewItem; if (node != null && node.Tag is string) { string path = (string)node.Tag; if (path != CurrentPath) Navigate(path,true); } };
            PathChanged += delegate { displayVersion++; displayCancellation.Cancel(); displayCancellation.Dispose(); displayCancellation = new CancellationTokenSource(); thumbnailPending.Clear(); if (treeVisible) EnsureTreeRoot(); ScheduleThumbnails(); };
            Unloaded += delegate { displayVersion++; displayCancellation.Cancel(); treeRootKey = null; };
            List.ItemContainerGenerator.StatusChanged += delegate { if (List.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated) ScheduleThumbnails(); };
            List.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate { ScheduleThumbnails(); }));
            List.LayoutUpdated += delegate { if (displayMode == "Thumbnails") ScheduleThumbnails(); };
            List.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) { if (displayMode != "Thumbnails" || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return; ChangeThumbnailSize(e.Delta > 0 ? 1 : -1); e.Handled = true; };
            List.PreviewKeyDown += delegate(object sender,KeyEventArgs e) { if (displayMode == "Details" || e.Handled || Keyboard.Modifiers != ModifierKeys.None) return; int delta = e.Key == Key.Up ? -DisplayColumns : e.Key == Key.Down ? DisplayColumns : e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0; if (delta != 0 && List.Items.Count > 0) { List.SelectedIndex = Math.Max(0,Math.Min(List.Items.Count - 1,List.SelectedIndex + delta)); List.ScrollIntoView(List.SelectedItem); e.Handled = true; } };
        }
        internal void SetDisplay(string mode)
        {
            if (mode != "Short" && mode != "Thumbnails") mode = "Details";
            displayMode = mode; if (mode != "Thumbnails") { foreach (var old in thumbnailVisible) old.Thumbnail = null; thumbnailVisible.Clear(); } if (displayGrid == null) return;
            var selected = List.SelectedItem;
            if (mode == "Details") { List.ItemTemplate = null; List.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel))); List.View = view; List.ItemContainerStyle = RowStyle(); ScrollViewer.SetHorizontalScrollBarVisibility(List,ScrollBarVisibility.Auto); }
            else {
                List.View = null;
                var tiles = new FrameworkElementFactory(typeof(VirtualFileTiles)); tiles.SetValue(VirtualFileTiles.TileWidthProperty,mode == "Thumbnails" ? TileWidth : 220.0); tiles.SetValue(VirtualFileTiles.TileHeightProperty,mode == "Thumbnails" ? TileHeight : Math.Max(25, List.FontSize * 1.65 + 6)); List.ItemsPanel = new ItemsPanelTemplate(tiles); ScrollViewer.SetHorizontalScrollBarVisibility(List,ScrollBarVisibility.Disabled); ScrollViewer.SetCanContentScroll(List,true);
                var style = new Style(typeof(ListViewItem)); style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)); style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(4,2,4,2))); style.Setters.Add(new Setter(FrameworkElement.HeightProperty,mode == "Thumbnails" ? TileHeight - 2 : Math.Max(25, List.FontSize * 1.65 + 6)));
                var marked = new DataTrigger { Binding = new Binding("IsMarked"),Value = true }; marked.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.Firebrick)); style.Triggers.Add(marked); List.ItemContainerStyle = style;
                var body = new FrameworkElementFactory(typeof(DockPanel)); var icon = new FrameworkElementFactory(typeof(Image)); if (mode == "Thumbnails") icon.SetValue(DockPanel.DockProperty,Dock.Top); if (mode != "Thumbnails") { icon.SetValue(FrameworkElement.WidthProperty, 18.0); icon.SetValue(FrameworkElement.HeightProperty, 18.0); } icon.SetValue(FrameworkElement.MarginProperty,new Thickness(2,0,mode == "Thumbnails" ? 2 : 10,0)); icon.SetBinding(Image.SourceProperty,new Binding(mode == "Thumbnails" ? "Thumbnail" : "Icon")); if (mode == "Thumbnails") { var slot = new FrameworkElementFactory(typeof(Border)); slot.SetValue(DockPanel.DockProperty, Dock.Top); slot.SetValue(FrameworkElement.HeightProperty, TileHeight - 64 - Math.Max(0, List.FontSize - 12) * 2.4); var iconStyle = new Style(typeof(Image)); iconStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, TileWidth - 20)); iconStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, TileHeight - 64 - Math.Max(0, List.FontSize - 12) * 2.4)); var folder = new DataTrigger { Binding = new Binding("IsDirectory"), Value = true }; folder.Setters.Add(new Setter(FrameworkElement.WidthProperty, 56.0)); folder.Setters.Add(new Setter(FrameworkElement.HeightProperty, 48.0)); iconStyle.Triggers.Add(folder); icon.SetValue(FrameworkElement.StyleProperty, iconStyle);  icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); slot.AppendChild(icon); body.AppendChild(slot); } else body.AppendChild(icon);
                if (mode == "Thumbnails") { var info = new FrameworkElementFactory(typeof(TextBlock)); info.SetValue(DockPanel.DockProperty, Dock.Bottom); info.SetValue(TextBlock.FontSizeProperty, 10.0); info.SetValue(TextBlock.ForegroundProperty, FluentTheme.Muted); info.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center); info.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); info.SetBinding(TextBlock.TextProperty, new Binding("MediaInfo")); info.SetBinding(FrameworkElement.ToolTipProperty, new Binding("MediaInfo")); body.AppendChild(info); }
                var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty,new Binding() { Converter = new DisplayFileName() }); name.SetBinding(FrameworkElement.ToolTipProperty, new Binding("NameToolTip")); name.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis); name.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); if (mode == "Thumbnails") { name.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center); name.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap); name.SetValue(FrameworkElement.MaxHeightProperty,Math.Max(36, List.FontSize * 2.4)); } body.AppendChild(name); List.ItemTemplate = new DataTemplate { VisualTree = body };
            }
            if (selected != null) List.SelectedItem = selected; ScheduleThumbnails(); if (DisplayModeChanged != null) DisplayModeChanged();
        }
        internal void ChangeThumbnailSize(int delta) { thumbnailSize = Math.Max(0, Math.Min(2, thumbnailSize + delta)); SetDisplay("Thumbnails"); }
        internal int DisplayColumns { get { return displayMode == "Details" ? 1 : Math.Max(1,(int)((List.ActualWidth - 18) / (displayMode == "Thumbnails" ? TileWidth : 220))); } }
        sealed class DisplayFileName : IValueConverter
        {
            public object Convert(object value,Type type,object parameter,System.Globalization.CultureInfo culture) { var entry = value as Entry; return entry == null ? "" : entry.IsParent ? ".." : entry.IsDirectory ? entry.ListName : entry.FullName; }
            public object ConvertBack(object value,Type type,object parameter,System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
        }
        internal int LastThumbnailScanCount;
        void ScheduleThumbnails()
        {
            LastThumbnailScanCount = 0;
            if (displayMode != "Thumbnails" || !List.IsVisible || displayCancellation.IsCancellationRequested) return;
            var viewport = new Rect(0,0,List.ActualWidth,List.ActualHeight); int request = displayVersion;
            var realized = RealizedRows(List).ToArray(); var visible = new HashSet<Entry>(realized.Select(x => x.Content as Entry).Where(x => x != null));
            foreach (var old in thumbnailVisible.Where(x => !visible.Contains(x)).ToArray()) { old.Thumbnail = null; thumbnailVisible.Remove(old); }
            foreach (var row in realized) { LastThumbnailScanCount++;
                var entry = row.Content as Entry; if (entry == null) continue; thumbnailVisible.Add(entry);
                if (row == null || !row.IsLoaded || row.ActualHeight <= 0 || !viewport.IntersectsWith(row.TransformToAncestor(List).TransformBounds(new Rect(0,0,row.ActualWidth,row.ActualHeight)))) continue;
                if (entry.IsDirectory) { entry.Thumbnail = entry.IsParent ? parentThumbnail : folderThumbnail; continue; }
                string kind = FileViewers.Kind(entry.FullPath);
                if (entry.IsLink || IsArchive || (IsRemote && (Remote.Profile.Protocol == NetworkProtocol.SCP || kind != "image")) || (kind != "image" && kind != "media")) { entry.Thumbnail = entry.Icon ?? documentThumbnail; continue; }
                if (thumbnailPending.Contains(entry) || thumbnailPending.Count >= 64) continue;
                string key = (IsRemote ? DurableQueue.Identity(Remote.Profile) : "local") + "|" + entry.FullPath + "|" + entry.Bytes + "|" + entry.ModifiedTime.Ticks; ImageSource cached;
                if (thumbnailCache.TryGetValue(key,out cached)) { entry.Thumbnail = cached; string detail; if (thumbnailDetails.TryGetValue(key, out detail)) entry.MediaInfo = detail; continue; }
                entry.Thumbnail = kind == "media" ? MediaThumbnails.Fallback(entry.FullPath) : documentThumbnail; thumbnailPending.Add(entry); LoadThumbnail(entry,key,request);
            }
        }
        async void LoadThumbnail(Entry entry,string key,int request)
        {
            var remote = IsRemote ? Remote.TransferConnection() : null; var token = displayCancellation.Token; bool acquired = false;
            try {
                await thumbnailSlots.WaitAsync(token); acquired = true;
                if (request != displayVersion || displayMode != "Thumbnails" || !thumbnailVisible.Contains(entry)) return;
                string mediaInfo = "";
                var image = await Task.Run(delegate { if (remote != null) return RemotePreview.Load(remote,entry.FullPath,System.Text.Encoding.UTF8,token,180,4 * 1024 * 1024).Image;
                    token.ThrowIfCancellationRequested(); if (FileViewers.Kind(entry.FullPath) == "media") { mediaInfo = MediaThumbnails.Details(entry.FullPath); return MediaThumbnails.Load(entry.FullPath); }
                    var bitmap = new BitmapImage(); using (var stream = new FileStream(entry.FullPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 240; bitmap.StreamSource = stream; bitmap.EndInit(); } bitmap.Freeze(); return (ImageSource)bitmap; });
                if (request != displayVersion) return;
                entry.MediaInfo = mediaInfo;
                if (thumbnailCache.Count >= 128) { thumbnailCache.Clear(); thumbnailDetails.Clear(); }
                thumbnailDetails[key] = mediaInfo; thumbnailCache[key] = image ?? entry.Thumbnail ?? documentThumbnail; entry.Thumbnail = thumbnailCache[key];
            } catch (OperationCanceledException) { } catch (Exception) { /* Keep the file icon when a decoder or remote preview is unavailable. */ }
            finally { thumbnailPending.Remove(entry); if (acquired) thumbnailSlots.Release(); if (remote != null) remote.Dispose(); }
        }
        internal void ToggleTree() { SetTree(!treeRequested); }
        internal void SetLayoutTreeVisibility(bool allowed) { treeAllowedByLayout = allowed; SetTree(treeRequested); }
        internal void SetTree(bool show)
        {
            treeRevealVersion++;
            treeRequested = show; treeVisible = show && treeAllowedByLayout; if (displayGrid == null) return;
            if (treeGrip != null) treeGrip.Visibility = treeVisible ? Visibility.Visible : Visibility.Collapsed;
            folderTree.Visibility = treeVisible ? Visibility.Visible : Visibility.Collapsed; displayGrid.ColumnDefinitions[0].Width = new GridLength(treeVisible ? treeWidth : 0); if (treeVisible) EnsureTreeRoot();
        }
    }
}


