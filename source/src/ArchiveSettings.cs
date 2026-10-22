using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace CommanderCZ
{
    public class ArchiveSettings
    {
        public string WinRarPath = "", ArjPath = "", LhaPath = "";
        public int MaximumItems = 100000, MaximumMemoryMiB = 1024, MaximumSeconds = 600, MaximumListingMiB = 16, MaximumNesting = 8;
        public long MaximumExtractedMiB = 65536, MinimumFreeMiB = 512;
        internal static string TestPath;
        public void ValidateLimits()
        {
            if (MaximumItems < 1 || MaximumItems > 1000000 || MaximumMemoryMiB < 64 || MaximumMemoryMiB > 16384 || MaximumSeconds < 1 || MaximumSeconds > 86400 || MaximumListingMiB < 1 || MaximumListingMiB > 256 || MaximumNesting < 1 || MaximumNesting > 32 || MaximumExtractedMiB < 1 || MaximumExtractedMiB > 16777216 || MinimumFreeMiB < 0 || MinimumFreeMiB > 16777216)
                throw new IOException(L.Text("archive.invalidResourceLimits"));
        }
        public static string ConfigPath { get { return System.IO.Path.Combine(AppPaths.ConfigDirectory, "archives.xml"); } }
        public static ArchiveSettings Load(string path = null)
        {
            path = path ?? TestPath ?? ConfigPath;
            if (!File.Exists(path)) return new ArchiveSettings { WinRarPath = FindWinRar() ?? "" };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            { var settings = (ArchiveSettings)new XmlSerializer(typeof(ArchiveSettings)).Deserialize(reader); settings.ValidateLimits(); return settings; }
        }
        public void Save(string path = null)
        {
            ValidateLimits(); path = path ?? TestPath ?? ConfigPath; Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) { new XmlSerializer(typeof(ArchiveSettings)).Serialize(stream, this); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string FindWinRar()
        {
            foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
                foreach (string sub in new[] { "WinRAR", "Programs\\WinRAR" })
                foreach (string name in new[] { "WinRAR.exe", "Rar.exe" })
                { string path = Path.Combine(folder, sub, name); if (File.Exists(path)) return path; }
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                foreach (string name in new[] { "WinRAR.exe", "Rar.exe" })
                    try { string path = Path.Combine(folder.Trim('"'), name); if (Path.IsPathRooted(path) && File.Exists(path)) return path; } catch (ArgumentException) { }
            return null;
        }
        public static string FindLegacyArchiver(bool arj)
        {
            string[] names = arj ? new[] { "ARJ32.exe", "arj.exe" } : new[] { "lha.exe" };
            var folders = new System.Collections.Generic.List<string>((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                foreach (string sub in new[] { "ARJ", "ARJ32", "LHA", "GnuWin32\\bin" }) folders.Add(Path.Combine(root, sub));
            foreach (string folder in folders) foreach (string name in names)
                try { string candidate = Path.Combine(folder.Trim('"'), name); if (Path.IsPathRooted(candidate) && File.Exists(candidate)) return candidate; } catch (ArgumentException) { }
            return ArchivePluginInstaller.Find(arj ? ArchivePluginInstaller.Arj : ArchivePluginInstaller.Lha);
        }
        public string LegacyExecutable(bool arj)
        {
            string configured = arj ? ArjPath : LhaPath;
            string path = Environment.ExpandEnvironmentVariables((configured ?? "").Trim().Trim('"'));
            if (!Path.IsPathRooted(path) || !File.Exists(path)) throw new IOException(L.Text("archive.configureLegacyPath") + " " + (arj ? "ARJ32.exe" : "lha.exe (GnuWin32)"));
            return path;
        }
        public void ValidateCreationTool(string target)
        {
            string extension = Path.GetExtension(target).ToLowerInvariant();
            try {
                if (extension == ".rar") RarExecutable();
                else if (extension == ".arj" || extension == ".lzh" || extension == ".lha") LegacyExecutable(extension == ".arj");
            }
            catch (IOException ex) { throw new ArchiveToolMissingException(ex.Message); }
        }
        public string RarExecutable()
        {
            string path = Environment.ExpandEnvironmentVariables((WinRarPath ?? "").Trim().Trim('"'));
            if (!Path.IsPathRooted(path) || !File.Exists(path)) throw new IOException(L.Text("archive.configureWinRarPath"));
            string console = Path.Combine(Path.GetDirectoryName(path), "Rar.exe");
            return File.Exists(console) ? console : path;
        }
    }
    public sealed class ArchiveToolMissingException : IOException
    {
        public ArchiveToolMissingException(string message) : base(message) { }
    }
    public sealed class ArchiveSettingsDialog : Window
    {
        public ArchiveSettingsDialog(Window owner)
        {
            CopyUi.Style(this, owner, L.Text("settings.archivesTitle"), 560);
            var panel = new StackPanel { Margin = new Thickness(10) }; Content = panel;
            panel.Children.Add(CopyUi.Label(L.Text("archive.portableSevenZipModule")));
            panel.Children.Add(CopyUi.Label(ArchiveEngine.SevenZipLibraryPath));
            panel.Children.Add(CopyUi.Label(L.Text("settings.winRarExecutableLabel")));
            var settings = ArchiveSettings.Load(); var input = new TextBox { Text = settings.WinRarPath, Margin = new Thickness(3), Height = 24 }; panel.Children.Add(input);
            var choices = new UniformGrid { Columns = 2 }; panel.Children.Add(choices);
            choices.Children.Add(CopyUi.Button(L.Text("action.chooseFile"), delegate
            {
                var picker = new OpenFileDialog { Filter = L.Text("settings.winRarExecutableFilter"), CheckFileExists = true };
                if (picker.ShowDialog(this) == true) input.Text = picker.FileName;
            }));
            choices.Children.Add(CopyUi.Button(L.Text("action.detectAutomatically"), delegate
            { string found = ArchiveSettings.FindWinRar(); if (found != null) input.Text = found; else MessageBox.Show(this, L.Text("archive.winRarNotFound"), "WinRAR"); }));
            panel.Children.Add(CopyUi.Label(L.Text("archive.supportedFormatsHelp")));
            var limits = new ArchiveLimitEditor(settings); panel.Children.Add(limits);
            var buttons = new UniformGrid { Columns = 2 }; panel.Children.Add(buttons);
            var ok = CopyUi.Button(L.Text("action.save"), delegate
            {
                try
                {
                    settings.WinRarPath = input.Text.Trim().Trim('"');
                    limits.Apply(settings);
                    if (settings.WinRarPath.Length > 0 && !File.Exists(Environment.ExpandEnvironmentVariables(settings.WinRarPath))) throw new IOException(L.Text("error.programPathMissing"));
                    settings.Save(); DialogResult = true;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("settings.archivesTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
            }); ok.IsDefault = true; buttons.Children.Add(ok);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; buttons.Children.Add(cancel);
        }
    }
    internal sealed class ArchiveLimitEditor : GroupBox
    {
        readonly TextBox[] inputs;
        internal ArchiveLimitEditor(ArchiveSettings settings)
        {
            Header = L.Text("archive.resourceLimitsGroup"); Margin = new Thickness(0, 12, 0, 8);
            var grid = new Grid(); Content = grid; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            string[] labels = { L.Text("archive.maximumItemsLabel"), L.Text("archive.workerMemoryMiBLabel"), L.Text("archive.stepTimeoutSecondsLabel"), L.Text("archive.expandedSizeMiBLabel"), L.Text("archive.freeSpaceReserveMiBLabel"), L.Text("archive.listingSizeMiBLabel"), L.Text("archive.nestingDepthLabel") };
            long[] values = { settings.MaximumItems, settings.MaximumMemoryMiB, settings.MaximumSeconds, settings.MaximumExtractedMiB, settings.MinimumFreeMiB, settings.MaximumListingMiB, settings.MaximumNesting };
            inputs = new TextBox[labels.Length];
            for (int i = 0; i < labels.Length; i++) { grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); var label = CopyUi.Label(labels[i]); Grid.SetRow(label, i); grid.Children.Add(label); var input = inputs[i] = new TextBox { Text = values[i].ToString(), Margin = new Thickness(3) }; Grid.SetRow(input, i); Grid.SetColumn(input, 1); grid.Children.Add(input); }
        }
        internal void Apply(ArchiveSettings settings)
        {
            var copy = new ArchiveSettings { MaximumItems = Int32.Parse(inputs[0].Text), MaximumMemoryMiB = Int32.Parse(inputs[1].Text), MaximumSeconds = Int32.Parse(inputs[2].Text), MaximumExtractedMiB = Int64.Parse(inputs[3].Text), MinimumFreeMiB = Int64.Parse(inputs[4].Text), MaximumListingMiB = Int32.Parse(inputs[5].Text), MaximumNesting = Int32.Parse(inputs[6].Text) }; copy.ValidateLimits();
            settings.MaximumItems = copy.MaximumItems; settings.MaximumMemoryMiB = copy.MaximumMemoryMiB; settings.MaximumSeconds = copy.MaximumSeconds; settings.MaximumExtractedMiB = copy.MaximumExtractedMiB; settings.MinimumFreeMiB = copy.MinimumFreeMiB; settings.MaximumListingMiB = copy.MaximumListingMiB; settings.MaximumNesting = copy.MaximumNesting;
        }
    }
}

