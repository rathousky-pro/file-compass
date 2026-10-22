using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal sealed class ArchivePluginPackage
    {
        internal readonly string Id, Name, FileName, Url, Hash, Executable, ExecutableHash, LicenseUrl;
        internal readonly bool Installer;
        internal ArchivePluginPackage(string id, string name, string file, string url, string hash, string executable, string executableHash, string license, bool installer = false)
        { Id = id; Name = name; FileName = file; Url = url; Hash = hash; Executable = executable; ExecutableHash = executableHash; LicenseUrl = license; Installer = installer; }
    }
    internal static class ArchivePluginInstaller
    {
        internal static readonly ArchivePluginPackage Rar = new ArchivePluginPackage("winrar-7.23", "WinRAR / RAR 7.23", "winrar-x64-723.exe", "https://www.rarlab.com/rar/winrar-x64-723.exe", "8FF0DAF3ED564CC743C0E23FF2E253997FFC74460F9673F0B6DD037B2DB4CE7B", "winrar-x64-723.exe", "8FF0DAF3ED564CC743C0E23FF2E253997FFC74460F9673F0B6DD037B2DB4CE7B", "https://www.rarlab.com/license.htm", true);
        internal static readonly ArchivePluginPackage Arj = new ArchivePluginPackage("arj-3.31", "ARJ32 3.31", "ARJ32V331.ZIP", "https://img1.wsimg.com/blobby/go/39066221-0fdc-4357-b8da-71670d7430a0/downloads/555d532d-59a0-4ce9-abaa-546e21639ac9/ARJ32V331.ZIP", "5342EF8B4A872C086564FDC18F4686A3F19875CC0E95EEF474F8AB92E8A225B2", "ARJ32.EXE", "B63DEE82566850BABD33D6F0C43E5E47EA608ED3DCB5BC6F3EFB9162CC344657", "https://arj32.com/");
        internal static readonly ArchivePluginPackage Lha = new ArchivePluginPackage("lha-1.14i", "LHa 1.14i (GnuWin32)", "lha-1.14i-1-bin.zip", "https://downloads.sourceforge.net/gnuwin32/lha-1.14i-1-bin.zip", "C460C5360A85919CBF1790EBB65061640AA412BA28B05F6612A41D57B53131FB", "bin/lha.exe", "53EB005881132CD84C2389AD9CBBB0B5373058CF8019997228AD6E7060C6FEB1", "https://gnuwin32.sourceforge.net/packages/lha.htm");
        internal static readonly ArchivePluginPackage[] Packages = { Rar, Arj, Lha };
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        internal static string Root { get { return Path.Combine(AppPaths.ConfigDirectory, "plugins", "archivers"); } }

        internal static FileStream VerifyHash(string path, string expected)
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try {
                if (file.Length == 0 || file.Length > 128L * 1024 * 1024) throw new IOException(L.Text("archivePlugin.invalidPackage"));
                using (var hash = SHA256.Create()) if (!String.Equals(BitConverter.ToString(hash.ComputeHash(file)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("archivePlugin.hashMismatch"));
                file.Position = 0; return file;
            } catch { file.Dispose(); throw; }
        }
        internal static string Target(ArchivePluginPackage package, string root)
        { return Path.Combine(root, package.Id, package.Executable.Replace('/', Path.DirectorySeparatorChar)); }
        internal static IDisposable AcquireManagedExecutable(string path, string root = null)
        {
            root = root ?? Root;
            foreach (var package in Packages.Where(x => !x.Installer))
                if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(Target(package, root)), StringComparison.OrdinalIgnoreCase)) return VerifyHash(path, package.ExecutableHash);
            string full = Path.GetFullPath(path), managedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("archivePlugin.unknownExecutable"));
            return null;
        }
        internal static string Find(ArchivePluginPackage package, string root = null)
        {
            string path = Target(package, root ?? Root); if (!File.Exists(path)) return null;
            using (VerifyHash(path, package.ExecutableHash)) return path;
        }
        internal static void VerifyInstaller(string path)
        { using (VerifyHash(path, Rar.Hash)) SshfsSignature.Verify(path); }

        internal static string Install(ArchivePluginPackage package, CancellationToken token, Action<SshfsSetupProgress> report, string root = null, Action<string, Stream, CancellationToken, Action<long, long>> download = null)
        {
            if (!Packages.Contains(package)) throw new ArgumentException("package");
            root = Path.GetFullPath(root ?? Root);
            Gate.Wait(token);
            string job = null;
            try {
                token.ThrowIfCancellationRequested();
                string existing = Find(package, root); if (existing != null) { if (package.Installer) VerifyInstaller(existing); return existing; }
                Directory.CreateDirectory(root);
                job = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(job);
                string archive = Path.Combine(job, package.FileName);
                var clock = System.Diagnostics.Stopwatch.StartNew(); long last = -1000;
                report(new SshfsSetupProgress("download", 0, L.Text("archivePlugin.downloading") + " " + package.Name));
                for (int attempt = 0; ; attempt++) {
                    token.ThrowIfCancellationRequested();
                    try {
                        using (var output = new FileStream(archive, FileMode.Create, FileAccess.Write, FileShare.None)) {
                            (download ?? CurlClient.DownloadHttps)(package.Url, output, token, delegate(long received, long total) {
                                if (clock.ElapsedMilliseconds - last < 100) return; last = clock.ElapsedMilliseconds;
                                report(new SshfsSetupProgress("download", total > 0 ? (int)Math.Min(100, received * 100 / total) : -1, L.Text("archivePlugin.downloading") + " " + package.Name + " · " + CopyUi.Bytes(received)));
                            });
                            output.Flush(true);
                        }
                        break;
                    } catch (TransientNetworkException) {
                        if (attempt >= 2) throw;
                        report(new SshfsSetupProgress("retry", -1, L.Text("archivePlugin.retryDownload")));
                        if (token.WaitHandle.WaitOne(1000 * (attempt + 1))) token.ThrowIfCancellationRequested();
                    }
                }
                token.ThrowIfCancellationRequested();
                report(new SshfsSetupProgress("verify", -1, L.Text("archivePlugin.verifying")));
                string payload = Path.Combine(job, "payload"); Directory.CreateDirectory(payload);
                using (VerifyHash(archive, package.Hash)) {
                    if (package.Installer) { VerifyInstaller(archive); File.Copy(archive, Path.Combine(payload, package.Executable)); }
                    else {
                        var outer = ArchiveEngine.Open(archive, null, token);
                        string extracted = Path.Combine(job, "outer"); Directory.CreateDirectory(extracted);
                        ArchiveEngine.Extract(outer, outer.Items.Select(x => x.Path).ToArray(), extracted, token);
                        if (package == Arj) {
                            string sfx = Path.Combine(extracted, "ARJ32V331.EXE");
                            using (VerifyHash(sfx, "1D97C9B804DC32FC378377508ED1AFD89B2673B79929B9B821B2B0FC7C3B4409")) {
                                var inner = ArchiveEngine.Open(sfx, null, token);
                                ArchiveEngine.Extract(inner, inner.Items.Select(x => x.Path).ToArray(), payload, token);
                            }
                        } else foreach (string entry in Directory.GetFileSystemEntries(extracted)) {
                            string destination = Path.Combine(payload, Path.GetFileName(entry));
                            if (Directory.Exists(entry)) Directory.Move(entry, destination); else File.Move(entry, destination);
                        }
                    }
                }
                string ready = Path.Combine(payload, package.Executable.Replace('/', Path.DirectorySeparatorChar));
                using (VerifyHash(ready, package.ExecutableHash)) token.ThrowIfCancellationRequested();
                Directory.Move(payload, Path.Combine(root, package.Id));
                using (VerifyHash(Target(package, root), package.ExecutableHash)) { }
                report(new SshfsSetupProgress("ready", 100, package.Installer ? L.Text("archivePlugin.installerReady") : L.Text("archivePlugin.ready")));
                return Target(package, root);
            }
            finally {
                if (job != null) Cleanup(job, root);
                Gate.Release();
            }
        }
        static void Cleanup(string job, string root)
        {
            string full = Path.GetFullPath(job), prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith(".download-", StringComparison.Ordinal)) throw new IOException(L.Text("archivePlugin.invalidFolder"));
            try { if (Directory.Exists(full)) Directory.Delete(full, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}


