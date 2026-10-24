using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass
{
    internal static class InstanceStateTests
    {
        static void Send(string identity, string folder)
        {
            using (var child = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--instance-forward-test " + SshTerminal.QuoteWindowsArgument(identity) + " " + SshTerminal.QuoteWindowsArgument(folder)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true })) {
                if (!child.WaitForExit(12000)) { child.Kill(); throw new Exception("forward timeout"); }
                if (child.ExitCode != 0) throw new Exception("forward child " + child.ExitCode + ": " + child.StandardError.ReadToEnd());
            }
        }
        static void PumpUntil(Func<bool> done)
        {
            var timer = Stopwatch.StartNew();
            while (!done() && timer.ElapsedMilliseconds < 10000) Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
            if (!done()) throw new Exception("UI timeout");
        }
        internal static int Run(string root)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null;
            Action<bool,string> check = delegate(bool valid, string name) { if (!valid) throw new Exception(name); checks.Add(name); };
            try {
                string identity = root + "-" + Guid.NewGuid().ToString("N"), folder = Directory.CreateDirectory(Path.Combine(root, "česká ' $ složka")).FullName;
                var received = new List<string>();
                using (var server = new InstanceCoordinator(identity)) {
                    check(server.Primary, "first process owns the instance mutex");
                    Send(identity, folder);
                    server.Attach(delegate(string value) { lock (received) received.Add(value); });
                    check(received.Count == 1 && received[0] == folder, "request arriving before window initialization is retained exactly");
                    Send(identity, ""); check(received.Last() == "", "launch without path forwards activation only");
                    using (var invalid = new NamedPipeClientStream(".", server.PipeName, PipeDirection.InOut)) { invalid.Connect(5000); using (var writer = new BinaryWriter(invalid)) { writer.Write(Int32.MaxValue); writer.Flush(); } }
                    Send(identity, folder); check(received.Count == 3 && received.Last() == folder, "oversized IPC request is rejected and server remains usable");
                }
                using (var restarted = new InstanceCoordinator(identity)) check(restarted.Primary, "mutex is released cleanly for the next application start");
                bool refused = false; try { InstanceCoordinator.FolderArgument(new[] { "--unknown" }); } catch (ArgumentException) { refused = true; }
                check(refused && InstanceCoordinator.FolderArgument(new string[0]) == "", "startup accepts only an optional folder argument");
                for (int mask = 0; mask < 16; mask++) {
                    var stored = new ProgramSettings { SaveFoldersOnExit = (mask & 1) != 0, SaveTabsOnExit = (mask & 2) != 0, SaveViewOnExit = (mask & 4) != 0, SaveWindowOnExit = (mask & 8) != 0, LeftFolder = "old", WindowWidth = 900, LeftView = new PanelView { Display = "Details" }, LeftTabs = new[] { new SavedTab { Path = "old", View = new PanelView { Display = "Details" } } } };
                    var current = new ProgramSettings { LeftFolder = "new", WindowWidth = 1300, LeftView = new PanelView { Display = "Short" }, LeftTabs = new[] { new SavedTab { Path = "new", View = new PanelView { Display = "Short" } } } };
                    ExitPreferences.Merge(stored, current);
                    check(stored.LeftFolder == ((mask & 1) != 0 ? "new" : "old") && stored.WindowWidth == ((mask & 8) != 0 ? 1300 : 900) && stored.LeftView.Display == ((mask & 4) != 0 ? "Short" : "Details") && stored.LeftTabs[0].Path == ((mask & 2) != 0 ? "new" : "old") && stored.LeftTabs[0].View.Display == ((mask & 4) != 0 ? "Short" : "Details"), "independent state saving combination " + mask);
                }
                main = new MainWindow(root); main.Show(); NavigationTests.WaitPanels(main);
                var panel = FluentUiTests.Children<FilePanel>(main).First(); int before = panel.TabCount;
                main.AcceptLaunch(folder); PumpUntil(delegate { return panel.CurrentPath == folder && !panel.LocalLoading; });
                check(panel.TabCount == before + 1, "forwarded folder opens in a new tab with exact Unicode path");
                before = panel.TabCount; main.AcceptLaunch(""); check(panel.TabCount == before, "activation does not create an empty tab");
                var storedState = new ProgramSettings { SaveFoldersOnExit = true, SaveTabsOnExit = true, SaveViewOnExit = true, SaveWindowOnExit = true };
                string path = Path.Combine(root, "state.xml"); storedState.Save(path); main.SaveExitPreferences(path); var restored = ProgramSettings.Load(path);
                check(restored.LeftFolder == folder && restored.LeftTabs.Length == panel.TabCount && restored.WindowWidth > 800 && restored.ColumnWidths.Length > 0, "real window state saves atomically to selected configuration");
                main.WindowState = WindowState.Maximized; Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
                main.WindowState = WindowState.Minimized; var minimized = new ProgramSettings(); main.CaptureView(minimized);
                check(minimized.Maximized && minimized.WindowWidth > 800, "minimized maximized window preserves its restored state"); main.WindowState = WindowState.Normal;
                string local = Directory.CreateDirectory(Path.Combine(root, "preferred")).FullName; panel.Navigate(local, false); NavigationTests.Wait(panel.NavigationTask);
                panel.RestoreTabs(new[] { new SavedTab { Path = folder } }, 0, false);
                check(panel.CurrentPath == local, "saving folders without tab paths does not reopen the old active-tab folder");
                var config = new ConfigurationDialog(main, L.Text("settings.startupViewTitle")); NetworkTests.Render(config, Path.Combine(root, "startup-settings.png")); config.Close();
                File.WriteAllLines(Path.Combine(root, "instance-state-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "instance-state-result.txt"), String.Join("\n", checks) + "\nFAIL: " + ex); return 1; }
            finally { if (main != null) main.Close(); }
        }
    }
}

