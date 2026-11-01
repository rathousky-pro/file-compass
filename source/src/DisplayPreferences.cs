using System;
using System.Windows.Media;
namespace FileCompass
{
    public partial class FilePanel
    {
        internal static double IconSize(double value,double fallback){return Double.IsNaN(value)||Double.IsInfinity(value)?fallback:Math.Max(8,Math.Min(32,value));}
        bool showHiddenFiles = true, showSystemFiles = true, naturalSorting = true, foldersAlwaysByName, driveAlwaysRoot, maskIncludesFolders = true, folderBrackets, fullNameTooltips = true;
        internal void ApplyDisplaySettings(ProgramSettings settings)
        {
            driveIconSize=IconSize(settings.DriveIconSize,10);
            SetAutomaticFolderViews(settings.AutomaticFolderViews); foldersAlwaysByName = settings.FoldersAlwaysByName; driveAlwaysRoot = settings.DriveAlwaysRoot; maskIncludesFolders = settings.MaskIncludesFolders; ApplySelectionMode(settings.WindowsMouseSelection);
            naturalSorting = settings.NaturalSorting; folderBrackets = settings.FolderBrackets; fullNameTooltips = settings.FullNameTooltips;
            showHiddenFiles = settings.ShowHiddenFiles; showSystemFiles = settings.ShowSystemFiles;
            System.Windows.Documents.TextElement.SetFontFamily(this, DisplayPreferences.Font(settings.InterfaceFontFamily)); System.Windows.Documents.TextElement.SetFontSize(this, DisplayPreferences.Size(settings.InterfaceFontSize));
            List.FontFamily = DisplayPreferences.Font(settings.FileListFontFamily); List.FontSize = DisplayPreferences.Size(settings.FileListFontSize);RefreshCleanDrives();
            SetDisplay(displayMode); Sort(); ApplyQuickFilter();if(CurrentPath!=null&&!EmptyPanel&&!IsRemote&&!IsArchive&&!IsShellBrowser&&!IsNetworkBrowser)UpdateDiskInfo(); treeRootKey = null; if (treeVisible) EnsureTreeRoot();
        }
    }
    public partial class MainWindow
    {
        internal void ApplyDisplaySettings(ProgramSettings settings)
        {
            ByteSize.Mode=ByteSize.Normalize(settings.SizeUnits);
            buttonTileIconSize=FilePanel.IconSize(settings.ButtonIconSize,20);
            FontFamily = DisplayPreferences.Font(settings.InterfaceFontFamily); FontSize = DisplayPreferences.Size(settings.InterfaceFontSize);
            if (System.Windows.Application.Current != null) System.Windows.Application.Current.Resources["ControlContentThemeFontSize"] = FontSize;
            left.ApplyDisplaySettings(settings); right.ApplyDisplaySettings(settings);
            ApplyToolbarTileSizes();
            toolbarAllFiles = settings.ShowHiddenFiles && settings.ShowSystemFiles;
            toolbarHiddenSymbol=toolbarAllFiles?"hidden-files-visible":settings.ShowHiddenFiles||settings.ShowSystemFiles?"hidden-files-partial":"hidden-files-hidden";
            UpdateViewToolbarControls();
        }
    }
    public partial class MainWindow {
        double buttonTileIconSize=20;
        internal void ApplyToolbarTileSizes(){foreach(var icon in toolbarImages){icon.Width=icon.Height=buttonTileIconSize;System.Windows.DependencyObject parent=icon;while(parent!=null&&!(parent is System.Windows.Controls.Button)&&!(parent is System.Windows.Controls.MenuItem))parent=System.Windows.LogicalTreeHelper.GetParent(parent);var button=parent as System.Windows.Controls.Button;if(button==null)continue;double pad=Math.Max(3,buttonTileIconSize*.35);button.Height=Math.Max(FontSize+10,buttonTileIconSize+12);button.MinHeight=0;button.MinWidth=buttonTileIconSize+2*pad+4;button.Padding=new System.Windows.Thickness(pad,2,pad,2);}}
    }
    internal static class DisplayPreferences
    {
        internal static double Size(double value) { return Double.IsNaN(value) || Double.IsInfinity(value) || value < 10 || value > 32 ? 12 : value; }
        internal static FontFamily Font(string name) { try { return new FontFamily(String.IsNullOrWhiteSpace(name) || name.Length > 128 ? "Segoe UI" : name); } catch (ArgumentException) { return new FontFamily("Segoe UI"); } }
    }
}

