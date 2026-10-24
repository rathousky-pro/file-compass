using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace FileCompass
{
    public sealed class SearchProfile
    {
        public string Name; public FileSearchOptions Options;
        public override string ToString() { return Name; }
    }
    internal static class SearchAdvanced
    {
        internal static string TestDirectory;
        internal static string ProfilePath { get { return Path.Combine(TestDirectory ?? AppPaths.ConfigDirectory,"search-profiles.xml"); } }
        internal static string[] Roots(string value) { return (value ?? "").Split(new[] { ';','\r','\n' },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().Trim('"')).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
        internal static bool Metadata(FileSearchOptions options,DateTime modified,FileAttributes attributes)
        { return (!options.ModifiedFrom.HasValue || modified >= options.ModifiedFrom.Value.ToUniversalTime()) && (!options.ModifiedTo.HasValue || modified <= options.ModifiedTo.Value.ToUniversalTime()) && (attributes & options.RequiredAttributes) == options.RequiredAttributes && (attributes & options.ExcludedAttributes) == 0; }
        internal static bool ContainsText(string path,FileSearchOptions options,CancellationToken token)
        {
            if (options.Text.Length > 65536) throw new IOException(L.Text("search.textLengthLimit"));
            using (var reader = new StreamReader(path,Encoding.GetEncoding(options.EncodingCodePage),true,65536)) {
                char[] buffer = new char[65536]; string tail = ""; int read;
                while ((read = reader.Read(buffer,0,buffer.Length)) > 0) {
                    token.ThrowIfCancellationRequested(); string part = tail + new string(buffer,0,read);
                    if (options.TextRegex) {
                        int start = 0,end;
                        while ((end = part.IndexOf('\n',start)) >= 0) { if (end - start > 131072) throw new IOException(L.Text("search.regexLineLengthLimit")); if (TextMatches(part.Substring(start,end - start).TrimEnd('\r'),options)) return true; start = end + 1; }
                        tail = part.Substring(start); if (tail.Length > 131072) throw new IOException(L.Text("search.regexLineLengthLimit")); continue;
                    }
                    bool matches = part.IndexOf(options.Text,options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;
                    if (matches) return true;
                    int keep = Math.Max(0,options.Text.Length - 1); tail = part.Substring(Math.Max(0,part.Length - keep));
                }
                if (options.TextRegex && TextMatches(tail.TrimEnd('\r'),options)) return true;
            } return false;
        }
        static bool TextMatches(string line,FileSearchOptions options) { return Regex.IsMatch(line,options.Text,options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase,TimeSpan.FromSeconds(1)); }
        internal static int Duplicates(IEnumerable<SearchHit> candidates,CancellationToken token,Action<SearchHit> found,Action<string> warning)
        {
            int count = 0, group = 0;
            foreach (var size in candidates.GroupBy(x => x.Bytes).Where(x => x.Count() > 1)) {
                var hashes = new Dictionary<string,List<SearchHit>>();
                foreach (var hit in size) try {
                    token.ThrowIfCancellationRequested(); byte[] buffer = new byte[65536];
                    using (var stream = new FileStream(hit.Path,FileMode.Open,FileAccess.Read,FileShare.Read)) using (var hash = SHA256.Create()) {
                        if (stream.Length != hit.Bytes || File.GetLastWriteTimeUtc(hit.Path) != hit.Modified) throw new IOException(L.Text("search.fileChangedAfterMatch"));
                        int read; while ((read = stream.Read(buffer,0,buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer,0,read,null,0); } hash.TransformFinalBlock(new byte[0],0,0); if (File.GetLastWriteTimeUtc(hit.Path) != hit.Modified) throw new IOException(L.Text("compare.fileChangedDuringContentCheck"));
                        string key = Convert.ToBase64String(hash.Hash); List<SearchHit> list; if (!hashes.TryGetValue(key,out list)) hashes[key] = list = new List<SearchHit>(); list.Add(hit);
                    }
                } catch (IOException ex) { warning(hit.Path + ": " + ex.Message); } catch (UnauthorizedAccessException ex) { warning(hit.Path + ": " + ex.Message); }
                foreach (var equal in hashes.Values.Where(x => x.Count > 1)) { group++; foreach (var hit in equal) { hit.DuplicateGroup = "Skupina " + group; found(hit); count++; } }
            } return count;
        }
        internal static List<SearchProfile> LoadProfiles()
        { if (!File.Exists(ProfilePath)) return new List<SearchProfile>(); using (var reader = XmlReader.Create(ProfilePath,new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,XmlResolver = null })) return (List<SearchProfile>)new XmlSerializer(typeof(List<SearchProfile>)).Deserialize(reader); }
        internal static void SaveProfiles(List<SearchProfile> values)
        { Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)); string temp = ProfilePath + ".tmp"; using (var writer = XmlWriter.Create(temp,new XmlWriterSettings { Indent = true })) new XmlSerializer(typeof(List<SearchProfile>)).Serialize(writer,values); if (File.Exists(ProfilePath)) File.Replace(temp,ProfilePath,null); else File.Move(temp,ProfilePath); }
    }
}

