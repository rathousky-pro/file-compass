using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace FileCompass {
    internal static partial class CloudStore {
        internal static string AccountKeyFile(CloudAccount account) { return Path.Combine(AccountPath(account), "encryption-key.dat"); }
        static void CreateAccountKey(CloudAccount account) {
            string path = AccountKeyFile(account); if (File.Exists(path)) return;
            var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var encrypted = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes("FileCompass.Cloud"), DataProtectionScope.CurrentUser);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(encrypted, 0, encrypted.Length);
        }
        internal static string AccountKey(CloudAccount account) {
            try {
                string path = AccountKeyFile(account);
                return File.Exists(path) ? Convert.ToBase64String(ProtectedData.Unprotect(File.ReadAllBytes(path), Encoding.UTF8.GetBytes("FileCompass.Cloud"), DataProtectionScope.CurrentUser)) : Key;
            } catch (CryptographicException ex) { throw new IOException(L.Text("cloud.reauthorizeRequired"), ex); }
        }
        internal static void RemoveAccountFiles(CloudAccount account) {
            string directory = Path.GetFullPath(AccountPath(account));
            string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var paths = Directory.GetDirectories(Root, account.Id + ".before-signin-*").Where(x => { Guid suffix; return Guid.TryParseExact(Path.GetFileName(x).Substring((account.Id + ".before-signin-").Length), "N", out suffix); }).Concat(new[] { directory });
            foreach (string path in paths) {
                string full = Path.GetFullPath(path); if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("cloud.invalidAccount"));
                if (!Directory.Exists(full)) continue;
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("cloud.invalidAccount"));
                foreach (string file in Directory.GetFiles(full)) File.Delete(file);
                Directory.Delete(full);
            }
        }
        internal static async Task Reauthorize(CloudAccount account, Func<CloudQuestion, Task<string>> ask, CancellationToken token, Action<string> openBrowser = null, Func<CloudAccount, CancellationToken, Task> authorization = null) {
            if (authorization == null) CloudApplicationOAuth.RequireReady(account.Provider);
            // Authorize independently of the imported PC's DPAPI key. Publish only a complete, verified account.
            var staged = new CloudAccount { Name = account.Name, Provider = account.Provider };
            string stagePath = AccountPath(staged), destination = AccountPath(account), backup = destination + ".before-signin-" + Guid.NewGuid().ToString("N");
            int oldPort = account.Port; string oldPassword = account.BridgePassword, oldFingerprint = account.Fingerprint, oldClient = account.AuthorizationClient; bool oldReady = account.Ready;
            bool movedOld = false, movedNew = false, committed = false;
            try {
                await Initialize(staged, token).ConfigureAwait(false);
                if (authorization == null) await Authorize(staged, ask, token, openBrowser).ConfigureAwait(false); else await authorization(staged, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await Run(staged, new[] { "lsjson", "remote:", "--stat" }, token, 60000).ConfigureAwait(false);
                staged.Port = CloudRuntime.FreePort(); staged.BridgePassword = NetworkSettingsStore.Protect(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
                CloudKeys.Create(staged); token.ThrowIfCancellationRequested();
                var accounts = Load(); var saved = accounts.Accounts.FirstOrDefault(x => x.Id == account.Id); if (saved == null) throw new IOException(L.Text("cloud.accountMissing"));
                if (Directory.Exists(destination)) { Directory.Move(destination, backup); movedOld = true; }
                Directory.Move(stagePath, destination); movedNew = true;
                account.Port = saved.Port = staged.Port; account.BridgePassword = saved.BridgePassword = staged.BridgePassword; account.Fingerprint = saved.Fingerprint = staged.Fingerprint; account.Ready = saved.Ready = true;
                account.AuthorizationClient = saved.AuthorizationClient = staged.AuthorizationClient;
                Save(accounts); committed = true;
                if(movedOld)PathSafety.DeleteTree(backup);
            } finally {
                if (!committed) {
                    account.Port = oldPort; account.BridgePassword = oldPassword; account.Fingerprint = oldFingerprint; account.Ready = oldReady;
                    account.AuthorizationClient = oldClient;
                    if (movedNew) Directory.Move(destination, stagePath);
                    if (movedOld) Directory.Move(backup, destination);
                }
                string full = Path.GetFullPath(stagePath), root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) PathSafety.DeleteTree(full);
            }
        }
    }
}
