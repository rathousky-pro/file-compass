using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class ErgonomicsTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); Window current = null; MainWindow main = null;
            var originalTheme = ToolbarThemeSettings.Current; var originalIcon = ApplicationBrand.Current;
            Action<bool, string> check = (valid, text) => { if (!valid) throw new Exception(text); checks.Add(text); };
            try {
                FluentTheme.Ensure();
                foreach (var size in new[] { new Size(1280, 760), new Size(1024, 720), new Size(1024, 620) }) foreach (NetworkProtocol type in Enum.GetValues(typeof(NetworkProtocol))) {
                    var profile = new NetworkProfile { Protocol = type, Name = "Testovací profil", Host = "example.invalid", UserName = "tester", Path = type == NetworkProtocol.SMB ? "data" : "/data", DriveLetter = "Z:", ProxyMethod = 1, ProxyHost = "proxy.invalid", ProxyPort = 1080 };
                    var dialog = new NetworkProfileDialog(null, profile) { LayoutWorkArea = new Rect(-18000, -18000, size.Width, size.Height), Width = Math.Min(960, size.Width - 32), Left = -17984, Top = -17984, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; current = dialog; dialog.Show();
                    for (int tab = 0; tab < 2; tab++) {
                        dialog.Tabs.SelectedIndex = tab; NavigationTests.Wait(Task.Delay(30)); dialog.FitProfilePage();
                        var scroll = (ScrollViewer)((TabItem)dialog.Tabs.SelectedItem).Content;
                        string label = type + " " + tab + " @" + size.Width;
                        check(scroll.ExtentHeight <= scroll.ViewportHeight + 1, label + " fits without scrolling (" + scroll.ExtentHeight + "/" + scroll.ViewportHeight + ")");
                        check(dialog.Height <= size.Height - 32 && dialog.Width <= size.Width - 32, label + " fits the work area");
                        NextStageTests.Draw(dialog, Path.Combine(root, "network-" + size.Width + "x" + size.Height + "-" + type + "-" + tab + ".png"));
                    }
                    var read = dialog.ReadProfile(); check(read.Name == profile.Name && read.Host == profile.Host && read.UserName == profile.UserName && read.TimeoutSeconds == profile.TimeoutSeconds, type + " preserves editable connection values after rearranging");
                    dialog.SelectProtocol(type == NetworkProtocol.FTP ? NetworkProtocol.SFTP : NetworkProtocol.FTP); dialog.SelectProtocol(type); NavigationTests.Wait(Task.Delay(20));
                    check(dialog.Tabs.Items.Count == 2, type + " rebuilds protocol pages without parenting errors");
                    dialog.Close(); current = null;
                }
                var input = new TextBox { Text = "1" }; var spinner = new NumberSpinner(input, 1, 12);
                check(spinner.Children.OfType<Border>().Any(x=>x.BorderThickness.Left==1&&x.BorderBrush!=null),"numeric arrows have a complete shared outline");
                spinner.Change(-1); check(input.Text == "1", "Numeric lower boundary is enforced"); input.Text = "12"; spinner.Change(1); check(input.Text == "12", "Numeric upper boundary is enforced");
                input.Text = "invalid"; spinner.Change(1); check(input.Text == "1", "Arrow recovers an invalid manual value");
                var font = new FontChoiceControl("Test", "Segoe UI", 12, 24); var fontSpinner = AdvancedToolsTests.Visual<NumberSpinner>(font).FirstOrDefault();
                fontSpinner = ((DockPanel)font.Children[1]).Children.OfType<NumberSpinner>().Single(); fontSpinner.Change(1);
                double points; check(Double.TryParse(font.Points.Text, out points) && points == 9.5, "Font arrows support half-point changes");
                foreach (var changing in new[] { false, true }) {
                    var question = new CloudQuestion { Option = new CloudOption { Name = "config_change_team_drive", Help = changing ? "Change current Shared Drive (Team Drive) ID test?" : "Configure this as a Shared Drive (Team Drive)?", Default = false, Exclusive = true, Examples = new[] { new CloudExample { Value = "true", Help = "Yes" }, new CloudExample { Value = "false", Help = "No" } } } };
                    var dialog = new CloudQuestionDialog(null, question) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; current = dialog; dialog.Show(); NavigationTests.Wait(Task.Delay(30));
                    check(dialog.ReadAnswer() == "false", "Cloud boolean default is preserved: " + changing);
                    var radios = AdvancedToolsTests.Visual<RadioButton>(dialog).ToArray();
                    check(radios.Length == 2 && radios.All(r => ((TextBlock)r.Content).Text.Length > 20), "Cloud choices explain the consequences: " + changing);
                    radios.First(r => (string)r.Tag == "true").IsChecked = true; check(dialog.ReadAnswer() == "true", "Translated cloud answers preserve the original service values: " + changing);
                    NextStageTests.Draw(dialog, Path.Combine(root, changing ? "cloud-change-shared.png" : "cloud-personal-or-shared.png")); dialog.Close(); current = null;
                }
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; main.Show(); NavigationTests.WaitPanels(main);
                var choiceDialog = new CloudQuestionDialog(null, new CloudQuestion { Option = new CloudOption { Name = "config_driveid", Help = "Select a drive", Exclusive = true, Default = "second", Examples = new[] { new CloudExample { Value = "first", Help = "First drive" }, new CloudExample { Value = "second", Help = "Second drive" } } } });
                check(choiceDialog.ReadAnswer() == "second", "Cloud storage lists preserve the service default instead of selecting the first item"); choiceDialog.Close();
                var settings = new ConfigurationDialog(main, L.Text("settings.iconsSection")) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; current = settings; settings.Show(); NavigationTests.Wait(Task.Delay(30));
                byte[] before = File.Exists(ToolbarThemeSettings.ConfigPath) ? File.ReadAllBytes(ToolbarThemeSettings.ConfigPath) : null;
                var themes = AdvancedToolsTests.Visual<RadioButton>(settings).Where(x => x.GroupName == "ConfigurationIcons").ToArray();
                themes[((int)originalTheme + 1) % 3].IsChecked = true;
                check(ToolbarThemeSettings.Current != originalTheme, "Changing the toolbar theme applies a live preview");
                var icons = AdvancedToolsTests.Visual<RadioButton>(settings).Where(x => x.GroupName == "ApplicationIcon").ToArray(); icons[((int)originalIcon + 1) % icons.Length].IsChecked = true;
                check(ApplicationBrand.Current != originalIcon && main.Icon == ApplicationBrand.Icon, "Application icon preview updates existing windows");
                check(before == null ? !File.Exists(ToolbarThemeSettings.ConfigPath) : before.SequenceEqual(File.ReadAllBytes(ToolbarThemeSettings.ConfigPath)), "Preview does not write saved appearance settings");
                NextStageTests.Draw(main, Path.Combine(root, "toolbar-live-preview.png")); settings.Close(); current = null;
                check(ToolbarThemeSettings.Current == originalTheme && ApplicationBrand.Current == originalIcon, "Closing without Save restores both icon choices");
                string file = Path.Combine(root, "rename.txt"), other = Path.Combine(root, "second.txt"); File.WriteAllText(file, "rename preview"); File.WriteAllText(other, "second rename preview");
                var rename = new BatchRenameWindow(main, new[] { file, other }, delegate { }) { Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false }; current = rename; rename.Show(); NavigationTests.Wait(rename.PreviewTask);
                var numbers = AdvancedToolsTests.Visual<NumberSpinner>(rename).ToArray(); check(numbers.Length == 3, "Batch rename exposes arrows for start, step and digits");
                AdvancedToolsTests.Visual<TextBox>(rename).Single(x => x.Text == "[N]").Text = "[C]";
                numbers[0].Change(1); NavigationTests.Wait(rename.PreviewTask);
                var renamed = AdvancedToolsTests.Visual<ListView>(rename).Single();
                check(renamed.Items.Cast<RenameRow>().Select(x => x.NewName).SequenceEqual(new[] { "2.txt", "3.txt" }), "Start arrows update the actual filename preview");
                numbers[1].Change(1); NavigationTests.Wait(rename.PreviewTask); check(renamed.Items.Cast<RenameRow>().Last().NewName == "4.txt", "Step arrows update successive names");
                numbers[2].Change(1); NavigationTests.Wait(rename.PreviewTask); check(renamed.Items.Cast<RenameRow>().Select(x => x.NewName).SequenceEqual(new[] { "02.txt", "04.txt" }), "Digit arrows update number padding");
                NextStageTests.Draw(rename, Path.Combine(root, "batch-rename-spinners.png"));
                File.WriteAllLines(Path.Combine(root, "result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), String.Join(Environment.NewLine, checks) + "\nFAIL: " + ex); return 1; }
            finally { if (current != null) current.Close(); if (main != null) main.Close(); ToolbarThemeSettings.Current = originalTheme; ApplicationBrand.Apply(originalIcon); }
        }
    }
}
