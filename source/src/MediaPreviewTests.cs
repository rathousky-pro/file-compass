using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CommanderCZ
{
    internal static class MediaPreviewTests
    {
        static void Pump() { Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); Thread.Sleep(15); }
        static void Until(Func<bool> condition) { var until = DateTime.UtcNow.AddSeconds(10); while (!condition() && DateTime.UtcNow < until) Pump(); if (!condition()) throw new Exception("Timed out waiting for media preview."); }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); ImageViewerWindow image = null; MediaViewerWindow media = null; Window panelWindow = null;
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)); string images = Path.Combine(root, "images"); Directory.CreateDirectory(images);
                for (int i = 0; i < 3; i++) { var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) dc.DrawRectangle(i == 0 ? Brushes.CornflowerBlue : i == 1 ? Brushes.SeaGreen : Brushes.Coral, null, new Rect(0, 0, 320 + i * 20, 200)); var bitmap = new RenderTargetBitmap(320 + i * 20, 200, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(images, "image" + i + ".png"))) encoder.Save(stream); }
                image = new ImageViewerWindow(null, Path.Combine(images, "image0.png")) { Left = -20000, Top = -20000 }; image.Show();
                Until(delegate { return image.LoadingTask.IsCompleted; }); if (image.LoadingTask.IsFaulted || image.Picture.Source == null) throw new Exception("Initial image failed."); checks.Add("image loads asynchronously");
                Until(delegate { return image.Title.Contains("/ 3"); });
                image.Move(1); Until(delegate { return image.LoadingTask.IsCompleted; }); if (!image.Status.Text.Contains("image1.png")) throw new Exception("Next image not selected."); checks.Add("folder discovery and next image");
                image.Move(1); image.Move(-1); image.Move(-1); Until(delegate { return image.LoadingTask.IsCompleted; }); if (!image.Status.Text.Contains("image0.png")) throw new Exception("Stale image replaced latest navigation."); checks.Add("rapid navigation keeps latest image");
                image.UpdateLayout(); NextStageTests.Draw(image, Path.Combine(root, "image-viewer.png"));
                using (File.Open(Path.Combine(images, "image0.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } checks.Add("image and film strip release source files");
                image.Close(); string brokenImage = Path.Combine(root, "broken.png"); File.WriteAllText(brokenImage, "invalid image fixture"); image = new ImageViewerWindow(null, brokenImage) { Left = -20000, Top = -20000 }; image.Show(); Until(delegate { return image.LoadingTask.IsCompleted; }); if (image.LoadingTask.IsFaulted || image.Picture.Source != null || !image.Status.Text.StartsWith(L.Text("viewer.imageLoadFailedPrefix"))) throw new Exception("Damaged image was not handled safely."); checks.Add("damaged image reports error without crashing viewer"); image.Close(); image = null;
                string audio = Path.Combine(root, "silence.wav"); using (var writer = new BinaryWriter(File.Create(audio))) { writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 16000 * 3); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(16000 * 3); writer.Write(new byte[16000 * 3]); }
                var details = Task.Run(delegate { return MediaThumbnails.Details(audio); }); Until(delegate { return details.IsCompleted; }); if (!details.Result.Contains("00:00:03")) throw new Exception("WAV duration metadata missing: " + details.Result); checks.Add("Windows audio duration metadata");
                media = new MediaViewerWindow(null, audio) { Left = -20000, Top = -20000 }; media.Player.Volume = 0; media.Show(); Until(delegate { return media.Player.NaturalDuration.HasTimeSpan; });
                if (media.Player.Position.TotalSeconds > .1) throw new Exception("Media started automatically."); checks.Add("audio remains paused until explicit play");
                media.Toggle(); Until(delegate { return media.Player.Position.TotalSeconds > .3; }); media.Toggle(); checks.Add("integrated audio playback and pause");
                media.UpdateLayout(); NextStageTests.Draw(media, Path.Combine(root, "audio-player.png")); media.Close(); media = null;
                string video = Path.Combine(root, "video.wmv"); if (!File.Exists(video)) throw new Exception("Missing WMV integration fixture.");
                var thumb = Task.Run(delegate { return MediaThumbnails.Load(video); }); Until(delegate { return thumb.IsCompleted; }); if (thumb.Result == null || !thumb.Result.IsFrozen) throw new Exception("Windows did not extract video thumbnail."); checks.Add("video thumbnail extracted and frozen on worker");
                media = new MediaViewerWindow(null, video) { Left = -20000, Top = -20000 }; media.Player.Volume = 0; media.Show(); Until(delegate { return media.Player.NaturalVideoWidth > 0; });
                media.Toggle(); Until(delegate { return media.Player.Position.TotalSeconds > .2; }); media.Toggle(); media.UpdateLayout(); NextStageTests.Draw(media, Path.Combine(root, "video-player.png")); checks.Add("integrated video decoding and playback");
                media.Close(); media = null;
                string tileFolder = Path.Combine(root, "tile-files"); Directory.CreateDirectory(tileFolder); File.Copy(video, Path.Combine(tileFolder, "video.wmv"), true); File.Copy(audio, Path.Combine(tileFolder, "silence.wav"), true); foreach (string picture in Directory.GetFiles(images, "*.png")) File.Copy(picture, Path.Combine(tileFolder, Path.GetFileName(picture)), true); video = Path.Combine(tileFolder, "video.wmv"); audio = Path.Combine(tileFolder, "silence.wav");
                var panel = new FilePanel(delegate { }); panelWindow = new Window { Content = panel, Width = 920, Height = 620, Left = -20000, Top = -20000 }; panelWindow.Show(); panel.Navigate(tileFolder, true);
                Until(delegate { return panel.NavigationTask != null && panel.NavigationTask.IsCompleted; }); panel.SetDisplay("Thumbnails"); panelWindow.UpdateLayout();
                var videoEntry = panel.List.Items.Cast<Entry>().Single(x => x.FullPath == video); var audioEntry = panel.List.Items.Cast<Entry>().Single(x => x.FullPath == audio);
                panel.List.ScrollIntoView(videoEntry); panelWindow.UpdateLayout(); Until(delegate { return videoEntry.Thumbnail is BitmapSource && !Object.ReferenceEquals(videoEntry.Thumbnail, videoEntry.Icon); }); checks.Add("actual panel displays video thumbnail");
                panel.List.ScrollIntoView(audioEntry); panelWindow.UpdateLayout(); Until(delegate { return audioEntry.MediaInfo.Contains("00:00:03"); }); checks.Add("actual audio tile displays duration");
                panel.ChangeThumbnailSize(1); var saved = new PanelView(); panel.SavePanelView(saved); if (saved.ThumbnailSize != 2) throw new Exception("Large tile size not saved."); panel.ChangeThumbnailSize(-1); panel.RestorePanelView(saved); var restored = new PanelView(); panel.SavePanelView(restored); if (restored.ThumbnailSize != 2) throw new Exception("Tile size not restored."); panelWindow.UpdateLayout(); NextStageTests.Draw(panelWindow, Path.Combine(root, "media-tiles.png")); checks.Add("three tile sizes and saved panel view");
                string broken = Path.Combine(root, "broken.mp3"); File.WriteAllText(broken, "invalid media fixture"); bool failed = false; media = new MediaViewerWindow(null, broken) { Left = -20000, Top = -20000 }; media.Player.MediaFailed += delegate { failed = true; }; media.Show(); Until(delegate { return failed; }); checks.Add("unsupported media reports failure without executing the file");
                File.WriteAllLines(Path.Combine(root, "media-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllLines(Path.Combine(root, "media-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
            finally { if (image != null) image.Close(); if (media != null) media.Close(); if (panelWindow != null) panelWindow.Close(); }
        }
    }
}

