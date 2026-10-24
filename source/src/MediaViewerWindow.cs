using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FileCompass
{
    public sealed class MediaViewerWindow : Window
    {
        internal readonly MediaElement Player = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform, Volume = .5 };
        readonly Slider position = new Slider { Minimum = 0, IsEnabled = false, Margin = new Thickness(4, 8, 4, 8) };
        readonly TextBlock status = new TextBlock { Foreground = FluentTheme.Muted, TextWrapping = TextWrapping.Wrap };
        readonly StackPanel audioCard = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
        readonly Image cover = new Image { Width = 200, Height = 200, Stretch = Stretch.Uniform };
        readonly TextBlock track = new TextBlock { Foreground = Brushes.White, FontSize = 18, TextAlignment = TextAlignment.Center, Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        string[] files; int index; bool playing, seeking, closed, fullscreen; WindowState previousState; WindowStyle previousStyle;
        public MediaViewerWindow(Window owner, string path)
        {
            files = new[] { Path.GetFullPath(path) }; CopyUi.Style(this, owner, Path.GetFileName(path), 820); Height = 570; SizeToContent = SizeToContent.Manual; ResizeMode = ResizeMode.CanResize; MinWidth = 500; MinHeight = 300;
            var body = new DockPanel { Margin = new Thickness(12) }; Content = body;
            var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); body.Children.Add(bottom); bottom.Children.Add(position);
            var tools = new StackPanel { Orientation = Orientation.Horizontal }; bottom.Children.Add(tools);
            tools.Children.Add(CopyUi.Button("◀", delegate { Move(-1); })); tools.Children.Add(CopyUi.Button(L.Text("media.playPause"), delegate { Toggle(); }));
            tools.Children.Add(CopyUi.Button("■", delegate { Player.Stop(); playing = false; })); tools.Children.Add(CopyUi.Button("▶", delegate { Move(1); }));
            tools.Children.Add(CopyUi.Button(L.Text("media.fullscreen"), delegate { Fullscreen(); }));
            tools.Children.Add(CopyUi.Button(L.Text("media.external"), delegate { try { System.Diagnostics.Process.Start(FileViewers.LaunchInfo(files[index], ProgramSettings.Load())); } catch (Exception ex) { status.Text = ex.Message; } }));
            var volume = new Slider { Minimum = 0, Maximum = 1, Value = .5, Width = 90, VerticalAlignment = VerticalAlignment.Center, ToolTip = L.Text("media.volume"), Margin = new Thickness(12, 0, 0, 0) }; volume.ValueChanged += delegate { Player.Volume = volume.Value; }; tools.Children.Add(volume);
            bottom.Children.Add(status); var screen = new Grid { Background = CompactTheme.ColorBrush("#20242B") }; screen.Children.Add(Player);
            audioCard.Children.Add(cover); audioCard.Children.Add(track); screen.Children.Add(audioCard); body.Children.Add(screen);
            position.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(delegate { seeking = true; })); position.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(delegate { seeking = false; Seek(); }));
            position.PreviewMouseLeftButtonUp += delegate { Seek(); };
            Player.MediaOpened += delegate { position.IsEnabled = Player.NaturalDuration.HasTimeSpan; position.Maximum = Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan.TotalSeconds : 0; status.Text = L.Text("media.ready"); audioCard.Visibility = Player.NaturalVideoWidth == 0 ? Visibility.Visible : Visibility.Collapsed; };
            Player.MediaFailed += delegate(object sender, ExceptionRoutedEventArgs e) { playing = false; position.IsEnabled = false; status.Text = L.Text("media.unsupported") + " " + e.ErrorException.Message; };
            Player.MediaEnded += delegate { playing = false; Player.Pause(); };
            timer.Tick += delegate { if (!seeking && position.IsEnabled) position.Value = Player.Position.TotalSeconds; if (position.IsEnabled) status.Text = Player.Position.ToString(@"hh\:mm\:ss") + " / " + Player.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss") + (Player.NaturalVideoWidth > 0 ? " · " + Player.NaturalVideoWidth + " × " + Player.NaturalVideoHeight : ""); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Space) Toggle(); else if (e.Key == Key.F11) Fullscreen(); else if (e.Key == Key.Escape) { if (fullscreen) Fullscreen(); else Close(); } else if (e.Key == Key.Left) Player.Position = TimeSpan.FromSeconds(Math.Max(0, Player.Position.TotalSeconds - 5)); else if (e.Key == Key.Right && position.IsEnabled) Player.Position = TimeSpan.FromSeconds(Math.Min(position.Maximum, Player.Position.TotalSeconds + 5)); else return; e.Handled = true; };
            Closed += delegate { closed = true; timer.Stop(); Player.Close(); Player.Source = null; };
            OpenCurrent(); Dispatcher.InvokeAsync(new Action(Discover)); timer.Start();
        }
        internal void Toggle() { if (playing) Player.Pause(); else Player.Play(); playing = !playing; }
        void Seek() { if (position.IsEnabled) Player.Position = TimeSpan.FromSeconds(position.Value); }
        void Move(int delta) { index = Math.Max(0, Math.Min(files.Length - 1, index + delta)); OpenCurrent(); }
        void OpenCurrent() { Player.Close(); playing = false; position.IsEnabled = false; position.Value = 0; Title = Path.GetFileName(files[index]); track.Text = Title; audioCard.Visibility = Visibility.Collapsed; cover.Source = null; status.Text = L.Text("media.ready"); Player.Source = new Uri(files[index]); Player.Pause(); Dispatcher.InvokeAsync(new Action(delegate { LoadCover(files[index]); })); }
        async void LoadCover(string path) { cover.Source = MediaThumbnails.Fallback(path); try { var image = await Task.Run(delegate { return MediaThumbnails.Load(path); }); if (!closed && image != null && String.Equals(files[index], path, StringComparison.OrdinalIgnoreCase)) cover.Source = image; } catch (Exception) { } }
        async void Discover() { string path = files[0]; try { var found = await Task.Run(delegate { return Directory.EnumerateFiles(Path.GetDirectoryName(path)).Where(x => FileViewers.Kind(x) == "media").Take(10000).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray(); }); if (!closed) { int selected = Array.FindIndex(found, x => String.Equals(x, path, StringComparison.OrdinalIgnoreCase)); if (selected >= 0) { files = found; index = selected; } } } catch (Exception) { } }
        void Fullscreen() { if (!fullscreen) { previousState = WindowState; previousStyle = WindowStyle; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; } else { WindowState = WindowState.Normal; WindowStyle = previousStyle; WindowState = previousState; } fullscreen = !fullscreen; }
    }
}

