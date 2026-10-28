using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileCompass
{
    public static partial class FileViewers
    {
        static readonly string[] Images = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".wdp" };
        static readonly string[] Media = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".m4v", ".ts", ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus" };
        static readonly string[] Documents = { ".pdf", ".doc", ".docx", ".docm", ".dot", ".dotx", ".dotm", ".xls", ".xlsx", ".xlsm", ".xlsb", ".xlt", ".xltx", ".xltm", ".ppt", ".pptx", ".pptm", ".pps", ".ppsx", ".ppsm", ".pot", ".potx", ".potm", ".odt", ".ods", ".odp", ".odg", ".ott", ".ots", ".otp", ".rtf", ".epub", ".xps", ".oxps" };
        internal static bool IsVideo(string path) { return new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".m4v", ".ts" }.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        public static string Kind(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            return Images.Contains(extension) ? "image" : Media.Contains(extension) ? "media" : Documents.Contains(extension) ? "system" : "text";
        }
        public static string FindMediaPlayer()
        {
            foreach (string folder in new[] { Path.Combine(AppPaths.Resource("plugins"), "mpc-hc"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                foreach (string relative in new[] { "mpc-hc64.exe", "mpc-hc.exe", @"MPC-HC\mpc-hc64.exe", @"MPC-HC\mpc-hc.exe", @"K-Lite Codec Pack\MPC-HC64\mpc-hc64.exe", @"K-Lite Codec Pack\MPC-HC\mpc-hc.exe" })
                { string path = Path.Combine(folder, relative); if (File.Exists(path)) return path; }
            return "";
        }
        public static ProcessStartInfo LaunchInfo(string file, ProgramSettings settings, bool edit = false)
        {
            if(edit&&Path.GetExtension(file).Equals(".csv",StringComparison.OrdinalIgnoreCase))return ExcelEditInfo(file);
            if(edit&&new[]{".html",".htm"}.Contains(Path.GetExtension(file).ToLowerInvariant()))return NotepadEditInfo(file);
            if(edit && UsesAssociatedEditor(file))return AssociatedOpenInfo(file);
            string kind = edit ? "text" : Kind(file);
            // Only explicitly recognized document formats may use an association.
            // Unknown files, scripts and executables are passed as data to a text editor.
            if (kind == "system") return new ProcessStartInfo(Path.GetFullPath(file)) { UseShellExecute = true };
            string program = kind == "text" ? settings.TextEditorPath : kind == "image" ? settings.ImageViewerPath : kind == "media" ? settings.MediaPlayerPath : "";
            if (kind == "text" && String.IsNullOrWhiteSpace(program)) program = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            if (kind == "media" && String.IsNullOrWhiteSpace(program)) program = FindMediaPlayer();
            if (kind == "media" && String.IsNullOrWhiteSpace(program)) return new ProcessStartInfo(Path.GetFullPath(file)) { UseShellExecute = true };
            if (String.IsNullOrWhiteSpace(program)) throw new IOException(L.Text("viewer.configureProgramHelp"));
            program = Environment.ExpandEnvironmentVariables(program.Trim().Trim('"'));
            if (!File.Exists(program)) throw new IOException(L.Text("viewer.programMissingPrefix") + program + L.Text("viewer.correctProgramPathSuffix"));
            return new ProcessStartInfo(program, SshTerminal.QuoteWindowsArgument(Path.GetFullPath(file))) { UseShellExecute = true };
        }
        public static void Open(Window owner, string file, bool edit = false)
        {
            var settings = ProgramSettings.Load();
            if (!edit && settings.InternalCodePreview && RichFilePreview.Supports(file)) { MainWindow.ShowRichPreviewWindow(owner, file); return; }
            if (!edit && Kind(file) == "image" && String.IsNullOrWhiteSpace(settings.ImageViewerPath))
            { new ImageViewerWindow(owner, file).Show(); return; }
            if (!edit && Kind(file) == "media" && String.IsNullOrWhiteSpace(settings.MediaPlayerPath))
            { new MediaViewerWindow(owner, file).Show(); return; }
            var info=LaunchInfo(file, settings, edit);
            if(edit && UsesAssociatedEditor(file))StartAssociatedEdit(info,delegate(ProcessStartInfo start){Process.Start(start);},owner);
            else Process.Start(info);
        }
    }
}
