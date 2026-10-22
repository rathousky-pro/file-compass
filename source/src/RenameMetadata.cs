using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CommanderCZ
{
    internal static class RenameMetadata
    {
        internal static readonly string[] Tokens = { "[EXIF.Date]", "[EXIF.Time]", "[EXIF.Camera]", "[MP3.Artist]", "[MP3.Album]", "[MP3.Title]", "[MP3.Track]", "[MP3.Year]" };
        internal static bool Needed(string mask) { return mask.Contains("[EXIF.") || mask.Contains("[MP3."); }
        internal static string Safe(string value)
        {
            if (value == null) return "";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => c < 32 || invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        }
        internal static Dictionary<string, string> Read(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var values = Tokens.ToDictionary(x => x, x => "");
            using (var source = new MetadataSource(path, token))
            using (var media = TagLib.File.Create(source, TagLib.ReadStyle.None))
            {
                var image = media.Tag as TagLib.Image.CombinedImageTag;
                if (image != null)
                {
                    if (image.DateTime.HasValue) { values["[EXIF.Date]"] = image.DateTime.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); values["[EXIF.Time]"] = image.DateTime.Value.ToString("HH-mm-ss", CultureInfo.InvariantCulture); }
                    values["[EXIF.Camera]"] = Safe(String.Join(" ", new[] { image.Make, image.Model }.Where(x => !String.IsNullOrWhiteSpace(x))));
                }
                values["[MP3.Artist]"] = Safe(String.Join(", ", media.Tag.Performers ?? new string[0]));
                values["[MP3.Album]"] = Safe(media.Tag.Album); values["[MP3.Title]"] = Safe(media.Tag.Title);
                values["[MP3.Track]"] = media.Tag.Track == 0 ? "" : media.Tag.Track.ToString("D2", CultureInfo.InvariantCulture);
                values["[MP3.Year]"] = media.Tag.Year == 0 ? "" : media.Tag.Year.ToString(CultureInfo.InvariantCulture);
            }
            token.ThrowIfCancellationRequested(); return values;
        }
        internal static string Expand(string mask, Dictionary<string,string> values)
        {
            return Regex.Replace(mask, @"\[(?:EXIF|MP3)\.[A-Za-z]+\]", delegate(Match match)
            {
                string value; if (!values.TryGetValue(match.Value, out value) || String.IsNullOrWhiteSpace(value)) throw new IOException(L.Format("rename.metadataMissing", match.Value));
                return value;
            });
        }
        sealed class MetadataSource : TagLib.File.IFileAbstraction, IDisposable
        {
            readonly List<Stream> streams = new List<Stream>(); readonly CancellationToken token;
            public string Name { get; private set; }
            internal MetadataSource(string path, CancellationToken token) { Name = path; this.token = token; }
            public Stream ReadStream { get { var stream = new BoundedRead(Name, token); streams.Add(stream); return stream; } }
            public Stream WriteStream { get { throw new NotSupportedException(); } }
            public void CloseStream(Stream stream) { stream.Dispose(); streams.Remove(stream); }
            public void Dispose() { foreach (var stream in streams) stream.Dispose(); streams.Clear(); }
        }
        sealed class BoundedRead : Stream
        {
            readonly FileStream input; readonly CancellationToken token; long bytes;
            internal BoundedRead(string path, CancellationToken token) { input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); this.token = token; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                token.ThrowIfCancellationRequested();
                if (bytes + count > 16L * 1048576) throw new IOException(L.Text("rename.metadataLimit"));
                int read = input.Read(buffer, offset, count); bytes += read; return read;
            }
            public override long Seek(long offset, SeekOrigin origin) { token.ThrowIfCancellationRequested(); return input.Seek(offset, origin); }
            public override long Position { get { return input.Position; } set { Seek(value, SeekOrigin.Begin); } }
            public override long Length { get { return input.Length; } }
            public override bool CanRead { get { return true; } } public override bool CanSeek { get { return true; } } public override bool CanWrite { get { return false; } }
            public override void Flush() { } public override void SetLength(long value) { throw new NotSupportedException(); } public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
        }
    }
}

