using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace FileCompass {
    internal static class UsabilityTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null; Window previewWindow = null; QuickPreviewPane preview = null; ConfigurationDialog configuration = null;
            string previousCloudRoot = CloudStore.TestRoot;
            Action<bool, string> check = (valid, name) => { if (!valid) throw new Exception(name); checks.Add(name); File.AppendAllText(Path.Combine(root, "progress.txt"), name + Environment.NewLine); };
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Width = 1440, Height = 850, Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main); main.CheckUsabilityToolbar(check); NextStageTests.Draw(main, Path.Combine(root, "toolbar.png"));
                configuration = new ConfigurationDialog(main) { Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual }; configuration.Show();
                for (int index = 0; index < configuration.Sections.Items.Count; index++) {
                    configuration.Sections.SelectedIndex = index; configuration.UpdateLayout(); NavigationTests.Wait(Task.Delay(30));
                    var scroller = AdvancedToolsTests.Visual<ScrollViewer>(configuration).Single(x => x.Content is StackPanel && ((StackPanel)x.Content).Children.OfType<TextBlock>().Any(t => t.FontSize == 18));
                    check(scroller.ExtentHeight <= scroller.ViewportHeight + 1, "Settings fit without scrolling: " + configuration.Sections.Items[index] + " (" + scroller.ExtentHeight + "/" + scroller.ViewportHeight + ", window=" + configuration.Width + "x" + configuration.Height + ")");
                    NextStageTests.Draw(configuration, Path.Combine(root, "settings-" + index + ".png"));
                }
                configuration.Width = Math.Min(configuration.Width, 1180); configuration.Height = Math.Min(configuration.Height, 780);
                for (int index = 0; index < configuration.Sections.Items.Count; index++) {
                    configuration.Sections.SelectedIndex = index; configuration.UpdateLayout(); NavigationTests.Wait(Task.Delay(20));
                    var scroller = AdvancedToolsTests.Visual<ScrollViewer>(configuration).Single(x => x.Content is StackPanel && ((StackPanel)x.Content).Children.OfType<TextBlock>().Any(t => t.FontSize == 18));
                    check(scroller.ExtentHeight <= scroller.ViewportHeight + 1, "Settings also fit in a smaller window: " + configuration.Sections.Items[index] + " (" + scroller.ExtentHeight + "/" + scroller.ViewportHeight + ")");
                }
                configuration.Close(); configuration = null;
                string file = Path.Combine(root, "Obsah.txt"); File.WriteAllText(file, "Archive contents preview");
                preview = new QuickPreviewPane(delegate { }); previewWindow = new Window { Content = preview, Width = 1000, Height = 700, Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false }; previewWindow.Show();
                foreach (string extension in new[] { "zip", "7z", "rar" }) {
                    string archive = Path.Combine(root, "preview." + extension); ArchiveEngine.Create(archive, new[] { file }, ArchiveSettings.Load(), CancellationToken.None);
                    preview.ShowFile(archive); NavigationTests.Wait(preview.LoadingTask); previewWindow.UpdateLayout();
                    check(AdvancedToolsTests.Visual<ListView>(preview).Any(x => x.IsVisible && x.Items.Count == 1 && Convert.ToString(x.Items[0].GetType().GetProperty("Path").GetValue(x.Items[0], null)) == "Obsah.txt"), extension + " shows the archive listing instead of hexadecimal data");
                    NextStageTests.Draw(previewWindow, Path.Combine(root, "preview-" + extension + ".png"));
                    preview.SetViewMode(PreviewMode.Hex); NavigationTests.Wait(preview.LoadingTask);
                    check(!AdvancedToolsTests.Visual<ListView>(preview).Any(x => x.IsVisible) && AdvancedToolsTests.Visual<TextBox>(preview).Any(x => x.IsVisible && x.Text.Contains("00000000")), extension + " still offers explicitly selected HEX mode");
                    preview.SetViewMode(PreviewMode.Auto); NavigationTests.Wait(preview.LoadingTask);
                }
                CloudStore.TestRoot = Path.Combine(root, "cloud"); CloudStore.SecureDirectory(CloudStore.Root);
                File.WriteAllBytes(CloudStore.KeyFile, new byte[] { 1, 2, 3, 4 });
                var account = new CloudAccount { Name = "Imported account", Provider = "local", Ready = true, BridgePassword = "foreign-dpapi", Port = 12345, Fingerprint = "old" };
                CloudStore.SecureDirectory(CloudStore.AccountPath(account)); File.WriteAllText(CloudStore.Config(account), "Original encrypted configuration from another PC");
                var other = new CloudAccount { Name = "Unchanged account", Provider = "drive", Ready = false }; var accounts = new CloudAccounts(); accounts.Accounts.Add(account); accounts.Accounts.Add(other); CloudStore.Save(accounts);
                string original = File.ReadAllText(CloudStore.Config(account)); bool cancelled = false;
                try { CloudStore.Reauthorize(account, q => Task.FromResult(""), CancellationToken.None, null, (stage, token) => { throw new OperationCanceledException(); }).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                check(cancelled && File.ReadAllText(CloudStore.Config(account)) == original && account.BridgePassword == "foreign-dpapi", "Cancelling reauthorization preserves the imported account and its credentials");
                string remote = Directory.CreateDirectory(Path.Combine(root, "cloud-files")).FullName; File.WriteAllText(Path.Combine(remote, "data.txt"), "account recovered");
                CloudStore.Reauthorize(account, q => Task.FromResult(""), CancellationToken.None, null, async (stage, token) => { await CloudStore.Run(stage, new[] { "config", "create", "remote", "alias", "remote", remote, "--non-interactive" }, token); }).GetAwaiter().GetResult();
                check(CloudStore.Run(account, new[] { "lsjson", "remote:" }, CancellationToken.None).GetAwaiter().GetResult().Contains("data.txt"), "Reauthorized account works even when the imported global DPAPI key is unreadable");
                check(File.ReadAllBytes(CloudStore.KeyFile).SequenceEqual(new byte[] { 1, 2, 3, 4 }) && CloudStore.Load().Accounts.Any(x => x.Id == other.Id && !x.Ready), "Reauthorization does not replace other accounts or their legacy key");
                check(Directory.GetDirectories(CloudStore.Root, account.Id + ".before-signin-*").Any(x => File.ReadAllText(Path.Combine(x, "rclone.conf")) == original), "Original account directory is retained as an encrypted recovery backup");
                check(CloudStore.Find(account.Id).Ready && NetworkSettingsStore.Unprotect(account.BridgePassword).Length > 0 && account.Fingerprint.StartsWith("SHA256:"), "Reauthorization persists the same account identity with valid local bridge credentials");
                CloudStore.RemoveAccountFiles(account); check(!Directory.Exists(CloudStore.AccountPath(account)) && Directory.GetDirectories(CloudStore.Root, account.Id + ".before-signin-*").Length == 0 && File.Exists(CloudStore.KeyFile), "Removing an account also removes its own recovery backups and preserves the shared legacy key");
                File.WriteAllLines(Path.Combine(root, "result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), String.Join(Environment.NewLine, checks) + "\nFAIL: " + ex); return 1; }
            finally { if (configuration != null) configuration.Close(); if (preview != null) preview.DisposePreview(); if (previewWindow != null) previewWindow.Close(); if (main != null) main.Close(); CloudStore.TestRoot = previousCloudRoot; }
        }
    }
    public partial class MainWindow {
        internal void CheckUsabilityToolbar(Action<bool, string> check) {
            SetPanelLayout("SideBySide"); SetActive(left); ToggleQuickPreview();
            check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarPreviewButton) == L.Text("toolbar.enabled"), "Ctrl+Q highlights the preview toolbar button");
            check(toolbarPreviewButton.Background != System.Windows.Media.Brushes.White && !((StackPanel)toolbarPreviewButton.Content).Children.OfType<Border>().Any(x => x.Tag as string == "toggle-state"), "Preview uses a colored pressed state without a checkbox");
            var tools = (Panel)toolbarAllFilesButton.Parent;
            check(tools.Children.IndexOf(toolbarThumbnailsButton) == tools.Children.IndexOf(toolbarAllFilesButton) + 1 && tools.Children.IndexOf(toolbarPreviewButton) == tools.Children.IndexOf(toolbarThumbnailsButton) + 1, "Toolbar order is hidden files, thumbnails, preview");
            CloseQuickPreview(); check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarPreviewButton) == L.Text("toolbar.disabled"), "Closing preview resets its toolbar state");
            var menu = AdvancedToolsTests.Visual<Menu>(this).First(x => x.Items.OfType<MenuItem>().Any(y => Convert.ToString(y.Header) == L.Text("menu.commands")));
            var root = menu.Items.OfType<MenuItem>().Single(x => Convert.ToString(x.Header) == L.Text("menu.commands"));
            check(root.Items.OfType<MenuItem>().Count() <= 10 && root.Items.OfType<MenuItem>().Any(x => x.Items.Count >= 3), "Commands menu is divided into short logical submenus");
            SetPanelLayout("Single"); left.SetDetailExpanded(true); UpdateLayout();
            check(System.Windows.Automation.AutomationProperties.GetItemStatus(toolbarPreviewButton) == L.Text("toolbar.enabled"), "Bottom preview also highlights the same toolbar button");
        }
    }
}
