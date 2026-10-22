using System;
using System.IO;
using System.Xml;
namespace CommanderCZ {
    internal static class AppPaths {
        internal static string ProgramDirectory { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        static readonly object gate = new object(); static bool ready;
        internal static string ConfigDirectory { get { lock(gate) { if(!ready) { Migrate(); ready=true; } return Path.Combine(ProgramDirectory,"config"); } } }
        internal static string Resource(string folder) { var portable=Path.Combine(ProgramDirectory,"config",folder); return Directory.Exists(portable) ? portable : Path.Combine(ProgramDirectory,folder); }
        static void CopyMissing(string source,string target) {
            if(!Directory.Exists(source) || (File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)return;
            Directory.CreateDirectory(target);
            foreach(var file in Directory.GetFiles(source)) {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0 || file.EndsWith(".lock",StringComparison.OrdinalIgnoreCase))continue;
                string destination=Path.Combine(target,Path.GetFileName(file)); if(!File.Exists(destination))File.Copy(file,destination);
            }
            foreach(var directory in Directory.GetDirectories(source))CopyMissing(directory,Path.Combine(target,Path.GetFileName(directory)));
        }
        static void Migrate() {
            string target=Path.Combine(ProgramDirectory,"config"); Directory.CreateDirectory(target);
            string marker=Path.Combine(target,"file-compass-migrated.txt"); if(File.Exists(marker))return;
            // Preserve the original sources as a recovery copy. Existing portable settings always win.
            CopyMissing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CommanderCZ"),target);
            CopyMissing(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"CommanderCZ"),target);
            string appearance=Path.Combine(target,"appearance.xml");
            if(File.Exists(appearance)) {
                var xml=new XmlDocument { XmlResolver=null }; using(var reader=XmlReader.Create(appearance,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null }))xml.Load(reader);
                var icon=xml.DocumentElement.SelectSingleNode("ApplicationIcon"); if(icon==null){icon=xml.CreateElement("ApplicationIcon");xml.DocumentElement.AppendChild(icon);} icon.InnerText="FloppyOrbit";xml.Save(appearance);
            }
            File.WriteAllText(marker,"File Compass portable configuration. Imported settings are stored here; existing files were preserved.");
        }
    }
}
