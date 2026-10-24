using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
namespace FileCompass {
 internal static class FileRenameFields {
  internal static readonly string[] Tokens={"[File.Created]","[File.CreatedTime]","[File.Modified]","[File.ModifiedTime]","[File.Accessed]","[File.AccessedTime]","[File.Size]","[File.Folder]","[File.Created:yyyyMMdd_HHmmss]","[File.Modified:yyyyMMdd_HHmmss]"};
  internal static bool Needed(string mask){return mask.Contains("[File.");}
  internal static Dictionary<string,object> Read(string path){FileSystemInfo info=Directory.Exists(path)?(FileSystemInfo)new DirectoryInfo(path):new FileInfo(path);info.Refresh();if(!info.Exists)throw new IOException(L.Text("fileFields.missing"));var file=info as FileInfo;return new Dictionary<string,object>{{"File.Created",info.CreationTime},{"File.CreatedTime",info.CreationTime},{"File.Modified",info.LastWriteTime},{"File.ModifiedTime",info.LastWriteTime},{"File.Accessed",info.LastAccessTime},{"File.AccessedTime",info.LastAccessTime},{"File.Size",file==null?null:(object)file.Length},{"File.Folder",Path.GetFileName((Path.GetDirectoryName(path)??"").TrimEnd('\\','/'))}};}
  internal static string Expand(string mask,Dictionary<string,object> values){return Regex.Replace(mask,@"\[(File\.[A-Za-z]+)(?::([^\[\]]{1,100}))?\]",delegate(Match m){object value;if(!values.TryGetValue(m.Groups[1].Value,out value)||value==null)throw new IOException(L.Format("rename.metadataMissing",m.Value));string format=m.Groups[2].Success?m.Groups[2].Value:null,result;
    try{if(value is DateTime)result=((DateTime)value).ToString(format??(m.Groups[1].Value.EndsWith("Time")?"HH-mm-ss":"yyyy-MM-dd"),CultureInfo.InvariantCulture);else if(value is long)result=((long)value).ToString(format??"D",CultureInfo.InvariantCulture);else{if(format!=null)throw new FormatException();result=(string)value;}}catch(FormatException){throw new IOException(L.Text("fileFields.badFormat")+" "+m.Value);}if(String.IsNullOrWhiteSpace(result))throw new IOException(L.Format("rename.metadataMissing",m.Value));return RenameMetadata.Safe(result);
   },RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));}
 }
}
