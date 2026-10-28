using System;
using System.Windows;
using System.Windows.Media;

namespace FileCompass
{
    // Separate, non-animated tracks keep the copy window stable during updates.
    internal sealed class LocalCopyProgressBar : FrameworkElement
    {
        readonly Brush fill;
        readonly bool total;
        static readonly Brush track = new SolidColorBrush(Color.FromRgb(238,238,238));
        double fraction;
        bool preparing;
        internal bool Preparing { get { return preparing; } set { preparing = value; InvalidateVisual(); } }
        internal LocalCopyProgressBar(bool total) {
            this.total = total; fill = new SolidColorBrush(total ? Color.FromRgb(20,87,162) : Color.FromRgb(133,197,244));
            Loaded += delegate { InterfaceTheme.State.Changed -= OnMotifChanged; InterfaceTheme.State.Changed += OnMotifChanged; InvalidateVisual(); };
            Unloaded += delegate { InterfaceTheme.State.Changed -= OnMotifChanged; };
        }
        // Loaded/Unloaded subscription avoids retaining closed transfer windows through the theme singleton.
        void OnMotifChanged(object sender, EventArgs args) { InvalidateVisual(); }
        internal void Update(double value) { fraction = Math.Max(0,Math.Min(1,value)); InvalidateVisual(); }
        protected override void OnRender(DrawingContext drawing)
        {
            double width = Math.Max(0,ActualWidth), height = Math.Min(total ? 8 : 3, Math.Max(0,ActualHeight));
            if (width <= 0 || height <= 0) return;
            double top = (ActualHeight - height) / 2;
            drawing.DrawRoundedRectangle(AppearanceTheme.IsBalanced ? CompactTheme.ThemeBrush("#EEEEEE") : track,null,new Rect(0,top,width,height),1.5,1.5);
            drawing.DrawRoundedRectangle(AppearanceTheme.IsBalanced ? CompactTheme.ThemeBrush(total ? "#1457A2" : "#85C5F4") : fill,null,new Rect(0,top,width * (preparing ? .25 : fraction),height),1.5,1.5);
        }
    }
}
