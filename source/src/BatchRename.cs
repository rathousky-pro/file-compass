using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CommanderCZ
{
    public sealed class RenameOptions
    {
        public string Name = "[N]", Extension = "[E]", Find = "", Replace = "";
        public bool Regex, CaseSensitive, IncludeExtension = true;
        public int Start = 1, Step = 1, Digits = 1, LetterCase;
    }
    public sealed class RenameRow
    {
        public string Path { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
        public string Warning { get; set; }
        public bool Changed { get { return OldName != NewName; } }
        public string Status { get { return Warning ?? (Changed ? L.Text("action.rename") : L.Text("attributes.unchanged")); } }
        public string NewPath { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName); } }
    }
    public static class BatchRenameEngine
    {
        static string Expand(string mask, string name, string ext, int counter, DateTime date)
        {
            string result = Regex.Replace(mask, @"\[(N|E)(\d+)(?:-(\d+))?\]", delegate(Match m)
            {
                string part = m.Groups[1].Value == "N" ? name : ext; int from = Int32.Parse(m.Groups[2].Value) - 1;
                int to = m.Groups[3].Success ? Int32.Parse(m.Groups[3].Value) : from + 1;
                if (from < 0 || to < from + 1) throw new IOException(L.Text("rename.invalidMaskRange"));
                return from >= part.Length ? "" : part.Substring(from, Math.Min(to - from, part.Length - from));
            });
            return result.Replace("[N]", name).Replace("[E]", ext).Replace("[C]", counter.ToString()).Replace("[RMD]", date.ToString("yyyyMMdd")).Replace("[hms]", date.ToString("HHmmss"));
        }
        public static RenameRow[] Preview(string[] paths, RenameOptions options, CancellationToken token = default(CancellationToken))
        {
            if (options.Start < 0 || options.Step < 1 || options.Digits < 1 || options.Digits > 12) throw new IOException(L.Text("rename.invalidCounterSettings"));
            var rows = new List<RenameRow>();
            for (int i = 0; i < paths.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                string path = System.IO.Path.GetFullPath(paths[i]), old = System.IO.Path.GetFileName(path); bool folder = Directory.Exists(path);
                string name = folder ? old : System.IO.Path.GetFileNameWithoutExtension(old), ext = folder ? "" : System.IO.Path.GetExtension(old).TrimStart('.');
                int count = checked(options.Start + i * options.Step); DateTime date = File.GetLastWriteTime(path);
                string nameMask = Expand(options.Name.Replace("[C]", count.ToString("D" + options.Digits)), name, ext, count, date);
                string extensionMask = folder ? "" : Expand(options.Extension.Replace("[C]", count.ToString("D" + options.Digits)), name, ext, count, date);
                if (RenameMetadata.Needed(nameMask + extensionMask))
                {
                    try
                    {
                        var metadata = RenameMetadata.Read(path, token);
                        nameMask = RenameMetadata.Expand(nameMask, metadata); extensionMask = RenameMetadata.Expand(extensionMask, metadata);
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is IOException || ex is TagLib.CorruptFileException || ex is TagLib.UnsupportedFormatException || ex is UnauthorizedAccessException)) throw;
                        rows.Add(new RenameRow { Path = path, OldName = old, NewName = old, Warning = L.Text("rename.metadataSkipped") + " " + ex.Message }); continue;
                    }
                }
                string stem = nameMask;
                string extension = extensionMask;
                Func<string, string> replace = delegate(string input)
                {
                    if (options.Find.Length == 0) return input;
                    return Regex.Replace(input, options.Regex ? options.Find : Regex.Escape(options.Find), options.Regex ? options.Replace : options.Replace.Replace("$", "$$"), options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                };
                string combined = stem + (extension.Length == 0 ? "" : "." + extension);
                if (options.IncludeExtension) combined = replace(combined); else combined = replace(stem) + (extension.Length == 0 ? "" : "." + extension);
                if (options.LetterCase == 1) combined = combined.ToUpperInvariant(); else if (options.LetterCase == 2) combined = combined.ToLowerInvariant(); else if (options.LetterCase == 3) combined = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(combined.ToLower());
                FileOperations.ValidateName(combined); rows.Add(new RenameRow { Path = path, OldName = old, NewName = combined });
            }
            Validate(rows.ToArray(), token); return rows.ToArray();
        }
        static void Validate(RenameRow[] rows, CancellationToken token = default(CancellationToken))
        {
            var originals = new HashSet<string>(rows.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
            if (originals.Count != rows.Length || rows.Select(x => x.NewPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Length) throw new IOException(L.Text("rename.duplicateNewNames"));
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                FileOperations.ValidateName(row.NewName);
                if ((File.GetAttributes(row.Path) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("rename.linksRejected"));
                if ((File.Exists(row.NewPath) || Directory.Exists(row.NewPath)) && !originals.Contains(row.NewPath)) throw new IOException(L.Text("rename.destinationExistsPrefix") + row.NewName);
                for (string parent = System.IO.Path.GetDirectoryName(row.Path); !String.IsNullOrEmpty(parent); parent = System.IO.Path.GetDirectoryName(parent))
                    if (originals.Contains(parent)) throw new IOException(L.Text("rename.parentAndChildRejected"));
            }
        }
        static void Move(string from, string to) { if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to); }
        public static void Apply(RenameRow[] rows, CancellationToken token)
        {
            Validate(rows, token); var staged = new Dictionary<RenameRow, string>(); var committed = new HashSet<RenameRow>();
            try
            {
                foreach (var row in rows) { token.ThrowIfCancellationRequested(); if (row.OldName == row.NewName) continue; string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(row.Path), ".CommanderCZ-rename-" + Guid.NewGuid().ToString("N")); Move(row.Path, temp); staged.Add(row, temp); }
                foreach (var pair in staged) { token.ThrowIfCancellationRequested(); Move(pair.Value, pair.Key.NewPath); committed.Add(pair.Key); }
                FileAssociations.Moved(committed.Select(x => new KeyValuePair<string,string>(x.Path, x.NewPath)));
            }
            catch (Exception error)
            {
                var failures = new List<string>();
                foreach (var row in committed) try { Move(row.NewPath, staged[row]); } catch (Exception ex) { failures.Add(ex.Message); }
                foreach (var pair in staged) try { if (File.Exists(pair.Value) || Directory.Exists(pair.Value)) Move(pair.Value, pair.Key.Path); } catch (Exception ex) { failures.Add(pair.Value + ": " + ex.Message); }
                if (failures.Count > 0) throw new IOException(L.Text("rename.rollbackFailedPrefix") + String.Join("\n", failures), error);
                throw;
            }
        }
    }
}

