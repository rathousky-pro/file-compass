using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
    internal static class CloudQuestionLanguageTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); Window window = null;
            var language = L.Language; var installed = AppPaths.Resource("languages");
            Action<bool, string> check = (valid, text) => { if (!valid) throw new Exception(text); checks.Add(text); };
            try {
                FluentTheme.Ensure();
                var source = new CloudQuestion { State = "client_id_warning", Option = new CloudOption { Name = "config_shared_client_id", Help = "rclone's shared Google Drive client_id is being retired and will stop working during 2026.\nCreate your own to avoid interruption: https://rclone.org/drive/#making-your-own-client-id\nContinue using the shared client_id anyway?", Default = false, Exclusive = true, Examples = new[] { new CloudExample { Value = "true", Help = "Yes" }, new CloudExample { Value = "false", Help = "No" } } } };
                var packs = Path.Combine(root, "languages"); Directory.CreateDirectory(packs); File.Copy(Path.Combine(installed, "cs-CZ.lng"), Path.Combine(packs, "cs-CZ.lng"), true);
                var translated = new Dictionary<string, string> { { "cloud.sharedClientQuestion", "TEST question" }, { "cloud.sharedClientHelp", "TEST explanation for {0}" }, { "cloud.sharedClientYes", "TEST accept" }, { "cloud.sharedClientNo", "TEST own application" }, { "cloud.clientIdQuestion", "TEST client ID" }, { "cloud.clientSecretQuestion", "TEST client secret" } };
                File.WriteAllText(Path.Combine(packs, "test.lng"), new JavaScriptSerializer().Serialize(translated));
                foreach (string selected in new[] { "cs-CZ", "test" }) {
                    L.LoadForTests(packs, selected);
                    var dialog = new CloudQuestionDialog(null, source) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; window = dialog; dialog.Show(); NavigationTests.Wait(Task.Delay(30));
                    var visible = AdvancedToolsTests.Visual<TextBlock>(dialog).Where(x => x.IsVisible).Select(x => x.Text).ToArray();
                    check(visible.Contains(L.Text("cloud.sharedClientQuestion")), selected + ": the question comes from the selected language pack");
                    check(visible.Contains(L.Format("cloud.sharedClientHelp", "2026")), selected + ": explanation is translated and preserves the year from the service");
                    check(!visible.Any(x => x.Contains("Continue using the shared client_id") || x.StartsWith("rclone's shared")), selected + ": raw English is not shown as the main question");
                    var answers = AdvancedToolsTests.Visual<RadioButton>(dialog).ToArray();
                    check(answers.Select(x => ((TextBlock)x.Content).Text).SequenceEqual(new[] { L.Text("cloud.sharedClientYes"), L.Text("cloud.sharedClientNo") }), selected + ": both answers use the chosen language");
                    check(dialog.ReadAnswer() == "false", selected + ": default No is retained");
                    answers.Single(x => (string)x.Tag == "true").IsChecked = true; check(dialog.ReadAnswer() == "true", selected + ": translated Yes retains the original wire value");
                    answers.Single(x => (string)x.Tag == "false").IsChecked = true; check(dialog.ReadAnswer() == "false", selected + ": translated No retains the original wire value");
                    NextStageTests.Draw(dialog, Path.Combine(root, "client-warning-" + selected + ".png")); dialog.Close(); window = null;
                    foreach (string field in new[] { "client_id", "client_secret" }) {
                        var input = new CloudQuestionDialog(null, new CloudQuestion { Option = new CloudOption { Name = field, Help = "Google Application " + field, Required = true } }) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; window = input; input.Show(); NavigationTests.Wait(Task.Delay(20));
                        check(AdvancedToolsTests.Visual<TextBlock>(input).Any(x => x.IsVisible && x.Text == L.Text(field == "client_id" ? "cloud.clientIdQuestion" : "cloud.clientSecretQuestion")), selected + ": next wizard step is translated: " + field);
                        check(input.ReadAnswer() == null, selected + ": required credential input is not silently accepted empty: " + field);
                        if (field == "client_secret") check(AdvancedToolsTests.Visual<PasswordBox>(input).Count(x => x.IsVisible) == 1, selected + ": client secret uses a masked field");
                        input.Close(); window = null;
                    }
                }
                File.WriteAllLines(Path.Combine(root, "result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), String.Join(Environment.NewLine, checks) + "\nFAIL: " + ex); return 1; }
            finally { if (window != null) window.Close(); L.LoadForTests(installed, language); }
        }
    }
}
