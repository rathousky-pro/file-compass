using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass
{
    internal static class DeleteDisplayTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null; var windows = new List<Window>();
            Action<bool,string> check = delegate(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); };
            try {
                string normal = Path.Combine(root, "normal.txt"), hidden = Path.Combine(root, "hidden.txt"), system = Path.Combine(root, "system.txt"), both = Path.Combine(root, "both.txt");
                foreach (string path in new[] { normal, hidden, system, both }) File.WriteAllText(path, "test");
                File.SetAttributes(hidden, FileAttributes.Hidden); File.SetAttributes(system, FileAttributes.System); File.SetAttributes(both, FileAttributes.Hidden | FileAttributes.System);
                var preferences = new ProgramSettings { ShowHiddenFiles = false, ShowSystemFiles = false, InterfaceFontFamily = "Segoe UI", InterfaceFontSize = 16, FileListFontFamily = "Consolas", FileListFontSize = 24 };
                main = new MainWindow(root, preferences); main.Left = -20000; main.Top = -20000; main.Show(); NavigationTests.WaitPanels(main);
                var panel = FluentUiTests.Children<FilePanel>(main).First(); Func<string,bool> visible = delegate(string path) { return panel.List.Items.Cast<Entry>().Any(x => x.FullPath == path); };
                check(visible(normal) && !visible(hidden) && !visible(system) && !visible(both), "actual hidden/system attributes are filtered independently of quick search");
                preferences.ShowHiddenFiles = true; main.ApplyDisplaySettings(preferences); check(visible(hidden) && !visible(system) && !visible(both), "show hidden does not expose system files");
                preferences.ShowSystemFiles = true; main.ApplyDisplaySettings(preferences); check(visible(hidden) && visible(system) && visible(both), "enabling both flags restores every file without reloading folder");
                preferences.ShowHiddenFiles = false; main.ApplyDisplaySettings(preferences); check(!visible(hidden) && visible(system) && !visible(both), "system visibility does not override hidden visibility");
                check(main.FontSize == 16 && panel.List.FontSize == 24 && panel.List.FontFamily.Source == "Consolas", "interface and file list use distinct selected font sizes and families");
                main.UpdateLayout(); var entry = panel.List.Items.Cast<Entry>().First(x => x.FullPath == normal); panel.List.ScrollIntoView(entry); main.UpdateLayout(); var row = panel.List.ItemContainerGenerator.ContainerFromItem(entry) as ListViewItem;
                check(row != null && row.ActualHeight >= 42, "file row grows to fit larger font");
                string settingsFile = Path.Combine(root, "preferences.xml"); preferences.Save(settingsFile); var restored = ProgramSettings.Load(settingsFile); check(!restored.ShowHiddenFiles && restored.ShowSystemFiles && restored.InterfaceFontSize == 16 && restored.FileListFontSize == 24 && restored.FileListFontFamily == "Consolas", "font and visibility choices survive XML restart");
                var font = new FontChoiceControl("test", "Consolas", 12, 24); font.Points.Text = "NaN"; bool invalid = false; string family; double size; try { font.Read(out family, out size); } catch (ArgumentException) { invalid = true; } check(invalid, "invalid font size is rejected");
                NextStageTests.Draw(main, Path.Combine(root, "large-font-panels.png"));
                var selected = new List<Entry> { new Entry { FullPath = normal, Name = "normal" } };
                var dialog = new DeleteConfirmationDialog(main, selected, true, false, root); windows.Add(dialog); dialog.Show(); dialog.UpdateLayout();
                check(!dialog.Permanent && dialog.Recycle.IsEnabled && dialog.Recycle.IsDefault, "local deletion defaults to the recycle action");
                check(!FluentUiTests.Children<CheckBox>(dialog).Any(), "deletion choice has no checkbox");
                check(FluentUiTests.Children<Button>(dialog).Count(x => new[] { L.Text("action.delete"), L.Text("action.no"), L.Text("delete.moveToRecycleBin") }.Contains(x.Content as string)) == 3, "confirmation offers Delete No and Recycle");
                NextStageTests.Draw(dialog, Path.Combine(root, "delete-permanent.png")); dialog.Close();
                var unavailable = new DeleteConfirmationDialog(main, selected, false, true, "\\\\server\\share"); windows.Add(unavailable); unavailable.Show(); check(unavailable.Permanent && !unavailable.Recycle.IsEnabled, "network deletion cannot select unavailable recycle mode"); NextStageTests.Draw(unavailable, Path.Combine(root, "delete-network.png")); unavailable.Close();
                foreach (string answer in new[] { "action.no", "action.delete", "delete.moveToRecycleBin" }) {
                    var modal = new DeleteConfirmationDialog(main, selected, true, false, root); windows.Add(modal);
                    modal.Loaded += delegate { modal.Dispatcher.BeginInvoke(new Action(delegate { FluentUiTests.Children<Button>(modal).Single(x => (x.Content as string) == L.Text(answer)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); })); };
                    check(modal.ShowDialog() == (answer != "action.no"), "modal answer handled correctly: " + answer);
                    if (answer != "action.no") check(modal.Permanent == (answer == "action.delete"), "button selects the correct deletion mode: " + answer);
                }
                check(File.Exists(normal) && File.Exists(hidden) && File.Exists(system), "confirmation tests never delete fixture files");
                var config = new ConfigurationDialog(main); windows.Add(config); config.Show(); config.UpdateLayout(); NextStageTests.Draw(config, Path.Combine(root, "appearance-settings.png")); config.Close();
                File.WriteAllLines(Path.Combine(root, "delete-display-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllLines(Path.Combine(root, "delete-display-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
            finally { foreach (var window in windows) window.Close(); if (main != null) main.Close(); foreach (string name in new[] { "hidden.txt", "system.txt", "both.txt" }) { string path = Path.Combine(root, name); if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal); } }
        }
    }
}

