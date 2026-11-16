using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass
{
    public partial class FilePanel
    {
        static readonly object HoverNameTag = new object();
        bool hoverPreviewEnabled, hoverImagesEnabled, hoverVideosEnabled;
        DispatcherTimer hoverPreviewDelay;
        Image hoverName;
        Entry hoverEntry;
        Point hoverPosition;
        Popup hoverPreview;
        Window hoverOwner;

        void InitializeHoverPreview()
        {
            hoverPreviewDelay = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            { Interval = TimeSpan.FromSeconds(1) };
            hoverPreviewDelay.Tick += ShowHoverPreview;
            List.PreviewMouseMove += TrackHoverName;
            List.MouseLeave += delegate { CloseHoverPreview(); };
            List.PreviewMouseDown += delegate { CloseHoverPreview(); };
            List.PreviewKeyDown += delegate { CloseHoverPreview(); };
            List.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(delegate { CloseHoverPreview(); }));
            PathChanged += delegate { CloseHoverPreview(); };
            DisplayModeChanged += delegate { CloseHoverPreview(); };
            IsVisibleChanged += delegate { if (!IsVisible) CloseHoverPreview(); };
            Unloaded += delegate { CloseHoverPreview(); };
            Loaded += delegate
            {
                hoverOwner = Window.GetWindow(this);
                if (hoverOwner != null)
                {
                    hoverOwner.Deactivated += OnHoverOwnerChanged;
                    hoverOwner.LocationChanged += OnHoverOwnerChanged;
                }
            };
            Unloaded += delegate
            {
                if (hoverOwner != null)
                {
                    hoverOwner.Deactivated -= OnHoverOwnerChanged;
                    hoverOwner.LocationChanged -= OnHoverOwnerChanged;
                }
                hoverOwner = null;
            };
        }

        void OnHoverOwnerChanged(object sender, EventArgs e) { CloseHoverPreview(); }

        bool CanHoverPreview(Entry entry)
        {
            if (entry == null || entry.IsDirectory || entry.IsLink || IsShellBrowser || IsNetworkBrowser ||
                String.IsNullOrEmpty(entry.FullPath)) return false;
            if (FileViewers.IsVideo(entry.FullPath)) return hoverVideosEnabled && !IsRemote;
            if (FileViewers.Kind(entry.FullPath) == "image") return hoverImagesEnabled;
            return hoverPreviewEnabled;
        }

        void ConfigureHoverName(FrameworkElementFactory name)
        {
            name.SetValue(FrameworkElement.TagProperty, HoverNameTag);
            name.AddHandler(FrameworkElement.ToolTipOpeningEvent, new ToolTipEventHandler((sender, e) =>
            {
                if (CanHoverPreview(((FrameworkElement)sender).DataContext as Entry)) e.Handled = true;
            }));
        }

        void TrackHoverName(object sender, MouseEventArgs e)
        {
            if ((!hoverPreviewEnabled && !hoverImagesEnabled && !hoverVideosEnabled) ||
                e.LeftButton != MouseButtonState.Released || e.RightButton != MouseButtonState.Released) return;
            var name = e.OriginalSource as Image;
            var entry = name == null ? null : name.DataContext as Entry;
            if (name == null || name.Tag != HoverNameTag || !CanHoverPreview(entry))
            {
                CloseHoverPreview();
                return;
            }
            var position = e.GetPosition(name);
            if (name == hoverName && entry == hoverEntry)
            {
                if (hoverPreview != null) return;
                if (hoverPreviewDelay.IsEnabled && position == hoverPosition) return;
            }
            CloseHoverPreview();
            hoverName = name;
            hoverEntry = entry;
            hoverPosition = position;
            hoverPreviewDelay.Start();
        }

        void ShowHoverPreview(object sender, EventArgs e)
        {
            hoverPreviewDelay.Stop();
            var name = hoverName;
            var entry = hoverEntry;
            if (name == null || !name.IsVisible || name.DataContext != entry || !CanHoverPreview(entry) ||
                !ReferenceEquals(List.InputHitTest(Mouse.GetPosition(List)), name))
            {
                CloseHoverPreview();
                return;
            }

            bool video = FileViewers.IsVideo(entry.FullPath);
            var cursor = name.PointToScreen(Mouse.GetPosition(name));
            var monitor = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)cursor.X, (int)cursor.Y));
            var work = monitor.WorkingArea;
            var area = new Rect(name.PointFromScreen(new Point(work.Left, work.Top)),
                name.PointFromScreen(new Point(work.Right, work.Bottom)));
            var bounds = HoverPreviewBounds(area, name.ActualHeight, video ? new Size(520, 340) : new Size(440, 300));
            if (bounds.IsEmpty) { CloseHoverPreview(); return; }
            // WPF adds the custom placement to the target origin in screen pixels.
            var screenOffset = name.PointToScreen(bounds.TopLeft) - name.PointToScreen(new Point());

            var popup = new Popup
            {
                AllowsTransparency = true,
                Focusable = false,
                IsHitTestVisible = false,
                PlacementTarget = name,
                Placement = PlacementMode.Custom,
                CustomPopupPlacementCallback = (size, target, offset) =>
                    new[] { new CustomPopupPlacement((Point)screenOffset, PopupPrimaryAxis.None) }
            };
            var pane = new QuickPreviewPane(CloseHoverPreview, true, hoverOnly: true)
            {
                Width = bounds.Width,
                Height = bounds.Height,
                IsHitTestVisible = false
            };
            popup.Child = pane;
            hoverPreview = popup;
            popup.IsOpen = true;
            try { PreviewEntry(pane, entry); }
            catch (Exception ex) { pane.ShowMessage(L.Text("preview.loadFailedPrefix") + ex.Message); }
        }

        // Work area coordinates are relative to the top-left of the hovered name.
        static Rect HoverPreviewBounds(Rect workArea, double nameHeight, Size preferred)
        {
            const double edge = 8, gap = 6;
            double top = Math.Min(workArea.Bottom, workArea.Top + edge);
            double bottom = Math.Max(top, workArea.Bottom - edge);
            double left = Math.Min(workArea.Right, workArea.Left + edge);
            double right = Math.Max(left, workArea.Right - edge);
            double aboveEnd = Math.Max(top, Math.Min(bottom, -gap));
            double belowStart = Math.Max(top, Math.Min(bottom, nameHeight + gap));
            double above = aboveEnd - top;
            double below = bottom - belowStart;
            bool openAbove = above > below;
            double height = Math.Min(preferred.Height, openAbove ? above : below);
            double width = Math.Min(preferred.Width, right - left);
            if (width <= 0 || height <= 0) return Rect.Empty;
            double x = Math.Max(left, Math.Min(0, right - width));
            return new Rect(x, openAbove ? aboveEnd - height : belowStart, width, height);
        }

        void CloseHoverPreview()
        {
            hoverPreviewDelay.Stop();
            hoverName = null;
            hoverEntry = null;
            if (hoverPreview == null) return;
            var popup = hoverPreview;
            hoverPreview = null;
            popup.IsOpen = false;
            ((QuickPreviewPane)popup.Child).DisposePreview();
            popup.Child = null;
        }
    }
}
