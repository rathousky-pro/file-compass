using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Globalization;

namespace CommanderCZ
{
    public static class L
    {
        static readonly object gate = new object();
        static Dictionary<string, string> baseline, selected, reverse;
        public static string Language { get; private set; }
        public static void Initialize() { Ensure(); }
        static Dictionary<string, string> Read(string path)
        {
            var info = new FileInfo(path); if (info.Length > 4 * 1024 * 1024) throw new IOException("Language file exceeds 4 MiB: " + path);
            var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            var result = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, new UTF8Encoding(false, true)));
            if (result == null || result.Any(x => x.Value == null)) throw new IOException("Invalid language file: " + path);
            return LanguageKeyAliases.Normalize(result);
        }
        static void Ensure()
        {
            lock (gate) {
                if (baseline != null) return;
                string folder = Path.Combine(AppPaths.Resource("languages"));
                var original = Read(Path.Combine(folder, "cs-CZ.lng"));
                string name = Environment.GetEnvironmentVariable("COMMANDERCZ_LANGUAGE");
                string config = Path.Combine(folder, "language.txt");
                if (String.IsNullOrWhiteSpace(name) && File.Exists(config)) name = File.ReadAllText(config).Trim();
                if (String.IsNullOrWhiteSpace(name)) name = "cs-CZ";
                if (name.Length > 40 || name.Any(c => !(Char.IsLetterOrDigit(c) || c == '-'))) throw new IOException("Invalid language identifier.");
                var translation = name == "cs-CZ" ? original : Read(Path.Combine(folder, name + ".lng"));
                Apply(original, translation, name);
            }
        }
        static void Apply(Dictionary<string,string> original, Dictionary<string,string> translation, string name)
        {
            selected = translation; reverse = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var item in original) if (!item.Key.StartsWith("_")) reverse[item.Value] = item.Key;
            Language = name; baseline = original;
        }
        internal static void LoadForTests(string folder, string name)
        { lock (gate) { var original = Read(Path.Combine(folder, "cs-CZ.lng")); Apply(original, name == "cs-CZ" ? original : Read(Path.Combine(folder, name + ".lng")), name); } }
        public static string Text(string key) { Ensure(); key = LanguageKeyAliases.Resolve(key); string value; if (selected.TryGetValue(key, out value) || baseline.TryGetValue(key, out value)) return value; throw new KeyNotFoundException("Missing language key: " + key); }
        // Canonical persisted states are independent of the selected display language.
        public static string Code(string key) { Ensure(); return baseline[LanguageKeyAliases.Resolve(key)]; }
        public static string Display(string value) { if (value == null) return null; Ensure(); string key; return reverse.TryGetValue(value, out key) ? Text(key) : value; }
        public static string Format(string key, params object[] args) { return String.Format(CultureInfo.CurrentCulture, Text(key), args); }
        public static System.Windows.Data.Binding Binding(string property)
        {
            var result = new System.Windows.Data.Binding(property);
            if (new[] { "State", "Status", "Policy", "Action", "Difference" }.Contains(property)) result.Converter = new LanguageValueConverter();
            return result;
        }
    }
    public sealed class LanguageValueConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture) { return L.Display(value as string) ?? value; }
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) { throw new NotSupportedException(); }
    }
}

