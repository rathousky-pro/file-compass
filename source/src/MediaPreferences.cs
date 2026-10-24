using System;
using System.IO;
using System.Globalization;
namespace FileCompass {
 internal static class MediaPreferences {
  internal static int LoadWorkers(){try{int value;if(Int32.TryParse(File.ReadAllText(Path.Combine(AppPaths.ConfigDirectory,"media-workers.txt")),out value)&&Array.IndexOf(new[]{1,2,3,4,6,8},value)>=0)return value;}catch(IOException){}catch(UnauthorizedAccessException){}return 2;}
  internal static void SaveWorkers(int value){if(Array.IndexOf(new[]{1,2,3,4,6,8},value)<0)return;string temp=null;try{Directory.CreateDirectory(AppPaths.ConfigDirectory);string path=Path.Combine(AppPaths.ConfigDirectory,"media-workers.txt");temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,value.ToString(CultureInfo.InvariantCulture));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}catch(IOException){}catch(UnauthorizedAccessException){}finally{if(temp!=null&&File.Exists(temp))try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}}
 }
}
