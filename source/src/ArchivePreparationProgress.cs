using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
namespace CommanderCZ {
    internal sealed class ArchivePreparationProgress {
        internal string Path = "";
        internal long Items, Bytes;
        internal string Encode() { return "@archive-preparation|" + Items + "|" + Bytes + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Path)); }
        internal static bool TryParse(string text, out ArchivePreparationProgress value) {
            value = null; if (text == null || !text.StartsWith("@archive-preparation|", StringComparison.Ordinal)) return false;
            var parts = text.Split('|'); long items, bytes;
            if (parts.Length != 4 || !Int64.TryParse(parts[1], out items) || !Int64.TryParse(parts[2], out bytes) || items < 0 || bytes < 0) return false;
            try { value = new ArchivePreparationProgress { Items = items, Bytes = bytes, Path = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3])) }; return true; } catch (FormatException) { return false; }
        }
        internal string Summary { get { return L.Text("archive.preparationFiles") + " · " + Items.ToString("N0") + " " + L.Text("archive.preparationItems") + " · " + (Bytes / 1048576.0).ToString("N1") + " MB " + L.Text("archive.preparationReady"); } }
    }
    internal sealed class ArchivePreparationReporter {
        readonly Action<string> report; readonly string root; readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ArchivePreparationProgress state = new ArchivePreparationProgress(); bool sent;
        internal ArchivePreparationReporter(Action<string> report, string root = null) { this.report = report; this.root = root; }
        internal void Visit(string path) { string prefix = root == null ? null : root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar; state.Path = prefix != null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path.Substring(prefix.Length) : path; state.Items++; Send(); }
        internal void Copied(long bytes) { state.Bytes += bytes; Send(); }
        internal void Complete() { if (report != null) report(state.Encode()); }
        void Send() { if (report != null && (!sent || clock.ElapsedMilliseconds >= 150)) { sent = true; clock.Restart(); report(state.Encode()); } }
    }
}




