using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;
namespace FileCompass {
    public sealed class CloudAccount {
        public string Id=Guid.NewGuid().ToString("N"),Name="",Provider="drive",BridgePassword="",Fingerprint="";
        public int Port;public bool Ready;
        public string AuthorizationClient="";
        public override string ToString(){return Name+" · "+CloudStore.ProviderName(Provider);}
    }
    public sealed class CloudAccounts {
        public List<CloudAccount> Accounts=new List<CloudAccount>();
    }
    internal static partial class CloudStore {
        internal static string TestRoot;
        internal static string Root {get{return TestRoot??Path.Combine(AppPaths.DataDirectory,"cloud");}}
        internal static string KeyFile {get{return Path.Combine(Root,"key.dat");}}
        internal static string Rclone {get{return Path.Combine(AppPaths.Resource("plugins"),"cloud","rclone.exe");}}
        internal static string AccountPath(CloudAccount a){
            Guid id;if(!Guid.TryParseExact(a.Id,"N",out id))throw new IOException(L.Text("cloud.invalidAccount"));
            return Path.Combine(Root,a.Id);
        }
        internal static string Config(CloudAccount a){return Path.Combine(AccountPath(a),"rclone.conf");}
        internal static string ProviderName(string p){return p=="drive"?"Google Drive":p=="onedrive"?"OneDrive":p=="dropbox"?"Dropbox":p==CloudICloud.Provider?"iCloud Drive":p=="local"?"Local test":p;}
        internal static void SecureDirectory(string path){
            PathSafety.RejectLinks(path);
            Directory.CreateDirectory(path);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.SetAccessControl(path,acl);
        }
        internal static string Key {
            get {
                SecureDirectory(Root);
                if(!File.Exists(KeyFile)){
                    var bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
                    var protectedKey=ProtectedData.Protect(bytes,Encoding.UTF8.GetBytes("FileCompass.Cloud"),DataProtectionScope.CurrentUser);
                    try{using(var s=new FileStream(KeyFile,FileMode.CreateNew))s.Write(protectedKey,0,protectedKey.Length);}catch(IOException){if(!File.Exists(KeyFile))throw;}
                }
                return Convert.ToBase64String(ProtectedData.Unprotect(File.ReadAllBytes(KeyFile),Encoding.UTF8.GetBytes("FileCompass.Cloud"),DataProtectionScope.CurrentUser));
            }
        }
        internal static int PrintKey(string file){
            try{string full=Path.GetFullPath(file),root=Path.GetFullPath(Root).TrimEnd('\\')+"\\";PathSafety.RejectLinks(full);
                bool legacy=String.Equals(full,Path.GetFullPath(KeyFile),StringComparison.OrdinalIgnoreCase);
                Guid account;if(!legacy&&(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(full)!="encryption-key.dat"||!Guid.TryParseExact(Path.GetFileName(Path.GetDirectoryName(full)),"N",out account)||Path.GetDirectoryName(Path.GetDirectoryName(full))+"\\"!=root))return 1;
                using(var parent=ProcessIdentity.Parent()) {if(!String.Equals(parent.MainModule.FileName,Rclone,StringComparison.OrdinalIgnoreCase))return 1;using(PluginIntegrity.Acquire("cloud\\rclone.exe")){
                    var bytes=ProtectedData.Unprotect(File.ReadAllBytes(full),Encoding.UTF8.GetBytes("FileCompass.Cloud"),DataProtectionScope.CurrentUser);try{Console.WriteLine(Convert.ToBase64String(bytes));return 0;}finally{Array.Clear(bytes,0,bytes.Length);}}}
            }catch{return 1;}
        }
        internal static CloudAccounts Load(){
            string path=Path.Combine(Root,"accounts.xml");if(!File.Exists(path))return new CloudAccounts();
            return ConfigurationIntegrity.Read<CloudAccounts>(path);
        }
        internal static void Save(CloudAccounts accounts){
            SecureDirectory(Root);string path=Path.Combine(Root,"accounts.xml"),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{ConfigurationIntegrity.Write(path,accounts);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        internal static CloudAccount Find(string id){return Load().Accounts.FirstOrDefault(x=>x.Id==id)??throwMissing();}
        static CloudAccount throwMissing(){throw new IOException(L.Text("cloud.accountMissing"));}
        internal static string Quote(string text){
            if(text.IndexOf('\0')>=0)throw new ArgumentException();
            var b=new StringBuilder("\"");int slashes=0;
            foreach(char c in text){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
            b.Append('\\',slashes*2);b.Append('"');return b.ToString();
        }
        internal static ProcessStartInfo StartInfo(CloudAccount account,IEnumerable<string> args,bool browserFlow=false){
            if(!File.Exists(Rclone))throw new IOException(L.Text("cloud.pluginMissing")); using(var stream=File.OpenRead(Rclone))using(var sha=SHA256.Create())if(!BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").Equals(PluginHashes.Expected["cloud\\rclone.exe"],StringComparison.OrdinalIgnoreCase))throw new IOException(L.Text("cloud.pluginMissing"));
            var info=new ProcessStartInfo(Rclone,String.Join(" ",args.Concat(new[]{"--config",Config(account),"--log-level",browserFlow?"NOTICE":"ERROR"}).Select(Quote))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true};
            foreach(string variable in info.EnvironmentVariables.Keys.Cast<string>().Where(x=>x.StartsWith("RCLONE_",StringComparison.OrdinalIgnoreCase)).ToArray())info.EnvironmentVariables.Remove(variable);
            if(!args.Any(x=>x=="serve"))CloudApplicationOAuth.Apply(account,info);
            string keyFile=File.Exists(AccountKeyFile(account))?AccountKeyFile(account):KeyFile;
            info.Arguments+=" --password-command "+Quote(Quote(Process.GetCurrentProcess().MainModule.FileName)+" --cloud-config-key "+Quote(keyFile))+" --ask-password=false";
            if(browserFlow)info.EnvironmentVariables["RCLONE_AUTH_NO_OPEN_BROWSER"]="true";
            return info;
        }
        internal static async Task<string> Run(CloudAccount a,IEnumerable<string> args,CancellationToken token,int timeout=180000,Dictionary<string,string> environment=null,Action<string> openBrowser=null){
            token.ThrowIfCancellationRequested();var command=args.ToArray();var info=StartInfo(a,command,openBrowser!=null);if(environment!=null)foreach(var pair in environment)info.EnvironmentVariables[pair.Key]=pair.Value;
            using(PluginIntegrity.Acquire("cloud\\rclone.exe"))using(var job=new ArchiveProcessJob(512))using(var process=new Process{StartInfo=info}){
                process.Start();if(!(command.Length>1&&command[0]=="config"&&(command[1]=="create"||command[1]=="update")))job.Assign(process);process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync();var error=ReadError(process.StandardError,openBrowser);var clock=Stopwatch.StartNew();
                try{
                    while(!process.HasExited){token.ThrowIfCancellationRequested();if(clock.ElapsedMilliseconds>timeout)throw new IOException(L.Text("cloud.timeout"));await Task.Delay(50,token).ConfigureAwait(false);}
                    string result=await output.ConfigureAwait(false);await error.ConfigureAwait(false);
                    if(process.ExitCode!=0||result.Length>1048576)throw new IOException(L.Text("cloud.commandFailed"));
                    return result;
                }finally{if(!process.HasExited){try{process.Kill();process.WaitForExit(1000);}catch{}}}
            }
        }
        internal static async Task PersistPublisherIdentity(CloudAccount account){
            if(account.Provider!="drive"||account.AuthorizationClient!=CloudApplicationOAuth.FileCompass)return;
            var client=CloudApplicationOAuth.LoadGoogle();
            // RCLONE_JSON supplies the rc --json flag only to this short-lived configuration command.
            // The loopback rc mode invokes the API in-process; it opens no listening socket.
            string json=new JavaScriptSerializer().Serialize(new{name="remote",parameters=new{client_id=client.client_id,client_secret=client.client_secret},opt=new{nonInteractive=true,noObscure=true}});
            await Run(account,new[]{"rc","--loopback","config/update","--no-output"},CancellationToken.None,30000,new Dictionary<string,string>{{"RCLONE_JSON",json}}).ConfigureAwait(false);
            var diskOnly=new CloudAccount{Id=account.Id,Provider=account.Provider};
            string dump=await Run(diskOnly,new[]{"config","dump"},CancellationToken.None,30000).ConfigureAwait(false);
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,Dictionary<string,object>>>(dump);Dictionary<string,object> remote;object id,secret;
            if(data==null||!data.TryGetValue("remote",out remote)||!remote.TryGetValue("client_id",out id)||!remote.TryGetValue("client_secret",out secret)||Convert.ToString(id)!=client.client_id||Convert.ToString(secret)!=client.client_secret)throw new IOException("Google application identity was not persisted to encrypted cloud configuration. Sign in again.");
        }
        internal static async Task ReadError(StreamReader reader,Action<string> openBrowser){
            string line;var opened=new HashSet<string>();
            while((line=await reader.ReadLineAsync().ConfigureAwait(false))!=null){
                if(openBrowser==null)continue;
                var match=System.Text.RegularExpressions.Regex.Match(line,@"http://(?:127\.0\.0\.1|localhost):\d+/auth\?[^\s]+");
                if(match.Success&&opened.Add(match.Value))openBrowser(match.Value);
            }
        }
        internal static async Task Initialize(CloudAccount a,CancellationToken token){
            SecureDirectory(AccountPath(a));
            if(!File.Exists(Config(a))||new FileInfo(Config(a)).Length==0){
                CreateAccountKey(a);
                File.WriteAllText(Config(a),"",new UTF8Encoding(false));
                string command=Quote(Process.GetCurrentProcess().MainModule.FileName)+" --cloud-config-key "+Quote(AccountKeyFile(a));
                await Run(a,new[]{"config","encryption","set","--password-command",command},token,30000).ConfigureAwait(false);
            }
            if(!File.ReadLines(Config(a)).Take(4).Any(x=>x.Trim()=="RCLONE_ENCRYPT_V0:"))throw new IOException(L.Text("cloud.encryptionRequired"));
        }
        internal static async Task Authorize(CloudAccount a,Func<CloudQuestion,Task<string>> ask,CancellationToken token,Action<string> openBrowser=null){
            CloudApplicationOAuth.RequireReady(a.Provider);
            if(a.Provider=="drive")a.AuthorizationClient=CloudApplicationOAuth.FileCompass;
            await Initialize(a,token).ConfigureAwait(false);
            string output=a.Provider==CloudICloud.Provider?await CloudICloud.Begin(a,ask,token).ConfigureAwait(false):await Run(a,new[]{"config","create","remote",a.Provider,"config_is_local","true","--non-interactive"},token,180000,null,openBrowser).ConfigureAwait(false);
            for(int step=0;step<30;step++){
                var state=new JavaScriptSerializer{MaxJsonLength=1048576}.Deserialize<CloudQuestion>(output);
                if(state==null)throw new IOException(L.Text("cloud.commandFailed"));
                if(state.Option==null&&String.IsNullOrEmpty(state.State)){
                    await Run(a,new[]{"lsjson","remote:","--stat"},token,60000).ConfigureAwait(false);
                    a.Ready=true;return;
                }
                if(state.Option==null)throw new IOException(L.Text("cloud.commandFailed"));
                if(a.Provider==CloudICloud.Provider)CloudICloud.PrepareQuestion(state);
                CloudApplicationOAuth.PublisherAnswer(a,state.Option);
                string value;
                if(state.Option.Name=="config_is_local")value="true";
                else if(state.Option.Name=="config_refresh_token")value="true";
                else value=await ask(state).ConfigureAwait(false);
                if(value==null)throw new OperationCanceledException();
                output=await Run(a,new[]{"config","update","remote","--continue","--non-interactive"},token,180000,new Dictionary<string,string>{{"RCLONE_STATE",state.State??""},{"RCLONE_RESULT",value}},openBrowser).ConfigureAwait(false);
            }
            throw new IOException(L.Text("cloud.commandFailed"));
        }
    }
    internal sealed class CloudQuestion {
        public string Provider{get;set;}public string State{get;set;}public string Error{get;set;}public CloudOption Option{get;set;}
    }
    internal sealed class CloudOption {
        public string Name{get;set;}public string Help{get;set;}public object Default{get;set;}
        public bool Required{get;set;}public bool IsPassword{get;set;}public bool Exclusive{get;set;}
        public CloudExample[] Examples{get;set;}
    }
    internal sealed class CloudExample {
        public string Value{get;set;}public string Help{get;set;}
        public override string ToString(){return Help??Value;}
    }
    internal static class CloudKeys {
        internal static string ClientKey(CloudAccount a){return Path.Combine(CloudStore.AccountPath(a),"client-key.pem");}
        internal static void EnsureClient(CloudAccount a){
            string path=ClientKey(a),authorized=Path.Combine(CloudStore.AccountPath(a),"authorized_keys");
            CloudStore.SecureDirectory(CloudStore.AccountPath(a));PathSafety.RejectLinks(path);PathSafety.RejectLinks(authorized);
            if(File.Exists(path)&&File.Exists(authorized))return;
            using(var rsa=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;var p=rsa.ExportParameters(true);
                var der=Der(48,Join(Integer(new byte[]{0}),Integer(p.Modulus),Integer(p.Exponent),Integer(p.D),Integer(p.P),Integer(p.Q),Integer(p.DP),Integer(p.DQ),Integer(p.InverseQ)));
                try{File.WriteAllText(path,"-----BEGIN RSA PRIVATE KEY-----\n"+Convert.ToBase64String(der,Base64FormattingOptions.InsertLineBreaks)+"\n-----END RSA PRIVATE KEY-----\n");
                    byte[] exponent=p.Exponent[0]>=128?Join(new byte[]{0},p.Exponent):p.Exponent,modulus=p.Modulus[0]>=128?Join(new byte[]{0},p.Modulus):p.Modulus;
                    File.WriteAllText(authorized,"ssh-rsa "+Convert.ToBase64String(Join(String(Encoding.ASCII.GetBytes("ssh-rsa")),String(exponent),String(modulus)))) ;
                }finally{Array.Clear(der,0,der.Length);foreach(var bytes in new[]{p.D,p.P,p.Q,p.DP,p.DQ,p.InverseQ})if(bytes!=null)Array.Clear(bytes,0,bytes.Length);}
            }
        }
        static byte[] Join(params byte[][] parts){return parts.SelectMany(x=>x).ToArray();}
        static byte[] Der(byte tag,byte[] data){
            var size=new List<byte>();int n=data.Length;
            do{size.Insert(0,(byte)(n&255));n>>=8;}while(n>0);
            return Join(new[]{tag},data.Length<128?new[]{(byte)data.Length}:Join(new[]{(byte)(128+size.Count)},size.ToArray()),data);
        }
        static byte[] Integer(byte[] value){value=value.SkipWhile(x=>x==0).ToArray();if(value.Length==0)value=new byte[]{0};if(value[0]>=128)value=Join(new byte[]{0},value);return Der(2,value);}
        static byte[] String(byte[] value){int n=value.Length;return Join(new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n},value);}
        internal static void Create(CloudAccount a){
            string path=Path.Combine(CloudStore.AccountPath(a),"host-key.pem");
            if(File.Exists(path)&&!System.String.IsNullOrEmpty(a.Fingerprint))return;
            using(var rsa=new RSACryptoServiceProvider(2048)){
                rsa.PersistKeyInCsp=false;var p=rsa.ExportParameters(true);
                var der=Der(48,Join(Integer(new byte[]{0}),Integer(p.Modulus),Integer(p.Exponent),Integer(p.D),Integer(p.P),Integer(p.Q),Integer(p.DP),Integer(p.DQ),Integer(p.InverseQ)));
                File.WriteAllText(path,"-----BEGIN RSA PRIVATE KEY-----\n"+Convert.ToBase64String(der,Base64FormattingOptions.InsertLineBreaks)+"\n-----END RSA PRIVATE KEY-----\n");
                byte[] exponent=p.Exponent[0]>=128?Join(new byte[]{0},p.Exponent):p.Exponent,modulus=p.Modulus[0]>=128?Join(new byte[]{0},p.Modulus):p.Modulus;
                using(var sha=SHA256.Create())a.Fingerprint="ssh-rsa SHA256:"+Convert.ToBase64String(sha.ComputeHash(Join(String(Encoding.ASCII.GetBytes("ssh-rsa")),String(exponent),String(modulus)))).TrimEnd('=');
            }
        }
    }
    internal static class CloudRuntime {
        static readonly Dictionary<string,Process> processes=new Dictionary<string,Process>();
        static readonly Dictionary<string,ArchiveProcessJob> jobs=new Dictionary<string,ArchiveProcessJob>();
        static readonly object sync=new object();
        internal static int FreePort(){var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int p=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();return p;}
        static bool Listening(int port){try{using(var socket=new TcpClient()){var result=socket.BeginConnect(IPAddress.Loopback,port,null,null);if(!result.AsyncWaitHandle.WaitOne(150))return false;socket.EndConnect(result);return true;}}catch{return false;}}
        internal static void Ensure(string id){
            lock(sync){
                Process existing;if(processes.TryGetValue(id,out existing)&&!existing.HasExited)return;
                if(existing!=null)Stop(id);
                var account=CloudStore.Find(id);if(!account.Ready)throw new IOException(L.Text("cloud.signInRequired"));
                account.Port=FreePort(); // A persisted port or an unrelated listener never proves ownership.
                var accounts=CloudStore.Load();var saved=accounts.Accounts.FirstOrDefault(x=>x.Id==id);if(saved==null)throw new IOException(L.Text("cloud.accountMissing"));saved.Port=account.Port;CloudStore.Save(accounts);
                CloudStore.PersistPublisherIdentity(account).GetAwaiter().GetResult();
                CloudKeys.EnsureClient(account);
                var info=CloudStore.StartInfo(account,new[]{"serve","sftp","remote:","--addr","127.0.0.1:"+account.Port,"--key",Path.Combine(CloudStore.AccountPath(account),"host-key.pem"),"--user","compass","--authorized-keys",Path.Combine(CloudStore.AccountPath(account),"authorized_keys"),"--vfs-cache-mode","off","--dir-cache-time","5s"});
                var job=new ArchiveProcessJob(512);var process=new Process{StartInfo=info};
                try{
                    using(PluginIntegrity.Acquire("cloud\\rclone.exe"))process.Start();job.Assign(process);process.StandardInput.Close();
                    process.OutputDataReceived+=delegate{};process.ErrorDataReceived+=delegate{};process.BeginOutputReadLine();process.BeginErrorReadLine();
                    var clock=Stopwatch.StartNew();
                    while(!Listening(account.Port)){if(process.HasExited||clock.ElapsedMilliseconds>15000)throw new IOException(L.Text("cloud.connectionFailed"));Thread.Sleep(80);}
                    processes[id]=process;jobs[id]=job;
                }catch{try{if(!process.HasExited)process.Kill();}catch{}process.Dispose();job.Dispose();throw;}
            }
        }
        internal static NetworkProfile SavedProfile(string id){var a=CloudStore.Load().Accounts.FirstOrDefault(x=>x.Id==id);return a==null?null:Profile(a);} internal static NetworkProfile Profile(CloudAccount a){
            CloudKeys.EnsureClient(a);
            return new NetworkProfile{Id="cloud-"+a.Id,CloudAccountId=a.Id,CloudProvider=CloudStore.ProviderName(a.Provider),Name=a.Name,Protocol=NetworkProtocol.SFTP,Host="127.0.0.1",Port=a.Port,Path="/",UserName="compass",PrivateKeyPath=CloudKeys.ClientKey(a),EncryptedPassword="",HostKeyFingerprint=a.Fingerprint.StartsWith("SHA256:",StringComparison.Ordinal)?"ssh-rsa "+a.Fingerprint:a.Fingerprint,SshAgent=false,TimeoutSeconds=60};
        }
        internal static void ApplyEndpoint(NetworkProfile profile,FileCompass.NetworkTransport.SessionOptions options){Ensure(profile.CloudAccountId);profile.Port=CloudStore.Find(profile.CloudAccountId).Port;options.PortNumber=profile.Port;}
        internal static void Stop(string id){
            lock(sync){Process p;if(processes.TryGetValue(id,out p)){try{if(!p.HasExited){p.Kill();p.WaitForExit(3000);}}catch{}p.Dispose();processes.Remove(id);}ArchiveProcessJob job;if(jobs.TryGetValue(id,out job)){job.Dispose();jobs.Remove(id);}}
        }
        internal static void StopAll(){foreach(string id in processes.Keys.ToArray())Stop(id);}
    }
}
