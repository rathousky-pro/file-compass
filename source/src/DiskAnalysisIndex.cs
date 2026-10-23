using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace CommanderCZ {
    internal sealed class DiskScanOptions {
        internal long MinimumBytes;
        internal double MinimumAgeDays;
        internal string Category="", Masks="", Exclusions="";
        internal bool StoreIndex;
        internal int SnapshotIntervalMilliseconds=500;
        internal static readonly string[] CategoryKeys={"images","video","audio","documents","archives","code","programs","other"};
        static readonly string[] extensions={"jpg jpeg png gif bmp tif tiff webp svg ico heic avif raw cr2 nef", "mp4 mkv avi mov webm m4v wmv mpg mpeg ts", "mp3 flac wav aac ogg m4a wma opus", "pdf doc docx xls xlsx ppt pptx odt ods odp txt rtf epub", "zip rar 7z tar gz bz2 xz iso cab arj lzh", "cs cpp c h hpp py js ts tsx jsx java go rs rb php html htm css scss json xml yaml yml sql ps1 sh md", "exe dll msi msp com bat cmd sys"};
        internal static string FileCategory(string path) { string ext=Path.GetExtension(path).TrimStart('.').ToLowerInvariant();for(int i=0;i<extensions.Length;i++)if((" "+extensions[i]+" ").Contains(" "+ext+" ")&&ext.Length>0)return CategoryKeys[i];return "other"; }
        internal string Signature { get { return MinimumBytes+"|"+MinimumAgeDays.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+Category+"|"+Masks.Trim().ToLowerInvariant()+"|"+Exclusions.Trim().ToLowerInvariant(); } }
        internal string Description {get{return (Category.Length==0?L.Text("analyzer.allCategories"):L.Text("analyzer.category."+Category))+" · "+L.Text("analyzer.minimumMiB")+": "+(MinimumBytes/1048576m).ToString("0.##")+" · "+L.Text("analyzer.minimumDays")+": "+MinimumAgeDays.ToString("0.##")+" · "+L.Text("analyzer.masks")+": "+(Masks.Length==0?"—":Masks)+" · "+L.Text("analyzer.excludeFolders")+": "+(Exclusions.Length==0?"—":Exclusions);}}
        internal bool Accept(string path,long bytes,DateTime modified,DateTime clock) {
            if(bytes<MinimumBytes||(MinimumAgeDays>0&&(clock-modified).TotalDays<MinimumAgeDays)||(Category.Length>0&&FileCategory(path)!=Category))return false;
            return String.IsNullOrWhiteSpace(Masks)||Masks.Split(';').Where(x=>x.Trim().Length>0).Any(x=>Mask(Path.GetFileName(path),x.Trim()));
        }
        internal bool Exclude(string root,string directory) {
            string relative=directory.Substring(root.TrimEnd('\\').Length).TrimStart('\\');
            foreach(string value in Exclusions.Split(';')) {string rule=value.Trim().Replace('/','\\').TrimEnd('\\');if(rule.Length==0)continue;
                if(Path.IsPathRooted(rule)) { if(DiskFileIndex.Within(directory,rule))return true; }
                else if(rule.IndexOf('\\')>=0) { if(Mask(relative,rule)||relative.StartsWith(rule+"\\",StringComparison.OrdinalIgnoreCase))return true; }
                else if(Mask(Path.GetFileName(directory),rule))return true;
            }return false;
        }
        static bool Mask(string name,string mask) { return Regex.IsMatch(name,"^"+Regex.Escape(mask).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant); }
    }
    internal static class DiskFileIndex {
        internal static bool Within(string path,string folder) { return String.Equals(path.TrimEnd('\\'),folder.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||path.StartsWith(folder.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase); }
        internal static IEnumerable<DiskLargeFile> Read(string path,CancellationToken token) {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new BinaryReader(file,Encoding.UTF8)) {
                long end=file.Length;
                while(file.Position<end) {token.ThrowIfCancellationRequested();DiskLargeFile row;
                    try { row=new DiskLargeFile{Path=reader.ReadString(),Bytes=reader.ReadInt64(),Modified=new DateTime(reader.ReadInt64(),DateTimeKind.Utc).ToLocalTime()}; }
                    catch(EndOfStreamException){yield break;}
                    yield return row;
                }
            }
        }
        internal static List<DiskLargeFile> Page(string path,Func<DiskLargeFile,bool> match,int page,int pageSize,CancellationToken token,out long total) {
            var rows=new List<DiskLargeFile>();total=0;long skip=(long)page*pageSize;
            foreach(var file in Read(path,token))if(match(file)) {if(total>=skip&&rows.Count<pageSize)rows.Add(file);total++;}return rows;
        }
        internal static string Bucket(DiskLargeFile file,int group,string folder,DateTime clock) {
            if(group==0){if(!Within(file.Path,folder))return null;string relative=file.Path.Substring(folder.TrimEnd('\\').Length).TrimStart('\\');int slash=relative.IndexOf('\\');return slash<0?"__direct":"folder:"+relative.Substring(0,slash);}
            if(group==1){string ext=Path.GetExtension(file.Path);return ext.Length==0?"analyzer.noExtension":ext.ToLowerInvariant();}
            if(group==2)return "analyzer.age"+DiskAnalysis.AgeBucket(file.Modified.ToUniversalTime(),clock);
            if(group==3)return "analyzer.size"+DiskAnalysis.SizeBucket(file.Bytes);
            return "analyzer.category."+DiskScanOptions.FileCategory(file.Path);
        }
        internal static void Delete(string path){if(String.IsNullOrEmpty(path))return;try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
