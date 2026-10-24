using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
namespace FileCompass {
    internal static class MediumAuditTests {
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add(label);};
            Action<Action,string> reject=(action,label)=>{bool failed=false;try{action();}catch(IOException){failed=true;}check(failed,label);};
            try{
                FileIntegrity.TestDirectory=Path.Combine(root,"integrity");
                string config=Path.Combine(root,"legacy-settings.xml");using(var file=File.Create(config))new XmlSerializer(typeof(ProgramSettings)).Serialize(file,new ProgramSettings{TextEditorPath="legacy-editor",SingleInstance=true});
                check(ProgramSettings.Load(config).SingleInstance,"legacy XML is migrated without losing settings");
                check(File.ReadAllText(config).Contains("legacy-editor"),"signed configuration retains readable portable XML");
                var settings=ProgramSettings.Load(config);settings.TextEditorPath="new-editor";settings.Save(config);check(ProgramSettings.Load(config).TextEditorPath=="new-editor","signed configuration supports subsequent atomic saves");
                byte[] trusted=File.ReadAllBytes(config);File.WriteAllText(config,File.ReadAllText(config).Replace("new-editor","injected-editor"));
                reject(()=>ProgramSettings.Load(config),"changed editor configuration fails closed");reject(()=>settings.Save(config),"save cannot legitimize a tampered configuration");File.WriteAllBytes(config,trusted);
                foreach(string signature in Directory.GetFiles(Path.Combine(FileIntegrity.TestDirectory,"configuration-seals"),"*.xml")){string backup=signature+".test-backup";File.Move(signature,backup);reject(()=>ProgramSettings.Load(config),"removing a migrated signature does not reenable legacy import");File.Move(backup,signature);}
                check(ProgramSettings.Load(config).TextEditorPath=="new-editor","restored valid signature is accepted");
                string concurrent=Path.Combine(root,"concurrent-settings.xml");System.Threading.Tasks.Parallel.For(0,8,i=>{new ProgramSettings{TextEditorPath="editor-"+i}.Save(concurrent);ProgramSettings.Load(concurrent);});check(ProgramSettings.Load(concurrent).TextEditorPath.StartsWith("editor-"),"concurrent configuration saves retain a matching authenticated snapshot");
                string connections=Path.Combine(root,"connections.xml");var store=new NetworkSettingsStore(connections);var profile=new NetworkProfile{Protocol=NetworkProtocol.SFTP,Name="SSH",Host="server.example",UserName="user",HostKeyFingerprint="ssh-ed25519 "+Convert.ToBase64String(new byte[32])};store.Put(profile);
                check(new NetworkSettingsStore(connections).Settings.Connections.Single().HostKeyFingerprint.Contains(" SHA256:"),"legacy algorithm plus SHA-256 hash is normalized on save");
                File.AppendAllText(connections," ");reject(()=>new NetworkSettingsStore(connections),"network profiles reject modified exact XML bytes");
                reject(()=>SshHostPin.Normalize("SHA256:"+Convert.ToBase64String(new byte[32])),"algorithm-free SSH fingerprints are rejected");
                reject(()=>SshHostPin.Normalize("ssh-rsa MD5:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd:ee:ff"),"MD5 SSH fingerprints are rejected");
                reject(()=>SshHostPin.Normalize("ssh-rsa SHA256:AAAA"),"incorrect SHA-256 digest length is rejected");
                check(!FileViewers.UsesAssociatedEditor("drawing.svg")&&!FileViewers.UsesAssociatedEditor("drawing.svgz"),"SVG edit always treats the file as text");
                string quoted=SshTerminal.QuoteWindowsArgument("C:\\");check(quoted=="\"C:\\\\\"","quoted Windows directory roots retain trailing slash");
                IntPtr secret=Marshal.AllocHGlobal(48);try{Marshal.Copy(Enumerable.Repeat((byte)123,48).ToArray(),0,secret,48);SensitiveMemory.Clear(secret,48);var bytes=new byte[48];Marshal.Copy(secret,bytes,0,48);check(bytes.All(x=>x==0),"unmanaged sensitive buffers are overwritten before release");}finally{Marshal.FreeHGlobal(secret);}
                string tree=Path.Combine(root,"tree");Directory.CreateDirectory(Path.Combine(tree,"nested"));File.WriteAllText(Path.Combine(tree,"nested","file.txt"),"fixture");DeleteOperations.Local(tree,true);check(!Directory.Exists(tree),"permanent deletion removes normal nested files using guarded recursion");
                reject(()=>PathSafety.DeleteTree(Path.GetPathRoot(root)),"guarded deletion rejects drive roots");
                using(var temp=new ArchiveTemp(root)){check(Directory.Exists(temp.Path),"private guarded archive workspace can be created and removed inside its owned parent");}
                var account=new CloudAccount{Name="fixture",Provider="local",Fingerprint="SHA256:"+Convert.ToBase64String(new byte[32])};CloudStore.TestRoot=Path.Combine(root,"cloud");CloudKeys.EnsureClient(account);
                check(File.Exists(CloudKeys.ClientKey(account))&&File.ReadAllText(Path.Combine(CloudStore.AccountPath(account),"authorized_keys")).StartsWith("ssh-rsa "),"cloud bridge generates a separate client key and authorized public key");
                var cloudProfile=CloudRuntime.Profile(account);check(cloudProfile.EncryptedPassword==""&&cloudProfile.PrivateKeyPath==CloudKeys.ClientKey(account)&&cloudProfile.HostKeyFingerprint.StartsWith("ssh-rsa SHA256:"),"legacy cloud pins migrate without password-based bridge authentication");
                var info=CloudStore.StartInfo(account,new[]{"serve","sftp","remote:"});check(!info.EnvironmentVariables.ContainsKey("RCLONE_CONFIG_PASS")&&!info.EnvironmentVariables.ContainsKey("RCLONE_PASS")&&!info.EnvironmentVariables.ContainsKey("RCLONE_CONFIG_REMOTE_CLIENT_SECRET")&&info.Arguments.Contains("--password-command"),"long-lived cloud process receives no config, bridge or OAuth password in its environment");
                check(CloudStore.PrintKey(Path.Combine(root,"outside-key.dat"))==1,"decryption helper rejects arbitrary key locations and unauthorized callers");
                check(InstanceCoordinator.ValidateFolder(@"\\server\share")==@"\\server\share","authenticated instance forwarding retains support for UNC folders pending UI consent");
                string viewer=File.ReadAllText(Path.Combine(AppPaths.Resource("preview"),"viewer.js"));check(viewer.Contains("XLSX.version!==\"0.20.3\"")&&!viewer.Contains("sheet_to_html")&&viewer.Contains("td.textContent"),"spreadsheet preview blocks vulnerable parser versions and renders cells as text");
                string docx=File.ReadAllText(Path.Combine(AppPaths.Resource("preview"),"docx-preview.min.js"));check(!docx.Contains("t.innerHTML=`&#x${e.char};`")&&docx.Contains("{textContent:e}"),"DOCX symbols and stylesheet contents cannot inject markup");
                check(PluginHashes.Expected.ContainsKey("smart\\smartctl.exe")&&PluginHashes.Expected.Keys.Any(x=>x.StartsWith("libraries\\"))&&PluginHashes.Expected.Keys.Any(x=>x.StartsWith("preview\\")),"compiled hashes cover SMART, managed libraries and preview scripts");
                reject(()=>File.AppendAllText(Path.Combine(AppPaths.Resource("preview"),"viewer.js"),"tamper"),"verified preview dependencies remain locked for the entire process lifetime");
                string external=Path.Combine(root,"external-tool.exe");File.WriteAllText(external,"unsigned fixture");
                reject(()=>{using(ToolTrust.Acquire(external)){}},"unsigned external executable requires explicit approval");
                ToolTrust.Approve(external);using(ToolTrust.Acquire(external)){reject(()=>File.WriteAllText(external,"changed fixture"),"approved executable remains locked during process launch");}
                File.WriteAllText(external,"changed fixture");reject(()=>{using(ToolTrust.Acquire(external)){}},"changed external executable invalidates its saved approval");
                File.WriteAllLines(Path.Combine(root,"medium-audit-result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"medium-audit-result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}
        }
    }
}
