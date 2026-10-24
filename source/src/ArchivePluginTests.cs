using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FileCompass
{
    internal static class ArchivePluginTests
    {
        internal static int Run(string root)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Directory.CreateDirectory(root); var checks = new List<string>();
            Action<bool,string> check = delegate(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); };
            try {
                string installed = Path.Combine(root, "installed");
                foreach (var package in ArchivePluginInstaller.Packages) {
                    var reports = new List<SshfsSetupProgress>();
                    bool wasExisting = ArchivePluginInstaller.Find(package, installed) != null;
                    string path = ArchivePluginInstaller.Install(package, CancellationToken.None, reports.Add, installed);
                    check(File.Exists(path) && (wasExisting || reports.Count > 1), package.Name + (wasExisting ? " verified cached package" : " actual HTTPS download, pin validation and publication"));
                    check(ArchivePluginInstaller.Find(package, installed) == path, package.Name + " managed version can be detected");
                    bool noDownload = true;
                    string cached = ArchivePluginInstaller.Install(package, CancellationToken.None, reports.Add, installed, delegate(string url, Stream output, CancellationToken token, Action<long,long> progress) { noDownload = false; throw new Exception("cache not used"); });
                    check(noDownload && cached == path, package.Name + " verified installed package is reused without network");
                    if (package.Installer) { ArchivePluginInstaller.VerifyInstaller(path); check(true, "WinRAR original installer passes Authenticode; never executed by tests"); }
                    else {
                        using (ArchivePluginInstaller.AcquireManagedExecutable(path, installed)) {
                            bool locked = false; try { using (File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } } catch (IOException) { locked = true; }
                            check(locked, package.Name + " verified executable remains locked during use");
                        }
                        byte[] bytes = File.ReadAllBytes(path); File.AppendAllText(path, "tamper");
                        bool rejected = false; try { ArchivePluginInstaller.Find(package, installed); } catch (IOException) { rejected = true; }
                        check(rejected, package.Name + " modified executable is rejected"); File.WriteAllBytes(path, bytes);
                        var settings = new ArchiveSettings { ArjPath = ArchivePluginInstaller.Target(ArchivePluginInstaller.Arj, installed), LhaPath = ArchivePluginInstaller.Target(ArchivePluginInstaller.Lha, installed) }; settings.Save();
                        string input = Path.Combine(root, "payload.txt"); File.WriteAllText(input, "plugin payload");
                        string archive = Path.Combine(root, "payload." + (package == ArchivePluginInstaller.Arj ? "arj" : "lzh"));
                        ArchiveEngine.Create(archive, new[] { input }, settings, CancellationToken.None); ArchiveEngine.Test(ArchiveEngine.Open(archive), CancellationToken.None);
                        check(true, package.Name + " downloaded executable creates a verified archive");
                    }
                }
                int attempts = 0;
                string retried = ArchivePluginInstaller.Install(ArchivePluginInstaller.Rar, CancellationToken.None, delegate { }, Path.Combine(root, "retry"), delegate(string url, Stream output, CancellationToken token, Action<long,long> changed) {
                    if (++attempts == 1) { output.WriteByte(1); throw new TransientNetworkException("temporary test failure"); }
                    using (var source = File.OpenRead(ArchivePluginInstaller.Target(ArchivePluginInstaller.Rar, installed))) source.CopyTo(output);
                });
                check(attempts == 2 && File.Exists(retried), "transient failure retries from a clean partial file and validates the complete package");
                string invalidRoot = Path.Combine(root, "invalid"); bool corrupt = false;
                try { ArchivePluginInstaller.Install(ArchivePluginInstaller.Arj, CancellationToken.None, delegate { }, invalidRoot, delegate(string url, Stream output, CancellationToken token, Action<long,long> changed) { output.WriteByte(1); }); } catch (IOException) { corrupt = true; }
                check(corrupt && !Directory.EnumerateFileSystemEntries(invalidRoot).Any(), "incorrect download is rejected and staging is removed");
                string cancelledRoot = Path.Combine(root, "cancelled"); bool cancelled = false;
                using (var source = new CancellationTokenSource()) {
                    try { ArchivePluginInstaller.Install(ArchivePluginInstaller.Arj, source.Token, delegate { }, cancelledRoot, delegate(string url, Stream output, CancellationToken token, Action<long,long> changed) { output.WriteByte(1); source.Cancel(); token.ThrowIfCancellationRequested(); }); } catch (OperationCanceledException) { cancelled = true; }
                }
                check(cancelled && !Directory.EnumerateFileSystemEntries(cancelledRoot).Any(), "cancelled transfer publishes nothing and removes staging");
                foreach (var package in ArchivePluginInstaller.Packages) {
                    var window = new ArchivePluginWindow(null, package, delegate { });
                    NetworkTests.Render(window, Path.Combine(root, package.Id + ".png"));
                    check(FluentUiTests.Children<ProgressBar>(window).Single().Height == 3, package.Name + " thin progress line"); window.Close();
                }
                string selected = null; string arj = ArchivePluginInstaller.Target(ArchivePluginInstaller.Arj, installed);
                var existingWindow = new ArchivePluginWindow(null, ArchivePluginInstaller.Arj, delegate(string path) { selected = path; }, arj); existingWindow.Show();
                FluentUiTests.Children<Button>(existingWindow).Single(x => (string)x.Content == L.Text("archivePlugin.download")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (selected == null && timer.ElapsedMilliseconds < 10000) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
                check(selected == arj && ArchiveSettings.Load().ArjPath == arj, "existing custom installation is selected and persisted without another download"); existingWindow.Close();
                var baseline = ArchiveSettings.Load(); baseline.LhaPath = ""; baseline.Save();
                var configuration = new ConfigurationDialog(null, L.Text("settings.archivesSection"));
                var current = ArchiveSettings.Load(); current.LhaPath = ArchivePluginInstaller.Target(ArchivePluginInstaller.Lha, installed); current.Save();
                typeof(ConfigurationDialog).GetMethod("RefreshArchivePaths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(configuration, null);
                check(FluentUiTests.Children<TextBox>(configuration).Any(x => x.Text == current.LhaPath), "settings opened before background completion merge the newly saved plugin path"); configuration.Close();
                File.WriteAllLines(Path.Combine(root, "archive-plugin-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "archive-plugin-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
        }
    }
}

