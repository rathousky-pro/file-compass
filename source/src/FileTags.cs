using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace CommanderCZ
{
    public sealed class FileTagRecord { public string Path, Identity; public long CreatedUtc; public string[] Tags = new string[0]; }
    public sealed class FileTagData { public List<FileTagRecord> Items = new List<FileTagRecord>(); }
    internal sealed class FileTagStore
    {
        internal static string TestPath;
        internal static string LastError;
        internal static FileTagStore Current { get { return new FileTagStore(TestPath ?? System.IO.Path.Combine(AppPaths.ConfigDirectory, "tags.xml")); } }
        readonly string path;
        static readonly object gate = new object();
        sealed class Cached { internal long Length; internal DateTime Written; internal FileTagRecord[] Rows; }
        static readonly Dictionary<string,Cached> cache = new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);
        internal FileTagStore(string path) { this.path = path; }
        internal static string[] Normalize(string text)
        {
            var tags = text.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
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
        internal FileTagRecord[] Snapshot()
        {
            lock (gate) try
            {
                var info = new FileInfo(path); Cached saved;
                if (info.Exists && cache.TryGetValue(path, out saved) && saved.Length == info.Length && saved.Written == info.LastWriteTimeUtc) { LastError = null; return saved.Rows; }
                var data = Read(); var rows = data.Items.Where(x => x != null && !String.IsNullOrWhiteSpace(x.Path)).ToArray();
                if (info.Exists) cache[path] = new Cached { Length = info.Length, Written = info.LastWriteTimeUtc, Rows = rows }; else cache.Remove(path);
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
        internal void Change(string[] paths, string[] tags, int mode)
        {
            Update(delegate(FileTagData data)
            {
                foreach (string item in paths)
                {
                    string full = System.IO.Path.GetFullPath(item); var attributes = File.GetAttributes(full);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("tags.linksUnsupported"));
                    long created = File.GetCreationTimeUtc(full).Ticks; string identity = LocalFileIdentity.Read(full);
                    var old = data.Items.FirstOrDefault(x => String.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase));
                    var existing = old == null || (old.Identity == null ? old.CreatedUtc != created : old.Identity != identity) ? new string[0] : old.Tags;
                    var selected = mode == 0 ? existing.Concat(tags).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray() : mode == 1 ? tags : existing.Except(tags, StringComparer.CurrentCultureIgnoreCase).ToArray();
                    if (selected.Length > 24) throw new IOException(L.Text("tags.invalidTags"));
                    if (old != null) data.Items.Remove(old);
                    if (selected.Length > 0) data.Items.Add(new FileTagRecord { Path = full, Identity = identity, CreatedUtc = created, Tags = selected });
                }
            });
        }
        internal static void Apply(IEnumerable<Entry> rows)
        {
            var records = Current.Snapshot().GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.IsParent) continue; FileTagRecord value;
                try { row.TagNames = records.TryGetValue(row.FullPath, out value) && (value.Identity == null ? File.GetCreationTimeUtc(row.FullPath).Ticks == value.CreatedUtc : LocalFileIdentity.Read(row.FullPath) == value.Identity) ? value.Tags ?? new string[0] : new string[0]; }
                catch (IOException) { row.TagNames = new string[0]; } catch (UnauthorizedAccessException) { row.TagNames = new string[0]; }
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

