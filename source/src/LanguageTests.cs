using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace FileCompass
{
    internal static class LanguageTests
    {
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>();
            string installed = Path.Combine(AppPaths.Resource("languages"));
            Action<bool,string> check = delegate(bool valid, string label) { if (!valid) throw new Exception(label); checks.Add(label); };
            try {
                check(L.StartupLanguage(null,null)=="en" && L.StartupLanguage(" "," ")=="en", "English is the default without a saved choice");
                check(L.StartupLanguage(null,"cs")=="cs" && L.StartupLanguage("en","cs")=="en", "Saved language and explicit environment override retain priority");
                var json = new JavaScriptSerializer(); var source = json.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(installed, "cs.lng"), Encoding.UTF8));
                File.Copy(Path.Combine(installed, "cs.lng"), Path.Combine(root, "cs.lng"), true);
                check(L.StartupLanguage(null,"cs-CZ")=="cs"&&L.StartupLanguage(" CS-cz ","en")=="cs","Old saved and environment Czech identifiers normalize to cs");
                L.ChangeLanguage("cs-CZ");check(L.Language=="cs"&&L.SupportedLanguages.Count(x=>x=="cs")==1,"Legacy identifier selects one canonical Czech language");
                L.SaveSelection("cs-CZ",root);check(File.ReadAllText(Path.Combine(root,"language.txt"))=="cs","Saving a legacy identifier writes cs and requires only cs.lng");
                string menu = source.First(x => x.Value == "Soubor").Key, error = source.First(x => x.Value == "Chyba").Key, fallback = source.First(x => x.Value == "Velikost").Key;
                var english = new Dictionary<string,string> { { "_language", "en" }, { menu, "File" }, { error, "Error" }, { "test.format", "Hello {0}" }, { "text.679e0f3cda397a79", ".invalid" }, { "text.982d9e3eb996f559", "invalid-viewer" } };
                File.WriteAllText(Path.Combine(root, "en.lng"), json.Serialize(english), Encoding.UTF8);
                L.LoadForTests(root, "en");
                check(L.Text(menu) == "File", "selected language overrides a Czech UI caption");
                check(L.Text(fallback) == "Velikost", "partial translation falls back to Czech per missing key");
                check(L.Code(error) == "Chyba" && L.Display(L.Code(error)) == "Error", "canonical persisted states remain unchanged while display text is translated");
                check(L.Format("test.format", "Jan") == "Hello Jan", "format arguments remain separate from translated text");
                check((string)CompactTheme.Button("Chyba", "user label", null).Content == "Chyba", "user-provided tab and button names are never translated implicitly");
                check(ArchiveEngine.Recognized("sample.zip") && FileViewers.Kind("sample.txt") == "text", "technical archive and viewer identifiers cannot be changed by language entries");
                foreach (var item in source.Where(x => !x.Key.StartsWith("_"))) check(L.Code(item.Key) == item.Value, "UTF-8 and escaped text round trip: " + item.Key);
                check(source.Keys.All(x => !System.Text.RegularExpressions.Regex.IsMatch(x, @"^text\.[0-9a-f]{16}$")), "catalog contains semantic keys only");
                var legacy = new Dictionary<string,string> { { menu, "File" }, { "text.89c0199fee4eea4d", "Legacy copy" }, { "text.d24832d8225672a7", "Legacy paste" }, { "action.copy", "Copy" }, { "internal.sshExitStatusScript", "UNTRUSTED_SCRIPT" }, { "proxy.basicAuthorizationHeader", "UNTRUSTED_HEADER" }, { "clipboard.panelDataFormat", "UNTRUSTED_FORMAT" } };
                File.WriteAllText(Path.Combine(root, "legacy.lng"), json.Serialize(legacy), Encoding.UTF8); L.LoadForTests(root, "legacy");
                check(L.Text("action.paste") == "Legacy paste", "old hash-based translation loads under semantic key");
                check(L.Text("action.copy") == "Copy", "explicit semantic translation takes precedence over old alias");
                check(L.Text("text.d24832d8225672a7") == "Legacy paste" && L.Code("text.d24832d8225672a7") == source["action.paste"], "legacy lookup API and canonical values remain compatible");
                string command = SshTerminal.BuildCommand(new NetworkProfile { Host = "server", UserName = "tester", Name = "test" }, "ssh.exe", Path.Combine(root, "known_hosts"));
                check(!command.Contains("UNTRUSTED_SCRIPT") && command.Contains("Write-Host (") && command.Contains("$LASTEXITCODE)"), "translation cannot replace executable SSH script");
                check(L.Code("proxy.basicAuthorizationHeader") == "Proxy-Authorization: Basic " && L.Code("clipboard.panelDataFormat") == "FileCompass.Panel", "protocol and clipboard identifiers remain independent of translation");
                var aliases = (Dictionary<string,string>)typeof(LanguageKeyAliases).GetField("names", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                check(aliases.All(x => L.Code(x.Key) == L.Code(x.Value)), "all legacy aliases resolve to unchanged canonical values");
                File.WriteAllText(Path.Combine(root, "bad.lng"), "{broken", Encoding.UTF8);
                bool rejected = false; try { L.LoadForTests(root, "bad"); } catch (ArgumentException) { rejected = true; }
                check(rejected && L.Text(menu) == "File", "invalid JSON is rejected without corrupting the active language");
                File.WriteAllLines(Path.Combine(root, "language-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "language-result.txt"), "FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { L.LoadForTests(installed, "cs"); }
        }
    }
}
