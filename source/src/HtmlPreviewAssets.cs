using System;
using System.IO;
using System.Collections.Generic;
namespace FileCompass {
 internal static class HtmlPreviewAssets {
  static readonly Dictionary<string,string> Types=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {".css","text/css"},{".png","image/png"},{".jpg","image/jpeg"},{".jpeg","image/jpeg"},{".gif","image/gif"},{".svg","image/svg+xml"},{".webp","image/webp"},{".avif","image/avif"},{".ico","image/x-icon"},{".bmp","image/bmp"},{".woff","font/woff"},{".woff2","font/woff2"},{".ttf","font/ttf"},{".otf","font/otf"}
  };
  internal static Stream Open(string source,string origin,string url,out string mime) {
   mime="text/plain";
   try {
    if(!url.StartsWith(origin,StringComparison.Ordinal))return null;
    string relative=Uri.UnescapeDataString(new Uri(url).AbsolutePath.Substring(new Uri(origin).AbsolutePath.Length));
    if(relative.IndexOfAny(new[]{':','\0'})>=0)return null;
    string root=Path.GetFullPath(Path.GetDirectoryName(source)).TrimEnd('\\')+"\\";
    string file=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
    if(!file.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Types.TryGetValue(Path.GetExtension(file),out mime))return null;
    for(string current=file;!String.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)) {
     if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return null;
     if(String.Equals(current,root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))break;
    }
    var info=new FileInfo(file);if(!info.Exists||info.Length>8L*1024*1024)return null;
    return new MemoryStream(File.ReadAllBytes(file),false);
   }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;}
  }
 }
}
