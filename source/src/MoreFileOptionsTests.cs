using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ
{
    internal static class MoreFileOptionsTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            Action<bool,string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                string data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
                string alpha = Directory.CreateDirectory(Path.Combine(data, "Alpha")).FullName, zulu = Directory.CreateDirectory(Path.Combine(data, "Zulu")).FullName;
                Directory.SetLastWriteTimeUtc(alpha, new DateTime(2020, 1, 2, 12, 0, 0, DateTimeKind.Utc)); Directory.SetLastWriteTimeUtc(zulu, new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc));
                string first = Path.Combine(data, "first.txt"), second = Path.Combine(data, "second.txt"); File.WriteAllText(first, "first"); File.WriteAllText(second, "second");
                var settings = new ProgramSettings { WindowsMouseSelection = true, MaskIncludesFolders = false, ArchiveNewestFileDate = true, FoldersAlwaysByName = true, DriveAlwaysRoot = true };
                string config = Path.Combine(root, "settings.xml"); settings.Save(config); var restored = ProgramSettings.Load(config);
                check(restored.WindowsMouseSelection && !restored.MaskIncludesFolders && restored.ArchiveNewestFileDate && restored.FoldersAlwaysByName && restored.DriveAlwaysRoot, "all new settings survive XML roundtrip");
                main = new MainWindow(data, settings); main.Show(); NavigationTests.WaitPanels(main);
                var panel = FluentUiTests.Children<FilePanel>(main).First();
                check(panel.List.SelectionMode == SelectionMode.Extended, "Windows mode enables native Ctrl and Shift selection");
                var files = panel.List.Items.Cast<Entry>().Where(x => !x.IsDirectory).ToArray(); panel.List.SelectedItems.Clear(); foreach (var file in files) panel.List.SelectedItems.Add(file);
                check(panel.Selected().Count == 2 && files.All(x => x.IsMarked), "native multi-selection and operation inputs agree");
                panel.MarkAll(false); panel.MarkMask("*.*", true);
                check(panel.Selected().Count == 2 && panel.Selected().All(x => !x.IsDirectory), "Num mask excludes directories when configured");
                panel.Refresh(); NavigationTests.Wait(panel.NavigationTask);
                check(panel.Selected().Count == 2 && panel.Selected().All(x => !x.IsDirectory), "refresh preserves Windows multi-selection");
                settings.MaskIncludesFolders = true; panel.ApplyDisplaySettings(settings); panel.MarkAll(false); panel.MarkMask("*.*", true);
                check(panel.Selected().Count == 4, "Num mask can include directories");
                var modified = ((GridView)panel.List.View).Columns.Select(x => (GridViewColumnHeader)x.Header).Single(x => (string)x.Tag == "Modified"); modified.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(panel.List.Items.Cast<Entry>().Where(x => x.IsDirectory && !x.IsParent).First().FullPath == alpha, "folder-by-name mode ignores date column");
                settings.FoldersAlwaysByName = false; panel.ApplyDisplaySettings(settings);
                check(panel.List.Items.Cast<Entry>().Where(x => x.IsDirectory && !x.IsParent).First().FullPath == zulu, "normal folder sorting follows date column");
                string disk = Path.GetPathRoot(data); panel.DriveFolderResolver = delegate { return data; }; panel.SelectDrive(disk); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == disk, "root preference overrides remembered drive folder");
                settings.DriveAlwaysRoot = false; panel.ApplyDisplaySettings(settings); panel.SelectDrive(disk); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == data, "last-folder preference uses remembered drive folder");
                settings.WindowsMouseSelection = false; panel.ApplyDisplaySettings(settings); check(panel.List.SelectionMode == SelectionMode.Single, "Commander selection can be restored");
                DateTime expected = new DateTime(2020, 2, 3, 12, 30, 0, DateTimeKind.Utc); File.SetLastWriteTimeUtc(first, expected.AddDays(-1)); File.SetLastWriteTimeUtc(second, expected);
                string zip = Path.Combine(root, "dated.zip"); ArchiveEngine.Create(zip, new[] { first, second }, new ArchiveSettings(), CancellationToken.None, null, new ArchivePackOptions { NewestFileDate = true });
                check(File.GetLastWriteTimeUtc(zip) == expected, "new archive timestamp equals newest input file");
                File.SetLastWriteTimeUtc(zip, expected.AddDays(10));
                ArchiveEngine.Create(zip, new[] { first, second }, new ArchiveSettings(), CancellationToken.None, null, new ArchivePackOptions { NewestFileDate = true });
                check(File.GetLastWriteTimeUtc(zip) == expected, "replacing an existing archive preserves the selected newest-input timestamp");
                var editor = new ConfigurationDialog(main, L.Text("settings.fileOperations")); NetworkTests.Render(editor, Path.Combine(root, "settings.png")); editor.Close();
                File.WriteAllLines(Path.Combine(root, "more-options-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "more-options-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }
}

