using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ
{
    internal static class FileBehaviorTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            Action<bool,string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                check(FileBehavior.NameSelectionLength("photo.part.jpg") == 10 && FileBehavior.NameSelectionLength(".env") == 4 && FileBehavior.NameSelectionLength("README") == 6, "rename preserves final extension and selects dotfiles and extensionless names completely");
                check(FileBehavior.CompareNames("foto2", "foto10", true) < 0 && FileBehavior.CompareNames("foto2", "foto10", false) > 0, "natural and lexical sorting differ as expected");
                check(FileBehavior.CompareNames("x9999999999999999999999999999999", "x10000000000000000000000000000000", true) < 0, "numeric comparison cannot overflow");
                check(FileBehavior.CompareNames("x2", "x02", true) < 0, "equal numbers have stable leading-zero order");
                var previous = CultureInfo.CurrentCulture;
                try { System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("cs-CZ"); check(FileBehavior.CompareNames("číslo2", "dopis1", true) < 0, "text comparison respects Czech collation"); } finally { System.Threading.Thread.CurrentThread.CurrentCulture = previous; }
                var settings = new ProgramSettings { DefaultCopyVerify = true, DefaultCopyTimes = false, DefaultCopyAttributes = false, DefaultCopyPermissions = true, DefaultCopyEmptyFolders = false, DefaultCopySkipHidden = true, DeleteToRecycleBin = false };
                string config = Path.Combine(root, "preferences.xml"); settings.Save(config); var restored = ProgramSettings.Load(config);
                check(restored.DefaultCopyVerify && !restored.DefaultCopyTimes && !restored.DefaultCopyAttributes && restored.DefaultCopyPermissions && !restored.DefaultCopyEmptyFolders && restored.DefaultCopySkipHidden && !restored.DeleteToRecycleBin && restored.NaturalSorting && restored.RenameWithoutExtension, "operation defaults and behavior survive settings roundtrip");
                string folder = Directory.CreateDirectory(Path.Combine(root, "files")).FullName;
                Directory.CreateDirectory(Path.Combine(folder, "[brackets]")); File.WriteAllText(Path.Combine(folder, "foto10.txt"), "10"); File.WriteAllText(Path.Combine(folder, "foto2.txt"), "2");
                main = new MainWindow(folder, settings); main.Show(); NavigationTests.WaitPanels(main);
                var panel = FluentUiTests.Children<FilePanel>(main).First();
                var names = panel.List.Items.Cast<Entry>().Where(x => !x.IsDirectory).Select(x => x.FullName).ToArray();
                check(names.SequenceEqual(new[] { "foto2.txt", "foto10.txt" }), "real panel uses natural ordering");
                var directory = panel.List.Items.Cast<Entry>().Single(x => x.IsDirectory && !x.IsParent);
                check(directory.ListName == "[brackets]" && directory.FullName == "[brackets]", "display preference does not strip real brackets from directory names");
                check(panel.List.Items.Cast<Entry>().Single(x => x.FullName == "foto2.txt").NameToolTip == "foto2.txt", "tooltip includes complete filename and extension");
                settings.FolderBrackets = true; settings.FullNameTooltips = false; settings.NaturalSorting = false; panel.ApplyDisplaySettings(settings);
                check(directory.ListName == "[[brackets]]" && directory.NameToolTip == null, "bracket and tooltip preferences update existing entries");
                check(panel.List.Items.Cast<Entry>().First(x => !x.IsDirectory).FullName == "foto10.txt", "sorting preference updates live");
                var selected = new List<Entry> { panel.List.Items.Cast<Entry>().First(x => !x.IsDirectory) };
                var copy = new CopySetupDialog(main, selected, root, new string[0], false, settings); var options = copy.CreateOptions(false);
                check(options.Verify && !options.Times && !options.Attributes && options.Permissions && !options.EmptyFolders && options.SkipHidden && options.Rule == CopyRule.Ask, "copy dialog starts with configured defaults and still asks before overwrite"); copy.Close();
                var editor = new ConfigurationDialog(main, L.Text("settings.fileOperations")); NetworkTests.Render(editor, Path.Combine(root, "settings.png")); editor.Close();
                NetworkTests.Render(main, Path.Combine(root, "main.png"));
                File.WriteAllLines(Path.Combine(root, "file-behavior-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "file-behavior-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }
}

