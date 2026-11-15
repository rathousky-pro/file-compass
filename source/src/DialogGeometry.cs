using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace FileCompass {
    internal static class DialogGeometry {
        static readonly DependencyProperty CenterAnimationVersionProperty = DependencyProperty.RegisterAttached("CenterAnimationVersion", typeof(int), typeof(DialogGeometry), new PropertyMetadata(0));
        // Keep the user's chosen center when a section changes the window size.
        internal static void PreserveCenterAfterLayout(Window window) {
            if (!window.IsLoaded || window.WindowState != WindowState.Normal || Double.IsNaN(window.Left) || Double.IsNaN(window.Top)) return;
            // A second click during the animation starts at the position currently on screen.
            double currentLeft = window.Left, currentTop = window.Top;
            window.BeginAnimation(Window.LeftProperty, null); window.BeginAnimation(Window.TopProperty, null);
            window.Left = currentLeft; window.Top = currentTop;
            int version = (int)window.GetValue(CenterAnimationVersionProperty) + 1;
            window.SetValue(CenterAnimationVersionProperty, version);
            double x = window.Left + window.ActualWidth / 2, y = window.Top + window.ActualHeight / 2;
            var oldBounds = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(delegate {
                if (!window.IsVisible || window.WindowState != WindowState.Normal || (int)window.GetValue(CenterAnimationVersionProperty) != version) return;
                window.UpdateLayout();
                double left = x - window.ActualWidth / 2, top = y - window.ActualHeight / 2;
                var area = WorkArea(window);
                if (oldBounds.IntersectsWith(area)) {
                    left = Math.Max(area.Left, Math.Min(left, area.Right - window.ActualWidth));
                    top = Math.Max(area.Top, Math.Min(top, area.Bottom - window.ActualHeight));
                }
                AnimatePosition(window, left, top);
            }));
        }
        static void AnimatePosition(Window window, double left, double top) {
            double fromLeft = window.Left, fromTop = window.Top;
            window.BeginAnimation(Window.LeftProperty, null); window.BeginAnimation(Window.TopProperty, null);
            window.Left = left; window.Top = top;
            var duration = new Duration(TimeSpan.FromMilliseconds(160));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (Math.Abs(fromLeft-left) > .5) window.BeginAnimation(Window.LeftProperty, new DoubleAnimation(fromLeft, left, duration) { EasingFunction=easing, FillBehavior=FillBehavior.Stop });
            if (Math.Abs(fromTop-top) > .5) window.BeginAnimation(Window.TopProperty, new DoubleAnimation(fromTop, top, duration) { EasingFunction=easing, FillBehavior=FillBehavior.Stop });
        }
        internal static void CenterOnExpanderChanges(Window window) {
            RoutedEventHandler handler = delegate(object sender, RoutedEventArgs e) {
                if (e.OriginalSource is System.Windows.Controls.Expander) PreserveCenterAfterLayout(window);
            };
            window.AddHandler(System.Windows.Controls.Expander.ExpandedEvent, handler, true);
            window.AddHandler(System.Windows.Controls.Expander.CollapsedEvent, handler, true);
        }
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
