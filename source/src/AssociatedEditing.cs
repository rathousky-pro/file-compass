using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace FileCompass {
    public static partial class FileViewers {
        static readonly string[] AdditionalEditFormats = {
            ".webp",".avif",".heic",".heif",".svg",".svgz",".jfif",".jpe",".dib",".cur",".ani",".psd",".psb",".ai",".eps",".raw",".dng",".cr2",".cr3",".nef",".nrw",".arw",".orf",".raf",".rw2",
            ".aif",".aiff",".alac",".ape",".mid",".midi",".m4b",".m4v",".m2ts",".mts",".vob",".ogv",".3gp",".3g2",".mxf",".flv"
        };
        internal static bool CanEditInternally(string file){string name=Path.GetFileName(file),ext=Path.GetExtension(file).ToLowerInvariant();return new[]{".txt",".text",".log",".csv",".tsv",".json",".jsonc",".lng",".yaml",".yml",".xml",".xaml",".config",".ini",".cfg",".conf",".toml",".md",".markdown",".rst",".html",".htm",".css",".scss",".sass",".less",".js",".mjs",".cjs",".jsx",".ts",".tsx",".cs",".csx",".vb",".fs",".fsx",".c",".cpp",".cc",".cxx",".h",".hpp",".java",".kt",".go",".rs",".py",".pyw",".rb",".php",".sql",".ps1",".psm1",".psd1",".bat",".cmd",".sh",".bash",".zsh",".sln",".csproj",".vbproj",".fsproj",".props",".targets",".resx",".reg",".srt",".vtt",".ass",".svg",".asp",".aspx",".vue",".svelte",".dart",".tex",".vbs",".wsf"}.Contains(ext)||new[]{"Dockerfile","Makefile","LICENSE","README",".gitignore",".gitattributes",".editorconfig"}.Contains(name,StringComparer.OrdinalIgnoreCase);}
        static bool ExecutableEditTarget(string file){return new[]{".exe",".dll",".com",".scr",".msi",".msp",".cpl",".lnk",".url",".appref-ms",".appx",".msix",".hta"}.Contains(Path.GetExtension(file).ToLowerInvariant());}
        internal static bool UsesAssociatedEditor(string file){return !ExecutableEditTarget(file)&&!CanEditInternally(file);}
        internal static ProcessStartInfo AssociatedOpenInfo(string file,Func<string,int,string,string> query=null){query=query??QueryAssociation;string extension=Path.GetExtension(file).ToLowerInvariant();bool open=!String.IsNullOrWhiteSpace(query(extension,1,"open"))||!String.IsNullOrWhiteSpace(query(extension,18,"open"))||!String.IsNullOrWhiteSpace(query(extension,4,"open"));return new ProcessStartInfo(Path.GetFullPath(file)){UseShellExecute=true,Verb=open?"open":"openas"};}
        [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]
        static extern int AssocQueryString(uint flags,int value,string association,string extra,StringBuilder output,ref uint length);
        static string QueryAssociation(string extension,int value,string verb) {
            uint size=32768;var text=new StringBuilder((int)size);
            return AssocQueryString(0x400,value,extension,verb,text,ref size)==0?text.ToString():null;
        }
        internal static ProcessStartInfo ExternalEditorInfo(string file,ProgramSettings settings){string ext=Path.GetExtension(file).ToLowerInvariant();if(ext==".csv")return ExcelEditInfo(file);if(ext==".html"||ext==".htm")return NotepadEditInfo(file);if(UsesAssociatedEditor(file))return AssociatedOpenInfo(file);bool executable=ExecutableEditTarget(file)||new[]{".ps1",".psm1",".psd1",".bat",".cmd",".js",".mjs",".cjs",".vbs",".wsf",".py",".pyw",".rb",".php",".sh",".bash",".zsh",".reg",".csx",".fsx"}.Contains(ext);if(!executable){try{return AssociatedOpenInfo(file);}catch(IOException){}}return LaunchInfo(file,settings,true);}
        internal static ProcessStartInfo AssociatedEditInfo(string file,Func<string,int,string,string> query=null) {
            query=query??QueryAssociation;
            string extension=Path.GetExtension(file).ToLowerInvariant();
            // Query the user's effective association, including delegated/UWP handlers.
            bool edit=!String.IsNullOrWhiteSpace(query(extension,1,"edit"))||!String.IsNullOrWhiteSpace(query(extension,18,"edit"));
            bool open=edit||!String.IsNullOrWhiteSpace(query(extension,1,"open"))||!String.IsNullOrWhiteSpace(query(extension,18,"open"))||!String.IsNullOrWhiteSpace(query(extension,4,"open"));
            if(!open)throw new IOException(L.Text("editor.noAssociatedApplication"));
            return new ProcessStartInfo(Path.GetFullPath(file)){UseShellExecute=true,Verb=edit?"edit":"open"};
        }
#if APP_TESTS
        internal static Action<ProcessStartInfo> AssociatedStartForTests;
        internal static Action<string> OpenWithForTests;
#endif
        internal static void OfferOpenWith(string file,System.Windows.Window owner){
#if APP_TESTS
            if(OpenWithForTests!=null){OpenWithForTests(file);return;}
#endif
            MainWindow.ShellAction(owner==null?IntPtr.Zero:new System.Windows.Interop.WindowInteropHelper(owner).Handle,Path.GetFullPath(file),"openas");
        }
        internal static void StartAssociatedEdit(ProcessStartInfo info,Action<ProcessStartInfo> start,System.Windows.Window owner=null) {
#if APP_TESTS
            if(AssociatedStartForTests!=null){AssociatedStartForTests(info);return;}
#endif
            if(info.Verb=="openas"){OfferOpenWith(info.FileName,owner);return;}
            try {start(info);}
            catch(Win32Exception ex) {
                if(ex.NativeErrorCode==1155||ex.NativeErrorCode==31||ex.NativeErrorCode==2||ex.NativeErrorCode==3){if(String.Equals(info.Verb,"open",StringComparison.OrdinalIgnoreCase)&&File.Exists(info.FileName)){OfferOpenWith(info.FileName,owner);return;}throw new IOException(L.Text("editor.noAssociatedApplication"),ex);}
                throw new IOException(L.Text("editor.associatedApplicationFailed")+Environment.NewLine+ex.Message,ex);
            }
        }
    }
}
