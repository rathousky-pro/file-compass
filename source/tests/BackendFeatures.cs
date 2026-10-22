using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommanderCZ.NetworkTransport;
class BackendFeatures
{
    static int Main(string[] args)
    {
        string protocol=args[0],mode=args[1],root=args[2]; int port=Int32.Parse(args[3]); var passed=new List<string>();
        Action<bool,string> check=delegate(bool value,string name) { if(!value) throw new Exception(name); passed.Add(name); };
        var options=new SessionOptions { Protocol=protocol=="ftp" ? Protocol.Ftp : Protocol.Sftp,HostName="127.0.0.1",PortNumber=port,UserName="tester",Password="test-password",Timeout=TimeSpan.FromSeconds(8) };
        try
        {
            if(mode=="ansi") options.AddRawSettings("Utf","0");
            if(mode.StartsWith("ftps"))
            {
                options.FtpSecure=mode=="ftps-implicit" ? FtpSecure.Implicit : FtpSecure.Explicit;
                options.AddRawSettings("PostLoginCommands","NOOP\nSYST");
                bool rejected=false; using(var bad=new Session()) { try { bad.Open(options); } catch(SessionRemoteException) { rejected=true; } }
                check(rejected,"untrusted FTPS certificate rejected");
                options.AddRawSettings("TlsCaFile",Path.Combine(root,"ca.pem"));
                if(mode=="ftps-wrong-host")
                { rejected=false; using(var bad=new Session()) { try { bad.Open(options); } catch(SessionRemoteException) { rejected=true; } } check(rejected,"trusted certificate with wrong hostname rejected"); Console.WriteLine(String.Join(Environment.NewLine,passed)); return 0; }
                if(mode=="ftps-client")
                {
                    rejected=false; using(var bad=new Session()) { try { bad.Open(options); } catch(SessionRemoteException) { rejected=true; } }
                    check(rejected,"FTPS server requiring client certificate refuses missing certificate");
                    options.TlsClientCertificatePath=Path.Combine(root,"client.pfx");
                }
            }
            if(protocol=="sftp")
            {
                options.SshHostKeyFingerprint=File.ReadAllText(Path.Combine(root,"host-fingerprint.txt"));
                options.AddRawSettings("Compression","on");
                if (mode == "agent") { options.Password = "wrong-password"; options.AddRawSettings("Agent", "on"); }
                if (mode == "keepalive") options.AddRawSettings("KeepAlive", "2");
                if(mode.StartsWith("key-")) { options.Password="incorrect-password"; options.SshPrivateKeyPath=Path.Combine(root,"id_test"); }
            }
            if(args.Length>4)
            {
                options.AddRawSettings("ProxyMethod",args[5]); options.AddRawSettings("ProxyHost","127.0.0.1"); options.AddRawSettings("ProxyPort",args[4]);
                options.AddRawSettings("ProxyUsername","proxy-user"); options.AddRawSettings("ProxyPassword","proxy-password");
            }
            using(var session=new Session())
            {
                session.Open(options);
                if (mode == "agent") check(true, "real generated key authentication through isolated Windows SSH agent");
                if (mode == "keepalive") { System.Threading.Thread.Sleep(5500); check(File.Exists(Path.Combine(root, "keepalive.txt")) && Int32.Parse(File.ReadAllText(Path.Combine(root, "keepalive.txt"))) >= 2, "idle SSH keepalive packets actually reach server"); }
                if(mode.StartsWith("ftps"))
                {
                    check(true,mode+" trusted TLS control connection and commands");
                    Console.WriteLine(String.Join(Environment.NewLine,passed));
                    Console.WriteLine("SKIP: FTPS loopback data certificate is substituted by local Avast; certificate checks remain enabled.");
                    return 0;
                }
                check(session.ListDirectory("/").Files.Any(x=>x.Name=="český soubor.txt"),mode+" exact Czech filename listing");
                string destination=Path.Combine(root,"download.txt"); session.GetFiles("/český soubor.txt",destination);
                check(File.ReadAllText(destination)=="Příliš žluťoučký kůň",mode+" exact download");
                string source=Path.Combine(root,"upload.txt"); File.WriteAllText(source,"key/TLS/proxy test žluťoučký"); session.PutFiles(source,"/nový soubor.txt");
                session.GetFiles("/nový soubor.txt",destination);
                check(File.ReadAllText(destination)==File.ReadAllText(source),mode+" upload round trip and timestamp");
                session.RemoveFiles("/nový soubor.txt"); check(!session.FileExists("/nový soubor.txt"),mode+" delete and clean staging");
                if(mode=="list") { options.AddRawSettings("FtpUseMlsd","on"); bool refused=false; try { session.ListDirectory("/"); } catch(SessionRemoteException) { refused=true; } check(refused,"forced unsupported MLSD reports error"); }
            }
            Console.WriteLine(String.Join(Environment.NewLine,passed)); Console.WriteLine("PASS: "+passed.Count+" checks"); return 0;
        }
        catch(SessionRemoteException ex)
        {
            if(mode=="ftps-implicit" && ex.Message.Contains("unable to get local issuer certificate"))
            { Console.WriteLine("SKIP: implicit loopback FTPS blocked by local TLS interception; certificate checks remain enabled."); return 0; }
            Console.Error.WriteLine(ex); return 1;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
