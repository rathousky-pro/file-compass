using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace FileCompass {
    internal static class SettingsConnectionsTests {
        static Button Find(DependencyObject root, string key) { return AdvancedToolsTests.Visual<Button>(root).First(x => x.IsVisible && Convert.ToString(x.Content) == L.Text(key)); }
        static void Editor(ConfigurationDialog host, Action open, Action<Window> edit) {
            Exception error = null; bool opened = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            timer.Tick += delegate {
                var child = host.OwnedWindows.Cast<Window>().FirstOrDefault(x => x.IsVisible); if (child == null) return;
                timer.Stop(); opened = true;
                try { if (child.Owner != host) throw new Exception("Editor has the wrong owner");if(!AdvancedToolsTests.Visual<TextBlock>(child).Any(x=>x.FontSize==20))throw new Exception("Settings child is missing shared clean header"); edit(child); }
                catch (Exception ex) { error = ex; child.DialogResult = false; }
            };
            timer.Start(); try { open(); } finally { timer.Stop(); }
            if (error != null) throw error; if (!opened) throw new Exception("The editor did not open");
        }
        internal static int Run(string root) {
            Directory.CreateDirectory(root); var checks = new List<string>(); MainWindow main = null; ConfigurationDialog dialog = null; ConnectionsDialog standalone = null;
            string previous = CloudStore.TestRoot;
            Action<bool, string> check = (valid, text) => { if (!valid) throw new Exception(text); checks.Add(text); };
            try {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                CloudStore.TestRoot = Path.Combine(root, "cloud");
                var account = new CloudAccount { Name = "Osobní disk", Provider = "drive" };
                CloudStore.Save(new CloudAccounts { Accounts = new List<CloudAccount> { account, new CloudAccount { Name = "Pracovní účet", Provider = "onedrive" } } });
                var store = new NetworkSettingsStore(Path.Combine(root, "connections.xml"));
                var ftp = new NetworkProfile { Name = "FTP server", Host = "example.invalid", Protocol = NetworkProtocol.FTP, Port = 21 };
                var sftp = new NetworkProfile { Name = "SFTP server", Host = "example.invalid", Protocol = NetworkProtocol.SFTP, UserName = "tester" };
                store.Put(ftp); store.Put(sftp);
                main = new MainWindow(root, new ProgramSettings { AutoRefresh = false }) { Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                main.Show(); NavigationTests.WaitPanels(main);
                var loginCheck = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                bool loginShown = false;
                loginCheck.Tick += delegate
                {
                    loginCheck.Stop();
                    var login = main.OwnedWindows.Cast<Window>().FirstOrDefault(x => x.IsVisible && x.Title == L.Text("network.loginTitlePrefix") + ftp.Name);
                    if (login == null) return;
                    loginShown = AdvancedToolsTests.Visual<TextBox>(login).Count() == 1 && AdvancedToolsTests.Visual<PasswordBox>(login).Count() == 1 && !Find(login, "ssh.loginContinue").IsEnabled;
                    login.DialogResult = false;
                };
                loginCheck.Start();
                try { NavigationTests.Wait((Task)typeof(MainWindow).GetMethod("ConnectNetworkTask", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, new object[] { ftp })); }
                finally { loginCheck.Stop(); }
                check(loginShown && ftp.FtpNeedsUser, "Blank FTP user opens a username and password prompt, and cancel leaves the profile untouched");
                dialog = new ConfigurationDialog(main, L.Text("network.connectionsTitle"), null, store) { Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                dialog.Show(); NavigationTests.Wait(Task.Delay(40)); dialog.FitSettingsPage();
                var view = dialog.Connections;
                check(view.Tabs.Items.Cast<TabItem>().Select(x => Convert.ToString(x.Header)).SequenceEqual(new[] { L.Text("network.connectionsTitle"), L.Text("connections.cloudTab"), L.Text("schedule.tab") }), "Settings embeds three connection and speed tabs");
                check(!view.Network.IsVisible && !view.Cloud.IsVisible, "Settings does not open separate manager windows");
                check(!AdvancedToolsTests.Visual<Button>(view).Any(x => Convert.ToString(x.Content) == L.Text("action.append") || Convert.ToString(x.Content) == L.Text("ssh.terminalTitle")), "Settings offers management without Connect or terminal actions");
                var list = AdvancedToolsTests.Visual<ListBox>(view).Single();
                check(list.Items.Count == 2, "Network settings lists saved profiles");
                view.Network.SelectFilter(NetworkProtocol.SFTP); dialog.UpdateLayout();
                check(list.Items.Count == 1 && ((NetworkProfile)list.Items[0]).Id == sftp.Id, "Settings protocol filter uses the shared saved profiles");
                NextStageTests.Draw(dialog, Path.Combine(root, "settings-network.png"));
                Editor(dialog, () => Find(view, "action.editDialog").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), child => { ((NetworkProfileDialog)child).Profile.Name = "Upravený server"; child.DialogResult = true; });
                check(new NetworkSettingsStore(store.FilePath).Settings.Connections.Single(x => x.Id == sftp.Id).Name == "Upravený server", "Editing a profile persists immediately through the existing editor");
                Editor(dialog, () => list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent }), child => { check(child is NetworkProfileDialog, "Double click in settings edits instead of connecting"); child.DialogResult = false; });
                check(view.Network.ConnectProfile == null, "Editing does not request a connection or close settings");
                Editor(dialog, () => Find(view, "action.duplicate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), child => { ((NetworkProfileDialog)child).Profile.Name = "Kopie serveru"; child.DialogResult = true; });
                check(store.Settings.Connections.Count == 3 && store.Settings.Connections.Any(x => x.Name == "Kopie serveru" && x.Id != sftp.Id), "Duplicate creates a separate saved profile");
                view.Network.SelectFilter(NetworkProtocol.NFS); dialog.UpdateLayout();
                check(list.Items.Count == 0 && !Find(view, "action.editDialog").IsEnabled && !Find(view, "action.remove").IsEnabled && !Find(view, "action.duplicate").IsEnabled, "Empty selection disables profile operations");
                Editor(dialog, () => Find(view, "network.newConnection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), child => { check(child is NetworkProfileDialog, "New profile opens the existing editor from settings"); child.DialogResult = false; });
                view.Tabs.SelectedIndex = 1; dialog.UpdateLayout(); NavigationTests.Wait(Task.Delay(30));
                check(!AdvancedToolsTests.Visual<Button>(view).Any(x => Convert.ToString(x.Content) == L.Text("cloud.connect")), "Cloud settings omits Connect");
                var cloudList = AdvancedToolsTests.Visual<ListBox>(view).Single();
                check(cloudList.Items.Count == 2, "Cloud settings lists saved accounts");
                check(new[] { "cloud.add", "cloud.signInAgain", "action.rename", "cloud.remove" }.All(key => Find(view, key) != null), "Cloud settings offers add, reauthorize, rename and remove");
                NextStageTests.Draw(dialog, Path.Combine(root, "settings-cloud.png"));
                Editor(dialog, () => Find(view, "cloud.add").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), child => { check(AdvancedToolsTests.Visual<ComboBox>(child).Single().Items.Count == 4, "Cloud wizard retains all four providers");NextStageTests.Draw(child,Path.Combine(root,"cloud-add.png")); child.DialogResult = false; });
                Editor(dialog, () => Find(view, "action.rename").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), child => { AdvancedToolsTests.Visual<TextBox>(child).Single().Text = "Přejmenovaný disk"; child.DialogResult = true; });
                check(CloudStore.Find(account.Id).Name == "Přejmenovaný disk", "Renaming an account persists through the shared cloud manager");
                cloudList.SelectedIndex = -1;
                check(!Find(view, "cloud.signInAgain").IsEnabled && !Find(view, "action.rename").IsEnabled && !Find(view, "cloud.remove").IsEnabled, "No account selection disables account operations");
                cloudList.SelectedIndex = 0;
                var pending = new TaskCompletionSource<bool>();
                Func<CancellationToken, Task> wait = token => { token.Register(() => pending.TrySetCanceled()); return pending.Task; };
                var work = (Task)typeof(CloudManagerWindow).GetMethod("Work", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view.Cloud, new object[] { wait });
                check(!dialog.Sections.IsEnabled && !Find(dialog, "action.save").IsEnabled && !((TabItem)view.Tabs.Items[0]).IsEnabled, "Authorization keeps the settings host on the cloud page and disables Save");
                dialog.Close();
                check(dialog.IsVisible, "Closing during authorization cancels the operation before closing settings");
                NavigationTests.Wait(work);
                check(dialog.Sections.IsEnabled && Find(dialog, "action.save").IsEnabled && !AdvancedToolsTests.Visual<Button>(view).Any(x => x.IsVisible && Convert.ToString(x.Tag) == "cloud-cancel"), "Cancellation restores normal settings controls");
                view.Tabs.SelectedIndex = 2; dialog.UpdateLayout(); NavigationTests.Wait(Task.Delay(40));
                var editor = AdvancedToolsTests.Visual<NetworkScheduleEditor>(view).Single();
                var addRule = Find(editor, "schedule.add");
                for (int i = 0; i < 3; i++) addRule.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(!addRule.IsEnabled, "Schedule supports four scenarios and disables adding a fifth");
                var savedSchedule = new ProgramSettings(); editor.Apply(savedSchedule);
                check(savedSchedule.NetworkScheduleRules.Count == 4, "Four scenarios are saved");
                string schedulePath = Path.Combine(root, "schedule.xml"); savedSchedule.Save(schedulePath);
                check(ProgramSettings.Load(schedulePath).NetworkScheduleRules.Count == 4, "Four scenarios survive a settings round trip");
                var monday = new DateTime(2026, 11, 9, 23, 0, 0);
                savedSchedule.NetworkScheduleEnabled = true;
                savedSchedule.NetworkScheduleRules = new List<NetworkScheduleRule> { new NetworkScheduleRule { Days = 1, Start = 1320, End = 360, KiB = 500 }, new NetworkScheduleRule { Days = 1, Start = 1380, End = 60, KiB = 100 } };
                check(NetworkSchedule.Scheduled(savedSchedule, monday) == 100 * 1024, "Overlapping rules choose the lower speed");
                check(NetworkSchedule.Scheduled(savedSchedule, monday.AddHours(2)) == 500 * 1024, "Overnight rules use the previous selected weekday");
                savedSchedule.NetworkScheduleEnabled = false; check(NetworkSchedule.Scheduled(savedSchedule, monday) == 0, "Master switch disables every rule");
                NextStageTests.Draw(dialog, Path.Combine(root, "settings-speed.png"));
                dialog.Close(); dialog = null;
                standalone = new ConnectionsDialog(main, main, store, null, true) { Left = -18000, Top = -18000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                standalone.Show(); standalone.UpdateLayout();
                check(Find(standalone, "cloud.connect").IsVisible && AdvancedToolsTests.Visual<ListBox>(standalone).Single().Items.Cast<CloudAccount>().Any(x => x.Name == "Přejmenovaný disk"), "Standalone window retains Connect and sees changes made in settings");
                File.WriteAllLines(Path.Combine(root, "result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "result.txt"), String.Join(Environment.NewLine, checks) + "\nFAIL: " + ex); return 1; }
            finally { if (dialog != null) dialog.Close(); if (standalone != null) standalone.Close(); if (main != null) main.Close(); CloudStore.TestRoot = previous; }
        }
    }
}
