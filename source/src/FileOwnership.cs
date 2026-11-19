using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass
{
    internal static class FileOwnership
    {
        internal static bool IsCandidate(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length < 4 || path[1] != ':' || path[2] != '\\' ||
                path.IndexOfAny(new[] { '*', '?', '"', '\r', '\n', '\0' }) >= 0 || path.IndexOf(':', 2) >= 0) return false;
            try
            {
                string full = Path.GetFullPath(path).TrimEnd('\\');
                return full.Length > 2 && Char.IsLetter(full[0]);
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
        }

        internal static int Run(string[] args)
        {
            using (var identity = WindowsIdentity.GetCurrent())
                if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 2;
            var sid = new SecurityIdentifier(args[1]);
            bool recursive = args[2] == "1", allowDelete = args[3] == "1";
            var pending = new Stack<string>(Encoding.UTF8.GetString(Convert.FromBase64String(args[4]))
                .Split('\0').Reverse());
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int failed = 0;
            while (pending.Count > 0)
            {
                string path = pending.Pop();
                try
                {
                    if (!IsCandidate(path)) { failed++; continue; }
                    path = Path.GetFullPath(path).TrimEnd('\\');
                    if (!visited.Add(path)) continue;
                    if (new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network) { failed++; continue; }
                    using (PathSafety.LockAncestors(path))
                    using (PathSafety.LockOrdinaryItem(path, out var attributes))
                    {
                        bool directory = (attributes & FileAttributes.Directory) != 0;
                        string target = Quote(path);
                        // Take ownership as the elevated account first so it can grant access to the requesting user.
                        if (!Execute("takeown.exe", "/F " + target)) { failed++; continue; }
                        string principal = "*" + sid.Value;
                        bool granted = !allowDelete || Execute("icacls.exe", target + " /grant " + principal + ":(M) /L");
                        if (recursive && directory)
                        {
                            try
                            {
                                foreach (string child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
                            }
                            catch (IOException) { failed++; }
                            catch (UnauthorizedAccessException) { failed++; }
                        }
                        if (!Execute("icacls.exe", target + " /setowner " + principal + " /L") || !granted) failed++;
                    }
                }
                catch (IOException) { failed++; }
                catch (UnauthorizedAccessException) { failed++; }
                catch (Win32Exception) { failed++; }
            }
            return Math.Min(failed, 65535);
        }

        // Canonical paths have no embedded quotes or trailing separator.
        static string Quote(string value) { return "\"" + value + "\""; }

        static bool Execute(string executable, string arguments)
        {
            using (var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, executable), arguments)
            {
                UseShellExecute = false, CreateNoWindow = true
            }))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
    }

    public partial class MainWindow
    {
        async void TakeFileOwnership(FilePanel panel, Entry[] entries)
        {
            var dialog = new Window();
            CopyUi.Style(dialog, this, L.Text("ownership.title"), 540);
            var body = SettingsUx.Frame(dialog, L.Text("ownership.title"), out var actions);
            var options = new StackPanel { Margin = new Thickness(16) };
            body.Children.Add(options);
            options.Children.Add(CopyUi.Label(L.Text("ownership.confirm")));
            options.Children.Add(new TextBlock { Text = String.Join("\n", entries.Take(5).Select(x => x.FullPath)), TextTrimming = TextTrimming.CharacterEllipsis });
            var recursive = new CheckBox { Content = L.Text("ownership.recursive"), IsChecked = true, Margin = new Thickness(0, 12, 0, 5) };
            if (entries.Any(x => x.IsDirectory)) options.Children.Add(recursive);
            var allowDelete = new CheckBox { Content = L.Text("ownership.allowDelete"), IsChecked = true };
            options.Children.Add(allowDelete);
            var cancel = CopyUi.Button(L.Text("action.cancel"), dialog.Close); cancel.IsCancel = true; actions.Children.Add(cancel);
            var start = CopyUi.Button(L.Text("action.start"), () => dialog.DialogResult = true);
            start.IsDefault = true; actions.Children.Add(start);
            if (dialog.ShowDialog() != true) return;

            try
            {
                string sid;
                using (var identity = WindowsIdentity.GetCurrent()) sid = identity.User.Value;
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Join("\0", entries.Select(x => x.FullPath))));
                string args = "--take-file-ownership " + sid + (recursive.IsChecked == true ? " 1" : " 0") +
                    (allowDelete.IsChecked == true ? " 1 " : " 0 ") + encoded;
                fileToolsRunning++;
                try
                {
                    int? exitCode = await ShowOwnershipProgress(args);
                    if (exitCode.HasValue)
                    {
                        MessageBox.Show(this, L.Text(exitCode == 0 ? "ownership.success" : "ownership.partial"),
                            L.Text("ownership.title"), MessageBoxButton.OK,
                            exitCode == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                    }
                }
                finally { fileToolsRunning--; panel.Refresh(); }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.Text("ownership.title")); }
        }

        async Task<int?> ShowOwnershipProgress(string arguments)
        {
            string title = L.Text("ownership.working");
            var window = new Window();
            CopyUi.Style(window, this, title, 480);
            var body = SettingsUx.Frame(window, title, out var actions);
            var content = new StackPanel { Margin = new Thickness(18) };
            body.Children.Add(content);
            content.Children.Add(CopyUi.Label(L.Text("ownership.workingHelp")));
            content.Children.Add(new ProgressBar { Height = 8, IsIndeterminate = true, Margin = new Thickness(2, 4, 2, 0) });

            bool finished = false;
            var completion = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.Closing += delegate(object sender, CancelEventArgs e) { e.Cancel = !finished; };
            ToolDock.Attach(window, actions, () => new DockStatus
            {
                Title = title, Description = L.Text("ownership.workingHelp"), Busy = !finished
            });
            EventHandler run = null;
            run = async delegate
            {
                window.ContentRendered -= run;
                int? exitCode = null;
                Exception failure = null;
                try
                {
                    exitCode = await Task.Run(() =>
                    {
                        using (var process = Process.Start(new ProcessStartInfo(typeof(FileOwnership).Assembly.Location, arguments)
                        {
                            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
                        }))
                        {
                            process.WaitForExit();
                            return process.ExitCode;
                        }
                    });
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    finished = true;
                    window.Close();
                    if (failure == null) completion.SetResult(exitCode);
                    else completion.SetException(failure);
                }
            };
            window.ContentRendered += run;
            window.Show();
            return await completion.Task;
        }
    }
}
