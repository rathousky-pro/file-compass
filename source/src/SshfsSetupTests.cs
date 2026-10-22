using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CommanderCZ
{
    internal static class SshfsSetupTests
    {
        internal static int Run(string folder)
        {
            folder = Path.GetFullPath(folder); Directory.CreateDirectory(folder);
            var checks = new List<string>(); Action<bool, string> check = delegate(bool valid, string text) { if (!valid) throw new Exception(text); checks.Add(text); };
            Action<Action, string> rejects = delegate(Action action, string text) { bool rejected = false; try { action(); } catch { rejected = true; } check(rejected, text); };
            try
            {
                check(SshfsSetup.MissingPackages(true, true) == 0 && SshfsSetup.MissingPackages(false, true) == 1 && SshfsSetup.MissingPackages(true, false) == 2 && SshfsSetup.MissingPackages(false, false) == 3, "detect each missing dependency without reinstalling existing components");
                rejects(delegate { SshfsSetup.Select(0).ToArray(); }, "reject empty component selection");
                rejects(delegate { SshfsSetup.Select(4).ToArray(); }, "reject unknown component selection");
                check(SshfsSetup.Select(3).Select(x => x.Flag).SequenceEqual(new[] { 1, 2 }), "install WinFsp before SSHFS-Win");
                check(SshfsSetup.Helper(new[] { "--sshfs-install-helper", "unknown-pipe", folder, "3" }) == 87, "elevated worker rejects arbitrary pipe names before accessing files");
                check(SshfsSetup.Helper(new[] { "--sshfs-install-helper", "CommanderCZ-sshfs-" + Guid.NewGuid().ToString("N"), folder, "4" }) == 87, "elevated worker rejects arbitrary installer selection");
                var wire = new SshfsSetupProgress("error", -1, "Česká chyba\nDruhý řádek\tPodrobnosti"); var restored = SshfsSetup.Decode(SshfsSetup.Encode(wire));
                check(restored.Stage == wire.Stage && restored.Text == wire.Text && restored.Percent == wire.Percent, "IPC preserves Czech text, tabs and multiline errors");
                rejects(delegate { SshfsSetup.Decode("wrong protocol"); }, "reject malformed IPC message");
                var meter = new SshfsMsiProgress();
                check(meter.Update(0, 100, 0) == 0 && meter.Update(2, 25, 0) == 25 && meter.Update(3, 100, 0) == 12 && meter.Update(2, 175, 0) == 100, "MSI progress follows real ticks and adjusts total");
                check(meter.Update(0, 100, 1) == 100 && meter.Update(2, 25, 0) == 75, "MSI rollback reports backwards progress");
                string corrupt = Path.Combine(folder, "corrupt.msi"); File.WriteAllText(corrupt, "not an official installer");
                rejects(delegate { SshfsSetup.Verify(SshfsSetup.Packages[0], corrupt); }, "reject modified installer before privileged execution");
                string unsigned = Path.Combine(folder, "unsigned.msi"); File.Copy(corrupt, unsigned, true); string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(unsigned))).Replace("-", "");
                var fake = new SshfsPackage(1, "Unsigned test", "unsigned.msi", "https://example.invalid/", hash);
                rejects(delegate { SshfsSetup.Verify(fake, unsigned); }, "correct hash alone cannot bypass Authenticode verification");
                string downloads = Path.Combine(folder, "packages-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(downloads);
                var cancel = new CancellationTokenSource(); cancel.Cancel();
                rejects(delegate { SshfsSetup.Download(SshfsSetup.Packages[0], downloads, cancel.Token, delegate { }).GetAwaiter().GetResult(); }, "cancel download before network access"); cancel.Dispose();
                check(!File.Exists(Path.Combine(downloads, SshfsSetup.Packages[0].FileName)), "cancelled download never publishes an installer");
                var events = new List<SshfsSetupProgress>();
                foreach (var package in SshfsSetup.Packages)
                {
                    SshfsSetup.Download(package, downloads, CancellationToken.None, delegate(SshfsSetupProgress p) { lock (events) events.Add(p); }).GetAwaiter().GetResult();
                    SshfsSetup.Verify(package, Path.Combine(downloads, package.FileName));
                    check(true, "real signed official " + package.Name + " download passes SHA-256 and Authenticode");
                }
                check(events.Any(x => x.Stage == "download" && x.Percent >= 0 && x.Percent <= 100) && events.Any(x => x.Stage == "verify"), "real downloads expose progress and verification stages");
                events.Clear();
                int result = SshfsSetup.Install(downloads, 3, CancellationToken.None, delegate(SshfsSetupProgress p) { lock (events) events.Add(p); }, true).GetAwaiter().GetResult();
                check(result == 0 && events.Count(x => x.Stage == "install") == 2 && events.Last().Stage == "result", "real separate worker and ACL-protected IPC verify and stage both packages without modifying system drivers");
                check(!Directory.GetDirectories(downloads, "check-*").Any(), "worker cleans its private verification staging directory");
                var installed = SshfsSetup.MissingPackages();
                FluentTheme.Ensure(); var window = new SshfsSetupWindow(null); NetworkTests.Render(window, Path.Combine(folder, "sshfs-setup.png"));
                check(FluentUiTests.Children<ProgressBar>(window).Single().Height >= 22, "installer window displays a readable progress indicator");
                check(FluentUiTests.Children<TextBlock>(window).Any(x => x.Text.Contains("Antigravity")), "installer explains disk availability to external applications");
                check(FluentUiTests.Children<TextBlock>(window).Any(x => x.Text.Contains("správce")), "installer explains Windows elevation before installation");
                window.Close(); check(SshfsSetup.MissingPackages() == installed, "verification does not install, upgrade or remove existing dependencies");
                // Optional disposable MSI fixtures exercise Windows Installer itself, not a progress simulation.
                string msi = Path.Combine(folder, "fixture", "fixture.msi");
                if (File.Exists(msi))
                {
                    string target = Path.Combine(folder, "fixture", "target", "payload.txt");
                    if (File.Exists(target)) File.Delete(target);
                    events.Clear(); result = SshfsMsi.Install(msi, "Testovací balíček", delegate(SshfsSetupProgress p) { events.Add(p); }, delegate { return false; });
                    File.WriteAllLines(Path.Combine(folder, "fixture-progress.txt"), events.Select(SshfsSetup.Encode));
                    check(result == 0 && File.Exists(target) && File.ReadAllText(target) == "CommanderCZ installation fixture", "real per-user MSI copies fixture file only into test workspace");
                    check(events.Any(x => x.Stage == "install" && x.Percent > 0), "real Windows Installer progress callback reaches our progress channel");
                    File.Delete(target);
                    result = SshfsMsi.Install(msi, "Test zrušení", delegate { }, delegate { return true; });
                    check(result == 1602 && !File.Exists(target), "real MSI cancellation rolls back without killing installer or leaving target file");
                }
                File.WriteAllLines(Path.Combine(folder, "sshfs-setup-test-result.txt"), checks.Concat(new[] { "PASS: " + checks.Count + " checks" })); return 0;
            }
            catch (Exception ex) { File.WriteAllLines(Path.Combine(folder, "sshfs-setup-test-result.txt"), checks.Concat(new[] { "FAIL: " + ex })); return 1; }
        }
    }
}

