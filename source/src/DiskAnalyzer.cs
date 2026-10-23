using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace CommanderCZ {
    internal sealed class DiskBucket {
        internal string Key;
        public string Name { get; set; }
        public long Bytes { get; set; }
        public long Files { get; set; }
        public string Size { get { return DiskAnalysis.FormatSize(Bytes); } }
    }
    internal sealed class DiskFolder {
        public string Path { get; set; }
        public string Name { get { return System.IO.Path.GetFileName(Path.TrimEnd('\\')); } }
        public long Bytes { get; set; }
        public long Files { get; set; }
        public string Size { get { return DiskAnalysis.FormatSize(Bytes); } }
        internal long DirectBytes, DirectFiles;
        internal DiskFolder Parent;
        internal readonly List<DiskFolder> Children = new List<DiskFolder>();
    }
    internal sealed class DiskLargeFile {
        internal string RootPath;
        public string Path { get; set; }
        public long Bytes { get; set; }
        public DateTime Modified { get; set; }
        public string Size { get { return DiskAnalysis.FormatSize(Bytes); } }
        public string RelativePath {get{return !String.IsNullOrEmpty(RootPath)&&DiskFileIndex.Within(Path,RootPath)?Path.Substring(RootPath.TrimEnd('\\').Length).TrimStart('\\'):Path;}}
        public string Category { get { return L.Text("analyzer.category."+DiskScanOptions.FileCategory(Path)); } }
    }
    internal sealed class DiskAnalysis {
        internal DiskFolder Root;
        internal readonly Dictionary<string, DiskBucket> Extensions = new Dictionary<string, DiskBucket>(StringComparer.OrdinalIgnoreCase);
        internal readonly DiskBucket[] Ages = Enumerable.Range(0,6).Select(i => new DiskBucket { Name = "analyzer.age" + i }).ToArray();
        internal readonly DiskBucket[] Sizes = Enumerable.Range(0,6).Select(i => new DiskBucket { Name = "analyzer.size" + i }).ToArray();
        internal readonly List<DiskLargeFile> Largest = new List<DiskLargeFile>();
        internal readonly List<string> Issues = new List<string>();
        internal long Skipped, Folders;
        internal bool Cancelled, IsPartial;
        internal string IndexPath, FilterSignature="", FilterDescription="";
        internal DateTime ScannedUtc;
        internal readonly Dictionary<string,DiskBucket> Categories=new Dictionary<string,DiskBucket>();
        internal static string FormatSize(long bytes) {
            string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" }; double value = bytes; int unit = 0;
            while (value >= 1024 && unit < units.Length-1) { value /= 1024; unit++; }
            return value.ToString(unit == 0 ? "N0" : "N2") + " " + units[unit];
        }
        internal static DiskAnalysis Scan(string path, CancellationToken token, Action<long,long,string> progress, DateTime? now = null, DiskScanOptions options=null, Action<DiskAnalysis> update=null) {
            var result = new DiskAnalysis { Root = new DiskFolder { Path = System.IO.Path.GetFullPath(path) } };
            if (!Directory.Exists(result.Root.Path)) throw new DirectoryNotFoundException(path);
            options=options??new DiskScanOptions();result.FilterSignature=options.Signature;result.FilterDescription=options.Description;result.ScannedUtc=now??DateTime.UtcNow;
            BinaryWriter index=null;
            if(options.StoreIndex){result.IndexPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"FileCompass-analyzer-"+Guid.NewGuid().ToString("N")+".idx");index=new BinaryWriter(new FileStream(result.IndexPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read|FileShare.Delete),System.Text.Encoding.UTF8);}
            try {
            var stack = new Stack<DiskFolder>(); var visited = new List<DiskFolder>(); stack.Push(result.Root);
            DateTime clock = result.ScannedUtc; var timer = System.Diagnostics.Stopwatch.StartNew();
            while (stack.Count > 0 && !token.IsCancellationRequested) {
                var folder = stack.Pop(); visited.Add(folder); result.Folders++;
                try {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(folder.Path)) {
                        if (token.IsCancellationRequested) break;
                        if(String.Equals(entry,result.IndexPath,StringComparison.OrdinalIgnoreCase))continue;
                        try {
                            var attributes = File.GetAttributes(entry);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) { result.Issue(entry, L.Text("analyzer.linkSkipped")); continue; }
                            if ((attributes & FileAttributes.Directory) != 0) { if(options.Exclude(result.Root.Path,entry))continue;var child = new DiskFolder { Path = entry, Parent = folder }; folder.Children.Add(child); stack.Push(child); }
                            else {
                                var info = new FileInfo(entry); long bytes = info.Length; DateTime modified = info.LastWriteTimeUtc;
                                if(!options.Accept(entry,bytes,modified,clock))continue;
                                if(index!=null)try{index.Write(entry);index.Write(bytes);index.Write(modified.Ticks);}catch(IOException ex){throw new InvalidOperationException(L.Text("analyzer.indexFailed"),ex);}
                                folder.DirectBytes += bytes; folder.DirectFiles++;
                                for(var ancestor=folder;ancestor!=null;ancestor=ancestor.Parent){ancestor.Bytes+=bytes;ancestor.Files++;}
                                string category="analyzer.category."+DiskScanOptions.FileCategory(entry);DiskBucket categoryBucket;if(!result.Categories.TryGetValue(category,out categoryBucket))result.Categories[category]=categoryBucket=new DiskBucket{Name=category};categoryBucket.Bytes+=bytes;categoryBucket.Files++;
                                string extension = System.IO.Path.GetExtension(entry); if (extension.Length == 0) extension = "analyzer.noExtension";
                                DiskBucket bucket; if (!result.Extensions.TryGetValue(extension, out bucket)) result.Extensions[extension] = bucket = new DiskBucket { Name = extension.StartsWith("analyzer.",StringComparison.Ordinal)?extension:extension.ToLowerInvariant() };
                                bucket.Bytes += bytes; bucket.Files++;
                                int age = AgeBucket(modified,clock);
                                result.Ages[age].Bytes += bytes; result.Ages[age].Files++;
                                int size = SizeBucket(bytes);
                                result.Sizes[size].Bytes += bytes; result.Sizes[size].Files++;
                                if (result.Largest.Count < 100 || bytes > result.Largest[result.Largest.Count-1].Bytes) {
                                    result.Largest.Add(new DiskLargeFile { Path = entry, RootPath=result.Root.Path, Bytes = bytes, Modified = modified.ToLocalTime() });
                                    result.Largest.Sort((a,b) => b.Bytes.CompareTo(a.Bytes)); if (result.Largest.Count > 100) result.Largest.RemoveAt(100);
                                }
                            }
                        } catch (UnauthorizedAccessException ex) { result.Issue(entry, ex.Message); } catch (IOException ex) { result.Issue(entry, ex.Message); }
                        if(timer.ElapsedMilliseconds>=options.SnapshotIntervalMilliseconds){if(index!=null)index.Flush();if(progress!=null)progress(result.Root.Files,result.Root.Bytes,folder.Path);if(update!=null)update(result.Snapshot());timer.Restart();}
                    }
                } catch (UnauthorizedAccessException ex) { result.Issue(folder.Path, ex.Message); } catch (IOException ex) { result.Issue(folder.Path, ex.Message); }
            }
            result.Cancelled = token.IsCancellationRequested;result.IsPartial=result.Cancelled;if(index!=null)index.Flush();
            return result;
            }catch{DiskFileIndex.Delete(result.IndexPath);throw;}finally{if(index!=null)index.Dispose();}
        }
        internal static int AgeBucket(DateTime modified,DateTime clock){double days=(clock-modified).TotalDays;return days<7?0:days<30?1:days<90?2:days<365?3:days<1095?4:5;}
        internal static int SizeBucket(long bytes){return bytes<1024?0:bytes<1048576?1:bytes<104857600?2:bytes<1073741824L?3:bytes<10737418240L?4:5;}
        internal DiskAnalysis Snapshot(){
            var copy=new DiskAnalysis{Root=CloneFolder(Root),Skipped=Skipped,Folders=Folders,IndexPath=IndexPath,FilterSignature=FilterSignature,FilterDescription=FilterDescription,ScannedUtc=ScannedUtc,IsPartial=true};
            foreach(var child in Root.Children)copy.Root.Children.Add(CloneFolder(child));
            foreach(var item in Extensions)copy.Extensions[item.Key]=new DiskBucket{Name=item.Value.Name,Bytes=item.Value.Bytes,Files=item.Value.Files};
            foreach(var item in Categories)copy.Categories[item.Key]=new DiskBucket{Name=item.Value.Name,Bytes=item.Value.Bytes,Files=item.Value.Files};
            for(int i=0;i<6;i++){copy.Ages[i].Bytes=Ages[i].Bytes;copy.Ages[i].Files=Ages[i].Files;copy.Sizes[i].Bytes=Sizes[i].Bytes;copy.Sizes[i].Files=Sizes[i].Files;}
            copy.Largest.AddRange(Largest);copy.Issues.AddRange(Issues);return copy;
        }
        static DiskFolder CloneFolder(DiskFolder f){return new DiskFolder{Path=f.Path,Bytes=f.Bytes,Files=f.Files,DirectBytes=f.DirectBytes,DirectFiles=f.DirectFiles};}
        void Issue(string path, string reason) { Skipped++; if (Issues.Count < 500) Issues.Add(path + " — " + reason); }
    }
}
