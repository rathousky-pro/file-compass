using System;
using System.IO;
using System.Xml;
namespace FileCompass {
    internal static class AppPaths {
        internal static string ProgramDirectory { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        internal static string DataPathForTerminal() {
            string root = Path.Combine(DataDirectory, "ssh-terminal");
            Directory.CreateDirectory(root); CloudStore.SecureDirectory(root); return root;
        }
        static readonly object gate = new object(); static bool ready,dataReady;
#if APP_TESTS
        internal static string TestProfileDirectory;
#endif
        internal static string ProfileDirectory { get {
#if APP_TESTS
            if(TestProfileDirectory!=null)return TestProfileDirectory;
#endif
            return File.Exists(Path.Combine(ProgramDirectory,"development-profile.txt")) ? Path.GetFullPath(Path.Combine(ProgramDirectory,"..","local-profile")) : ProgramDirectory;
        } }
        internal static string ConfigDirectory { get { lock(gate) { if(!ready) { Migrate(); ready=true; } return Path.Combine(ProfileDirectory,"config"); } } }
        internal static string DataDirectory { get { string config=ConfigDirectory; lock(gate){string data=Path.Combine(ProfileDirectory,"data");if(!dataReady){CloudStore.SecureDirectory(data);foreach(string folder in new[]{"edits","sshfs","transfers","transfer-queue","jit","cloud"})CloudStore.SecureDirectory(Path.Combine(data,folder));dataReady=true;}return data;} } }
        internal static string Resource(string folder) { return ResourceAt(ProgramDirectory,folder); }
        internal static string ResourceAt(string root,string folder) {
            string current=Path.Combine(root,folder=="ui"?"libraries":folder);
            if(Directory.Exists(current))return current;
            string legacy=Path.Combine(root,"config",folder);
            return Directory.Exists(legacy)?legacy:current;
        }
        internal static string ResolveLegacyResourceFile(string file) {
            if(String.IsNullOrWhiteSpace(file))return file;
            string prefix=Path.Combine(ProgramDirectory,"config","plugins")+Path.DirectorySeparatorChar;
            if(file.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) {
                string updated=Path.Combine(Resource("plugins"),file.Substring(prefix.Length)); if(File.Exists(updated))return updated;
            }
            return file;
        }
        internal static void MigrateLayout(string root) {
            string config=Path.Combine(root,"config"),data=Path.Combine(root,"data");
            string marker=Path.Combine(config,"layout-v2.txt"); if(File.Exists(marker))return;
            foreach(string folder in new[]{"cloud","transfers","transfer-queue","edits","jit","sshfs","sshfs-setup"})CopyMissing(Path.Combine(config,folder),Path.Combine(data,folder));
            foreach(string file in new[]{"integrity.key","operations.xml","operations.xml.mac"})CopyFileMissing(Path.Combine(config,file),Path.Combine(data,file));
            CopyFileMissing(Path.Combine(config,"languages","language.txt"),Path.Combine(config,"language.txt"));
            CopyFileMissing(Path.Combine(root,"languages","language.txt"),Path.Combine(config,"language.txt"));
            CopyFileMissing(Path.Combine(config,"plugins","cloud","google-oauth.json"),Path.Combine(config,"oauth","google.json"));
            Directory.CreateDirectory(config); File.WriteAllText(marker,"Configuration and runtime data use separate directories.");
        }
        static void CopyFileMissing(string source,string target) {
            if(File.Exists(source)&&!File.Exists(target)&&(File.GetAttributes(source)&FileAttributes.ReparsePoint)==0) { Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source,target); }
        }
        static void CopyMissing(string source,string target) {
            if(!Directory.Exists(source) || (File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)return;
            bool existed=Directory.Exists(target);
            Directory.CreateDirectory(target);
            CloudStore.SecureDirectory(target);
            foreach(var file in Directory.GetFiles(source)) {
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0 || file.EndsWith(".lock",StringComparison.OrdinalIgnoreCase))continue;
                string destination=Path.Combine(target,Path.GetFileName(file)); if(!File.Exists(destination))File.Copy(file,destination);
            }
            foreach(var directory in Directory.GetDirectories(source))CopyMissing(directory,Path.Combine(target,Path.GetFileName(directory)));
        }
        static void Migrate() {
            string target=Path.Combine(ProfileDirectory,"config"); CloudStore.SecureDirectory(target);
            string marker=Path.Combine(target,"file-compass-migrated.txt"); if(File.Exists(marker)){MigrateLayout(ProfileDirectory);return;}
            // Fresh distributions must not silently import personal credentials.
            MigrateLayout(ProfileDirectory);
            string appearance=Path.Combine(target,"appearance.xml");
            if(File.Exists(appearance)) {
                var xml=new XmlDocument { XmlResolver=null }; using(var reader=XmlReader.Create(appearance,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null }))xml.Load(reader);
                var icon=xml.DocumentElement.SelectSingleNode("ApplicationIcon"); if(icon==null){icon=xml.CreateElement("ApplicationIcon");xml.DocumentElement.AppendChild(icon);} icon.InnerText="FloppyOrbit";xml.Save(appearance);
            }
            File.WriteAllText(marker,"File Compass portable configuration. Imported settings are stored here; existing files were preserved.");
        }
    }
}
