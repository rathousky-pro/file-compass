using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
namespace FileCompass {
    internal static class RuntimeIntegrity {
        internal static IDisposable Acquire(){var lease=new Lease();try{
            foreach(var entry in PluginHashes.Expected.Where(x=>x.Key.StartsWith("libraries\\",StringComparison.Ordinal)||x.Key.StartsWith("preview\\",StringComparison.Ordinal))){
                int slash=entry.Key.IndexOf('\\');string path=Path.Combine(AppPaths.Resource(entry.Key.Substring(0,slash)),entry.Key.Substring(slash+1));PathSafety.RejectLinks(path);
                var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);lease.Files.Add(file);using(var sha=SHA256.Create())if(!String.Equals(BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""),entry.Value,StringComparison.OrdinalIgnoreCase))throw new IOException("Application dependency integrity check failed: "+entry.Key);
            }return lease;
        }catch{lease.Dispose();throw;}}
        sealed class Lease:IDisposable{internal List<FileStream> Files=new List<FileStream>();public void Dispose(){foreach(var file in Files)file.Dispose();}}
    }
}
