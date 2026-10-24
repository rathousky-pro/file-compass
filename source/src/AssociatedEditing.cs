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
        internal static bool UsesAssociatedEditor(string file) {string extension=Path.GetExtension(file).ToLowerInvariant(); if(extension==".svg"||extension==".svgz")return false;return Kind(file)!="text"||AdditionalEditFormats.Contains(extension);}
        [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]
        static extern int AssocQueryString(uint flags,int value,string association,string extra,StringBuilder output,ref uint length);
        static string QueryAssociation(string extension,int value,string verb) {
            uint size=32768;var text=new StringBuilder((int)size);
            return AssocQueryString(0x400,value,extension,verb,text,ref size)==0?text.ToString():null;
        }
        internal static ProcessStartInfo AssociatedEditInfo(string file,Func<string,int,string,string> query=null) {
            query=query??QueryAssociation;
            string extension=Path.GetExtension(file).ToLowerInvariant();
            // Query the user's effective association, including delegated/UWP handlers.
            bool edit=!String.IsNullOrWhiteSpace(query(extension,1,"edit"))||!String.IsNullOrWhiteSpace(query(extension,18,"edit"));
            bool open=edit||!String.IsNullOrWhiteSpace(query(extension,1,"open"))||!String.IsNullOrWhiteSpace(query(extension,18,"open"))||!String.IsNullOrWhiteSpace(query(extension,4,"open"));
            if(!open)throw new IOException(L.Text("editor.noAssociatedApplication"));
            return new ProcessStartInfo(Path.GetFullPath(file)){UseShellExecute=true,Verb=edit?"edit":"open"};
        }
        internal static void StartAssociatedEdit(ProcessStartInfo info,Action<ProcessStartInfo> start) {
            try {start(info);}
            catch(Win32Exception ex) {
                if(ex.NativeErrorCode==1155||ex.NativeErrorCode==31||ex.NativeErrorCode==2||ex.NativeErrorCode==3)
                    throw new IOException(L.Text("editor.noAssociatedApplication"),ex);
                throw new IOException(L.Text("editor.associatedApplicationFailed")+Environment.NewLine+ex.Message,ex);
            }
        }
    }
}
