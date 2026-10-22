using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ
{
    internal static class NetworkBrowserTests
    {
        internal static int Live(string root)
        {
            Directory.CreateDirectory(root);
            try { var task = NetworkBrowser.Read(NetworkBrowser.Root); NavigationTests.Wait(task); File.WriteAllText(Path.Combine(root, "network-browser-live.txt"), "Devices=" + task.Result.Count + "; UNC=" + task.Result.All(x => NetworkBrowser.IsServer(x.FullPath))); return 0; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "network-browser-live.txt"), ex.ToString()); return 1; }
        }
        internal static int Run(string root)
        {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow window = null;
            Action<bool, string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                NetworkBrowser.TestProvider = delegate(string path) { return new List<Entry> { new Entry { Name = path == NetworkBrowser.Root ? "Test server" : "Data", FullPath = path == NetworkBrowser.Root ? "\\\\test-server" : "\\\\test-server\\Data", IsDirectory = true, Icon = DesktopIcons.FileIcon("", true) } }; };
                window = new MainWindow(root); window.Show(); NavigationTests.WaitPanels(window);
                var panel = FluentUiTests.Children<FilePanel>(window).First();
                panel.Navigate(NetworkBrowser.Root, true); NavigationTests.Wait(panel.NavigationTask);
                check(panel.IsNetworkBrowser && panel.CurrentPath == NetworkBrowser.Root && panel.List.Items.Count == 1, "network button destination displays devices in the file panel");
                panel.List.SelectedIndex = 0; panel.OpenSelected(); NavigationTests.Wait(panel.NavigationTask);
                check(panel.CurrentPath == "\\\\test-server" && panel.List.Items.Count == 2, "opening a device enumerates shares and a parent entry");
                var share = panel.List.Items.Cast<Entry>().Single(x => !x.IsParent); panel.List.SelectedItem = share;
                check(NetworkBrowser.CanMap(share) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = "\\\\test-server" }) && !NetworkBrowser.CanMap(new Entry { IsDirectory = true, FullPath = root }), "mapping accepts UNC shares and rejects devices and local directories");
                panel.List.ContextMenu.IsOpen = true; window.UpdateLayout();
                check(panel.List.ContextMenu.Items.OfType<MenuItem>().Any(x => (string)x.Header == "Mapovat jako síťový disk…" && x.Visibility == Visibility.Visible), "UNC folder context menu offers network drive mapping"); panel.List.ContextMenu.IsOpen = false;
                panel.Up(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == NetworkBrowser.Root, "parent navigation returns from device to network");
                panel.Back(); NavigationTests.Wait(panel.NavigationTask); check(panel.CurrentPath == "\\\\test-server", "network browsing participates in back history");
                panel.Navigate(root, true); NavigationTests.Wait(panel.NavigationTask); check(!panel.IsNetworkBrowser && panel.CurrentPath == root, "local navigation leaves network browsing cleanly");
                NetworkBrowser.TestProvider = delegate(string location) { System.Threading.Thread.Sleep(100); return new List<Entry>(); };
                panel.Navigate(NetworkBrowser.Root, true); var stale = panel.NavigationTask;
                panel.Navigate(root, true); NavigationTests.Wait(panel.NavigationTask); NavigationTests.Wait(stale);
                check(panel.CurrentPath == root && !panel.LocalLoading && panel.List.IsEnabled, "late network discovery cannot replace a newer local navigation");
                string literalFolder = "C:\\folder's $data"; var info = MainWindow.PowerShellStartInfo(literalFolder);
                string decoded = Encoding.Unicode.GetString(Convert.FromBase64String(info.Arguments.Split(' ').Last()));
                check(info.WorkingDirectory == literalFolder && decoded == "Set-Location -LiteralPath 'C:\\folder''s $data'", "PowerShell preserves literal folder names and never interpolates path source");
                check(FluentUiTests.Children<MenuItem>(window).Any(x => (string)x.Header == "Otevřít PowerShell v této složce"), "PowerShell command is present in the main menu");
                check(FluentUiTests.Children<Button>(window).Any(x => (x.ToolTip as string) == "Komprimovat (Alt+F5)") && FluentUiTests.Children<Button>(window).Any(x => (x.ToolTip as string) == "Extrahovat (Alt+F9)"), "compression and extraction are visible toolbar commands");
                NetworkTests.Render(window, Path.Combine(root, "main.png"));
                File.WriteAllLines(Path.Combine(root, "network-browser-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "network-browser-result.txt"), "FAIL after " + checks.Count + " checks\n" + ex); return 1; }
            finally { NetworkBrowser.TestProvider = null; if (window != null) window.Close(); }
        }
    }
}

