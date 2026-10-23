using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CommanderCZ
{
    public static class SmokeTests
    {
        public static int Run(string workspace)
        {
            string root = Path.Combine(Path.GetFullPath(workspace), "commandercz-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int checks = 0;
            try
            {
                string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
                string target = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
                string file = Path.Combine(source, "český soubor.txt"); File.WriteAllText(file, "Příliš žluťoučký kůň");
                FileOperations.Copy(file, target);
                Check(File.ReadAllText(Path.Combine(target, Path.GetFileName(file))) == File.ReadAllText(file), "copy content"); checks++;
                ExpectFailure(delegate { FileOperations.Copy(file, target); });
                Check(File.ReadAllText(Path.Combine(target, Path.GetFileName(file))) == "Příliš žluťoučký kůň", "collision preserves target"); checks++;
                ExpectFailure(delegate { FileOperations.Copy(file, source); }); checks++;
                string sub = Directory.CreateDirectory(Path.Combine(source, "složka")).FullName;
                File.WriteAllText(Path.Combine(sub, "data.txt"), "nested");
                FileOperations.Copy(sub, target); Check(File.ReadAllText(Path.Combine(target, "složka", "data.txt")) == "nested", "nested copy"); checks++;
                ExpectFailure(delegate { FileOperations.Copy(source, sub); }); checks++;
                FileOperations.Rename(file, "nový.txt"); Check(!File.Exists(file) && File.Exists(Path.Combine(source, "nový.txt")), "rename"); checks++;
                FileOperations.Move(Path.Combine(source, "nový.txt"), target); Check(!File.Exists(Path.Combine(source, "nový.txt")) && File.Exists(Path.Combine(target, "nový.txt")), "move"); checks++;
                ExpectFailure(delegate { FileOperations.ValidateName("..\\escape"); }); checks++;
                ExpectFailure(delegate { FileOperations.ValidateName("CON.txt"); }); checks++;
                string trashFile = Path.Combine(root, "recycle-file.txt"); File.WriteAllText(trashFile, "recycle test");
                DeleteOperations.Local(trashFile, false); Check(!File.Exists(trashFile), "file recycled"); checks++;
                string trashFolder = Directory.CreateDirectory(Path.Combine(root, "recycle-folder")).FullName;
                File.WriteAllText(Path.Combine(trashFolder, "nested.txt"), "nested recycle test");
                DeleteOperations.Local(trashFolder, false); Check(!Directory.Exists(trashFolder), "nonempty directory recycled"); checks++;
                ExpectFailure(delegate { DeleteOperations.Validate(Path.GetPathRoot(root), false); }); checks++;
                ExpectFailure(delegate { DeleteOperations.Validate("/", true); }); checks++;
                var panel = new FilePanel(delegate { }); panel.Navigate(root, false); NavigationTests.Wait(panel.NavigationTask);
                var first = panel.List.Items.Cast<Entry>().First(x => x.FullPath == source);
                var second = panel.List.Items.Cast<Entry>().First(x => x.FullPath == target);
                var parent = panel.List.Items.Cast<Entry>().First(x => x.IsParent);
                panel.BeginRightMark(first); panel.EndRightMark();
                panel.List.SelectedItem = second;
                Check(first.IsMarked && !second.IsMarked && panel.Selected().Single() == first, "right marking stays separate from cursor and operation targets"); checks++;
                panel.BeginRightMark(first); panel.EndRightMark();
                Check(!first.IsMarked, "second right click unmarks"); checks++;
                panel.BeginRightMark(first); panel.ContinueRightMark(second); panel.ContinueRightMark(first); panel.EndRightMark();
                Check(first.IsMarked && second.IsMarked && panel.Selected().Count == 2, "right drag marks once per row"); checks++;
                panel.BeginRightMark(first); panel.ContinueRightMark(second); panel.EndRightMark();
                Check(!first.IsMarked && !second.IsMarked, "right drag starting on marked row clears marks"); checks++;
                panel.BeginRightMark(parent); panel.EndRightMark(); Check(!parent.IsMarked && panel.Selected().Count == 0, "parent cannot be marked"); checks++;
                panel.MarkAll(true); panel.List.SelectedItem = first; panel.Refresh(); NavigationTests.Wait(panel.NavigationTask);
                Check(panel.Selected().Count == 2 && ((Entry)panel.List.SelectedItem).FullPath == source, "refresh preserves marking and cursor"); checks++;
                var window = new MainWindow(root); NavigationTests.WaitPanels(window); Check(window.Title.Contains(ApplicationBrand.Name), "WPF window creation");
                var content = (System.Windows.FrameworkElement)window.Content;
                var split = ((DockPanel)content).Children.OfType<Grid>().Single();
                var previewPanel = split.Children.OfType<FilePanel>().First();
                var previewItems = previewPanel.List.Items.Cast<Entry>().Where(x => !x.IsParent).Take(3).ToArray();
                foreach (var entry in previewItems) previewPanel.ToggleMark(entry);
                previewPanel.List.SelectedItem = previewItems.Last();
                content.Measure(new System.Windows.Size(1600, 900));
                content.Arrange(new System.Windows.Rect(0, 0, 1600, 900));
                content.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(delegate { }));
                var markedRow = (ListViewItem)previewPanel.List.ItemContainerGenerator.ContainerFromItem(previewItems[0]);
                Check(((SolidColorBrush)markedRow.Foreground).Color == ((SolidColorBrush)CompactTheme.ThemeBrush("#B92331")).Color, "marked row uses the themed mark color"); checks++;
                var cursorRow = (ListViewItem)previewPanel.List.ItemContainerGenerator.ContainerFromItem(previewItems.Last());
                Check(((SolidColorBrush)cursorRow.Background).Color == ((SolidColorBrush)CompactTheme.ThemeBrush("#E4F0FF")).Color, "current marked row retains blue cursor"); checks++;
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1600, 900, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(workspace, "preview.png"))) encoder.Save(stream);
                window.Close(); checks++;
                File.WriteAllText(Path.Combine(workspace, "test-result.txt"), "PASS: " + checks + " checks; " + DateTime.Now.ToString("s"));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(workspace, "test-result.txt"), "FAIL: " + ex); return 1; }
            finally
            {
                string expectedParent = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(root).StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Test cleanup path is outside its workspace.");
                Directory.Delete(root, true);
            }
        }
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
        static void ExpectFailure(Action action)
        {
            try { action(); } catch (IOException) { return; }
            throw new Exception("Expected safe rejection");
        }
    }
}

