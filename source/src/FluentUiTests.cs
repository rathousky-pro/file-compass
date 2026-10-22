using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CommanderCZ
{
    public static class FluentUiTests
    {
        internal static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T) yield return (T)root;
            foreach (object item in LogicalTreeHelper.GetChildren(root))
            {
                var child = item as DependencyObject;
                if (child != null) foreach (var found in Children<T>(child)) yield return found;
            }
        }
        static void Draw(Window window, string output, double scale)
        {
            var content = (FrameworkElement)window.Content;
            double width = content.ActualWidth + content.Margin.Left + content.Margin.Right, height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            bitmap.Render(background); bitmap.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(output)) encoder.Save(stream);
        }
        static void Layout(Window window)
        {
            window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000; window.Top = -20000; window.Show();
            NavigationTests.WaitPanels(window); window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); window.UpdateLayout();
        }
        static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T) yield return (T)root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var item in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return item;
        }
        static void DrawElement(FrameworkElement element, string path, double scale)
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
        public static int Run(string folder)
        {
            Directory.CreateDirectory(folder); folder = Path.GetFullPath(folder);
            var checks = new List<string>(); var windows = new List<Window>();
            Action<bool, string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try
            {
                FluentTheme.Ensure();
                string sample = Path.Combine(folder, "Ukázka.txt"); File.WriteAllText(sample, "ukázka pro náhled přejmenování");
                var main = new MainWindow(folder); windows.Add(main); Layout(main);
                var fileIcons = Visuals<Image>(main).Where(x => x.DataContext is Entry && x.Source is BitmapSource).ToArray();
                check(fileIcons.Length > 0, "file rows display native Windows shell icons");
                check(fileIcons.All(x => x.UseLayoutRounding && x.SnapsToDevicePixels && x.VerticalAlignment == VerticalAlignment.Center), "file icons are centered and aligned to physical pixels");
                check(fileIcons.All(x => RenderOptions.GetBitmapScalingMode(x) == BitmapScalingMode.NearestNeighbor), "file icon rendering does not blur neighboring pixels");
                check(fileIcons.All(x => x.Source.IsFrozen && ((BitmapSource)x.Source).PixelWidth >= 16), "shell icon bitmaps retain native resolution and remain safe for background loading");
                foreach (var fileList in Visuals<ListView>(main).Where(x => x.View is GridView).ToArray()) {
                    var columns = ((GridView)fileList.View).Columns;
                    for (int i = 0; i < columns.Count; i++) {
                        var columnHeader = (GridViewColumnHeader)columns[i].Header;
                        var grip = columnHeader.Template.FindName("PART_HeaderGripper", columnHeader) as System.Windows.Controls.Primitives.Thumb;
                        check(grip != null && grip.ActualWidth >= 6, "panel column exposes usable resize grip " + i);
                        double oldWidth = columns[i].ActualWidth;
                        grip.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
                        grip.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(24, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                        grip.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(24, 0, false) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                        main.UpdateLayout(); check(Math.Abs(columns[i].ActualWidth - oldWidth - 24) < 1, "dragging resizes actual panel column " + i);
                        check(Visuals<ListView>(main).Where(x => x.View is GridView).All(x => Math.Abs(((GridView)x.View).Columns[i].Width - columns[i].Width) < 1), "resize synchronizes corresponding column in both directions " + i);
                    }
                    double keptWidth = columns[0].Width; main.Width -= 30; main.UpdateLayout(); check(columns[0].Width == keptWidth, "manual column width survives window resize");
                }
                var dates = Visuals<TextBlock>(main).Where(x => x.DataContext is Entry && System.Windows.Data.BindingOperations.GetBinding(x, TextBlock.TextProperty) != null && System.Windows.Data.BindingOperations.GetBinding(x, TextBlock.TextProperty).Path.Path == "Modified").ToArray();
                check(dates.Length > 0 && dates.All(x => x.TextAlignment == TextAlignment.Right), "date cells align to the right in both panels");
                var savedColumns = new ProgramSettings { LeftFolder = folder, RightFolder = folder }; main.CaptureView(savedColumns);
                check(savedColumns.ColumnWidths.Length == 5 && savedColumns.ColumnWidths.SequenceEqual(savedColumns.LeftView.Widths) && savedColumns.ColumnWidths.SequenceEqual(savedColumns.RightView.Widths), "save view captures shared widths for both panels");
                foreach (var tab in savedColumns.LeftTabs.Concat(savedColumns.RightTabs)) if (tab.View != null) tab.View.Widths = new[] { 70.0, 70.0, 70.0, 70.0, 70.0 };
                string sharedConfig = Path.Combine(folder, "shared-columns.xml"); savedColumns.Save(sharedConfig);
                var reopened = new MainWindow(null, ProgramSettings.Load(sharedConfig)); windows.Add(reopened); NavigationTests.WaitPanels(reopened);
                check(Children<FilePanel>(reopened).Count() == 2 && Children<FilePanel>(reopened).All(x => ((GridView)x.List.View).Columns.Select(c => c.Width).SequenceEqual(savedColumns.ColumnWidths)), "restart restores shared widths from disk and ignores older tab widths");
                reopened.Close();
                foreach (var readyPanel in Children<FilePanel>(main)) NavigationTests.Wait(readyPanel.DriveDiscovery);
                var mainDrives = Children<ComboBox>(main).Where(x => x.Items.Count > 0 && x.Items[0] is DriveChoice).ToArray();
                check(mainDrives.Length == 2 && mainDrives.All(x => x.SelectedItem != null && x.SelectedItem.ToString() == Path.GetPathRoot(folder)), "both panels synchronize drive selection with the current folder");
                foreach (var drive in mainDrives)
                {
                    var toggle = Visuals<System.Windows.Controls.Primitives.ToggleButton>(drive).Single();
                    foreach (double x in new[] { 10.0, 34.0, drive.ActualWidth - 10 })
                    {
                        var hit = drive.InputHitTest(new Point(x, drive.ActualHeight / 2)) as DependencyObject;
                        while (hit != null && hit != toggle) hit = VisualTreeHelper.GetParent(hit);
                        check(hit == toggle, "drive icon, letter and arrow all hit the dropdown button");
                    }
                    var click = typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    click.Invoke(toggle, null); drive.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
                    check(drive.IsDropDownOpen && ((System.Windows.Controls.Primitives.Popup)drive.Template.FindName("PART_Popup", drive)).IsOpen, "dropdown button opens the popup in each panel");
                    click.Invoke(toggle, null); check(!drive.IsDropDownOpen, "dropdown button closes the popup in each panel");
                }
                var fileMenu = Children<MenuItem>(main).First(x => (string)x.Header == "Soubor");
                var header = fileMenu.Template.FindName("Header", fileMenu) as FrameworkElement;
                check(header != null && header.ActualHeight >= 12, "main menu captions fit the compact Fluent templates");
                check(Children<Button>(main).All(x => x is Wpf.Ui.Controls.Button || x == Children<Button>(main).FirstOrDefault(b => (b.Content as string) == "Zrušit")), "toolbar, disks and function keys use Fluent controls");
                Draw(main, Path.Combine(folder, "main.png"), 1); main.Width = 900; main.UpdateLayout(); Draw(main, Path.Combine(folder, "main-compact.png"), 1); main.Hide();
                File.SetLastWriteTime(sample, new DateTime(2026, 9, 8, 16, 43, 38));
                var attrs = new AttributesDialog(null, new[] { sample }, delegate { }, sample, sample); windows.Add(attrs); Layout(attrs);
                PanelWorkflowTests.Until(delegate { return Children<Button>(attrs).First(x => (x.Content as string) == "OK").IsEnabled; });
                var attributeChecks = Children<CheckBox>(attrs).ToArray();
                check(attributeChecks.First(x => (string)x.Content == "a  Archivovat").IsChecked == true, "attributes dialog initially shows actual file archive flag");
                check(Children<TextBox>(attrs).Any(x => x.Text == "08.09.2026") && Children<TextBox>(attrs).Any(x => x.Text == "16:43:38"), "attributes dialog loads separate Czech date and time");
                check(Children<TextBox>(attrs).Where(x => x.Text == "08.09.2026" || x.Text == "16:43:38").All(x => !x.IsEnabled), "timestamp edits require explicit opt-in");
                attributeChecks.First(x => (string)x.Content == "Změnit datum a čas:").IsChecked = true;
                check(Children<TextBox>(attrs).Where(x => x.Text == "08.09.2026" || x.Text == "16:43:38").All(x => x.IsEnabled), "timestamp opt-in enables both controls");
                Draw(attrs, Path.Combine(folder, "attributes.png"), 1); Draw(attrs, Path.Combine(folder, "attributes-150.png"), 1.5); attrs.Hide();
                var rename = new BatchRenameWindow(null, new[] { sample }, delegate { }); windows.Add(rename); Layout(rename);
                check(Children<GroupBox>(rename).Count() == 4 && Children<GroupBox>(rename).All(x => ((SolidColorBrush)x.Background).Color == Colors.White && x.Template != null), "rename fields use four readable Compact sections");
                check(Children<Control>(rename).Where(x => x is TextBox || x is ComboBox).All(x => ((SolidColorBrush)x.Background).Color == Colors.White), "rename text fields and dropdowns are white");
                var start = Children<Button>(rename).Single(x => (x.Content as string) == "Spustit");
                check(!start.IsEnabled, "rename cannot start when preview contains no changes");
                var mask = Children<TextBox>(rename).First(x => x.Text == "[N]"); mask.Text = "Nový_[N]"; rename.UpdateLayout();
                check(!start.IsEnabled && !rename.PreviewTask.IsCompleted, "rename waits asynchronously for debounced preview");
                mask.Text = "Starý_[N]"; mask.Text = "Nový_[N]";
                NavigationTests.Wait(rename.PreviewTask);
                check(start.IsEnabled, "rename starts only after a valid changed preview");
                var items = (RenameRow[])Children<ListView>(rename).Single().ItemsSource;
                check(items.Single().Changed && items.Single().NewName == "Nový_Ukázka.txt", "changed rename preview uses actual output names");
                Draw(rename, Path.Combine(folder, "rename.png"), 1); Draw(rename, Path.Combine(folder, "rename-150.png"), 1.5); rename.Hide();
                var search = new FileSearchWindow(null, folder, true, true, delegate { return 0; }, delegate { }); windows.Add(search);
                var pack = new ArchivePackDialog(null, 1, Path.Combine(folder, "Ukázka.zip")); windows.Add(pack);
                var archive = new ArchiveCopyDialog(null, 1, folder, Path.Combine(folder, "Ukázka.zip"), true); windows.Add(archive);
                var network = new NetworkCopyDialog(null, 1, "sftp://server:22/data", folder, new NetworkProfile { Name = "Server", Host = "server", Protocol = NetworkProtocol.SFTP, UserName = "jan", Port = 22 }); windows.Add(network);
                check(Children<Button>(network).Any(x => (x.Content as string) == "Kopírovat a navázat" && x.IsEnabled), "network copy offers the resume action");
                var appearance = new AppearanceDialog(null); windows.Add(appearance);
                var settings = new ArchiveSettingsDialog(null); windows.Add(settings);
                var queue = new NetworkQueueWindow(null, delegate { return new NetworkTransferJob[0]; }, null); windows.Add(queue);
                var names = new[] { "search", "pack", "archive-copy", "network-copy", "appearance", "archive-settings", "queue" };
                var dialogs = new Window[] { search, pack, archive, network, appearance, settings, queue };
                for (int i = 0; i < dialogs.Length; i++)
                {
                    var dialog = dialogs[i]; Layout(dialog);
                    check(dialog.FontFamily.Source == "Segoe UI" && dialog.FontSize == 12, names[i] + " uses common typography");
                    foreach (var button in Children<Button>(dialog).Where(x => x.IsDefault && x.IsEnabled))
                    {
                        var background = button.Background as SolidColorBrush;
                        check(background != null && background.Color.A > 0 && button.ActualWidth >= 80, names[i] + " primary action is visible and legible");
                    }
                    foreach (var input in Children<Control>(dialog).Where(x => (x is TextBox || x is PasswordBox || x is ComboBox) && x.IsVisible))
                        check(input.ActualHeight >= 27, names[i] + " input fits its Fluent template");
                    foreach (var checkbox in Children<CheckBox>(dialog).Where(x => x.IsVisible))
                    {
                        var bullet = (FrameworkElement)checkbox.Template.FindName("Box", checkbox);
                        var bounds = bullet.TransformToAncestor(checkbox).TransformBounds(new Rect(new Point(), bullet.RenderSize));
                        check(bounds.Height == 16 && bounds.Top >= 0 && bounds.Bottom <= checkbox.ActualHeight, names[i] + " checkbox fits entirely, including disabled options");
                        check(((SolidColorBrush)((Border)bullet).Background).Color == Colors.White, names[i] + " checkbox remains white on gray background");
                    }
                    Draw(dialog, Path.Combine(folder, names[i] + ".png"), 1); dialog.Hide();
                }
                Layout(pack); Draw(pack, Path.Combine(folder, "pack-150.png"), 1.5); pack.Hide();
                var store = new NetworkSettingsStore(Path.Combine(folder, "connections.xml"));
                foreach (var protocol in new[] { NetworkProtocol.SCP, NetworkProtocol.SSH, NetworkProtocol.FTP, NetworkProtocol.SFTP })
                    store.Settings.Connections.Add(new NetworkProfile { Name = protocol.ToString() + " server", Protocol = protocol, Host = "10.0.10.101", Port = protocol == NetworkProtocol.FTP ? 21 : 22 });
                var manager = new NetworkManagerDialog(null, store, null); windows.Add(manager); Layout(manager);
                var profileList = Children<ListBox>(manager).Single(); var frame = LogicalTreeHelper.GetParent(profileList) as Border;
                check(frame != null && frame.Background == Brushes.White && frame.BorderThickness.Left == 1, "connection list has a white bordered surface");
                Draw(manager, Path.Combine(folder, "connections.png"), 1); manager.Hide();
                var selector = new ComboBox { Height = 24, MinHeight = 0 }; DriveSelector.Configure(selector);
                var local = new DriveChoice("G:\\"); local.Update("Games", "", false, true, null);
                var mapped = new DriveChoice("X:\\"); mapped.Update("", @"\\10.0.10.101\BACKUP", true, true, null);
                var offline = new DriveChoice("Q:\\"); offline.Update("", @"\\sshfs.r\root@10.0.10.201", true, false, null);
                selector.Items.Add(local); selector.Items.Add(offline); selector.Items.Add(mapped); selector.SelectedItem = local;
                var driveWindow = new Window { Content = new StackPanel { Children = { selector } }, Width = 420, Height = 120 }; FluentTheme.Dialog(driveWindow); windows.Add(driveWindow); Layout(driveWindow);
                selector.IsDropDownOpen = true; selector.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); selector.UpdateLayout();
                var popup = (System.Windows.Controls.Primitives.Popup)selector.Template.FindName("PART_Popup", selector);
                var popupFrame = (Border)popup.Child; popupFrame.UpdateLayout();
                var selectedDrive = (ComboBoxItem)selector.ItemContainerGenerator.ContainerFromItem(local);
                check(popupFrame.ActualWidth >= 370 && popupFrame.Background == Brushes.White, "drive popup is wide and white");
                check(((SolidColorBrush)selectedDrive.Background).Color == Color.FromRgb(0, 103, 192) && selectedDrive.Foreground == Brushes.White, "drive selection has strong blue and white contrast");
                check(Visuals<TextBlock>(popupFrame).Any(x => x.Text == "Games") && Visuals<TextBlock>(popupFrame).Any(x => x.Text == mapped.Description), "drive rows show volume names and UNC addresses");
                check(Visuals<Image>(popupFrame).Count(x => x.Source != null) == 3 && offline.Description.StartsWith("(Odpojeno)"), "drive rows include Windows icons and disconnected state");
                DrawElement(popupFrame, Path.Combine(folder, "drive-popup.png"), 1); DrawElement(popupFrame, Path.Combine(folder, "drive-popup-150.png"), 1.5);
                selector.SelectedItem = mapped; check(selector.SelectedItem.ToString() == "X:\\", "drive selection retains the real navigation path");
                selector.IsDropDownOpen = false; driveWindow.Hide();
                string preferencesPath = Path.Combine(folder, "settings-test.xml");
                string leftFolder = Path.Combine(folder, "Levý panel"), rightFolder = Path.Combine(folder, "Pravý panel"); Directory.CreateDirectory(leftFolder); Directory.CreateDirectory(rightFolder);
                var preferences = new ProgramSettings { MediaPlayerPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, TextEditorPath = Path.Combine(Environment.SystemDirectory, "notepad.exe") };
                preferences.RememberFolders(leftFolder, rightFolder); preferences.Save(preferencesPath);
                var restored = ProgramSettings.Load(preferencesPath);
                check(restored.LeftFolder == leftFolder && restored.RightFolder == rightFolder && restored.MediaPlayerPath == preferences.MediaPlayerPath, "saved folders and viewer programs survive XML reload");
                restored.Save(preferencesPath); check(Directory.GetFiles(folder, "settings-test.xml.*.tmp").Length == 0, "settings replacement leaves no temporary files");
                check(ProgramSettings.StartupFolder(Path.Combine(folder, "missing-folder"), folder) == Path.Combine(folder, "missing-folder"), "saved folder is not probed on the UI thread");
                var restoredMain = new MainWindow(null, restored); windows.Add(restoredMain); Layout(restoredMain);
                check(restoredMain.ViewFolders().SequenceEqual(new[] { leftFolder, rightFolder }), "startup restores a different folder in each panel"); restoredMain.Hide();
                var configMenu = Children<MenuItem>(main).Single(x => (x.Header as string) == "Konfigurace");
                check(configMenu.Items.OfType<MenuItem>().Select(x => (string)x.Header).SequenceEqual(new[] { "Nastavení…", "Uložit pohled", "Historie operací…" }), "configuration menu has one settings window and Save View");
                var configuration = new ConfigurationDialog(main); windows.Add(configuration); Layout(configuration);
                for (int i = 0; i < configuration.Sections.Items.Count; i++)
                {
                    configuration.Sections.SelectedIndex = i; configuration.UpdateLayout();
                    check(Children<TextBlock>(configuration).Any(x => x.Text == configuration.Sections.Items[i].ToString() && x.FontSize == 18), "configuration section opens: " + configuration.Sections.Items[i]);
                    foreach (var field in Children<TextBox>(configuration).Where(x => x.IsVisible)) check(field.ActualHeight >= 28, "configuration program path fits its control");
                    Draw(configuration, Path.Combine(folder, "configuration-" + i + ".png"), 1);
                }
                configuration.Hide();
                check(FileViewers.Kind("Photo.JPG") == "image" && FileViewers.Kind("song.FLAC") == "media" && FileViewers.Kind("clip.mkv") == "media" && FileViewers.Kind("text.json") == "text" && FileViewers.Kind("paper.pdf") == "system", "F3 routes image, audio, video, text and other files correctly");
                var mediaStart = FileViewers.LaunchInfo(Path.Combine(folder, "Český název videa.mkv"), restored);
                check(mediaStart.FileName == restored.MediaPlayerPath && mediaStart.Arguments == "\"" + Path.Combine(folder, "Český název videa.mkv") + "\"", "configured media launch preserves spaces and Czech names");
                var editStart = FileViewers.LaunchInfo(sample, new ProgramSettings(), true);
                check(editStart.FileName == Path.Combine(Environment.SystemDirectory, "notepad.exe") && editStart.Arguments == "\"" + sample + "\"", "F4 defaults to Notepad with a quoted file path");
                var documentStart = FileViewers.LaunchInfo(sample + ".pdf", new ProgramSettings());
                check(documentStart.FileName == sample + ".pdf" && documentStart.UseShellExecute, "F3 opens recognized PDF documents through their associated application");
                foreach (string suffix in new[] { ".lng", ".unknown", ".exe", ".lnk" })
                {
                    var textStart = FileViewers.LaunchInfo(sample + suffix, new ProgramSettings());
                    check(textStart.FileName == Path.Combine(Environment.SystemDirectory, "notepad.exe") && textStart.Arguments == "\"" + sample + suffix + "\"", "F3 safely opens unknown files in Notepad: " + suffix);
                }
                bool missingViewer = false;
                try { FileViewers.LaunchInfo(sample, new ProgramSettings { TextEditorPath = Path.Combine(folder, "missing-editor.exe") }); }
                catch (IOException ex) { missingViewer = ex.Message.Contains("Konfigurace"); }
                check(missingViewer, "missing configured viewer reports how to correct its path");
                string picturePath = Path.Combine(folder, "sample-image.png");
                var pictureBitmap = new RenderTargetBitmap(320, 200, 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual(); using (var draw = visual.RenderOpen()) { draw.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(0, 0, 320, 200)); draw.DrawEllipse(Brushes.DodgerBlue, null, new Point(160, 100), 60, 60); } pictureBitmap.Render(visual);
                var pictureEncoder = new PngBitmapEncoder(); pictureEncoder.Frames.Add(BitmapFrame.Create(pictureBitmap)); using (var stream = File.Create(picturePath)) pictureEncoder.Save(stream);
                var imageViewer = new ImageViewerWindow(main, picturePath); windows.Add(imageViewer); Layout(imageViewer);
                var imageWait = System.Diagnostics.Stopwatch.StartNew();
                while (!imageViewer.LoadingTask.IsCompleted && imageWait.ElapsedMilliseconds < 5000) { imageViewer.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); System.Threading.Thread.Sleep(10); }
                imageViewer.UpdateLayout(); check(imageViewer.Picture.Source != null && imageViewer.Status.Text.Contains("320 × 200"), "built-in image viewer asynchronously loads pixels and dimensions: " + imageViewer.Status.Text + " / " + imageViewer.LoadingTask.Status + " / " + imageViewer.LoadingTask.Exception);
                using (var unlocked = new FileStream(picturePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) check(unlocked.Length > 0, "image viewer releases the file after loading");
                Draw(imageViewer, Path.Combine(folder, "image-viewer.png"), 1);
                var buttonClick = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                buttonClick.Invoke(Children<Button>(imageViewer).Single(x => (x.Content as string) == "100 %"), null); imageViewer.UpdateLayout();
                check(imageViewer.Picture.Stretch == Stretch.None && Children<ScrollViewer>(imageViewer).Single().HorizontalScrollBarVisibility == ScrollBarVisibility.Auto, "image viewer switches to native size with scrollbars");
                buttonClick.Invoke(Children<Button>(imageViewer).Single(x => (x.Content as string) == "Přizpůsobit oknu"), null);
                check(imageViewer.Picture.Stretch == Stretch.Uniform, "image viewer switches back to fit the window"); imageViewer.Hide();
                var level = Children<ComboBox>(pack).Single(x => x.Items.Count == 10);
                var selected = level.Template.FindName("PART_ContentPresenter", level) as FrameworkElement;
                check(level.SelectedIndex == 6 && selected != null && selected.ActualWidth >= 7, "compression level remains visible at default 6");
                File.WriteAllLines(Path.Combine(folder, "ui-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "ui-result.txt"), "FAIL after " + checks.Count + " checks\n" + String.Join("\n", checks) + "\n" + ex); return 1; }
            finally { foreach (var window in windows) window.Close(); }
        }
    }
}

