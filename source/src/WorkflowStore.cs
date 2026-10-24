using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

namespace FileCompass
{
    internal static class WorkflowStore
    {
        static readonly object gate = new object();
        internal static string TestRoot = null;
        internal static string Root { get { return TestRoot ?? AppPaths.ConfigDirectory; } }
        internal static T Load<T>(string name) where T : new()
        {
            string path = Path.Combine(Root, name); if (!File.Exists(path)) return new T();
            if (new FileInfo(path).Length > 8 * 1048576) throw new IOException(L.Text("workflow.storageLimit"));
            using (var input = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1048576 })) return (T)new XmlSerializer(typeof(T)).Deserialize(input);
        }
        internal static void Save<T>(string name, T data)
        {
            Directory.CreateDirectory(Root); string path = Path.Combine(Root, name), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { using (var output = File.Create(temp)) { new XmlSerializer(typeof(T)).Serialize(output, data); if (output.Length > 8 * 1048576) throw new IOException(L.Text("workflow.storageLimit")); output.Flush(true); } if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static T Edit<T>(string name, Action<T> update) where T : new()
        {
            lock (gate)
            {
                Directory.CreateDirectory(Root); FileStream lease = null;
                for (int attempt = 0; ; attempt++) { try { lease = new FileStream(Path.Combine(Root, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); break; } catch (IOException) { if (attempt >= 30) throw; Thread.Sleep(50); } }
                using (lease) { var data = Load<T>(name); update(data); Save(name, data); return data; }
            }
        }
    }
}

