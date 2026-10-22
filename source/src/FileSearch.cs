using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CommanderCZ
{
    public sealed class SearchHit
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Bytes { get; set; }
        public bool Directory { get; set; }
        public DateTime Modified { get; set; }
        public string DuplicateGroup { get; set; } public string Metadata {get;set;}
        public string Size { get { return Directory ? "<DIR>" : Bytes.ToString("N0"); } }
    }
    public sealed class FileSearchOptions
    {
        public string Root, Mask = "*.*", Text;
        public bool Regex, TextRegex, CaseSensitive, SelectedOnly;
        public int Depth = Int32.MaxValue;
        public long Minimum, Maximum = Int64.MaxValue;
        public DateTime? ModifiedFrom, ModifiedTo;
        public FileAttributes RequiredAttributes, ExcludedAttributes;
        public int EncodingCodePage = 65001;
        public long MaximumTextBytes = 64L * 1024 * 1024;
        public bool Duplicates; public MetadataSearchCondition[] MetadataConditions = new MetadataSearchCondition[0]; [System.Xml.Serialization.XmlIgnore] public Action<string> MetadataInspecting;
    }
    public static class FileSearchEngine
    {
        public static bool Matches(string name, FileSearchOptions options)
        {
            string mask = String.IsNullOrWhiteSpace(options.Mask) ? "*.*" : options.Mask;
            string expression = options.Regex ? mask : "^(?:" + String.Join("|", mask.Split(';').Select(x => x.Trim() == "*.*" ? ".*" : Regex.Escape(x.Trim()).Replace("\\*", ".*").Replace("\\?", "."))) + ")$";
            return Regex.IsMatch(name, expression, options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        }
        public static int Local(FileSearchOptions options, IEnumerable<string> selected, CancellationToken token, Action<SearchHit> found, Action<string> warning)
        {
            MetadataSearch.Validate(options); int count = 0; var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = options.Duplicates ? new List<SearchHit>() : null;
            Action<SearchHit> emit = candidates == null ? found : candidates.Add;
            Action<string, int> walk = null;
            walk = delegate(string path, int depth)
            {
                token.ThrowIfCancellationRequested(); if (count >= 20000) return;
                path = Path.GetFullPath(path); if (!visited.Add(path)) return;
                try
                {
                    var attributes = File.GetAttributes(path); if ((attributes & FileAttributes.ReparsePoint) != 0) return;
                    bool directory = (attributes & FileAttributes.Directory) != 0;
                    long bytes = directory ? 0 : new FileInfo(path).Length;
                    DateTime modified = File.GetLastWriteTimeUtc(path);
                    if (Matches(System.IO.Path.GetFileName(path), options) && bytes >= options.Minimum && bytes <= options.Maximum && SearchAdvanced.Metadata(options,modified,attributes) && (!options.Duplicates || !directory))
                    {
                        bool text = String.IsNullOrEmpty(options.Text);
                        if (!directory && !text)
                        {
                            if (options.MaximumTextBytes > 0 && bytes > options.MaximumTextBytes) warning(L.Text("search.textSkippedByLimitPrefix") + path);
                            else text = SearchAdvanced.ContainsText(path,options,token);
                        }
                        string metadataSummary=""; if(text && MetadataSearch.Enabled(options))text=!directory&&MetadataSearch.Check(path,options,token,warning,out metadataSummary); if (text) { emit(new SearchHit { Path = path, Name = System.IO.Path.GetFileName(path), Bytes = bytes, Directory = directory, Modified = modified, Metadata=metadataSummary }); count++; }
                    }
                    if (directory && depth < options.Depth) foreach (string child in System.IO.Directory.EnumerateFileSystemEntries(path)) walk(child, depth + 1);
                }
                catch (IOException ex) { warning(path + ": " + ex.Message); }
                catch (UnauthorizedAccessException ex) { warning(path + ": " + ex.Message); }
            };
            foreach (string root in options.SelectedOnly ? selected : SearchAdvanced.Roots(options.Root)) {
                if (options.SelectedOnly || File.Exists(root)) walk(root,0);
                else foreach (string path in System.IO.Directory.EnumerateFileSystemEntries(root)) walk(path,0);
            }
            if (candidates != null) return SearchAdvanced.Duplicates(candidates,token,found,warning);
            return count;
        }
        public static int Remote(FileSearchOptions options, IEnumerable<string> selected, RemoteConnection connection, CancellationToken token, Action<SearchHit> found)
        {
            if(MetadataSearch.Enabled(options))throw new IOException(L.Text("metaSearch.localOnly")); int count = 0; Action<string, int> walk = null;
            walk = delegate(string path, int depth)
            {
                token.ThrowIfCancellationRequested(); if (count >= 20000) return;
                var file = connection.Session.GetFileInfo(path); if (Char.ToUpperInvariant(file.FileType) == 'L') return;
                if (Matches(file.Name, options) && SearchAdvanced.Metadata(options,file.LastWriteTime.ToUniversalTime(),0) && (file.IsDirectory || (file.Length >= options.Minimum && file.Length <= options.Maximum)))
                { found(new SearchHit { Path = path, Name = file.Name, Bytes = file.Length, Directory = file.IsDirectory, Modified = file.LastWriteTime }); count++; }
                if (file.IsDirectory && depth < options.Depth)
                    foreach (var child in connection.Session.ListDirectory(path).Files)
                        if (!child.IsParentDirectory && !child.IsThisDirectory && Char.ToUpperInvariant(child.FileType) != 'L') walk(child.FullName, depth + 1);
            };
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string root in options.SelectedOnly ? selected : SearchAdvanced.Roots(options.Root)) {
                foreach (string path in options.SelectedOnly ? new[] { root } : connection.Session.ListDirectory(root).Files.Where(x => !x.IsParentDirectory && !x.IsThisDirectory).Select(x => x.FullName)) if (visited.Add(path)) walk(path,0);
            }
            return count;
        }
    }
}

