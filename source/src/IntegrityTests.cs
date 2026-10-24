using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using FileCompass.NetworkTransport;
namespace FileCompass
{
    /// <summary>State-file integrity tests for transfer queues and HMAC signatures.</summary>
    public static class IntegrityTests
    {
        static int checks;
        static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException("Integrity test selhal: " + message); }
        public static int Run(string workspace)
        {
            string root = Path.Combine(Path.GetFullPath(workspace), "integrity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                SecurityRegression(root);
                string sample = Path.Combine(root, "sample.xml"); File.WriteAllText(sample, "<a/>");
                Check(!FileIntegrity.Verify(sample), "soubor bez podpisu se nesmí přijmout");
                SignLegacyXml(sample); Check(FileIntegrity.Verify(sample), "podepsaný soubor musí projít");
                File.WriteAllText(sample, "<b/>"); Check(!FileIntegrity.Verify(sample), "upravený soubor se nesmí přijmout");
                string other = Path.Combine(root, "other.xml"); File.WriteAllText(other, "<a/>"); File.Copy(sample + ".mac", other + ".mac", true);
                Check(!FileIntegrity.Verify(other), "podpis zkopírovaný k jinému souboru se nesmí přijmout");

                string id;
                using (var queue = new DurableQueue(root))
                {
                    var job = queue.Begin("local", new[] { "a" }, "b", null, NetworkCopyRule.Skip, false, null, null); id = job.Id;
                    queue.Plan(job, new[] { new DurableFile { Source = "s1", Target = "t1" }, new DurableFile { Source = "s2", Target = "t2" } });
                    queue.Done(job, "s1", "t1"); queue.Finish(job, false);
                }
                using (var queue = new DurableQueue(root))
                {
                    Check(queue.Jobs.Count == 1, "podepsaná úloha se musí načíst (" + queue.LastError + ")");
                    var files = queue.Jobs[0].Files;
                    Check(files.Single(x => x.Source == "s1").Done && !files.Single(x => x.Source == "s2").Done, "potvrzení hotových souborů musí fungovat");
                }
                string xml = Path.Combine(root, "transfer-queue", id + ".xml"), receipt = Path.Combine(root, "transfer-queue", id + ".done");
                File.AppendAllText(receipt, "falesne-potvrzeni\n");
                using (var queue = new DurableQueue(root)) Check(queue.Jobs.Count == 1 && !queue.Jobs[0].Files.Single(x => x.Source == "s2").Done, "podvržené potvrzení se nesmí uznat");
                Check(!File.Exists(xml + ".mac"), "nová fronta obsahuje podpis v jediném souboru");
                var legacyJob = FileIntegrity.ReadXml<DurableTransfer>(xml);
                using (var stream = File.Create(xml)) new XmlSerializer(typeof(DurableTransfer)).Serialize(stream, legacyJob);
                SignLegacyXml(xml);
                using (var queue = new DurableQueue(root))
                {
                    Check(queue.Jobs.Count == 1, "starší podepsaná fronta se musí načíst");
                    queue.Finish(queue.Jobs[0], false);
                }
                File.Delete(xml + ".mac");
                using (var queue = new DurableQueue(root)) Check(queue.Jobs.Count == 1, "uložení starší fronty ji atomicky převede do nové obálky");
                var changed = File.ReadAllBytes(xml); changed[20] ^= 1; File.WriteAllBytes(xml, changed);
                using (var queue = new DurableQueue(root)) Check(queue.Jobs.Count == 0 && !String.IsNullOrEmpty(queue.LastError), "upravená úloha se nesmí načíst");
                File.WriteAllBytes(xml, new byte[0]);
                using (var queue = new DurableQueue(root)) Check(queue.Jobs.Count == 0, "úloha bez podpisu se nesmí načíst");

                string secret = "Heslo-" + Guid.NewGuid().ToString("N");
                string protectedValue = NetworkSettingsStore.Protect(secret);
                Check(NetworkSettingsStore.Unprotect(protectedValue) == secret, "heslo musí projít DPAPI s entropií");
                string legacy = Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(secret), null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
                Check(NetworkSettingsStore.Unprotect(NetworkSettingsStore.UpgradePassword(legacy)) == secret, "heslo uložené starší verzí (bez entropie) se musí stále načíst");
                Report(workspace, "Integrity test OK, kontrol: " + checks);
                return 0;
            }
            catch (Exception ex) { Report(workspace, ex.ToString()); return 1; }
            finally { try { Directory.Delete(root, true); } catch (IOException) { } }
        }
        static void SecurityRegression(string root)
        {
            // These calls prepare launch information only; they must never start test programs.
            foreach (string extension in new[] { ".exe", ".com", ".msi", ".lnk", ".url", ".hta", ".unknown", ".lng", "" })
            {
                var start = FileViewers.LaunchInfo(Path.Combine(root, "marker" + extension), new ProgramSettings());
                Check(start.FileName == Path.Combine(Environment.SystemDirectory, "notepad.exe") && start.Arguments == "\"" + Path.Combine(root, "marker" + extension) + "\"", "F3 musí předat neznámý nebo spustitelný soubor jako data editoru: " + extension);
            }
            foreach (string extension in new[] { ".cmd", ".bat", ".ps1", ".js", ".html" })
            {
                var start = FileViewers.LaunchInfo(Path.Combine(root, "marker" + extension), new ProgramSettings());
                Check(start.FileName == Path.Combine(Environment.SystemDirectory, "notepad.exe"), "skript musí být předán textovému editoru: " + extension);
            }

            string path = Path.Combine(root, "state.xml");
            FileIntegrity.WriteXml(path, "original");
            Check(FileIntegrity.ReadXml<string>(path) == "original" && !File.Exists(path + ".mac"), "atomická obálka musí obsahovat payload i podpis");
            using (var snapshot = FileIntegrity.ReadVerified(path))
            {
                FileIntegrity.WriteXml(path, "replacement");
                Check((string)new XmlSerializer(typeof(string)).Deserialize(snapshot) == "original", "deserializace musí používat ověřený snapshot, nikoliv nově otevřenou cestu");
            }
            bool failed = false;
            try { FileIntegrity.BeforeCommit = delegate { throw new IOException("simulace pádu před commit"); }; FileIntegrity.WriteXml(path, "unfinished"); }
            catch (IOException) { failed = true; }
            finally { FileIntegrity.BeforeCommit = null; }
            Check(failed && FileIntegrity.ReadXml<string>(path) == "replacement", "nedokončený zápis musí ponechat předchozí platný stav");
            Check(!Directory.GetFiles(root, "*.tmp").Any(), "neúspěšný zápis uklidí vlastní temp");
            string other = Path.Combine(root, "renamed.xml"); File.Copy(path, other);
            Check(!FileIntegrity.Verify(other), "obálku nelze přesunout pod jiné jméno");
            var bytes = File.ReadAllBytes(path); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(path, bytes);
            Check(!FileIntegrity.Verify(path), "změna MAC se odmítne");
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("CCZST001"));
            Check(!FileIntegrity.Verify(path), "neúplná obálka se odmítne");
            FileIntegrity.WriteXml(path, "bounded"); bytes = File.ReadAllBytes(path); Array.Copy(BitConverter.GetBytes(Int32.MaxValue), 0, bytes, 8, 4); File.WriteAllBytes(path, bytes);
            Check(!FileIntegrity.Verify(path), "neplatná délka se odmítne před alokací payloadu");
            using (var stream = File.Create(path)) new XmlSerializer(typeof(string)).Serialize(stream, "legacy");
            SignLegacyXml(path);
            Check(FileIntegrity.ReadXml<string>(path) == "legacy", "starší podepsané XML musí být čteno ze stejného snapshotu");
            FileIntegrity.WriteXml(path, "migrated"); File.Delete(path + ".mac");
            Check(FileIntegrity.ReadXml<string>(path) == "migrated", "nový zápis již nesmí záviset na starém sidecar podpisu");

            var options = new SessionOptions { Protocol = Protocol.Sftp, HostName = "test", UserName = "tester", PortNumber = 22 };
            options.AddRawSettings("ResumeDirectory", Path.Combine(root, "checkpoints"));
            using (var checkpoint = new TransferCheckpoint(options, true, "source", "target"))
                checkpoint.Initialize("stage", 123, DateTime.UtcNow, null, false, 0, DateTime.MinValue);
            using (var checkpoint = new TransferCheckpoint(options, true, "source", "target"))
                Check(checkpoint.Data.Length == 123 && checkpoint.Data.Stage == "stage", "checkpoint musí projít novou obálkou");

            string relative = "security-test-" + Guid.NewGuid().ToString("N") + ".bin";
            string plugin = Path.Combine(PluginIntegrity.PluginRoot, relative);
            File.WriteAllBytes(plugin, new byte[] { 1, 2, 3, 4 });
            using (var sha = SHA256.Create()) PluginHashes.Expected.Add(relative, BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(plugin))).Replace("-", ""));
            try
            {
                using (PluginIntegrity.Acquire(relative))
                {
                    bool writeBlocked = false, deleteBlocked = false;
                    try { using (new FileStream(plugin, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } } catch (IOException) { writeBlocked = true; }
                    try { File.Delete(plugin); } catch (IOException) { deleteBlocked = true; }
                    Check(writeBlocked && deleteBlocked, "ověřený plugin musí být zamčený proti zápisu a odstranění");
                }
                DateTime stamp = File.GetLastWriteTimeUtc(plugin);
                File.WriteAllBytes(plugin, new byte[] { 4, 3, 2, 1 }); File.SetLastWriteTimeUtc(plugin, stamp);
                bool rejected = false;
                try { using (PluginIntegrity.Acquire(relative)) { } } catch (IOException) { rejected = true; }
                Check(rejected, "změněný plugin se stejnou velikostí a časem musí být odmítnut");
            }
            finally { PluginHashes.Expected.Remove(relative); File.Delete(plugin); }
        }
        // Only tests generate the former XML + sidecar format to verify migration.
        internal static void SignLegacyXml(string path)
        {
            File.WriteAllText(path + ".mac", FileIntegrity.Mac(Path.GetFileName(path).ToLowerInvariant() + "\0" + Encoding.UTF8.GetString(File.ReadAllBytes(path))), Encoding.ASCII);
        }
        static void Report(string workspace, string text) { File.WriteAllText(Path.Combine(Path.GetFullPath(workspace), "integrity-result.txt"), text); }
    }
}

