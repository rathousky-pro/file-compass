using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace FileCompass
{
    /// <summary>Verify native plugins against compiled-in hashes (see gen-plugin-hashes.ps1).</summary>
    internal static class PluginIntegrity
    {
        internal static string PluginRoot { get { return Path.Combine(AppPaths.Resource("plugins")); } }

        /// <summary>Open read-only without write sharing, verify the hash and return the open stream.
        /// Keep the stream open during loading to prevent replacement between verification and use.</summary>
        internal static FileStream OpenVerified(string relative)
        {
            string expected;
            string path = UpdateRegistry.Resolve(relative);
            if (!UpdateRegistry.Expected(path,out expected) && !PluginHashes.Expected.TryGetValue(relative, out expected)) throw new IOException(L.Text("security.pluginNotTrustedPrefix") + relative);
            for (string parent = path; !String.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException(L.Text("security.pluginLinkPathRejectedPrefix") + relative);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                string actual;
                using (var sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                stream.Position = 0;
                if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Plugin " + relative + L.Text("security.pluginHashMismatchSuffix"));
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        /// <summary>Keep verified files locked until Dispose; metadata alone is not proof of integrity.</summary>
        internal static IDisposable Acquire(params string[] relatives)
        {
            return UpdateRegistry.Acquire(relatives);
        }
        sealed class PluginLease : IDisposable
        {
            internal readonly List<FileStream> Files = new List<FileStream>();
            public void Dispose() { foreach (var file in Files) file.Dispose(); Files.Clear(); }
        }
    }
}
