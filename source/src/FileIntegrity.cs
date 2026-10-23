using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
namespace CommanderCZ
{
    /// <summary>
    /// State files use HMAC-SHA256 with a key protected by DPAPI for the current user.
    /// Verify the exact bytes deserialized. DPAPI does not isolate processes running as the same user.
    /// </summary>
    internal static class FileIntegrity
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CommanderCZ.integrity-key.v1");
        static readonly object gate = new object();
        static byte[] key;
        internal static string TestDirectory;
        internal static Action BeforeCommit;
        const int MaximumBytes = 64 * 1024 * 1024;
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("CCZST001");
        internal static string KeyPath
        { get { return Path.Combine(TestDirectory ?? AppPaths.ConfigDirectory, "integrity.key"); } }

        static byte[] Key()
        {
            lock (gate)
            {
                if (key != null) return key;
                string path = KeyPath; Directory.CreateDirectory(Path.GetDirectoryName(path));
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (File.Exists(path))
                    {
                        try { return key = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser); }
                        catch (CryptographicException) { throw new IOException(L.Text("integrity.userKeyLoadFailedPrefix") + path); }
                        catch (IOException) { System.Threading.Thread.Sleep(50); continue; }
                    }
                    var fresh = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(fresh);
                    try
                    {
                        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { var blob = ProtectedData.Protect(fresh, Entropy, DataProtectionScope.CurrentUser); stream.Write(blob, 0, blob.Length); stream.Flush(true); }
                        return key = fresh;
                    }
                    catch (IOException) { System.Threading.Thread.Sleep(50); }
                }
                throw new IOException(L.Text("integrity.keyCreationFailedPrefix") + path);
            }
        }

        internal static string Mac(string text)
        { using (var hmac = new HMACSHA256(Key())) return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(text))); }

        static byte[] FileMac(string path, byte[] data, bool envelope)
        {
            using (var hmac = new HMACSHA256(Key()))
            {
                var name = Encoding.UTF8.GetBytes((envelope ? "CommanderCZ.state.v1\0" : "") + Path.GetFileName(path).ToLowerInvariant() + "\0");
                hmac.TransformBlock(name, 0, name.Length, null, 0);
                hmac.TransformFinalBlock(data, 0, data.Length);
                return hmac.Hash;
            }
        }

        static bool Equal(byte[] expected, byte[] actual)
        {
            if (expected.Length != actual.Length) return false;
            int diff = 0; for (int i = 0; i < actual.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        // Return an immutable snapshot: never reopen the path after verifying its contents.
        internal static MemoryStream ReadVerified(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file, Encoding.UTF8, true))
            {
                if (file.Length > MaximumBytes + 44L) throw new IOException(L.Text("integrity.stateRecordTooLarge"));
                byte[] prefix = reader.ReadBytes(Magic.Length);
                byte[] data, expected;
                if (Equal(prefix, Magic))
                {
                    int length = reader.ReadInt32();
                    if (length < 0 || length > MaximumBytes || file.Length != Magic.Length + 4L + length + 32) throw new IOException(L.Text("integrity.invalidRecordLength"));
                    data = reader.ReadBytes(length); expected = reader.ReadBytes(32);
                    if (!Equal(expected, FileMac(path, data, true))) throw new IOException(L.Text("integrity.signatureVerificationFailed"));
                }
                else
                {
                    // Compatibility: accept old XML only with its valid sidecar signature.
                    file.Position = 0; data = reader.ReadBytes(checked((int)file.Length));
                    using (var signature = new FileStream(path + ".mac", FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (signature.Length > 128) throw new IOException(L.Text("integrity.invalidSignature"));
                        using (var text = new StreamReader(signature, Encoding.ASCII))
                            expected = Encoding.ASCII.GetBytes(text.ReadToEnd().Trim());
                    }
                    if (!Equal(expected, Encoding.ASCII.GetBytes(Convert.ToBase64String(FileMac(path, data, false))))) throw new IOException(L.Text("integrity.legacySignatureVerificationFailed"));
                }
                return new MemoryStream(data, false);
            }
        }

        internal static T ReadXml<T>(string path)
        {
            using (var data = ReadVerified(path))
            using (var reader = XmlReader.Create(data, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
        }

        internal static void WriteXml<T>(string path, T value)
        {
            byte[] data;
            using (var payload = new MemoryStream())
            {
                new XmlSerializer(typeof(T)).Serialize(payload, value);
                if (payload.Length > MaximumBytes) throw new IOException(L.Text("integrity.stateRecordTooLarge"));
                data = payload.ToArray();
            }
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new BinaryWriter(file, Encoding.UTF8, true))
                    { writer.Write(Magic); writer.Write(data.Length); writer.Write(data); writer.Write(FileMac(path, data, true)); writer.Flush(); }
                    file.Flush(true);
                }
                var before = BeforeCommit; if (before != null) before();
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        /// <summary>Diagnostic check only. Production reads use ReadXml, never Verify followed by reopening the path.</summary>
        internal static bool Verify(string path)
        {
            try { using (ReadVerified(path)) { } return true; }
            catch (IOException) { return false; }
        }

        internal static void DeleteSignature(string path) { try { if (File.Exists(path + ".mac")) File.Delete(path + ".mac"); } catch (IOException) { } }
    }
}

