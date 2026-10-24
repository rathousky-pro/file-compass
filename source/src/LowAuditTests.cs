using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
namespace FileCompass {
    internal static class LowAuditTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);int checks=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
            try {
                FileIntegrity.TestDirectory=Path.Combine(root,"integrity");
                foreach(char prefix in "=+-@\t\r\n")check(DiskAnalysisReports.Csv(prefix+"payload").StartsWith("\"'"),"CSV formula prefix neutralized");
                check(DiskAnalysisReports.Csv("ordinary\"name")=="\"ordinary\"\"name\"","CSV quotes preserved");
                string secret="migration-secret";
                string legacy=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret),null,DataProtectionScope.CurrentUser));
                string entropy=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes("CommanderCZ.connections.v2"),DataProtectionScope.CurrentUser));
                string path=Path.Combine(root,"connections.xml");
                ConfigurationIntegrity.Write(path,new NetworkSettings{Connections={new NetworkProfile{EncryptedPassword=legacy,EncryptedProxyPassword=entropy}}});
                var store=new NetworkSettingsStore(path);var p=store.Settings.Connections.Single();
                check(p.EncryptedPassword.StartsWith("dpapi-v3:")&&p.EncryptedProxyPassword.StartsWith("dpapi-v3:"),"both legacy formats migrated");
                check(NetworkSettingsStore.Unprotect(p.EncryptedPassword)==secret&&NetworkSettingsStore.Unprotect(p.EncryptedProxyPassword)==secret,"migrated passwords decrypt");
                check(new NetworkSettingsStore(path).Settings.Connections.Single().EncryptedPassword==p.EncryptedPassword,"migration persisted exactly once");
                foreach(string invalid in new[]{legacy,"dpapi-v3:"+legacy,"dpapi-v3:"+entropy}) {
                    bool rejected=false;try{NetworkSettingsStore.Unprotect(invalid);}catch(IOException){rejected=true;}check(rejected,"normal decryption cannot fall back to legacy formats");
                }
                string signed=Path.Combine(root,"state-a.xml"),other=Path.Combine(root,"state-b.xml");FileIntegrity.WriteXml(signed,new NetworkSettings());File.Copy(signed,other);
                check(!FileIntegrity.Verify(other),"signed state cannot be substituted under another name");
                string input=Path.Combine(root,"scan");Directory.CreateDirectory(input);File.WriteAllText(Path.Combine(input,"sample.txt"),"content");bool observed=false;
                var scan=DiskAnalysis.Scan(input,CancellationToken.None,null,null,new DiskScanOptions{StoreIndex=true,SnapshotIntervalMilliseconds=0},snapshot=>{
                    check(DiskFileIndex.Read(snapshot.IndexPath,CancellationToken.None).Any(),"live index remains readable");
                    foreach(bool delete in new[]{false,true}){bool rejected=false;try{if(delete)File.Delete(snapshot.IndexPath);else using(File.Open(snapshot.IndexPath,FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){} }catch(IOException){rejected=true;}check(rejected,"live index rejects other writers and deletion");}observed=true;
                });
                try{check(observed&&DiskFileIndex.Read(scan.IndexPath,CancellationToken.None).Count()==1,"completed index retains all rows");check(scan.IndexPath.StartsWith(Path.Combine(AppPaths.DataDirectory,"analyzer-index")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"index uses private application directory");}finally{DiskFileIndex.Delete(scan.IndexPath);}
                File.WriteAllText(Path.Combine(root,"result.txt"),"PASS: "+checks+" checks");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),"FAIL after "+checks+" checks: "+ex);return 1;}
        }
    }
}
