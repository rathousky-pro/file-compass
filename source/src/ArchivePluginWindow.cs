using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal sealed class ArchivePluginWindow : Window
    {
        static readonly System.Collections.Generic.Dictionary<string, ArchivePluginWindow> OpenWindows = new System.Collections.Generic.Dictionary<string, ArchivePluginWindow>();
        readonly ArchivePluginPackage package;
        readonly Action<string> use;
        readonly string configured;
        readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
        readonly ProgressBar progress = new ProgressBar { Height = 3, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 4, 0, 16) };
        readonly Button start, close;
        CancellationTokenSource cancellation;
        bool running, installing;
        string installer;

        internal static void Open(Window owner, ArchivePluginPackage package, Action<string> use, string existingPath = null)
        {
            ArchivePluginWindow window;
            if (OpenWindows.TryGetValue(package.Id, out window)) { window.WindowState = WindowState.Normal; window.Activate(); return; }
            while (owner != null && owner.Owner != null) owner = owner.Owner;
            window = new ArchivePluginWindow(owner, package, use, existingPath); OpenWindows.Add(package.Id, window); window.Show();
        }
        internal ArchivePluginWindow(Window owner, ArchivePluginPackage selected, Action<string> usePath, string existingPath = null)
        {
            package = selected; use = usePath; configured = existingPath;
            CopyUi.Style(this, owner, L.Text("archivePlugin.title") + " — " + package.Name, 570);
            var body = new StackPanel { Margin = new Thickness(16) }; Content = body;
            body.Children.Add(new TextBlock { Text = package.Name, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
            body.Children.Add(new TextBlock { Text = L.Text(package.Installer ? "archivePlugin.winRarLicense" : package == ArchivePluginInstaller.Arj ? "archivePlugin.arjLicense" : "archivePlugin.lhaLicense"), TextWrapping = TextWrapping.Wrap });
            body.Children.Add(new TextBlock { Text = L.Text("archivePlugin.downloadHelp"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4), Foreground = FluentTheme.Muted });
            var link = CopyUi.Button(L.Text("archivePlugin.publisherAndLicense"), delegate { try { Process.Start(new ProcessStartInfo(package.LicenseUrl) { UseShellExecute = true }); } catch (Exception ex) { status.Text = ex.Message; } }); body.Children.Add(link);
            body.Children.Add(status); body.Children.Add(progress);
            var actions = FluentTheme.Actions(); body.Children.Add(actions);
            start = CopyUi.Button(L.Text("archivePlugin.download"), Start); start.IsDefault = true; actions.Children.Add(start);
            close = CopyUi.Button(L.Text("action.close"), CloseOrCancel); actions.Children.Add(close);
            status.Text = L.Text("archivePlugin.onlyMissing");
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (running) { e.Cancel = true; CloseOrCancel(); } };
            Closed += delegate { OpenWindows.Remove(package.Id); if (cancellation != null) cancellation.Dispose(); };
        }
        string Existing()
        {
            string path = Environment.ExpandEnvironmentVariables((configured ?? "").Trim().Trim('"'));
            if (Path.IsPathRooted(path) && File.Exists(path)) { using (ArchivePluginInstaller.AcquireManagedExecutable(path)) return path; }
            return package.Installer ? ArchiveSettings.FindWinRar() : ArchiveSettings.FindLegacyArchiver(package == ArchivePluginInstaller.Arj);
        }
        void SelectPath(string path)
        {
            var settings = ArchiveSettings.Load();
            if (package.Installer) settings.WinRarPath = path;
            else if (package == ArchivePluginInstaller.Arj) settings.ArjPath = path;
            else settings.LhaPath = path;
            ToolTrust.Approve(path);
            settings.Save(); use(path);
        }
        async void Start()
        {
            if (running) return;
            running = true; start.IsEnabled = false; close.Content = L.Text("action.cancelOperation"); cancellation = new CancellationTokenSource();
            try {
                if (installer != null) {
                    installing = true; close.IsEnabled = false;
                    status.Text = L.Text("archivePlugin.installingWinRar");
                    using (ArchivePluginInstaller.VerifyHash(installer, package.Hash)) {
                        await Task.Run(delegate { ArchivePluginInstaller.VerifyInstaller(installer); });
                        using (var process = Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true })) if (process != null) await Task.Run(delegate { process.WaitForExit(); });
                    }
                    string found = Existing();
                    if (found != null) { SelectPath(found); start.Visibility = Visibility.Collapsed; status.Text = L.Text("archivePlugin.existingUsed"); }
                    else status.Text = L.Text("archivePlugin.finishAndDetect");
                } else {
                    string found = await Task.Run(new Func<string>(Existing));
                    if (found != null) { SelectPath(found); status.Text = L.Text("archivePlugin.existingUsed"); start.Visibility = Visibility.Collapsed; return; }
                    string path = await Task.Run(delegate { return ArchivePluginInstaller.Install(package, cancellation.Token, Report); });
                    if (package.Installer) { installer = path; start.Content = L.Text("archivePlugin.runInstaller"); status.Text = L.Text("archivePlugin.installerReady"); }
                    else { SelectPath(path); start.Visibility = Visibility.Collapsed; status.Text = L.Text("archivePlugin.pathSelected"); }
                    progress.IsIndeterminate = false; progress.Value = 100;
                }
            } catch (OperationCanceledException) { status.Text = L.Text("archivePlugin.cancelled"); }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { running = false; installing = false; progress.IsIndeterminate = false; start.IsEnabled = true; close.IsEnabled = true; close.Content = L.Text("action.close"); cancellation.Dispose(); cancellation = null; }
        }
        void CloseOrCancel()
        {
            if (!running) { Close(); return; }
            if (installing) return;
            cancellation.Cancel(); close.IsEnabled = false; status.Text = L.Text("archivePlugin.cancelling");
        }
        void Report(SshfsSetupProgress update)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(delegate { Report(update); })); return; }
            if (!running) return;
            status.Text = update.Text; progress.IsIndeterminate = update.Percent < 0;
            if (update.Percent >= 0) progress.Value = update.Percent;
        }
    }
}

