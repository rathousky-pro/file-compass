using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace FileCompass
{
    public sealed class FileTagRecord { public string Path, Identity; public long CreatedUtc; public string[] Tags = new string[0]; }
    public sealed class FileTagColor { public string Name, Color; }
    public sealed class FileTagData
    {
        public List<FileTagRecord> Items = new List<FileTagRecord>();
        public List<FileTagColor> Colors = new List<FileTagColor>();
        public string[] KnownLabels = new string[0];
    }
    internal sealed class FileTagStore
    {
        internal static string TestPath;
        internal static string LastError;
        internal static FileTagStore Current { get { return new FileTagStore(TestPath ?? System.IO.Path.Combine(AppPaths.ConfigDirectory, "tags.xml")); } }
        readonly string path;
        static readonly object gate = new object();
        sealed class Cached { internal long Length; internal DateTime Written; internal FileTagRecord[] Rows; internal Dictionary<string,string> Colors; internal string[] KnownLabels; }
        static readonly Dictionary<string,Cached> cache = new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);
        internal FileTagStore(string path) { this.path = path; }
        internal static string DisplayName(string name)
        {
            return String.Equals(name, "old", StringComparison.OrdinalIgnoreCase) ? "Old" : name;
        }

        internal static string[] Normalize(string text)
        {
            var tags = text.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => DisplayName(x.Trim())).Where(x => x.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
            if (tags.Length > 24 || tags.Any(x => x.Length > 48 || x.Any(Char.IsControl))) throw new IOException(L.Text("tags.invalidTags"));
            return tags;
        }
        FileTagData Read()
        {
            if (!File.Exists(path)) return new FileTagData();
            if (new FileInfo(path).Length > 4 * 1048576) throw new IOException(L.Text("tags.databaseLimit"));
            using (var input = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1048576 }))
                return (FileTagData)new XmlSerializer(typeof(FileTagData)).Deserialize(input);
        }
        internal FileTagRecord[] Snapshot() { return Snapshot(out _); }

        internal FileTagRecord[] Snapshot(out Dictionary<string,string> colors) { return Snapshot(out colors, out _); }

        internal FileTagRecord[] Snapshot(out Dictionary<string,string> colors, out string[] knownLabels)
        {
            colors = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            knownLabels = new string[0];
            lock (gate) try
            {
                var info = new FileInfo(path); Cached saved;
                if (info.Exists && cache.TryGetValue(path, out saved) && saved.Length == info.Length && saved.Written == info.LastWriteTimeUtc) { LastError = null; colors = saved.Colors; knownLabels = saved.KnownLabels; return saved.Rows; }
                var data = Read(); var rows = data.Items.Where(x => x != null && !String.IsNullOrWhiteSpace(x.Path)).ToArray();
                colors = data.Colors.Where(color => color != null && !String.IsNullOrEmpty(color.Name) && FileTagBadge.IsColorCode(color.Color))
                    .GroupBy(color => color.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Last().Color, StringComparer.OrdinalIgnoreCase);
                knownLabels = data.KnownLabels ?? new string[0];
                if (info.Exists) cache[path] = new Cached { Length = info.Length, Written = info.LastWriteTimeUtc, Rows = rows, Colors = colors, KnownLabels = knownLabels }; else cache.Remove(path);
                LastError = null; return rows;
            }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is XmlException)) throw; LastError = ex.Message; return new FileTagRecord[0]; }
        }
        void Update(Action<FileTagData> change)
        {
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); FileStream lease = null;
                for (int attempt = 0; ; attempt++) { try { lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); break; } catch (IOException) { if (attempt >= 30) throw; Thread.Sleep(50); } }
                using (lease)
                {
                    var data = Read(); change(data);
                    string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var output = File.Create(temp)) { new XmlSerializer(typeof(FileTagData)).Serialize(output, data); if (output.Length > 4 * 1048576) throw new IOException(L.Text("tags.databaseLimit")); output.Flush(true); }
                        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                        cache.Remove(path); LastError = null;
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
            }
        }
        internal void Change(string[] paths, string[] tags, int mode, IDictionary<string,string> colors = null)
        {
            Update(delegate(FileTagData data)
            {
                var known = new HashSet<string>(data.KnownLabels ?? new string[0], StringComparer.OrdinalIgnoreCase);
                if (colors != null)
                    foreach (var color in colors)
                    {
                        data.Colors.RemoveAll(item => item != null && String.Equals(item.Name, color.Key, StringComparison.OrdinalIgnoreCase));
                        if (FileTagBadge.IsColorCode(color.Value))
                            data.Colors.Add(new FileTagColor { Name = color.Key, Color = color.Value });
                    }
                foreach (string item in paths)
                {
                    string full = System.IO.Path.GetFullPath(item); var attributes = File.GetAttributes(full);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("tags.linksUnsupported"));
                    long created = File.GetCreationTimeUtc(full).Ticks; string identity = LocalFileIdentity.Read(full);
                    var old = data.Items.FirstOrDefault(x => String.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase));
                    var existing = old == null || (old.Identity == null ? old.CreatedUtc != created : old.Identity != identity) ? new string[0] : old.Tags.Select(DisplayName).ToArray();
                    var selected = mode == 0 ? existing.Concat(tags).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray() : mode == 1 ? tags : existing.Except(tags, StringComparer.CurrentCultureIgnoreCase).ToArray();
                    if (selected.Length > 24) throw new IOException(L.Text("tags.invalidTags"));
                    known.UnionWith(existing);
                    known.UnionWith(selected);
                    if (old != null) data.Items.Remove(old);
                    if (selected.Length > 0) data.Items.Add(new FileTagRecord { Path = full, Identity = identity, CreatedUtc = created, Tags = selected });
                }
                data.KnownLabels = known.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            });
        }
        internal static bool MatchesCurrentItem(FileTagRecord record)
        {
            try
            {
                return record.Identity != null
                    ? LocalFileIdentity.Read(record.Path) == record.Identity
                    : (File.Exists(record.Path) || Directory.Exists(record.Path)) &&
                        File.GetCreationTimeUtc(record.Path).Ticks == record.CreatedUtc;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }
        internal static void Apply(IEnumerable<Entry> rows)
        {
            var records = Current.Snapshot(out var colors).GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.IsParent) continue; FileTagRecord value;
                string[] tags = new string[0];
                if (records.TryGetValue(row.FullPath, out value) && MatchesCurrentItem(value))
                    tags = (value.Tags ?? new string[0]).Select(DisplayName).ToArray();
                row.TagColors = tags.Length == 0 ? null : colors;
                row.TagNames = tags;
            }
        }
        internal void Relocate(IEnumerable<KeyValuePair<string,string>> mappings)
        {
            var moves = mappings.OrderByDescending(x => x.Key.Length).ToArray();
            if (!Snapshot().Any(r => moves.Any(x => String.Equals(r.Path, x.Key, StringComparison.OrdinalIgnoreCase) || r.Path.StartsWith(x.Key.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))) return;
            Update(delegate(FileTagData data)
            {
                foreach (var record in data.Items)
                    foreach (var move in moves)
                        if (String.Equals(record.Path, move.Key, StringComparison.OrdinalIgnoreCase) || record.Path.StartsWith(move.Key.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        {
                            record.Path = move.Value.TrimEnd('\\') + record.Path.Substring(move.Key.TrimEnd('\\').Length);
                            if (File.Exists(record.Path) || Directory.Exists(record.Path)) { record.CreatedUtc = File.GetCreationTimeUtc(record.Path).Ticks; record.Identity = LocalFileIdentity.Read(record.Path); }
                            break;
                        }
                data.Items = data.Items.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToList();
            });
        }
        internal static void Moved(string from, string to) { Moved(new[] { new KeyValuePair<string,string>(from, to) }); }
        internal static void Moved(IEnumerable<KeyValuePair<string,string>> moves)
        {
            try { Current.Relocate(moves); }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is XmlException)) throw; LastError = ex.Message; }
        }
    }
}

