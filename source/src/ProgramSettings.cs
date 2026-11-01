using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace FileCompass
{
    public sealed class SavedTab { public ExtSource ExtSource; public ExtVolume ExtVolume; public ProjectionSpec Projection; public string Filter = ""; public string Path = "", Home = "", Label = "", Caption = "", ProfileId = "", ArchiveFile = "", ArchivePrefix = ""; public bool Locked; public PanelView View; }
    public sealed class FavoriteLocation { public string Name = "", Group = "", Left = "", Right = ""; public SavedTab LeftLocation, RightLocation; public override string ToString() { return (String.IsNullOrEmpty(Group) ? "" : Group + " / ") + Name; } }
    public sealed class PanelView
    {
        public string Sort = "Name"; public bool Descending; public double[] Widths = new double[0];
        public string Display = "Details"; public bool Tree; public string[] MetadataColumns = new string[0];
        public int ThumbnailSize = 1;
        public bool? ExtensionColumn, AttributesColumn, SizeColumn, ModifiedColumn;
        public string[] ColumnOrder = new string[0];
        public bool DetailExpanded, DetailConfigured;
        public double DetailRatio = .38, TreeWidth = 210;
    }
    public sealed class ProgramSettings
    {
        public string ColorTheme = "Light"; public string WindowBackdrop = "None";
        public string InterfaceMotif = "Clean";
        public string LeftFolder = "", RightFolder = "";
        public bool LeftPanelEmpty,RightPanelEmpty;
        public string SizeUnits = "IEC";
        public double WindowLeft = Double.NaN, WindowTop = Double.NaN, WindowWidth = 1440, WindowHeight = 800, SplitRatio = .5;
        public bool Maximized;
        public string PanelLayout = "SideBySide";
        public string LastDualLayout = "SideBySide";
        public bool SinglePanelRight;
        public double StackedSplitRatio = .5;
        public bool SingleInstance, SaveFoldersOnExit, SaveTabsOnExit, SaveViewOnExit, SaveWindowOnExit;
        public PanelView LeftView = new PanelView(), RightView = new PanelView();
        public double[] ColumnWidths = new double[0];
        public bool InternalTextEditor=true;
        public string ImageViewerPath = "", MediaPlayerPath = "", TextEditorPath = "";
        public SavedTab[] LeftTabs = new SavedTab[0], RightTabs = new SavedTab[0];
        public int LeftTabIndex, RightTabIndex;
        public System.Collections.Generic.List<FavoriteLocation> Favorites = new System.Collections.Generic.List<FavoriteLocation>();
        public bool AutoRefresh = true, WatchNetworkFolders;
        public bool InternalCodePreview; public bool AutomaticFolderViews;
        public bool ShowHiddenFiles = true, ShowSystemFiles = true;
        public bool FoldersAlwaysByName, DriveAlwaysRoot, WindowsMouseSelection, ArchiveNewestFileDate;
        public bool MaskIncludesFolders = true;
        public bool RenameWithoutExtension = true, NaturalSorting = true, FolderBrackets, FullNameTooltips = true;
        public bool DefaultCopyVerify, DefaultCopyPermissions, DefaultCopySkipHidden;
        public bool DefaultCopyTimes = true, DefaultCopyAttributes = true, DefaultCopyEmptyFolders = true, DeleteToRecycleBin = true;
        public string InterfaceFontFamily = "Segoe UI", FileListFontFamily = "Segoe UI";
        public double InterfaceFontSize = 12, FileListFontSize = 12;
        public double ButtonIconSize = 20, DriveIconSize = 10;
        public static string ConfigPath { get { return System.IO.Path.Combine(AppPaths.ConfigDirectory, "settings.xml"); } }
        public static ProgramSettings Load(string path = null)
        {
            try
            {
                path = path ?? ConfigPath; if (!File.Exists(path)) return new ProgramSettings();
                var saved=ConfigurationIntegrity.Read<ProgramSettings>(path); saved.InterfaceMotif="Clean";saved.ColorTheme="Light";saved.WindowBackdrop="None"; saved.ImageViewerPath=AppPaths.ResolveLegacyResourceFile(saved.ImageViewerPath); saved.MediaPlayerPath=AppPaths.ResolveLegacyResourceFile(saved.MediaPlayerPath); saved.TextEditorPath=AppPaths.ResolveLegacyResourceFile(saved.TextEditorPath); return saved;
            }
            catch (Exception ex) { if (ex is ConfigurationIntegrityException || !(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is XmlException)) throw; return new ProgramSettings(); }
        }
        public void Save(string path = null)
        {
            InterfaceMotif = "Clean"; ColorTheme="Light"; WindowBackdrop="None";
            path = path ?? ConfigPath; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                ConfigurationIntegrity.Write(path,this);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static string StartupFolder(string saved, string fallback)
        {
            if (String.IsNullOrWhiteSpace(saved)) return fallback;
            try { string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(saved)); return full; }
            catch (Exception ex) { if (!(ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)) throw; return fallback; }
        }
        internal void RememberFolders(string left, string right)
        {
            if (String.IsNullOrWhiteSpace(left) || String.IsNullOrWhiteSpace(right) || !Path.IsPathRooted(left) || !Path.IsPathRooted(right))
                throw new IOException(L.Text("settings.saveViewSupportedLocations"));
            LeftFolder = left; RightFolder = right;
        }
    }
}
