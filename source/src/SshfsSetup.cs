using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal sealed class SshfsPackage
    {
        internal readonly int Flag;
        internal readonly string Name, FileName, Url, Hash;
        internal SshfsPackage(int flag, string name, string file, string url, string hash)
        { Flag = flag; Name = name; FileName = file; Url = url; Hash = hash; }
    }
    internal sealed class SshfsSetupProgress
    {
        internal string Stage, Text;
        internal int Percent;
        internal SshfsSetupProgress(string stage, int percent, string text) { Stage = stage; Percent = percent; Text = text; }
    }
    internal static class SshfsSetup
    {
        // Pinned releases: no arbitrary URL, executable or MSI properties can be supplied to the elevated worker.
        internal static readonly SshfsPackage[] Packages = {
            new SshfsPackage(1, "WinFsp 2.1", "winfsp-2.1.25156.msi", "https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi", "073A70E00F77423E34BED98B86E600DEF93393BA5822204FAC57A29324DB9F7A"),
            new SshfsPackage(2, "SSHFS-Win 3.5 (64 bit)", "sshfs-win-3.5.20357-x64.msi", "https://github.com/winfsp/sshfs-win/releases/download/v3.5.20357/sshfs-win-3.5.20357-x64.msi", "1657E397F8DCE1C2D2E3220007F9C9F882631882B9BEC4608F7835E87DCD096C")
        };
        internal static bool WinFspInstalled()
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = hive.OpenSubKey(@"SOFTWARE\WinFsp"))
                {
                    string root = key == null ? null : key.GetValue("InstallDir") as string;
                    if (root != null && File.Exists(Path.Combine(root, "bin", "winfsp-x64.dll")) && File.Exists(Path.Combine(root, "bin", "winfsp-x64.sys"))) return true;
                }
            return false;
        }
        internal static int MissingPackages() { return MissingPackages(WinFspInstalled(), SshfsMount.Executable() != null); }
        internal static int MissingPackages(bool winfsp, bool sshfs) { return (winfsp ? 0 : 1) | (sshfs ? 0 : 2); }
        internal static IEnumerable<SshfsPackage> Select(int mask)
        {
            if (mask < 1 || mask > 3) throw new ArgumentException(L.Text("sshfsSetup.invalidComponentSelection"));
            return Packages.Where(x => (x.Flag & mask) != 0);
        }
        internal static string NewJob()
        {
            string path = Path.Combine(AppPaths.ConfigDirectory, "sshfs-setup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); return path;
        }
        internal static void Verify(SshfsPackage package, string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length == 0 || file.Length > 128L * 1024 * 1024) throw new IOException(L.Text("sshfsSetup.invalidInstallerSizePrefix") + package.Name + ".");
                using (var hash = SHA256.Create())
                    if (!String.Equals(BitConverter.ToString(hash.ComputeHash(file)).Replace("-", ""), package.Hash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(L.Text("sshfsSetup.packageChecksumPrefix") + package.Name + L.Text("sshfsSetup.packageChecksumMismatchSuffix"));
                SshfsSignature.Verify(path);
            }
        }
        internal static async Task Download(SshfsPackage package, string folder, CancellationToken token, Action<SshfsSetupProgress> changed)
        {
            string target = Path.Combine(folder, package.FileName), partial = target + ".partial";
            if (File.Exists(target)) { await Task.Run(delegate { Verify(package, target); }, token); return; }
            token.ThrowIfCancellationRequested();
            try
            {
                await Task.Run(delegate
                {
                    var clock = Stopwatch.StartNew(); long previous = -1000;
                    changed(new SshfsSetupProgress("download", 0, L.Text("sshfsSetup.downloadingPrefix") + package.Name + L.Text("common.ellipsis")));
                    using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        CurlClient.DownloadHttps(package.Url, output, token, delegate(long received, long total)
                        {
                            if (clock.ElapsedMilliseconds - previous >= 100)
                            { previous = clock.ElapsedMilliseconds; changed(new SshfsSetupProgress("download", total > 0 ? (int)Math.Min(100, received * 100 / total) : -1, L.Text("sshfsSetup.downloadingPrefix") + package.Name + " · " + CopyUi.Bytes(received) + (total > 0 ? " / " + CopyUi.Bytes(total) : ""))); }
                        });
                    }
                }, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); changed(new SshfsSetupProgress("verify", -1, L.Text("sshfsSetup.verifyingSignatureAndHashPrefix") + package.Name));
                await Task.Run(delegate { Verify(package, partial); }, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); File.Move(partial, target);
            }
            catch { if (File.Exists(partial)) File.Delete(partial); token.ThrowIfCancellationRequested(); throw; }
        }
        internal static async Task<int> Install(string folder, int mask, CancellationToken token, Action<SshfsSetupProgress> changed, bool checkOnly = false)
        {
            Select(mask).ToArray(); token.ThrowIfCancellationRequested();
            string pipeName = "FileCompass-sshfs-" + Guid.NewGuid().ToString("N");
            var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
            using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
            {
                var connection = pipe.WaitForConnectionAsync();
                var info = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,
                    "--sshfs-install-helper " + SshTerminal.QuoteWindowsArgument(pipeName) + " " + SshTerminal.QuoteWindowsArgument(Path.GetFullPath(folder)) + " " + mask + (checkOnly ? " --check-only" : ""))
                    { UseShellExecute = !checkOnly, Verb = checkOnly ? "" : "runas", WindowStyle = ProcessWindowStyle.Hidden };
                changed(new SshfsSetupProgress("elevation", -1, checkOnly ? L.Text("sshfsSetup.verifyingInstallerProcess") : L.Text("sshfsSetup.waitingForUac")));
                Process helper;
                try { helper = await Task.Run(delegate { return Process.Start(info); }).ConfigureAwait(false); }
                catch (Win32Exception ex) { if (ex.NativeErrorCode == 1223) throw new OperationCanceledException(L.Text("sshfsSetup.uacCancelled")); throw; }
                using (helper)
                {
                    var exit = Task.Run(delegate { helper.WaitForExit(); return helper.ExitCode; });
                    var first = await Task.WhenAny(connection, exit, Task.Delay(60000)).ConfigureAwait(false);
                    if (first != connection) throw new IOException(L.Text("sshfsSetup.installerPipeTimeout"));
                    await connection.ConfigureAwait(false);
                    using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                    using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
                    using (token.Register(delegate { try { writer.WriteLine("cancel"); } catch (IOException) { } catch (ObjectDisposedException) { } }))
                    {
                        string line; int result = -1;
                        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            var state = Decode(line); changed(state);
                            if (state.Stage == "result") result = state.Percent;
                        }
                        int code = await exit.ConfigureAwait(false);
                        if (result == 1602 || code == 1602) throw new OperationCanceledException(L.Text("sshfsSetup.cancelledRollingBackStatus"));
                        if (result != 0 && result != 3010) throw new IOException(L.Text("sshfsSetup.incompleteExitCodePrefix") + (result < 0 ? code : result) + L.Text("sshfsSetup.logDetailsSuffix"));
                        return result;
                    }
                }
            }
        }
        internal static string Encode(SshfsSetupProgress value) { return value.Stage + "\t" + value.Percent + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Text ?? "")); }
        internal static SshfsSetupProgress Decode(string line)
        {
            if (line.Length > 131072) throw new IOException(L.Text("sshfsSetup.invalidInstallerResponse"));
            string[] parts = line.Split('\t'); int number;
            if (parts.Length != 3 || !Int32.TryParse(parts[1], out number)) throw new IOException(L.Text("sshfsSetup.invalidInstallerResponse"));
            return new SshfsSetupProgress(parts[0], number, Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])));
        }
        internal static int Helper(string[] args)
        {
            if (args.Length < 4 || args.Length > 5) return 87;
            string pipeName = args[1]; Guid id;
            if (!pipeName.StartsWith("FileCompass-sshfs-", StringComparison.Ordinal) || !Guid.TryParseExact(pipeName.Substring("FileCompass-sshfs-".Length), "N", out id)) return 87;
            int mask; if (!Int32.TryParse(args[3], out mask) || mask < 1 || mask > 3) return 87;
            bool check = args.Length == 5 && args[4] == "--check-only"; if (args.Length == 5 && !check) return 87;
            if (!check && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 740;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
                {
                    pipe.Connect(15000);
                    using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
                    using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                    {
                        int cancelled = 0;
                        Task.Run(async delegate { try { while (await reader.ReadLineAsync().ConfigureAwait(false) != null) Interlocked.Exchange(ref cancelled, 1); Interlocked.Exchange(ref cancelled, 1); } catch (IOException) { Interlocked.Exchange(ref cancelled, 1); } catch (ObjectDisposedException) { } });
                        Action<SshfsSetupProgress> send = delegate(SshfsSetupProgress p) { try { writer.WriteLine(Encode(p)); } catch (IOException) { Interlocked.Exchange(ref cancelled, 1); } };
                        string stage = null;
                        try
                        {
                            // Privileged MSI installation uses a private, administrator-only staging directory.
                            stage = check ? Path.Combine(args[2], "check-" + Guid.NewGuid().ToString("N")) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "FileCompass-sshfs-" + Guid.NewGuid().ToString("N"));
                            if (check) Directory.CreateDirectory(stage);
                            else
                            {
                                var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
                                foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
                                    acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                                Directory.CreateDirectory(stage, acl);
                            }
                            bool restart = false;
                            // Copy and recheck ALL selected packages before changing the system.
                            foreach (var package in Select(mask))
                            {
                                if (Volatile.Read(ref cancelled) != 0) throw new OperationCanceledException();
                                string path = Path.Combine(stage, package.FileName); File.Copy(Path.Combine(Path.GetFullPath(args[2]), package.FileName), path, false);
                                send(new SshfsSetupProgress("verify", -1, L.Text("sshfsSetup.verifyingPackagePrefix") + package.Name + L.Text("sshfsSetup.beforeInstallationSuffix"))); Verify(package, path);
                            }
                            foreach (var package in Select(mask))
                            {
                                if (Volatile.Read(ref cancelled) != 0) throw new OperationCanceledException();
                                if (check) { send(new SshfsSetupProgress("install", 100, L.Text("sshfsSetup.verifiedWithoutInstallPrefix") + package.Name)); continue; }
                                // A second app may have installed the dependency while the download/UAC was pending.
                                if ((MissingPackages() & package.Flag) == 0) { send(new SshfsSetupProgress("install", 100, package.Name + L.Text("sshfsSetup.alreadyInstalledSuffix"))); continue; }
                                int code = SshfsMsi.Install(Path.Combine(stage, package.FileName), package.Name, send, delegate { return Volatile.Read(ref cancelled) != 0; });
                                if (code == 3010) restart = true;
                                else if (code != 0) { send(new SshfsSetupProgress("error", -1, MsiError(code))); send(new SshfsSetupProgress("result", code, L.Text("sshfsSetup.notCompleted"))); return code; }
                                // Do not start a dependent installer when Windows explicitly requires a reboot.
                                if (restart) break;
                            }
                            send(new SshfsSetupProgress("result", restart ? 3010 : 0, restart ? L.Text("sshfsSetup.restartThenConnectHelp") : L.Text("sshfsSetup.completedStatus"))); return restart ? 3010 : 0;
                        }
                        catch (OperationCanceledException) { send(new SshfsSetupProgress("result", 1602, L.Text("sshfsSetup.cancelledStatus"))); return 1602; }
                        catch (Exception ex) { send(new SshfsSetupProgress("error", -1, ex.Message)); send(new SshfsSetupProgress("result", 1603, L.Text("sshfsSetup.failedStatus"))); return 1603; }
                        finally { if (stage != null) try { foreach (string file in Directory.GetFiles(stage)) File.Delete(file); Directory.Delete(stage); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                    }
                }
            }
            catch { return 1603; }
        }
        internal static string MsiError(int code)
        {
            if (code == 1602) return L.Text("sshfsSetup.cancelledRollingBackHelp");
            if (code == 1618) return L.Text("sshfsSetup.anotherInstallerRunning");
            if (code == 1638) return L.Text("sshfsSetup.otherVersionInstalled");
            return L.Text("sshfsSetup.windowsInstallerErrorPrefix") + code + ".";
        }
    }
    internal static class SshfsSignature
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct FileInfo { internal uint Size; [MarshalAs(UnmanagedType.LPWStr)] internal string Path; internal IntPtr Handle, Subject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct TrustData
        {
            internal uint Size; internal IntPtr Policy, Sip; internal uint Ui, Revocation, Union; internal IntPtr File;
            internal uint Action; internal IntPtr State, Url; internal uint Flags, Context;
        }
        [DllImport("wintrust.dll", ExactSpelling = true)] static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
        internal static void Verify(string path)
        {
            var file = new FileInfo { Size = (uint)Marshal.SizeOf(typeof(FileInfo)), Path = Path.GetFullPath(path) };
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo))); Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf(typeof(TrustData)), Ui = 2, Union = 1, File = pointer, Action = 1 };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try { int code = WinVerifyTrust(new IntPtr(-1), ref action, ref data); if (code != 0) throw new IOException(L.Text("sshfsSetup.signatureVerificationFailedPrefix") + code.ToString("X8") + L.Text("sshfsSetup.installationRejectedSuffix")); }
            finally { data.Action = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); Marshal.DestroyStructure(pointer, typeof(FileInfo)); Marshal.FreeHGlobal(pointer); }
        }
    }
    internal sealed class SshfsMsiProgress
    {
        int total, done; bool backwards;
        internal int Update(int type, int value, int direction)
        {
            if (type == 0) { total = Math.Max(0, value); done = 0; backwards = direction != 0; }
            else if (type == 2) done += Math.Max(0, value);
            else if (type == 3) total += Math.Max(0, value);
            int percent = total > 0 ? (int)Math.Min(100, (long)done * 100 / total) : 0;
            return backwards ? 100 - percent : percent;
        }
    }
    internal static class SshfsMsi
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int UiHandler(IntPtr context, uint type, IntPtr record);
        [DllImport("msi.dll")] static extern uint MsiSetInternalUI(uint level, IntPtr owner);
        [DllImport("msi.dll")] static extern uint MsiSetExternalUIRecord(UiHandler handler, uint filter, IntPtr context, out IntPtr previous);
        [DllImport("msi.dll")] static extern int MsiRecordGetInteger(IntPtr record, uint field);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] static extern uint MsiFormatRecord(IntPtr install, IntPtr record, StringBuilder text, ref uint length);
        [DllImport("msi.dll", CharSet = CharSet.Unicode)] static extern uint MsiInstallProduct(string package, string properties);
        internal static int Install(string path, string name, Action<SshfsSetupProgress> changed, Func<bool> cancelled)
        {
            var meter = new SshfsMsiProgress(); string action = L.Text("sshfsSetup.installingPrefix") + name + L.Text("common.ellipsis");
            UiHandler callback = delegate(IntPtr context, uint type, IntPtr record)
            {
                try
                {
                    uint message = type & 0xFF000000;
                    if (message == 0x0A000000)
                    {
                        int percent = meter.Update(MsiRecordGetInteger(record, 1), MsiRecordGetInteger(record, 2), MsiRecordGetInteger(record, 3));
                        changed(new SshfsSetupProgress("install", percent, action));
                        if (cancelled()) return 2; // Windows Installer performs its own rollback; never kill msiexec.
                    }
                    else if (message == 0x08000000) { action = L.Text("sshfsSetup.installingPrefix") + name + L.Text("common.ellipsis"); changed(new SshfsSetupProgress("install", -1, action)); }
                    else if (message == 0x01000000 || message == 0x02000000)
                    {
                        uint length = 8192; var text = new StringBuilder((int)length); MsiFormatRecord(IntPtr.Zero, record, text, ref length);
                        changed(new SshfsSetupProgress("error", -1, text.ToString())); return 2;
                    }
                    else if (message == 0x05000000 || message == 0x19000000) return 2; // Files in use: no silent termination of user programs.
                    return 1;
                }
                catch { return 2; }
            };
            uint old = MsiSetInternalUI(2, IntPtr.Zero); IntPtr previous;
            uint result = MsiSetExternalUIRecord(callback, 0x400 | 0x100 | 0x2 | 0x4 | 0x20 | 0x2000000, IntPtr.Zero, out previous);
            try
            {
                if (result != 0) throw new IOException(L.Text("sshfsSetup.msiProgressReadFailedPrefix") + result + ").");
                changed(new SshfsSetupProgress("install", 0, action));
                using (var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return (int)MsiInstallProduct(path, "REBOOT=ReallySuppress MSIRESTARTMANAGERCONTROL=Disable");
            }
            finally { IntPtr ignored; MsiSetExternalUIRecord(null, 0, IntPtr.Zero, out ignored); MsiSetInternalUI(old, IntPtr.Zero); GC.KeepAlive(callback); }
        }
    }
}


