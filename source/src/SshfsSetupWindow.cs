using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileCompass
{
    internal sealed class SshfsSetupWindow : Window
    {
        static SshfsSetupWindow current;
        readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        readonly TextBlock components = CopyUi.Label(""), status = CopyUi.Label(""), percent = CopyUi.Label("");
        readonly ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 22, Foreground = FluentTheme.Accent, Background = CompactTheme.ThemeBrush("#E4EAF2"), Margin = new Thickness(2, 8, 2, 2) };
        readonly TextBox log = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 125, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        readonly Button install, close;
        readonly StringBuilder messages = new StringBuilder();
        CancellationTokenSource cancellation;
        bool running, ready, restart;
        string job;
        internal Task<bool> Completion { get { return completion.Task; } }
        internal static Task<bool> Open(Window owner)
        {
            if (current != null) { current.Activate(); return current.Completion; }
            while (owner != null && owner.Owner != null) owner = owner.Owner;
            var window = new SshfsSetupWindow(owner); current = window; window.Show(); return window.Completion;
        }
        internal SshfsSetupWindow(Window owner)
        {
            CopyUi.Style(this, owner, L.Text("sshfsSetup.title"), 560);
            var panel = new StackPanel { Margin = new Thickness(16) }; Content = panel;
            var heading = CopyUi.Label(L.Text("sshfsSetup.remoteFolderAsDriveTitle")); heading.FontSize = 17; heading.FontWeight = FontWeights.SemiBold; panel.Children.Add(heading);
            panel.Children.Add(CopyUi.Label(L.Text("sshfsSetup.driveAvailabilityHelp")));
            var card = new Border { Background = CompactTheme.WhiteSurface, BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(10), Margin = new Thickness(2, 6, 2, 8), Child = components }; panel.Children.Add(card);
            panel.Children.Add(CopyUi.Label(L.Text("sshfsSetup.downloadAndVerificationHelp")));
            panel.Children.Add(status); panel.Children.Add(progress); percent.HorizontalAlignment = HorizontalAlignment.Right; panel.Children.Add(percent);
            var links = new StackPanel { Orientation = Orientation.Horizontal };
            links.Children.Add(Link("SSHFS-Win / licence", "https://github.com/winfsp/sshfs-win"));
            links.Children.Add(Link("WinFsp / licence", "https://github.com/winfsp/winfsp")); panel.Children.Add(links);
            panel.Children.Add(new Expander { Header = L.Text("sshfsSetup.installationLogGroup"), Content = log, Margin = new Thickness(2, 6, 2, 8) });
            panel.Children.Add(CopyUi.Label(L.Text("sshfsSetup.winFspCopyright")));
            var actions = FluentTheme.Actions();
            install = CopyUi.Button(L.Text("sshfsSetup.downloadAndInstall"), delegate { if (ready) Close(); else Start(); }); install.IsDefault = true; install.MinWidth = 200; actions.Children.Add(install);
            close = CopyUi.Button(L.Text("action.close"), CloseOrCancel); close.MinWidth = 95; actions.Children.Add(close); panel.Children.Add(actions);
            UpdateComponents();
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (running) { e.Cancel = true; CloseOrCancel(); } };
            Closed += delegate { completion.TrySetResult(ready && !restart); if (current == this) current = null; if (cancellation != null) cancellation.Dispose(); };
        }
        static Button Link(string title, string url)
        {
            var button = CopyUi.Button(title, delegate { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, L.Text("shell.openLinkFailedTitle")); } });
            button.Margin = new Thickness(2, 3, 8, 3); button.FontSize = 11; return button;
        }
        void UpdateComponents()
        {
            int missing = SshfsSetup.MissingPackages(); ready = missing == 0 && !restart;
            components.Text = "WinFsp: " + ((missing & 1) == 0 ? L.Text("sshfsSetup.installedLabel") : L.Text("sshfsSetup.missingLabel")) + "\nSSHFS-Win: " + ((missing & 2) == 0 ? L.Text("sshfsSetup.availableLabel") : L.Text("sshfsSetup.missingLabel"));
            install.IsEnabled = missing != 0 && !running && !restart;
            if (!running) status.Text = ready ? L.Text("sshfsSetup.readyToMount") : restart ? L.Text("sshfsSetup.restartBeforeConnecting") : L.Text("sshfsSetup.onlyMissingComponentsHelp");
            if (ready) { progress.IsIndeterminate = false; progress.Value = 100; percent.Text = L.Text("state.ready"); }
        }
        async void Start()
        {
            if (running) return;
            int missing = SshfsSetup.MissingPackages(); if (missing == 0) { UpdateComponents(); return; }
            cancellation = new CancellationTokenSource(); running = true; install.IsEnabled = false; close.Content = L.Text("action.cancelOperation");
            try
            {
                job = SshfsSetup.NewJob();
                foreach (var package in SshfsSetup.Select(missing)) await SshfsSetup.Download(package, job, cancellation.Token, Report);
                int result = await SshfsSetup.Install(job, missing, cancellation.Token, Report);
                restart = result == 3010;
                progress.IsIndeterminate = false; progress.Value = 100;
                percent.Text = restart ? L.Text("sshfsSetup.restartRequiredTitle") : L.Code("state.completed");
                if (!restart && SshfsSetup.MissingPackages() != 0) throw new IOException(L.Text("sshfsSetup.dependenciesStillUnavailable"));
            }
            catch (OperationCanceledException) { status.Text = L.Text("sshfsSetup.cancelledCompletedComponentsPreserved"); percent.Text = L.Text("state.cancelled"); Append(status.Text); }
            catch (Exception ex) { status.Text = ex.Message; percent.Text = L.Text("sshfsSetup.installationFailedTitle"); Append(ex.Message); }
            finally
            {
                running = false; progress.IsIndeterminate = false; close.Content = L.Text("action.close"); close.IsEnabled = true;
                string final = status.Text; UpdateComponents(); if (!ready && !restart) status.Text = final;
                cancellation.Dispose(); cancellation = null;
                if (ready) { install.Content = L.Text("state.done"); install.IsEnabled = true; install.IsDefault = true; }
            }
        }
        void CloseOrCancel()
        {
            if (!running) { Close(); return; }
            cancellation.Cancel(); close.IsEnabled = false; status.Text = L.Text("sshfsSetup.cancellingWithRollback");
        }
        void Report(SshfsSetupProgress state)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(delegate { Report(state); })); return; }
            if (!running) return;
            if (state.Stage == "result") { status.Text = state.Text; Append(state.Text); return; }
            status.Text = state.Text; progress.IsIndeterminate = state.Percent < 0;
            if (state.Percent >= 0) progress.Value = Math.Max(0, Math.Min(100, state.Percent));
            percent.Text = state.Percent < 0 ? "" : state.Percent + " %";
            if (state.Stage != "download" && (messages.Length == 0 || !messages.ToString().EndsWith(state.Text + Environment.NewLine, StringComparison.Ordinal))) Append(state.Text);
        }
        void Append(string text)
        {
            messages.AppendLine(DateTime.Now.ToString("HH:mm:ss") + " · " + text);
            if (messages.Length > 65536) messages.Remove(0, messages.Length - 65536);
            log.Text = messages.ToString(); log.ScrollToEnd();
            if (job != null) try { File.WriteAllText(Path.Combine(job, "installation.log"), messages.ToString(), Encoding.UTF8); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}

