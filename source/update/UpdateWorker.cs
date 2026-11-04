using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace FileCompass.Updating {
 internal static class UpdateWorker {
  static void Replace(string source,string target){string temp=target+".update-"+Guid.NewGuid().ToString("N");try{Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,temp);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
  static void Retry(Action action){var watch=Stopwatch.StartNew();while(true){try{action();return;}catch(IOException){if(watch.ElapsedMilliseconds>15000)throw;}Thread.Sleep(150);}}
  static bool Alive(InstallRequest request){try{using(var parent=Process.GetProcessById(request.ParentId))return !parent.HasExited&&parent.StartTime.ToUniversalTime().Ticks==request.ParentStarted;}catch(ArgumentException){return false;}}
  internal static void Recover(InstallRequest request,string job){
   var written=UpdateContract.Read<List<RestoreFile>>(Path.Combine(job,"journal.json"));if(written==null||written.Count>20001)throw new IOException("Invalid rollback journal.");string backup=Path.Combine(job,"backup");
   foreach(var item in written){if(item==null||!UpdateContract.AllowedFile(item.Relative??""))throw new IOException("Invalid rollback file.");if(item.Existed&&UpdateContract.Hash(UpdateContract.SafePath(backup,item.Relative))!=item.Hash)throw new IOException("Backup verification failed: "+item.Relative);}
   foreach(var item in written.AsEnumerable().Reverse()){string target=UpdateContract.SafePath(request.Target,item.Relative);if(item.Existed)Retry(()=>Replace(UpdateContract.SafePath(backup,item.Relative),target));else if(File.Exists(target))File.Delete(target);}File.WriteAllText(Path.Combine(job,"result.txt"),"Previous application restored.");
  }
  internal static void Install(InstallRequest request,string job,Action<int> afterWrite=null){
   UpdateContract.Verify(request.Package,request.Release);if(UpdateContract.Hash(Path.Combine(request.Package,UpdateContract.ManifestName))!=request.ManifestHash)throw new IOException("Prepared manifest changed.");UpdateContract.RejectLinks(request.Target);string original=UpdateContract.SafePath(request.Target,UpdateContract.Executable);if(UpdateContract.Hash(original)!=request.OriginalHash)throw new IOException("Installed application changed before the update.");
   string backup=Path.Combine(job,"backup");Directory.CreateDirectory(backup);var restore=new List<RestoreFile>();var files=request.Release.Files.Select(x=>x.Relative).Concat(new[]{UpdateContract.ManifestName}).ToArray();
   // Back up everything before replacing the first installed file.
   foreach(string relative in files){string target=UpdateContract.SafePath(request.Target,relative);var item=new RestoreFile{Relative=relative,Existed=File.Exists(target)};if(item.Existed){string saved=UpdateContract.SafePath(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(saved));Retry(()=>File.Copy(target,saved,false));item.Hash=UpdateContract.Hash(saved);}restore.Add(item);}
   UpdateContract.Write(Path.Combine(job,"restore.json"),restore);
   var written=new List<RestoreFile>();try{
    foreach(var item in restore){string target=UpdateContract.SafePath(request.Target,item.Relative);if(item.Existed&&UpdateContract.Hash(target)!=item.Hash||!item.Existed&&File.Exists(target))throw new IOException("Installation changed during the update: "+item.Relative);written.Add(item);UpdateContract.Write(Path.Combine(job,"journal.json"),written);Retry(()=>Replace(UpdateContract.SafePath(request.Package,item.Relative),target));if(afterWrite!=null)afterWrite(written.Count);}
    UpdateContract.Verify(request.Target,request.Release);File.WriteAllText(Path.Combine(job,"result.txt"),"Installed "+UpdateContract.Information(request.Release));
   }catch{
    Exception rollbackError=null;foreach(var item in written.AsEnumerable().Reverse()){try{string target=UpdateContract.SafePath(request.Target,item.Relative);if(item.Existed)Retry(()=>Replace(UpdateContract.SafePath(backup,item.Relative),target));else if(File.Exists(target))File.Delete(target);}catch(Exception ex){rollbackError=ex;}}
    if(rollbackError!=null)throw new IOException("Update failed and rollback needs attention. Backup: "+backup,rollbackError);throw;
   }
  }
  [STAThread] static int Main(string[] args){
   bool recover=args.Length==3&&args[0]=="--rollback";if(args.Length!=2&&!recover)return 2;string requestPath=args[recover?1:0],expectedHash=args[recover?2:1],job=Path.GetDirectoryName(Path.GetFullPath(requestPath));InstallRequest request=null;bool committed=false;
   try{
    if(UpdateContract.Hash(requestPath)!=expectedHash)throw new IOException("Update request changed.");request=UpdateContract.Read<InstallRequest>(requestPath);UpdateContract.Validate(request.Release);UpdateContract.RejectLinks(job);UpdateContract.RejectLinks(request.Package);UpdateContract.RejectLinks(request.Target);
    if(recover){if(Alive(request))throw new IOException("Close File Compass before restoring its files.");committed=true;Recover(request,job);Restart(request.Target);return 0;}
    var watch=Stopwatch.StartNew();while(Alive(request)){if(File.Exists(Path.Combine(job,"cancel.signal")))return 0;if(watch.ElapsedMilliseconds>120000)throw new IOException("File Compass did not exit. No files were changed.");Thread.Sleep(100);}
    if(!File.Exists(Path.Combine(job,"start.signal"))||File.Exists(Path.Combine(job,"cancel.signal")))return 0;committed=true;
    Install(request,job);Restart(request.Target);return 0;
   }catch(Exception ex){File.WriteAllText(Path.Combine(job,"error.txt"),ex.ToString());if(committed){if(!recover&&request!=null&&File.Exists(Path.Combine(job,"journal.json")))try{Recover(request,job);}catch(Exception rollback){File.AppendAllText(Path.Combine(job,"error.txt"),"\r\nRollback: "+rollback);}System.Windows.Forms.MessageBox.Show("File Compass update failed. The previous files were restored when possible.\n\n"+ex.Message+"\n\nDetails: "+job,"File Compass",MessageBoxButtons.OK,MessageBoxIcon.Error);if(request!=null)try{Restart(request.Target);}catch{}}return 1;}
  }
  static void Restart(string target){Process.Start(new ProcessStartInfo(UpdateContract.SafePath(target,UpdateContract.Executable)){WorkingDirectory=target,UseShellExecute=true});}
 }
}
