using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace CommanderCZ
{
    /// <summary>Ověření integrity nativních pluginů proti hashům vkompilovaným do programu (viz gen-plugin-hashes.ps1).</summary>
    internal static class PluginIntegrity
    {
        internal static string PluginRoot { get { return Path.Combine(AppPaths.Resource("plugins")); } }

        /// <summary>Otevře soubor jen pro čtení bez možnosti zápisu, ověří hash a vrátí otevřený proud.
        /// Proud držený během načtení brání záměně souboru mezi kontrolou a použitím.</summary>
        internal static FileStream OpenVerified(string relative)
        {
            string expected;
            if (!PluginHashes.Expected.TryGetValue(relative, out expected)) throw new IOException(L.Text("security.pluginNotTrustedPrefix") + relative);
            string path = Path.GetFullPath(Path.Combine(PluginRoot, relative));
            if (!path.StartsWith(Path.GetFullPath(PluginRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.Text("security.invalidPluginPathPrefix") + relative);
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

        /// <summary>Drží všechny ověřené soubory zamčené až do Dispose; metadata nejsou důkazem integrity.</summary>
        internal static IDisposable Acquire(params string[] relatives)
        {
            var lease = new PluginLease();
            try
            {
                foreach (string relative in relatives) lease.Files.Add(OpenVerified(relative));
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        sealed class PluginLease : IDisposable
        {
            internal readonly List<FileStream> Files = new List<FileStream>();
            public void Dispose() { foreach (var file in Files) file.Dispose(); Files.Clear(); }
        }
    }
}

