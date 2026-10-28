using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FileCompass
{
    public partial class FilePanel
    {
        Grid detailGrid;
        RowDefinition detailRow;
        Thumb detailSplitter;
        ToggleButton detailToggle;
        Border detailBody;
        TextBlock detailMetadata;
        QuickPreviewPane fileDetail;
        bool detailExpanded, detailConfigured, updatingDetailToggle, detailDisposed;
        bool detailLayoutVisible = true;
        internal event Action DetailExpandedChanged;
        double detailRatio = .38;
        int detailRequest;
        internal bool DetailExpanded { get { return detailExpanded; } }
        internal Task DetailLoadingTask { get; private set; }

        void InitializeFileDetail(Grid grid)
        {
            detailGrid = new Grid();
            detailGrid.RowDefinitions.Add(new RowDefinition());
            detailGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
            detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailRow = new RowDefinition { Height = new GridLength(0) }; detailGrid.RowDefinitions.Add(detailRow);
            SizeChanged+=delegate{if(fileDetail!=null&&detailExpanded&&detailLayoutVisible)DetailPreviewMinimum(fileDetail.SelectedPreviewPath,fileDetail.SelectionCount);};
            grid.Children.Remove(List); Grid.SetRow(detailGrid, 3); Grid.SetColumn(detailGrid, 1); grid.Children.Add(detailGrid);
            Grid.SetRow(List, 0); Grid.SetColumn(List, 0); detailGrid.Children.Add(List);
            detailSplitter = new Thumb { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = CompactTheme.Chrome, Cursor = Cursors.SizeNS, Visibility = Visibility.Collapsed };
            Grid.SetRow(detailSplitter, 1); detailGrid.Children.Add(detailSplitter);
            detailSplitter.DragCompleted += delegate { RememberDetailRatio(); };
            detailSplitter.DragDelta += delegate(object sender, DragDeltaEventArgs e) {
                double total = detailGrid.RowDefinitions[0].ActualHeight + detailRow.ActualHeight;
                if (total <= 0) return;
                detailRatio = Math.Max(.15, Math.Min(.75, (detailRow.ActualHeight - e.VerticalChange) / total));
                detailGrid.RowDefinitions[0].Height = new GridLength(1 - detailRatio, GridUnitType.Star); detailRow.Height = new GridLength(detailRatio, GridUnitType.Star);
            };
            detailToggle = new ToggleButton { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = FluentTheme.Text, Padding = new Thickness(9, 4, 9, 4), MinHeight = 27, Background = CompactTheme.Chrome, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(0, 1, 0, 0), ToolTip = L.Text("layout.detailToggleHelp") };
            var headerFrame = new FrameworkElementFactory(typeof(Border)); headerFrame.SetValue(Border.BackgroundProperty, CompactTheme.Chrome); headerFrame.SetValue(Border.BorderBrushProperty, CompactTheme.Line); headerFrame.SetValue(Border.BorderThicknessProperty, new Thickness(0, 1, 0, 0));
            var headerText = new FrameworkElementFactory(typeof(TextBlock)); headerText.SetBinding(TextBlock.TextProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent }); headerText.SetValue(TextBlock.ForegroundProperty, FluentTheme.Text); headerText.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 4, 9, 4)); headerFrame.AppendChild(headerText);
            detailToggle.Template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = headerFrame };
            Grid.SetRow(detailToggle, 2); detailGrid.Children.Add(detailToggle);
            detailToggle.Checked += delegate { if (!updatingDetailToggle) SetDetailExpanded(true); };
            detailToggle.Unchecked += delegate { if (!updatingDetailToggle) SetDetailExpanded(false); };
            detailBody = new Border { Visibility = Visibility.Collapsed }; Grid.SetRow(detailBody, 3); detailGrid.Children.Add(detailBody);
            List.SelectionChanged += delegate { ScheduleFileDetail(); };PreviewSelectionChanged+=ScheduleFileDetail;
            PathChanged += delegate { ScheduleFileDetail(); };
            LocalNavigationCompleted += ScheduleFileDetail;
            IsVisibleChanged += delegate { if (detailDisposed) return; if (IsVisible) ScheduleFileDetail(); else { detailRequest++; if (fileDetail != null) fileDetail.ShowMessage(""); } };
            UpdateDetailHeader();
        }

        void DetailPreviewMinimum(string path,int count)
        {
            bool video=path!=null&&FileViewers.IsVideo(path);
            if(!video){
                detailRow.MinHeight=detailBody.MinHeight=Math.Min(count>1?280:0,Math.Max(0,detailGrid.ActualHeight*.7));
                detailRow.Height=new GridLength(detailRatio,GridUnitType.Star);return;
            }
            // Pixel sizing avoids a child minimum forcing the entire grid outside a small window.
            // Keep room for the list header and at least one file row above the preview.
            var parent=detailGrid.Parent as Grid;
            double fixedRows=parent==null?130:parent.RowDefinitions.Where((row,index)=>index!=3).Sum(row=>row.ActualHeight)+4;
            double available=Math.Max(0,ActualHeight-fixedRows),wanted=count>1?380:300;
            detailBody.MinHeight=detailRow.MinHeight=0;
            double preferred=Math.Max(available*detailRatio,Math.Min(wanted,Math.Max(0,available-95)));
            detailRow.Height=new GridLength(Math.Min(Math.Max(0,available-40),preferred));
        }
        void RememberDetailRatio()
        {
            if (!detailExpanded || !detailLayoutVisible || detailRow == null || detailRow.ActualHeight <= 0) return;
            double total = detailGrid.RowDefinitions[0].ActualHeight + detailRow.ActualHeight;
            if (total > 0) detailRatio = Math.Max(.15, Math.Min(.75, detailRow.ActualHeight / total));
        }

        void UpdateDetailHeader()
        {
            if (detailToggle != null) detailToggle.Content = (detailExpanded ? "▾  " : "▸  ") + L.Text("layout.fileDetail");
        }

        internal void SetDetailExpanded(bool expanded)
        {
            if (detailGrid == null || detailDisposed) return;
            RememberDetailRatio(); detailExpanded = expanded; detailConfigured = true; detailRequest++;
            bool visible = expanded && detailLayoutVisible;
            updatingDetailToggle = true; detailToggle.IsChecked = expanded; updatingDetailToggle = false; UpdateDetailHeader();
            detailGrid.RowDefinitions[0].Height = new GridLength(visible ? 1 - detailRatio : 1, GridUnitType.Star);
            detailGrid.RowDefinitions[1].Height = new GridLength(visible ? 6 : 0);
            detailRow.Height = visible ? new GridLength(detailRatio, GridUnitType.Star) : new GridLength(0);
            if(!visible)detailRow.MinHeight=detailBody.MinHeight=0;detailSplitter.Visibility = detailBody.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible && fileDetail == null) {
                var body = new DockPanel(); detailMetadata = new TextBlock { Margin = new Thickness(9, 4, 9, 2), TextTrimming = TextTrimming.CharacterEllipsis, Foreground = FluentTheme.Muted };
                DockPanel.SetDock(detailMetadata, Dock.Top); body.Children.Add(detailMetadata);
                fileDetail = new QuickPreviewPane(delegate { SetDetailExpanded(false); List.Focus(); }, true); body.Children.Add(fileDetail); detailBody.Child = body;
            }
            if (!visible) { if (fileDetail != null) fileDetail.ShowMessage(""); if (detailGrid.IsKeyboardFocusWithin) List.Focus(); }
            else ScheduleFileDetail();
            if (DetailExpandedChanged != null) DetailExpandedChanged();
        }

        internal void SetDetailLayoutVisibility(bool visible)
        {
            RememberDetailRatio();
            detailLayoutVisible = visible;
            detailToggle.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            bool configured = detailConfigured;
            SetDetailExpanded(detailExpanded);
            detailConfigured = configured;
        }

        internal void EnsureExplorerDetail() { if (!detailConfigured) SetDetailExpanded(true); }

        void ScheduleFileDetail()
        {
            if (detailDisposed || !detailExpanded || !detailLayoutVisible || fileDetail == null || LocalLoading) return;
            var entries=PreviewEntries();
            if (!IsRemote && !IsArchive && entries.Length>0 && fileDetail.SelectionMatches(entries,PreviewContext)&&fileDetail.IsCurrentLocalFile(fileDetail.SelectedPreviewPath??entries[0].FullPath)) return;
            DetailLoadingTask = LoadFileDetail(++detailRequest);
        }

        async Task LoadFileDetail(int request)
        {
            // Cancel immediately, then debounce cursor movement before reading another file.
            fileDetail.ShowMessage(L.Text("preview.loadingStatus"));
            await Task.Delay(150);
            if (request != detailRequest || !detailExpanded || !detailLayoutVisible || detailDisposed || !IsVisible || LocalLoading) return;
            var entries=PreviewEntries();DetailPreviewMinimum(entries.FirstOrDefault()?.FullPath,entries.Length);if(entries.Length>0){fileDetail.SetSelection(entries,PreviewContext,delegate(Entry chosen){DetailPreviewMinimum(chosen.FullPath,entries.Length);detailMetadata.Text=chosen.FullName+" · "+chosen.Size+" · "+chosen.Modified+(chosen.TagNames.Length==0?"":" · "+chosen.TagsLabel);detailMetadata.ToolTip=chosen.FullPath+"\n"+detailMetadata.Text;PreviewEntry(fileDetail,chosen);},!IsRemote&&!IsArchive&&!IsExt);if(fileDetail.LoadingTask!=null)await fileDetail.LoadingTask;return;}
            fileDetail.SetSelection(new Entry[0],PreviewContext,delegate{},false);
            var entry = List.SelectedItem as Entry;
            if (entry == null || entry.IsParent) { detailMetadata.Text = ""; fileDetail.ShowMessage(L.Text("layout.selectFile")); return; }
            detailMetadata.Text = entry.FullName + "  ·  " + (entry.IsDirectory ? L.Text("layout.folder") : entry.Size + L.Text("layout.bytesSuffix")) + "  ·  " + entry.Modified + (entry.TagNames.Length == 0 ? "" : "  ·  " + entry.TagsLabel);
            detailMetadata.ToolTip = entry.FullPath + "\n" + detailMetadata.Text;
            if (entry.IsDirectory) { fileDetail.ShowMessage(L.Text("layout.folderDetails")); return; }
            if (IsRemote && Remote.Profile.Protocol != NetworkProtocol.FTP && Remote.Profile.Protocol != NetworkProtocol.SFTP) { fileDetail.ShowMessage(L.Text("preview.scpArchiveUseF3")); return; }
            if(IsArchive)fileDetail.ShowArchiveFile(Archive,entry.FullPath);
            else if (IsRemote) fileDetail.ShowRemoteFile(entry.FullPath, Remote); else fileDetail.ShowFile(entry.FullPath, true);
            if (fileDetail.LoadingTask != null) await fileDetail.LoadingTask;
        }

        internal void SaveDetailView(PanelView saved)
        {
            RememberDetailRatio(); saved.DetailExpanded = detailExpanded; saved.DetailConfigured = detailConfigured; saved.DetailRatio = detailRatio; saved.TreeWidth = treeWidth;
        }

        void RestoreDetailView(PanelView saved)
        {
            SetDetailExpanded(false);
            detailRatio = !Double.IsNaN(saved.DetailRatio) && !Double.IsInfinity(saved.DetailRatio) && saved.DetailRatio >= .15 && saved.DetailRatio <= .75 ? saved.DetailRatio : .38;
            treeWidth = !Double.IsNaN(saved.TreeWidth) && !Double.IsInfinity(saved.TreeWidth) && saved.TreeWidth >= 120 && saved.TreeWidth <= 800 ? saved.TreeWidth : 210;
            if (treeVisible) SetTree(true);
            SetDetailExpanded(saved.DetailExpanded); detailConfigured = saved.DetailConfigured;
        }

        internal void DisposeFileDetail() { detailDisposed = true; detailRequest++; if (fileDetail != null) fileDetail.DisposePreview(); }
    }
}
