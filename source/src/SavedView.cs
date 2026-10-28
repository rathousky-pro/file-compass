using System;
using System.Threading.Tasks;
using System.Windows;
namespace FileCompass
{
    public partial class MainWindow
    {
        static double ValidDimension(double value, double min, double max, double fallback) { return Double.IsNaN(value) || Double.IsInfinity(value) || value < min || value > max ? fallback : value; }
        internal void InitializeColumnWidths(ProgramSettings settings)
        {
            left.SharedColumnWidths = right.SharedColumnWidths = true;
            var widths = settings == null ? null : settings.ColumnWidths;
            if (settings != null && (widths == null || widths.Length == 0)) widths = settings.LeftView == null ? null : settings.LeftView.Widths;
            if (widths != null) for (int i = 0; i < Math.Min(5, widths.Length); i++) if (widths[i] >= 25 && widths[i] <= 4000) { left.SetColumnWidth(i, widths[i]); right.SetColumnWidth(i, widths[i]); }
            left.ColumnWidthChanged += delegate(int index, double width) { left.SetColumnWidth(index, width); if (left.ColumnIdentity(index) == right.ColumnIdentity(index)) right.SetColumnWidth(index, width); };
            right.ColumnWidthChanged += delegate(int index, double width) { right.SetColumnWidth(index, width); if (left.ColumnIdentity(index) == right.ColumnIdentity(index)) left.SetColumnWidth(index, width); };
        }
        internal void CaptureView(ProgramSettings settings)
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            settings.WindowLeft = bounds.Left; settings.WindowTop = bounds.Top; settings.WindowWidth = bounds.Width; settings.WindowHeight = bounds.Height; settings.Maximized = WindowState == WindowState.Maximized || (WindowState == WindowState.Minimized && lastVisibleWindowState == WindowState.Maximized);
            CapturePanelLayout(settings);
            left.SavePanelView(settings.LeftView); right.SavePanelView(settings.RightView);
            settings.ColumnWidths = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(settings.LeftView.Widths, 5));
            settings.LeftTabs = left.SaveTabs(); settings.RightTabs = right.SaveTabs();
            settings.LeftTabIndex = left.CurrentTabIndex; settings.RightTabIndex = right.CurrentTabIndex;
        }
        async Task RestoreStartupFallback(string home)
        {
            await Task.WhenAll(left.NavigationTask, right.NavigationTask);
            await Dispatcher.InvokeAsync(delegate {
            if (left.CurrentPath == null && !left.HasUnavailableTab && !left.IsRemote && !left.LocalLoading) left.Navigate(home, false);
            if (right.CurrentPath == null && !right.HasUnavailableTab && !right.IsRemote && !right.LocalLoading) right.Navigate(home, false);
            });
        }
    }
}
