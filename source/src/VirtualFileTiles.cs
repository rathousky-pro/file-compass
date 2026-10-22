using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CommanderCZ
{
    internal sealed class VirtualFileTiles : VirtualizingPanel,IScrollInfo
    {
        public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register("TileWidth",typeof(double),typeof(VirtualFileTiles),new FrameworkPropertyMetadata(150.0,FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty TileHeightProperty = DependencyProperty.Register("TileHeight",typeof(double),typeof(VirtualFileTiles),new FrameworkPropertyMetadata(130.0,FrameworkPropertyMetadataOptions.AffectsMeasure));
        public double TileWidth { get { return (double)GetValue(TileWidthProperty); } set { SetValue(TileWidthProperty,value); } }
        public double TileHeight { get { return (double)GetValue(TileHeightProperty); } set { SetValue(TileHeightProperty,value); } }
        int columns = 1; double offset; Size extent,viewport;
        protected override Size MeasureOverride(Size available)
        {
            var owner = ItemsControl.GetItemsOwner(this); if (owner == null) return available;
            double width = Double.IsInfinity(available.Width) ? TileWidth : Math.Max(1,available.Width),height = Double.IsInfinity(available.Height) ? 500 : Math.Max(1,available.Height);
            columns = Math.Max(1,(int)(width / TileWidth)); viewport = new Size(width,height); extent = new Size(width,Math.Ceiling((double)owner.Items.Count / columns) * TileHeight); offset = Math.Max(0,Math.Min(offset,extent.Height - height)); if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
            int first = Math.Max(0,(int)(offset / TileHeight) * columns),last = Math.Min(owner.Items.Count - 1,(int)Math.Ceiling((offset + height) / TileHeight) * columns + columns - 1);
            var children = InternalChildren; var generator = ItemContainerGenerator;
            for (int i = InternalChildren.Count - 1; i >= 0; i--) { var position = new GeneratorPosition(i,0); int index = generator.IndexFromGeneratorPosition(position); if (index < first || index > last) { generator.Remove(position,1); RemoveInternalChildRange(i,1); } }
            var start = generator.GeneratorPositionFromIndex(first); int childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using (generator.StartAt(start,GeneratorDirection.Forward,true)) for (int index = first; index <= last; index++,childIndex++) {
                bool created; var child = (UIElement)generator.GenerateNext(out created); if (created) { if (childIndex >= InternalChildren.Count) AddInternalChild(child); else InsertInternalChild(childIndex,child); generator.PrepareItemContainer(child); } child.Measure(new Size(width / columns,TileHeight));
            }
            return viewport;
        }
        protected override Size ArrangeOverride(Size size)
        { for (int i = 0; i < InternalChildren.Count; i++) { int index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i,0)); InternalChildren[i].Arrange(new Rect((index % columns) * size.Width / columns,(index / columns) * TileHeight - offset,size.Width / columns,TileHeight)); } return size; }
        protected override void OnItemsChanged(object sender,ItemsChangedEventArgs args) { base.OnItemsChanged(sender,args); RemoveInternalChildRange(0,InternalChildren.Count); offset = 0; InvalidateMeasure(); }
        protected override void BringIndexIntoView(int index) { double top = (index / columns) * TileHeight; if (top < offset) SetVerticalOffset(top); else if (top + TileHeight > offset + viewport.Height) SetVerticalOffset(top + TileHeight - viewport.Height); }
        public bool CanVerticallyScroll { get; set; } public bool CanHorizontallyScroll { get; set; }
        public double ExtentHeight { get { return extent.Height; } } public double ExtentWidth { get { return extent.Width; } }
        public double ViewportHeight { get { return viewport.Height; } } public double ViewportWidth { get { return viewport.Width; } }
        public double HorizontalOffset { get { return 0; } } public double VerticalOffset { get { return offset; } }
        public ScrollViewer ScrollOwner { get; set; }
        public void SetVerticalOffset(double value) { if (Double.IsNaN(value)) return; offset = Math.Max(0,Math.Min(value,Math.Max(0,extent.Height - viewport.Height))); InvalidateMeasure(); if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo(); }
        public void SetHorizontalOffset(double value) { }
        public void LineUp() { SetVerticalOffset(offset - TileHeight); } public void LineDown() { SetVerticalOffset(offset + TileHeight); }
        public void PageUp() { SetVerticalOffset(offset - viewport.Height); } public void PageDown() { SetVerticalOffset(offset + viewport.Height); }
        public void MouseWheelUp() { SetVerticalOffset(offset - 3 * TileHeight); } public void MouseWheelDown() { SetVerticalOffset(offset + 3 * TileHeight); }
        public void LineLeft() { } public void LineRight() { } public void PageLeft() { } public void PageRight() { } public void MouseWheelLeft() { } public void MouseWheelRight() { }
        public Rect MakeVisible(Visual visual,Rect rectangle)
        { var owner = ItemsControl.GetItemsOwner(this); var container = owner == null ? null : ItemsControl.ContainerFromElement(owner,visual); if (container != null) { int index = owner.ItemContainerGenerator.IndexFromContainer(container); if (index >= 0) BringIndexIntoView(index); } return rectangle; }
    }
}

