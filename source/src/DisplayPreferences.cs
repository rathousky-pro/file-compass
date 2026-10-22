using System;
using System.Windows.Media;
namespace CommanderCZ
{
    public partial class FilePanel
    {
        bool showHiddenFiles = true, showSystemFiles = true, naturalSorting = true, foldersAlwaysByName, driveAlwaysRoot, maskIncludesFolders = true, folderBrackets, fullNameTooltips = true;
        internal void ApplyDisplaySettings(ProgramSettings settings)
        {
            SetAutomaticFolderViews(settings.AutomaticFolderViews); foldersAlwaysByName = settings.FoldersAlwaysByName; driveAlwaysRoot = settings.DriveAlwaysRoot; maskIncludesFolders = settings.MaskIncludesFolders; ApplySelectionMode(settings.WindowsMouseSelection);
            naturalSorting = settings.NaturalSorting; folderBrackets = settings.FolderBrackets; fullNameTooltips = settings.FullNameTooltips;
            showHiddenFiles = settings.ShowHiddenFiles; showSystemFiles = settings.ShowSystemFiles;
            System.Windows.Documents.TextElement.SetFontFamily(this, DisplayPreferences.Font(settings.InterfaceFontFamily)); System.Windows.Documents.TextElement.SetFontSize(this, DisplayPreferences.Size(settings.InterfaceFontSize));
            List.FontFamily = DisplayPreferences.Font(settings.FileListFontFamily); List.FontSize = DisplayPreferences.Size(settings.FileListFontSize);
            SetDisplay(displayMode); Sort(); ApplyQuickFilter(); treeRootKey = null; if (treeVisible) EnsureTreeRoot();
        }
    }
    public partial class MainWindow
    {
        internal void ApplyDisplaySettings(ProgramSettings settings)
        {
            FontFamily = DisplayPreferences.Font(settings.InterfaceFontFamily); FontSize = DisplayPreferences.Size(settings.InterfaceFontSize);
            if (System.Windows.Application.Current != null) System.Windows.Application.Current.Resources["ControlContentThemeFontSize"] = FontSize;
            left.ApplyDisplaySettings(settings); right.ApplyDisplaySettings(settings);
            toolbarAllFiles = settings.ShowHiddenFiles && settings.ShowSystemFiles;
            UpdateViewToolbarControls();
        }
    }
    internal static class DisplayPreferences
    {
        internal static double Size(double value) { return Double.IsNaN(value) || Double.IsInfinity(value) || value < 10 || value > 32 ? 12 : value; }
        internal static FontFamily Font(string name) { try { return new FontFamily(String.IsNullOrWhiteSpace(name) || name.Length > 128 ? "Segoe UI" : name); } catch (ArgumentException) { return new FontFamily("Segoe UI"); } }
    }
}

