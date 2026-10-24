using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FileCompass
{
    public static class L
    {
        static readonly object gate = new object();
        static Dictionary<string, string> baseline, selected, reverse;
        sealed class Translation { internal string Key; internal object[] Arguments; }
        // Match string identity, not text content, so identical user/file names are never translated.
        // Weak keys let obsolete formatted captions be collected during long-running transfers.
        static readonly ConditionalWeakTable<string,Translation> translations=new ConditionalWeakTable<string,Translation>();
        static string Remember(string value,string key,object[] arguments=null) {
            if(value.Length>0)translations.GetValue(value,ignored=>new Translation{Key=key,Arguments=arguments});return value;
        }
        internal static string TranslateExisting(string value) {
            if(value==null)return null;Translation translation;
            if(!translations.TryGetValue(value,out translation))return value;
            return translation.Arguments==null?Text(translation.Key):Format(translation.Key,translation.Arguments.Select(x=>x is string?(object)TranslateExisting((string)x):x).ToArray());
        }
        internal static string CatalogKey(string text){Translation translation;return text!=null&&translations.TryGetValue(text,out translation)?translation.Key:null;}
        internal static bool IsCatalogText(string value){Translation translation;return value!=null&&translations.TryGetValue(value,out translation);}
        internal static void ChangeLanguage(string name) {
            if(!SupportedLanguages.Contains(name))throw new ArgumentException("Unsupported language.");
            Ensure();string folder=AppPaths.Resource("languages");Dictionary<string,string> original;lock(gate)original=baseline;
            var translation=name=="cs-CZ"?original:Read(Path.Combine(folder,name+".lng"));
            lock(gate)Apply(original,translation,name);
        }
        public static string Language { get; private set; }
        internal const string DefaultLanguage = "en";
        internal static readonly string[] SupportedLanguages = { "cs-CZ", "en", "de", "es", "nl", "pl", "fr", "pt" };
        internal static readonly string[] LanguageNames = { "Čeština", "English", "Deutsch", "Español", "Nederlands", "Polski", "Français", "Português" };
        internal static string StartupLanguage(string environment, string saved) {
            return !String.IsNullOrWhiteSpace(environment) ? environment : !String.IsNullOrWhiteSpace(saved) ? saved.Trim() : DefaultLanguage;
        }
        public static void Initialize() { Ensure(); }
        internal static string PreferredLanguage {
            get { string path = Path.Combine(AppPaths.ConfigDirectory, "language.txt"); if (File.Exists(path)) { string name = File.ReadAllText(path).Trim(); if (SupportedLanguages.Contains(name)) return name; } return Language; }
        }
        internal static void SaveSelection(string name, string folder = null) {
            if (!SupportedLanguages.Contains(name)) throw new ArgumentException("Unsupported language.");
            bool explicitFolder = folder != null; folder = folder ?? AppPaths.Resource("languages");
            Read(Path.Combine(folder, name + ".lng"));
            string path = Path.Combine(explicitFolder ? folder : AppPaths.ConfigDirectory, "language.txt"), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, name, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
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
                string name = Environment.GetEnvironmentVariable("FILECOMPASS_LANGUAGE");
                string config = Path.Combine(AppPaths.ConfigDirectory, "language.txt");
                name = StartupLanguage(name, File.Exists(config) ? File.ReadAllText(config) : null);
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
        public static string Text(string key) { Ensure(); lock(gate){key = LanguageKeyAliases.Resolve(key); string value; if (selected.TryGetValue(key, out value) || baseline.TryGetValue(key, out value)) return Remember(value,key); throw new KeyNotFoundException("Missing language key: " + key);} }
        // Canonical persisted states are independent of the selected display language.
        public static string Code(string key) { Ensure(); lock(gate)return baseline[LanguageKeyAliases.Resolve(key)]; }
        public static string Display(string value) { if (value == null) return null; Ensure(); lock(gate){string key; return reverse.TryGetValue(value, out key) ? Text(key) : value;} }
        public static string Format(string key, params object[] args) { return Remember(String.Format(CultureInfo.CurrentCulture, Text(key), args),LanguageKeyAliases.Resolve(key),(object[])args.Clone()); }
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

