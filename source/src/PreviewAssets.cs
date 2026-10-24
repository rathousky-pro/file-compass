using System;
using System.IO;
using System.Reflection;
namespace FileCompass {
 internal static class PreviewAssets {
  static readonly Lazy<Assembly> library=new Lazy<Assembly>(delegate{
   string path=Path.GetFullPath(Path.Combine(DocumentPreview.Assets,"preview.dll"));
   PathSafety.RejectLinks(path);return Assembly.LoadFile(path);
  });
  internal static Stream Open(string name){
   if(Array.IndexOf(new[]{"index.html","viewer.js","docx-preview.min.js","jszip.min.js","xlsx.full.min.js"},name)<0)return null;
   var stream=library.Value.GetManifestResourceStream("FileCompass.Preview."+name);
   if(stream==null)throw new IOException("Missing embedded preview resource: "+name);return stream;
  }
 }
}
