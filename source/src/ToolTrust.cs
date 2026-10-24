using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace FileCompass {
    public sealed class TrustedToolRecord {public string Path,Hash;}
    public sealed class TrustedToolList {public List<TrustedToolRecord> Tools=new List<TrustedToolRecord>();}
    internal static class ToolTrust {
        static string Store{get{return Path.Combine(AppPaths.ConfigDirectory,"trusted-tools.xml");}}
        static string Hash(Stream file){using(var sha=SHA256.Create()){var hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");file.Position=0;return hash;}}
        internal static void Approve(string path){path=Path.GetFullPath(path);PathSafety.RejectLinks(path);using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){var list=File.Exists(Store)?ConfigurationIntegrity.Read<TrustedToolList>(Store):new TrustedToolList();list.Tools.RemoveAll(x=>String.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase));list.Tools.Add(new TrustedToolRecord{Path=path,Hash=Hash(file)});ConfigurationIntegrity.Write(Store,list);}}
        internal static IDisposable Acquire(string path){
            path=Path.GetFullPath(path);PathSafety.RejectLinks(path);var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            try{
                string plugins=Path.GetFullPath(AppPaths.Resource("plugins")).TrimEnd('\\')+"\\";
                string expected;if(path.StartsWith(plugins,StringComparison.OrdinalIgnoreCase)&&PluginHashes.Expected.TryGetValue(path.Substring(plugins.Length),out expected)){if(!String.Equals(Hash(file),expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Bundled executable integrity check failed: "+path);return file;}
                try{SshfsSignature.Verify(path);return file;}catch(IOException){}
                var list=File.Exists(Store)?ConfigurationIntegrity.Read<TrustedToolList>(Store):new TrustedToolList();string actual=Hash(file);
                if(list.Tools.Any(x=>String.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase)&&String.Equals(x.Hash,actual,StringComparison.OrdinalIgnoreCase)))return file;
                throw new IOException("Executable is unverified or changed. Select it and save it again in application settings: "+path);
            }catch{file.Dispose();throw;}
        }
        internal static IDisposable AcquireSshfs(string executable){
            var lease=new Lease();try{foreach(var path in Directory.GetFiles(Path.GetDirectoryName(executable)).Where(x=>new[]{".exe",".dll"}.Contains(Path.GetExtension(x).ToLowerInvariant())))lease.Files.Add(Acquire(path));return lease;}catch{lease.Dispose();throw;}
        }
        internal static void ApproveSshfs(string executable){foreach(var path in Directory.GetFiles(Path.GetDirectoryName(executable)).Where(x=>new[]{".exe",".dll"}.Contains(Path.GetExtension(x).ToLowerInvariant())))Approve(path);}
        sealed class Lease:IDisposable{internal List<IDisposable> Files=new List<IDisposable>();public void Dispose(){foreach(var file in Files)file.Dispose();}}
    }
}
