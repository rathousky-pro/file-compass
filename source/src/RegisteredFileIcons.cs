using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace FileCompass {
    public static partial class DesktopIcons {
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint ExtractIconEx(string file,int index,IntPtr[] large,IntPtr[] small,uint count);
        static ImageSource RegisteredFileIcon(string extension,string actual) {
            if(String.IsNullOrEmpty(extension))return null;
            try {
                string type=null;
                using(var choice=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\"+extension+@"\UserChoice"))if(choice!=null)type=choice.GetValue("ProgId") as string;
                if(String.IsNullOrEmpty(type))using(var association=Registry.ClassesRoot.OpenSubKey(extension))if(association!=null)type=association.GetValue("") as string;
                if(String.IsNullOrEmpty(type))return null;
                string value=null;using(var icon=Registry.ClassesRoot.OpenSubKey(type+@"\DefaultIcon"))if(icon!=null)value=icon.GetValue("") as string;
                if(String.IsNullOrWhiteSpace(value))return null;
                value=Environment.ExpandEnvironmentVariables(value);int index=0,comma=value.LastIndexOf(',');
                if(comma>=0 && Int32.TryParse(value.Substring(comma+1).Trim(),out index))value=value.Substring(0,comma);
                value=value.Trim().Trim('"');if(value=="%1")value=actual;
                if(String.IsNullOrEmpty(value)||!File.Exists(value))return null;
                var small=new IntPtr[1];var large=new IntPtr[1];
                try {
                    if(ExtractIconEx(value,index,large,small,1)==0)return null;
                    var handle=small[0]!=IntPtr.Zero?small[0]:large[0];if(handle==IntPtr.Zero)return null;
                    var image=Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;
                }finally{if(small[0]!=IntPtr.Zero)DestroyIcon(small[0]);if(large[0]!=IntPtr.Zero)DestroyIcon(large[0]);}
            }catch(System.Security.SecurityException){return null;}catch(IOException){return null;}catch(ArgumentException){return null;}catch(UnauthorizedAccessException){return null;}
        }
    }
}
