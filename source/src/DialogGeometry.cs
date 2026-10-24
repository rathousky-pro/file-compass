using System;
using System.Windows;
using System.Windows.Interop;
namespace FileCompass {
    internal static class DialogGeometry {
        internal static Rect WorkArea(Window window) {
            var handle = new WindowInteropHelper(window.IsLoaded ? window : window.Owner ?? window).Handle;
            if (handle == IntPtr.Zero) return SystemParameters.WorkArea;
            var bounds = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var source = HwndSource.FromHwnd(handle);
            var matrix = source == null || source.CompositionTarget == null ? System.Windows.Media.Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            return new Rect(matrix.Transform(new Point(bounds.Left, bounds.Top)), matrix.Transform(new Point(bounds.Right, bounds.Bottom)));
        }
        internal static void Bound(Window window, double width, double height, double minimumWidth, double minimumHeight) {
            var area = WorkArea(window);
            window.MinWidth = Math.Min(minimumWidth, area.Width - 32); window.MinHeight = Math.Min(minimumHeight, area.Height - 32);
            window.MaxWidth = area.Width - 32; window.MaxHeight = area.Height - 32;
            window.Width = Math.Min(width, window.MaxWidth); window.Height = Math.Min(height, window.MaxHeight);
            window.SizeToContent = SizeToContent.Manual;
            const string marker = "FileCompass.DialogBounds";
            if (!window.Resources.Contains(marker)) {
                window.Resources[marker] = true; var previousArea = area;
                Action refresh = delegate { var current = WorkArea(window); if (current == previousArea) return; previousArea = current; Bound(window, window.Width, window.Height, minimumWidth, minimumHeight); };
                window.Loaded += delegate { refresh(); };
                window.LocationChanged += delegate { if (window.IsLoaded) refresh(); };
            }
        }
    }
}
