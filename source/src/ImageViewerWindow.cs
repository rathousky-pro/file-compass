using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    public sealed class ImageViewerWindow : Window
    {
        internal readonly Image Picture = new Image { Stretch = Stretch.Uniform };
        internal readonly TextBlock Status = new TextBlock { Foreground = FluentTheme.Muted };
        internal Task LoadingTask;
        readonly ScrollViewer scroll;
        readonly StackPanel film = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        readonly Border filmFrame;
        string[] files; int index, version, filmVersion; bool closed, fullscreen;
        readonly System.Threading.SemaphoreSlim decodeSlots = new System.Threading.SemaphoreSlim(2);
        WindowState previousState; WindowStyle previousStyle; double zoom = 1;
        public ImageViewerWindow(Window owner, string path)
        {
            CopyUi.Style(this, owner, L.Text("viewer.imageTitlePrefix") + Path.GetFileName(path), 1000); Height = 720; MinWidth = 500; MinHeight = 340; SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize;
            files = new[] { Path.GetFullPath(path) };
            var body = new DockPanel { Margin = new Thickness(12) }; Content = body;
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(tools, Dock.Top); body.Children.Add(tools);
            tools.Children.Add(CopyUi.Button("◀", delegate { Move(-1); })); tools.Children.Add(CopyUi.Button("▶", delegate { Move(1); }));
            tools.Children.Add(CopyUi.Button(L.Text("viewer.fitToWindow"), delegate { Fit(); }));
            tools.Children.Add(CopyUi.Button("100 %", delegate { SetZoom(1); }));
            tools.Children.Add(CopyUi.Button("−", delegate { SetZoom(zoom / 1.25); })); tools.Children.Add(CopyUi.Button("+", delegate { SetZoom(zoom * 1.25); }));
            tools.Children.Add(CopyUi.Button(L.Text("media.fullscreen"), delegate { ToggleFullscreen(); }));
            tools.Children.Add(CopyUi.Button(L.Text("media.filmstrip"), delegate { filmFrame.Visibility = filmFrame.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }));
            Status.Margin = new Thickness(0, 6, 0, 0); DockPanel.SetDock(Status, Dock.Bottom); body.Children.Add(Status);
            filmFrame = new Border { Child = film, Height = 82, Background = CompactTheme.Surface, Margin = new Thickness(0, 8, 0, 0) }; DockPanel.SetDock(filmFrame, Dock.Bottom); body.Children.Add(filmFrame);
            scroll = new ScrollViewer { Content = Picture, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            body.Children.Add(new Border { Background = CompactTheme.ColorBrush("#20242B"), Child = scroll, Padding = new Thickness(4) });
            scroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) { if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) SetZoom(zoom * (e.Delta > 0 ? 1.25 : .8)); else Move(e.Delta > 0 ? -1 : 1); e.Handled = true; };
            Picture.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) { if (Picture.Stretch == Stretch.None) Fit(); else SetZoom(1); } };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.Key == Key.Left || e.Key == Key.PageUp) Move(-1); else if (e.Key == Key.Right || e.Key == Key.PageDown) Move(1);
                else if (e.Key == Key.F11 || e.Key == Key.Enter) ToggleFullscreen(); else if (e.Key == Key.Escape) { if (fullscreen) ToggleFullscreen(); else Close(); } else return; e.Handled = true;
            };
            Closed += delegate { closed = true; version++; Picture.Source = null; film.Children.Clear(); };
            LoadingTask = Dispatcher.InvokeAsync(new Func<Task>(delegate { return LoadImage(files[0]); })).Task.Unwrap();
            Dispatcher.InvokeAsync(new Action(delegate { Discover(files[0]); }));
        }
        internal void Move(int delta) { if (files.Length == 0) return; index = Math.Max(0, Math.Min(files.Length - 1, index + delta)); LoadingTask = LoadImage(files[index]); }
        internal void Fit() { zoom = 1; Picture.LayoutTransform = Transform.Identity; Picture.Width = Picture.Height = Double.NaN; Picture.Stretch = Stretch.Uniform; scroll.HorizontalScrollBarVisibility = scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; }
        void SetZoom(double value) { zoom = Math.Max(.05, Math.Min(8, value)); Picture.Stretch = Stretch.None; Picture.LayoutTransform = new ScaleTransform(zoom, zoom); scroll.HorizontalScrollBarVisibility = scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        void ToggleFullscreen() { if (!fullscreen) { previousState = WindowState; previousStyle = WindowStyle; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; } else { WindowState = WindowState.Normal; WindowStyle = previousStyle; WindowState = previousState; } fullscreen = !fullscreen; }
        async void Discover(string path)
        {
            try {
                var found = await Task.Run(delegate { return Directory.EnumerateFiles(Path.GetDirectoryName(path)).Where(x => FileViewers.Kind(x) == "image").Take(10000).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray(); });
                if (closed) return; int selected = Array.FindIndex(found, x => String.Equals(x, path, StringComparison.OrdinalIgnoreCase)); if (selected >= 0) { files = found; index = selected; await UpdateFilm(version); }
            } catch (Exception) { /* The selected file remains usable if its folder cannot be listed. */ }
        }
        static BitmapSource Decode(string path, int width)
        {
            var bitmap = new BitmapImage(); using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                var header = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                double ratio = width >= 4096 && (long)header.PixelWidth * header.PixelHeight <= 24000000 ? 1 : Math.Min(1, (double)width / Math.Max(header.PixelWidth, header.PixelHeight)); stream.Position = 0;
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = Math.Max(1, (int)(header.PixelWidth * ratio)); bitmap.StreamSource = stream; bitmap.EndInit();
            } bitmap.Freeze(); return bitmap;
        }
        async Task LoadImage(string path)
        {
            int request = ++version; Status.Text = L.Text("viewer.loadingImage"); Picture.Source = null; Fit(); Picture.LayoutTransform = Transform.Identity;
            try {
                BitmapSource bitmap; await decodeSlots.WaitAsync(); try { if (closed || request != version) return; bitmap = await Task.Run(delegate { return Decode(path, 4096); }); } finally { decodeSlots.Release(); }
                if (closed || request != version) return; Picture.Source = bitmap; Title = Path.GetFileName(path) + " · " + (index + 1) + " / " + files.Length;
                Status.Text = bitmap.PixelWidth + " × " + bitmap.PixelHeight + " px · " + Path.GetFileName(path) + " · " + L.Text("media.imageHelp"); await UpdateFilm(request);
            } catch (Exception ex) { if (!closed && request == version) Status.Text = L.Text("viewer.imageLoadFailedPrefix") + ex.Message; }
        }
        async Task UpdateFilm(int request)
        {
            int stripRequest = ++filmVersion; film.Children.Clear();
            for (int i = Math.Max(0, index - 3); i <= Math.Min(files.Length - 1, index + 3); i++) {
                if (closed || request != version || stripRequest != filmVersion) return; int selected = i; string path = files[i]; var image = new Image { Width = 76, Height = 58, Stretch = Stretch.Uniform };
                var button = CopyUi.Button("", delegate { index = selected; LoadingTask = LoadImage(files[index]); }); button.Content = image; button.ToolTip = Path.GetFileName(path); button.Width = 88; button.Height = 72; if (i == index) button.BorderBrush = FluentTheme.Accent; film.Children.Add(button);
                try { var thumb = await Task.Run(delegate { return Decode(path, 120); }); if (closed || request != version) return; image.Source = thumb; } catch (Exception) { }
            }
        }
    }
}

