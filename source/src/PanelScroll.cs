using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CommanderCZ
{
    public partial class FilePanel
    {
        internal static T VisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); var found = child as T ?? VisualChild<T>(child); if (found != null) return found; } return null;
        }
        double CaptureScroll() { var scroll = VisualChild<ScrollViewer>(List); return scroll == null ? 0 : scroll.VerticalOffset; }
        void RestoreScroll(double offset, int request)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new System.Action(delegate { if (request == navigationVersion) { var scroll = VisualChild<ScrollViewer>(List); if (scroll != null) scroll.ScrollToVerticalOffset(offset); } }));
        }
    }
}

