using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace CommanderCZ
{
    public sealed class FilePartInfo { public string Name, Hash; public long Length; }
    public sealed class FilePartsManifest { public int Version = 1; public string Name, Hash; public long Length, PartSize; public List<FilePartInfo> Parts = new List<FilePartInfo>(); }
    internal static class FileParts
    {
        internal static long ParseSize(string text)
        {
            var match = Regex.Match((text ?? "").Trim(), @"^(\d+(?:[.,]\d+)?)\s*(B|KB|MB|GB|KIB|MIB|GIB)?$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new IOException(L.Text("parts.invalidSize"));
            decimal value = Decimal.Parse(match.Groups[1].Value.Replace(',','.'), CultureInfo.InvariantCulture);
            string suffix = match.Groups[2].Value.ToUpperInvariant();
            long unit = suffix.StartsWith("K") ? 1024L : suffix.StartsWith("M") ? 1048576L : suffix.StartsWith("G") ? 1073741824L : 1L;
            decimal bytes = value * unit;
            if (bytes < 1 || bytes > Int64.MaxValue || Decimal.Truncate(bytes) != bytes) throw new IOException(L.Text("parts.invalidSize"));
            return (long)bytes;
        }
        internal static string Hash(string file, CancellationToken token)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) return Hash(stream,token);
        }
        internal static string Hash(Stream stream, CancellationToken token)
        {
            using (var hash = SHA256.Create()) {
                byte[] bytes = new byte[1048576]; int count;
                while ((count = stream.Read(bytes,0,bytes.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.TransformBlock(bytes,0,count,bytes,0); }
                hash.TransformFinalBlock(new byte[0],0,0); return BitConverter.ToString(hash.Hash).Replace("-","");
            }
        }
        internal static string BaseName(string part)
        {
            string full = Path.GetFullPath(part);
            var match = Regex.Match(full, @"^(.*)\.(\d{3,6})$");
            if (!match.Success) throw new IOException(L.Text("parts.selectNumbered"));
            return match.Groups[1].Value;
        }
        internal static string[] Discover(string part)
        {
            string name = BaseName(part), folder = Path.GetDirectoryName(name), prefix = Path.GetFileName(name) + ".";
            var found = Directory.EnumerateFiles(folder).Where(x => Path.GetFileName(x).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
                .Select(x => new { Path = x, Number = Path.GetFileName(x).Substring(prefix.Length) })
                .Where(x => Regex.IsMatch(x.Number,@"^\d{3,6}$")).Select(x => new { x.Path, Number = Int32.Parse(x.Number,CultureInfo.InvariantCulture) }).OrderBy(x => x.Number).ToArray();
            if (found.Length == 0 || found[0].Number != 1 || found.Select(x => x.Number).Distinct().Count() != found.Length || found.Where((x,i) => x.Number != i + 1).Any()) throw new IOException(L.Text("parts.missingPart"));
            return found.Select(x => x.Path).ToArray();
        }
        internal static List<string> Split(string source, string outputBase, long size, CancellationToken token, Action<string> progress)
        {
            source = Path.GetFullPath(source); outputBase = Path.GetFullPath(outputBase);
            if (size < 1 || !Directory.Exists(Path.GetDirectoryName(outputBase))) throw new IOException(L.Text("parts.invalidSize"));
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("parts.linkRejected"));
            var published = new List<string>(); var staged = new List<string>();
            string stage = Path.Combine(Path.GetDirectoryName(outputBase), ".FileCompass-parts-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try {
                using (var input = new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
                using (var whole = SHA256.Create()) {
                    long count = Math.Max(1, input.Length / size + (input.Length % size == 0 ? 0 : 1));
                    if (count > 100000) throw new IOException(L.Text("parts.tooMany"));
                    var outputs = Enumerable.Range(1,(int)count).Select(x => outputBase + "." + x.ToString("D3",CultureInfo.InvariantCulture)).ToArray();
                    string manifestTarget = outputBase + ".parts.json";
                    if (outputs.Any(x => File.Exists(x) || Directory.Exists(x)) || File.Exists(manifestTarget) || Directory.Exists(manifestTarget)) throw new IOException(L.Text("parts.outputExists"));
                    // A stale later volume must not be mistaken for part of the new set.
                    string prefix = Path.GetFileName(outputBase) + ".";
                    if (Directory.EnumerateFiles(Path.GetDirectoryName(outputBase)).Any(x => Path.GetFileName(x).StartsWith(prefix,StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(Path.GetFileName(x).Substring(prefix.Length),@"^\d{3,6}$"))) throw new IOException(L.Text("parts.outputExists"));
                    var manifest = new FilePartsManifest { Name = Path.GetFileName(source), Length = input.Length, PartSize = size };
                    byte[] bytes = new byte[1048576]; long done = 0;
                    for (int i = 0; i < outputs.Length; i++) {
                        token.ThrowIfCancellationRequested(); string file = Path.Combine(stage,Path.GetFileName(outputs[i])); staged.Add(file);
                        using (var output = new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        using (var hash = SHA256.Create()) {
                            long remaining = Math.Min(size, input.Length - input.Position), length = remaining;
                            while (remaining > 0) {
                                token.ThrowIfCancellationRequested(); int read = input.Read(bytes,0,(int)Math.Min(bytes.Length,remaining));
                                if (read == 0) throw new EndOfStreamException();
                                output.Write(bytes,0,read); whole.TransformBlock(bytes,0,read,bytes,0); hash.TransformBlock(bytes,0,read,bytes,0);
                                remaining -= read; done += read;
                                if (progress != null) progress(L.Format("parts.progress", i+1, outputs.Length, input.Length == 0 ? 100 : (int)(100.0*done/input.Length)));
                            }
                            output.Flush(true); hash.TransformFinalBlock(new byte[0],0,0);
                            manifest.Parts.Add(new FilePartInfo { Name = Path.GetFileName(outputs[i]), Length = length, Hash = BitConverter.ToString(hash.Hash).Replace("-","") });
                        }
                    }
                    whole.TransformFinalBlock(new byte[0],0,0); manifest.Hash = BitConverter.ToString(whole.Hash).Replace("-","");
                    for (int i = 0; i < staged.Count; i++) if (Hash(staged[i],token) != manifest.Parts[i].Hash) throw new IOException(L.Text("parts.hashMismatch"));
                    string manifestFile = Path.Combine(stage,"manifest.json"); staged.Add(manifestFile);
                    File.WriteAllText(manifestFile,new JavaScriptSerializer { MaxJsonLength = 32*1048576 }.Serialize(manifest),new UTF8Encoding(false));
                    for (int i = 0; i < outputs.Length; i++) { token.ThrowIfCancellationRequested(); File.Move(staged[i],outputs[i]); published.Add(outputs[i]); }
                    File.Move(manifestFile,manifestTarget); published.Add(manifestTarget);
                }
                return published;
            } catch {
                foreach (string file in published) try { File.Delete(file); } catch (IOException) { }
                throw;
            } finally {
                foreach (string file in staged) if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(stage)) Directory.Delete(stage,false);
            }
        }
        internal static void Combine(string part, string target, CancellationToken token, Action<string> progress)
        {
            string[] parts = Discover(part); target = Path.GetFullPath(target);
            if (File.Exists(target) || Directory.Exists(target) || parts.Any(x => WindowsShell.SamePath(x,target))) throw new IOException(L.Text("parts.outputExists"));
            string manifestPath = BaseName(part) + ".parts.json"; FilePartsManifest manifest = null;
            if (File.Exists(manifestPath)) {
                if (new FileInfo(manifestPath).Length > 32*1048576) throw new IOException(L.Text("parts.badManifest"));
                manifest = new JavaScriptSerializer { MaxJsonLength = 32*1048576 }.Deserialize<FilePartsManifest>(File.ReadAllText(manifestPath,Encoding.UTF8));
                if (manifest == null || manifest.Version != 1 || manifest.Parts == null || manifest.Parts.Count != parts.Length ||
                    manifest.Parts.Where((x,i) => x == null || x.Name != Path.GetFileName(parts[i]) || x.Length != new FileInfo(parts[i]).Length).Any()) throw new IOException(L.Text("parts.badManifest"));
            } else {
                long size = new FileInfo(parts[0]).Length;
                if (parts.Take(parts.Length-1).Any(x => new FileInfo(x).Length != size) || new FileInfo(parts.Last()).Length > size) throw new IOException(L.Text("parts.missingPart"));
            }
            string stage = Path.Combine(Path.GetDirectoryName(target),".FileCompass-combine-" + Guid.NewGuid().ToString("N"));
            try {
                long total = parts.Sum(x => new FileInfo(x).Length), done = 0;
                using (var output = new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using (var whole = SHA256.Create()) {
                    byte[] bytes = new byte[1048576];
                    for (int i = 0; i < parts.Length; i++) {
                        token.ThrowIfCancellationRequested();
                        if ((File.GetAttributes(parts[i]) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("parts.linkRejected"));
                        using (var input = new FileStream(parts[i],FileMode.Open,FileAccess.Read,FileShare.Read))
                        using (var hash = SHA256.Create()) {
                            int read; while ((read = input.Read(bytes,0,bytes.Length)) > 0) {
                                token.ThrowIfCancellationRequested(); output.Write(bytes,0,read); hash.TransformBlock(bytes,0,read,bytes,0); whole.TransformBlock(bytes,0,read,bytes,0); done += read;
                                if (progress != null) progress(L.Format("parts.progress",i+1,parts.Length,total == 0 ? 100 : (int)(100.0*done/total)));
                            }
                            hash.TransformFinalBlock(new byte[0],0,0);
                            if (manifest != null && BitConverter.ToString(hash.Hash).Replace("-","") != manifest.Parts[i].Hash) throw new IOException(L.Text("parts.hashMismatch"));
                        }
                    }
                    whole.TransformFinalBlock(new byte[0],0,0); output.Flush(true);
                    if (manifest != null && (done != manifest.Length || BitConverter.ToString(whole.Hash).Replace("-","") != manifest.Hash)) throw new IOException(L.Text("parts.hashMismatch"));
                }
                token.ThrowIfCancellationRequested(); File.Move(stage,target);
            } finally { if (File.Exists(stage)) File.Delete(stage); }
        }
    }
}
