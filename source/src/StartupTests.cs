using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CommanderCZ
{
    public static class StartupTests
    {
        public static int Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string result = Path.Combine(directory, "startup-result.txt");
            StartupAcceleration.Begin(Path.Combine(directory, "jit"));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var app = new Application();
            long themeMs = 0, constructorMs = 0, loadedMs = 0;
            bool failed = false, loaded = false;
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                failed = true; File.WriteAllText(result, "FAIL: " + e.Exception);
                e.Handled = true; app.Shutdown(1);
            };
            try
            {
                FluentTheme.Ensure(); themeMs = watch.ElapsedMilliseconds;
                var window = new MainWindow(directory) { WindowState = WindowState.Minimized, ShowInTaskbar = false };
                constructorMs = watch.ElapsedMilliseconds;
                var split = ((DockPanel)window.Content).Children.OfType<Grid>().Single();
                var panel = split.Children.OfType<FilePanel>().First();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var buttons = (Dictionary<string, Button>)typeof(FilePanel).GetField("driveButtons", flags).GetValue(panel);
                bool deferredDiscovery = buttons.Count == 0;
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                Button button = null; object original = null;
                window.Loaded += async delegate
                {
                    loaded = true; loadedMs = watch.ElapsedMilliseconds;
                    for (int attempt = 0; attempt < 100 && !buttons.ContainsKey(root); attempt++)
                        await System.Threading.Tasks.Task.Delay(50);
                    if (!buttons.TryGetValue(root, out button)) return;
                    original = button.Content;
                    typeof(FilePanel).GetMethod("CheckDrive", flags).Invoke(panel, new object[] { root, button });
                };
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    var pending = (HashSet<string>)typeof(FilePanel).GetField("checkingDrives", flags).GetValue(panel);
                    bool passed = deferredDiscovery && loaded && button != null && !pending.Contains(root) && !Object.ReferenceEquals(original, button.Content)
                        && ((StackPanel)button.Content).Dispatcher == window.Dispatcher;
                    if (!failed) File.WriteAllText(result, passed
                        ? "PASS: real application startup, background drive discovery, UI-thread icon update, periodic drive refresh. Theme=" + themeMs + "ms, constructed=" + constructorMs + "ms, loaded=" + loadedMs + "ms. " + FluentTheme.StartupTiming
                        : "FAIL: drive probe or window startup did not complete.");
                    app.Shutdown(passed && !failed ? 0 : 1);
                };
                timer.Start();
                return app.Run(window);
            }
            catch (Exception ex) { File.WriteAllText(result, "FAIL: " + ex); return 1; }
        }
    }
}

