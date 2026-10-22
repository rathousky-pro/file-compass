using System;
using System.Globalization;
namespace CommanderCZ
{
    internal sealed class ArchiveCompressionProgress
    {
        internal string CurrentFile;
        internal double Percent; internal long Original, Packed; internal bool Complete;
        internal long Processed = -1;
        internal string Encode() { return "@archive-compression|" + Percent.ToString("R", CultureInfo.InvariantCulture) + "|" + Original + "|" + Packed + "|" + (Complete ? "1" : "0") + "|" + Processed + (String.IsNullOrEmpty(CurrentFile) ? "" : "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CurrentFile))); }
        internal static bool TryParse(string text, out ArchiveCompressionProgress value)
        {
            value = null; if (!text.StartsWith("@archive-compression|", StringComparison.Ordinal)) return false; var parts = text.Split('|'); double percent; long original, packed;
            if ((parts.Length != 5 && parts.Length != 6 && parts.Length != 7) || !Double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out percent) || Double.IsNaN(percent) || Double.IsInfinity(percent) || !Int64.TryParse(parts[2], out original) || !Int64.TryParse(parts[3], out packed) || original < 0 || packed < 0) return false;
            value = new ArchiveCompressionProgress { Percent = Math.Max(0, Math.Min(100, percent)), Original = original, Packed = packed, Complete = parts[4] == "1" }; long processed; if (parts.Length >= 6 && Int64.TryParse(parts[5], out processed) && processed >= 0) value.Processed = Math.Min(original, processed); if (parts.Length == 7) { try { value.CurrentFile = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[6])); } catch (FormatException) { value = null; return false; } } return true;
        }
        internal string Summary()
        {
            double processed = Complete ? Original : Processed >= 0 ? Processed : Original * Percent / 100;
            string sizes = CopyUi.Bytes(Packed) + " / " + CopyUi.Bytes((long)processed);
            if (processed <= 0 || (!Complete && (Percent < 1 || Packed == 0))) return L.Text("archive.sizes") + " " + sizes;
            double ratio = Packed / processed * 100;
            return (Complete || Processed >= 0 ? L.Text("archive.ratio") : L.Text("archive.ratioEstimate")) + " " + ratio.ToString("F1") + " % · " + sizes;
        }
    }
}


