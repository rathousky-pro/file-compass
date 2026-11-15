using System;
using System.IO;
using System.Linq;
using FileCompass.NetworkTransport;

namespace FileCompass {
 internal static class NfsSmokeTests {
    internal static int Run(string[] args) {
      int i=Array.IndexOf(args,"--nfs-smoke-test");
      if(i<0 || args.Length<i+5) return 2;
      string file=Path.Combine(args[i+1],"nfs-smoke-result.txt");
      try {
        var profile=new NetworkProfile{ Name="NFS read-only smoke",Protocol=NetworkProtocol.NFS,Host=args[i+2],Path=args[i+3],NfsMapDrive=false,NfsVersion=Int32.Parse(args[i+4]) };
        profile.Validate();
        using(var connection=new RemoteConnection(profile)) {
            var options=connection.Options("");
            connection.Open(options);
            var result=connection.Session.ListDirectory("/");
            if(args.Length>i+5 && args[i+5]=="--preview"){
                var sample=result.Files.FirstOrDefault(x=>!x.IsDirectory && x.FileType!='L' && x.Name.EndsWith(".txt",StringComparison.OrdinalIgnoreCase));
                if(sample==null)throw new IOException("No TXT available in NFS test export.");
                var preview=RemotePreview.Load(connection,sample.FullName,System.Text.Encoding.UTF8,System.Threading.CancellationToken.None);
                if(preview.Text==null || preview.ReadBytes!=Math.Min(sample.Length,131072))throw new IOException("NFS preview did not load expected text content.");
                using(var transfer=connection.TransferConnection()){
                    transfer.OpenTransfer();
                    if(sample.Length>1){
                        byte[] first=transfer.Session.ReadSlice(sample.FullName,0,1);
                        byte[] second=transfer.Session.ReadSlice(sample.FullName,1,1);
                        byte[] both=transfer.Session.ReadSlice(sample.FullName,0,2);
                        if(first.Length!=1||second.Length!=1||both.Length!=2||first[0]!=both[0]||second[0]!=both[1])throw new IOException("NFS offset preview mismatch.");
                    }
                }
                File.WriteAllText(file,"OK NFSv"+profile.NfsVersion+" preview: "+sample.Name+" bytes="+preview.ReadBytes);
            }
            else if(args.Length>i+5 && args[i+5]=="--rw"){
                string remote="/.filecompass-nfs-smoke-"+Guid.NewGuid().ToString("N");
                string local=Path.Combine(args[i+1],"nfs-source.txt");
                string downloaded=Path.Combine(args[i+1],"nfs-downloaded.txt");
                try{
                    connection.Session.CreateDirectory(remote);
                    File.WriteAllText(local,"File Compass NFS smoke: initial payload");
                    connection.Session.PutFiles(local,remote+"/test.txt").Check();
                    connection.Session.GetFiles(remote+"/test.txt",downloaded).Check();
                    if(File.ReadAllText(local)!=File.ReadAllText(downloaded))throw new IOException("NFS download mismatch");
                    byte[] newData=System.Text.Encoding.UTF8.GetBytes("File Compass NFS editor save test");
                    var expected=connection.Session.GetFileInfo(remote+"/test.txt");
                    using(var input=new MemoryStream(newData)){
                        connection.Session.UploadRelay(input,input.Length,DateTime.Now,remote+"/test.txt",expected,delegate{},delegate{},delegate{});
                    }
                    connection.Session.GetFiles(remote+"/test.txt",downloaded).Check();
                    if(File.ReadAllText(downloaded)!="File Compass NFS editor save test")throw new IOException("NFS edited content differs");
                    using(var document=new RemoteEditDocument(connection,remote+"/test.txt",args[i+1])){
                        document.Download(System.Threading.CancellationToken.None);
                        if(File.ReadAllText(document.LocalPath)!="File Compass NFS editor save test")throw new IOException("NFS editor could not download");
                        File.WriteAllText(document.LocalPath,"File Compass editor saved over NFS");
                        document.Save(System.Threading.CancellationToken.None);
                    }
                    connection.Session.GetFiles(remote+"/test.txt",downloaded).Check();
                    if(File.ReadAllText(downloaded)!="File Compass editor saved over NFS")throw new IOException("NFS editor save mismatch");
                    // F6 move NFS -> Windows: verify complete target and identical metadata, then remove source.
                    {
                        string moveSource=remote+"/move-source.txt",moveTarget=Path.Combine(args[i+1],"nfs-moved.txt");
                        File.WriteAllText(local,"NFS verified-move test content");
                        connection.Session.PutFiles(local,moveSource).Check();
                        var original=connection.Session.GetFileInfo(moveSource);
                        connection.Session.GetFiles(moveSource,moveTarget).Check();
                        if(!File.Exists(moveTarget) || File.ReadAllText(moveTarget)!="NFS verified-move test content")throw new IOException("Move target data mismatch");
                        if(new FileInfo(moveTarget).Length!=original.Length || File.GetLastWriteTime(moveTarget)!=original.LastWriteTime)
                            throw new IOException("Move destination metadata mismatch "+File.GetLastWriteTime(moveTarget).ToString("O")+" versus "+original.LastWriteTime.ToString("O"));
                        connection.Session.RemoveVerifiedFile(moveSource,original.Length,original.LastWriteTime);
                        if(connection.Session.FileExists(moveSource))throw new IOException("NFS source remained after move");
                    }
                    if(args.Length>i+6 && args[i+6]=="--queue-move"){
                        string special="!!!recept na vánočku.txt";
                        string source=remote+"/"+special;
                        string outputDir=Path.Combine(args[i+1],"move-queue");Directory.CreateDirectory(outputDir);
                        File.WriteAllText(local,"NFS queued verified move of accented filename");
                        connection.Session.PutFiles(local,source).Check();
                        string output=Path.Combine(outputDir,special);
                        File.WriteAllText(output,"previous local file");
                        foreach(var rule in new[]{NetworkCopyRule.Skip,NetworkCopyRule.Overwrite}){
                            var job=new NetworkTransferJob(connection.Profile,true){Move=true};
                            job.Reserve();
                            using(var planner=connection.TransferConnection())
                                job.AddPrepared(NetworkCopyWorker.Prepare(planner,new[]{new Entry{FullPath=source}},true,outputDir,rule,job.Cancellation.Token));
                            job.PlanningDone(null,"",outputDir);
                            NetworkCopyResult moveResult;
                            using(var worker=connection.TransferConnection())moveResult=NetworkCopyWorker.Run(job,worker);
                            if(rule==NetworkCopyRule.Skip){
                                if(moveResult.Skipped!=1||!connection.Session.FileExists(source)||File.ReadAllText(output)!="previous local file")
                                  throw new IOException("Move Skip should keep both source and existing target unchanged");
                            }else if(moveResult.Copied!=1)throw new IOException("Move overwrite not counted successful");
                        }
                        if(!File.Exists(output))throw new IOException("Move queue did not create target");
                        if(connection.Session.FileExists(source))throw new IOException("Move queue left NFS source behind");
                        if(File.ReadAllText(output)!="NFS queued verified move of accented filename")throw new IOException("Move queue copied bad content");
                    }
                    File.WriteAllText(file,"OK NFSv"+profile.NfsVersion+" read/write/remote-editor save"+"/verified move"+"; entries="+result.Files.Count);
                } finally {
                    try{if(connection.Session.FileExists(remote+"/!!!recept na vánočku.txt"))connection.Session.RemoveFiles(remote+"/!!!recept na vánočku.txt");}catch{}
                    try{if(connection.Session.FileExists(remote+"/move-source.txt"))connection.Session.RemoveFiles(remote+"/move-source.txt");}catch{}
                    try{if(connection.Session.FileExists(remote+"/test.txt"))connection.Session.RemoveFiles(remote+"/test.txt");}catch{}
                    try{if(connection.Session.FileExists(remote))connection.Session.RemoveFiles(remote);}catch{}
                }
            }else File.WriteAllText(file,"OK version="+profile.NfsVersion+" entries="+result.Files.Count);
        }
        return 0;
      } catch(Exception ex) {File.WriteAllText(file,ex.ToString());return 1;}
    }
 }
}
