using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class SettingsLayoutTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); ConfigurationDialog dialog = null;
            Action<bool, string> check = (valid, text) => { if (!valid) throw new Exception(text); checks.Add(text); };
            try {
                FluentTheme.Ensure();L.ChangeLanguage("cs");
                foreach(string motif in new[]{"Classic","Clean"}){AppearanceTheme.SetMotif(motif);
                foreach (var size in new[] { new Size(1920, 1040), new Size(1280, 760), new Size(1024, 720) }) {
                    dialog = new ConfigurationDialog(null) { LayoutWorkArea = new Rect(-18000, -18000, size.Width, size.Height), Left = -17984, Top = -17984, Width = Math.Min(1080, size.Width - 32), ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                    dialog.Show();
                    for (int index = 0; index < 7; index++) {
                        dialog.Sections.SelectedIndex = index; NavigationTests.Wait(Task.Delay(30)); dialog.FitSettingsPage();
                        var scroll = AdvancedToolsTests.Visual<ScrollViewer>(dialog).Single(x => x.Content is StackPanel && ((StackPanel)x.Content).Children.OfType<TextBlock>().Any(t => t.FontSize == 18));
                        string label = size.Width + ": " + dialog.Sections.Items[index];
                        check(dialog.Width <= 1080 && dialog.Height <= size.Height - 32, label + " stays within the monitor");
                        check(scroll.ViewportHeight>=160,label+": page has usable content area");
                        if(size.Height>=800)check(scroll.ExtentHeight<=scroll.ViewportHeight+1,label+": fits large desktop without scrolling");
                        var save=AdvancedToolsTests.Visual<Button>(dialog).Single(x=>Convert.ToString(x.Content)==L.Text("action.save"));var frame=(FrameworkElement)dialog.Content;var bottom=save.TransformToAncestor(frame).Transform(new Point(0,save.ActualHeight));check(bottom.Y<=frame.ActualHeight,label+": Save stays reachable while page may scroll");check(AdvancedToolsTests.Visual<TextBlock>(dialog).Any(x=>x.FontSize==20&&x.Text==L.Text("menu.configuration")),label+": shared clean settings header");
                        foreach (var grid in AdvancedToolsTests.Visual<Grid>(scroll)) {
                            var cards = grid.Children.OfType<GroupBox>().ToArray();
                            if (cards.Length < 2) continue;
                            check(cards.All(c => Math.Abs(c.TranslatePoint(new Point(), grid).Y - cards[0].TranslatePoint(new Point(), grid).Y) < 1 && Math.Abs(c.ActualHeight - cards[0].ActualHeight) < 1), label + " cards share aligned top and bottom edges");
                        }
                        var paths = AdvancedToolsTests.Visual<DockPanel>(scroll).Where(x => x.Children.OfType<TextBox>().Any() && x.Children.OfType<Button>().Any()).ToArray();
                        foreach (var row in paths) {
                            var input = row.Children.OfType<TextBox>().Single(); var browse = row.Children.OfType<Button>().Single();
                            check(Math.Abs(input.ActualHeight - browse.ActualHeight) < 1 && Math.Abs(input.TranslatePoint(new Point(), row).Y - browse.TranslatePoint(new Point(), row).Y) < 1, label + " input and browse button share height and baseline: "+input.ActualHeight+"/"+browse.ActualHeight+" y="+input.TranslatePoint(new Point(),row).Y+"/"+browse.TranslatePoint(new Point(),row).Y);
                        }
                        if (index == 1) check(AdvancedToolsTests.Visual<RadioButton>(scroll).Count(x => x.GroupName == "ApplicationIcon") == 7 && AdvancedToolsTests.Visual<RadioButton>(scroll).Count(x => x.GroupName == "ConfigurationIcons") == 3, label + " retains every application icon and toolbar theme");
                        if (index == 2) check(AdvancedToolsTests.Visual<TextBox>(scroll).Count() == 10, label + " retains three executable paths and all seven limits");
                        if (index == 6) {
                            var groups = AdvancedToolsTests.Visual<GroupBox>(scroll).Take(2).ToArray();
                            var left = AdvancedToolsTests.Visual<CheckBox>(groups[0]).ToArray(); var right = AdvancedToolsTests.Visual<CheckBox>(groups[1]).ToArray();
                            check(left.Length == 9 && right.Length == 6, label + " retains every file and copy option");
                            check(right.Select((item, i) => Math.Abs(item.TranslatePoint(new Point(0, item.ActualHeight / 2), scroll).Y - left[i].TranslatePoint(new Point(0, left[i].ActualHeight / 2), scroll).Y) < 1).All(x => x), label + " checkbox centers remain aligned when labels wrap");
                        }
                        if (index == 5) check(dialog.Connections.Tabs.Items.Count == 2 && scroll.ViewportHeight - scroll.ExtentHeight < 32, label + " accommodates both management tabs without excess empty space");
                        NextStageTests.Draw(dialog, Path.Combine(root, motif+"-settings-" + size.Width + "-" + index + ".png"));
                    }
                    dialog.Close(); dialog = null;
                }
                }
                // On a very small usable desktop, scrolling is the fallback; controls stay accessible.
                dialog = new ConfigurationDialog(null) { LayoutWorkArea = new Rect(-18000, -18000, 900, 540), Left = -17984, Top = -17984, Width = 868, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                dialog.Show(); dialog.Sections.SelectedIndex = 2; NavigationTests.Wait(Task.Delay(30)); dialog.FitSettingsPage();
                check(dialog.Height <= 508, "Small desktop keeps Save and Cancel within the usable area");
                File.WriteAllLines(Path.Combine(root, "result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), String.Join(Environment.NewLine, checks) + "\nFAIL: " + ex); return 1; }
            finally { if (dialog != null) dialog.Close(); }
        }
    }
}
