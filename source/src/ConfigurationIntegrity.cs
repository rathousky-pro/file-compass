using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;
namespace FileCompass {
    public sealed class ConfigurationSeal { public string Digest,PendingDigest; }
    public sealed class ConfigurationRegistry { public List<string> Paths=new List<string>(); }
    internal sealed class ConfigurationIntegrityException:IOException {internal ConfigurationIntegrityException(string text):base(text){} }
    internal static class ConfigurationIntegrity {
        static readonly object gate=new object();
        sealed class ProfileLease:IDisposable {
            readonly Mutex mutex;
            internal ProfileLease(){bool created;mutex=new Mutex(false,@"Local\FileCompass-config-"+Identity(RegistryPath),out created);try{try{if(!mutex.WaitOne(10000))throw new IOException("Configuration is being updated by another process.");}catch(AbandonedMutexException){}}catch{mutex.Dispose();throw;}}
            public void Dispose(){mutex.ReleaseMutex();mutex.Dispose();}
        }
        static string RegistryPath{get{return Path.Combine(Path.GetDirectoryName(FileIntegrity.KeyPath),"configuration-integrity.xml");}}
        static string Identity(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-","");}
        static string SealPath(string path){return Path.Combine(Path.GetDirectoryName(FileIntegrity.KeyPath),"configuration-seals",Identity(path)+".xml");}
        static string Digest(byte[] bytes){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
        static ConfigurationRegistry Registry(){try{return File.Exists(RegistryPath)?FileIntegrity.ReadXml<ConfigurationRegistry>(RegistryPath):new ConfigurationRegistry();}catch(IOException ex){throw new ConfigurationIntegrityException("Configuration migration registry cannot be verified: "+ex.Message);}}
        static byte[] Serialize<T>(T value){using(var s=new MemoryStream()){new XmlSerializer(typeof(T)).Serialize(s,value);return s.ToArray();}}
        internal static T Read<T>(string path){using(new ProfileLease())lock(gate){
            PathSafety.RejectLinks(path);byte[] bytes;using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length>64L*1024*1024)throw new ConfigurationIntegrityException("Configuration exceeds the size limit.");bytes=new byte[(int)file.Length];int n=0,read;while(n<bytes.Length&&(read=file.Read(bytes,n,bytes.Length-n))>0)n+=read;if(n!=bytes.Length)throw new EndOfStreamException();}
            var registry=Registry();string id=Identity(path),seal=SealPath(path);
            bool exists=File.Exists(seal);
            if(exists){try{var record=FileIntegrity.ReadXml<ConfigurationSeal>(seal);string digest=Digest(bytes);if(record.Digest!=digest&&record.PendingDigest!=digest)throw new ConfigurationIntegrityException("Configuration integrity check failed: "+path);}catch(ConfigurationIntegrityException){throw;}catch(IOException ex){throw new ConfigurationIntegrityException("Configuration signature cannot be verified: "+path+"; "+ex.Message);}}
            else if(registry.Paths.Contains(id))throw new ConfigurationIntegrityException("Configuration signature is missing: "+path);
            T value;using(var s=new MemoryStream(bytes,false))using(var reader=XmlReader.Create(s,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))value=(T)new XmlSerializer(typeof(T)).Deserialize(reader);
            if(!exists)Seal(path,bytes);
            if(!registry.Paths.Contains(id)){registry.Paths.Add(id);FileIntegrity.WriteXml(RegistryPath,registry);}
            return value;
        }}
        static void Seal(string path,byte[] bytes){string seal=SealPath(path);CloudStore.SecureDirectory(Path.GetDirectoryName(seal));FileIntegrity.WriteXml(seal,new ConfigurationSeal{Digest=Digest(bytes)});}
        internal static void Write<T>(string path,T value){using(new ProfileLease())lock(gate){
            PathSafety.RejectLinks(path);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));byte[] bytes=Serialize(value);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            // Do not turn tampered existing state into a new trusted baseline when saving.
            if(File.Exists(path))Read<T>(path);
            try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                string seal=SealPath(path);CloudStore.SecureDirectory(Path.GetDirectoryName(seal));
                FileIntegrity.WriteXml(seal,new ConfigurationSeal{Digest=File.Exists(seal)?FileIntegrity.ReadXml<ConfigurationSeal>(seal).Digest:null,PendingDigest=Digest(bytes)});
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);Seal(path,bytes);var registry=Registry();string id=Identity(path);if(!registry.Paths.Contains(id)){registry.Paths.Add(id);FileIntegrity.WriteXml(RegistryPath,registry);}}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }}
    }
}
