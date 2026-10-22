using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommanderCZ
{
    internal static class LocalCopyPresentationTests
    {
        internal static int Run(string directory)
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
            Directory.CreateDirectory(directory); var checks = new List<string>();
            Action<bool,string> check = delegate(bool valid,string label) { if (!valid) throw new Exception(label); checks.Add(label); };
            MainWindow main = null;
            try {
                string sources = Directory.CreateDirectory(Path.Combine(directory,"sources")).FullName, target = Directory.CreateDirectory(Path.Combine(directory,"target")).FullName;
                string first = Path.Combine(sources,"first.bin"), second = Path.Combine(sources,"second.bin"); File.WriteAllBytes(first,new byte[8 * 1024 * 1024]); File.WriteAllBytes(second,new byte[8 * 1024 * 1024]);
                var updates = new List<CopyUpdate>(); CopyResult result;
                using (var control = new CopyControl()) result = new CopyEngine(new CopyOptions { Target = target, Verify = true },control,c => new CopyDecision(CopyChoice.Overwrite),updates.Add).Run(new[] { first,second });
                check(result.Copied == 2 && updates.Any(x => x.Phase == "Kopírování" && x.BytesPerSecond > 0 && x.RemainingSeconds.HasValue && x.TotalDone < x.TotalBytes),"real multi-file copy reports speed and remaining time before completion");
                check(updates.Where(x => x.RemainingSeconds.HasValue).All(x => Math.Abs(x.RemainingSeconds.Value - (double)(x.TotalBytes - x.TotalDone) / x.BytesPerSecond) < .001),"remaining estimate uses all remaining files and actual measured rate");
                check(updates.Where(x => x.Phase == "Ověřování SHA-256").All(x => x.BytesPerSecond == 0 && !x.RemainingSeconds.HasValue),"verification has no misleading copy rate or ETA");
                updates.Clear(); using (var control = new CopyControl()) result = new CopyEngine(new CopyOptions { Target = target, Rule = CopyRule.SkipAll },control,c => new CopyDecision(CopyChoice.Skip),updates.Add).Run(new[] { first,second });
                check(result.Skipped == 2 && updates.All(x => x.BytesPerSecond == 0 && !x.RemainingSeconds.HasValue),"skipped files do not become fictitious transferred bytes");
                main = new MainWindow(target) { Left = -18000,Top = -18000,WindowStartupLocation = WindowStartupLocation.Manual,ShowActivated = false }; main.Show(); NavigationTests.WaitPanels(main);
                NavigationTests.Wait(main.VerifyLocalCopyPresentation(directory,check)); main.Close(); main = null;
                File.WriteAllLines(Path.Combine(directory,"local-copy-ui-result.txt"),checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory,"local-copy-ui-result.txt"),"FAIL after " + checks.Count + " checks\n" + String.Join("\n",checks) + "\n" + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }
    public partial class MainWindow
    {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        internal async Task VerifyLocalCopyPresentation(string directory,Action<bool,string> check)
        {
            using (var control = new CopyControl()) {
                var dialog = new CopyProgressDialog(this,control); copyProgress = dialog;
                var activity = AddCopyActivity("Místní kopírování",control.Cancel,ShowCopyProgress); activity.Backgrounded = false; activity.LocalMetrics = dialog.Metrics; dialog.MetricsChanged = RefreshCopySummary;
                copyControl = control; activity.TransferTitle = "Kopírování fotografií"; activity.TransferRoute = "E:\\Fotografie → D:\\Archiv\\Fotografie";
                dialog.BecameBackground = delegate { activity.Backgrounded = true; RefreshCopySummary(); }; dialog.BecameForeground = delegate { activity.Backgrounded = false; RefreshCopySummary(); };
                try {
                    var update = new CopyUpdate { Phase = "Kopírování",Source = "E:\\Temp\\aiarty-video-enhancer.exe",Target = "E:\\Temp\\Test\\aiarty-video-enhancer.exe",FileDone = 56,FileTotal = 100,TotalDone = 563L * 1048576,TotalBytes = 13341L * 1048576,Completed = 1,TotalFiles = 19,BytesPerSecond = (long)(135.9 * 1048576),RemainingSeconds = 94 };
                    dialog.Update(update); dialog.Show(); dialog.Activate(); await Task.Delay(100); dialog.UpdateLayout();
                    var texts = FluentUiTests.Children<TextBlock>(dialog).ToArray();
                    check(texts.Any(x => x.Text.Contains("135") && x.Text.Contains("MB/s")) && texts.Any(x => x.Text.Contains("00:01:34")),"foreground local copy displays rate and ETA");
                    check(texts.Any(x => x.Text == "56 %") && texts.Any(x => x.Text == "4 %") && texts.Any(x => x.Text == "1 / 19"),"compact dialog displays file percent, total percent and file count");
                    var tracks = FluentUiTests.Children<LocalCopyProgressBar>(dialog).ToArray(); check(tracks.Length == 2 && tracks.All(x => x.ActualHeight == 3),"file and total progress use thin three-pixel lines");
                    var content = (FrameworkElement)dialog.Content; var bitmap = new RenderTargetBitmap((int)(content.ActualWidth + content.Margin.Left + content.Margin.Right),(int)(content.ActualHeight + content.Margin.Top + content.Margin.Bottom),96,96,PixelFormats.Pbgra32); var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(dialog.Background,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight)); bitmap.Render(background); bitmap.Render(content);
                    foreach (int i in new[] { 0,1 }) { var point = tracks[i].TranslatePoint(new Point(3,tracks[i].ActualHeight / 2),content); byte[] pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect((int)(point.X + content.Margin.Left),(int)(point.Y + content.Margin.Top),1,1),pixel,4,0); check(pixel[0] == (i == 0 ? 244 : 162) && pixel[1] == (i == 0 ? 197 : 87) && pixel[2] == (i == 0 ? 133 : 20),(i == 0 ? "file bar is light blue" : "total bar is dark blue") + " pixel=" + String.Join(",",pixel) + " point=" + point); }
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(directory,"local-copy-progress.png"))) encoder.Save(stream);
                    double height = dialog.ActualHeight, width = dialog.ActualWidth; var foreground = GetForegroundWindow(); int activated = 0; EventHandler activatedHandler = delegate { activated++; }; Activated += activatedHandler;
                    for (int i = 0; i < 30; i++) { update.Source = "E:\\" + new string('a',i * 10) + "\\soubor.bin"; dialog.Update(update); dialog.UpdateLayout(); }
                    check(dialog.ActualHeight == height && dialog.ActualWidth == width,"varying path lengths do not resize the copy window");
                    // A real watcher event must remain pending while the foreground copy is open.
                    string watched = Path.Combine(left.CurrentPath,"watch-proof.txt"); File.WriteAllText(watched,"watcher test"); await Task.Delay(1800);
                    check(!left.List.Items.Cast<Entry>().Any(x => x.FullPath == watched) && !right.List.Items.Cast<Entry>().Any(x => x.FullPath == watched),"automatic panel refresh is deferred behind foreground copy");
                    check(activated == 0 && GetForegroundWindow() == foreground,"progress and watcher updates do not activate windows behind copy"); Activated -= activatedHandler;
                    var buttons = FluentUiTests.Children<Button>(dialog).ToArray(); buttons.Single(x => (string)x.Content == "Minimalizovat").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); dialog.Update(update);
                    activity.Update("Kopírování", .56, .04, false); UpdateLayout();
                    check(summaryTrack.Height == 3 && summaryTrack.Value == .04 && summaryPercent.Text == "4 %" && summaryLabel.Text == "Kopírování fotografií" && summaryRoute.Text.Contains("→"), "minimized transfer uses title, route, percentage and a thin progress track");
                    check(!dialog.IsVisible && copySummary.Visibility == Visibility.Visible && summaryTransferInfo.Visibility == Visibility.Visible && summaryTransferInfo.Text.Contains("135") && summaryTransferInfo.Text.Contains("00:01:34"),"minimized local copy shows speed and ETA before Cancel in bottom queue");
                    summaryPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    check(control.Paused && dialog.Metrics().BytesPerSecond == 0 && !dialog.Metrics().RemainingSeconds.HasValue && summaryTransferInfo.Text.EndsWith("—"),"pause immediately clears local speed and ETA in bottom queue");
                    var deadline = DateTime.UtcNow.AddSeconds(5); while (!left.List.Items.Cast<Entry>().Any(x => x.FullPath == watched) && DateTime.UtcNow < deadline) await Task.Delay(50);
                    check(left.List.Items.Cast<Entry>().Any(x => x.FullPath == watched),"deferred watcher event refreshes after copy is minimized");
                    summaryPause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); dialog.Update(update);
                    check(!control.Paused && summaryPause.Content.ToString() == "Ⅱ", "bottom summary pause button resumes the actual copy control");
                    UpdateLayout(); var summaryBitmap = new RenderTargetBitmap((int)Math.Ceiling(copySummary.ActualWidth), (int)Math.Ceiling(copySummary.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    var summaryVisual = new DrawingVisual(); using (var drawing = summaryVisual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(copySummary), null, new Rect(0, 0, copySummary.ActualWidth, copySummary.ActualHeight));
                    summaryBitmap.Render(summaryVisual); var summaryEncoder = new PngBitmapEncoder(); summaryEncoder.Frames.Add(BitmapFrame.Create(summaryBitmap)); using (var stream = File.Create(Path.Combine(directory, "minimized-copy-summary.png"))) summaryEncoder.Save(stream);
                    ShowSummaryDetails();
                    check(dialog.IsVisible && copySummary.Visibility == Visibility.Collapsed && dialog.Metrics().BytesPerSecond > 0,"bottom queue restores local copy with live metrics after pause");
                } finally { dialog.Finish(); copyProgress = null; copyControl = null; activity.Finish("Dokončeno",true); }
            }
        }
    }
}

