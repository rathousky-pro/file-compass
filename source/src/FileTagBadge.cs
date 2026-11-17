using System;
using System.Windows.Media;

namespace FileCompass
{
    public sealed class FileTagBadge
    {
        static readonly Brush Orange = CompactTheme.ColorBrush("#9F560E");
        static readonly Brush Slate = CompactTheme.ColorBrush("#57677E");
        static readonly Brush Green = CompactTheme.ColorBrush("#23744D");
        static readonly Brush Red = CompactTheme.ColorBrush("#A83252");
        static readonly Brush[] Custom = { CompactTheme.ColorBrush("#326BB1"), CompactTheme.ColorBrush("#754CAB"),
            CompactTheme.ColorBrush("#287581"), CompactTheme.ColorBrush("#86602F") };

        internal static bool IsColorCode(string value)
        {
            return value != null && value.Length == 7 && value[0] == '#' &&
                UInt32.TryParse(value.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        internal FileTagBadge(string name, string color = null)
        {
            Name = FileTagStore.DisplayName(name);
            if (IsColorCode(color)) { Color = CompactTheme.ColorBrush(color); return; }
            switch (name.ToUpperInvariant())
            {
                case "OLD": Color = Slate; return;
                case "VYŘÍDIT": case "DEAL": case "TODO": case "ANGEBOT": case "OFERTA": case "OFFRE": case "AFFARE":
                    Color = Orange; return;
                case "HOTOVO": case "DONE": case "FERTIG": case "HECHO": case "TERMINÉ": case "GOTOWE": case "KLAAR":
                case "FATTO": case "CONCLUÍDO": Color = Green; return;
                case "DŮLEŽITÉ": case "IMPORTANT": case "WICHTIG": case "IMPORTANTE": case "WAŻNE": case "BELANGRIJK":
                    Color = Red; return;
            }
            uint hash = 0;
            unchecked { foreach (char value in name.ToUpperInvariant()) hash = hash * 31 + value; }
            Color = Custom[hash % (uint)Custom.Length];
        }

        public string Name { get; }
        public Brush Color { get; }
    }
}
