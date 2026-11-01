using System.Windows.Media;
namespace FileCompass {
    internal static class CleanToolbarIcons {
        internal static ImageSource Drawing(string name){return DesktopIcons.ToolbarDrawing(name,ToolbarThemeSettings.Current);}
    }
}