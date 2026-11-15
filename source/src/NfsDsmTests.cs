using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileCompass.NetworkTransport;
namespace FileCompass {
 internal static class NfsDsmTests {
  internal static int Run(string[] args) {
   int i=Array.IndexOf(args,"--nfs-dsm-test"); if(i<0||args.Length<i+4)return 2;
   string root=args[i+1], report=Path.Combine(root,"nfs-dsm-test.txt"), remote="/.filecompass-nfs-test-"+Guid.NewGuid().ToString("N");
   string local=Path.Combine(root,"payload.bin"),download=Path.Combine(root,"download.bin");
   if(args.Length>i+4 && args[i+4].StartsWith("/.filecompass-nfs-test-",StringComparison.Ordinal))remote=args[i+4];
   bool cleanupOnly=args.Length>i+4;
   Directory.CreateDirectory(root); var log=new System.Collections.Generic.List<string>(); bool made=false;
   var profile=new NetworkProfile{Name="NFS experimental test",Protocol=NetworkProtocol.NFS,Host=args[i+2],Path=args[i+3],NfsMapDrive=false,NfsVersion=4};
   Action<string> pass=x=>{log.Add("PASS: "+x);File.WriteAllLines(report,log);};
   try {
    profile.Validate();
    if(cleanupOnly){made=true;} else using(var c=new RemoteConnection(profile)) {
     c.Open(c.Options("")); c.Session.CreateDirectory(remote); made=true; pass("connect and create isolated directory");
     var data=new byte[3*1024*1024+37];new Random(124).NextBytes(data);
     File.WriteAllBytes(local,data);
     c.Session.PutFiles(local,remote+"/data.bin").Check();c.Session.GetFiles(remote+"/data.bin",download).Check();
     if(!data.SequenceEqual(File.ReadAllBytes(download)))throw new IOException("Binary roundtrip mismatch"); pass("3 MiB binary upload/download byte comparison");
     byte[] slice=c.Session.ReadSlice(remote+"/data.bin",65530,40);if(!slice.SequenceEqual(data.Skip(65530).Take(40)))throw new IOException("Offset read mismatch");pass("offset read across buffer boundary");
     string empty=Path.Combine(root,"empty.txt");File.WriteAllBytes(empty,new byte[0]);c.Session.PutFiles(empty,remote+"/prázdný soubor.txt").Check();
     c.Session.MoveFile(remote+"/prázdný soubor.txt",remote+"/přejmenováno.txt");
     if(c.Session.GetFileInfo(remote+"/přejmenováno.txt").Length!=0)throw new IOException("Empty file mismatch");pass("empty file, Unicode name and rename");
     c.Session.CreateDirectory(remote+"/složka");c.Session.MoveFile(remote+"/přejmenováno.txt",remote+"/složka/přejmenováno.txt");
     c.Session.RemoveFiles(remote+"/složka/přejmenováno.txt");c.Session.RemoveFiles(remote+"/složka");pass("nested directory, move and delete");
     var tasks=Enumerable.Range(0,3).Select(n=>Task.Run(delegate {using(var parallel=new RemoteConnection(profile)){parallel.Open(parallel.Options(""));var path=remote+"/parallel-"+n+".bin";parallel.Session.PutFiles(local,path).Check();var info=parallel.Session.GetFileInfo(path);if(info.Length!=data.Length)throw new IOException("Parallel size mismatch");parallel.Session.RemoveFiles(path);}})).ToArray();Task.WaitAll(tasks);pass("three concurrent independent connections upload/stat/delete");
    }
    if(!cleanupOnly)using(var reconnect=new RemoteConnection(profile)){reconnect.Open(reconnect.Options(""));reconnect.Session.GetFiles(remote+"/data.bin",download).Check();if(!dataCheck(local,download))throw new IOException("Reconnect data mismatch");pass("reconnect and read persisted bytes");}
   } catch(Exception ex){log.Add("FAIL: "+ex);}
   finally {if(made)try {using(var cleanup=new RemoteConnection(profile)){cleanup.Open(cleanup.Options(""));var entries=cleanup.Session.ListDirectory(remote).Files;
     var folder=entries.FirstOrDefault(x=>x.Name=="složka" && x.IsDirectory);
     if(folder!=null){var nested=cleanup.Session.ListDirectory(folder.FullName).Files;foreach(var item in nested.Where(x=>x.Name=="přejmenováno.txt" && !x.IsDirectory))cleanup.Session.RemoveFiles(item.FullName);NfsNative.Action(profile.Host,profile.Path,4,4,folder.FullName);}
     foreach(var item in entries.Where(x=>!x.IsDirectory && new[]{"data.bin","prázdný soubor.txt","přejmenováno.txt","parallel-0.bin","parallel-1.bin","parallel-2.bin"}.Contains(x.Name)))cleanup.Session.RemoveFiles(item.FullName);
     NfsNative.Action(profile.Host,profile.Path,4,4,remote);log.Add("PASS: test directory removed");}}catch(Exception ex){log.Add("CLEANUP FAILED: "+remote+" "+ex.Message);} File.WriteAllLines(report,log);}
   return log.Any(x=>x.StartsWith("FAIL:")||x.StartsWith("CLEANUP FAILED:"))?1:0;
  }
  static bool dataCheck(string a,string b){return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));}
 }
}
